using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using System.Threading.Tasks;

namespace FACTicket_Scanner
{
    public partial class Form1 : Form
    {
        public  const int Timeout_Dialogos = 10;
        private const string Version = " - 1.38 beta";
        // -----------------------------------------------------------------------
        // Dependencias
        // -----------------------------------------------------------------------
        private readonly CameraManager camara = new CameraManager();
        private readonly AlbumGenerator album;

        // -----------------------------------------------------------------------
        // Estado de captura
        // -----------------------------------------------------------------------
        private AjustesEscaner ajustes = new AjustesEscaner();
        private Mat? fotoCapturada = null;       // foto original cuando el usuario pulsa "Tomar foto"
        private Mat? resultadoProcesado = null;  // resultado procesado actual
        private int rotacionActual = 0;
        private bool modoCaptura = false;        // true = mostrando foto procesada, false = live
        private bool modoSimulado = false;
        private bool guardadoEnCurso = false;
        private event EventHandler? GuardadoTerminado;

        private string NombreCarpeta = "Facturas";
        private string NombreAlbum = "album.html";
        private string NombreDatos = "datos.json";

        // --- Cola de procesado por lotes (carga múltiple de archivos) ---
        private List<string> colaArchivos = new();
        private int indiceColaActual = -1;

        // --- Zoom interactivo sobre la imagen (rueda del ratón) ---
        private float zoomFactor = 1.0f;
        private const float ZOOM_MIN = 1.0f;
        private const float ZOOM_MAX = 8.0f;
        private const float ZOOM_PASO = 1.15f;
        private System.Drawing.Point panOffset = System.Drawing.Point.Empty;
        private bool arrastrandoPan = false;
        private System.Drawing.Point puntoArrastreInicial;
        private System.Drawing.Point panOffsetInicial;

        // --- Controles del panel derecho (siempre visibles) ---
        private Button btnCapturar = null!;
        private Button btnRepetir = null!;
        private Button btnRotar = null!;
        private Button btnSalirLote = null!;
        private Label lblProgresoLote = null!;
        private PanelAjustesEscaneo panelAjustes = null!;
        private PanelRevisionTicket panelRevision = null!;
        private PanelGuardarFactura panelGuardar = null!;

        // --- Autoguardado en lote: cuenta atrás de 5s si no se toca nada ---
        private System.Windows.Forms.Timer? timerAutoGuardarLote;
        private int segundosAutoGuardarLote;

        private Label lblEstado = null!;

        private static void Log(string mensaje)
        {
            try
            {
                string ruta = System.IO.Path.Combine(AppContext.BaseDirectory, "debug_log.txt");
                System.IO.File.AppendAllText(ruta, $"{DateTime.Now:HH:mm:ss.fff} - {mensaje}\r\n");
            }
            catch { }
        }

        // -----------------------------------------------------------------------
        // Iconos como texto Unicode → Bitmap 22×22 (sin dependencia de shell32)
        // -----------------------------------------------------------------------
        private static Image IconoTexto(string emoji, int size = 22)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Segoe UI Emoji", size * 0.75f, GraphicsUnit.Pixel);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(emoji, font, Brushes.Black, new RectangleF(0, 0, size, size), sf);
            return bmp;
        }

        // Asigna iconos emoji a cada opción del menú desplegable.
        private void AsignarIconosMenu()
        {
            abrirToolStripMenuItem.Image = IconoTexto("📂", 16);
            guardarToolStripMenuItem.Image = IconoTexto("💾", 16);
            salirToolStripMenuItem.Image = IconoTexto("🚪", 16);
            camarasIpToolStripMenuItem.Image = IconoTexto("🔌", 16);
            reconectarToolStripMenuItem.Image = IconoTexto("🔍", 16);
            carpetaToolStripMenuItem.Image = IconoTexto("🗂️", 16);
            aboutToolStripMenuItem.Image = IconoTexto("ℹ️", 16);
            logToolStripMenuItem.Image = IconoTexto("📋", 16);

            // Iconos de los botones rápidos del toolbar (declarados en el Designer)
            btnBuscarCamara.Image = IconoTexto("🔍", 22);
            btnReconectarRapido.Image = IconoTexto("🔁", 22);
            btnVisorRapido.Image = IconoTexto("🌐", 22);
            btnCarpetaRapida.Image = IconoTexto("🗂️", 22);
            btnGuardarRapido.Image = IconoTexto("💾", 22);
            btnAbrirRapido.Image = IconoTexto("📂", 22);
        }

        // -----------------------------------------------------------------------
        // Botones de la barra de menú (iconos inline en menuStrip1)
        // -----------------------------------------------------------------------
        // -----------------------------------------------------------------------
        // Inicialización dinámica del toolbar.
        // Los controles (combos, textbox, botones) ya están declarados y
        // colocados en Form1.Designer.cs — aquí solo se rellena lo que NO
        // se puede fijar en tiempo de diseño: opciones del combo, valor
        // inicial dependiente de ajustes guardados, y el ajuste de margen
        // según el ancho real del panel izquierdo.
        // -----------------------------------------------------------------------
        private void ConstruirToolBar()
        {
            cmbTipoCamara.Items.AddRange(new object[] { "📷  USB", "🔌  IP" });
            cmbTipoCamara.SelectedIndex = 0;

            // Texto inicial según tipo por defecto (USB)
            txtUrlCamara.Text = ajustes.UltimoIndiceCamaraUsb >= 0
                ? $"USB Puerto {ajustes.UltimoIndiceCamaraUsb}" : "";

            // Alinear toolbar al borde derecho del panelIzquierdo
            this.Resize += (s, e) => AjustarMargenToolbar();
            this.Load += (s, e) => AjustarMargenToolbar();
        }

