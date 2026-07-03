using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Formulario de exportación autónomo: busca las facturas directamente
    // en disco (carpetas + datos.json), sin depender del visor web ni de
    // JavaScript. Filtra por rango de fechas y empresa, y permite marcar
    // manualmente qué facturas exportar.
    // -----------------------------------------------------------------------
    public class ExportarForm : Form
    {
        private readonly string _carpetaTickets;
        private readonly List<(DatosTicket ticket, string rutaJson, DateTime? fecha)> _todasLasFacturas = new();

        private DateTimePicker dtpDesde = new() { Format = DateTimePickerFormat.Short };
        private DateTimePicker dtpHasta = new() { Format = DateTimePickerFormat.Short };
        private ComboBox cmbEmpresa = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        private Button btnFiltrar = new() { Text = "Filtrar" };

        private CheckedListBox clbFacturas = new() { CheckOnClick = true, Dock = DockStyle.Fill };
        private Button btnMarcarTodas = new() { Text = "Marcar todas" };
        private Button btnDesmarcarTodas = new() { Text = "Desmarcar todas" };

        private CheckBox chkPdf = new() { Text = "PDF", Checked = true };
        private CheckBox chkJson = new() { Text = "JSON", Checked = true };
        private CheckBox chkJpg = new() { Text = "JPG procesado", Checked = true };
        private CheckBox chkOriginal = new() { Text = "JPG original" };

        private Label lblEstado = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };
        private Button btnExportar = new() { Text = "Exportar a ZIP" };
        private Button btnCancelar = new() { Text = "Cancelar" };

        private static readonly string[] FormatosFecha =
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "yyyy/MM/dd"
        };

        public ExportarForm(string? carpetaTickets = null)
        {
            _carpetaTickets = carpetaTickets ?? Path.Combine(AppContext.BaseDirectory, "Facturas");

            Text = "Exportar documentos";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(480, 480);
            MinimumSize = new System.Drawing.Size(420, 400);
            Font = new System.Drawing.Font("Segoe UI", 9F);

            ConstruirUi();
            CargarFacturasDesdeDisco();
            PoblarEmpresas();
            AplicarFiltro();
        }

        private void ConstruirUi()
        {
            var panelFiltros = new Panel { Dock = DockStyle.Top, Height = 92 };

            var lblDesde = new Label { Text = "Desde:", AutoSize = true, Location = new System.Drawing.Point(10, 12) };
            dtpDesde.Location = new System.Drawing.Point(60, 8);
            dtpDesde.Width = 100;

            var lblHasta = new Label { Text = "Hasta:", AutoSize = true, Location = new System.Drawing.Point(170, 12) };
            dtpHasta.Location = new System.Drawing.Point(220, 8);
            dtpHasta.Width = 100;

            var lblEmpresa = new Label { Text = "Empresa:", AutoSize = true, Location = new System.Drawing.Point(10, 42) };
            cmbEmpresa.Location = new System.Drawing.Point(70, 38);

            btnFiltrar.Location = new System.Drawing.Point(300, 8);
            btnFiltrar.Height = 30;
            btnFiltrar.Click += (s, e) => AplicarFiltro();

            panelFiltros.Controls.AddRange(new Control[] { lblDesde, dtpDesde, lblHasta, dtpHasta, lblEmpresa, cmbEmpresa, btnFiltrar });

            var panelBotonesLista = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, FlowDirection = FlowDirection.LeftToRight };
            btnMarcarTodas.Click += (s, e) => MarcarTodas(true);
            btnDesmarcarTodas.Click += (s, e) => MarcarTodas(false);
            panelBotonesLista.Controls.AddRange(new Control[] { btnMarcarTodas, btnDesmarcarTodas });

            var panelLista = new Panel { Dock = DockStyle.Fill };
            panelLista.Controls.Add(clbFacturas);
            panelLista.Controls.Add(panelBotonesLista);

            var panelIncluir = new Panel { Dock = DockStyle.Bottom, Height = 30 };
            chkPdf.Location = new System.Drawing.Point(10, 6);
            chkJson.Location = new System.Drawing.Point(80, 6);
            chkJpg.Location = new System.Drawing.Point(160, 6);
            chkOriginal.Location = new System.Drawing.Point(280, 6);
            foreach (var c in new[] { chkPdf, chkJson, chkJpg, chkOriginal }) c.AutoSize = true;
            panelIncluir.Controls.AddRange(new Control[] { chkPdf, chkJson, chkJpg, chkOriginal });

            var panelInferior = new Panel { Dock = DockStyle.Bottom, Height = 66 };
            lblEstado.Location = new System.Drawing.Point(10, 6);
            lblEstado.MaximumSize = new System.Drawing.Size(450, 0);
            btnCancelar.Location = new System.Drawing.Point(280, 28);
            btnExportar.Location = new System.Drawing.Point(370, 28);
            btnCancelar.Click += (s, e) => Close();
            btnExportar.Click += async (s, e) => await ExportarAsync();
            panelInferior.Controls.AddRange(new Control[] { lblEstado, btnCancelar, btnExportar });

            Controls.Add(panelLista);
            Controls.Add(panelInferior);
            Controls.Add(panelIncluir);
            Controls.Add(panelFiltros);
        }

        // -----------------------------------------------------------------------
        // Búsqueda pura en disco: recorre todas las carpetas buscando datos.json
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

        private void PoblarEmpresas()
        {
            cmbEmpresa.Items.Clear();
            cmbEmpresa.Items.Add("(Todas)");
            var empresas = _todasLasFacturas
                .Select(x => (x.ticket.Empresa ?? "").Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e);
            foreach (var e in empresas) cmbEmpresa.Items.Add(e);
            cmbEmpresa.SelectedIndex = 0;

            var fechas = _todasLasFacturas.Where(x => x.fecha.HasValue).Select(x => x.fecha!.Value).ToList();
            dtpDesde.Value = fechas.Any() ? fechas.Min() : DateTime.Today.AddMonths(-3);
            dtpHasta.Value = fechas.Any() ? fechas.Max() : DateTime.Today;
        }

        // -----------------------------------------------------------------------
        // Filtra por fecha + empresa y repuebla la lista marcable
        // -----------------------------------------------------------------------
        private void AplicarFiltro()
        {
            clbFacturas.Items.Clear();

            string empresaFiltro = cmbEmpresa.SelectedIndex > 0 ? cmbEmpresa.SelectedItem!.ToString()! : "";
            DateTime desde = dtpDesde.Value.Date;
            DateTime hasta = dtpHasta.Value.Date;

            var filtradas = _todasLasFacturas.Where(x =>
                (!x.fecha.HasValue || (x.fecha.Value.Date >= desde && x.fecha.Value.Date <= hasta)) &&
                (string.IsNullOrEmpty(empresaFiltro) ||
                 string.Equals((x.ticket.Empresa ?? "").Trim(), empresaFiltro, StringComparison.OrdinalIgnoreCase))
            ).OrderByDescending(x => x.fecha ?? DateTime.MinValue);

            foreach (var (ticket, rutaJson, _) in filtradas)
            {
                string etiqueta = $"{ticket.Empresa} | {ticket.Fecha} | {ticket.Numero} | {ticket.Total}";
                clbFacturas.Items.Add(new FacturaListItem(ticket, rutaJson, etiqueta), true);
            }

            lblEstado.Text = $"{clbFacturas.Items.Count} factura(s) encontradas.";
        }

        private void MarcarTodas(bool marcar)
        {
            for (int i = 0; i < clbFacturas.Items.Count; i++)
                clbFacturas.SetItemChecked(i, marcar);
        }

        private async System.Threading.Tasks.Task ExportarAsync()
        {
            var seleccionadas = clbFacturas.CheckedItems.Cast<FacturaListItem>().ToList();
            if (!seleccionadas.Any())
            {
                lblEstado.Text = "No hay facturas marcadas para exportar.";
                return;
            }
            if (!chkPdf.Checked && !chkJson.Checked && !chkJpg.Checked && !chkOriginal.Checked)
            {
                lblEstado.Text = "Selecciona al menos un tipo de archivo.";
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = "Archivo ZIP (*.zip)|*.zip",
                FileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            btnExportar.Enabled = false;
            lblEstado.Text = "Generando ZIP...";

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var zip = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create);
                    foreach (var item in seleccionadas)
                    {
                        var t = item.Ticket;
                        AgregarSiExiste(zip, t.PdfRelativa, chkPdf.Checked);
                        AgregarSiExiste(zip, t.JsonRelativa, chkJson.Checked);
                        AgregarSiExiste(zip, t.ImagenRelativa, chkJpg.Checked);

                        if (chkOriginal.Checked)
                        {
                            string carpeta = Path.GetDirectoryName(RutaRelativa(item.RutaJson)) ?? "";
                            AgregarSiExiste(zip, Path.Combine(carpeta, "original.jpg").Replace('\\', '/'), true);
                        }
                    }
                });

                lblEstado.Text = "Descarga completada.";
            }
            catch (Exception ex)
            {
                lblEstado.Text = "Error generando el ZIP: " + ex.Message;
            }
            finally
            {
                btnExportar.Enabled = true;
            }
        }

        private string RutaRelativa(string rutaAbsoluta) =>
            Path.GetRelativePath(_carpetaTickets, rutaAbsoluta).Replace('\\', '/');

        private void AgregarSiExiste(ZipArchive zip, string? rutaRelativa, bool incluir)
        {
            if (!incluir || string.IsNullOrWhiteSpace(rutaRelativa)) return;
            string rutaAbsoluta = Path.Combine(_carpetaTickets, rutaRelativa);
            if (File.Exists(rutaAbsoluta)) zip.CreateEntryFromFile(rutaAbsoluta, rutaRelativa);
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