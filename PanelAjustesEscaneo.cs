using System;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // UserControl con todos los sliders de ajuste de procesado de imagen.
    // Se muestra en panelScrollable, visible desde el arranque (antes de
    // cargar cualquier imagen). Expone los TrackBar/CheckBox públicos para
    // que Form1 lea sus valores igual que antes, y un evento ValorCambiado
    // para disparar el reprocesado en tiempo real.
    // -----------------------------------------------------------------------
    public class PanelAjustesEscaneo : UserControl
    {
        public TrackBar trkBlock = null!;
        public TrackBar trkC = null!;
        public TrackBar trkContraste = null!;
        public TrackBar trkBrillo = null!;
        public TrackBar trkRuido = null!;
        public TrackBar trkNitidez = null!;
        public TrackBar trkGrueso = null!;
        public TrackBar trkUmbral = null!;
        public TrackBar trkMargen = null!;
        public TrackBar trkMargenSup = null!;
        public TrackBar trkMargenInf = null!;
        public TrackBar trkMargenIzq = null!;
        public TrackBar trkMargenDer = null!;
        public CheckBox chkEdicionManual = null!;

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

        // Se dispara cada vez que cambia cualquier slider o chkEdicionManual.
        public event EventHandler? ValorCambiado;

        private bool _construido = false;

        public PanelAjustesEscaneo()
        {
            AutoScroll = true;
            HandleCreated += (s, e) => ConstruirSliders();
            SizeChanged += (s, e) => { if (_construido) ConstruirSliders(); };
            CreateControl();
        }

        private void ConstruirSliders()
        {
            _construido = true;
            Controls.Clear();

            int wTotal = ClientSize.Width - 16;
            if (wTotal < 200) wTotal = 200;

            int y = 8;
            (trkBlock, valBlock) = Slider("Detalle:", y, 1, 40, 25, 2); y += 26;
            (trkC, valC) = Slider("Brillo/C:", y, -20, 20, 10, 4); y += 26;
            (trkContraste, valContraste) = Slider("CLAHE (clipLimit):", y, 0, 8, 2, 1); y += 26;
            (trkBrillo, valBrillo) = Slider("Brillo imagen:", y, -50, 50, 0, 10); y += 26;
            (trkRuido, valRuido) = Slider("Reducir ruido:", y, 0, 4, 1, 1); y += 26;
            (trkNitidez, valNitidez) = Slider("Nitidez (post):", y, 0, 3, 0, 1); y += 26;
            (trkGrueso, valGrueso) = Slider("Grosor texto:", y, -3, 3, 0, 1); y += 26;
            (trkUmbral, valUmbral) = Slider("Umbral fijo:", y, 0, 254, 0, 20); y += 26;
            (trkMargen, valMargen) = Slider("Sensib. recorte:", y, 1, 30, 5, 5); y += 26;
            (trkMargenSup, valMargenSup) = Slider("Margen sup. (%):", y, 0, 50, 0, 5); y += 26;
            (trkMargenInf, valMargenInf) = Slider("Margen inf. (%):", y, 0, 50, 0, 5); y += 26;
            (trkMargenIzq, valMargenIzq) = Slider("Margen izq. (%):", y, 0, 50, 0, 5); y += 26;
            (trkMargenDer, valMargenDer) = Slider("Margen der. (%):", y, 0, 50, 0, 5); y += 26;

            chkEdicionManual = new CheckBox
            {
                Left = 0,
                Top = y,
                Width = wTotal,
                AutoSize = false,
                Height = 22,
                Text = "Edición manual (sin recorte automático)"
            };
            chkEdicionManual.CheckedChanged += (s, e) => ValorCambiado?.Invoke(this, EventArgs.Empty);
            Controls.Add(chkEdicionManual);
            y += 26;

            Controls.Add(new Label
            {
                Left = 0,
                Top = y,
                Width = wTotal,
                Height = 30,
                AutoSize = false,
                Text = "CLAHE/Brillo/Ruido se auto-calibran al capturar.",
                ForeColor = System.Drawing.Color.Gray,
                Font = new System.Drawing.Font(Font.FontFamily, 7.5f)
            });

            ActualizarEtiquetas();

            foreach (TrackBar t in new[] { trkBlock, trkC, trkContraste, trkBrillo, trkRuido,
                trkNitidez, trkGrueso, trkUmbral, trkMargen,
                trkMargenSup, trkMargenInf, trkMargenIzq, trkMargenDer })
            {
                t.ValueChanged += (s, e) =>
                {
                    ActualizarEtiquetas();
                    ValorCambiado?.Invoke(this, EventArgs.Empty);
                };
            }
        }

        private (TrackBar trk, Label val) Slider(string texto, int y, int min, int max, int valor, int tick)
        {
            int wTotal = ClientSize.Width - 16;
            if (wTotal < 200) wTotal = 200;
            int wLbl = 130, wVal = 48, wTrk = wTotal - wLbl - wVal - 4;

            Controls.Add(new Label
            {
                Left = 0,
                Top = y + 4,
                Width = wLbl,
                Text = texto,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight
            });

            var trk = new TrackBar
            {
                Left = wLbl + 2,
                Top = y,
                Width = wTrk,
                Height = 24,
                Minimum = min,
                Maximum = max,
                Value = Math.Min(max, Math.Max(min, valor)),
                TickFrequency = tick,
                AutoSize = false
            };
            Controls.Add(trk);

            var lbl = new Label { Left = wLbl + wTrk + 4, Top = y + 4, Width = wVal };
            Controls.Add(lbl);

            return (trk, lbl);
        }

        public void ActualizarEtiquetas()
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
    }
}