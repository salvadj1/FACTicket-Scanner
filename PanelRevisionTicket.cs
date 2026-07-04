using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    public class RevisionCompletadaEventArgs : EventArgs
    {
        public DatosTicket? Resultado { get; }
        public RevisionCompletadaEventArgs(DatosTicket? resultado) => Resultado = resultado;
    }

    // -----------------------------------------------------------------------
    // UserControl que sustituye al antiguo Form modal de revisión de datos
    // extraídos por Gemini. Se muestra en panelScrollable (ocultando los
    // sliders mientras tanto) con cuenta atrás propia: por defecto Guardar
    // en 5s si el usuario no interactúa.
    // -----------------------------------------------------------------------
    public class PanelRevisionTicket : UserControl
    {
        private readonly List<TextBox> _textBoxes = new();
        private ComboBox _cmbTipo = null!;
        private DatosTicket? _datosActuales;
        private readonly Timer _timer = new() { Interval = 1000 };
        private int _restantes;
        private Label _lblContador = null!;

        public event EventHandler<RevisionCompletadaEventArgs>? RevisionCompletada;

        public PanelRevisionTicket()
        {
            AutoScroll = true;
            Visible = false;
        }

        public void Mostrar(DatosTicket datos)
        {
            _datosActuales = datos;
            ConstruirUi(datos);
            Visible = true;
            BringToFront();

            _restantes = Form1.Timeout_Dialogos;
            _lblContador.Text = $"Se guardará automáticamente en {_restantes}s...";
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _restantes--;
            if (_restantes <= 0)
            {
                _timer.Stop();
                Finalizar(guardar: true);
                return;
            }
            _lblContador.Text = $"Se guardará automáticamente en {_restantes}s...";
        }

        private void ConstruirUi(DatosTicket datos)
        {
            Controls.Clear();
            _textBoxes.Clear();

            int y = 6, xLbl = 10, wLbl = 150, xTxt = 170, wTxt = 270, rowH = 24;

            // Tipo de documento (factura/albaran/ticket), detectado por Gemini
            // pero editable por si se equivoca. Determina dónde se guarda.
            Controls.Add(new Label { Text = "Tipo de documento:", Left = xLbl, Top = y + 3, Width = wLbl });
            _cmbTipo = new ComboBox { Left = xTxt, Top = y, Width = wTxt, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTipo.Items.AddRange(new object[] { "factura", "albaran", "ticket" });
            _cmbTipo.SelectedItem = string.IsNullOrWhiteSpace(datos.TipoDocumento) ? "factura" : datos.TipoDocumento;
            if (_cmbTipo.SelectedIndex < 0) _cmbTipo.SelectedIndex = 0;
            Controls.Add(_cmbTipo);
            y += rowH;

            var campos = new List<(string etiqueta, string valor)>
            {
                ("Empresa (emisor):",     datos.Empresa),
                ("Fecha emisión:",        datos.Fecha),
                ("Fecha vencimiento:",    datos.FechaVencimiento),
                ("Nº Factura:",           datos.Numero),
                ("CIF/NIF emisor:",       datos.Cif),
                ("Dirección emisor:",     datos.Direccion),
                ("Teléfono:",             datos.Telefono),
                ("Receptor nombre:",      datos.ReceptorNombre),
                ("Receptor CIF/NIF:",     datos.ReceptorCif),
                ("Receptor dirección:",   datos.ReceptorDireccion),
                ("Base imponible:",       datos.Base),
                ("IVA:",                  datos.Iva),
                ("Total:",                datos.Total),
                ("Método de pago:",       datos.MetodoPago),
            };

            foreach (var (etiqueta, valor) in campos)
            {
                Controls.Add(new Label { Text = etiqueta, Left = xLbl, Top = y + 3, Width = wLbl });
                var txt = new TextBox { Left = xTxt, Top = y, Width = wTxt, Text = valor };
                Controls.Add(txt);
                _textBoxes.Add(txt);
                y += rowH;
            }

            if (datos.Items.Count > 0)
            {
                Controls.Add(new Label
                {
                    Text = $"Líneas ({datos.Items.Count}):",
                    Left = xLbl,
                    Top = y + 3,
                    Width = wLbl,
                    ForeColor = System.Drawing.Color.DarkSlateBlue
                });
                var txtItems = new TextBox
                {
                    Left = xTxt,
                    Top = y,
                    Width = wTxt,
                    Height = 56,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    ReadOnly = true,
                    Text = string.Join("\r\n", datos.Items.Select(
                        it => $"{it.Descripcion} x{it.Cantidad} = {it.Subtotal}"))
                };
                Controls.Add(txtItems);
                y += 62;
            }

            var btnOk = new Button { Text = "Guardar", Left = xLbl, Top = y, Width = 120, Height = 30 };
            var btnCan = new Button { Text = "Cancelar", Left = xLbl + 170, Top = y, Width = 120, Height = 30 };
            _lblContador = new Label { Left = xLbl, Top = y + 34, Width = 290, ForeColor = System.Drawing.Color.DimGray, Font = new System.Drawing.Font(Font.FontFamily, 10, System.Drawing.FontStyle.Bold) };
            var btnCancelarTimer = new Button { Left = xLbl + 300, Top = y + 32, Width = 22, Height = 22, Text = "✕", FlatStyle = FlatStyle.Flat };
            btnCancelarTimer.Click += (s, e) => { _timer.Stop(); _lblContador.Text = ""; btnCancelarTimer.Visible = false; };
            Controls.Add(btnOk);
            Controls.Add(btnCan);
            Controls.Add(_lblContador);
            Controls.Add(btnCancelarTimer);

            btnOk.Click += (s, e) => Finalizar(guardar: true);
            btnCan.Click += (s, e) => Finalizar(guardar: false);
        }

        private void Finalizar(bool guardar)
        {
            _timer.Stop();
            Visible = false;

            if (!guardar || _datosActuales == null)
            {
                RevisionCompletada?.Invoke(this, new RevisionCompletadaEventArgs(null));
                return;
            }

            var datos = _datosActuales;
            datos.TipoDocumento = _cmbTipo.SelectedItem?.ToString() ?? "factura";
            datos.Empresa = _textBoxes[0].Text.Trim();
            datos.Fecha = _textBoxes[1].Text.Trim();
            datos.FechaVencimiento = _textBoxes[2].Text.Trim();
            datos.Numero = _textBoxes[3].Text.Trim();
            datos.Cif = _textBoxes[4].Text.Trim();
            datos.Direccion = _textBoxes[5].Text.Trim();
            datos.Telefono = _textBoxes[6].Text.Trim();
            datos.ReceptorNombre = _textBoxes[7].Text.Trim();
            datos.ReceptorCif = _textBoxes[8].Text.Trim();
            datos.ReceptorDireccion = _textBoxes[9].Text.Trim();
            datos.Base = _textBoxes[10].Text.Trim();
            datos.Iva = _textBoxes[11].Text.Trim();
            datos.Total = _textBoxes[12].Text.Trim();
            datos.MetodoPago = _textBoxes[13].Text.Trim();

            RevisionCompletada?.Invoke(this, new RevisionCompletadaEventArgs(datos));
        }
    }
}