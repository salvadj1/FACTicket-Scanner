using System;
using System.Drawing;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Etiqueta REUTILIZABLE para mostrar cuentas atrás de forma muy visible:
    // fuente grande y en negrita, parpadeo suave y, cuando quedan pocos
    // segundos, parpadeo más rápido y en rojo.
    //
    // No depende de nada de la aplicación: solo de System.Windows.Forms.
    //
    // Uso típico (en cada tick de 1 s del temporizador de la cuenta atrás):
    //   var lbl = new CuentaAtrasVisual();             // 16 pt por defecto
    //   lbl.Actualizar($"Se cerrará en {restantes}s...", restantes);
    // Al cancelar o terminar:
    //   lbl.Detener("Cuenta atrás cancelada.");
    // -----------------------------------------------------------------------
    internal sealed class CuentaAtrasVisual : Label
    {
        private readonly System.Windows.Forms.Timer _parpadeo = new System.Windows.Forms.Timer();
        private bool _fase = true;
        private bool _urgente;

        /// <summary>Con este número de segundos (o menos) pasa a modo urgente.</summary>
        public int SegundosUrgente { get; set; } = 10;

        /// <summary>Color del texto en modo normal.</summary>
        public Color ColorNormal { get; set; } = Color.DarkOrange;

        /// <summary>Color del texto en modo urgente (últimos segundos).</summary>
        public Color ColorUrgente { get; set; } = Color.Firebrick;

        /// <summary>Milisegundos entre cambios de fase en modo normal.</summary>
        public int IntervaloNormalMs { get; set; } = 700;

        /// <summary>Milisegundos entre cambios de fase en modo urgente.</summary>
        public int IntervaloUrgenteMs { get; set; } = 250;

        // tamanoFuente: tamaño en puntos del texto mientras la cuenta atrás está activa.
        public CuentaAtrasVisual(float tamanoFuente = 16f)
        {
            Font = new Font("Segoe UI", tamanoFuente, FontStyle.Bold);
            AutoSize = false;
            TextAlign = ContentAlignment.MiddleLeft;
            Height = (int)Math.Ceiling(tamanoFuente * 2.6f);   // alto suficiente para la fuente
            ForeColor = ColorNormal;
            _parpadeo.Tick += (s, e) =>
            {
                _fase = !_fase;
                Color c = _urgente ? ColorUrgente : ColorNormal;
                ForeColor = _fase ? c : Atenuar(c);
            };
        }

        // -------------------------------------------------------------------
        // Muestra el texto y (re)configura el parpadeo según los segundos
        // restantes. Llamar en cada tick de la cuenta atrás.
        // -------------------------------------------------------------------
        public void Actualizar(string texto, int segundosRestantes)
        {
            Text = texto;
            _urgente = segundosRestantes <= SegundosUrgente;
            _parpadeo.Interval = _urgente ? IntervaloUrgenteMs : IntervaloNormalMs;
            if (!_parpadeo.Enabled)
            {
                _fase = true;
                ForeColor = _urgente ? ColorUrgente : ColorNormal;
                _parpadeo.Start();
            }
        }

        // -------------------------------------------------------------------
        // Para el parpadeo y deja un texto discreto (gris, fuente normal).
        // textoFinal null = conserva el texto actual.
        // -------------------------------------------------------------------
        public void Detener(string? textoFinal = null)
        {
            _parpadeo.Stop();
            ForeColor = Color.DimGray;
            if (Math.Abs(Font.Size - 10f) > 0.01f)          // solo reemplaza la fuente si hace falta
            {
                Font anterior = Font;
                Font = new Font(anterior.FontFamily, 10f, FontStyle.Bold);
                anterior.Dispose();
            }
            if (textoFinal != null) Text = textoFinal;
        }

        // Mezcla un color con blanco (~75 %) para la fase "apagada" del parpadeo,
        // de modo que el texto sigue siendo legible mientras pulsa.
        private static Color Atenuar(Color c) =>
            Color.FromArgb(255, c.R + (255 - c.R) * 3 / 4, c.G + (255 - c.G) * 3 / 4, c.B + (255 - c.B) * 3 / 4);

        protected override void Dispose(bool disposing)
        {
            if (disposing) _parpadeo.Dispose();
            base.Dispose(disposing);
        }
    }

    // -----------------------------------------------------------------------
    // Hace parpadear el color de fondo de CUALQUIER control (p. ej. un botón
    // con cuenta atrás en su texto) entre su color original y uno de aviso.
    // Al detenerlo restaura los colores originales. REUTILIZABLE.
    //
    // Uso:
    //   var p = new ParpadeoControl(btn, Color.Orange);  p.Iniciar();
    //   ...
    //   p.Dispose();   // detiene y restaura
    // -----------------------------------------------------------------------
    internal sealed class ParpadeoControl : IDisposable
    {
        private readonly Control _control;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Color _colorAviso;
        private readonly Color _fondoOriginal;
        private readonly Color _textoOriginal;
        private readonly bool _estiloVisualOriginal;
        private bool _fase;

        public ParpadeoControl(Control control, Color colorAviso, int intervaloMs = 350)
        {
            _control = control;
            _colorAviso = colorAviso;
            _fondoOriginal = control.BackColor;
            _textoOriginal = control.ForeColor;
            _estiloVisualOriginal = (control as ButtonBase)?.UseVisualStyleBackColor ?? true;
            _timer = new System.Windows.Forms.Timer { Interval = intervaloMs };
            _timer.Tick += (s, e) =>
            {
                _fase = !_fase;
                _control.BackColor = _fase ? _colorAviso : _fondoOriginal;
                _control.ForeColor = _fase ? Color.Black : _textoOriginal;
            };
        }

        /// <summary>Empieza a parpadear.</summary>
        public void Iniciar()
        {
            if (_control is ButtonBase b) b.UseVisualStyleBackColor = false;   // para que BackColor se vea
            _timer.Start();
        }

        /// <summary>Detiene el parpadeo y restaura los colores originales.</summary>
        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            if (_control.IsDisposed) return;
            _control.BackColor = _fondoOriginal;
            _control.ForeColor = _textoOriginal;
            if (_control is ButtonBase b) b.UseVisualStyleBackColor = _estiloVisualOriginal;
        }
    }
}
