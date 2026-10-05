using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Ventana de la herramienta "Analizar pHash de todas las facturas".
    // Calcula la huella visual (pHash) de original.jpg de las facturas y
    // albaranes guardados, para poder detectar imágenes duplicadas. Muestra
    // barra de progreso, resultado por documento y permite detener el proceso.
    //
    // El análisis corre en segundo plano (AlbumGenerator.AnalizarPHashDocumentos)
    // y la interfaz se actualiza mediante BeginInvoke.
    // -----------------------------------------------------------------------
    internal sealed class AnalizarPHashForm : Form
    {
        private readonly AlbumGenerator _album;
        private CancellationTokenSource? _cts;
        private bool _enCurso = false;

        // Fila de la tabla por documento (clave: ruta del datos.json). Se guarda la
        // fila y no su índice, porque el usuario puede ordenar la tabla.
        private readonly Dictionary<string, DataGridViewRow> _filaPorRuta = new();

        private readonly CheckBox chkRecalcular = new() { Text = "Recalcular también los que ya tienen pHash", AutoSize = true };
        private readonly Button btnIniciar = new() { Text = "▶  Iniciar análisis", Height = 34, Width = 150 };
        private readonly Button btnCancelar = new() { Text = "Cerrar", Height = 34, Width = 100 };
        private readonly ProgressBar barra = new() { Dock = DockStyle.Top, Height = 14, Style = ProgressBarStyle.Continuous };
        private readonly Label lblEstado = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray, Text = "Pulsa «Iniciar análisis» para empezar." };

        private readonly DataGridView grid = new()
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

        // album: generador que contiene la lógica de cálculo y las carpetas de datos.
        public AnalizarPHashForm(AlbumGenerator album)
        {
            _album = album;

            Text = "Analizar pHash de todas las facturas";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(980, 520);
            MinimumSize = new System.Drawing.Size(640, 400);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            BackColor = System.Drawing.Color.White;

            ConstruirUi();
            ConfigurarGrid();

            // Al abrir, se muestran los pHash ya existentes (sin analizar nada).
            Shown += async (s, e) => await CargarListadoAsync();

            // Si se cierra la ventana con el análisis en marcha, se detiene.
            FormClosing += (s, e) => { if (_enCurso) _cts?.Cancel(); };
        }

        // Construye la cabecera (título, descripción, opciones y botones), la
        // barra de progreso, la tabla de resultados y la barra de estado.
        private void ConstruirUi()
        {
            var panelSuperior = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = System.Drawing.Color.FromArgb(248, 249, 250) };

            var lblTitulo = new Label
            {
                Text = "Análisis de pHash",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Location = new System.Drawing.Point(14, 8)
            };
            var lblDescripcion = new Label
            {
                Text = "Muestra el pHash de las facturas y albaranes guardados. «Iniciar análisis» calcula los que faltan " +
                       "a partir de original.jpg (en verde los recién calculados); con él se avisa de imágenes repetidas.",
                Location = new System.Drawing.Point(14, 30),
                Size = new System.Drawing.Size(720, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = System.Drawing.Color.FromArgb(90, 90, 90)
            };
            chkRecalcular.Location = new System.Drawing.Point(14, 68);

            var panelBotones = new FlowLayoutPanel
            {
                Location = new System.Drawing.Point(14, 92),
                Size = new System.Drawing.Size(280, 36),
                FlowDirection = FlowDirection.LeftToRight
            };
            btnIniciar.BackColor = System.Drawing.Color.SeaGreen;
            btnIniciar.ForeColor = System.Drawing.Color.White;
            btnIniciar.FlatStyle = FlatStyle.Flat;
            btnIniciar.FlatAppearance.BorderSize = 0;
            btnIniciar.Click += BtnIniciar_Click;
            btnCancelar.Click += BtnCancelar_Click;
            panelBotones.Controls.Add(btnIniciar);
            panelBotones.Controls.Add(btnCancelar);

            panelSuperior.Controls.Add(lblTitulo);
            panelSuperior.Controls.Add(lblDescripcion);
            panelSuperior.Controls.Add(chkRecalcular);
            panelSuperior.Controls.Add(panelBotones);

            var panelInferior = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            lblEstado.Location = new System.Drawing.Point(14, 6);
            panelInferior.Controls.Add(lblEstado);

            // Orden de alta: el Fill primero; lo añadido después se acopla antes.
            Controls.Add(grid);
            Controls.Add(barra);
            Controls.Add(panelInferior);
            Controls.Add(panelSuperior);
        }

        // Define las columnas y el estilo de la tabla de resultados.
        private void ConfigurarGrid()
        {
            grid.Columns.Clear();
            grid.Columns.Add("Tipo", "Tipo");
            grid.Columns.Add("Empresa", "Empresa");
            grid.Columns.Add("Numero", "Nº");
            grid.Columns.Add("PHash", "pHash");
            grid.Columns.Add("Estado", "Estado");
            grid.Columns["Tipo"]!.FillWeight = 12;
            grid.Columns["Empresa"]!.FillWeight = 38;
            grid.Columns["Numero"]!.FillWeight = 18;
            grid.Columns["Estado"]!.FillWeight = 32;

            // pHash: 63 bits, ancho fijo con fuente monoespaciada.
            var colHash = grid.Columns["PHash"]!;
            colHash.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colHash.Width = 450;
            colHash.DefaultCellStyle.Font = new System.Drawing.Font("Consolas", 8.5F);

            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            grid.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            grid.ColumnHeadersHeight = 34;
            grid.EnableHeadersVisualStyles = false;
            grid.RowTemplate.Height = 28;
            grid.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(250, 250, 251);
            grid.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(232, 240, 254);
            grid.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
        }

        // Lanza el análisis en segundo plano y actualiza la interfaz al terminar.
        private async void BtnIniciar_Click(object? sender, EventArgs e)
        {
            if (_enCurso) return;
            _enCurso = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            bool recalcular = chkRecalcular.Checked;

            // La tabla conserva el listado; solo se quitan los resaltados de un análisis anterior.
            foreach (DataGridViewRow fila in grid.Rows) fila.DefaultCellStyle.BackColor = System.Drawing.Color.Empty;
            barra.Value = 0;
            btnIniciar.Enabled = false;
            chkRecalcular.Enabled = false;
            btnCancelar.Text = "Detener";
            lblEstado.Text = "Analizando...";

            try
            {
                var r = await Task.Run(() => _album.AnalizarPHashDocumentos(recalcular,
                    (hechas, total, res) => AlProgreso(hechas, total, res), token));

                if (IsDisposed) return;
                lblEstado.Text = (token.IsCancellationRequested ? "⏹ Detenido: " : "✅ Completado: ")
                    + $"{r.procesadas} calculados, {r.omitidas} omitidos, {r.errores} con error.";
            }
            catch (Exception ex)
            {
                if (!IsDisposed) lblEstado.Text = "Error: " + ex.Message;
            }
            finally
            {
                _enCurso = false;
                _cts?.Dispose();
                _cts = null;
                if (!IsDisposed)
                {
                    btnIniciar.Enabled = true;
                    chkRecalcular.Enabled = true;
                    btnCancelar.Enabled = true;
                    btnCancelar.Text = "Cerrar";
                }
            }
        }

        // Carga en segundo plano el listado de documentos con su pHash actual.
        private async Task CargarListadoAsync()
        {
            btnIniciar.Enabled = false;
            lblEstado.Text = "Cargando listado...";
            try
            {
                var lista = await Task.Run(() => _album.ListarPHashDocumentos());
                if (IsDisposed) return;

                grid.SuspendLayout();
                grid.Rows.Clear();
                _filaPorRuta.Clear();
                foreach (var r in lista) AgregarOActualizarFila(r, resaltarNuevo: false);
                grid.ResumeLayout();

                int con = lista.Count(x => !string.IsNullOrEmpty(x.PHash));
                lblEstado.Text = $"{lista.Count} documento(s): {con} con pHash, {lista.Count - con} sin pHash. " +
                                 "Pulsa «Iniciar análisis» para calcular los que faltan.";
            }
            catch (Exception ex)
            {
                if (!IsDisposed) lblEstado.Text = "Error al cargar el listado: " + ex.Message;
            }
            finally
            {
                if (!IsDisposed && !_enCurso) btnIniciar.Enabled = true;
            }
        }

        // Añade la fila del documento o, si ya existe, actualiza sus valores.
        // resaltarNuevo: pinta de verde las filas recién calculadas.
        private DataGridViewRow AgregarOActualizarFila(ResultadoPHash r, bool resaltarNuevo)
        {
            if (!_filaPorRuta.TryGetValue(r.RutaJson, out var fila))
            {
                int idx = grid.Rows.Add(r.Tipo, r.Empresa, r.Numero, r.PHash, r.Estado);
                fila = grid.Rows[idx];
                _filaPorRuta[r.RutaJson] = fila;
            }
            else
            {
                fila.Cells["Tipo"].Value = r.Tipo;
                fila.Cells["Empresa"].Value = r.Empresa;
                fila.Cells["Numero"].Value = r.Numero;
                fila.Cells["PHash"].Value = r.PHash;
                fila.Cells["Estado"].Value = r.Estado;
            }

            if (resaltarNuevo && r.Estado.StartsWith("✅"))
                fila.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(226, 245, 232);
            return fila;
        }

        // "Detener" si hay análisis en marcha; si no, cierra la ventana.
        private void BtnCancelar_Click(object? sender, EventArgs e)
        {
            if (_enCurso)
            {
                _cts?.Cancel();
                btnCancelar.Enabled = false;
                lblEstado.Text = "Deteniendo...";
                return;
            }
            Close();
        }

        // Recibe el progreso desde el hilo de fondo y lo pasa al hilo de la interfaz.
        private void AlProgreso(int hechas, int total, ResultadoPHash res)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed) return;
                    if (total > 0) barra.Maximum = total;
                    barra.Value = Math.Min(hechas, barra.Maximum);
                    var fila = AgregarOActualizarFila(res, resaltarNuevo: true);
                    if (fila.Index >= 0) grid.FirstDisplayedScrollingRowIndex = fila.Index;
                    lblEstado.Text = $"Analizando {hechas}/{total}...";
                }));
            }
            catch (InvalidOperationException) { /* ventana cerrada durante el análisis */ }
        }
    }
}
