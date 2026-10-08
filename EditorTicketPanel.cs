using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    /// <summary>Resultado de cerrar el editor.</summary>
    public enum ResultadoEditor { Cancelar, Guardar }

    /// <summary>
    /// Editor de ticket INTEGRADO (UserControl, sin ventana propia) con cuadrícula 2x2:
    ///   [procesada | controles]
    ///   [original  | datos    ]
    /// y barra inferior con acciones independientes:
    ///   - Reprocesar imagen: aplica solo los controles (brillo/contraste...) en LOCAL.
    ///   - Reescanear solo datos: llama a Gemini y refresca el panel de datos.
    ///   - Guardar / Cancelar (disparan el evento Cerrado).
    /// Ambas vistas de imagen tienen zoom (rueda), arrastre y doble clic para resetear.
    /// Reutilizable: no depende de Form1; recibe los paneles y delegados por constructor.
    /// </summary>
    public class EditorTicketPanel : UserControl
    {
        private readonly Mat _original;
        private readonly Func<Mat> _reprocesarLocal;
        private readonly Func<Mat, Task> _reescanearDatos;

        private readonly VisorZoomPictureBox _visorProcesada = new VisorZoomPictureBox { Dock = DockStyle.Fill, Etiqueta = "Procesada" };
        private readonly VisorZoomPictureBox _visorOriginal = new VisorZoomPictureBox { Dock = DockStyle.Fill, Etiqueta = "Original" };
        private readonly Button _btnReprocesar = new Button { Text = "Reprocesar imagen", AutoSize = true };
        private readonly Button _btnReescanear = new Button { Text = "Reescanear solo datos", AutoSize = true };
        private readonly Button _btnGuardar = new Button { Text = "Guardar", AutoSize = true };
        private readonly Button _btnCancelar = new Button { Text = "Cancelar", AutoSize = true };

        /// <summary>Se dispara al pulsar Guardar o Cancelar.</summary>
        public event EventHandler<ResultadoEditor>? Cerrado;

        /// <summary>Imagen procesada actual (propiedad del editor; se libera en Dispose).</summary>
        public Mat? ImagenProcesada { get; private set; }

        /// <summary>
        /// Crea el editor.
        /// </summary>
        /// <param name="original">Foto original (solo lectura; NO se libera aquí).</param>
        /// <param name="procesadaInicial">Imagen procesada inicial (el editor pasa a ser su dueño).</param>
        /// <param name="panelControles">Control con sliders de brillo/contraste... (cuadrante superior derecho).</param>
        /// <param name="panelDatos">Control con los datos editables (cuadrante inferior derecho).</param>
        /// <param name="reprocesarLocal">Lee los controles y devuelve la nueva imagen procesada (sin red).</param>
        /// <param name="reescanearDatos">Reescanea con Gemini sobre la imagen indicada y actualiza panelDatos.</param>
        public EditorTicketPanel(Mat original, Mat procesadaInicial, Control panelControles, Control panelDatos,
                                 Func<Mat> reprocesarLocal, Func<Mat, Task> reescanearDatos)
        {
            _original = original;
            ImagenProcesada = procesadaInicial;
            _reprocesarLocal = reprocesarLocal;
            _reescanearDatos = reescanearDatos;

            Dock = DockStyle.Fill;

            var rejilla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            rejilla.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            rejilla.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            rejilla.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            panelControles.Dock = DockStyle.Fill;
            panelDatos.Dock = DockStyle.Fill;
            rejilla.Controls.Add(_visorProcesada, 0, 0);
            rejilla.Controls.Add(panelControles, 1, 0);
            rejilla.Controls.Add(_visorOriginal, 0, 1);
            rejilla.Controls.Add(panelDatos, 1, 1);

            // Barra de botones: acciones a la izquierda, Guardar/Cancelar a la derecha.
            var barra = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var izq = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            izq.Controls.Add(_btnReprocesar);
            izq.Controls.Add(_btnReescanear);
            // WrapContents=false: evita que Cancelar/Guardar se apilen en dos filas.
            var der = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            der.Controls.Add(_btnCancelar);
            der.Controls.Add(_btnGuardar);
            barra.Controls.Add(izq, 0, 0);
            barra.Controls.Add(der, 1, 0);
            rejilla.Controls.Add(barra, 0, 2);
            rejilla.SetColumnSpan(barra, 2);
            Controls.Add(rejilla);

            _btnReprocesar.Click += (s, e) => ReprocesarImagen();
            _btnReescanear.Click += async (s, e) => await ReescanearDatosAsync();
            _btnGuardar.Click += (s, e) => Cerrado?.Invoke(this, ResultadoEditor.Guardar);
            _btnCancelar.Click += (s, e) => Cerrado?.Invoke(this, ResultadoEditor.Cancelar);

            MostrarImagenes(refrescarOriginal: true);
        }

        /// <summary>Libera la imagen procesada y los bitmaps mostrados al destruir el control.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _visorProcesada.Imagen?.Dispose();
                _visorOriginal.Imagen?.Dispose();
                ImagenProcesada?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>Refresca los visores liberando los Bitmap anteriores (el zoom actual se conserva).</summary>
        private void MostrarImagenes(bool refrescarOriginal)
        {
            var antP = _visorProcesada.Imagen;
            _visorProcesada.Imagen = ImagenProcesada != null ? ImageProcessor.MatToBitmap(ImagenProcesada) : null;
            antP?.Dispose();

            if (refrescarOriginal)
            {
                var antO = _visorOriginal.Imagen;
                _visorOriginal.Imagen = ImageProcessor.MatToBitmap(_original);
                antO?.Dispose();
            }
        }

        /// <summary>Aplica solo los controles de imagen en local (sin llamar a Gemini).</summary>
        private void ReprocesarImagen()
        {
            try
            {
                var nueva = _reprocesarLocal();
                ImagenProcesada?.Dispose();
                ImagenProcesada = nueva;
                MostrarImagenes(refrescarOriginal: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo reprocesar la imagen:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Reescanea los datos con Gemini sobre la imagen procesada actual, sin tocar la imagen.</summary>
        private async Task ReescanearDatosAsync()
        {
            if (ImagenProcesada == null) return;
            _btnReescanear.Enabled = false;
            try
            {
                await _reescanearDatos(ImagenProcesada);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo reescanear:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnReescanear.Enabled = true;
            }
        }
    }
}
