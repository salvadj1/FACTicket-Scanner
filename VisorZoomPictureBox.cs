using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    /// <summary>
    /// Visor de imagen reutilizable con zoom y desplazamiento:
    ///   - Rueda del ratón: zoom centrado en el cursor.
    ///   - Arrastrar con botón izquierdo: mover la imagen (si hay zoom).
    ///   - Doble clic: restablecer zoom y posición.
    /// No es propietario de la imagen: quien la asigna es quien la libera.
    /// </summary>
    public class VisorZoomPictureBox : Control
    {
        private const float ZoomMin = 1.0f;
        private const float ZoomMax = 8.0f;
        private const float ZoomPaso = 1.15f;

        private Image? _imagen;
        private float _zoom = 1.0f;
        private PointF _pan = PointF.Empty;      // desplazamiento respecto a la imagen centrada
        private bool _arrastrando;
        private Point _origenArrastre;
        private PointF _panInicial;

        /// <summary>Texto pequeño opcional mostrado en la esquina (p. ej. "Original").</summary>
        public string Etiqueta { get; set; } = "";

        /// <summary>Crea el visor con doble búfer para evitar parpadeo al hacer zoom.</summary>
        public VisorZoomPictureBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(40, 40, 40);
            TabStop = true;
        }

        /// <summary>
        /// Imagen mostrada. Al cambiarla se conserva el zoom actual (útil al reprocesar);
        /// el llamador sigue siendo responsable de liberar la imagen anterior.
        /// </summary>
        public Image? Imagen
        {
            get => _imagen;
            set { _imagen = value; AjustarPan(); Invalidate(); }
        }

        /// <summary>Restablece zoom y posición.</summary>
        public void ResetearZoom()
        {
            _zoom = 1.0f;
            _pan = PointF.Empty;
            Invalidate();
        }

        /// <summary>Escala que hace que la imagen quepa entera en el control (zoom 1).</summary>
        private float EscalaAjustada()
        {
            if (_imagen == null || _imagen.Width == 0 || _imagen.Height == 0) return 1f;
            return Math.Min((float)Width / _imagen.Width, (float)Height / _imagen.Height);
        }

        /// <summary>Rectángulo donde se dibuja la imagen con el zoom y pan actuales.</summary>
        private RectangleF RectImagen()
        {
            if (_imagen == null) return RectangleF.Empty;
            float esc = EscalaAjustada() * _zoom;
            float w = _imagen.Width * esc, h = _imagen.Height * esc;
            return new RectangleF((Width - w) / 2f + _pan.X, (Height - h) / 2f + _pan.Y, w, h);
        }

        /// <summary>Evita que la imagen se pueda sacar del área visible.</summary>
        private void AjustarPan()
        {
            if (_imagen == null) { _pan = PointF.Empty; return; }
            float esc = EscalaAjustada() * _zoom;
            float maxX = Math.Max(0, (_imagen.Width * esc - Width) / 2f);
            float maxY = Math.Max(0, (_imagen.Height * esc - Height) / 2f);
            _pan = new PointF(Math.Min(maxX, Math.Max(-maxX, _pan.X)), Math.Min(maxY, Math.Max(-maxY, _pan.Y)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            if (_imagen != null)
            {
                g.InterpolationMode = _zoom > 2f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_imagen, RectImagen());
            }
            if (!string.IsNullOrEmpty(Etiqueta))
            {
                using var fuente = new Font(Font.FontFamily, 8f);
                g.DrawString(Etiqueta, fuente, Brushes.White, 6, 4);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); }   // la rueda necesita foco

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_imagen == null) return;

            float antes = _zoom;
            float nuevo = e.Delta > 0 ? _zoom * ZoomPaso : _zoom / ZoomPaso;
            nuevo = Math.Min(ZoomMax, Math.Max(ZoomMin, nuevo));
            if (Math.Abs(nuevo - antes) < 0.001f) return;

            // Punto de la imagen (en píxeles de la imagen) que está bajo el cursor.
            RectangleF r = RectImagen();
            float escAntes = EscalaAjustada() * antes;
            float px = (e.X - r.X) / escAntes, py = (e.Y - r.Y) / escAntes;

            _zoom = nuevo;
            float escDespues = EscalaAjustada() * _zoom;
            float w = _imagen.Width * escDespues, h = _imagen.Height * escDespues;
            // Nueva posición para mantener ese punto bajo el cursor.
            _pan = new PointF(e.X - px * escDespues - (Width - w) / 2f, e.Y - py * escDespues - (Height - h) / 2f);
            AjustarPan();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _zoom <= ZoomMin) return;
            _arrastrando = true;
            _origenArrastre = e.Location;
            _panInicial = _pan;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_arrastrando) return;
            _pan = new PointF(_panInicial.X + e.X - _origenArrastre.X, _panInicial.Y + e.Y - _origenArrastre.Y);
            AjustarPan();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _arrastrando = false;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            ResetearZoom();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AjustarPan();
        }
    }
}
