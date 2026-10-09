using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Utilidades para importar archivos (imágenes y PDF) en una cola de lote.
    // Clase estática e independiente del formulario: reutilizable en otros
    // proyectos que usen OpenCvSharp + PdfHelper (Docnet.Core).
    // -----------------------------------------------------------------------
    public static class ImportadorArchivos
    {
        // Extensiones de imagen que OpenCV (Cv2.ImRead) sabe leer.
        public static readonly string[] ExtensionesImagen =
        {
            ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".dib",
            ".tif", ".tiff", ".webp", ".jp2",
            ".pbm", ".pgm", ".ppm", ".sr", ".ras"
        };

        // Extensión de documentos PDF (se renderizan con PdfHelper.PdfAImagenes).
        public const string ExtensionPdf = ".pdf";

        // -------------------------------------------------------------------
        // Devuelve la cadena Filter para un OpenFileDialog: primero
        // "Todos los soportados", luego un filtro por cada tipo.
        // -------------------------------------------------------------------
        public static string ObtenerFiltroDialogo()
        {
            string Patron(IEnumerable<string> exts) => string.Join(";", exts.Select(e => "*" + e));

            var todas = ExtensionesImagen.Append(ExtensionPdf);
            return
                $"Todos los soportados|{Patron(todas)}" +
                $"|Imágenes|{Patron(ExtensionesImagen)}" +
                $"|PDF (*.pdf)|*.pdf" +
                $"|JPEG (*.jpg;*.jpeg;*.jpe)|*.jpg;*.jpeg;*.jpe" +
                $"|PNG (*.png)|*.png" +
                $"|BMP (*.bmp;*.dib)|*.bmp;*.dib" +
                $"|TIFF (*.tif;*.tiff)|*.tif;*.tiff" +
                $"|WebP (*.webp)|*.webp" +
                $"|Todos los archivos (*.*)|*.*";
        }

        // -------------------------------------------------------------------
        // Indica si la ruta corresponde a un PDF (por extensión).
        // -------------------------------------------------------------------
        public static bool EsPdf(string ruta) =>
            string.Equals(Path.GetExtension(ruta), ExtensionPdf, StringComparison.OrdinalIgnoreCase);

        // -------------------------------------------------------------------
        // Recibe las rutas elegidas y devuelve la lista final para la cola:
        // las imágenes pasan tal cual; cada PDF se sustituye por una imagen
        // PNG por página, guardada en carpetaTemp.
        //
        //  - mapaPdfOrigen: se rellena con (ruta PNG temporal -> ruta del PDF
        //    original) para poder conservar el PDF al guardar.
        //  - errores: mensajes de los PDF que no se pudieron leer.
        //  - dpi: resolución de renderizado de las páginas.
        // -------------------------------------------------------------------
        public static List<string> ExpandirPdfs(IEnumerable<string> rutas, string carpetaTemp,
            Dictionary<string, string> mapaPdfOrigen, List<string> errores, int dpi = 200)
        {
            var resultado = new List<string>();
            Directory.CreateDirectory(carpetaTemp);

            foreach (string ruta in rutas)
            {
                if (!EsPdf(ruta))
                {
                    resultado.Add(ruta);
                    continue;
                }

                try
                {
                    List<Mat> paginas = PdfHelper.PdfAImagenes(ruta, dpi);
                    string baseNombre = Path.GetFileNameWithoutExtension(ruta);
                    string id = Guid.NewGuid().ToString("N").Substring(0, 8);

                    for (int i = 0; i < paginas.Count; i++)
                    {
                        using Mat pagina = paginas[i];
                        string rutaPng = Path.Combine(carpetaTemp, $"{baseNombre}_{id}_p{i + 1:000}.png");
                        Cv2.ImWrite(rutaPng, pagina);
                        mapaPdfOrigen[rutaPng] = ruta;
                        resultado.Add(rutaPng);
                    }

                    if (paginas.Count == 0)
                        errores.Add($"El PDF no tiene páginas:\n{ruta}");
                }
                catch (Exception ex)
                {
                    errores.Add($"No se pudo leer el PDF:\n{ruta}\n({ex.Message})");
                }
            }

            return resultado;
        }

        // -------------------------------------------------------------------
        // Borra el contenido de la carpeta temporal de páginas de PDF
        // (ignora errores: archivos en uso, carpeta inexistente, etc.).
        // -------------------------------------------------------------------
        public static void LimpiarTemporales(string carpetaTemp)
        {
            try
            {
                if (!Directory.Exists(carpetaTemp)) return;
                foreach (string f in Directory.GetFiles(carpetaTemp))
                {
                    try { File.Delete(f); } catch { /* en uso: se ignora */ }
                }
            }
            catch { /* sin permisos u otro error: se ignora */ }
        }
    }
}
