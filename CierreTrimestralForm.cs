using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Formulario para el cierre de un trimestre fiscal: permite filtrar las
    // facturas guardadas en disco por rango de fechas, marcarlas/desmarcarlas
    // en bloque como "presentadas" en un trimestre concreto (campos
    // Presentado/TrimestrePresentado de DatosTicket), y guarda el cambio en
    // cada datos.json correspondiente. Reutiliza el mismo patrón de escaneo
    // en disco que BuscarDuplicadosForm/ExportarForm.
    // -----------------------------------------------------------------------
    public class CierreTrimestralForm : Form
    {
        private readonly string _carpetaTickets;
        private readonly List<(DatosTicket ticket, string rutaJson, DateTime? fecha)> _todasLasFacturas = new();

        private DateTimePicker dtpDesde = new() { Format = DateTimePickerFormat.Short };
        private DateTimePicker dtpHasta = new() { Format = DateTimePickerFormat.Short };
        private ComboBox cmbAnio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        private ComboBox cmbTrimestre = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
        private Button btnFiltrar = new() { Text = "Filtrar" };
        // Desmarcado por defecto: si viniera marcado, las facturas ya
        // presentadas quedarían ocultas sin que se note, dando la falsa
        // impresión de que ya no se pueden modificar/desmarcar.
        private CheckBox chkSoloNoPresentadas = new() { Text = "Solo no presentadas", Checked = false, AutoSize = true };

        private CheckedListBox clbFacturas = new() { CheckOnClick = true, Dock = DockStyle.Fill };
        private Button btnMarcarTodas = new() { Text = "Marcar todas" };
        private Button btnDesmarcarTodas = new() { Text = "Desmarcar todas" };

        private Label lblEstado = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };
        private Button btnCerrarTrimestre = new() { Text = "✔ Cerrar trimestre con las marcadas" };
        private Button btnQuitarPresentado = new() { Text = "Quitar marca de presentado" };
        private Button btnCancelar = new() { Text = "Cerrar ventana" };

        private static readonly string[] FormatosFecha =
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "yyyy/MM/dd"
        };

        public CierreTrimestralForm(string? carpetaTickets = null)
        {
            _carpetaTickets = carpetaTickets ?? Path.Combine(AppContext.BaseDirectory, "Facturas");

            Text = "Cierre de trimestre";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(760, 560);
            MinimumSize = new System.Drawing.Size(640, 420);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            BackColor = System.Drawing.Color.White;

            ConstruirUi();
            CargarFacturasDesdeDisco();
            PoblarAnios();
            AplicarFiltro();
        }

        private void ConstruirUi()
        {
            var panelSuperior = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(14, 10, 14, 8) };
            panelSuperior.BackColor = System.Drawing.Color.FromArgb(248, 249, 250);

            var lblTitulo = new Label
            {
                Text = "Filtrar facturas por fecha y trimestre destino",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Location = new System.Drawing.Point(0, 0)
            };

            var lblDesde = new Label { Text = "Desde:", AutoSize = true, Location = new System.Drawing.Point(0, 30) };
            dtpDesde.Location = new System.Drawing.Point(50, 26);
            dtpDesde.Width = 100;

            var lblHasta = new Label { Text = "Hasta:", AutoSize = true, Location = new System.Drawing.Point(160, 30) };
            dtpHasta.Location = new System.Drawing.Point(210, 26);
            dtpHasta.Width = 100;

            btnFiltrar.Location = new System.Drawing.Point(320, 24);
            btnFiltrar.Height = 28;
            btnFiltrar.Click += (s, e) => AplicarFiltro();

            chkSoloNoPresentadas.Location = new System.Drawing.Point(410, 30);
            chkSoloNoPresentadas.CheckedChanged += (s, e) => AplicarFiltro();

            var lblDestino = new Label { Text = "Marcar como presentadas en:", AutoSize = true, Location = new System.Drawing.Point(0, 68) };
            cmbAnio.Location = new System.Drawing.Point(200, 64);
            cmbTrimestre.Location = new System.Drawing.Point(296, 64);
            cmbTrimestre.Items.AddRange(new object[] { "1T", "2T", "3T", "4T" });
            cmbTrimestre.SelectedIndex = ((DateTime.Today.Month - 1) / 3);

            panelSuperior.Controls.AddRange(new Control[]
            {
                lblTitulo, lblDesde, dtpDesde, lblHasta, dtpHasta, btnFiltrar, chkSoloNoPresentadas,
                lblDestino, cmbAnio, cmbTrimestre
            });

            var panelBotonesLista = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, FlowDirection = FlowDirection.LeftToRight };
            btnMarcarTodas.Click += (s, e) => MarcarTodas(true);
            btnDesmarcarTodas.Click += (s, e) => MarcarTodas(false);
            panelBotonesLista.Controls.AddRange(new Control[] { btnMarcarTodas, btnDesmarcarTodas });

            var panelLista = new Panel { Dock = DockStyle.Fill };
            panelLista.Controls.Add(clbFacturas);
            panelLista.Controls.Add(panelBotonesLista);

            var panelInferior = new Panel { Dock = DockStyle.Bottom, Height = 74 };
            lblEstado.Location = new System.Drawing.Point(14, 6);
            lblEstado.MaximumSize = new System.Drawing.Size(730, 0);

            btnCancelar.Location = new System.Drawing.Point(14, 30);
            btnCancelar.Width = 120;
            btnCancelar.Click += (s, e) => Close();

            btnQuitarPresentado.Location = new System.Drawing.Point(300, 30);
            btnQuitarPresentado.Width = 190;
            btnQuitarPresentado.Click += async (s, e) => await AplicarMarcadoAsync(presentar: false);

            btnCerrarTrimestre.Location = new System.Drawing.Point(500, 30);
            btnCerrarTrimestre.Width = 246;
            btnCerrarTrimestre.Height = 30;
            btnCerrarTrimestre.BackColor = System.Drawing.Color.SeaGreen;
            btnCerrarTrimestre.ForeColor = System.Drawing.Color.White;
            btnCerrarTrimestre.FlatStyle = FlatStyle.Flat;
            btnCerrarTrimestre.Click += async (s, e) => await AplicarMarcadoAsync(presentar: true);

            panelInferior.Controls.AddRange(new Control[] { lblEstado, btnCancelar, btnQuitarPresentado, btnCerrarTrimestre });

            Controls.Add(panelLista);
            Controls.Add(panelInferior);
            Controls.Add(panelSuperior);
        }

        // -----------------------------------------------------------------------
        // Escanea todas las facturas en disco (mismo criterio que ExportarForm
        // y BuscarDuplicadosForm: cualquier datos.json bajo _carpetaTickets)
        // -----------------------------------------------------------------------
        private void CargarFacturasDesdeDisco()
        {
            _todasLasFacturas.Clear();
            if (!Directory.Exists(_carpetaTickets)) return;

            foreach (var rutaJson in Directory.GetFiles(_carpetaTickets, "datos.json", SearchOption.AllDirectories))
            {
                var t = DatosTicket.CargarUnico(rutaJson);
                if (t == null) continue;
                _todasLasFacturas.Add((t, rutaJson, ParsearFecha(t.Fecha)));
            }
        }

        private static DateTime? ParsearFecha(string? fecha)
        {
            if (string.IsNullOrWhiteSpace(fecha)) return null;
            if (DateTime.TryParseExact(fecha.Trim(), FormatosFecha, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime f))
                return f;
            if (DateTime.TryParse(fecha.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out f))
                return f;
            return null;
        }

        private void PoblarAnios()
        {
            var fechas = _todasLasFacturas.Where(x => x.fecha.HasValue).Select(x => x.fecha!.Value).ToList();
            dtpDesde.Value = fechas.Any() ? fechas.Min() : DateTime.Today.AddMonths(-3);
            dtpHasta.Value = fechas.Any() ? fechas.Max() : DateTime.Today;

            cmbAnio.Items.Clear();
            int anioActual = DateTime.Today.Year;
            for (int a = anioActual + 1; a >= anioActual - 5; a--)
                cmbAnio.Items.Add(a);
            cmbAnio.SelectedItem = anioActual;
        }

        // -----------------------------------------------------------------------
        // Filtra por rango de fechas (y opcionalmente solo no presentadas) y
        // repuebla la lista marcable
        // -----------------------------------------------------------------------
        private void AplicarFiltro()
        {
            clbFacturas.Items.Clear();

            DateTime desde = dtpDesde.Value.Date;
            DateTime hasta = dtpHasta.Value.Date;

            var filtradas = _todasLasFacturas.Where(x =>
                (!x.fecha.HasValue || (x.fecha.Value.Date >= desde && x.fecha.Value.Date <= hasta)) &&
                (!chkSoloNoPresentadas.Checked || !x.ticket.Presentado)
            ).OrderBy(x => (x.ticket.Empresa ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
             .ThenBy(x => x.fecha ?? DateTime.MinValue);

            foreach (var (ticket, rutaJson, _) in filtradas)
            {
                string estado = ticket.Presentado ? $" [ya: {ticket.TrimestrePresentado}]" : "";
                string etiqueta = $"{ticket.Empresa} | {ticket.Fecha} | {ticket.Numero} | {ticket.Total}{estado}";
                clbFacturas.Items.Add(new FacturaListItem(ticket, rutaJson, etiqueta), ticket.Presentado); // ya presentadas -> marcadas
            }

            lblEstado.Text = $"{clbFacturas.Items.Count} factura(s) en el rango seleccionado.";
        }

        private void MarcarTodas(bool marcar)
        {
            for (int i = 0; i < clbFacturas.Items.Count; i++)
                clbFacturas.SetItemChecked(i, marcar);
        }

        // -----------------------------------------------------------------------
        // Aplica (o retira) la marca de presentado a las facturas marcadas en
        // la lista, reescribiendo cada datos.json en disco.
        // -----------------------------------------------------------------------
        private async System.Threading.Tasks.Task AplicarMarcadoAsync(bool presentar)
        {
            var seleccionadas = clbFacturas.CheckedItems.Cast<FacturaListItem>().ToList();
            // Al presentar se ignoran las ya presentadas (vienen marcadas por defecto)
            // para no sobrescribir el trimestre en el que se presentaron.
            if (presentar)
                seleccionadas = seleccionadas.Where(x => !x.Ticket.Presentado).ToList();
            if (!seleccionadas.Any())
            {
                lblEstado.Text = presentar
                    ? "No hay facturas nuevas marcadas (las ya presentadas se ignoran)."
                    : "No hay facturas marcadas en la lista.";
                return;
            }

            string etiquetaTrimestre = "";
            if (presentar)
            {
                if (cmbAnio.SelectedItem == null)
                {
                    lblEstado.Text = "Elige un año de destino.";
                    return;
                }
                etiquetaTrimestre = $"{cmbAnio.SelectedItem}-{cmbTrimestre.SelectedItem}";

                var confirmar = MessageBox.Show(
                    $"Se marcarán {seleccionadas.Count} factura(s) como presentadas en {etiquetaTrimestre}.\n\n¿Continuar?",
                    "Confirmar cierre de trimestre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirmar != DialogResult.Yes) return;
            }
            else
            {
                var confirmar = MessageBox.Show(
                    $"Se quitará la marca de presentado a {seleccionadas.Count} factura(s).\n\n¿Continuar?",
                    "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirmar != DialogResult.Yes) return;
            }

            btnCerrarTrimestre.Enabled = false;
            btnQuitarPresentado.Enabled = false;
            int ok = 0, error = 0;

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var item in seleccionadas)
                {
                    try
                    {
                        item.Ticket.Presentado = presentar;
                        item.Ticket.TrimestrePresentado = presentar ? etiquetaTrimestre : "";
                        DatosTicket.GuardarUnico(item.RutaJson, item.Ticket);
                        ok++;
                    }
                    catch
                    {
                        error++;
                    }
                }
            });

            btnCerrarTrimestre.Enabled = true;
            btnQuitarPresentado.Enabled = true;
            lblEstado.Text = presentar
                ? $"Marcadas {ok} factura(s) como presentadas en {etiquetaTrimestre}. Errores: {error}."
                : $"Desmarcadas {ok} factura(s). Errores: {error}.";

            CargarFacturasDesdeDisco();
            AplicarFiltro();
        }

        private class FacturaListItem
        {
            public DatosTicket Ticket { get; }
            public string RutaJson { get; }
            private readonly string _etiqueta;

            public FacturaListItem(DatosTicket ticket, string rutaJson, string etiqueta)
            {
                Ticket = ticket;
                RutaJson = rutaJson;
                _etiqueta = etiqueta;
            }

            public override string ToString() => _etiqueta;
        }
    }
}