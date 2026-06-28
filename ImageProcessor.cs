using System;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;

namespace FACTicket_Scanner
{
    internal static class ImageProcessor
    {
        private static void Log(string mensaje)
        {
            try
            {
                string ruta = System.IO.Path.Combine(AppContext.BaseDirectory, "debug_log.txt");
                System.IO.File.AppendAllText(ruta, $"{DateTime.Now:HH:mm:ss.fff} - {mensaje}\r\n");
            }
            catch { }
        }

        // -----------------------------------------------------------------------
        // BUG E: calcula ajustes automáticos según histograma
        // -----------------------------------------------------------------------
        internal static (int contraste, int brillo, int ruido) CalcularAjustesAutomaticos(Mat original)
        {
            using Mat gris = new Mat();
            Cv2.CvtColor(original, gris, ColorConversionCodes.BGR2GRAY);

            // 1. Brillo medio y desviación estándar
            Cv2.MeanStdDev(gris, out Scalar media, out Scalar desv);
            double brilloMedio = media.Val0;
            double desviacion = desv.Val0;

            // Ajuste de brillo según si la imagen está muy oscura o muy clara
            int brilloSugerido = 0;
            if (brilloMedio < 90)
                brilloSugerido = (int)Math.Min(40, (110 - brilloMedio) * 0.6);
            else if (brilloMedio > 180)
                brilloSugerido = (int)Math.Max(-40, (160 - brilloMedio) * 0.6);

            // 2. Contraste según desviación estándar (cuánto varía el gris)
            int contrasteSugerido = desviacion < 35 ? 4 : desviacion < 55 ? 2 : 1;

            // 3. Estimación de ruido / nitidez usando Laplaciano
            using Mat lap = new Mat();
            Cv2.Laplacian(gris, lap, MatType.CV_64F);
            using Mat lap32 = new Mat();
            lap.ConvertTo(lap32, MatType.CV_32F);
            Cv2.MeanStdDev(lap32, out _, out Scalar desvLap);
            double nitidezEstim = desvLap.Val0 * desvLap.Val0;

            int ruidoSugerido = nitidezEstim < 150 ? 2 : nitidezEstim < 400 ? 1 : 0;

            return (contrasteSugerido, brilloSugerido, ruidoSugerido);
        }

