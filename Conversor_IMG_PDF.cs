using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Formulario para convertir en ambos sentidos: Imagen → PDF (un PDF por
    // imagen) y PDF → Imagen (una imagen JPG por página). Reutiliza
    // PdfHelper (PdfHelper.cs), clase estática reutilizable en otros
    // proyectos.
    // -----------------------------------------------------------------------
    public class Conversor_IMG_PDF : Form
    {
        // Modo de conversión activo
        private enum ModoConversion { ImagenAPdf, PdfAImagen }
        private ModoConversion _modo = ModoConversion.ImagenAPdf;

        private RadioButton rbImagenAPdf = new() { Text = "Imagen → PDF", Checked = true, AutoSize = true };
        private RadioButton rbPdfAImagen = new() { Text = "PDF → Imagen", AutoSize = true };

        private ListBox lstArchivos = new() { Dock = DockStyle.Fill };
        private Button btnAgregar = new() { Text = "Agregar archivos..." };
        private Button btnQuitar = new() { Text = "Quitar seleccionada" };
        private Button btnLimpiar = new() { Text = "Limpiar lista" };

        private TextBox txtDestino = new() { ReadOnly = true, Width = 260 };
        private Button btnDestino = new() { Text = "Elegir carpeta..." };

        private ProgressBar barraProgreso = new() { Dock = DockStyle.Bottom, Height = 22 };
        private Label lblEstado = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };
        private Button btnConvertir = new() { Text = "Convertir", Height = 34 };

        private readonly List<string> _rutasArchivos = new();
        private string _carpetaDestino = "";

        public Conversor_IMG_PDF()
        {
            Text = "Conversor Imagen ⇄ PDF";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(460, 440);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            MinimumSize = new System.Drawing.Size(400, 370);

            ConstruirUi();
        }

        private void ConstruirUi()
        {
            var panelSuperior = new Panel { Dock = DockStyle.Top, Height = 110 };

            var panelModo = new FlowLayoutPanel
            {
                Location = new System.Drawing.Point(10, 8),
                Size = new System.Drawing.Size(420, 24),
                FlowDirection = FlowDirection.LeftToRight
            };
            rbImagenAPdf.Margin = new Padding(0, 0, 20, 0);
            rbImagenAPdf.CheckedChanged += RbModo_CheckedChanged;
            rbPdfAImagen.CheckedChanged += RbModo_CheckedChanged;
            panelModo.Controls.Add(rbImagenAPdf);
            panelModo.Controls.Add(rbPdfAImagen);

            var lblDestino = new Label { Text = "Carpeta de salida:", AutoSize = true, Location = new System.Drawing.Point(10, 40) };
            txtDestino.Location = new System.Drawing.Point(10, 61);
            btnDestino.Location = new System.Drawing.Point(280, 59);
            btnDestino.Click += BtnDestino_Click;

            panelSuperior.Controls.AddRange(new Control[] { panelModo, lblDestino, txtDestino, btnDestino });

            var panelBotonesLista = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.LeftToRight };
            btnAgregar.Click += BtnAgregar_Click;
            btnQuitar.Click += BtnQuitar_Click;
            btnLimpiar.Click += BtnLimpiar_Click;
            panelBotonesLista.Controls.AddRange(new Control[] { btnAgregar, btnQuitar, btnLimpiar });

            var panelLista = new Panel { Dock = DockStyle.Fill };
            panelLista.Controls.Add(lstArchivos);
            panelLista.Controls.Add(panelBotonesLista);

            var panelInferior = new Panel { Dock = DockStyle.Bottom, Height = 70 };
            lblEstado.Location = new System.Drawing.Point(10, 8);
            btnConvertir.Location = new System.Drawing.Point(10, 28);
            btnConvertir.Width = 440;
            btnConvertir.Click += BtnConvertir_Click;
            panelInferior.Controls.AddRange(new Control[] { lblEstado, btnConvertir });

            Controls.Add(panelLista);
            Controls.Add(panelInferior);
            Controls.Add(barraProgreso);
            Controls.Add(panelSuperior);

            ActualizarTextosPorModo();
        }

        // -----------------------------------------------------------------------
        // Cambia filtros de diálogo, título del botón y limpia la lista al
        // alternar de modo (evita mezclar imágenes y PDFs en la conversión).
        // -----------------------------------------------------------------------
        private void RbModo_CheckedChanged(object? sender, EventArgs e)
        {
            _modo = rbPdfAImagen.Checked ? ModoConversion.PdfAImagen : ModoConversion.ImagenAPdf;
            _rutasArchivos.Clear();
            lstArchivos.Items.Clear();
            ActualizarTextosPorModo();
        }

        private void ActualizarTextosPorModo()
        {
            bool esPdfAImagen = _modo == ModoConversion.PdfAImagen;
            Text = esPdfAImagen ? "Conversor PDF → Imagen" : "Conversor Imagen → PDF";
            btnAgregar.Text = esPdfAImagen ? "Agregar PDFs..." : "Agregar imágenes...";
            btnConvertir.Text = "Convertir";
            lblEstado.Text = "";
        }

        private void BtnAgregar_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = _modo == ModoConversion.PdfAImagen
                    ? "Documentos PDF (*.pdf)|*.pdf"
                    : "Imágenes (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            foreach (var ruta in dlg.FileNames)
            {
                if (!_rutasArchivos.Contains(ruta))
                {
                    _rutasArchivos.Add(ruta);
                    lstArchivos.Items.Add(Path.GetFileName(ruta));
                }
            }
        }

        private void BtnQuitar_Click(object? sender, EventArgs e)
        {
            int idx = lstArchivos.SelectedIndex;
            if (idx < 0) return;
            _rutasArchivos.RemoveAt(idx);
            lstArchivos.Items.RemoveAt(idx);
        }

        private void BtnLimpiar_Click(object? sender, EventArgs e)
        {
            _rutasArchivos.Clear();
            lstArchivos.Items.Clear();
        }

        private void BtnDestino_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _carpetaDestino = dlg.SelectedPath;
            txtDestino.Text = _carpetaDestino;
        }

        private async void BtnConvertir_Click(object? sender, EventArgs e)
        {
            if (_rutasArchivos.Count == 0)
            {
                MessageBox.Show("Agrega al menos un archivo.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_carpetaDestino))
            {
                MessageBox.Show("Elige una carpeta de destino.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_modo == ModoConversion.PdfAImagen)
                await ConvertirPdfsAImagenesAsync();
            else
                await ConvertirImagenesAPdfAsync();
        }

        // -----------------------------------------------------------------------
        // Imagen → PDF: un PDF por imagen, mismo comportamiento de siempre.
        // -----------------------------------------------------------------------
        private async System.Threading.Tasks.Task ConvertirImagenesAPdfAsync()
        {
            btnConvertir.Enabled = false;
            barraProgreso.Minimum = 0;
            barraProgreso.Maximum = _rutasArchivos.Count;
            barraProgreso.Value = 0;

            int exitos = 0, fallos = 0;

            for (int i = 0; i < _rutasArchivos.Count; i++)
            {
                string rutaOrigen = _rutasArchivos[i];
                string nombreBase = Path.GetFileNameWithoutExtension(rutaOrigen);
                string rutaPdf = Path.Combine(_carpetaDestino, nombreBase + ".pdf");

                lblEstado.Text = $"Convirtiendo {i + 1}/{_rutasArchivos.Count}: {Path.GetFileName(rutaOrigen)}";

                try
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using Mat img = Cv2.ImRead(rutaOrigen, ImreadModes.Unchanged);
                        if (img.Empty()) throw new Exception("No se pudo leer la imagen.");
                        PdfHelper.GuardarComoPdf(img, rutaPdf);
                    });
                    exitos++;
                }
                catch (Exception ex)
                {
                    fallos++;
                    lblEstado.Text = $"Error con {Path.GetFileName(rutaOrigen)}: {ex.Message}";
                }

                barraProgreso.Value = i + 1;
            }

            btnConvertir.Enabled = true;
            lblEstado.Text = $"Completado: {exitos} correctas, {fallos} con error.";
            MessageBox.Show($"Conversión finalizada.\n\nCorrectas: {exitos}\nCon error: {fallos}",
                "Conversor Imagen → PDF", MessageBoxButtons.OK,
                fallos > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // -----------------------------------------------------------------------
        // PDF → Imagen: cada página del PDF se guarda como
        // {nombrePdf}_pag{N}.jpg en la carpeta de destino, usando
        // PdfHelper.PdfAImagenes (renderizado vía Docnet.Core/PDFium).
        // -----------------------------------------------------------------------
        private async System.Threading.Tasks.Task ConvertirPdfsAImagenesAsync()
        {
            btnConvertir.Enabled = false;
            barraProgreso.Minimum = 0;
            barraProgreso.Maximum = _rutasArchivos.Count;
            barraProgreso.Value = 0;

            int exitos = 0, fallos = 0, paginasTotal = 0;

            for (int i = 0; i < _rutasArchivos.Count; i++)
            {
                string rutaOrigen = _rutasArchivos[i];
                string nombreBase = Path.GetFileNameWithoutExtension(rutaOrigen);

                lblEstado.Text = $"Convirtiendo {i + 1}/{_rutasArchivos.Count}: {Path.GetFileName(rutaOrigen)}";

                try
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        List<Mat> paginas = PdfHelper.PdfAImagenes(rutaOrigen);
                        try
                        {
                            for (int p = 0; p < paginas.Count; p++)
                            {
                                string sufijo = paginas.Count > 1 ? $"_pag{p + 1}" : "";
                                string rutaJpg = Path.Combine(_carpetaDestino, nombreBase + sufijo + ".jpg");
                                Cv2.ImWrite(rutaJpg, paginas[p]);
                                paginasTotal++;
                            }
                        }
                        finally
                        {
                            foreach (var pagina in paginas) pagina.Dispose();
                        }
                    });
                    exitos++;
                }
                catch (Exception ex)
                {
                    fallos++;
                    lblEstado.Text = $"Error con {Path.GetFileName(rutaOrigen)}: {ex.Message}";
                }

                barraProgreso.Value = i + 1;
            }

            btnConvertir.Enabled = true;
            lblEstado.Text = $"Completado: {exitos} PDF(s) correctos ({paginasTotal} página(s)), {fallos} con error.";
            MessageBox.Show($"Conversión finalizada.\n\nPDFs correctos: {exitos}\nPáginas generadas: {paginasTotal}\nCon error: {fallos}",
                "Conversor PDF → Imagen", MessageBoxButtons.OK,
                fallos > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}