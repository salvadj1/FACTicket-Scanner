using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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

        // --- Filtros avanzados (panel plegable bajo la barra de filtros) ---
        private Button btnAvanzados = new() { Text = "Filtros avanzados  ▼", Width = 190, Height = 28 };
        private TableLayoutPanel panelAvanzado = new()
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Visible = false, ColumnCount = 6, Padding = new Padding(8, 2, 8, 6)
        };
        private ComboBox cmbPresentacion = NuevoCombo();
        private ComboBox cmbTrimestre = NuevoCombo();
        private ComboBox cmbPago = NuevoCombo();
        private ComboBox cmbIva = NuevoCombo();
        private ComboBox cmbVencimiento = NuevoCombo();
        private ComboBox cmbPdf = NuevoCombo();
        private ComboBox cmbImagen = NuevoCombo();
        private TextBox txtImporteMin = new() { Dock = DockStyle.Fill };
        private TextBox txtImporteMax = new() { Dock = DockStyle.Fill };
        private TextBox txtTexto = new() { Dock = DockStyle.Fill };
        private CheckBox chkTipoFactura = new() { Text = "Facturas", Checked = true, AutoSize = true };
        private CheckBox chkTipoAlbaran = new() { Text = "Albaranes", Checked = true, AutoSize = true };
        private CheckBox chkTipoTicket = new() { Text = "Tickets", Checked = true, AutoSize = true };
        private CheckBox chkSinFecha = new() { Text = "Incluir documentos sin fecha", Checked = true, AutoSize = true };
        private Button btnLimpiarAvanzados = new() { Text = "Limpiar filtros avanzados", AutoSize = true };

        // Evitan reentradas: _cargandoLista mientras se repuebla la lista;
        // _suspenderFiltro mientras se restablecen varios controles a la vez.
        private bool _cargandoLista;
        private bool _suspenderFiltro;

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

        // Preferencias de exportación (formato preseleccionado, carpeta inicial y
        // abrir carpeta al terminar). Si es null se usan los valores clásicos.
        private readonly AjustesEscaner? _ajustes;

        public ExportarForm(string? carpetaTickets = null, AjustesEscaner? ajustes = null)
        {
            _carpetaTickets = carpetaTickets ?? Path.Combine(AppContext.BaseDirectory, "Facturas");
            _ajustes = ajustes;

            Text = "Exportar documentos";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(660, 640);
            MinimumSize = new System.Drawing.Size(560, 480);
            Font = new System.Drawing.Font("Segoe UI", 9F);

            ConstruirUi();
            AplicarFormatoPorDefecto();
            CargarFacturasDesdeDisco();
            PoblarEmpresas();
            PoblarFiltrosAvanzados();
            ConectarEventosFiltros();
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

            btnAvanzados.Location = new System.Drawing.Point(300, 36);
            btnAvanzados.Click += (s, e) =>
            {
                panelAvanzado.Visible = !panelAvanzado.Visible;
                RefrescarBotonAvanzados();
            };
            ConstruirPanelAvanzado();

            panelFiltros.Controls.AddRange(new Control[] { lblDesde, dtpDesde, lblHasta, dtpHasta, lblEmpresa, cmbEmpresa, btnFiltrar, btnAvanzados });

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
            btnExportar.Width = 110;
            btnCancelar.Top = btnExportar.Top = 28;
            // Botones siempre pegados a la derecha, aunque se redimensione la ventana.
            void AlinearInferior()
            {
                btnExportar.Left = panelInferior.ClientSize.Width - btnExportar.Width - 12;
                btnCancelar.Left = btnExportar.Left - btnCancelar.Width - 8;
                lblEstado.MaximumSize = new System.Drawing.Size(Math.Max(100, panelInferior.ClientSize.Width - 20), 0);
            }
            panelInferior.Resize += (s, e) => AlinearInferior();
            Load += (s, e) => AlinearInferior();
            clbFacturas.ItemCheck += (s, e) =>
            {
                // CheckedItems aún no refleja el cambio dentro del evento: se actualiza después.
                if (!_cargandoLista && IsHandleCreated) BeginInvoke(new Action(ActualizarResumen));
            };
            btnCancelar.Click += (s, e) => Close();
            btnExportar.Click += async (s, e) => await ExportarAsync();
            panelInferior.Controls.AddRange(new Control[] { lblEstado, btnCancelar, btnExportar });

            Controls.Add(panelLista);
            Controls.Add(panelInferior);
            Controls.Add(panelIncluir);
            Controls.Add(panelAvanzado);   // antes que panelFiltros: queda justo debajo de él
            Controls.Add(panelFiltros);
            AcceptButton = btnFiltrar;
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
                _todasLasFacturas.Add((t, rutaJson, FiltrosExportacion.ParsearFecha(t.Fecha)));
            }
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
        // Crea un desplegable de solo selección que rellena su celda.
        // -----------------------------------------------------------------------
        private static ComboBox NuevoCombo() =>
            new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };

        // Sustituye los elementos del desplegable y selecciona el primero.
        private static void Llenar(ComboBox combo, params string[] items)
        {
            combo.Items.Clear();
            combo.Items.AddRange(items);
            combo.SelectedIndex = 0;
        }

        // Añade a la rejilla del panel avanzado una etiqueta (columna) y su
        // control (columna + 1), opcionalmente ocupando varias columnas.
        private void Celda(string etiqueta, Control control, int columna, int fila, int colSpan = 1)
        {
            panelAvanzado.Controls.Add(new Label
            {
                Text = etiqueta, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 6)
            }, columna, fila);
            panelAvanzado.Controls.Add(control, columna + 1, fila);
            if (colSpan > 1) panelAvanzado.SetColumnSpan(control, colSpan);
        }

        // -----------------------------------------------------------------------
        // Construye la rejilla del panel de filtros avanzados (3 pares
        // etiqueta/control por fila). Los desplegables dinámicos (trimestre,
        // pago, IVA) se rellenan después en PoblarFiltrosAvanzados().
        // -----------------------------------------------------------------------
        private void ConstruirPanelAvanzado()
        {
            for (int i = 0; i < 3; i++)
            {
                panelAvanzado.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                panelAvanzado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            }

            Llenar(cmbPresentacion, "(Todas)", "Presentadas", "No presentadas");
            Llenar(cmbVencimiento, "(Todas)", "Vencidas", "No vencidas", "Sin vencimiento");
            Llenar(cmbPdf, "(Indiferente)", "Con PDF", "Sin PDF");
            Llenar(cmbImagen, "(Indiferente)", "Con imagen", "Sin imagen");

            Celda("Presentación:", cmbPresentacion, 0, 0);
            Celda("Trimestre:", cmbTrimestre, 2, 0);
            Celda("Pago:", cmbPago, 4, 0);
            Celda("Importe desde:", txtImporteMin, 0, 1);
            Celda("hasta:", txtImporteMax, 2, 1);
            Celda("IVA:", cmbIva, 4, 1);
            Celda("Vencimiento:", cmbVencimiento, 0, 2);
            Celda("PDF:", cmbPdf, 2, 2);
            Celda("Imagen:", cmbImagen, 4, 2);

            var tipos = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            tipos.Controls.AddRange(new Control[] { chkTipoFactura, chkTipoAlbaran, chkTipoTicket });
            Celda("Tipo:", tipos, 0, 3, 5);

            Celda("Texto:", txtTexto, 0, 4, 3);
            panelAvanzado.Controls.Add(chkSinFecha, 4, 4);
            panelAvanzado.SetColumnSpan(chkSinFecha, 2);

            panelAvanzado.Controls.Add(btnLimpiarAvanzados, 1, 5);
        }

        // -----------------------------------------------------------------------
        // Rellena los desplegables que dependen de los datos existentes
        // (trimestres presentados, métodos de pago y tipos de IVA).
        // -----------------------------------------------------------------------
        private void PoblarFiltrosAvanzados()
        {
            Llenar(cmbTrimestre, new[] { "(Todos)" }.Concat(_todasLasFacturas
                .Select(x => (x.ticket.TrimestrePresentado ?? "").Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(t => t)).ToArray());

            Llenar(cmbPago, new[] { "(Todos)" }.Concat(_todasLasFacturas
                .Select(x => (x.ticket.MetodoPago ?? "").Trim())
                .Where(m => m.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m)).ToArray());

            Llenar(cmbIva, new[] { "(Todos)" }.Concat(_todasLasFacturas
                .Select(x => Math.Round(x.ticket.IvaPorcentaje, 2))
                .Where(v => v > 0)
                .Distinct()
                .OrderBy(v => v)
                .Select(v => v.ToString("0.##", CultureInfo.InvariantCulture) + " %")).ToArray());
        }

        // -----------------------------------------------------------------------
        // Aplica el filtro automáticamente al cambiar cualquier criterio.
        // -----------------------------------------------------------------------
        private void ConectarEventosFiltros()
        {
            EventHandler cambio = (s, e) => { if (!_suspenderFiltro) AplicarFiltro(); };

            foreach (var c in new[] { cmbEmpresa, cmbPresentacion, cmbTrimestre, cmbPago, cmbIva, cmbVencimiento, cmbPdf, cmbImagen })
                c.SelectedIndexChanged += cambio;
            foreach (var c in new[] { chkTipoFactura, chkTipoAlbaran, chkTipoTicket, chkSinFecha })
                c.CheckedChanged += cambio;
            foreach (var t in new[] { txtImporteMin, txtImporteMax, txtTexto })
                t.TextChanged += cambio;
            dtpDesde.ValueChanged += cambio;
            dtpHasta.ValueChanged += cambio;

            btnLimpiarAvanzados.Click += (s, e) => { LimpiarAvanzados(); AplicarFiltro(); };
        }

        // Restablece los criterios avanzados (no toca fechas ni empresa).
        private void LimpiarAvanzados()
        {
            _suspenderFiltro = true;
            foreach (var c in new[] { cmbPresentacion, cmbTrimestre, cmbPago, cmbIva, cmbVencimiento, cmbPdf, cmbImagen })
                c.SelectedIndex = 0;
            txtImporteMin.Clear(); txtImporteMax.Clear(); txtTexto.Clear();
            chkTipoFactura.Checked = chkTipoAlbaran.Checked = chkTipoTicket.Checked = chkSinFecha.Checked = true;
            _suspenderFiltro = false;
        }

        // Convierte el texto de un cuadro de importe en número (null si no hay dígitos).
        private static double? LeerImporte(TextBox txt)
        {
            string s = txt.Text.Trim();
            return s.Any(char.IsDigit) ? FiltrosExportacion.ParsearImporte(s) : (double?)null;
        }

        // Valor seleccionado de un desplegable cuyo primer elemento es "(Todos)".
        private static string ValorSeleccionado(ComboBox combo) =>
            combo.SelectedIndex > 0 ? combo.SelectedItem?.ToString() ?? "" : "";

        // -----------------------------------------------------------------------
        // Lee los controles y construye el conjunto de criterios de filtrado.
        // -----------------------------------------------------------------------
        private FiltrosExportacion LeerFiltros()
        {
            var f = new FiltrosExportacion
            {
                Desde = dtpDesde.Value.Date,
                Hasta = dtpHasta.Value.Date,
                IncluirSinFecha = chkSinFecha.Checked,
                Empresa = ValorSeleccionado(cmbEmpresa),
                Presentacion = (FiltroPresentacion)Math.Max(0, cmbPresentacion.SelectedIndex),
                Trimestre = ValorSeleccionado(cmbTrimestre),
                MetodoPago = ValorSeleccionado(cmbPago),
                ImporteMin = LeerImporte(txtImporteMin),
                ImporteMax = LeerImporte(txtImporteMax),
                Vencimiento = (FiltroVencimiento)Math.Max(0, cmbVencimiento.SelectedIndex),
                Pdf = (FiltroArchivo)Math.Max(0, cmbPdf.SelectedIndex),
                Imagen = (FiltroArchivo)Math.Max(0, cmbImagen.SelectedIndex),
                Texto = txtTexto.Text.Trim()
            };

            string iva = ValorSeleccionado(cmbIva).Replace("%", "").Trim();
            if (iva.Length > 0 && double.TryParse(iva, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
                f.IvaPorcentaje = pct;

            // Con los tres tipos marcados (o ninguno) no se filtra por tipo.
            bool todos = chkTipoFactura.Checked && chkTipoAlbaran.Checked && chkTipoTicket.Checked;
            bool ninguno = !chkTipoFactura.Checked && !chkTipoAlbaran.Checked && !chkTipoTicket.Checked;
            if (!todos && !ninguno)
            {
                if (chkTipoFactura.Checked) f.Tipos.Add("factura");
                if (chkTipoAlbaran.Checked) f.Tipos.Add("albaran");
                if (chkTipoTicket.Checked) f.Tipos.Add("ticket");
            }
            return f;
        }

        // -----------------------------------------------------------------------
        // Filtra con todos los criterios (básicos + avanzados) y repuebla la
        // lista marcable. Todas las facturas resultantes quedan marcadas.
        // -----------------------------------------------------------------------
        private void AplicarFiltro()
        {
            var filtros = LeerFiltros();
            var filtradas = _todasLasFacturas
                .Where(x => filtros.Cumple(x.ticket, x.fecha, _carpetaTickets))
                .OrderByDescending(x => x.fecha ?? DateTime.MinValue)
                .ToList();

            _cargandoLista = true;
            clbFacturas.BeginUpdate();
            clbFacturas.Items.Clear();
            foreach (var (ticket, rutaJson, _) in filtradas)
            {
                string etiqueta = $"{ticket.Empresa} | {ticket.Fecha} | {ticket.Numero} | {ticket.Total}";
                clbFacturas.Items.Add(new FacturaListItem(ticket, rutaJson, etiqueta), true);
            }
            clbFacturas.EndUpdate();
            _cargandoLista = false;

            RefrescarBotonAvanzados();
            ActualizarResumen();
        }

        // Texto del botón de filtros avanzados: nº de criterios activos y flecha ▲/▼.
        private void RefrescarBotonAvanzados()
        {
            int activos = LeerFiltros().ContarAvanzadosActivos();
            btnAvanzados.Text = "Filtros avanzados" + (activos > 0 ? $" ({activos})" : "") +
                                (panelAvanzado.Visible ? "  ▲" : "  ▼");
        }

        // -----------------------------------------------------------------------
        // Muestra cuántas facturas hay, cuántas están marcadas y su importe
        // total (los albaranes no suman, igual que en el resto de la aplicación).
        // -----------------------------------------------------------------------
        private void ActualizarResumen()
        {
            var marcadas = clbFacturas.CheckedItems.Cast<FacturaListItem>().ToList();
            double total = marcadas
                .Where(m => !string.Equals(m.Ticket.TipoDocumento, "albaran", StringComparison.OrdinalIgnoreCase))
                .Sum(m => FiltrosExportacion.ParsearImporte(m.Ticket.Total));
            lblEstado.Text = $"{clbFacturas.Items.Count} encontrada(s) · {marcadas.Count} marcada(s) · total {total:N2} €";
        }

        private void MarcarTodas(bool marcar)
        {
            _cargandoLista = true;
            for (int i = 0; i < clbFacturas.Items.Count; i++)
                clbFacturas.SetItemChecked(i, marcar);
            _cargandoLista = false;
            ActualizarResumen();
        }

        // -----------------------------------------------------------------------
        // Marca los tipos de archivo según el ajuste "Formato por defecto":
        // "TODO" (PDF + JSON + JPG procesado), "PDF", "JSON", "JPG" u "ORIGINAL".
        // -----------------------------------------------------------------------
        private void AplicarFormatoPorDefecto()
        {
            string f = _ajustes?.FormatoExportacion ?? "TODO";
            if (f == "TODO") return; // estado inicial clásico (PDF, JSON y JPG procesado)
            chkPdf.Checked = f == "PDF";
            chkJson.Checked = f == "JSON";
            chkJpg.Checked = f == "JPG";
            chkOriginal.Checked = f == "ORIGINAL";
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
            if (!string.IsNullOrWhiteSpace(_ajustes?.CarpetaExportacion) && Directory.Exists(_ajustes!.CarpetaExportacion))
                dlg.InitialDirectory = _ajustes.CarpetaExportacion;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            // Se leen los controles aquí: el trabajo pesado corre en otro hilo.
            string rutaZip = dlg.FileName;
            bool incPdf = chkPdf.Checked, incJson = chkJson.Checked, incJpg = chkJpg.Checked, incOriginal = chkOriginal.Checked;

            btnExportar.Enabled = false;
            lblEstado.Text = "Generando ZIP...";

            try
            {
                var res = await System.Threading.Tasks.Task.Run(() =>
                    GenerarZip(rutaZip, seleccionadas, incPdf, incJson, incJpg, incOriginal));

                lblEstado.Text = $"ZIP creado: {res.documentos} documento(s) y {res.archivos} archivo(s) en Facturas/" +
                                 (res.faltantes > 0 ? $" ({res.faltantes} no encontrados)." : ".");
                if (_ajustes?.AbrirCarpetaAlExportar == true)
                    ExportadorArchivos.AbrirCarpetaConArchivo(rutaZip);
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

        // -----------------------------------------------------------------------
        // Crea el ZIP con esta estructura:
        //   Album.html            álbum independiente (listado + vista previa)
        //   Facturas/             archivos exportados, SIN subcarpetas, con
        //                         nombre Empresa_AAAA-MM-DD_Numero[.ext]
        // No toca la interfaz (se ejecuta en un hilo secundario). Devuelve el
        // nº de documentos, de archivos copiados y de archivos no encontrados.
        // -----------------------------------------------------------------------
        private (int documentos, int archivos, int faltantes) GenerarZip(string rutaZip, List<FacturaListItem> items,
            bool incPdf, bool incJson, bool incJpg, bool incOriginal)
        {
            // La lógica vive en AlbumExportador.GenerarZip para reutilizarla (p. ej. desde el panel web).
            var docs = items.Select(i => (i.Ticket, i.RutaJson)).ToList();
            return AlbumExportador.GenerarZip(_carpetaTickets, rutaZip, docs, incPdf, incJson, incJpg, incOriginal);
        }

        // Ruta de un archivo relativa a la carpeta de facturas, con '/' como separador.
        private string RutaRelativa(string rutaAbsoluta) =>
            Path.GetRelativePath(_carpetaTickets, rutaAbsoluta).Replace('\\', '/');

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