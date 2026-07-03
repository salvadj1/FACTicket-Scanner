using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Formulario exclusivo para exportar documentos (PDF/JSON/JPG) a ZIP.
    // Se abre desde el botón "Exportar" del visor, ya pre-filtrado con el
    // año/trimestre/empresa/factura seleccionados en el visor web.
    // -----------------------------------------------------------------------
    public class ExportarForm : Form
    {
        private readonly string _carpetaTickets;
        private readonly string _anio, _trimestre, _empresa, _jsonFacturaAbierta;

        private RadioButton rbTrimestre = new() { Text = "Trimestre actual" };
        private RadioButton rbAnio = new() { Text = "Año actual" };
        private RadioButton rbEmpresa = new() { Text = "Empresa filtrada" };
        private RadioButton rbFactura = new() { Text = "Factura abierta en el visor" };

        private CheckBox chkPdf = new() { Text = "PDF", Checked = true };
        private CheckBox chkJson = new() { Text = "JSON", Checked = true };
        private CheckBox chkJpg = new() { Text = "JPG procesado", Checked = true };
        private CheckBox chkOriginal = new() { Text = "JPG original" };

        private Label lblEstado = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };
        private Button btnExportar = new() { Text = "Exportar a ZIP" };
        private Button btnCancelar = new() { Text = "Cancelar" };

        public ExportarForm(string carpetaTickets, string anio, string trimestre, string empresa, string jsonFacturaAbierta)
        {
            _carpetaTickets = carpetaTickets;
            _anio = anio; _trimestre = trimestre; _empresa = empresa; _jsonFacturaAbierta = jsonFacturaAbierta;

            Text = "Exportar documentos";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(340, 330);
            Font = new System.Drawing.Font("Segoe UI", 9F);

            ConstruirUi();

            // Preselección de ámbito según el contexto del visor
            if (!string.IsNullOrEmpty(_jsonFacturaAbierta)) rbFactura.Checked = true;
            else if (!string.IsNullOrEmpty(_empresa)) rbEmpresa.Checked = true;
            else if (!string.IsNullOrEmpty(_trimestre)) rbTrimestre.Checked = true;
            else rbAnio.Checked = true;

            rbFactura.Enabled = !string.IsNullOrEmpty(_jsonFacturaAbierta);
            rbEmpresa.Enabled = !string.IsNullOrEmpty(_empresa);
        }

        private void ConstruirUi()
        {
            var lblAmbito = new Label { Text = "ÁMBITO", AutoSize = true, ForeColor = System.Drawing.Color.SteelBlue, Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold), Location = new System.Drawing.Point(16, 14) };
            rbTrimestre.Location = new System.Drawing.Point(16, 36);
            rbAnio.Location = new System.Drawing.Point(16, 60);
            rbEmpresa.Location = new System.Drawing.Point(16, 84);
            rbFactura.Location = new System.Drawing.Point(16, 108);
            foreach (var rb in new[] { rbTrimestre, rbAnio, rbEmpresa, rbFactura }) rb.AutoSize = true;

            var lblIncluir = new Label { Text = "INCLUIR", AutoSize = true, ForeColor = System.Drawing.Color.SteelBlue, Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold), Location = new System.Drawing.Point(16, 146) };
            chkPdf.Location = new System.Drawing.Point(16, 168);
            chkJson.Location = new System.Drawing.Point(16, 192);
            chkJpg.Location = new System.Drawing.Point(16, 216);
            chkOriginal.Location = new System.Drawing.Point(16, 240);
            foreach (var c in new[] { chkPdf, chkJson, chkJpg, chkOriginal }) c.AutoSize = true;

            lblEstado.Location = new System.Drawing.Point(16, 270);
            lblEstado.MaximumSize = new System.Drawing.Size(310, 0);

            btnCancelar.Location = new System.Drawing.Point(140, 296);
            btnExportar.Location = new System.Drawing.Point(230, 296);
            btnCancelar.Click += (s, e) => Close();
            btnExportar.Click += async (s, e) => await ExportarAsync();

            Controls.AddRange(new Control[] {
                lblAmbito, rbTrimestre, rbAnio, rbEmpresa, rbFactura,
                lblIncluir, chkPdf, chkJson, chkJpg, chkOriginal,
                lblEstado, btnCancelar, btnExportar
            });
        }

        private string Ambito() =>
            rbFactura.Checked ? "factura" :
            rbEmpresa.Checked ? "empresa" :
            rbAnio.Checked ? "anio" : "trimestre";

        // Recorre todas las carpetas de factura (datos.json) y filtra según ámbito
        private List<DatosTicket> TicketsFiltrados()
        {
            var resultado = new List<DatosTicket>();
            if (!Directory.Exists(_carpetaTickets)) return resultado;

            foreach (var rutaJson in Directory.GetFiles(_carpetaTickets, "datos.json", SearchOption.AllDirectories))
            {
                var t = DatosTicket.CargarUnico(rutaJson);
                if (t == null) continue;

                switch (Ambito())
                {
                    case "factura":
                        if (RutaRelativa(rutaJson) == _jsonFacturaAbierta) resultado.Add(t);
                        break;
                    case "empresa":
                        if ((t.Empresa ?? "").Trim() == _empresa) resultado.Add(t);
                        break;
                    case "anio":
                        if (AnioDe(t) == _anio) resultado.Add(t);
                        break;
                    default: // trimestre
                        if (AnioDe(t) == _anio && (string.IsNullOrEmpty(_trimestre) || TrimestreDe(t) == _trimestre))
                            resultado.Add(t);
                        break;
                }
            }
            return resultado;
        }

        private string RutaRelativa(string rutaAbsoluta) =>
            Path.GetRelativePath(_carpetaTickets, rutaAbsoluta).Replace('\\', '/');

        private static string AnioDe(DatosTicket t)
        {
            var f = t.Fecha ?? "";
            return f.Length >= 4 ? f.Substring(0, 4) : "";
        }

        private static string TrimestreDe(DatosTicket t)
        {
            var f = t.Fecha ?? "";
            if (f.Length < 7 || !int.TryParse(f.Substring(5, 2), out int mes)) return "";
            return (((mes - 1) / 3) + 1).ToString();
        }

        private async System.Threading.Tasks.Task ExportarAsync()
        {
            var lista = TicketsFiltrados();
            if (!lista.Any())
            {
                lblEstado.Text = "No hay documentos para ese ámbito.";
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
                FileName = $"export_{Ambito()}_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            btnExportar.Enabled = false;
            lblEstado.Text = "Generando ZIP...";

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var zip = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create);
                    foreach (var t in lista)
                    {
                        AgregarSiExiste(zip, t.PdfRelativa, chkPdf.Checked);
                        AgregarSiExiste(zip, t.JsonRelativa, chkJson.Checked);
                        AgregarSiExiste(zip, t.ImagenRelativa, chkJpg.Checked);

                        if (chkOriginal.Checked)
                        {
                            string carpeta = Path.GetDirectoryName(t.JsonRelativa) ?? "";
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

        private void AgregarSiExiste(ZipArchive zip, string? rutaRelativa, bool incluir)
        {
            if (!incluir || string.IsNullOrWhiteSpace(rutaRelativa)) return;
            string rutaAbsoluta = Path.Combine(_carpetaTickets, rutaRelativa);
            if (File.Exists(rutaAbsoluta)) zip.CreateEntryFromFile(rutaAbsoluta, rutaRelativa);
        }
    }
}