        // Botón rápido del toolbar: reconectar sin diálogo (distinto del
        // menú Cámara > Reconectar, que abre selección USB).
        private void BtnReconectarRapido_Click(object? sender, EventArgs e)
        {
            ReconectarUltimaCamara();
        }

        private void AjustarMargenToolbar()
        {
            menuStrip1.Padding = new System.Windows.Forms.Padding(0, 0, this.ClientSize.Width - panelIzquierdo.Width, 0);
        }

        // -----------------------------------------------------------------------
        // Muestra el logo cuando no hay cámara ni imagen activa
        // -----------------------------------------------------------------------
        private void MostrarLogo()
        {
            if (modoCaptura) return;
            try
            {
                string ruta = System.IO.Path.Combine(AppContext.BaseDirectory, "facticket_logo.png");
                if (System.IO.File.Exists(ruta))
                {
                    var bitmapAnterior = pictureBox1.Image;
                    pictureBox1.Image = Image.FromFile(ruta);
                    bitmapAnterior?.Dispose();
                }
            }
            catch { }
        }

        // -----------------------------------------------------------------------
        public Form1()
        {
            InitializeComponent();
            this.Text = "FACTicket Scanner" + Version;
            this.Icon = new System.Drawing.Icon("icono.ico");
            this.WindowState = FormWindowState.Maximized;
            this.MinimumSize = new System.Drawing.Size(800, 600);

            ConstruirToolBar();
            AsignarIconosMenu();
            ConfigurarZoomImagen();

            album = new AlbumGenerator(NombreCarpeta, NombreAlbum, NombreDatos);
            ajustes = album.CargarAjustes();

            // Suscribir eventos de CameraManager
            camara.FrameReady += Camara_FrameReady;
            camara.Conectada += Camara_Conectada;
            camara.Desconectada += Camara_Desconectada;
            camara.ErrorConexion += Camara_ErrorConexion;

            // Ajustar ancho del panel izquierdo al 55% de la pantalla
            this.Load += (s, e) =>
            {
                panelIzquierdo.Width = this.ClientSize.Width * 55 / 100;
                txtUrlCamara.Text = ajustes.UltimaUrlCamaraIp;
                ConstruirPanelIzquierdoLote();
                ConstruirPanelDerecho();
                album.RegenerarAlbumInicial();
                MostrarLogo();
            };

            this.Resize += (s, e) =>
            {
                panelIzquierdo.Width = this.ClientSize.Width * 55 / 100;
                if (zoomFactor > ZOOM_MIN) AplicarZoom();
            };

            //Visualizar tickets al iniciar
            visorToolStripMenuItem_Click(null, null);
        }

        // -----------------------------------------------------------------------
        // Eventos de CameraManager
        // -----------------------------------------------------------------------
        private void Camara_FrameReady(object? sender, Mat frame)
        {
            _ultimoFrame?.Dispose();
            _ultimoFrame = frame.Clone();
            if (modoCaptura) { frame.Dispose(); return; }
            var bitmapAnterior = pictureBox1.Image;
            pictureBox1.Image = ImageProcessor.MatToBitmap(frame);
            bitmapAnterior?.Dispose();
            frame.Dispose();
        }

        private void Camara_Conectada(object? sender, string descripcion)
        {
            if (descripcion == "FILE")
            {
                modoSimulado = true;
                btnCapturar.Visible = false;
                lblEstado.Text = "Modo archivo – sin cámara";
            }
            else
            {
                modoSimulado = false;
                btnCapturar.Text = "📷  Tomar foto";
                btnCapturar.Enabled = true;
                btnCapturar.Visible = true;
                lblEstado.Text = $"✅ Cámara conectada – {descripcion}";
            }
        }

