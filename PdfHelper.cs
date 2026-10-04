using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;
using Docnet.Core;
using Docnet.Core.Models;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Utilidades PDF reutilizables en cualquier proyecto: generación de PDF
    // a partir de una imagen (Mat) y extracción de imágenes desde un PDF.
    // No depende de estado de ninguna clase concreta de la app.
    // -----------------------------------------------------------------------
    public static class PdfHelper
    {
        // -----------------------------------------------------------------------
        // Genera un PDF de una sola página embebiendo el JPG directamente
        // (filtro DCTDecode), sin necesidad de librerías externas.
        // -----------------------------------------------------------------------
        public static void GuardarComoPdf(Mat imagen, string rutaPdf)
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

        // -----------------------------------------------------------------------
        // Renderiza cada página de un PDF como Mat (BGR), usando Docnet.Core
        // (wrapper de PDFium). dpi controla la resolución de salida; a mayor
        // dpi, más nítido pero más lento/pesado. El llamador es responsable
        // de hacer Dispose() de cada Mat devuelto.
        // -----------------------------------------------------------------------
        public static List<Mat> PdfAImagenes(string rutaPdf, int dpi = 200)
        {
            var resultado = new List<Mat>();
            if (!File.Exists(rutaPdf))
                throw new FileNotFoundException("No se encontró el PDF.", rutaPdf);

            // Conversión de DPI a factor de escala (base: 72 dpi = 1.0x)
            double escala = dpi / 72.0;

            using var docReader = DocLib.Instance.GetDocReader(
                rutaPdf,
                new PageDimensions(escala));

            for (int i = 0; i < docReader.GetPageCount(); i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                int ancho = pageReader.GetPageWidth();
                int alto = pageReader.GetPageHeight();
                byte[] bytesBgra = pageReader.GetImage(); // BGRA crudo

                using Mat matBgra = new Mat(alto, ancho, MatType.CV_8UC4);
                System.Runtime.InteropServices.Marshal.Copy(bytesBgra, 0, matBgra.Data, bytesBgra.Length);

                // PDFium devuelve zonas transparentes con RGB=0 (negro) y
                // alpha=0. Sin componer sobre un fondo, esas zonas se ven
                // negras en vez de blancas. Se compone manualmente aquí.
                Mat matBgr = ComponerSobreBlanco(matBgra);
                resultado.Add(matBgr);
            }

            return resultado;
        }

        // -----------------------------------------------------------------------
        // Compone una imagen BGRA sobre un fondo blanco usando el canal alpha
        // como máscara: resultado = color*alpha + 255*(1-alpha). Devuelve un
        // Mat de 3 canales (BGR) listo para guardar como JPG.
        // -----------------------------------------------------------------------
        private static Mat ComponerSobreBlanco(Mat matBgra)
        {
            Mat[] canales = Cv2.Split(matBgra); // [0]=B [1]=G [2]=R [3]=A
            try
            {
                using Mat alphaF = new Mat();
                canales[3].ConvertTo(alphaF, MatType.CV_32F, 1.0 / 255.0);

                using Mat unoMenosAlpha = new Mat();
                Cv2.Subtract(new Scalar(1.0), alphaF, unoMenosAlpha);

                var canalesBgr = new Mat[3];
                for (int c = 0; c < 3; c++)
                {
                    using Mat colorF = new Mat();
                    canales[c].ConvertTo(colorF, MatType.CV_32F, 1.0 / 255.0);

                    using Mat colorPonderado = new Mat();
                    Cv2.Multiply(colorF, alphaF, colorPonderado);

                    using Mat combinado = new Mat();
                    Cv2.Add(colorPonderado, unoMenosAlpha, combinado);

                    canalesBgr[c] = new Mat();
                    combinado.ConvertTo(canalesBgr[c], MatType.CV_8U, 255.0);
                }

                Mat resultado = new Mat();
                Cv2.Merge(canalesBgr, resultado);
                foreach (var m in canalesBgr) m.Dispose();
                return resultado;
            }
            finally
            {
                foreach (var m in canales) m.Dispose();
            }
        }
    }
}