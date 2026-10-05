using System;
using System.Linq;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // UserControl con el botón Guardar y sus checkboxes de opciones
    // (original / jpg / pdf / extraer con Gemini). Se aloja en panelBotones.
    // -----------------------------------------------------------------------
    public class PanelGuardarFactura : UserControl
    {
        public Button btnRotar = null!;
        public Button btnRepetir = null!;
        public Button btnGuardar = null!;
        public Button btnSaltar = null!;
        public Button btnCancelarAuto = null!;
        public Button btnSalirLote = null!;
        public Label lblProgresoLote = null!;
        public CheckBox chkGuardarOriginal = null!;
        public CheckBox chkGuardarJpg = null!;
        public CheckBox chkGuardarPdf = null!;
        public CheckBox chkExtraerGemini = null!;

        // Reglas de verificación de duplicados (siempre visibles, independientes de Gemini)
        public CheckBox chkRegNumero = null!;
        public CheckBox chkRegFecha = null!;
        public CheckBox chkRegTotal = null!;
        public CheckBox chkRegEmpresa = null!;
        private Label lblTituloArchivos = null!;
        private Label lblTituloReglas = null!;

        // Se lanza al cambiar alguna regla de duplicados (ya validada).
        public event EventHandler? ReglasCambiadas;

        // Alto fijo del panel: fila de botones + archivos a guardar + reglas de duplicados.
        private const int ALTURA_PANEL = 126;

        private bool _construido = false;
        private int _anchoConstruido = -1;
        private bool _ajustandoReglas = false;
        private bool _regNumero = true, _regFecha = true, _regTotal = true, _regEmpresa = false;
        private readonly ToolTip _tip = new ToolTip();

        public PanelGuardarFactura()
        {
            Height = ALTURA_PANEL;
            HandleCreated += (s, e) => ConstruirUi();
            SizeChanged += (s, e) => { if (_construido && Width != _anchoConstruido) ConstruirUi(); };
            CreateControl();
        }

        private void ConstruirUi()
        {
            _construido = true;
            _anchoConstruido = Width;

            // Conserva el estado de los checkboxes si el panel se reconstruye
            bool estOriginal = chkGuardarOriginal?.Checked ?? true;
            bool estJpg = chkGuardarJpg?.Checked ?? true;
            bool estPdf = chkGuardarPdf?.Checked ?? true;
            bool estGemini = chkExtraerGemini?.Checked ?? true;

            Controls.Clear();
            int wTotal = Width - 8;
            if (wTotal < 200) wTotal = 200;

            // Fila: [Rotar] [Repetir] | separador | [Guardar] [Saltar] [Cancelar auto] [Progreso] [Salir lote]
            const int wRotar = 90, wRepetir = 90, wSeparador = 2, wSaltar = 85, wCancelarAuto = 105,
                wProgreso = 65, wSalirLote = 115, margen = 6;

            btnRotar = new Button { Left = 0, Top = 0, Width = wRotar, Height = 40, Text = "↻ Rotar", Enabled = false };
            btnRepetir = new Button { Left = wRotar + margen, Top = 0, Width = wRepetir, Height = 40, Text = "🔁 Repetir", Enabled = false };

            var separador = new Panel
            {
                Left = wRotar + margen + wRepetir + margen,
                Top = 4,
                Width = wSeparador,
                Height = 32,
                BackColor = System.Drawing.Color.Gainsboro
            };

            int xDerecha = separador.Right + margen;
            int wGuardar = Math.Max(90, wTotal - xDerecha - (wSaltar + wCancelarAuto + wProgreso + wSalirLote + margen * 4));

            btnGuardar = new Button
            {
                Left = xDerecha,
                Top = 0,
                Width = wGuardar,
                Height = 40,
                Text = "💾  Guardar",
                Enabled = false,
                BackColor = System.Drawing.Color.SeaGreen,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaltar = new Button
            {
                Left = btnGuardar.Right + margen,
                Top = 0,
                Width = wSaltar,
                Height = 40,
                Text = "⏭ Saltar",
                Visible = false
            };
            btnCancelarAuto = new Button
            {
                Left = btnSaltar.Right + margen,
                Top = 0,
                Width = wCancelarAuto,
                Height = 40,
                Text = "✕ Cancelar auto",
                Visible = false
            };
            lblProgresoLote = new Label
            {
                Left = btnCancelarAuto.Right + margen,
                Top = 0,
                Width = wProgreso,
                Height = 40,
                Text = "",
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.SteelBlue,
                Visible = false
            };
            btnSalirLote = new Button
            {
                Left = lblProgresoLote.Right + margen,
                Top = 0,
                Width = wSalirLote,
                Height = 40,
                Text = "✖ Salir del lote",
                Visible = false,
                BackColor = System.Drawing.Color.IndianRed,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };

            Controls.Add(btnRotar);
            Controls.Add(btnRepetir);
            Controls.Add(separador);
            Controls.Add(btnGuardar);
            Controls.Add(btnSaltar);
            Controls.Add(btnCancelarAuto);
            Controls.Add(lblProgresoLote);
            Controls.Add(btnSalirLote);

            // Fila 1: título + checkboxes de archivos a guardar
            var fuenteTitulo = new System.Drawing.Font(Font.FontFamily, 8, System.Drawing.FontStyle.Bold);
            lblTituloArchivos = new Label { Left = 0, Top = 44, Width = wTotal, Height = 16, Text = "Archivos a guardar", Font = fuenteTitulo, ForeColor = System.Drawing.Color.DimGray };
            int wChk = (wTotal - 24) / 4;
            chkGuardarOriginal = new CheckBox { Left = 0, Top = 60, Width = wChk, Height = 20, Text = "Original", Checked = estOriginal };
            chkGuardarJpg = new CheckBox { Left = wChk + 8, Top = 60, Width = wChk, Height = 20, Text = "Jpg procesado", Checked = estJpg };
            chkGuardarPdf = new CheckBox { Left = (wChk + 8) * 2, Top = 60, Width = wChk, Height = 20, Text = "Pdf procesado", Checked = estPdf };
            chkExtraerGemini = new CheckBox { Left = (wChk + 8) * 3, Top = 60, Width = wChk, Height = 20, Text = "Datos (Gemini)", Checked = estGemini };

            // Fila 2: título + reglas de verificación de duplicados
            lblTituloReglas = new Label { Left = 0, Top = 86, Width = wTotal, Height = 16, Text = "Reglas Verificación Duplicados", Font = fuenteTitulo, ForeColor = System.Drawing.Color.DimGray };
            chkRegNumero = new CheckBox { Left = 0, Top = 102, Width = wChk, Height = 20, Text = "Nº factura", Checked = _regNumero };
            chkRegFecha = new CheckBox { Left = wChk + 8, Top = 102, Width = wChk, Height = 20, Text = "Fecha", Checked = _regFecha };
            chkRegTotal = new CheckBox { Left = (wChk + 8) * 2, Top = 102, Width = wChk, Height = 20, Text = "Total", Checked = _regTotal };
            chkRegEmpresa = new CheckBox { Left = (wChk + 8) * 3, Top = 102, Width = wChk, Height = 20, Text = "Nombre empresa", Checked = _regEmpresa };
            foreach (var c in new[] { chkRegNumero, chkRegFecha, chkRegTotal, chkRegEmpresa })
                c.CheckedChanged += AlCambiarRegla;
            _tip.SetToolTip(lblTituloReglas, "Es duplicado si coinciden TODAS las reglas marcadas (mínimo 2).");

            Controls.Add(lblTituloArchivos);
            Controls.Add(chkGuardarOriginal);
            Controls.Add(chkGuardarJpg);
            Controls.Add(chkGuardarPdf);
            Controls.Add(chkExtraerGemini);
            Controls.Add(lblTituloReglas);
            Controls.Add(chkRegNumero);
            Controls.Add(chkRegFecha);
            Controls.Add(chkRegTotal);
            Controls.Add(chkRegEmpresa);
        }

        // -------------------------------------------------------------------
        // Valida y registra un cambio en las reglas. Exige un mínimo de
        // ReglasDuplicados.MinimoReglas marcadas: si el cambio deja menos,
        // se revierte.
        // -------------------------------------------------------------------
        private void AlCambiarRegla(object? sender, EventArgs e)
        {
            if (_ajustandoReglas) return;

            int activas = new[] { chkRegNumero.Checked, chkRegFecha.Checked, chkRegTotal.Checked, chkRegEmpresa.Checked }.Count(b => b);
            if (activas < ReglasDuplicados.MinimoReglas)
            {
                _ajustandoReglas = true;
                ((CheckBox)sender!).Checked = true;
                _ajustandoReglas = false;
                return;
            }

            _regNumero = chkRegNumero.Checked;
            _regFecha = chkRegFecha.Checked;
            _regTotal = chkRegTotal.Checked;
            _regEmpresa = chkRegEmpresa.Checked;
            ReglasCambiadas?.Invoke(this, EventArgs.Empty);
        }

        // Devuelve las reglas de duplicados actualmente seleccionadas.
        public ReglasDuplicados ObtenerReglasDuplicados() => new ReglasDuplicados
        {
            Numero = _regNumero,
            Fecha = _regFecha,
            Total = _regTotal,
            Empresa = _regEmpresa
        };

        // Carga reglas (p. ej. desde ajustes.json) sin lanzar ReglasCambiadas.
        // Se ignoran si tienen menos del mínimo de reglas activas.
        public void AplicarReglasDuplicados(ReglasDuplicados reglas)
        {
            if (reglas.CantidadActivas < ReglasDuplicados.MinimoReglas) return;
            _regNumero = reglas.Numero;
            _regFecha = reglas.Fecha;
            _regTotal = reglas.Total;
            _regEmpresa = reglas.Empresa;

            if (!_construido || chkRegNumero == null) return;
            _ajustandoReglas = true;
            chkRegNumero.Checked = _regNumero;
            chkRegFecha.Checked = _regFecha;
            chkRegTotal.Checked = _regTotal;
            chkRegEmpresa.Checked = _regEmpresa;
            _ajustandoReglas = false;
        }
    }
}