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
        // Ancho con el que se construyeron los sliders la última vez. Sirve
        // para no reconstruir en CUALQUIER SizeChanged (p.ej. al aparecer/
        // desaparecer la barra de scroll), sino solo cuando el ancho
        // disponible cambia de verdad (redimensionar la ventana).
        private int _anchoConstruido = -1;

        public PanelAjustesEscaneo()
        {
            // AutoScroll = true; // ELIMINADO: el padre (panelScrollable) ya
            // tiene AutoScroll propio. Tenerlo también aquí generaba un
            // cálculo de ancho incorrecto (bucle de scroll anidado) que
            // hacía que los sliders se dibujaran más anchos que el panel.
            HandleCreated += (s, e) => ConstruirSliders();
            SizeChanged += (s, e) =>
            {
                // No reconstruir mientras el usuario tiene el ratón pulsado
                // (arrastrando un slider): eso destruía el TrackBar bajo el
                // cursor a mitad de arrastre, dejándolo "huérfano" y sin
                // reflejar el cambio real. Sí reconstruir en cualquier otro
                // cambio de tamaño real (redimensionar ventana, aparición
                // de scrollbar, etc.) para que el layout siga ajustándose.
                if (_construido && Control.MouseButtons == MouseButtons.None)
                    ConstruirSliders();
            };
            CreateControl();
        }

        private void ConstruirSliders()
        {
            // Si ya existían trackbars (reconstrucción por SizeChanged), se
            // guardan sus valores actuales para no perderlos al recrearlos.
            bool haviaValores = _construido && trkBlock != null;
            int vBlock = haviaValores ? trkBlock.Value : 25;
            int vC = haviaValores ? trkC.Value : 10;
            int vContraste = haviaValores ? trkContraste.Value : 2;
            int vBrillo = haviaValores ? trkBrillo.Value : 0;
            int vRuido = haviaValores ? trkRuido.Value : 1;
            int vNitidez = haviaValores ? trkNitidez.Value : 0;
            int vGrueso = haviaValores ? trkGrueso.Value : 0;
            int vUmbral = haviaValores ? trkUmbral.Value : 0;
            int vMargen = haviaValores ? trkMargen.Value : 5;
            int vMargenSup = haviaValores ? trkMargenSup.Value : 0;
            int vMargenInf = haviaValores ? trkMargenInf.Value : 0;
            int vMargenIzq = haviaValores ? trkMargenIzq.Value : 0;
            int vMargenDer = haviaValores ? trkMargenDer.Value : 0;
            bool vEdicionManual = haviaValores && chkEdicionManual != null && chkEdicionManual.Checked;

            _construido = true;
            _anchoConstruido = ClientSize.Width;
            Controls.Clear();

            int wTotal = ClientSize.Width - 16;
            if (wTotal < 200) wTotal = 200;

            int y = 8;
            (trkBlock, valBlock) = Slider("Detalle:", y, 1, 40, vBlock, 2); y += 26;
            (trkC, valC) = Slider("Brillo/C:", y, -20, 20, vC, 4); y += 26;
            (trkContraste, valContraste) = Slider("CLAHE (clipLimit):", y, 0, 8, vContraste, 1); y += 26;
            (trkBrillo, valBrillo) = Slider("Brillo imagen:", y, -50, 50, vBrillo, 10); y += 26;
            (trkRuido, valRuido) = Slider("Reducir ruido:", y, 0, 4, vRuido, 1); y += 26;
            (trkNitidez, valNitidez) = Slider("Nitidez (post):", y, 0, 3, vNitidez, 1); y += 26;
            (trkGrueso, valGrueso) = Slider("Grosor texto:", y, -3, 3, vGrueso, 1); y += 26;
            (trkUmbral, valUmbral) = Slider("Umbral fijo:", y, 0, 254, vUmbral, 20); y += 26;
            (trkMargen, valMargen) = Slider("Sensib. recorte:", y, 1, 30, vMargen, 5); y += 26;
            (trkMargenSup, valMargenSup) = Slider("Margen sup. (%):", y, 0, 50, vMargenSup, 5); y += 26;
            (trkMargenInf, valMargenInf) = Slider("Margen inf. (%):", y, 0, 50, vMargenInf, 5); y += 26;
            (trkMargenIzq, valMargenIzq) = Slider("Margen izq. (%):", y, 0, 50, vMargenIzq, 5); y += 26;
            (trkMargenDer, valMargenDer) = Slider("Margen der. (%):", y, 0, 50, vMargenDer, 5); y += 26;

            chkEdicionManual = new CheckBox
            {
                Left = 0,
                Top = y,
                Width = wTotal,
                AutoSize = false,
                Height = 22,
                Text = "Edición manual (sin recorte automático)",
                Checked = vEdicionManual
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