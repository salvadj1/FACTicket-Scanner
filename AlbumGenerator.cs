using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    internal class AlbumGenerator
    {
        private readonly string NombreCarpeta;
        private readonly string NombreAlbum;
        private readonly string NombreDatos;

        public AlbumGenerator(string nombreCarpeta, string nombreAlbum, string nombreDatos)
        {
            NombreCarpeta = nombreCarpeta;
            NombreAlbum = nombreAlbum;
            NombreDatos = nombreDatos;
        }

        // -----------------------------------------------------------------------
        // Recorre Facturas/{Año}/{Empresa}/{Factura_x}/datos.json y reconstruye
        // la lista completa (sustituye al antiguo datos.json global único).
        // -----------------------------------------------------------------------
        private static List<DatosTicket> CargarTodasLasFacturas(string carpetaTickets)
        {
            var lista = new List<DatosTicket>();
            if (!System.IO.Directory.Exists(carpetaTickets)) return lista;

            foreach (string carpetaAnio in System.IO.Directory.GetDirectories(carpetaTickets))
                foreach (string carpetaEmpresa in System.IO.Directory.GetDirectories(carpetaAnio))
                    foreach (string carpetaFactura in System.IO.Directory.GetDirectories(carpetaEmpresa))
                    {
                        string rutaJson = System.IO.Path.Combine(carpetaFactura, "datos.json");
                        var datos = DatosTicket.CargarUnico(rutaJson);
                        if (datos != null) lista.Add(datos);
                    }
            return lista;
        }

        // -----------------------------------------------------------------------
        public void RegenerarAlbumInicial()
        {
            try
            {
                string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
                System.IO.Directory.CreateDirectory(carpetaTickets);
                var lista = CargarTodasLasFacturas(carpetaTickets);
                HtmlBuilder.GenerarAlbum(carpetaTickets, lista, NombreAlbum);
            }
            catch { }
        }

        // -----------------------------------------------------------------------
        // Guardar imagen — usa Gemini para extracción de datos
        // -----------------------------------------------------------------------
        public async void GuardarImagen(Mat imagenProcesada, Mat original, int rotacion,
            AjustesEscaner ajustes,
            bool guardarOriginal, bool guardarJpg, bool guardarPdf, bool extraerConGemini,
            Action<AjustesEscaner> guardarAjustes,
            Action<string> actualizarEstado, Action habilitarCapturar, Action guardadoTerminado)
        {
            using Mat _imagenProcesada = imagenProcesada;
            using Mat _original = original;

            try
            {
                DatosTicket datos;
                if (extraerConGemini)
                {
                    actualizarEstado("⏳ Analizando con Gemini...");
                    datos = await ExtraerConGeminiConReintento(original);

                    if (!string.IsNullOrEmpty(datos.ErrorDiagnostico))
                        MostrarAvisoAutoConfirmar(
                            "Extracción incompleta. Puedes rellenar los campos manualmente.",
                            "Aviso Gemini", segundos: 5);
                }
                else
                {
                    datos = new DatosTicket();
                }

                habilitarCapturar();

                string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
                var listaExistente = CargarTodasLasFacturas(carpetaTickets);

                DatosTicket? duplicado = BuscarPosibleDuplicado(datos, listaExistente);
                if (duplicado != null && !ConfirmarContinuarConDuplicado(duplicado))
                {
                    actualizarEstado("Guardado cancelado (factura duplicada)");
                    return;
                }

                DatosTicket? datosRevisados = MostrarDialogoRevision(datos);
                if (datosRevisados == null) { actualizarEstado("Guardado cancelado"); return; }

                string subcarpetaEmpresa;
                if (!string.IsNullOrWhiteSpace(datosRevisados.Empresa))
                {
                    subcarpetaEmpresa = ResolverCarpetaEmpresa(carpetaTickets, datosRevisados.Empresa);
                    datosRevisados.Empresa = subcarpetaEmpresa; // mismo nombre canónico en carpeta y JSON
                }
                else
                {
                    subcarpetaEmpresa = "Sin_empresa";
                }
                string nombreFactura = $"Factura_{DateTime.Now:yyyyMMdd_HHmmss}";
                string carpetaDestino = System.IO.Path.Combine(
                    carpetaTickets, DateTime.Now.Year.ToString(), subcarpetaEmpresa, nombreFactura);
                System.IO.Directory.CreateDirectory(carpetaDestino);

                string baseNombre = $"{datosRevisados?.Numero}_{datosRevisados?.Fecha}";
                baseNombre = SanearNombreCarpeta(baseNombre);
                if (string.IsNullOrWhiteSpace(baseNombre.Trim('_')))
                    baseNombre = $"procesada_{DateTime.Now:yyyyMMdd_HHmmss}";

                string rutaProcesada = System.IO.Path.Combine(carpetaDestino, baseNombre + ".jpg");
                string rutaOriginal = System.IO.Path.Combine(carpetaDestino, "original.jpg");
                string rutaPdf = System.IO.Path.Combine(carpetaDestino, baseNombre + ".pdf");
                string rutaJsonFactura = System.IO.Path.Combine(carpetaDestino, "datos.json");

                try
                {
                    if (guardarJpg) Cv2.ImWrite(rutaProcesada, imagenProcesada);
                    if (guardarOriginal) Cv2.ImWrite(rutaOriginal, original);
                    if (guardarPdf) GuardarComoPdf(imagenProcesada, rutaPdf);
                    ajustes.UltimaCarpetaGuardado = carpetaDestino;
                    guardarAjustes(ajustes);

                    datosRevisados.ImagenRelativa = guardarJpg
                        ? System.IO.Path.GetRelativePath(carpetaTickets, rutaProcesada).Replace('\\', '/')
                        : "";
                    datosRevisados.PdfRelativa = guardarPdf
                        ? System.IO.Path.GetRelativePath(carpetaTickets, rutaPdf).Replace('\\', '/')
                        : "";
                    datosRevisados.JsonRelativa = System.IO.Path.GetRelativePath(carpetaTickets, rutaJsonFactura).Replace('\\', '/');
                    datosRevisados.FechaGuardado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    DatosTicket.GuardarUnico(rutaJsonFactura, datosRevisados);

                    listaExistente.Add(datosRevisados);
                    HtmlBuilder.GenerarAlbum(carpetaTickets, listaExistente, NombreAlbum);

                    actualizarEstado($"✅ Guardado: {carpetaDestino}");
                    MessageBox.Show($"Guardado en:\n{carpetaDestino}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                habilitarCapturar();
                guardadoTerminado();
            }
        }

        // -----------------------------------------------------------------------
        // Diálogo de revisión de datos (ampliado con campos Gemini)
        // -----------------------------------------------------------------------
        private DatosTicket? MostrarDialogoRevision(DatosTicket datos)
        {
            using var dlg = new Form
            {
                Text = "Revisar datos del ticket",
                Width = 480,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            int y = 15, xLbl = 10, wLbl = 150, xTxt = 170, wTxt = 270, rowH = 32;

            var campos = new List<(string etiqueta, string valor)>
            {
                ("Empresa (emisor):",     datos.Empresa),
                ("Fecha emisión:",        datos.Fecha),
                ("Fecha vencimiento:",    datos.FechaVencimiento),
                ("Nº Factura:",           datos.Numero),
                ("CIF/NIF emisor:",       datos.Cif),
                ("Dirección emisor:",     datos.Direccion),
                ("Teléfono:",             datos.Telefono),
                ("Receptor nombre:",      datos.ReceptorNombre),
                ("Receptor CIF/NIF:",     datos.ReceptorCif),
                ("Receptor dirección:",   datos.ReceptorDireccion),
                ("Base imponible:",       datos.Base),
                ("IVA:",                  datos.Iva),
                ("Total:",                datos.Total),
                ("Método de pago:",       datos.MetodoPago),
            };

            var textBoxes = new List<TextBox>();
            foreach (var (etiqueta, valor) in campos)
            {
                dlg.Controls.Add(new Label { Text = etiqueta, Left = xLbl, Top = y + 5, Width = wLbl });
                var txt = new TextBox { Left = xTxt, Top = y, Width = wTxt, Text = valor };
                dlg.Controls.Add(txt);
                textBoxes.Add(txt);
                y += rowH;
            }

            // Mostrar items (solo lectura en el diálogo, son muchos campos)
            if (datos.Items.Count > 0)
            {
                dlg.Controls.Add(new Label
                {
                    Text = $"Líneas ({datos.Items.Count}):",
                    Left = xLbl,
                    Top = y + 5,
                    Width = wLbl,
                    ForeColor = System.Drawing.Color.DarkSlateBlue
                });
                var txtItems = new TextBox
                {
                    Left = xTxt,
                    Top = y,
                    Width = wTxt,
                    Height = 80,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    ReadOnly = true,
                    Text = string.Join("\r\n", datos.Items.Select(
                        it => $"{it.Descripcion} x{it.Cantidad} = {it.Subtotal}"))
                };
                dlg.Controls.Add(txtItems);
                y += 88;
            }

            var btnOk = new Button { Text = "Guardar", Left = 80, Top = y, Width = 120, Height = 34, DialogResult = DialogResult.OK };
            var btnCan = new Button { Text = "Cancelar", Left = 250, Top = y, Width = 120, Height = 34, DialogResult = DialogResult.Cancel };
            var lblContador = new Label { Left = 80, Top = y + 40, Width = 290, ForeColor = System.Drawing.Color.DimGray };
            dlg.Controls.Add(btnOk);
            dlg.Controls.Add(btnCan);
            dlg.Controls.Add(lblContador);
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCan;
            dlg.Height = y + 120;

            int restantes = 5;
            lblContador.Text = $"Se guardará automáticamente en {restantes}s...";
            using var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                restantes--;
                if (restantes <= 0)
                {
                    timer.Stop();
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                    return;
                }
                lblContador.Text = $"Se guardará automáticamente en {restantes}s...";
            };
            dlg.Shown += (s, e) => timer.Start();
            btnOk.Click += (s, e) => timer.Stop();
            btnCan.Click += (s, e) => timer.Stop();

            if (dlg.ShowDialog() != DialogResult.OK) return null;

            datos.Empresa = textBoxes[0].Text.Trim();
            datos.Fecha = textBoxes[1].Text.Trim();
            datos.FechaVencimiento = textBoxes[2].Text.Trim();
            datos.Numero = textBoxes[3].Text.Trim();
            datos.Cif = textBoxes[4].Text.Trim();
            datos.Direccion = textBoxes[5].Text.Trim();
            datos.Telefono = textBoxes[6].Text.Trim();
            datos.ReceptorNombre = textBoxes[7].Text.Trim();
            datos.ReceptorCif = textBoxes[8].Text.Trim();
            datos.ReceptorDireccion = textBoxes[9].Text.Trim();
            datos.Base = textBoxes[10].Text.Trim();
            datos.Iva = textBoxes[11].Text.Trim();
            datos.Total = textBoxes[12].Text.Trim();
            datos.MetodoPago = textBoxes[13].Text.Trim();
            return datos;
        }

        // -----------------------------------------------------------------------
        // Detección de facturas duplicadas.
        // Criterio: misma Empresa + mismo Nº de factura. Si no hay número de
        // factura (campo vacío en cualquiera de las dos), se usa como
        // respaldo Empresa + Fecha + Total. La comparación es insensible a
        // mayúsculas/minúsculas y a espacios sobrantes.
        // -----------------------------------------------------------------------
        private static DatosTicket? BuscarPosibleDuplicado(DatosTicket nuevo, List<DatosTicket> existentes)
        {
            string empresaNueva = NormalizarComparable(nuevo.Empresa);
            if (string.IsNullOrEmpty(empresaNueva)) return null; // sin empresa no hay base fiable de comparación

            string numeroNuevo = NormalizarComparable(nuevo.Numero);

            foreach (var existente in existentes)
            {
                string empresaExistente = NormalizarComparable(existente.Empresa);
                if (empresaExistente != empresaNueva) continue;

                string numeroExistente = NormalizarComparable(existente.Numero);

                if (!string.IsNullOrEmpty(numeroNuevo) && !string.IsNullOrEmpty(numeroExistente))
                {
                    if (numeroNuevo == numeroExistente) return existente;
                    continue; // misma empresa pero número distinto y ambos lo tienen → no es duplicado
                }

                // Respaldo: sin número de factura en alguno de los dos lados,
                // comparamos Empresa + Fecha + Total.
                string fechaNueva = NormalizarComparable(nuevo.Fecha);
                string totalNuevo = NormalizarComparable(nuevo.Total);
                string fechaExistente = NormalizarComparable(existente.Fecha);
                string totalExistente = NormalizarComparable(existente.Total);

                if (!string.IsNullOrEmpty(fechaNueva) && !string.IsNullOrEmpty(totalNuevo)
                    && fechaNueva == fechaExistente && totalNuevo == totalExistente)
                {
                    return existente;
                }
            }

            return null;
        }

        private static string NormalizarComparable(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";
            return valor.Trim().ToUpperInvariant();
        }

        // -----------------------------------------------------------------------
        // Aviso al detectar una posible factura duplicada. Devuelve true si el
        // usuario quiere continuar guardando igualmente, false si cancela.
        // -----------------------------------------------------------------------
        private static bool ConfirmarContinuarConDuplicado(DatosTicket existente)
        {
            string resumen =
                $"Empresa: {existente.Empresa}\n" +
                $"Nº Factura: {(string.IsNullOrWhiteSpace(existente.Numero) ? "(sin número)" : existente.Numero)}\n" +
                $"Fecha: {existente.Fecha}\n" +
                $"Total: {existente.Total}\n" +
                $"Guardada el: {existente.FechaGuardado}";

            return MostrarConfirmacionAutoConfirmar(
                $"Parece que esta factura ya se guardó anteriormente:\n\n{resumen}\n\n¿Quieres continuar y guardarla de nuevo?",
                "Posible factura duplicada", resultadoPorDefecto: false, segundos: 5);
        }

        // -----------------------------------------------------------------------
        // Diálogo Sí/No con cuenta atrás propia. resultadoPorDefecto se aplica
        // si el usuario no responde a tiempo.
        // -----------------------------------------------------------------------
        private static bool MostrarConfirmacionAutoConfirmar(string mensaje, string titulo, bool resultadoPorDefecto, int segundos)
        {
            using var dlg = new Form
            {
                Text = titulo,
                Width = 420,
                Height = 210,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblMensaje = new Label { Text = mensaje, Left = 15, Top = 15, Width = 380, Height = 100 };
            var lblContador = new Label { Left = 15, Top = 120, Width = 380, ForeColor = System.Drawing.Color.DimGray };
            var btnSi = new Button { Text = "Sí", Left = 130, Top = 150, Width = 100, Height = 32, DialogResult = DialogResult.Yes };
            var btnNo = new Button { Text = "No", Left = 240, Top = 150, Width = 100, Height = 32, DialogResult = DialogResult.No };
            dlg.Controls.AddRange(new Control[] { lblMensaje, lblContador, btnSi, btnNo });
            dlg.AcceptButton = resultadoPorDefecto ? btnSi : btnNo;

            int restantes = segundos;
            lblContador.Text = $"Se autoconfirmará en {restantes}s...";
            using var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                restantes--;
                if (restantes <= 0)
                {
                    timer.Stop();
                    dlg.DialogResult = resultadoPorDefecto ? DialogResult.Yes : DialogResult.No;
                    dlg.Close();
                    return;
                }
                lblContador.Text = $"Se autoconfirmará en {restantes}s...";
            };
            dlg.Shown += (s, e) => timer.Start();
            btnSi.Click += (s, e) => timer.Stop();
            btnNo.Click += (s, e) => timer.Stop();

            return dlg.ShowDialog() == DialogResult.Yes;
        }

        // -----------------------------------------------------------------------
        // Aviso simple (solo Aceptar) con cuenta atrás propia.
        // -----------------------------------------------------------------------
        private static void MostrarAvisoAutoConfirmar(string mensaje, string titulo, int segundos)
        {
            using var dlg = new Form
            {
                Text = titulo,
                Width = 420,
                Height = 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblMensaje = new Label { Text = mensaje, Left = 15, Top = 15, Width = 380, Height = 70 };
            var lblContador = new Label { Left = 15, Top = 90, Width = 380, ForeColor = System.Drawing.Color.DimGray };
            var btnOk = new Button { Text = "Aceptar", Left = 150, Top = 115, Width = 100, Height = 32, DialogResult = DialogResult.OK };
            dlg.Controls.AddRange(new Control[] { lblMensaje, lblContador, btnOk });
            dlg.AcceptButton = btnOk;

            int restantes = segundos;
            lblContador.Text = $"Se cerrará en {restantes}s...";
            using var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                restantes--;
                if (restantes <= 0)
                {
                    timer.Stop();
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                    return;
                }
                lblContador.Text = $"Se cerrará en {restantes}s...";
            };
            dlg.Shown += (s, e) => timer.Start();
            btnOk.Click += (s, e) => timer.Stop();

            dlg.ShowDialog();
        }

        // -----------------------------------------------------------------------
        // Extrae datos con Gemini. Si falla, abre el gestor de APIs para que
        // el usuario elija/edite otra clave y reintenta automáticamente.
        // -----------------------------------------------------------------------
        private static async System.Threading.Tasks.Task<DatosTicket> ExtraerConGeminiConReintento(Mat original)
        {
            try
            {
                return await System.Threading.Tasks.Task.Run(() => GeminiAPI.ExtraerDatosFactura(original));
            }
            catch (Exception exGemini)
            {
                MessageBox.Show(
                    $"No se pudo extraer datos con Gemini:\n\n{exGemini.Message}\n\nElige otra API key para reintentar.",
                    "Error Gemini", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                GeminiAPI.AbrirGestionApis();

                try
                {
                    return await System.Threading.Tasks.Task.Run(() => GeminiAPI.ExtraerDatosFactura(original));
                }
                catch (Exception exReintento)
                {
                    MessageBox.Show(
                        $"Sigue sin poder extraer datos con Gemini:\n\n{exReintento.Message}\n\nRellena los datos manualmente.",
                        "Error Gemini", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return new DatosTicket();
                }
            }
        }

        // -----------------------------------------------------------------------
        private static string SanearNombreCarpeta(string nombre)
        {
            foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(ch, '_');
            return nombre.Trim();
        }

        // -----------------------------------------------------------------------
        // Genera un PDF de una sola página embebiendo el JPG directamente
        // (filtro DCTDecode), sin necesidad de librerías externas.
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

            using var ms = new System.IO.MemoryStream();
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

            System.IO.File.WriteAllBytes(rutaPdf, ms.ToArray());
        }

        // -----------------------------------------------------------------------
        // Resuelve el nombre de carpeta de empresa a usar: si ya existe una
        // carpeta con el mismo nombre (o muy similar, tras normalizar) se
        // reutiliza en vez de crear una carpeta nueva para la misma empresa
        // real. Sin diccionario canónico: se compara contra las carpetas ya
        // presentes en disco cada vez.
        // -----------------------------------------------------------------------
        private static string ResolverCarpetaEmpresa(string carpetaTickets, string nombreEmpresaOriginal)
        {
            string candidata = SanearNombreCarpeta(nombreEmpresaOriginal);
            if (string.IsNullOrWhiteSpace(candidata)) return "Sin_empresa";

            var existentes = ObtenerNombresEmpresaExistentes(carpetaTickets);

            // Coincidencia exacta (insensible a mayúsculas) → reusar tal cual
            string? exacta = existentes.FirstOrDefault(e => string.Equals(e, candidata, StringComparison.OrdinalIgnoreCase));
            if (exacta != null) return exacta;

            // Buscar la carpeta existente más parecida (normalizando y con fuzzy match)
            string normCandidata = NormalizarNombreEmpresa(candidata);
            string? mejorMatch = null;
            double mejorSimilitud = 0;

            foreach (var existente in existentes)
            {
                double sim = CalcularSimilitud(normCandidata, NormalizarNombreEmpresa(existente));
                if (sim > mejorSimilitud)
                {
                    mejorSimilitud = sim;
                    mejorMatch = existente;
                }
            }

            const double UMBRAL_SIMILITUD = 0.82;
            if (mejorMatch != null && mejorSimilitud >= UMBRAL_SIMILITUD)
            {
                var resultado = MessageBox.Show(
                    $"El nombre de empresa detectado es:\n\"{nombreEmpresaOriginal}\"\n\n" +
                    $"Ya existe una carpeta similar:\n\"{mejorMatch}\"\n\n" +
                    "¿Es la misma empresa?\n\nSí = guardar en la carpeta existente\nNo = crear una carpeta nueva",
                    "Posible empresa duplicada", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (resultado == DialogResult.Yes) return mejorMatch;
            }

            return candidata;
        }

        private static HashSet<string> ObtenerNombresEmpresaExistentes(string carpetaTickets)
        {
            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!System.IO.Directory.Exists(carpetaTickets)) return nombres;

            foreach (string carpetaAnio in System.IO.Directory.GetDirectories(carpetaTickets))
                foreach (string carpetaEmpresa in System.IO.Directory.GetDirectories(carpetaAnio))
                    nombres.Add(System.IO.Path.GetFileName(carpetaEmpresa));

            return nombres;
        }

        // Normaliza agresivamente para comparar: mayúsculas, sin acentos, sin
        // puntuación y sin sufijos societarios habituales (SL, SA, SLU...).
        private static string NormalizarNombreEmpresa(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "";

            string s = nombre.ToUpperInvariant();

            s = string.Concat(s.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark));

            foreach (char ch in new[] { '.', ',', '_', '-' })
                s = s.Replace(ch, ' ');

            string[] sufijos = { "SLU", "SL", "SA", "SAU", "SCOOP", "SC", "SOCIEDAD LIMITADA", "SOCIEDAD ANONIMA" };
            var palabras = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            while (palabras.Count > 1 && sufijos.Contains(palabras[palabras.Count - 1]))
                palabras.RemoveAt(palabras.Count - 1);

            return string.Join(" ", palabras).Trim();
        }

        private static double CalcularSimilitud(string a, string b)
        {
            if (a == b) return 1.0;
            if (a.Length == 0 || b.Length == 0) return 0.0;

            int distancia = DistanciaLevenshtein(a, b);
            int maxLen = Math.Max(a.Length, b.Length);
            return 1.0 - (double)distancia / maxLen;
        }

        private static int DistanciaLevenshtein(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + costo);
                }

            return d[a.Length, b.Length];
        }

        // -----------------------------------------------------------------------
        // pHash: huella perceptual de 63 bits basada en DCT (8x8 de baja
        // frecuencia, sin el coeficiente DC). Robusta a compresión JPG y
        // reescalados leves, a diferencia de un hash criptográfico normal.
        // -----------------------------------------------------------------------
        private static string CalcularPHash(Mat imagen)
        {
            using Mat gris = new Mat();
            if (imagen.Channels() > 1) Cv2.CvtColor(imagen, gris, ColorConversionCodes.BGR2GRAY);
            else imagen.CopyTo(gris);

            using Mat pequena = new Mat();
            Cv2.Resize(gris, pequena, new OpenCvSharp.Size(32, 32), 0, 0, InterpolationFlags.Area);

            using Mat flotante = new Mat();
            pequena.ConvertTo(flotante, MatType.CV_32F);

            using Mat dct = new Mat();
            Cv2.Dct(flotante, dct);

            var valores = new List<float>();
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (!(x == 0 && y == 0)) valores.Add(dct.At<float>(y, x));

            var ordenados = valores.OrderBy(v => v).ToList();
            float mediana = ordenados[ordenados.Count / 2];

            var bits = new StringBuilder();
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    if (x == 0 && y == 0) continue;
                    bits.Append(dct.At<float>(y, x) > mediana ? '1' : '0');
                }

            return bits.ToString();
        }

        private static int DistanciaHamming(string a, string b)
        {
            int len = Math.Min(a.Length, b.Length);
            int distancia = Math.Abs(a.Length - b.Length);
            for (int i = 0; i < len; i++)
                if (a[i] != b[i]) distancia++;
            return distancia;
        }

        // Umbral: por debajo de estos bits distintos (de 63) se considera la
        // misma foto. Ajustable si da falsos positivos/negativos en la práctica.
        private const int UMBRAL_PHASH = 8;

        // -----------------------------------------------------------------------
        // Recorre todas las facturas guardadas y (re)calcula el pHash de su
        // imagen original, actualizando cada datos.json. Pensado para
        // ejecutarse manualmente (ej. ítem de menú) tras incorporar esta
        // función a una base de datos ya existente.
        // -----------------------------------------------------------------------
        public void EscanearPHashFacturas(Action<string> actualizarEstado)
        {
            string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
            if (!System.IO.Directory.Exists(carpetaTickets))
            {
                actualizarEstado("No hay carpeta de tickets todavía.");
                return;
            }

            var rutasJson = System.IO.Directory.GetFiles(carpetaTickets, "datos.json", System.IO.SearchOption.AllDirectories);
            int procesadas = 0, errores = 0;

            foreach (var rutaJson in rutasJson)
            {
                try
                {
                    var datos = DatosTicket.CargarUnico(rutaJson);
                    if (datos == null) continue;

                    string carpeta = System.IO.Path.GetDirectoryName(rutaJson) ?? "";
                    string rutaImagen = System.IO.Path.Combine(carpeta, "original.jpg");
                    if (!System.IO.File.Exists(rutaImagen)) continue; // sin original.jpg: se omite, no es error

                    using Mat img = Cv2.ImRead(rutaImagen);
                    if (img.Empty()) { errores++; continue; }

                    datos.PHash = CalcularPHash(img);
                    DatosTicket.GuardarUnico(rutaJson, datos);
                    procesadas++;
                    actualizarEstado($"Escaneando pHash: {procesadas + errores}/{rutasJson.Length}");
                }
                catch { errores++; }
            }

            actualizarEstado($"✅ pHash completado: {procesadas} procesadas, {errores} con error.");
        }

        // -----------------------------------------------------------------------
        // Busca si una imagen recién cargada/capturada ya coincide (por pHash)
        // con alguna factura guardada previamente.
        // -----------------------------------------------------------------------
        public DatosTicket? BuscarDuplicadoPorPHash(Mat imagenNueva, out int distanciaEncontrada)
        {
            distanciaEncontrada = -1;
            string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
            string hashNuevo = CalcularPHash(imagenNueva);

            var existentes = CargarTodasLasFacturas(carpetaTickets);
            DatosTicket? mejor = null;
            int mejorDistancia = int.MaxValue;

            foreach (var t in existentes)
            {
                if (string.IsNullOrEmpty(t.PHash)) continue;
                int d = DistanciaHamming(hashNuevo, t.PHash);
                if (d < mejorDistancia) { mejorDistancia = d; mejor = t; }
            }

            if (mejor != null && mejorDistancia <= UMBRAL_PHASH)
            {
                distanciaEncontrada = mejorDistancia;
                return mejor;
            }
            return null;
        }

        // -----------------------------------------------------------------------
        // Ajustes
        // -----------------------------------------------------------------------
        public string RutaAjustes()
        {
            string c = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FACTicket_Scanner");
            return System.IO.Path.Combine(c, "ajustes.json");
        }

        public AjustesEscaner CargarAjustes()
        {
            try
            {
                string r = RutaAjustes();
                if (System.IO.File.Exists(r))
                {
                    var v = JsonSerializer.Deserialize<AjustesEscaner>(System.IO.File.ReadAllText(r));
                    if (v != null) return v;
                }
            }
            catch { }
            return new AjustesEscaner();
        }

        public void GuardarAjustes(AjustesEscaner d)
        {
            try
            {
                string r = RutaAjustes();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(r)!);
                System.IO.File.WriteAllText(r, JsonSerializer.Serialize(d));
            }
            catch { }
        }
    }
}