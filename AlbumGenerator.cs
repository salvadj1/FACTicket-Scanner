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
        // Lista de empresas a partir de las carpetas Facturas/{Año}/{Empresa}
        // que existen en disco, ignorando el contenido de los datos.json.
        // Así una empresa aparece en el filtro aunque su datos.json esté
        // corrupto, vacío o el campo "empresa" no coincida con la carpeta.
        // -----------------------------------------------------------------------
        private static List<string> ObtenerEmpresasDesdeCarpetas(string carpetaTickets)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!System.IO.Directory.Exists(carpetaTickets)) return new List<string>();

            foreach (string carpetaAnio in System.IO.Directory.GetDirectories(carpetaTickets))
                foreach (string carpetaEmpresa in System.IO.Directory.GetDirectories(carpetaAnio))
                    set.Add(System.IO.Path.GetFileName(carpetaEmpresa));

            return set.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Misma raíz "Albaranes" usada en GuardarImagen() para separar la
        // contabilidad de facturas y albaranes.
        private static string CarpetaAlbaranes() => System.IO.Path.Combine(AppContext.BaseDirectory, "Albaranes");

        // -----------------------------------------------------------------------
        public void RegenerarAlbumInicial()
        {
            try
            {
                string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
                System.IO.Directory.CreateDirectory(carpetaTickets);
                var lista = CargarTodasLasFacturas(carpetaTickets);
                string carpetaAlbaranes = CarpetaAlbaranes();
                var listaAlbaranes = CargarTodasLasFacturas(carpetaAlbaranes);
                HtmlBuilder.GenerarAlbum(carpetaTickets, lista, NombreAlbum, ObtenerEmpresasDesdeCarpetas(carpetaTickets),
                    listaAlbaranes, ObtenerEmpresasDesdeCarpetas(carpetaAlbaranes));
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
            Action<string> actualizarEstado, Action habilitarCapturar, Action guardadoTerminado,
            Func<DatosTicket, System.Threading.Tasks.Task<DatosTicket?>> mostrarRevisionEmbebida)
        {
            using Mat _imagenProcesada = imagenProcesada;
            using Mat _original = original;

            try
            {
                DatosTicket datos;
                if (extraerConGemini)
                {
                    actualizarEstado("⏳ Analizando con Gemini...");
                    datos = await ExtraerConGeminiConReintento(original, 5);

                    if (!string.IsNullOrEmpty(datos.ErrorDiagnostico))
                        DialogoAutoConfirmar.Aviso(
                            "Extracción incompleta. Puedes rellenar los campos manualmente.",
                            "Aviso Gemini");
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

                DatosTicket? datosRevisados = await mostrarRevisionEmbebida(datos);
                if (datosRevisados == null) { actualizarEstado("Guardado cancelado"); return; }

                string subcarpetaEmpresa;
                if (!string.IsNullOrWhiteSpace(datosRevisados.Empresa))
                {
                    var resolucionCarpeta = ResolverCarpetaEmpresa(carpetaTickets, datosRevisados.Empresa, datosRevisados.Cif);
                    subcarpetaEmpresa = resolucionCarpeta.carpeta;
                    bool requiereRevision = resolucionCarpeta.requiereRevision;
                    datosRevisados.Empresa = subcarpetaEmpresa; // mismo nombre canónico en carpeta y JSON

                    if (requiereRevision)
                    {
                        DatosTicket? datosReRevisados = await mostrarRevisionEmbebida(datosRevisados);
                        if (datosReRevisados == null) { actualizarEstado("Guardado cancelado"); return; }
                        datosRevisados = datosReRevisados;
                        datosRevisados.Empresa = subcarpetaEmpresa;
                    }
                }
                else
                {
                    subcarpetaEmpresa = "Sin_empresa";
                }
                string nombreFactura = $"Factura_{DateTime.Now:yyyyMMdd_HHmmss}";
                // Los albaranes NO entran en la contabilidad de facturas: van a una
                // raíz "Albaranes" aparte, con la misma estructura Año/Empresa/Doc.
                bool esAlbaran = datosRevisados.TipoDocumento == "albaran";
                string raizDestino = esAlbaran
                    ? System.IO.Path.Combine(AppContext.BaseDirectory, "Albaranes")
                    : carpetaTickets;
                string carpetaDestino = System.IO.Path.Combine(
                    raizDestino, DateTime.Now.Year.ToString(), subcarpetaEmpresa, nombreFactura);
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
                        ? System.IO.Path.GetRelativePath(raizDestino, rutaProcesada).Replace('\\', '/')
                        : "";
                    datosRevisados.PdfRelativa = guardarPdf
                        ? System.IO.Path.GetRelativePath(raizDestino, rutaPdf).Replace('\\', '/')
                        : "";
                    datosRevisados.JsonRelativa = System.IO.Path.GetRelativePath(raizDestino, rutaJsonFactura).Replace('\\', '/');
                    datosRevisados.FechaGuardado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    datosRevisados.PHash = CalcularPHash(original);

                    DatosTicket.GuardarUnico(rutaJsonFactura, datosRevisados);

                    if (!esAlbaran) listaExistente.Add(datosRevisados);
                    // Se recarga desde disco (ya incluye el recién guardado si esAlbaran==true).
                    string carpetaAlbaranesTras = CarpetaAlbaranes();
                    var listaAlbaranesTras = CargarTodasLasFacturas(carpetaAlbaranesTras);
                    HtmlBuilder.GenerarAlbum(carpetaTickets, listaExistente, NombreAlbum, ObtenerEmpresasDesdeCarpetas(carpetaTickets),
                        listaAlbaranesTras, ObtenerEmpresasDesdeCarpetas(carpetaAlbaranesTras));

                    actualizarEstado($"✅ Guardado: {carpetaDestino}");
                    DialogoAutoConfirmar.Aviso($"Guardado en:\n{carpetaDestino}", "Éxito", 2);
                }
                catch (Exception ex)
                {
                    DialogoAutoConfirmar.Aviso($"Error al guardar: {ex.Message}", "Error");
                }
            }
            finally
            {
                habilitarCapturar();
                guardadoTerminado();
            }
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

            return DialogoAutoConfirmar.Confirmar(
                $"Parece que esta factura ya se guardó anteriormente:\n\n{resumen}\n\n¿Quieres continuar y guardarla de nuevo?",
                "Posible factura duplicada", resultadoPorDefecto: false);
        }

        // -----------------------------------------------------------------------
        // Extrae datos con Gemini. Si falla, abre el gestor de APIs para que
        // el usuario elija/edite otra clave y reintenta automáticamente.
        // -----------------------------------------------------------------------

        private static async System.Threading.Tasks.Task<DatosTicket> ExtraerConGeminiConReintento(Mat original, int maxReintentos = 1)
        {
            Exception? ultimoError = null;

            for (int intento = 1; intento <= maxReintentos; intento++)
            {
                try
                {
                    return await System.Threading.Tasks.Task.Run(() => GeminiAPI.ExtraerDatosFactura(original));
                }
                catch (Exception ex)
                {
                    ultimoError = ex;

                    if (intento < maxReintentos)
                    {
                        DialogoAutoConfirmar.Aviso(
                            $"No se pudo extraer datos con Gemini (intento {intento}/{maxReintentos}):\n\n{ex.Message}\n\nElige otra API key para reintentar.",
                            "Error Gemini");
                        GeminiAPI.AbrirGestionApis();
                    }
                }
            }

            DialogoAutoConfirmar.Aviso(
                $"Sigue sin poder extraer datos con Gemini:\n\n{ultimoError?.Message}\n\nRellena los datos manualmente.",
                "Error Gemini");
            return new DatosTicket();
        }


        /* private static async System.Threading.Tasks.Task<DatosTicket> ExtraerConGeminiConReintento(Mat original)
         {
             try
             {
                 return await System.Threading.Tasks.Task.Run(() => GeminiAPI.ExtraerDatosFactura(original));
             }
             catch (Exception exGemini)
             {
                 DialogoAutoConfirmar.Aviso(
                     $"No se pudo extraer datos con Gemini:\n\n{exGemini.Message}\n\nElige otra API key para reintentar.",
                     "Error Gemini");

                 GeminiAPI.AbrirGestionApis();

                 try
                 {
                     return await System.Threading.Tasks.Task.Run(() => GeminiAPI.ExtraerDatosFactura(original));
                 }
                 catch (Exception exReintento)
                 {
                     DialogoAutoConfirmar.Aviso(
                         $"Sigue sin poder extraer datos con Gemini:\n\n{exReintento.Message}\n\nRellena los datos manualmente.",
                         "Error Gemini");
                     return new DatosTicket();
                 }
             }
         }*/

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
        private static (string carpeta, bool requiereRevision) ResolverCarpetaEmpresa(string carpetaTickets, string nombreEmpresaOriginal, string cifOriginal)
        {
            string candidata = SanearNombreCarpeta(nombreEmpresaOriginal);
            if (string.IsNullOrWhiteSpace(candidata)) return (candidata: "Sin_empresa", requiereRevision: false);

            // 1) Match por CIF: mismo CIF = misma empresa con total seguridad,
            //    sin importar cómo haya escrito Gemini el nombre esta vez
            //    (nombre comercial, legal, o ambos combinados).
            string cifNorm = NormalizarCif(cifOriginal);
            if (cifNorm.Length > 0)
            {
                var cifsExistentes = ObtenerCifsPorEmpresa(carpetaTickets);
                if (cifsExistentes.TryGetValue(cifNorm, out string? carpetaPorCif))
                    return (carpetaPorCif, false);
            }

            var existentes = ObtenerNombresEmpresaExistentes(carpetaTickets);

            // Coincidencia exacta (insensible a mayúsculas) → reusar tal cual,
            // salvo que el CIF nuevo choque con uno distinto ya guardado en esa
            // carpeta: mismo nombre pero CIF distinto = empresa distinta con
            // total probabilidad, así que se pregunta sin cuenta atrás.
            string? exacta = existentes.FirstOrDefault(e => string.Equals(e, candidata, StringComparison.OrdinalIgnoreCase));
            if (exacta != null)
            {
                if (CifDistintoEnCarpeta(carpetaTickets, exacta, cifNorm))
                {
                    var resultado = MessageBox.Show(
                        $"La empresa \"{nombreEmpresaOriginal}\" ya existe, pero con un CIF distinto al detectado ahora ({cifOriginal}).\n\n" +
                        "¿Es la misma empresa?\n\nSí = guardar en la carpeta existente\nNo = crear una carpeta nueva",
                        "CIF distinto detectado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (resultado == DialogResult.Yes) return (exacta, requiereRevision: true);
                    return (candidata, requiereRevision: false);
                }
                return (exacta, false);
            }

            // 2) Fallback sin CIF: fuzzy por conjunto de palabras. Tolera que
            //    el nombre extraído sea solo el comercial o solo el legal
            //    (subconjunto de palabras) y erratas de OCR letra a letra,
            //    a diferencia de comparar la cadena completa.
            string normCandidata = NormalizarNombreEmpresa(candidata);
            string? mejorMatch = null;
            double mejorSimilitud = 0;

            foreach (var existente in existentes)
            {
                double sim = CalcularSimilitudPalabras(normCandidata, NormalizarNombreEmpresa(existente));
                if (sim > mejorSimilitud)
                {
                    mejorSimilitud = sim;
                    mejorMatch = existente;
                }
            }

            const double UMBRAL_SIMILITUD = 0.65;
            if (mejorMatch != null && mejorSimilitud >= UMBRAL_SIMILITUD)
            {
                bool esMismaEmpresa = DialogoAutoConfirmar.Confirmar(
                    $"El nombre de empresa detectado es:\n\"{nombreEmpresaOriginal}\"\n\n" +
                    $"Ya existe una carpeta similar:\n\"{mejorMatch}\"\n\n" +
                    "¿Es la misma empresa?\n\nSí = guardar en la carpeta existente\nNo = crear una carpeta nueva",
                    "Posible empresa duplicada", resultadoPorDefecto: true);

                if (esMismaEmpresa) return (mejorMatch, false);
            }

            return (candidata, false);
        }

        // -----------------------------------------------------------------------
        // Comprueba si en la carpeta de empresa dada ya hay alguna factura con
        // un CIF distinto (y no vacío) al nuevo. Ignora cif vacíos (no hay dato
        // suficiente para afirmar que son empresas distintas).
        // -----------------------------------------------------------------------
        private static bool CifDistintoEnCarpeta(string carpetaTickets, string carpetaEmpresa, string cifNuevoNorm)
        {
            if (cifNuevoNorm.Length == 0) return false;
            foreach (var t in CargarTodasLasFacturas(carpetaTickets))
            {
                if (!string.Equals(t.Empresa, carpetaEmpresa, StringComparison.OrdinalIgnoreCase)) continue;
                string cifExistente = NormalizarCif(t.Cif);
                if (cifExistente.Length > 0 && cifExistente != cifNuevoNorm) return true;
            }
            return false;
        }

        // -----------------------------------------------------------------------
        // Aplica los datos reprocesados con Gemini desde el visor (botón ✏️).
        // Conserva ImagenRelativa/PdfRelativa/FechaGuardado/PHash (Gemini no
        // los recalcula) y resuelve la carpeta de empresa igual que al
        // guardar por primera vez: si el nombre nuevo no coincide con la
        // carpeta actual, MUEVE la carpeta de la factura para que carpeta
        // real y JSON nunca diverjan (bug de "nombres que no coinciden").
        // Limitación: no cambia de raíz Facturas/Albaranes si el tipo de
        // documento cambia durante la edición.
        // -----------------------------------------------------------------------
        public string ActualizarFacturaEditada(string rutaJsonActual, DatosTicket nuevosDatos)
        {
            var datosAntiguos = DatosTicket.CargarUnico(rutaJsonActual);
            if (datosAntiguos == null) throw new Exception("No se pudo leer la factura original.");

            nuevosDatos.FechaGuardado = datosAntiguos.FechaGuardado;
            nuevosDatos.PHash = datosAntiguos.PHash;

            string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
            string carpetaFacturaActual = System.IO.Path.GetDirectoryName(rutaJsonActual)!;
            string carpetaEmpresaActual = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(carpetaFacturaActual)!);

            string carpetaEmpresaNueva = string.IsNullOrWhiteSpace(nuevosDatos.Empresa)
                ? carpetaEmpresaActual
                : ResolverCarpetaEmpresa(carpetaTickets, nuevosDatos.Empresa, nuevosDatos.Cif).carpeta;

            string rutaJsonFinal = rutaJsonActual;

            if (!string.Equals(carpetaEmpresaNueva, carpetaEmpresaActual, StringComparison.OrdinalIgnoreCase))
            {
                string carpetaAnio = System.IO.Path.GetDirectoryName(carpetaFacturaActual)!;
                string raiz = System.IO.Path.GetDirectoryName(carpetaAnio)!;
                string nombreAnio = System.IO.Path.GetFileName(carpetaAnio);
                string nombreFactura = System.IO.Path.GetFileName(carpetaFacturaActual);
                string carpetaEmpresaDestino = System.IO.Path.Combine(raiz, nombreAnio, carpetaEmpresaNueva);
                System.IO.Directory.CreateDirectory(carpetaEmpresaDestino);
                string carpetaFacturaNueva = System.IO.Path.Combine(carpetaEmpresaDestino, nombreFactura);
                System.IO.Directory.Move(carpetaFacturaActual, carpetaFacturaNueva);
                rutaJsonFinal = System.IO.Path.Combine(carpetaFacturaNueva, "datos.json");

                string Reemplazar(string relativa) => string.IsNullOrEmpty(relativa)
                    ? relativa
                    : relativa.Replace($"/{carpetaEmpresaActual}/", $"/{carpetaEmpresaNueva}/");

                nuevosDatos.ImagenRelativa = Reemplazar(datosAntiguos.ImagenRelativa);
                nuevosDatos.PdfRelativa = Reemplazar(datosAntiguos.PdfRelativa);
                nuevosDatos.JsonRelativa = Reemplazar(datosAntiguos.JsonRelativa);
            }
            else
            {
                nuevosDatos.ImagenRelativa = datosAntiguos.ImagenRelativa;
                nuevosDatos.PdfRelativa = datosAntiguos.PdfRelativa;
                nuevosDatos.JsonRelativa = datosAntiguos.JsonRelativa;
            }

            nuevosDatos.Empresa = carpetaEmpresaNueva; // mismo nombre canónico en carpeta y JSON
            DatosTicket.GuardarUnico(rutaJsonFinal, nuevosDatos);

            var lista = CargarTodasLasFacturas(carpetaTickets);
            string carpetaAlbaranesRename = CarpetaAlbaranes();
            var listaAlbaranesRename = CargarTodasLasFacturas(carpetaAlbaranesRename);
            HtmlBuilder.GenerarAlbum(carpetaTickets, lista, NombreAlbum, ObtenerEmpresasDesdeCarpetas(carpetaTickets),
                listaAlbaranesRename, ObtenerEmpresasDesdeCarpetas(carpetaAlbaranesRename));

            return rutaJsonFinal;
        }

        // -----------------------------------------------------------------------
        // Edición completa desde el visor: reprocesa imagen+pdf sobre la MISMA
        // carpeta ya existente (no crea factura nueva) y vuelve a extraer datos
        // con Gemini. Si la empresa cambia, ActualizarFacturaEditada mueve la
        // carpeta entera a la ruta correcta.
        // -----------------------------------------------------------------------
        public async System.Threading.Tasks.Task<string> EditarFacturaCompleta(
            string rutaJsonActual, Mat imagenProcesada, Mat original,
            bool guardarOriginal, bool guardarJpg, bool guardarPdf,
            Func<DatosTicket, System.Threading.Tasks.Task<DatosTicket?>> mostrarRevisionEmbebida)
        {
            string carpetaFactura = System.IO.Path.GetDirectoryName(rutaJsonActual)!;
            var datosAntiguos = DatosTicket.CargarUnico(rutaJsonActual)
                ?? throw new Exception("No se pudo leer la factura original.");

            string baseNombre = System.IO.Path.GetFileNameWithoutExtension(
                string.IsNullOrEmpty(datosAntiguos.ImagenRelativa)
                    ? "procesada" : datosAntiguos.ImagenRelativa);

            string rutaProcesada = System.IO.Path.Combine(carpetaFactura, baseNombre + ".jpg");
            string rutaOriginal = System.IO.Path.Combine(carpetaFactura, "original.jpg");
            string rutaPdf = System.IO.Path.Combine(carpetaFactura, baseNombre + ".pdf");

            if (guardarJpg) Cv2.ImWrite(rutaProcesada, imagenProcesada);
            if (guardarOriginal) Cv2.ImWrite(rutaOriginal, original);
            if (guardarPdf) GuardarComoPdf(imagenProcesada, rutaPdf);

            DatosTicket nuevosDatos = await GeminiAPI.ExtraerDatosFactura(original);

            DatosTicket? datosRevisados = await mostrarRevisionEmbebida(nuevosDatos);
            if (datosRevisados == null) return "";

            return ActualizarFacturaEditada(rutaJsonActual, datosRevisados);
        }

        // -----------------------------------------------------------------------
        // Localiza el datos.json de una factura cuando su propio campo "json"
        // viene vacío (registros corruptos del bug antiguo de editar). Mismo
        // criterio que BuscarPosibleDuplicado: Empresa+Nº, o Empresa+Fecha+Total
        // si no hay número.
        // -----------------------------------------------------------------------
        public string? BuscarRutaJsonFactura(string empresa, string numero, string fecha, string total)
        {
            string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
            if (!System.IO.Directory.Exists(carpetaTickets)) return null;

            string empN = NormalizarComparable(empresa);
            string numN = NormalizarComparable(numero);
            string fecN = NormalizarComparable(fecha);
            string totN = NormalizarComparable(total);
            if (empN.Length == 0) return null;

            foreach (var rutaJson in System.IO.Directory.GetFiles(carpetaTickets, "datos.json", System.IO.SearchOption.AllDirectories))
            {
                var t = DatosTicket.CargarUnico(rutaJson);
                if (t == null || NormalizarComparable(t.Empresa) != empN) continue;

                if (numN.Length > 0)
                {
                    if (NormalizarComparable(t.Numero) == numN) return rutaJson;
                    continue;
                }
                if (fecN.Length > 0 && totN.Length > 0
                    && NormalizarComparable(t.Fecha) == fecN && NormalizarComparable(t.Total) == totN)
                    return rutaJson;
            }
            return null;
        }

        // -----------------------------------------------------------------------
        // CIF → carpeta de empresa, leído de los datos.json ya guardados.
        // -----------------------------------------------------------------------
        private static Dictionary<string, string> ObtenerCifsPorEmpresa(string carpetaTickets)
        {
            var mapa = new Dictionary<string, string>();
            if (!System.IO.Directory.Exists(carpetaTickets)) return mapa;

            foreach (string rutaJson in System.IO.Directory.GetFiles(carpetaTickets, "datos.json", System.IO.SearchOption.AllDirectories))
            {
                var t = DatosTicket.CargarUnico(rutaJson);
                if (t == null || string.IsNullOrWhiteSpace(t.Cif)) continue;

                string cifNorm = NormalizarCif(t.Cif);
                if (cifNorm.Length == 0) continue;

                string carpetaFactura = System.IO.Path.GetDirectoryName(rutaJson)!;
                string carpetaEmpresa = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(carpetaFactura)!);

                if (!mapa.ContainsKey(cifNorm)) mapa[cifNorm] = carpetaEmpresa;
            }
            return mapa;
        }

        private static string NormalizarCif(string cif)
        {
            if (string.IsNullOrWhiteSpace(cif)) return "";
            return new string(cif.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        // Conectores sin valor distintivo para identificar la empresa.
        private static readonly HashSet<string> CONECTORES_EMPRESA = new(StringComparer.OrdinalIgnoreCase)
        { "E", "Y", "DE", "DEL", "LA", "EL", "LOS", "LAS", "P" };

        // Similitud por conjunto de palabras: ignora el orden, tolera que una
        // de las dos sea un subconjunto de la otra (nombre comercial vs legal)
        // y usa Levenshtein palabra a palabra para tolerar erratas de OCR.
        private static double CalcularSimilitudPalabras(string a, string b)
        {
            //var palabrasA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !CONECTORES_EMPRESA.Contains(p)).Distinct().ToList();
            //var palabrasB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !CONECTORES_EMPRESA.Contains(p)).Distinct().ToList();
            var palabrasA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !CONECTORES_EMPRESA.Contains(p) && p.Length > 1).Distinct().ToList();
            var palabrasB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !CONECTORES_EMPRESA.Contains(p) && p.Length > 1).Distinct().ToList();

            if (palabrasA.Count == 0 || palabrasB.Count == 0) return 0.0;

            var (cortas, largas) = palabrasA.Count <= palabrasB.Count ? (palabrasA, palabrasB) : (palabrasB, palabrasA);

            int coincidencias = 0;
            foreach (var palabra in cortas)
            {
                double mejor = largas.Max(p => CalcularSimilitud(palabra, p));
                if (mejor >= 0.8) coincidencias++;
            }

            return (double)coincidencias / cortas.Count;
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