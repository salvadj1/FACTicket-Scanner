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
        private Button btnGuardar = null!;
        private Button btnRepetir = null!;
        private Button btnRotar = null!;
        private Button btnSalirLote = null!;
        private Label lblProgresoLote = null!;

        private TrackBar trkBlock = null!;
        private TrackBar trkC = null!;
        private TrackBar trkContraste = null!;
        private TrackBar trkBrillo = null!;
        private TrackBar trkRuido = null!;
        private TrackBar trkNitidez = null!;
        private TrackBar trkGrueso = null!;
        private TrackBar trkUmbral = null!;
        private TrackBar trkMargen = null!;
        private TrackBar trkMargenSup = null!;
        private TrackBar trkMargenInf = null!;
        private TrackBar trkMargenIzq = null!;
        private TrackBar trkMargenDer = null!;

        private Label valBlock = null!;
        private Label valC = null!;
        private Label valContraste = null!;
        private Label valBrillo = null!;
        private Label valRuido = null!;
        private Label valNitidez = null!;
        private Label valGrueso = null!;
        private Label valUmbral = null!;
        private Label valMargen = null!;
        private Label valMargenSup = null!;
        private Label valMargenInf = null!;
        private Label valMargenIzq = null!;
        private Label valMargenDer = null!;

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
            this.Text = "FACTicket Scanner - 1.054 beta";
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

            var (baseW, baseH) = CalcularTamanoAjustado(pictureBox1.Image.Size, panelIzquierdo.ClientSize);
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

            // ── SLIDERS (panelScrollable) ────────────────────────────────────
            int y = 8;

            (trkBlock, valBlock) = Slider("Detalle:", y, 1, 40, 25, 2); y += 34;
            (trkC, valC) = Slider("Brillo/C:", y, -20, 20, 10, 4); y += 34;
            (trkContraste, valContraste) = Slider("CLAHE (clipLimit):", y, 0, 8, 2, 1); y += 34;
            (trkBrillo, valBrillo) = Slider("Brillo imagen:", y, -50, 50, 0, 10); y += 34;
            (trkRuido, valRuido) = Slider("Reducir ruido:", y, 0, 4, 1, 1); y += 34;
            (trkNitidez, valNitidez) = Slider("Nitidez (post):", y, 0, 3, 0, 1); y += 34;
            (trkGrueso, valGrueso) = Slider("Grosor texto:", y, -3, 3, 0, 1); y += 34;
            (trkUmbral, valUmbral) = Slider("Umbral fijo:", y, 0, 254, 0, 20); y += 34;
            (trkMargen, valMargen) = Slider("Sensib. recorte:", y, 1, 30, 5, 5); y += 34;
            (trkMargenSup, valMargenSup) = Slider("Margen sup. (%):", y, 0, 50, 0, 5); y += 34;
            (trkMargenInf, valMargenInf) = Slider("Margen inf. (%):", y, 0, 50, 0, 5); y += 34;
            (trkMargenIzq, valMargenIzq) = Slider("Margen izq. (%):", y, 0, 50, 0, 5); y += 34;
            (trkMargenDer, valMargenDer) = Slider("Margen der. (%):", y, 0, 50, 0, 5); y += 34;

            var chkEdicionManual = new CheckBox
            {
                Left = 0,
                Top = y,
                Width = wTotal,
                AutoSize = false,
                Height = 24,
                Text = "Edición manual (sin recorte automático)"
            };
            chkEdicionManual.CheckedChanged += (s, e) => Reprocesar();
            panelScrollable.Controls.Add(chkEdicionManual);
            y += 30;

            panelScrollable.Controls.Add(new Label
            {
                Left = 0,
                Top = y,
                Width = wTotal,
                Height = 36,
                AutoSize = false,
                Text = "CLAHE/Brillo/Ruido se auto-calibran al capturar.",
                ForeColor = System.Drawing.Color.Gray,
                Font = new System.Drawing.Font(Font.FontFamily, 7.5f)
            });

            ActualizarEtiquetas();

            // Suscribir sliders al reprocesado
            foreach (TrackBar t in new[] { trkBlock, trkC, trkContraste, trkBrillo, trkRuido,
                trkNitidez, trkGrueso, trkUmbral, trkMargen,
                trkMargenSup, trkMargenInf, trkMargenIzq, trkMargenDer })
                t.ValueChanged += (s, e) => { if (modoCaptura) Reprocesar(); };

            // ── BOTONES (panelBotones, fijo abajo) ──────────────────────────
            int wP = panelBotones.ClientSize.Width - 16;
            if (wP < 200) wP = 200;
            int wBtn3 = (wP - 16) / 3;

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

            // Fila: [Rotar] [Repetir] [Guardar]
            btnRotar = new Button
            {
                Left = 0,
                Top = 12,
                Width = wBtn3,
                Height = 40,
                Text = "↻  Rotar 90°",
                Enabled = false
            };
            btnRotar.Click += (s, e) => { rotacionActual = (rotacionActual + 90) % 360; Reprocesar(); };
            panelBotones.Controls.Add(btnRotar);

            btnRepetir = new Button
            {
                Left = wBtn3 + 8,
                Top = 12,
                Width = wBtn3,
                Height = 40,
                Text = "🔁  Repetir",
                Enabled = false
            };
            btnRepetir.Click += BtnRepetir_Click;
            panelBotones.Controls.Add(btnRepetir);

            btnGuardar = new Button
            {
                Left = (wBtn3 + 8) * 2,
                Top = 12,
                Width = wBtn3,
                Height = 40,
                Text = "💾  Guardar",
                Enabled = false,
                BackColor = System.Drawing.Color.SeaGreen,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnGuardar.Click += BtnGuardar_Click;
            panelBotones.Controls.Add(btnGuardar);

            // Separador
            panelBotones.Controls.Add(new Label
            {
                Left = 0,
                Top = 58,
                Width = wP,
                Height = 1,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.Silver
            });

            // Fila: [lblEstado] [btnCapturar]
            lblEstado = new Label
            {
                Left = 0,
                Top = 66,
                Width = wP - 160,
                Height = 36,
                Text = "Sin cámara – usa Configuracion > Camara",
                Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.DarkSlateBlue,
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            panelBotones.Controls.Add(lblEstado);

            btnCapturar = new Button
            {
                Left = wP - 152,
                Top = 62,
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

            // --- Progreso del lote + botón Salir (solo visibles durante un lote) ---
            lblProgresoLote = new Label
            {
                Left = 0,
                Top = 104,
                Width = wP - 160,
                Height = 22,
                Text = "",
                Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.SteelBlue,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Visible = false
            };
            panelBotones.Controls.Add(lblProgresoLote);

            btnSalirLote = new Button
            {
                Left = wP - 152,
                Top = 104,
                Width = 152,
                Height = 28,
                Text = "✖  Salir del lote",
                Visible = false,
                BackColor = System.Drawing.Color.IndianRed,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSalirLote.Click += BtnSalirLote_Click;
            panelBotones.Controls.Add(btnSalirLote);

            // Ajustar altura del panelBotones según contenido
            panelBotones.Height = lblProgresoLote.Bottom + 8;
        }

        // Helper: crea un par Label+TrackBar+Label de valor en el panelScrollable
        private (TrackBar trk, Label val) Slider(string texto, int y,
            int min, int max, int valor, int tick)
        {
            int wTotal = panelScrollable.ClientSize.Width - 16;
            if (wTotal < 200) wTotal = 200;
            int wLbl = 130, wVal = 48, wTrk = wTotal - wLbl - wVal - 4;

            panelScrollable.Controls.Add(new Label
            {
                Left = 0,
                Top = y + 6,
                Width = wLbl,
                Text = texto,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight
            });

            var trk = new TrackBar
            {
                Left = wLbl + 2,
                Top = y,
                Width = wTrk,
                Height = 30,
                Minimum = min,
                Maximum = max,
                Value = Math.Min(max, Math.Max(min, valor)),
                TickFrequency = tick,
                AutoSize = false
            };
            panelScrollable.Controls.Add(trk);

            var lbl = new Label { Left = wLbl + wTrk + 4, Top = y + 6, Width = wVal };
            panelScrollable.Controls.Add(lbl);

            return (trk, lbl);
        }

        // -----------------------------------------------------------------------
        // Actualiza etiquetas de valores de sliders
        // -----------------------------------------------------------------------
        private void ActualizarEtiquetas()
        {
            if (valBlock == null) return;
            valBlock.Text = (trkBlock.Value * 2 + 1).ToString();
            valC.Text = trkC.Value.ToString();
            valContraste.Text = trkContraste.Value == 0 ? "off" : trkContraste.Value.ToString();
            valBrillo.Text = trkBrillo.Value.ToString("+#;-#;0");
            valRuido.Text = trkRuido.Value == 0 ? "off" : trkRuido.Value.ToString();
            valNitidez.Text = trkNitidez.Value == 0 ? "off" : trkNitidez.Value.ToString();
            valGrueso.Text = trkGrueso.Value.ToString("+#;-#;0");
            valUmbral.Text = trkUmbral.Value == 0 ? "auto" : trkUmbral.Value.ToString();
            valMargen.Text = $"{trkMargen.Value}%";
            valMargenSup.Text = trkMargenSup.Value == 0 ? "off" : $"{trkMargenSup.Value}%";
            valMargenInf.Text = trkMargenInf.Value == 0 ? "off" : $"{trkMargenInf.Value}%";
            valMargenIzq.Text = trkMargenIzq.Value == 0 ? "off" : $"{trkMargenIzq.Value}%";
            valMargenDer.Text = trkMargenDer.Value == 0 ? "off" : $"{trkMargenDer.Value}%";
        }

        // -----------------------------------------------------------------------
        // Reprocesa la foto capturada y actualiza el PictureBox en tiempo real
        // -----------------------------------------------------------------------
        private void Reprocesar()
        {
            if (!modoCaptura || fotoCapturada == null) return;
            Log("Reprocesar: inicio");
            ActualizarEtiquetas();

            resultadoProcesado?.Dispose();
            Log("Reprocesar: llamando ProcesarImagen");
            CheckBox? chkEdicionManual = null;
            foreach (Control c in panelScrollable.Controls)
                if (c is CheckBox chk) { chkEdicionManual = chk; break; }

            resultadoProcesado = ImageProcessor.ProcesarImagen(fotoCapturada, rotacionActual,
                trkBlock.Value * 2 + 1, trkC.Value,
                trkRuido.Value, trkNitidez.Value, trkGrueso.Value,
                trkContraste.Value, trkBrillo.Value,
                trkUmbral.Value, trkMargen.Value,
                chkEdicionManual?.Checked ?? false,
                trkMargenSup.Value, trkMargenInf.Value,
                trkMargenIzq.Value, trkMargenDer.Value);
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
                trkContraste.Value = Math.Min(trkContraste.Maximum, Math.Max(trkContraste.Minimum, autoContraste));
                trkBrillo.Value = Math.Min(trkBrillo.Maximum, Math.Max(trkBrillo.Minimum, autoBrillo));
                trkRuido.Value = Math.Min(trkRuido.Maximum, Math.Max(trkRuido.Minimum, autoRuido + 1));

                trkBlock.Value = 25;
                trkC.Value = 10;
                trkNitidez.Value = 1;
                trkGrueso.Value = 0;
                trkUmbral.Value = 10;
                trkMargen.Value = 5;
                trkMargenSup.Value = 0;
                trkMargenInf.Value = 0;
                trkMargenIzq.Value = 0;
                trkMargenDer.Value = 0;
                Log("BtnCapturar_Click: sliders reseteados OK");

                modoCaptura = true;
                btnGuardar.Enabled = true;
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
            modoCaptura = false;
            fotoCapturada?.Dispose();
            fotoCapturada = null;
            resultadoProcesado?.Dispose();
            resultadoProcesado = null;

            btnGuardar.Enabled = false;
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
            if (resultadoProcesado == null || fotoCapturada == null) return;
            Mat copiaImg = resultadoProcesado.Clone();
            Mat copiaOriginal = fotoCapturada.Clone();
            int rot = rotacionActual;

            bool perteneceALote = indiceColaActual >= 0 && indiceColaActual < colaArchivos.Count;

            btnGuardar.Enabled = false;
            btnRotar.Enabled = false;
            btnRepetir.Enabled = false;
            btnCapturar.Enabled = false;
            this.UseWaitCursor = true;

            guardadoEnCurso = true;
            album.GuardarImagen(copiaImg, copiaOriginal, rot, ajustes,
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
                });
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
                    MessageBox.Show($"No se pudo leer la imagen:\n{ruta}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    CargarSiguienteDeCola();
                    return;
                }

                fotoCapturada?.Dispose();
                fotoCapturada = img;
                rotacionActual = 0;
                modoCaptura = true;
                ResetearZoom();

                var (autoContraste, autoBrillo, autoRuido) = ImageProcessor.CalcularAjustesAutomaticos(fotoCapturada);
                trkContraste.Value = Math.Min(trkContraste.Maximum, Math.Max(trkContraste.Minimum, autoContraste));
                trkBrillo.Value = Math.Min(trkBrillo.Maximum, Math.Max(trkBrillo.Minimum, autoBrillo));
                trkRuido.Value = Math.Min(trkRuido.Maximum, Math.Max(trkRuido.Minimum, autoRuido + 1));
                trkNitidez.Value = 1;
                trkUmbral.Value = 10;

                btnGuardar.Enabled = true;
                btnRepetir.Enabled = true;
                btnRotar.Enabled = true;
                btnCapturar.Enabled = false;

                ActualizarVisibilidadLote();
                lblEstado.Text = colaArchivos.Count > 1
                    ? "📂 Ajusta la imagen y pulsa Guardar"
                    : "📂 Imagen cargada – ajusta y pulsa Guardar";

                Reprocesar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }
}