        private void Camara_Desconectada(object? sender, EventArgs e)
        {
            btnCapturar.Visible = false;
            lblEstado.Text = "⚠️ Cámara desconectada";
            MostrarLogo();
            MessageBox.Show("Se perdió la conexión con la cámara.", "Cámara desconectada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Camara_ErrorConexion(object? sender, string mensaje)
        {
            MessageBox.Show($"Error al inicializar la cámara: {mensaje}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblEstado.Text = "Error de cámara";
            btnCapturar.Visible = false;
            MostrarLogo();
        }

        // -----------------------------------------------------------------------
        // Zoom interactivo
        // -----------------------------------------------------------------------
        private void ConfigurarZoomImagen()
        {
            pictureBox1.MouseWheel += PictureBox1_MouseWheel;
            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;
            pictureBox1.MouseDoubleClick += (s, e) => ResetearZoom();
        }

        private void PictureBox1_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            System.Drawing.Point cursorEnPanel = panelIzquierdo.PointToClient(pictureBox1.PointToScreen(e.Location));

            float factorAnterior = zoomFactor;
            float nuevoZoom = e.Delta > 0 ? zoomFactor * ZOOM_PASO : zoomFactor / ZOOM_PASO;
            nuevoZoom = Math.Min(ZOOM_MAX, Math.Max(ZOOM_MIN, nuevoZoom));
            if (Math.Abs(nuevoZoom - factorAnterior) < 0.001f) return;

            float puntoImagenX = (cursorEnPanel.X - panOffset.X) / factorAnterior;
            float puntoImagenY = (cursorEnPanel.Y - panOffset.Y) / factorAnterior;

            zoomFactor = nuevoZoom;

            if (zoomFactor <= ZOOM_MIN + 0.001f)
            {
                ResetearZoom();
                return;
            }

            panOffset = new System.Drawing.Point(
                (int)(cursorEnPanel.X - puntoImagenX * zoomFactor),
                (int)(cursorEnPanel.Y - puntoImagenY * zoomFactor));

            AplicarZoom();
        }

        private void PictureBox1_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || zoomFactor <= ZOOM_MIN) return;
            arrastrandoPan = true;
            puntoArrastreInicial = Cursor.Position;
            panOffsetInicial = panOffset;
            pictureBox1.Cursor = Cursors.SizeAll;
        }

