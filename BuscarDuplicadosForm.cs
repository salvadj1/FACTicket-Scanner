using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Formulario para buscar facturas potencialmente duplicadas en disco,
    // según criterios seleccionables (fecha, importe, empresa, número).
    // Muestra los resultados agrupados con acciones Ver / Editar / Eliminar.
    // -----------------------------------------------------------------------
    public class BuscarDuplicadosForm : Form
    {
        private readonly string _carpetaTickets;

        private CheckBox chkFecha = new() { Text = "Coincidir fecha", Checked = true, AutoSize = true };
        private CheckBox chkImporte = new() { Text = "Coincidir importe", Checked = true, AutoSize = true };
        private CheckBox chkEmpresa = new() { Text = "Coincidir empresa", AutoSize = true };
        private CheckBox chkNumero = new() { Text = "Coincidir número de factura", AutoSize = true };

        private Button btnBuscar = new() { Text = "🔎  Buscar", Height = 34 };
        private Button btnCancelar = new() { Text = "Cancelar", Height = 34 };

        private DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = System.Drawing.Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = System.Drawing.Color.FromArgb(230, 230, 230)
        };

        private Label lblEstado = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };

        private readonly List<(DatosTicket ticket, string rutaJson)> _todasLasFacturas = new();

        public BuscarDuplicadosForm(string? carpetaTickets = null)
        {
            _carpetaTickets = carpetaTickets ?? Path.Combine(AppContext.BaseDirectory, "Facturas");

            Text = "Buscar facturas duplicadas";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(760, 520);
            MinimumSize = new System.Drawing.Size(640, 400);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            BackColor = System.Drawing.Color.White;

            ConstruirUi();
            ConfigurarGrid();
        }

        private void ConstruirUi()
        {
            var panelSuperior = new Panel { Dock = DockStyle.Top, Height = 96, Padding = new Padding(14, 12, 14, 8) };
            panelSuperior.BackColor = System.Drawing.Color.FromArgb(248, 249, 250);

            var lblTitulo = new Label
            {
                Text = "Criterios de coincidencia",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Location = new System.Drawing.Point(14, 8)
            };

            var panelChecks = new FlowLayoutPanel
            {
                Location = new System.Drawing.Point(14, 32),
                Size = new System.Drawing.Size(720, 30),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };
            foreach (var chk in new[] { chkImporte, chkFecha, chkEmpresa, chkNumero })
            {
                chk.Margin = new Padding(0, 0, 24, 0);
                panelChecks.Controls.Add(chk);
            }

            var panelBotones = new FlowLayoutPanel
            {
                Location = new System.Drawing.Point(14, 62),
                Size = new System.Drawing.Size(300, 34),
                FlowDirection = FlowDirection.LeftToRight
            };
            btnBuscar.Width = 120;
            btnBuscar.BackColor = System.Drawing.Color.SeaGreen;
            btnBuscar.ForeColor = System.Drawing.Color.White;
            btnBuscar.FlatStyle = FlatStyle.Flat;
            btnBuscar.FlatAppearance.BorderSize = 0;
            btnBuscar.Click += BtnBuscar_Click;

            btnCancelar.Width = 100;
            btnCancelar.Click += (s, e) => Close();

            panelBotones.Controls.Add(btnBuscar);
            panelBotones.Controls.Add(btnCancelar);

            panelSuperior.Controls.Add(lblTitulo);
            panelSuperior.Controls.Add(panelChecks);
            panelSuperior.Controls.Add(panelBotones);

            var panelInferior = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(14, 4, 14, 4) };
            lblEstado.Location = new System.Drawing.Point(14, 6);
            panelInferior.Controls.Add(lblEstado);

            Controls.Add(grid);
            Controls.Add(panelInferior);
            Controls.Add(panelSuperior);
        }

        private void ConfigurarGrid()
        {
            grid.Columns.Clear();
            grid.Columns.Add("Empresa", "Empresa");
            grid.Columns.Add("Fecha", "Fecha");
            grid.Columns.Add("Numero", "Nº Factura");
            grid.Columns.Add("Total", "Total");
            grid.Columns.Add("Guardado", "Guardada el");

            var colVer = new DataGridViewButtonColumn { Name = "Ver", HeaderText = "", Text = "👁 Ver", UseColumnTextForButtonValue = true, Width = 70 };
            var colEditar = new DataGridViewButtonColumn { Name = "Editar", HeaderText = "", Text = "✏ Editar", UseColumnTextForButtonValue = true, Width = 80 };
            var colEliminar = new DataGridViewButtonColumn { Name = "Eliminar", HeaderText = "", Text = "🗑 Eliminar", UseColumnTextForButtonValue = true, Width = 90 };
            grid.Columns.Add(colVer);
            grid.Columns.Add(colEditar);
            grid.Columns.Add(colEliminar);

            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            grid.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            grid.ColumnHeadersHeight = 34;
            grid.EnableHeadersVisualStyles = false;
            grid.RowTemplate.Height = 32;
            grid.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(250, 250, 251);
            grid.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(232, 240, 254);
            grid.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;

            grid.CellContentClick += Grid_CellContentClick;
        }

        // -----------------------------------------------------------------------
        // Escanea todas las facturas en disco (igual criterio que ExportarForm)
        // -----------------------------------------------------------------------
        private void CargarFacturasDesdeDisco()
        {
            _todasLasFacturas.Clear();
            if (!Directory.Exists(_carpetaTickets)) return;

            foreach (var rutaJson in Directory.GetFiles(_carpetaTickets, "datos.json", SearchOption.AllDirectories))
            {
                var t = DatosTicket.CargarUnico(rutaJson);
                if (t == null) continue;
                _todasLasFacturas.Add((t, rutaJson));
            }
        }

        private void BtnBuscar_Click(object? sender, EventArgs e)
        {
            if (!chkFecha.Checked && !chkImporte.Checked && !chkEmpresa.Checked && !chkNumero.Checked)
            {
                MessageBox.Show("Selecciona al menos un criterio de coincidencia.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CargarFacturasDesdeDisco();

            string Clave((DatosTicket ticket, string rutaJson) x)
            {
                string k = "";
                if (chkFecha.Checked) k += "|F:" + (x.ticket.Fecha ?? "").Trim().ToLowerInvariant();
                if (chkImporte.Checked) k += "|T:" + (x.ticket.Total ?? "").Trim().ToLowerInvariant();
                if (chkEmpresa.Checked) k += "|E:" + (x.ticket.Empresa ?? "").Trim().ToLowerInvariant();
                if (chkNumero.Checked) k += "|N:" + (x.ticket.Numero ?? "").Trim().ToLowerInvariant();
                return k;
            }

            var grupos = _todasLasFacturas
                .GroupBy(Clave)
                .Where(g => g.Key.Length > 0 && g.Count() > 1)
                .OrderBy(g => g.First().ticket.Empresa)
                .ToList();

            grid.Rows.Clear();
            grid.Tag = null;
            var filas = new List<(DatosTicket ticket, string rutaJson)>();

            foreach (var grupo in grupos)
            {
                foreach (var item in grupo.OrderBy(x => x.ticket.FechaGuardado))
                {
                    filas.Add(item);
                    int idx = grid.Rows.Add(
                        item.ticket.Empresa,
                        item.ticket.Fecha,
                        string.IsNullOrWhiteSpace(item.ticket.Numero) ? "(sin número)" : item.ticket.Numero,
                        item.ticket.Total,
                        item.ticket.FechaGuardado);
                }
            }

            grid.Tag = filas;
            lblEstado.Text = grupos.Count == 0
                ? "No se han encontrado facturas duplicadas con los criterios seleccionados."
                : $"{grupos.Count} grupo(s) de posibles duplicados – {filas.Count} factura(s) en total.";
        }

        private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (grid.Tag is not List<(DatosTicket ticket, string rutaJson)> filas) return;
            if (e.RowIndex >= filas.Count) return;

            var (ticket, rutaJson) = filas[e.RowIndex];
            string nombreColumna = grid.Columns[e.ColumnIndex].Name;

            switch (nombreColumna)
            {
                case "Ver":
                    AccionVer(ticket, rutaJson);
                    break;
                case "Editar":
                    AccionEditar(ticket, rutaJson);
                    break;
                case "Eliminar":
                    AccionEliminar(ticket, rutaJson);
                    break;
            }
        }

        // -----------------------------------------------------------------------
        // Acciones por fila: se dejan como puntos de extensión; el enganche
        // real con el visor / edición / borrado se hace desde Form1 según
        // convenga (evita duplicar aquí lógica ya existente en Form1).
        // -----------------------------------------------------------------------
        private void AccionVer(DatosTicket ticket, string rutaJson)
        {
            string carpeta = Path.GetDirectoryName(rutaJson) ?? "";
            string rutaImagen = Path.Combine(carpeta, string.IsNullOrEmpty(ticket.ImagenRelativa)
                ? "" : Path.GetFileName(ticket.ImagenRelativa));

            if (File.Exists(rutaImagen))
            {
                Process.Start(new ProcessStartInfo { FileName = rutaImagen, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("No se encontró la imagen de esta factura.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void AccionEditar(DatosTicket ticket, string rutaJson)
        {
            MessageBox.Show(
                $"Editar factura:\nEmpresa: {ticket.Empresa}\nNº: {ticket.Numero}\n\n" +
                "Enganchar aquí con el flujo de edición del visor (Form1).",
                "Editar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AccionEliminar(DatosTicket ticket, string rutaJson)
        {
            var confirmar = MessageBox.Show(
                $"¿Eliminar definitivamente esta factura?\n\nEmpresa: {ticket.Empresa}\nNº: {ticket.Numero}\nFecha: {ticket.Fecha}",
                "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmar != DialogResult.Yes) return;

            try
            {
                string carpeta = Path.GetDirectoryName(rutaJson) ?? "";
                if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
                BtnBuscar_Click(null, EventArgs.Empty); // refresca la búsqueda
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
