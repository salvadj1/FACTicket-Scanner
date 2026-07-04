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

            // Fila: [Rotar] [Repetir] [Guardar]
            int wBtn3 = (wTotal - 16) / 3;
            btnRotar = new Button
            {
                Left = 0,
                Top = 0,
                Width = wBtn3,
                Height = 40,
                Text = "↻  Rotar 90°",
                Enabled = false
            };
            btnRepetir = new Button
            {
                Left = wBtn3 + 8,
                Top = 0,
                Width = wBtn3,
                Height = 40,
                Text = "🔁  Repetir",
                Enabled = false
            };
            btnGuardar = new Button
            {
                Left = (wBtn3 + 8) * 2,
                Top = 0,
                Width = wTotal - (wBtn3 + 8) * 2,
                Height = 40,
                Text = "💾  Guardar",
                Enabled = false,
                BackColor = System.Drawing.Color.SeaGreen,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            Controls.Add(btnRotar);
            Controls.Add(btnRepetir);
            Controls.Add(btnGuardar);

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