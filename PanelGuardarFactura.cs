using System;
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

        private bool _construido = false;

        public PanelGuardarFactura()
        {
            Height = 84;
            HandleCreated += (s, e) => ConstruirUi();
            SizeChanged += (s, e) => { if (_construido) ConstruirUi(); };
            CreateControl();
        }

        private void ConstruirUi()
        {
            _construido = true;
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

            // Fila: 4 checkboxes en una sola línea
            int wChk = (wTotal - 24) / 4;
            chkGuardarOriginal = new CheckBox { Left = 0, Top = 50, Width = wChk, Height = 20, Text = "Original", Checked = true };
            chkGuardarJpg = new CheckBox { Left = wChk + 8, Top = 50, Width = wChk, Height = 20, Text = "Jpg procesado", Checked = true };
            chkGuardarPdf = new CheckBox { Left = (wChk + 8) * 2, Top = 50, Width = wChk, Height = 20, Text = "Pdf procesado", Checked = true };
            chkExtraerGemini = new CheckBox { Left = (wChk + 8) * 3, Top = 50, Width = wChk, Height = 20, Text = "Datos (Gemini)", Checked = true };
            Controls.Add(chkGuardarOriginal);
            Controls.Add(chkGuardarJpg);
            Controls.Add(chkGuardarPdf);
            Controls.Add(chkExtraerGemini);
        }
    }
}