        private void PictureBox1_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!arrastrandoPan) return;
            int dx = Cursor.Position.X - puntoArrastreInicial.X;
            int dy = Cursor.Position.Y - puntoArrastreInicial.Y;
            panOffset = new System.Drawing.Point(panOffsetInicial.X + dx, panOffsetInicial.Y + dy);
            AplicarZoom();
        }

        private void PictureBox1_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            arrastrandoPan = false;
            pictureBox1.Cursor = Cursors.Default;
        }

        private void AplicarZoom()
        {
            if (pictureBox1.Image == null) return;

            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

            var (baseW, baseH) = CalcularTamanoAjustado(pictureBox1.Image.Size, pictureBox1.Size);
            int w = (int)(baseW * zoomFactor);
            int h = (int)(baseH * zoomFactor);

            pictureBox1.Dock = DockStyle.None;
            pictureBox1.Size = new System.Drawing.Size(w, h);
            pictureBox1.Location = panOffset;
        }

        private static (int, int) CalcularTamanoAjustado(System.Drawing.Size imagen, System.Drawing.Size contenedor)
        {
            if (imagen.Width == 0 || imagen.Height == 0 || contenedor.Width == 0 || contenedor.Height == 0)
                return (contenedor.Width, contenedor.Height);

            double escala = Math.Min((double)contenedor.Width / imagen.Width, (double)contenedor.Height / imagen.Height);
            return ((int)(imagen.Width * escala), (int)(imagen.Height * escala));
        }

        private void ResetearZoom()
        {
            zoomFactor = 1.0f;
            panOffset = System.Drawing.Point.Empty;
            arrastrandoPan = false;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.Dock = DockStyle.Fill;
        }

        // -----------------------------------------------------------------------
        // Autoguardado en lote: si tras cargar una imagen del lote no se toca
        // nada (sliders, checkboxes, rotar, repetir) durante 5s, se pulsa
        // Guardar automáticamente. El botón muestra la cuenta atrás.
        // -----------------------------------------------------------------------
        private void IniciarAutoGuardadoLote()
        {
            CancelarAutoGuardadoLote();
            if (colaArchivos.Count <= 1) return; // solo en lote real (>1 imagen)

            segundosAutoGuardarLote = 5;
            ActualizarTextoAutoGuardado();

            timerAutoGuardarLote = new System.Windows.Forms.Timer { Interval = 1000 };
            timerAutoGuardarLote.Tick += (s, e) =>
            {
                segundosAutoGuardarLote--;
                if (segundosAutoGuardarLote <= 0)
                {
                    CancelarAutoGuardadoLote();
                    if (panelGuardar.btnGuardar.Enabled) panelGuardar.btnGuardar.PerformClick();
                    return;
                }
                ActualizarTextoAutoGuardado();
            };
            timerAutoGuardarLote.Start();
        }

        private void ActualizarTextoAutoGuardado()
        {
            panelGuardar.btnGuardar.Text = $"💾  Guardar (auto en {segundosAutoGuardarLote}s)";
        }

        private void CancelarAutoGuardadoLote()
        {
            if (timerAutoGuardarLote == null) return;
            timerAutoGuardarLote.Stop();
            timerAutoGuardarLote.Dispose();
            timerAutoGuardarLote = null;
            panelGuardar.btnGuardar.Text = "💾  Guardar";
        }

        // -----------------------------------------------------------------------
        // Grupo "Imagen X/Y" + "Salir del lote", anclado abajo a la derecha
        // de panelIzquierdo (encima de él vive pictureBox1). Ambos controles
        // usan Anchor=Right para mantenerse pegados al borde derecho aunque
        // panelIzquierdo cambie de ancho.
        // -----------------------------------------------------------------------
        private void ConstruirPanelIzquierdoLote()
        {
            var panelLote = new Panel { Dock = DockStyle.Bottom, Height = 36 };
            panelIzquierdo.Controls.Add(panelLote);

            const int wBtn = 150, wLbl = 110, margen = 8;

            btnSalirLote = new Button
            {
                Text = "✖  Salir del lote",
                Width = wBtn,
                Height = 28,
                Top = 4,
                Left = panelLote.ClientSize.Width - wBtn - margen,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = false,
                BackColor = System.Drawing.Color.IndianRed,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSalirLote.Click += BtnSalirLote_Click;
            panelLote.Controls.Add(btnSalirLote);

            lblProgresoLote = new Label
            {
                Text = "",
                Width = wLbl,
                Height = 28,
                Top = 4,
                Left = btnSalirLote.Left - wLbl - margen,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.SteelBlue,
                Visible = false
            };
            panelLote.Controls.Add(lblProgresoLote);
        }

        // -----------------------------------------------------------------------
        // Construye los controles del panel derecho:
        //   panelScrollable → sliders (con scroll)
        //   panelBotones    → botones + estado (fijo abajo)
        // -----------------------------------------------------------------------
        private void ConstruirPanelDerecho()
        {
            panelScrollable.Controls.Clear();
            panelBotones.Controls.Clear();

            int wTotal = panelScrollable.ClientSize.Width - 16;
            if (wTotal < 200) wTotal = 200;

            // ── AJUSTES (panelScrollable) ────────────────────────────────────
            // Sliders y revisión de ticket viven en UserControls propios,
            // apilados en el mismo hueco: panelAjustes se ve desde el arranque
            // (antes de cargar ninguna imagen); panelRevision se muestra en su
            // lugar solo mientras se revisan los datos extraídos por Gemini.
            panelAjustes = new PanelAjustesEscaneo { Dock = DockStyle.Fill };
            panelAjustes.ValorCambiado += (s, e) => { CancelarAutoGuardadoLote(); if (modoCaptura) Reprocesar(); };
            panelScrollable.Controls.Add(panelAjustes);

            panelRevision = new PanelRevisionTicket { Dock = DockStyle.Fill, Visible = false };
            panelScrollable.Controls.Add(panelRevision);
            panelRevision.BringToFront();

            // ── BOTONES (panelBotones, fijo abajo) ──────────────────────────
            int wP = panelBotones.ClientSize.Width - 16;
            if (wP < 200) wP = 200;

            // Separador superior
            panelBotones.Controls.Add(new Label
            {
                Left = 0,
                Top = 4,
                Width = wP,
                Height = 1,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.Silver
            });

            // Panel de Guardar: fila Rotar/Repetir/Guardar + fila de checkboxes
            panelGuardar = new PanelGuardarFactura { Left = 0, Top = 12, Width = wP, Height = 84 };
            panelGuardar.btnRotar.Click += (s, e) => { CancelarAutoGuardadoLote(); rotacionActual = (rotacionActual + 90) % 360; Reprocesar(); };
            panelGuardar.btnRepetir.Click += BtnRepetir_Click;
            panelGuardar.btnGuardar.Click += BtnGuardar_Click;
            btnRotar = panelGuardar.btnRotar;
            btnRepetir = panelGuardar.btnRepetir;
            panelBotones.Controls.Add(panelGuardar);

            panelGuardar.chkGuardarOriginal.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();
            panelGuardar.chkGuardarJpg.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();
            panelGuardar.chkGuardarPdf.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();
            panelGuardar.chkExtraerGemini.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();

            // Separador
            panelBotones.Controls.Add(new Label
            {
                Left = 0,
                Top = 104,
                Width = wP,
                Height = 1,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.Silver
            });

            // Fila: [lblEstado] [btnCapturar]
            lblEstado = new Label
            {
                Left = 0,
                Top = 112,
                Width = wP - 160,
                Height = 36,
                Text = "Sin cámara – usa Configuracion > Camara",
                Font = new System.Drawing.Font(Font.FontFamily, 11, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.DarkSlateBlue,
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            panelBotones.Controls.Add(lblEstado);

            btnCapturar = new Button
            {
                Left = wP - 152,
                Top = 108,
                Width = 152,
                Height = 40,
                Text = "📷  Tomar foto",
                BackColor = System.Drawing.Color.SteelBlue,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font(Font.FontFamily, 10, System.Drawing.FontStyle.Bold),
                Visible = false   // solo visible cuando hay cámara real
            };
            btnCapturar.Click += BtnCapturar_Click;
            panelBotones.Controls.Add(btnCapturar);

            // Ajustar altura del panelBotones según contenido
            panelBotones.Height = btnCapturar.Bottom + 8;
        }

        // -----------------------------------------------------------------------
        // Reprocesa la foto capturada y actualiza el PictureBox en tiempo real
        // -----------------------------------------------------------------------
        private void Reprocesar()
        {
            if (!modoCaptura || fotoCapturada == null) return;
            Log("Reprocesar: inicio");

            resultadoProcesado?.Dispose();
            Log("Reprocesar: llamando ProcesarImagen");

            resultadoProcesado = ImageProcessor.ProcesarImagen(fotoCapturada, rotacionActual,
                panelAjustes.trkBlock.Value * 2 + 1, panelAjustes.trkC.Value,
                panelAjustes.trkRuido.Value, panelAjustes.trkNitidez.Value, panelAjustes.trkGrueso.Value,
                panelAjustes.trkContraste.Value, panelAjustes.trkBrillo.Value,
                panelAjustes.trkUmbral.Value, panelAjustes.trkMargen.Value,
                panelAjustes.chkEdicionManual.Checked,
                panelAjustes.trkMargenSup.Value, panelAjustes.trkMargenInf.Value,
                panelAjustes.trkMargenIzq.Value, panelAjustes.trkMargenDer.Value);
            Log("Reprocesar: ProcesarImagen OK");

            var bitmapAnterior = pictureBox1.Image;
            Log("Reprocesar: llamando MatToBitmap");
            pictureBox1.Image = ImageProcessor.MatToBitmap(resultadoProcesado);
            Log("Reprocesar: MatToBitmap OK");
            bitmapAnterior?.Dispose();
            if (zoomFactor > ZOOM_MIN) AplicarZoom();
            Log("Reprocesar: fin");
        }

        // -----------------------------------------------------------------------
        // Botón Tomar foto
        // -----------------------------------------------------------------------
        private void BtnCapturar_Click(object? sender, EventArgs e)
        {
            Log("BtnCapturar_Click: inicio");

            try
            {
                if (!camara.EstaConectada)
                {
                    MessageBox.Show("No hay imagen disponible.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Log("BtnCapturar_Click: parando timer");
                camara.PausarTimer();

                Log("BtnCapturar_Click: clonando frame");
                fotoCapturada?.Dispose();
                fotoCapturada = camara.CapturarFrame(GetLastFrame());
                if (fotoCapturada == null || fotoCapturada.Empty())
                {
                    MessageBox.Show("No hay imagen disponible.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    camara.ReanudarTimer();
                    return;
                }

                rotacionActual = 0;
                Log("BtnCapturar_Click: frame clonado OK, size=" + fotoCapturada.Size());

                Log("BtnCapturar_Click: llamando CalcularAjustesAutomaticos");
                var (autoContraste, autoBrillo, autoRuido) = ImageProcessor.CalcularAjustesAutomaticos(fotoCapturada);
                Log("BtnCapturar_Click: CalcularAjustesAutomaticos OK");
                panelAjustes.trkContraste.Value = Math.Min(panelAjustes.trkContraste.Maximum, Math.Max(panelAjustes.trkContraste.Minimum, autoContraste));
                panelAjustes.trkBrillo.Value = Math.Min(panelAjustes.trkBrillo.Maximum, Math.Max(panelAjustes.trkBrillo.Minimum, autoBrillo));
                panelAjustes.trkRuido.Value = Math.Min(panelAjustes.trkRuido.Maximum, Math.Max(panelAjustes.trkRuido.Minimum, autoRuido + 1));

                panelAjustes.trkBlock.Value = 25;
                panelAjustes.trkC.Value = 10;
                panelAjustes.trkNitidez.Value = 1;
                panelAjustes.trkGrueso.Value = 0;
                panelAjustes.trkUmbral.Value = 10;
                panelAjustes.trkMargen.Value = 5;
                panelAjustes.trkMargenSup.Value = 0;
                panelAjustes.trkMargenInf.Value = 0;
                panelAjustes.trkMargenIzq.Value = 0;
                panelAjustes.trkMargenDer.Value = 0;
                Log("BtnCapturar_Click: sliders reseteados OK");

                modoCaptura = true;
                panelGuardar.btnGuardar.Enabled = true;
                btnRepetir.Enabled = true;
                btnRotar.Enabled = true;
                btnCapturar.Enabled = false;
                lblEstado.Text = "📸 Foto capturada – ajusta y pulsa Guardar";

                Log("BtnCapturar_Click: llamando Reprocesar");
                Reprocesar();
                Log("BtnCapturar_Click: Reprocesar OK - fin");
            }
            catch (Exception ex)
            {
                Log("BtnCapturar_Click: EXCEPCION -> " + ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                VolverALive();
            }
        }

        // Último frame recibido por FrameReady
        private Mat? _ultimoFrame = null;
        private Mat GetLastFrame() => _ultimoFrame ?? new Mat();

        // -----------------------------------------------------------------------
        // Botón Repetir: vuelve al live
        // -----------------------------------------------------------------------
        private void BtnRepetir_Click(object? sender, EventArgs e)
        {
            CancelarAutoGuardadoLote();
            VolverALive();
        }

        // -----------------------------------------------------------------------
        // FIX: VolverALive
        // -----------------------------------------------------------------------
        private void VolverALive()
        {
            LimpiarImagenActual();

            if (modoSimulado)
            {
                lblEstado.Text = "📂  Cargar imagen desde 📂 del menú";
                return;
            }

            camara.ResetearFallos();
            camara.ReanudarTimer();

            if (camara.EstaConectada)
            {
                string tipoStr = int.TryParse(camara.FuenteActual, out _)
                    ? $"USB ({camara.FuenteActual})" : $"IP: {camara.FuenteActual}";
                lblEstado.Text = $"✅ Cámara conectada – {tipoStr}";
            }
            else
            {
                lblEstado.Text = "⚠️ Cámara desconectada – reconecta desde Configuracion";
            }
        }

        // -----------------------------------------------------------------------
        // Libera la imagen/resultado actual y resetea los botones
        // -----------------------------------------------------------------------
        private void LimpiarImagenActual()
        {
            CancelarAutoGuardadoLote();
            modoCaptura = false;
            fotoCapturada?.Dispose();
            fotoCapturada = null;
            resultadoProcesado?.Dispose();
            resultadoProcesado = null;

            panelGuardar.btnGuardar.Enabled = false;
            btnRepetir.Enabled = false;
            btnRotar.Enabled = false;
            btnCapturar.Enabled = true;

            var bitmapAnterior = pictureBox1.Image;
            pictureBox1.Image = null;
            bitmapAnterior?.Dispose();
            ResetearZoom();
        }

        // -----------------------------------------------------------------------
        // Botón Guardar
        // -----------------------------------------------------------------------
        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            CancelarAutoGuardadoLote();
            if (resultadoProcesado == null || fotoCapturada == null) return;
            Mat copiaImg = resultadoProcesado.Clone();
            Mat copiaOriginal = fotoCapturada.Clone();
            int rot = rotacionActual;

            bool perteneceALote = indiceColaActual >= 0 && indiceColaActual < colaArchivos.Count;

            panelGuardar.btnGuardar.Enabled = false;
            btnRotar.Enabled = false;
            btnRepetir.Enabled = false;
            btnCapturar.Enabled = false;
            this.UseWaitCursor = true;

            guardadoEnCurso = true;
            album.GuardarImagen(copiaImg, copiaOriginal, rot, ajustes,
                panelGuardar.chkGuardarOriginal.Checked, panelGuardar.chkGuardarJpg.Checked,
                panelGuardar.chkGuardarPdf.Checked, panelGuardar.chkExtraerGemini.Checked,
                a => album.GuardarAjustes(a),
                msg => { lblEstado.Text = msg; },
                () => { this.UseWaitCursor = false; btnCapturar.Enabled = true; },
                () =>
                {
                    if (perteneceALote) LimpiarImagenActual();
                    else VolverALive();

                    GuardadoTerminado?.Invoke(this, EventArgs.Empty);
                    guardadoEnCurso = false;
                    if (perteneceALote) CargarSiguienteDeCola();
                },
                MostrarRevisionEmbebida);
        }

        // -----------------------------------------------------------------------
        // Muestra el panel de revisión de datos (Gemini) dentro del hueco de
        // panelScrollable, ocultando temporalmente los sliders. Se resuelve
        // cuando el usuario pulsa Guardar/Cancelar o expira la cuenta atrás.
        // -----------------------------------------------------------------------
        private System.Threading.Tasks.Task<DatosTicket?> MostrarRevisionEmbebida(DatosTicket datos)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<DatosTicket?>();

            panelAjustes.Visible = false;
            panelRevision.Visible = true;

            void OnCompletada(object? s, RevisionCompletadaEventArgs e)
            {
                panelRevision.RevisionCompletada -= OnCompletada;
                panelRevision.Visible = false;
                panelAjustes.Visible = true;
                tcs.TrySetResult(e.Resultado);
            }

            panelRevision.RevisionCompletada += OnCompletada;
            panelRevision.Mostrar(datos);

            return tcs.Task;
        }

        // -----------------------------------------------------------------------
        // Permite elegir una o varias imágenes a la vez
        // -----------------------------------------------------------------------
        private void ProcesarDesdeArchivo()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            colaArchivos = dlg.FileNames.ToList();
            indiceColaActual = -1;
            CargarSiguienteDeCola();
        }

        // -----------------------------------------------------------------------
        // Carga la siguiente imagen pendiente de la cola de lote
        // -----------------------------------------------------------------------
        private void CargarSiguienteDeCola()
        {
            indiceColaActual++;

            if (indiceColaActual >= colaArchivos.Count)
            {
                bool eraLoteMultiple = colaArchivos.Count > 1;
                colaArchivos.Clear();
                indiceColaActual = -1;
                ActualizarVisibilidadLote();
                VolverALive();
                if (eraLoteMultiple)
                    lblEstado.Text = "✅ Lote completado – " + lblEstado.Text;
                return;
            }

            string ruta = colaArchivos[indiceColaActual];
            try
            {
                Mat img = Cv2.ImRead(ruta);
                if (img.Empty())
                {
                    DialogoAutoConfirmar.Aviso($"No se pudo leer la imagen:\n{ruta}", "Error");
                    CargarSiguienteDeCola();
                    return;
                }

                var duplicado = album.BuscarDuplicadoPorPHash(img, out int distanciaPHash);
                if (duplicado != null)
                {
                    string resumen =
                        $"Empresa: {duplicado.Empresa}\n" +
                        $"Nº Factura: {(string.IsNullOrWhiteSpace(duplicado.Numero) ? "(sin número)" : duplicado.Numero)}\n" +
                        $"Fecha: {duplicado.Fecha}\n" +
                        $"Total: {duplicado.Total}\n" +
                        $"Guardada el: {duplicado.FechaGuardado}\n" +
                        $"Coincidencia: {63 - distanciaPHash}/63 bits";

                    bool continuar = DialogoAutoConfirmar.Confirmar(
                        $"Esta imagen parece coincidir con una factura ya escaneada:\n\n{resumen}\n\n¿Continuar de todos modos?",
                        "Posible imagen duplicada", resultadoPorDefecto: true);

                    if (!continuar)
                    {
                        img.Dispose();
                        CargarSiguienteDeCola();
                        return;
                    }
                }

                fotoCapturada?.Dispose();
                fotoCapturada = img;
                rotacionActual = 0;
                modoCaptura = true;
                ResetearZoom();

                var (autoContraste, autoBrillo, autoRuido) = ImageProcessor.CalcularAjustesAutomaticos(fotoCapturada);
                panelAjustes.trkContraste.Value = Math.Min(panelAjustes.trkContraste.Maximum, Math.Max(panelAjustes.trkContraste.Minimum, autoContraste));
                panelAjustes.trkBrillo.Value = Math.Min(panelAjustes.trkBrillo.Maximum, Math.Max(panelAjustes.trkBrillo.Minimum, autoBrillo));
                panelAjustes.trkRuido.Value = Math.Min(panelAjustes.trkRuido.Maximum, Math.Max(panelAjustes.trkRuido.Minimum, autoRuido + 1));
                panelAjustes.trkNitidez.Value = 1;
                panelAjustes.trkUmbral.Value = 10;

                panelGuardar.btnGuardar.Enabled = true;
                btnRepetir.Enabled = true;
                btnRotar.Enabled = true;
                btnCapturar.Enabled = false;

                ActualizarVisibilidadLote();
                lblEstado.Text = colaArchivos.Count > 1
                    ? "📂 Ajusta la imagen y pulsa Guardar"
                    : "📂 Imagen cargada – ajusta y pulsa Guardar";

                Reprocesar();
                IniciarAutoGuardadoLote();
            }
            catch (Exception ex)
            {
                DialogoAutoConfirmar.Aviso($"Error: {ex.Message}", "Error");
                CargarSiguienteDeCola();
            }
        }

        // -----------------------------------------------------------------------
        // Muestra/oculta el contador y el botón "Salir del lote"
        // -----------------------------------------------------------------------
        private void ActualizarVisibilidadLote()
        {
            bool enLote = colaArchivos.Count > 1 && indiceColaActual >= 0 && indiceColaActual < colaArchivos.Count;
            lblProgresoLote.Visible = enLote;
            btnSalirLote.Visible = enLote;
            if (enLote)
                lblProgresoLote.Text = $"Imagen {indiceColaActual + 1}/{colaArchivos.Count}";
        }

        // -----------------------------------------------------------------------
        // Botón "Salir del lote"
        // -----------------------------------------------------------------------
        private void BtnSalirLote_Click(object? sender, EventArgs e)
        {
            if (colaArchivos.Count == 0) return;
            CancelarAutoGuardadoLote();

            var resultado = MessageBox.Show(
                "Vas a salir del lote. La imagen actual no se guardará y se descartarán las imágenes pendientes.\n\n¿Continuar?",
                "Salir del lote", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (resultado != DialogResult.Yes) return;

            colaArchivos.Clear();
            indiceColaActual = -1;
            ActualizarVisibilidadLote();
            VolverALive();
        }

        // -----------------------------------------------------------------------
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            camara.Dispose();
            fotoCapturada?.Dispose();
            resultadoProcesado?.Dispose();
            base.OnFormClosing(e);
        }

        // -----------------------------------------------------------------------
        // Toolbar: cambio de tipo cámara (USB / IP) → limpia resultados
        // -----------------------------------------------------------------------
        // -----------------------------------------------------------------------
        // Toolbar: reconectar última cámara usada
        // -----------------------------------------------------------------------
        private void ReconectarUltimaCamara()
        {
            if (string.IsNullOrEmpty(ajustes.UltimoTipoCamara)) return;
            camara.ConectarCamaraRecordada(ajustes);
        }

        private void CmbTipoCamara_SelectedIndexChanged(object? sender, EventArgs e)
        {
            bool esIp = cmbTipoCamara.SelectedIndex == 1;
            cmbResultadoCamara.Items.Clear();
            cmbResultadoCamara.Text = "";
            txtUrlCamara.Text = esIp ? ajustes.UltimaUrlCamaraIp
                                     : (ajustes.UltimoIndiceCamaraUsb >= 0 ? $"USB Puerto {ajustes.UltimoIndiceCamaraUsb}" : "");
        }

        // -----------------------------------------------------------------------
        // Toolbar: botón buscar → delega en CameraManager
        // -----------------------------------------------------------------------
        private async void BtnBuscarCamara_Click(object? sender, EventArgs e)
        {
            bool esUsb = cmbTipoCamara.SelectedIndex == 0;
            cmbResultadoCamara.Items.Clear();
            btnBuscarCamara.Enabled = false;
            btnBuscarCamara.ToolTipText = "Buscando...";

            if (esUsb)
            {
                var puertos = await Task.Run(() => camara.DetectarCamarasUsb());
                foreach (int p in puertos)
                    cmbResultadoCamara.Items.Add($"Puerto {p}");
            }
            else
            {
                var ips = await camara.EscanearCamarasIpAsync();
                foreach (string ip in ips)
                    cmbResultadoCamara.Items.Add(ip);
            }

            btnBuscarCamara.Enabled = true;
            btnBuscarCamara.ToolTipText = "Buscar cámaras";

            if (cmbResultadoCamara.Items.Count > 0)
                cmbResultadoCamara.SelectedIndex = 0;
            else
                MessageBox.Show(esUsb ? "No se encontraron cámaras USB." : "No se encontraron cámaras IP en la red.",
                    "Búsqueda", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // -----------------------------------------------------------------------
        // Toolbar: selección de resultado → conectar auto vía CameraManager
        // -----------------------------------------------------------------------
        private void CmbResultadoCamara_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbResultadoCamara.SelectedItem == null) return;
            bool esUsb = cmbTipoCamara.SelectedIndex == 0;
            string valor = cmbResultadoCamara.SelectedItem.ToString()!;

            if (esUsb)
            {
                if (int.TryParse(valor.Replace("Puerto ", ""), out int puerto))
                {
                    txtUrlCamara.Text = $"USB Puerto {puerto}";
                    camara.ConectarUsb(puerto, ajustes, a => album.GuardarAjustes(a));
                }
            }
            else
            {
                string url = $"http://{valor}:8080/video";
                txtUrlCamara.Text = url;
                camara.ConectarIp(url, ajustes, a => album.GuardarAjustes(a));
            }
        }

        private void camaraToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camara.IniciarSeleccionUsb(ajustes, this, a => album.GuardarAjustes(a));
        }

        private async void visorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string rutaHtml = Path.Combine(Application.StartupPath, NombreCarpeta, NombreAlbum);

                if (!File.Exists(rutaHtml))
                {
                    MessageBox.Show("No se encontró el archivo del ticket.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await webViewAlbum.EnsureCoreWebView2Async();
                webViewAlbum.CoreWebView2.Navigate(new Uri(rutaHtml).AbsoluteUri);
                panelIzquierdo.Visible = false;
                panelDerecho.Visible = false;
                panelVisor.Visible = true;
                panelVisor.BringToFront();
                btnCerrarVisor.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el visor de tickets:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // -----------------------------------------------------------------------
        // Visor web: botón ✕ → cierra y vuelve a la pantalla principal
        // -----------------------------------------------------------------------
        private void btnCerrarVisor_Click(object? sender, EventArgs e)
        {
            panelVisor.Visible = false;
            panelIzquierdo.Visible = true;
            panelDerecho.Visible = true;
            btnCerrarVisor.Visible = false;
        }

        // -----------------------------------------------------------------------
        // Menú: Archivo > Abrir
        // -----------------------------------------------------------------------
        private void abrirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProcesarDesdeArchivo();
        }

        // -----------------------------------------------------------------------
        // Menú: Archivo > Guardar
        // -----------------------------------------------------------------------
        private void guardarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BtnGuardar_Click(sender, e);
        }

        // -----------------------------------------------------------------------
        // Menú: Archivo > Salir
        // -----------------------------------------------------------------------
        private void salirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        // -----------------------------------------------------------------------
        // Menú: Configuración > Cámaras IP
        // -----------------------------------------------------------------------
        private void camarasIpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camara.IniciarSeleccionIp(ajustes, this, a => album.GuardarAjustes(a));
        }

        // -----------------------------------------------------------------------
        // Menú: Configuración > Reconectar
        // -----------------------------------------------------------------------
        private void reconectarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camara.IniciarSeleccionUsb(ajustes, this, a => album.GuardarAjustes(a));
        }

        // -----------------------------------------------------------------------
        // Menú: Ver > Carpeta de facturas
        // -----------------------------------------------------------------------
        private void carpetaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string rutaCarpeta = Path.Combine(Application.StartupPath, NombreCarpeta);

                if (!Directory.Exists(rutaCarpeta))
                {
                    MessageBox.Show("No se encontró la carpeta de facturas.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo { FileName = rutaCarpeta, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir la carpeta de facturas:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void editarClavesAPIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GeminiAPI.AbrirGestionApis(this);
        }
        // -----------------------------------------------------------------------
        // Menú: Ayuda > Acerca de
        // -----------------------------------------------------------------------
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "FACTicket Scanner\nVersión 1.0\n\nAplicación para escanear, procesar y archivar tickets/facturas.",
                "Acerca de",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // -----------------------------------------------------------------------
        // Menú: Ayuda > Ver log
        // -----------------------------------------------------------------------
        private void logToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string rutaLog = Path.Combine(AppContext.BaseDirectory, "debug_log.txt");

                if (!File.Exists(rutaLog))
                {
                    MessageBox.Show("No se encontró el archivo de log.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo { FileName = rutaLog, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el log:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void conversorIMGPDFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var conversor = new Conversor_IMG_PDF();
            conversor.ShowDialog(this);
            conversor.Dispose();
        }

        private void analizarPhashDeTodasLasFacturasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lblEstado.Text = "Iniciando escaneo de pHash...";
            album.EscanearPHashFacturas(msg => lblEstado.Text = msg);
        }
        private void exportarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var form = new ExportarForm();
            form.ShowDialog(this);
        }
    }
}