        // -----------------------------------------------------------------------
        // Pipeline de procesado
        // -----------------------------------------------------------------------
        internal static Mat ProcesarImagen(Mat original, int rotacion,
      int blockSize, int c,
      int ruido, int nitidez, int gruesoTexto,
      int claheClipLimit, int brillo,
      int umbralFijo, int margenRecorte,
      bool edicionManual,
      int margenSup, int margenInf, int margenIzq, int margenDer)
        {
            Log("ProcesarImagen: inicio");

            Log("ProcesarImagen: original.Empty=" + original.Empty() + " size=" + original.Size());
            Mat rotada = new Mat();
            switch (((rotacion % 360) + 360) % 360)
            {
                case 90: Cv2.Rotate(original, rotada, RotateFlags.Rotate90Clockwise); break;
                case 180: Cv2.Rotate(original, rotada, RotateFlags.Rotate180); break;
                case 270: Cv2.Rotate(original, rotada, RotateFlags.Rotate90Counterclockwise); break;
                default: rotada = original.Clone(); break;
            }
            using var _rotada = rotada;
            Log("ProcesarImagen: rotada OK, size=" + rotada.Size());

            using Mat gris = new Mat();
            Cv2.CvtColor(rotada, gris, ColorConversionCodes.BGR2GRAY);
            Log("ProcesarImagen: gris OK");

            using Mat conBrillo = new Mat();
            if (brillo != 0) gris.ConvertTo(conBrillo, MatType.CV_8U, 1.0, brillo);
            else gris.CopyTo(conBrillo);
            Log("ProcesarImagen: conBrillo OK");

            using Mat ecualizado = new Mat();
            if (claheClipLimit > 0)
            {
                using var clahe = Cv2.CreateCLAHE(clipLimit: claheClipLimit, tileGridSize: new OpenCvSharp.Size(8, 8));
                clahe.Apply(conBrillo, ecualizado);
            }
            else conBrillo.CopyTo(ecualizado);
            Log("ProcesarImagen: ecualizado OK");

            using Mat suavizado = new Mat();
            if (ruido > 0)
            {
                int d = Math.Min(9, ruido * 2 + 3);
                Cv2.BilateralFilter(ecualizado, suavizado, d, sigmaColor: 50, sigmaSpace: 50);
            }
            else ecualizado.CopyTo(suavizado);
            Log("ProcesarImagen: suavizado OK");

            using Mat normalizado = new Mat();
            Cv2.Normalize(suavizado, normalizado, 0, 255, NormTypes.MinMax);
            Log("ProcesarImagen: normalizado OK");

            Mat recortado;
            if (!edicionManual)
            {
                int areaMinPct = margenRecorte > 0 ? margenRecorte : 5;
                Log("ProcesarImagen: llamando RecortarAutomatico");
                recortado = RecortarAutomatico(rotada, gris, normalizado, areaMinPct);
                Log("ProcesarImagen: RecortarAutomatico OK, size=" + recortado.Size());
            }
            else
            {
                recortado = normalizado.Clone();
                Log("ProcesarImagen: edicionManual clone OK");
            }

            if (margenSup > 0 || margenInf > 0 || margenIzq > 0 || margenDer > 0)
            {
                int w = recortado.Width, h = recortado.Height;
                int top = h * margenSup / 100, bottom = h * margenInf / 100;
                int left = w * margenIzq / 100, right = w * margenDer / 100;
                int nw = Math.Max(1, w - left - right), nh = Math.Max(1, h - top - bottom);
                var roi = AcotarRect(new OpenCvSharp.Rect(left, top, nw, nh), w, h);
                Log($"ProcesarImagen: ROI manual w={w} h={h} roi={roi}");
                Mat manual = new Mat(recortado, roi).Clone();
                recortado.Dispose();
                recortado = manual;
                Log("ProcesarImagen: ROI manual OK");
            }

            using Mat escalado = new Mat();
            Cv2.Resize(recortado, escalado, new OpenCvSharp.Size(), 2.0, 2.0, InterpolationFlags.Cubic);
            recortado.Dispose();
            Log("ProcesarImagen: escalado OK, size=" + escalado.Size());

            using Mat preNitido = new Mat();
            using (Mat blurPre = new Mat())
            {
                Cv2.GaussianBlur(escalado, blurPre, new OpenCvSharp.Size(0, 0), 2);
                Cv2.AddWeighted(escalado, 1.35, blurPre, -0.35, 0, preNitido);
            }
            Log("ProcesarImagen: preNitido OK");

            Mat binarizado = new Mat();
            if (umbralFijo > 0)
                Cv2.Threshold(preNitido, binarizado, umbralFijo, 255, ThresholdTypes.Binary);
            else
            {
                // blockSize ya llega como (trkBlock.Value * 2 + 1) desde Reprocesar().
                // Solo garantizamos impar y >= 3; NO volvemos a doblar aquí
                // (el "bsv * 2 + 1" extra producía ventanas de 103px → texto ilegible).
                int bsv = Math.Max(3, blockSize % 2 == 0 ? blockSize + 1 : blockSize);
                Cv2.AdaptiveThreshold(preNitido, binarizado, 255,
                    AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, bsv, c);
            }
            Log("ProcesarImagen: binarizado OK");

            Mat limpio = new Mat();
            Cv2.MedianBlur(binarizado, limpio, 3);
            binarizado.Dispose();
            Log("ProcesarImagen: limpio OK");

            Mat nitidoFinal;
            if (nitidez > 0)
            {
                Mat base_ = limpio.Clone();
                limpio.Dispose();
                for (int i = 0; i < nitidez; i++)
                {
                    using Mat blurN = new Mat();
                    Cv2.GaussianBlur(base_, blurN, new OpenCvSharp.Size(0, 0), 3);
                    Mat sharpened = new Mat();
                    Cv2.AddWeighted(base_, 1.5, blurN, -0.5, 0, sharpened);
                    base_.Dispose();
                    base_ = sharpened;
                }
                nitidoFinal = base_;
            }
            else nitidoFinal = limpio;
            Log("ProcesarImagen: nitidoFinal OK");

            if (gruesoTexto != 0)
            {
                int iter = Math.Abs(gruesoTexto);
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2, 2));
                Mat morf = new Mat();
                if (gruesoTexto < 0) Cv2.Erode(nitidoFinal, morf, kernel, iterations: iter);
                else Cv2.Dilate(nitidoFinal, morf, kernel, iterations: iter);
                nitidoFinal.Dispose();
                Log("ProcesarImagen: morf OK - fin");
                return morf;
            }
            Log("ProcesarImagen: fin sin morf");
            return nitidoFinal;
        }

        internal static OpenCvSharp.Rect AcotarRect(OpenCvSharp.Rect r, int ancho, int alto)
        {
            int x = Math.Max(0, Math.Min(r.X, ancho - 1));
            int y = Math.Max(0, Math.Min(r.Y, alto - 1));
            int w = Math.Max(1, Math.Min(r.Width, ancho - x));
            int h = Math.Max(1, Math.Min(r.Height, alto - y));
            return new OpenCvSharp.Rect(x, y, w, h);
        }

        // -----------------------------------------------------------------------
        // Recorte automático
        // -----------------------------------------------------------------------
        internal static Mat RecortarAutomatico(Mat rotada, Mat gris, Mat imagenGris, int margenRecorte)
        {
            int h = gris.Rows, w = gris.Cols;
            int areaMinima = (int)(w * h * (margenRecorte / 100.0));

            // 1. Máscara principal: brillo + baja textura
            using Mat blur = new Mat();
            Cv2.GaussianBlur(gris, blur, new OpenCvSharp.Size(11, 11), 0);

            using Mat maskBrillo = new Mat();
            Cv2.Threshold(blur, maskBrillo, 130, 255, ThresholdTypes.Binary);

            // Cálculo de varianza local más eficiente
            using Mat gris32 = new Mat();
            gris.ConvertTo(gris32, MatType.CV_32F);
            using Mat mean = new Mat();
            Cv2.Blur(gris32, mean, new OpenCvSharp.Size(15, 15));
            using Mat sqMean = new Mat();
            Cv2.Multiply(mean, mean, sqMean);
            using Mat gris32sq = new Mat();
            Cv2.Multiply(gris32, gris32, gris32sq);
            using Mat meanSq = new Mat();
            Cv2.Blur(gris32sq, meanSq, new OpenCvSharp.Size(15, 15));
            using Mat varianza = new Mat();
            Cv2.Subtract(meanSq, sqMean, varianza);

            using Mat maskTexturaBaja32 = new Mat();
            Cv2.Threshold(varianza, maskTexturaBaja32, 800, 255, ThresholdTypes.BinaryInv);
            using Mat maskTexturaBaja = new Mat();
            maskTexturaBaja32.ConvertTo(maskTexturaBaja, MatType.CV_8U);

            using Mat mask = new Mat();
            Cv2.BitwiseAnd(maskBrillo, maskTexturaBaja, mask);

            // Limpieza morfológica
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(9, 9));
            using Mat maskLimpia = new Mat();
            Cv2.MorphologyEx(mask, maskLimpia, MorphTypes.Open, kernel, iterations: 2);
            Cv2.MorphologyEx(maskLimpia, mask, MorphTypes.Close, kernel, iterations: 4);

            CvPoint[][] contornos;
            Cv2.FindContours(mask, out contornos, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            CvPoint[]? mejor = null;
            double areaMax = 0;

            foreach (var cnt in contornos)
            {
                double area = Cv2.ContourArea(cnt);
                if (area > areaMax && area >= areaMinima)
                {
                    areaMax = area;
                    mejor = cnt;
                }
            }

            if (mejor != null)
            {
                var rect = Cv2.BoundingRect(mejor);
                int padding = Math.Max(15, (int)(rect.Width * 0.025));
                var roi = new OpenCvSharp.Rect(
                    Math.Max(0, rect.X - padding),
                    Math.Max(0, rect.Y - padding),
                    Math.Min(w - rect.X + padding, rect.Width + padding * 2),
                    Math.Min(h - rect.Y + padding, rect.Height + padding * 2)
                );
                return new Mat(imagenGris, AcotarRect(roi, w, h)).Clone();
            }

            // 2. Fallback Canny mejorado
            using Mat enhanced = new Mat();
            using (var clahe = Cv2.CreateCLAHE(3.0, new OpenCvSharp.Size(8, 8)))
                clahe.Apply(gris, enhanced);

            using Mat canny = new Mat();
            Cv2.Canny(enhanced, canny, 20, 80);

            using Mat dilated = new Mat();
            using var k = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
            Cv2.Dilate(canny, dilated, k, iterations: 3);

            Cv2.FindContours(dilated, out contornos, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            mejor = null;
            areaMax = 0;
            foreach (var cnt in contornos)
            {
                double area = Cv2.ContourArea(cnt);
                if (area > areaMax && area >= areaMinima * 0.6)
                {
                    areaMax = area;
                    mejor = cnt;
                }
            }

            if (mejor != null)
            {
                var rect = Cv2.BoundingRect(mejor);
                int padding = 20;
                var roi = new OpenCvSharp.Rect(rect.X - padding, rect.Y - padding,
                    rect.Width + padding * 2, rect.Height + padding * 2);
                return new Mat(imagenGris, AcotarRect(roi, w, h)).Clone();
            }

            Log("RecortarAutomatico: No se detectó contorno → sin recorte");
            return imagenGris.Clone();
        }

        internal static System.Drawing.Bitmap MatToBitmap(Mat mat)
        {
            Cv2.ImEncode(".bmp", mat, out byte[] bytes);
            using var ms = new System.IO.MemoryStream(bytes);
            using var temp = new System.Drawing.Bitmap(ms);
            return new System.Drawing.Bitmap(temp);
        }
    }
}
