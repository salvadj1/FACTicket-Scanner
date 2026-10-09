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
        public const int Timeout_Dialogos = 5;
        private const string Version = " - 1.80 beta";
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
        private int ultimaRotacion = 0;      // última rotación elegida por el usuario; se aplica a las siguientes imágenes cargadas
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
        private string? rutaJsonEdicionActual = null; // != null mientras se edita una factura desde el visor

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
        private PanelAjustesEscaneo panelAjustes = null!;
        private PanelRevisionTicket panelRevision = null!;
        private PanelGuardarFactura panelGuardar = null!;

        // --- Autoguardado en lote: cuenta atrás de 5s si no se toca nada ---
        private System.Windows.Forms.Timer? timerAutoGuardarLote;
        private ParpadeoControl? parpadeoAutoGuardarLote;   // el botón Guardar parpadea durante la cuenta atrás del lote
        private int segundosAutoGuardarLote;

        private Label lblEstado = null!;
        private Label separadorInferior = null!;   // línea bajo panelGuardar (se reubica con su alto)

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
            carpetaToolStripMenuItem.Image = IconoTexto("🗂️", 16);
            aboutToolStripMenuItem.Image = IconoTexto("ℹ️", 16);
            logToolStripMenuItem.Image = IconoTexto("📋", 16);
            exportarToolStripMenuItem.Image = IconoTexto("📤", 16);
            importarToolStripMenuItem.Image = IconoTexto("📥", 16);
            desdeCamaraToolStripMenuItem.Image = IconoTexto("📷", 16);
            visorToolStripMenuItem.Image = IconoTexto("🌐", 16);
            conversorIMGPDFToolStripMenuItem.Image = IconoTexto("🖼️", 16);
            analizarPhashDeTodasLasFacturasToolStripMenuItem.Image = IconoTexto("🔎", 16);
            editarClavesAPIToolStripMenuItem.Image = IconoTexto("🔑", 16);

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
            ConstruirBotonRecargarVisor();
            AsignarIconosMenu();
            ConfigurarZoomImagen();

            album = new AlbumGenerator(NombreCarpeta, NombreAlbum, NombreDatos);
            ajustes = album.CargarAjustes();
            AplicarAjustesGlobales();

            // Suscribir eventos de CameraManager
            camara.FrameReady += Camara_FrameReady;
            camara.Conectada += Camara_Conectada;
            camara.Desconectada += Camara_Desconectada;
            camara.ErrorConexion += Camara_ErrorConexion;

            // Ajustar ancho del panel izquierdo al 55% de la pantalla
            this.Load += (s, e) =>
            {
                panelIzquierdo.Width = this.ClientSize.Width * 55 / 100;
                // Tipo de cámara por defecto (Ajustes > Cámara): preselecciona IP si era la recordada.
                if (ajustes.UltimoTipoCamara == "IP") cmbTipoCamara.SelectedIndex = 1;
                txtUrlCamara.Text = ajustes.UltimaUrlCamaraIp;
                ConstruirPanelDerecho();
                album.RegenerarAlbumInicial();
                MostrarLogo();
                // Ajustes > Cámara > "Reconectar la última al iniciar"
                if (ajustes.ReconectarCamaraAlIniciar) ReconectarUltimaCamara();
            };

            this.Resize += (s, e) =>
            {
                panelIzquierdo.Width = this.ClientSize.Width * 55 / 100;
                if (zoomFactor > ZOOM_MIN) AplicarZoom();
            };

            // Visualizar tickets al iniciar (Ajustes > General > "Abrir el visor web al iniciar")
            if (ajustes.AbrirVisorAlIniciar) visorToolStripMenuItem_Click(null, null);
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
            if (!ajustes.AutoguardadoLote) return; // desactivado en Ajustes > Escaneo
            if (colaArchivos.Count <= 1) return; // solo en lote real (>1 imagen)

            segundosAutoGuardarLote = Math.Max(1, ajustes.SegundosCuentaAtras);
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

            // El botón Guardar parpadea (verde <-> naranja) mientras dura la cuenta atrás
            parpadeoAutoGuardarLote = new ParpadeoControl(panelGuardar.btnGuardar, System.Drawing.Color.Orange, 350);
            parpadeoAutoGuardarLote.Iniciar();
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
            parpadeoAutoGuardarLote?.Dispose();    // detiene el parpadeo y restaura los colores
            parpadeoAutoGuardarLote = null;
            panelGuardar.btnGuardar.Text = "💾  Guardar";
        }

        // -----------------------------------------------------------------------
        // Grupo "Imagen X/Y" + "Salir del lote", anclado abajo a la derecha
        // de panelIzquierdo (encima de él vive pictureBox1). Ambos controles
        // usan Anchor=Right para mantenerse pegados al borde derecho aunque
        // panelIzquierdo cambie de ancho.
        // -----------------------------------------------------------------------
        // NOTA: btnSalirLote y lblProgresoLote ahora viven dentro de
        // PanelGuardarFactura (panelGuardar.btnSalirLote / .lblProgresoLote),
        // junto a btnSaltar y btnCancelarAuto.

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
            panelAjustes.ValorCambiado += (s, e) => { Log($"ValorCambiado: modoCaptura={modoCaptura}"); CancelarAutoGuardadoLote(); if (modoCaptura) Reprocesar(); };
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
            panelGuardar = new PanelGuardarFactura { Left = 0, Top = 12, Width = wP };
            panelGuardar.btnRotar.Click += (s, e) => { CancelarAutoGuardadoLote(); rotacionActual = (rotacionActual + 90) % 360; ultimaRotacion = rotacionActual; Reprocesar(); };
            panelGuardar.btnRepetir.Click += BtnRepetir_Click;
            panelGuardar.btnGuardar.Click += BtnGuardar_Click;
            panelGuardar.btnCancelarAuto.Click += (s, e) => CancelarAutoGuardadoLote();
            panelGuardar.btnSaltar.Click += (s, e) => { CancelarAutoGuardadoLote(); CargarSiguienteDeCola(); };
            panelGuardar.btnSalirLote.Click += BtnSalirLote_Click;
            btnRotar = panelGuardar.btnRotar;
            btnRepetir = panelGuardar.btnRepetir;
            panelBotones.Controls.Add(panelGuardar);

            panelGuardar.chkGuardarOriginal.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();
            panelGuardar.chkGuardarJpg.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();
            panelGuardar.chkGuardarPdf.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();
            panelGuardar.chkExtraerGemini.CheckedChanged += (s, e) => CancelarAutoGuardadoLote();

            // Reglas de verificación de duplicados: se cargan de ajustes.json y se
            // guardan al cambiarlas.
            panelGuardar.AplicarReglasDuplicados(new ReglasDuplicados
            {
                Numero = ajustes.DupNumero,
                Fecha = ajustes.DupFecha,
                Total = ajustes.DupTotal,
                Empresa = ajustes.DupEmpresa
            });
            panelGuardar.ReglasCambiadas += (s, e) =>
            {
                CancelarAutoGuardadoLote();
                var r = panelGuardar.ObtenerReglasDuplicados();
                ajustes.DupNumero = r.Numero;
                ajustes.DupFecha = r.Fecha;
                ajustes.DupTotal = r.Total;
                ajustes.DupEmpresa = r.Empresa;
                album.GuardarAjustes(ajustes);
            };

            // Separador
            separadorInferior = new Label
            {
                Left = 0,
                Top = 104,
                Width = wP,
                Height = 1,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.Silver
            };
            panelBotones.Controls.Add(separadorInferior);

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

            // Ajustar posiciones y altura del panelBotones según contenido
            ReubicarPanelBotones();
        }

        // -----------------------------------------------------------------------
        // Recoloca separador, estado y botón "Tomar foto" bajo panelGuardar y
        // ajusta el alto de panelBotones según el alto de panelGuardar.
        // -----------------------------------------------------------------------
        private void ReubicarPanelBotones()
        {
            if (panelGuardar == null || separadorInferior == null || lblEstado == null || btnCapturar == null) return;
            int y = panelGuardar.Bottom + 8;
            separadorInferior.Top = y;
            lblEstado.Top = y + 8;
            btnCapturar.Top = y + 4;
            panelBotones.Height = btnCapturar.Bottom + 8;
        }

        // -----------------------------------------------------------------------
        // Reprocesa la foto capturada y actualiza el PictureBox en tiempo real
        // -----------------------------------------------------------------------
        private void Reprocesar()
        {
            Log($"Reprocesar: llamado - modoCaptura={modoCaptura} fotoCapturada={(fotoCapturada == null ? "null" : "OK")}");
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

                rotacionActual = ultimaRotacion; // recuerda la última rotación usada
                Log("BtnCapturar_Click: frame clonado OK, size=" + fotoCapturada.Size());

                Log("BtnCapturar_Click: llamando CalcularAjustesAutomaticos");
                var (autoContraste, autoBrillo, autoRuido) = ObtenerAjustesAutomaticos(fotoCapturada);
                Log("BtnCapturar_Click: CalcularAjustesAutomaticos OK");
                panelAjustes.trkContraste.Value = Math.Min(panelAjustes.trkContraste.Maximum, Math.Max(panelAjustes.trkContraste.Minimum, autoContraste));
                panelAjustes.trkBrillo.Value = Math.Min(panelAjustes.trkBrillo.Maximum, Math.Max(panelAjustes.trkBrillo.Minimum, autoBrillo));
                panelAjustes.trkRuido.Value = Math.Min(panelAjustes.trkRuido.Maximum, Math.Max(panelAjustes.trkRuido.Minimum, autoRuido + 1));

                panelAjustes.trkBlock.Value = 25;
                panelAjustes.trkC.Value = 10;
                panelAjustes.trkNitidez.Value = 1;
                panelAjustes.trkGrueso.Value = 0;
                // Umbral fijo: NO se resetea al cargar/capturar; se mantiene el valor elegido por el usuario.
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
                panelGuardar.chkExtraerGemini.Checked = true; // por defecto activado al capturar
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
        /* private void BtnCapturar_Click(object? sender, EventArgs e)
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
                 var (autoContraste, autoBrillo, autoRuido) = ObtenerAjustesAutomaticos(fotoCapturada);
                 Log("BtnCapturar_Click: CalcularAjustesAutomaticos OK");
                 panelAjustes.trkContraste.Value = Math.Min(panelAjustes.trkContraste.Maximum, Math.Max(panelAjustes.trkContraste.Minimum, autoContraste));
                 panelAjustes.trkBrillo.Value = Math.Min(panelAjustes.trkBrillo.Maximum, Math.Max(panelAjustes.trkBrillo.Minimum, autoBrillo));
                 panelAjustes.trkRuido.Value = Math.Min(panelAjustes.trkRuido.Maximum, Math.Max(panelAjustes.trkRuido.Minimum, autoRuido + 1));

                 panelAjustes.trkBlock.Value = 25;
                 panelAjustes.trkC.Value = 10;
                 panelAjustes.trkNitidez.Value = 1;
                 panelAjustes.trkGrueso.Value = 0;
                 panelAjustes.trkUmbral.Value = 0;
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
         }*/

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
                MostrarLogo();
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
                MostrarLogo();
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
        private async void BtnGuardar_Click(object? sender, EventArgs e)
        {
            CancelarAutoGuardadoLote();
            if (resultadoProcesado == null || fotoCapturada == null) return;

            if (rutaJsonEdicionActual != null)
            {
                string rutaJsonEnCurso = rutaJsonEdicionActual;
                Mat copiaImgEdicion = resultadoProcesado.Clone();
                Mat copiaOriginalEdicion = fotoCapturada.Clone();

                panelGuardar.btnGuardar.Enabled = false;
                btnRotar.Enabled = false;
                btnRepetir.Enabled = false;
                btnCapturar.Enabled = false;
                this.UseWaitCursor = true;
                lblEstado.Text = "🔎 Reescaneando y extrayendo datos con Gemini...";

                try
                {
                    await album.EditarFacturaCompleta(rutaJsonEnCurso, copiaImgEdicion, copiaOriginalEdicion,
                        panelGuardar.chkGuardarOriginal.Checked, panelGuardar.chkGuardarJpg.Checked,
                        panelGuardar.chkGuardarPdf.Checked, panelGuardar.chkExtraerGemini.Checked, MostrarRevisionEmbebida);
                    lblEstado.Text = "✅ Factura actualizada.";
                    DialogoAutoConfirmar.Aviso("La factura se editó y guardó correctamente.", "Éxito", 2, exito: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al editar la factura:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    copiaImgEdicion.Dispose();
                    copiaOriginalEdicion.Dispose();
                    rutaJsonEdicionActual = null;
                    this.UseWaitCursor = false;
                    btnCapturar.Enabled = true;
                    panelRevision.Visible = false;   
                    panelAjustes.Visible = true;     
                    LimpiarImagenActual();
                    VolverALive();
                }
                return;
            }

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
                    panelRevision.Visible = false;
                    panelAjustes.Visible = true;
                    if (perteneceALote) LimpiarImagenActual();
                    else VolverALive();

                    GuardadoTerminado?.Invoke(this, EventArgs.Empty);
                    guardadoEnCurso = false;
                    if (perteneceALote) CargarSiguienteDeCola();
                },
                MostrarRevisionEmbebida,
                reglasDuplicados: panelGuardar.ObtenerReglasDuplicados());
        }
        /*private async void BtnGuardar_Click(object? sender, EventArgs e)
        {
            CancelarAutoGuardadoLote();
            if (resultadoProcesado == null || fotoCapturada == null) return;

            if (rutaJsonEdicionActual != null)
            {
                string rutaJsonEnCurso = rutaJsonEdicionActual;
                Mat copiaImgEdicion = resultadoProcesado.Clone();
                Mat copiaOriginalEdicion = fotoCapturada.Clone();

                panelGuardar.btnGuardar.Enabled = false;
                btnRotar.Enabled = false;
                btnRepetir.Enabled = false;
                btnCapturar.Enabled = false;
                this.UseWaitCursor = true;
                lblEstado.Text = "🔎 Reescaneando y extrayendo datos con Gemini...";

                try
                {
                    await album.EditarFacturaCompleta(rutaJsonEnCurso, copiaImgEdicion, copiaOriginalEdicion,
                        panelGuardar.chkGuardarOriginal.Checked, panelGuardar.chkGuardarJpg.Checked,
                        panelGuardar.chkGuardarPdf.Checked, MostrarRevisionEmbebida);
                    lblEstado.Text = "✅ Factura actualizada.";
                    DialogoAutoConfirmar.Aviso("La factura se editó y guardó correctamente.", "Éxito", 2, exito: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al editar la factura:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    copiaImgEdicion.Dispose();
                    copiaOriginalEdicion.Dispose();
                    rutaJsonEdicionActual = null;
                    this.UseWaitCursor = false;
                    btnCapturar.Enabled = true;
                    panelRevision.Visible = false;
                    panelAjustes.Visible = true;
                    LimpiarImagenActual();
                    VolverALive();
                }
                return;
            }

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
                    panelRevision.Visible = false;
                    panelAjustes.Visible = true;
                    if (perteneceALote) LimpiarImagenActual();
                    else VolverALive();

                    GuardadoTerminado?.Invoke(this, EventArgs.Empty);
                    guardadoEnCurso = false;
                    if (perteneceALote) CargarSiguienteDeCola();
                },
                MostrarRevisionEmbebida);
        }*/

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
                tcs.TrySetResult(e.Resultado);
            }

            panelRevision.RevisionCompletada += OnCompletada;
            panelRevision.Mostrar(datos, sinCuentaAtras: rutaJsonEdicionActual != null);

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
            if (panelVisor.Visible) btnCerrarVisor_Click(null, EventArgs.Empty);
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
                {
                    lblEstado.Text = "✅ Lote completado – " + lblEstado.Text;
                    MostrarLogo();
                }
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

                var duplicado = album.BuscarDuplicadoPorPHash(img, out int distanciaPHash, out LogComparacionDuplicado logDuplicado);
                if (duplicado != null)
                {
                    string resumen =
                        $"Empresa: {duplicado.Empresa}\n" +
                        $"Nº Factura: {(string.IsNullOrWhiteSpace(duplicado.Numero) ? "(sin número)" : duplicado.Numero)}\n" +
                        $"Fecha: {duplicado.Fecha}\n" +
                        $"Total: {duplicado.Total}\n" +
                        $"Guardada el: {duplicado.FechaGuardado}\n" +
                        $"Coincidencia: {63 - distanciaPHash}/63 bits";

                    // Vista previa lado a lado: original (ya guardada) y duplicada
                    // (recién añadida), con 30 s de cuenta atrás para decidir.
                    // Se pasa el log de comparación para mostrarlo bajo las imágenes
                    // y poder exportarlo a .txt.
                    logDuplicado.RutaImagenNueva = ruta;
                    bool continuar = DialogoAutoConfirmar.ConfirmarDuplicadoConVistaPrevia(
                        img, ObtenerRutaImagenFactura(duplicado),
                        $"Esta imagen parece coincidir con una factura ya escaneada:\n\n{resumen}\n\n¿Procesar la imagen o saltarla?",
                        "Posible imagen duplicada", resultadoPorDefecto: false, segundos: 10,
                        log: logDuplicado);

                    if (!continuar)
                    {
                        img.Dispose();
                        CargarSiguienteDeCola();
                        return;
                    }
                }

                fotoCapturada?.Dispose();
                fotoCapturada = img;
                rotacionActual = ultimaRotacion; // recuerda la última rotación usada
                modoCaptura = true;
                ResetearZoom();

                var (autoContraste, autoBrillo, autoRuido) = ObtenerAjustesAutomaticos(fotoCapturada);
                panelAjustes.trkContraste.Value = Math.Min(panelAjustes.trkContraste.Maximum, Math.Max(panelAjustes.trkContraste.Minimum, autoContraste));
                panelAjustes.trkBrillo.Value = Math.Min(panelAjustes.trkBrillo.Maximum, Math.Max(panelAjustes.trkBrillo.Minimum, autoBrillo));
                panelAjustes.trkRuido.Value = Math.Min(panelAjustes.trkRuido.Maximum, Math.Max(panelAjustes.trkRuido.Minimum, autoRuido + 1));
                panelAjustes.trkNitidez.Value = 1;
                // Umbral fijo: NO se resetea al cargar/capturar; se mantiene el valor elegido por el usuario.

                panelGuardar.btnGuardar.Enabled = true;
                btnRepetir.Enabled = true;
                btnRotar.Enabled = true;
                btnCapturar.Enabled = false;
                panelGuardar.chkExtraerGemini.Checked = true; // por defecto activado al abrir/agregar lote

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
        /*private void CargarSiguienteDeCola()
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
                {
                    lblEstado.Text = "✅ Lote completado – " + lblEstado.Text;
                    MostrarLogo();
                }
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
                        $"Esta imagen parece coincidir con una factura ya escaneada:\n\n{resumen}\n\n¿Procesar la imagen o saltarla?",
                        "Posible imagen duplicada", resultadoPorDefecto: false, segundos: 10, traerAlFrente: true,
                        textoSi: "Procesar imagen", textoNo: "Saltar imagen", contadorRojoUltimoParpadea: true);

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

                var (autoContraste, autoBrillo, autoRuido) = ObtenerAjustesAutomaticos(fotoCapturada);
                panelAjustes.trkContraste.Value = Math.Min(panelAjustes.trkContraste.Maximum, Math.Max(panelAjustes.trkContraste.Minimum, autoContraste));
                panelAjustes.trkBrillo.Value = Math.Min(panelAjustes.trkBrillo.Maximum, Math.Max(panelAjustes.trkBrillo.Minimum, autoBrillo));
                panelAjustes.trkRuido.Value = Math.Min(panelAjustes.trkRuido.Maximum, Math.Max(panelAjustes.trkRuido.Minimum, autoRuido + 1));
                panelAjustes.trkNitidez.Value = 1;
                panelAjustes.trkUmbral.Value = 0;

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
        }*/

        // -----------------------------------------------------------------------
        // Muestra/oculta el contador y el botón "Salir del lote"
        // -----------------------------------------------------------------------
        private void ActualizarVisibilidadLote()
        {
            bool enLote = colaArchivos.Count > 1 && indiceColaActual >= 0 && indiceColaActual < colaArchivos.Count;
            panelGuardar.lblProgresoLote.Visible = enLote;
            panelGuardar.btnSalirLote.Visible = enLote;
            panelGuardar.btnSaltar.Visible = enLote;
            panelGuardar.btnCancelarAuto.Visible = enLote;
            if (enLote)
                panelGuardar.lblProgresoLote.Text = $"{indiceColaActual + 1}/{colaArchivos.Count}";

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
            MostrarLogo();
            VolverALive();
        }

        // -----------------------------------------------------------------------
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Ajustes > General > "Confirmar antes de salir": solo cuando cierra el usuario.
            if (ajustes.ConfirmarAlSalir && e.CloseReason == CloseReason.UserClosing &&
                MessageBox.Show(this, "¿Seguro que quieres salir de FACTicket Scanner?", "Salir",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

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
                webViewAlbum.CoreWebView2.WebMessageReceived -= WebViewAlbum_WebMessageReceived;
                webViewAlbum.CoreWebView2.WebMessageReceived += WebViewAlbum_WebMessageReceived;
                panelIzquierdo.Visible = false;
                panelDerecho.Visible = false;
                panelVisor.Visible = true;
                panelVisor.BringToFront();
                btnCerrarVisor.Visible = true;

                string htmlTexto = File.ReadAllText(rutaHtml);
                var m = System.Text.RegularExpressions.Regex.Match(htmlTexto, "const generado=\"([^\"]+)\"");
                lblEstadoVisor.Text = m.Success
                    ? $"📊 Panel de Facturas - Actualizado {m.Groups[1].Value}"
                    : "📊 Panel de Facturas";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el visor de tickets:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // -----------------------------------------------------------------------
        // Devuelve la ruta absoluta de la imagen de una factura ya guardada
        // (original.jpg junto a su datos.json; si no existe, la imagen
        // procesada). Devuelve null si no se encuentra ninguna.
        // -----------------------------------------------------------------------
        private string? ObtenerRutaImagenFactura(DatosTicket t)
        {
            string raiz = Path.Combine(Application.StartupPath, NombreCarpeta);

            if (!string.IsNullOrWhiteSpace(t.JsonRelativa))
            {
                string? carpeta = Path.GetDirectoryName(Path.Combine(raiz, t.JsonRelativa));
                if (carpeta != null)
                {
                    string original = Path.Combine(carpeta, "original.jpg");
                    if (File.Exists(original)) return original;
                }
            }

            if (!string.IsNullOrWhiteSpace(t.ImagenRelativa))
            {
                string procesada = Path.Combine(raiz, t.ImagenRelativa);
                if (File.Exists(procesada)) return procesada;
            }
            return null;
        }

        // -----------------------------------------------------------------------
        // Visor web: regenera el HTML del panel de facturas desde disco y lo
        // vuelve a cargar. Reutilizable desde cualquier punto que modifique
        // facturas (botón ⟳ del toolbar, diálogos que cambian datos, etc.).
        // -----------------------------------------------------------------------
        private void RecargarVisor()
        {
            try
            {
                panelNavModal.Visible = false;   // el modal se cierra al recargar la página
                album.RegenerarAlbumInicial();
                visorToolStripMenuItem_Click(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log("RecargarVisor: error - " + ex.Message);
            }
        }

        // Botón ⟳ del toolbar del visor
        private void btnRecargarVisor_Click(object? sender, EventArgs e) => RecargarVisor();

        // Crea el botón ⟳ junto al ✕ en la barra del visor
        private void ConstruirBotonRecargarVisor()
        {
            var btn = new Button
            {
                Name = "btnRecargarVisor",
                Text = "⟳",
                Dock = DockStyle.Right,
                Width = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(26, 115, 232),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += btnRecargarVisor_Click;
            new ToolTip().SetToolTip(btn, "Actualizar panel de facturas");
            panelBarraVisor.Controls.Add(btn);
            btn.BringToFront(); // se acopla a la izquierda de btnCerrarVisor
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
        // Visor web: recibe qué factura está abierta en el modal (postMessage
        // desde HtmlBuilder.cs -> abrirModal). Guarda ruta de imagen/json para
        // que los botones nativos (◀ ▶ ✏️) sepan sobre qué factura actuar.
        // -----------------------------------------------------------------------
        private string? rutaImagenVisorActual;
        private string? rutaJsonVisorActual;
        private string? empresaVisorActual, numeroVisorActual, fechaVisorActual, totalVisorActual;

        private void WebViewAlbum_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                string accion = root.TryGetProperty("accion", out var ac) ? ac.GetString() ?? "" : "";

                if (accion == "recargar")
                {
                    // Se difiere: RecargarVisor navega la propia página que envió el mensaje
                    BeginInvoke(new Action(RecargarVisor));
                    return;
                }

                // Exportación desde el visor web (CSV/JSON/Excel...): el HTML envía el
                // contenido y el nombre; aquí se pide la ruta con "Guardar como...".
                // Debe ir ANTES del resto de acciones para no alterar el estado del modal.
                if (accion == "guardarArchivo")
                {
                    string nombreArchivo = root.TryGetProperty("nombre", out var nm) ? nm.GetString() ?? "exportacion.txt" : "exportacion.txt";
                    string contenidoArchivo = root.TryGetProperty("contenido", out var ct) ? ct.GetString() ?? "" : "";
                    string filtroArchivo = root.TryGetProperty("filtro", out var fl) ? fl.GetString() ?? "Todos los archivos (*.*)|*.*" : "Todos los archivos (*.*)|*.*";
                    BeginInvoke(new Action(() =>
                    {
                        string? guardado = ExportadorArchivos.GuardarTextoConDialogo(this, nombreArchivo, contenidoArchivo, filtroArchivo,
                            carpetaInicial: ajustes.CarpetaExportacion, abrirCarpeta: ajustes.AbrirCarpetaAlExportar);
                        if (guardado != null) Log("Visor: exportado " + guardado);
                    }));
                    return;
                }

                if (accion == "cerrar")
                {
                    rutaImagenVisorActual = null;
                    rutaJsonVisorActual = null;
                    panelNavModal.Visible = false;
                    return;
                }

                rutaImagenVisorActual = root.TryGetProperty("imagen", out var im) ? im.GetString() : null;
                rutaJsonVisorActual = root.TryGetProperty("json", out var js) ? js.GetString() : null;
                string empresa = root.TryGetProperty("empresa", out var em) ? em.GetString() ?? "(sin empresa)" : "(sin empresa)";
                string fecha = root.TryGetProperty("fecha", out var fe) ? fe.GetString() ?? "—" : "—";

                lblTituloModal.Text = $"{empresa} · {fecha}";
                panelNavModal.Visible = true;
                CentrarPanelNavModal();
            }
            catch (Exception ex)
            {
                Log("WebMessage visor: error al leer datos - " + ex.Message);
            }
        }

        // -----------------------------------------------------------------------
        // Centra panelNavModal (controles nativos ◀ · ▶ ✏️ ➖ ✕ del modal) en
        // panelBarraVisor, que también aloja lblEstadoVisor a la izquierda y
        // btnCerrarVisor a la derecha (cierra todo el visor web, no el modal).
        // -----------------------------------------------------------------------
        private void CentrarPanelNavModal()
        {
            panelNavModal.Left = (panelBarraVisor.ClientSize.Width - panelNavModal.Width) / 2;
            panelNavModal.Top = (panelBarraVisor.ClientSize.Height - panelNavModal.Height) / 2;
        }

        private void panelBarraVisor_Resize(object? sender, EventArgs e)
        {
            CentrarPanelNavModal();
        }

        private async void btnAnteriorVisor_Click(object? sender, EventArgs e)
        {
            try { await webViewAlbum.CoreWebView2.ExecuteScriptAsync("navModal(-1)"); } catch { }
        }

        private async void btnSiguienteVisor_Click(object? sender, EventArgs e)
        {
            try { await webViewAlbum.CoreWebView2.ExecuteScriptAsync("navModal(1)"); } catch { }
        }

        // -----------------------------------------------------------------------
        // Visor web: botón ✏️ -> reprocesa la imagen de la factura abierta con
        // Gemini y sobrescribe su JSON, para corregir datos mal extraídos.
        // -----------------------------------------------------------------------
        // -----------------------------------------------------------------------
        // Pregunta al JS qué factura está realmente abierta en el modal ahora
        // mismo (datosModalActual en HtmlBuilder.cs) y actualiza rutaImagen/
        // rutaJsonVisorActual. Evita fiarse solo del postMessage de abrirModal,
        // que puede perderse o llegar tarde.
        // -----------------------------------------------------------------------
        private async System.Threading.Tasks.Task<bool> RefrescarFacturaVisorActual()
        {
            try
            {
                string resultado = await webViewAlbum.CoreWebView2.ExecuteScriptAsync("datosModalActual()");
                string interior = System.Text.Json.JsonSerializer.Deserialize<string>(resultado) ?? "";
                if (string.IsNullOrEmpty(interior)) return false;

                using var doc = System.Text.Json.JsonDocument.Parse(interior);
                var root = doc.RootElement;
                rutaImagenVisorActual = root.TryGetProperty("imagen", out var im) ? im.GetString() : null;
                rutaJsonVisorActual = root.TryGetProperty("json", out var js) ? js.GetString() : null;
                empresaVisorActual = root.TryGetProperty("empresa", out var em) ? em.GetString() : null;
                fechaVisorActual = root.TryGetProperty("fecha", out var fe) ? fe.GetString() : null;
                numeroVisorActual = root.TryGetProperty("numero", out var nu) ? nu.GetString() : null;
                totalVisorActual = root.TryGetProperty("total", out var to) ? to.GetString() : null;
                return !string.IsNullOrEmpty(rutaJsonVisorActual);
            }
            catch { return false; }
        }


        private async void btnEditarVisor_Click(object? sender, EventArgs e)
        {
            await RefrescarFacturaVisorActual();
            if (string.IsNullOrEmpty(rutaImagenVisorActual) || string.IsNullOrEmpty(rutaJsonVisorActual))
            {
                MessageBox.Show("No hay ninguna factura abierta en el visor.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string rutaImagenAbs = Path.Combine(Application.StartupPath, NombreCarpeta, rutaImagenVisorActual);
            string rutaJsonAbs = Path.Combine(Application.StartupPath, NombreCarpeta, rutaJsonVisorActual);

            if (!File.Exists(rutaImagenAbs) || !File.Exists(rutaJsonAbs))
            {
                MessageBox.Show("No se encontró la imagen o el JSON de esta factura.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string rutaOriginalAbs = Path.Combine(Path.GetDirectoryName(rutaImagenAbs)!, "original.jpg");
            string rutaParaCargar = File.Exists(rutaOriginalAbs) ? rutaOriginalAbs : rutaImagenAbs;

            Mat img = Cv2.ImRead(rutaParaCargar);
            if (img.Empty())
            {
                MessageBox.Show("No se pudo leer la imagen de esta factura.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnCerrarVisor_Click(null, EventArgs.Empty);
            await AbrirEditorTicketAsync(img, rutaJsonAbs);
        }

        // -----------------------------------------------------------------------
        // Abre EditorTicketPanel (integrado, cuadrícula 2x2) para editar una factura ya
        // guardada. "Reprocesar imagen" aplica solo los controles en local;
        // "Reescanear solo datos" llama a Gemini sin tocar la imagen. Al
        // pulsar Guardar se persiste sobre la MISMA carpeta (sin Gemini).
        // Toma posesión de 'img' (la libera al terminar).
        // -----------------------------------------------------------------------
        private async System.Threading.Tasks.Task AbrirEditorTicketAsync(Mat img, string rutaJsonAbs)
        {
            var datosIniciales = DatosTicket.CargarUnico(rutaJsonAbs);
            if (datosIniciales == null)
            {
                img.Dispose();
                MessageBox.Show("No se pudo leer el JSON de esta factura.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Controles propios del editor, inicializados con ajustes automáticos.
            var controles = new PanelAjustesEscaneo { Dock = DockStyle.Fill };
            var (autoContraste, autoBrillo, autoRuido) = ObtenerAjustesAutomaticos(img);
            controles.trkContraste.Value = Math.Min(controles.trkContraste.Maximum, Math.Max(controles.trkContraste.Minimum, autoContraste));
            controles.trkBrillo.Value = Math.Min(controles.trkBrillo.Maximum, Math.Max(controles.trkBrillo.Minimum, autoBrillo));
            controles.trkRuido.Value = Math.Min(controles.trkRuido.Maximum, Math.Max(controles.trkRuido.Minimum, autoRuido + 1));
            controles.trkNitidez.Value = 1;

            // Panel de datos sin botones propios (los ofrece el editor).
            var panelDatos = new PanelRevisionTicket { Dock = DockStyle.Fill, BotonesPropios = false };
            panelDatos.Mostrar(datosIniciales, sinCuentaAtras: true);

            // Reprocesado 100% local con los valores actuales de los sliders.
            Mat Reprocesado() => ImageProcessor.ProcesarImagen(img, 0,
                controles.trkBlock.Value * 2 + 1, controles.trkC.Value,
                controles.trkRuido.Value, controles.trkNitidez.Value, controles.trkGrueso.Value,
                controles.trkContraste.Value, controles.trkBrillo.Value,
                controles.trkUmbral.Value, controles.trkMargen.Value,
                controles.chkEdicionManual.Checked,
                controles.trkMargenSup.Value, controles.trkMargenInf.Value,
                controles.trkMargenIzq.Value, controles.trkMargenDer.Value);

            // Reescaneo de datos con Gemini (sobre la original, como hacía el flujo anterior).
            async System.Threading.Tasks.Task ReescanearDatos(Mat _)
            {
                var nuevos = await GeminiAPI.ExtraerDatosFactura(img);
                panelDatos.Mostrar(nuevos, sinCuentaAtras: true);
            }

            // Editor integrado: sustituye temporalmente al panel de captura dentro de Form1.
            var editor = new EditorTicketPanel(img, Reprocesado(), controles, panelDatos, Reprocesado, ReescanearDatos);
            var tcs = new System.Threading.Tasks.TaskCompletionSource<ResultadoEditor>();
            editor.Cerrado += (s, r) => tcs.TrySetResult(r);

            panelIzquierdo.Visible = false;
            panelDerecho.Visible = false;
            Controls.Add(editor);
            editor.BringToFront();

            ResultadoEditor resultado = await tcs.Task;

            Controls.Remove(editor);
            panelIzquierdo.Visible = true;
            panelDerecho.Visible = true;

            if (resultado != ResultadoEditor.Guardar || editor.ImagenProcesada == null)
            {
                editor.Dispose();
                img.Dispose();
                return;
            }

            var datosFinales = panelDatos.ObtenerDatosEditados();
            Mat copiaProcesada = editor.ImagenProcesada.Clone();
            editor.Dispose();
            this.UseWaitCursor = true;
            try
            {
                // extraerConGemini:false -> se guardan los datos editados en el editor.
                await album.EditarFacturaCompleta(rutaJsonAbs, copiaProcesada, img,
                    panelGuardar.chkGuardarOriginal.Checked, panelGuardar.chkGuardarJpg.Checked,
                    panelGuardar.chkGuardarPdf.Checked, false,
                    _ => System.Threading.Tasks.Task.FromResult<DatosTicket?>(datosFinales));
                lblEstado.Text = "✅ Factura actualizada.";
                DialogoAutoConfirmar.Aviso("La factura se editó y guardó correctamente.", "Éxito", 2, exito: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al editar la factura:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                copiaProcesada.Dispose();
                img.Dispose();
                this.UseWaitCursor = false;
            }
        }
        /*private async void btnEditarVisor_Click(object? sender, EventArgs e)
        {
            await RefrescarFacturaVisorActual();
            if (string.IsNullOrEmpty(rutaImagenVisorActual) || string.IsNullOrEmpty(rutaJsonVisorActual))
            {
                MessageBox.Show("No hay ninguna factura abierta en el visor.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string rutaImagenAbs = Path.Combine(Application.StartupPath, NombreCarpeta, rutaImagenVisorActual);
            string rutaJsonAbs = Path.Combine(Application.StartupPath, NombreCarpeta, rutaJsonVisorActual);

            if (!File.Exists(rutaImagenAbs) || !File.Exists(rutaJsonAbs))
            {
                MessageBox.Show("No se encontró la imagen o el JSON de esta factura.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string rutaOriginalAbs = Path.Combine(Path.GetDirectoryName(rutaImagenAbs)!, "original.jpg");
            string rutaParaCargar = File.Exists(rutaOriginalAbs) ? rutaOriginalAbs : rutaImagenAbs;

            Mat img = Cv2.ImRead(rutaParaCargar);
            if (img.Empty())
            {
                MessageBox.Show("No se pudo leer la imagen de esta factura.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnCerrarVisor_Click(null, EventArgs.Empty);

            rutaJsonEdicionActual = rutaJsonAbs;
            colaArchivos.Clear();
            indiceColaActual = -1;

            fotoCapturada?.Dispose();
            fotoCapturada = img;
            rotacionActual = 0;
            modoCaptura = true;
            ResetearZoom();

            var (autoContraste, autoBrillo, autoRuido) = ObtenerAjustesAutomaticos(fotoCapturada);
            panelAjustes.trkContraste.Value = Math.Min(panelAjustes.trkContraste.Maximum, Math.Max(panelAjustes.trkContraste.Minimum, autoContraste));
            panelAjustes.trkBrillo.Value = Math.Min(panelAjustes.trkBrillo.Maximum, Math.Max(panelAjustes.trkBrillo.Minimum, autoBrillo));
            panelAjustes.trkRuido.Value = Math.Min(panelAjustes.trkRuido.Maximum, Math.Max(panelAjustes.trkRuido.Minimum, autoRuido + 1));
            panelAjustes.trkNitidez.Value = 1;
            panelAjustes.trkUmbral.Value = 0;

            panelGuardar.btnGuardar.Enabled = true;
            btnRepetir.Enabled = true;
            btnRotar.Enabled = true;
            btnCapturar.Enabled = false;

            lblEstado.Text = "✏️ Editando factura – ajusta y pulsa Guardar";
            Reprocesar();
        }*/

        // -----------------------------------------------------------------------
        // Visor web: botón ✕ (panelNavModal) -> cierra solo la vista previa
        // de la factura (el modal), NO el visor web completo. Distinto de
        // btnCerrarVisor, que cierra panelVisor entero.
        // -----------------------------------------------------------------------
        private async void btnCerrarModalVisor_Click(object? sender, EventArgs e)
        {
            try { await webViewAlbum.CoreWebView2.ExecuteScriptAsync("cerrarModal()"); } catch { }
            rutaImagenVisorActual = null;
            rutaJsonVisorActual = null;
            panelNavModal.Visible = false;
        }

        // -----------------------------------------------------------------------
        // Visor web: botón ➖ -> elimina definitivamente la factura abierta en
        // el modal (carpeta completa en disco) y regenera el álbum.
        // -----------------------------------------------------------------------
        private async void btnEliminarVisor_Click(object? sender, EventArgs e)
        {
            await RefrescarFacturaVisorActual();

            string? rutaJsonRelativa = rutaJsonVisorActual;
            if (string.IsNullOrEmpty(rutaJsonRelativa) && !string.IsNullOrEmpty(empresaVisorActual))
            {
                string? rutaAbsEncontrada = album.BuscarRutaJsonFactura(
                    empresaVisorActual, numeroVisorActual ?? "", fechaVisorActual ?? "", totalVisorActual ?? "");
                if (rutaAbsEncontrada != null)
                    rutaJsonRelativa = Path.GetRelativePath(Path.Combine(Application.StartupPath, NombreCarpeta), rutaAbsEncontrada).Replace('\\', '/');
            }

            if (string.IsNullOrEmpty(rutaJsonRelativa))
            {
                MessageBox.Show("No hay ninguna factura abierta en el visor.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string rutaJsonAbs = Path.Combine(Application.StartupPath, NombreCarpeta, rutaJsonRelativa);
            string? carpetaFactura = Path.GetDirectoryName(rutaJsonAbs);

            if (string.IsNullOrEmpty(carpetaFactura) || !Directory.Exists(carpetaFactura))
            {
                // Huérfano: el datos.json apuntaba a una carpeta ya borrada a mano.
                // No hay nada que borrar en disco; solo hace falta que el álbum
                // deje de mostrar esta entrada fantasma.
                bool confirmarHuerfano = DialogoAutoConfirmar.Confirmar(
                    "La carpeta de esta factura ya no existe en disco (se borró manualmente).\n¿Quitarla del listado?",
                    "Factura huérfana", resultadoPorDefecto: false);
                if (!confirmarHuerfano) return;

                await btnCerrarModalVisor_ClickInterno();
                await RegenerarAlbumYRecargarVisor();
                Log("Entrada huérfana quitada del álbum (carpeta ya no existía): " + rutaJsonAbs);
                return;
            }

            bool confirmar = DialogoAutoConfirmar.Confirmar(
                "¿Eliminar definitivamente esta factura y todos sus archivos?\nEsta acción no se puede deshacer.",
                "Eliminar factura", resultadoPorDefecto: false);
            if (!confirmar) return;

            try
            {
                Directory.Delete(carpetaFactura, recursive: true);

                await btnCerrarModalVisor_ClickInterno();
                await RegenerarAlbumYRecargarVisor();

                Log("Factura eliminada desde el visor: " + carpetaFactura);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar la factura:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task btnCerrarModalVisor_ClickInterno()
        {
            try { await webViewAlbum.CoreWebView2.ExecuteScriptAsync("cerrarModal()"); } catch { }
            rutaImagenVisorActual = null;
            rutaJsonVisorActual = null;
            panelNavModal.Visible = false;
        }

        private async System.Threading.Tasks.Task RegenerarAlbumYRecargarVisor()
        {
            album.RegenerarAlbumInicial(); // reescanea Facturas/{Año}/{Empresa}/{Factura_x} en disco
            try { await webViewAlbum.CoreWebView2.ExecuteScriptAsync("location.reload()"); } catch { }
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
        // Menú: Facturas > Importar > Desde cámara
        // Conecta la cámara elegida en Ajustes > General > Cámara (la recordada
        // en ajustes.json) para poder tomar la foto. Si ya está conectada, no
        // hace nada (el visor en vivo ya está activo).
        // -----------------------------------------------------------------------
        private void desdeCamaraToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool sinCamara = string.IsNullOrEmpty(ajustes.UltimoTipoCamara) || ajustes.UltimoTipoCamara == "FILE";
            if (sinCamara)
            {
                DialogoAutoConfirmar.Aviso("Elige una cámara en Ajustes > General > Cámara.", "Importar desde cámara");
                return;
            }

            if (camara.EstaConectada) return;
            ReconectarUltimaCamara();
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
        // Traslada a las clases globales los ajustes que no consulta Form1 directamente
        // (cuenta atrás de los diálogos). Se llama al cargar y al aceptar Ajustes.
        // -----------------------------------------------------------------------
        private void AplicarAjustesGlobales()
        {
            CuentaAtrasConfig.Aplicar(ajustes.AutoConfirmarTrasCuentaAtras, ajustes.SegundosCuentaAtras);
        }

        // -----------------------------------------------------------------------
        // Contraste, brillo y ruido iniciales de una imagen recién cargada o capturada.
        // Con "Contraste y brillo automáticos" activo se calculan a partir de la foto;
        // si está desactivado se usan los valores neutros de ajustes (el ruido sigue
        // siendo automático).
        // -----------------------------------------------------------------------
        private (int contraste, int brillo, int ruido) ObtenerAjustesAutomaticos(Mat imagen)
        {
            var auto = ImageProcessor.CalcularAjustesAutomaticos(imagen);
            if (ajustes.ContrasteBrilloAutomaticos) return auto;
            return (ajustes.Contraste, ajustes.Brillo, auto.ruido);
        }

        // -----------------------------------------------------------------------
        // Menú: Ajustes > General (lista única: General, Cámara, Duplicados,
        // Escaneo, Exportación y Claves API). Al aceptar, persiste en ajustes.json y refresca
        // lo que ya está en pantalla (reglas de duplicados y texto de la cámara).
        // -----------------------------------------------------------------------
        private void generalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Los botones de selección de cámara de la ventana conectan y guardan al
            // momento (sobre la copia de ajustes que edita la ventana).
            using var form = new AjustesForm(ajustes,
                a => camara.IniciarSeleccionUsb(a, this, x => album.GuardarAjustes(x)),
                a => camara.IniciarSeleccionIp(a, this, x => album.GuardarAjustes(x)));

            // Aceptar: se usa la copia editada. Cancelar: se resincroniza con disco,
            // por si la cámara se cambió y guardó desde la ventana.
            bool aceptado = form.ShowDialog(this) == DialogResult.OK;
            ajustes = aceptado ? form.Resultado : album.CargarAjustes();
            if (aceptado) album.GuardarAjustes(ajustes);
            AplicarAjustesGlobales();

            // Reglas de duplicados: no dispara ReglasCambiadas (no hay doble guardado).
            panelGuardar?.AplicarReglasDuplicados(new ReglasDuplicados
            {
                Numero = ajustes.DupNumero,
                Fecha = ajustes.DupFecha,
                Total = ajustes.DupTotal,
                Empresa = ajustes.DupEmpresa
            });

            // Texto de la barra de cámara (sin limpiar la lista de cámaras encontradas).
            bool esIp = cmbTipoCamara.SelectedIndex == 1;
            txtUrlCamara.Text = esIp ? ajustes.UltimaUrlCamaraIp
                                     : (ajustes.UltimoIndiceCamaraUsb >= 0 ? $"USB Puerto {ajustes.UltimoIndiceCamaraUsb}" : "");
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

        private void buscarDuplicadosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new BuscarDuplicadosForm().ShowDialog(this);
        }

        private void cerrarTrimestreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new CierreTrimestralForm())
                form.ShowDialog(this);

            // Los cambios (presentado/trimestre) se guardan en los datos.json:
            // se regenera el panel de facturas para que se apliquen.
            try
            {
                if (panelVisor.Visible) RecargarVisor();
                else album.RegenerarAlbumInicial();
            }
            catch (Exception ex)
            {
                Log("cerrarTrimestre: error al actualizar panel - " + ex.Message);
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
            using var form = new AnalizarPHashForm(album);
            form.ShowDialog(this);
        }
        private void exportarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var form = new ExportarForm(null, ajustes);
            form.ShowDialog(this);
        }
    }
}