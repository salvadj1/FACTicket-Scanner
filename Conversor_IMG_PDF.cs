using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Formulario para convertir varias imágenes a PDF (un PDF por imagen).
    // Reutiliza el mismo método hand-crafted de generación PDF (DCTDecode,
    // /DeviceGray o /DeviceRGB según canales) que AlbumGenerator.GuardarComoPdf.
    // -----------------------------------------------------------------------
    public class Conversor_IMG_PDF : Form
    {
        private ListBox lstImagenes = new() { Dock = DockStyle.Fill };
        private Button btnAgregar = new() { Text = "Agregar imágenes..." };
        private Button btnQuitar = new() { Text = "Quitar seleccionada" };
        private Button btnLimpiar = new() { Text = "Limpiar lista" };

        private TextBox txtDestino = new() { ReadOnly = true, Width = 260 };
        private Button btnDestino = new() { Text = "Elegir carpeta..." };

        private ProgressBar barraProgreso = new() { Dock = DockStyle.Bottom, Height = 22 };
        private Label lblEstado = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };
        private Button btnConvertir = new() { Text = "Convertir", Height = 34 };

        private readonly List<string> _rutasImagenes = new();
        private string _carpetaDestino = "";

        public Conversor_IMG_PDF()
        {
            Text = "Conversor de Imágenes a PDF";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(460, 420);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            MinimumSize = new System.Drawing.Size(400, 350);

            ConstruirUi();
        }

        private void ConstruirUi()
        {
            var panelSuperior = new Panel { Dock = DockStyle.Top, Height = 90 };

            var lblDestino = new Label { Text = "Carpeta de salida:", AutoSize = true, Location = new System.Drawing.Point(10, 15) };
            txtDestino.Location = new System.Drawing.Point(10, 36);
            btnDestino.Location = new System.Drawing.Point(280, 34);
            btnDestino.Click += BtnDestino_Click;

            panelSuperior.Controls.AddRange(new Control[] { lblDestino, txtDestino, btnDestino });

            var panelBotonesLista = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.LeftToRight };
            btnAgregar.Click += BtnAgregar_Click;
            btnQuitar.Click += BtnQuitar_Click;
            btnLimpiar.Click += BtnLimpiar_Click;
            panelBotonesLista.Controls.AddRange(new Control[] { btnAgregar, btnQuitar, btnLimpiar });

            var panelLista = new Panel { Dock = DockStyle.Fill };
            panelLista.Controls.Add(lstImagenes);
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
        }

        private void BtnAgregar_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            foreach (var ruta in dlg.FileNames)
            {
                if (!_rutasImagenes.Contains(ruta))
                {
                    _rutasImagenes.Add(ruta);
                    lstImagenes.Items.Add(Path.GetFileName(ruta));
                }
            }
        }

        private void BtnQuitar_Click(object? sender, EventArgs e)
        {
            int idx = lstImagenes.SelectedIndex;
            if (idx < 0) return;
            _rutasImagenes.RemoveAt(idx);
            lstImagenes.Items.RemoveAt(idx);
        }

        private void BtnLimpiar_Click(object? sender, EventArgs e)
        {
            _rutasImagenes.Clear();
            lstImagenes.Items.Clear();
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
            if (_rutasImagenes.Count == 0)
            {
                MessageBox.Show("Agrega al menos una imagen.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_carpetaDestino))
            {
                MessageBox.Show("Elige una carpeta de destino.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnConvertir.Enabled = false;
            barraProgreso.Minimum = 0;
            barraProgreso.Maximum = _rutasImagenes.Count;
            barraProgreso.Value = 0;

            int exitos = 0, fallos = 0;

            for (int i = 0; i < _rutasImagenes.Count; i++)
            {
                string rutaOrigen = _rutasImagenes[i];
                string nombreBase = Path.GetFileNameWithoutExtension(rutaOrigen);
                string rutaPdf = Path.Combine(_carpetaDestino, nombreBase + ".pdf");

                lblEstado.Text = $"Convirtiendo {i + 1}/{_rutasImagenes.Count}: {Path.GetFileName(rutaOrigen)}";

                try
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using Mat img = Cv2.ImRead(rutaOrigen, ImreadModes.Unchanged);
                        if (img.Empty()) throw new Exception("No se pudo leer la imagen.");
                        GuardarComoPdf(img, rutaPdf);
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
                "Conversor de Imágenes a PDF", MessageBoxButtons.OK,
                fallos > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // -----------------------------------------------------------------------
        // Genera un PDF de una sola página embebiendo el JPG directamente
        // (filtro DCTDecode), sin necesidad de librerías externas.
        // Mismo método que AlbumGenerator.GuardarComoPdf.
        // -----------------------------------------------------------------------
        private static void GuardarComoPdf(Mat imagen, string rutaPdf)
        {
            Cv2.ImEncode(".jpg", imagen, out byte[] jpgBytes);

            // Tamaño de página en puntos (72 dpi), ajustado a la relación de aspecto
            double anchoPx = imagen.Width, altoPx = imagen.Height;
            double escala = Math.Min(595.0 / anchoPx, 842.0 / altoPx); // A4
            int anchoPt = (int)(anchoPx * escala);
            int altoPt = (int)(altoPx * escala);

            var objetos = new List<byte[]>();
            objetos.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
            objetos.Add(Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
            objetos.Add(Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {anchoPt} {altoPt}] " +
                "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>"));

            string colorSpace = imagen.Channels() == 1 ? "/DeviceGray" : "/DeviceRGB";
            byte[] imgDict = Encoding.ASCII.GetBytes(
                $"<< /Type /XObject /Subtype /Image /Width {imagen.Width} /Height {imagen.Height} " +
                $"/ColorSpace {colorSpace} /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpgBytes.Length + " >>\nstream\n");
            byte[] imgFooter = Encoding.ASCII.GetBytes("\nendstream");
            var imgObjeto = new byte[imgDict.Length + jpgBytes.Length + imgFooter.Length];
            Buffer.BlockCopy(imgDict, 0, imgObjeto, 0, imgDict.Length);
            Buffer.BlockCopy(jpgBytes, 0, imgObjeto, imgDict.Length, jpgBytes.Length);
            Buffer.BlockCopy(imgFooter, 0, imgObjeto, imgDict.Length + jpgBytes.Length, imgFooter.Length);
            objetos.Add(imgObjeto);

            string contenido = $"q {anchoPt} 0 0 {altoPt} 0 0 cm /Im0 Do Q";
            byte[] contenidoBytes = Encoding.ASCII.GetBytes(contenido);
            objetos.Add(Encoding.ASCII.GetBytes($"<< /Length {contenidoBytes.Length} >>\nstream\n")
                .Concat(contenidoBytes).Concat(Encoding.ASCII.GetBytes("\nendstream")).ToArray());

            using var ms = new MemoryStream();
            void Escribir(string s) => ms.Write(Encoding.ASCII.GetBytes(s), 0, Encoding.ASCII.GetByteCount(s));

            Escribir("%PDF-1.4\n");
            var offsets = new List<long>();
            for (int i = 0; i < objetos.Count; i++)
            {
                offsets.Add(ms.Position);
                Escribir($"{i + 1} 0 obj\n");
                ms.Write(objetos[i], 0, objetos[i].Length);
                Escribir("\nendobj\n");
            }

            long xrefOffset = ms.Position;
            Escribir($"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n");
            foreach (long off in offsets)
                Escribir($"{off:D10} 00000 n \n");

            Escribir($"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

            File.WriteAllBytes(rutaPdf, ms.ToArray());
        }
    }
}
