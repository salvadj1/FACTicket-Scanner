using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Resumen numérico que muestra el menú principal.
    // -----------------------------------------------------------------------
    public class EstadisticasMenu
    {
        // --- Actividad ---
        public int GuardadasHoy { get; set; }          // facturas guardadas hoy (por fecha de guardado)
        public int FacturasMes { get; set; }           // facturas con fecha en el mes actual
        public double TotalMes { get; set; }           // suma de totales del mes actual
        public int FacturasTrimestre { get; set; }     // facturas con fecha en el trimestre actual
        public double TotalTrimestre { get; set; }     // suma de totales del trimestre actual
        public double IvaTrimestre { get; set; }       // suma de IVA del trimestre actual
        public int FacturasAnio { get; set; }          // facturas con fecha en el año actual
        public double TotalAnio { get; set; }          // suma de totales del año actual

        // --- Control ---
        public int Pendientes { get; set; }            // facturas aún no presentadas en un trimestre
        public int PorRevisar { get; set; }            // sin empresa, sin total o sin fecha legible
        public double MasCaraImporte { get; set; }     // factura más cara del trimestre
        public string MasCaraEmpresa { get; set; } = "";
        public double MediaTrimestre =>                // importe medio por factura del trimestre
            FacturasTrimestre > 0 ? TotalTrimestre / FacturasTrimestre : 0;

        // --- Impuestos ---
        public double IvaMes { get; set; }             // suma de IVA de las facturas con fecha en el mes actual
        public double IvaAnio { get; set; }            // suma de IVA de las facturas con fecha en el año actual
        public double BaseTrimestre { get; set; }      // base imponible del trimestre actual
        // Reparto por tipo de IVA en el trimestre actual: "21", "10", "4" u "otros" -> (facturas, IVA en €)
        public Dictionary<string, (int Facturas, double Iva)> IvaPorTipo { get; set; } = new();
        // Facturas y total por trimestre de TODOS los años (clave "AAAA-N", ej. "2026-3"), por fecha de factura
        public Dictionary<string, (int Facturas, double Total)> PorTrimestre { get; set; } = new();
        // Trimestre presentado más reciente (etiqueta tal como se guardó, ej. "2026-2T") y su cierre
        public string TrimestrePresentado { get; set; } = "";
        public string CierreFecha { get; set; } = "";  // dd/MM/yyyy ("" si no se conoce)
        public bool CierreAproximado { get; set; }     // true si la fecha sale de la del archivo y no de FechaPresentado
        public int CierreDias { get; set; }            // días transcurridos desde el cierre

        // --- Calidad de los datos ---
        public int DuplicadosGrupos { get; set; }      // grupos de facturas con mismos Nº/fecha/total (según reglas)
        public int DuplicadosFacturas { get; set; }    // facturas que forman parte de esos grupos
        public int SinArchivos { get; set; }           // facturas con imagen o PDF ausente en disco
        public List<string> SinArchivosDetalle { get; set; } = new(); // una línea por factura (máx. 200)

        // --- General ---
        public int Facturas { get; set; }              // total de facturas guardadas
        public int Empresas { get; set; }              // empresas distintas
        public string EmpresaTop { get; set; } = "";   // empresa con más facturas
        public int EmpresaTopFacturas { get; set; }    // nº de facturas de esa empresa
        public string UltimaEmpresa { get; set; } = "";// última factura guardada
        public string UltimaFecha { get; set; } = "";  // cuándo se guardó (dd/MM HH:mm)
    }

    // -----------------------------------------------------------------------
    // Genera el menú principal en HTML autocontenido (sin recursos externos,
    // funciona sin conexión) y calcula las estadísticas a partir de los
    // datos.json. Clase estática e independiente del formulario: reutilizable
    // en otros proyectos que guarden facturas como DatosTicket.
    //
    // Comunicación con la aplicación: cada elemento con atributo data-a
    // envía {accion:"..."} mediante window.chrome.webview.postMessage.
    // Acciones: panel, escanear, importar, trimestre, exportar, duplicados,
    // conversor, ajustes, pendientes (panel filtrado: facturas sin presentar) y
    // revisar (panel filtrado: facturas con datos clave sin rellenar).
    //
    // Personalización: el botón "Personalizar" de la página permite ocultar/mostrar
    // tarjetas. Al pulsar "Listo" envía {accion:"tarjetas", ocultas:[ids]} con los
    // ids ocultos (ver IdsTarjetas); la aplicación los guarda y los pasa de nuevo a
    // Generar/GenerarEsqueleto en el parámetro 'ocultas'.
    // -----------------------------------------------------------------------
    public static class MenuPrincipalHtml
    {
        // -------------------------------------------------------------------
        // Ids de todas las tarjetas del menú (atributo data-c de la plantilla).
        // Es la lista blanca de ids válidos para ocultar.
        // -------------------------------------------------------------------
        public static readonly string[] IdsTarjetas =
        {
            "hoy", "ultima", "top",
            "mes", "trim", "anio", "facturas", "trimsel",
            "iva", "base", "ivatipos", "cierre",
            "pend", "revisar", "dup", "falt", "media", "cara"
        };

        // -------------------------------------------------------------------
        // Devuelve solo los ids conocidos y sin repetir. Se usa tanto al
        // guardar lo que envía la página como antes de insertarlo en el HTML,
        // para que un id inválido no pueda alterar la plantilla.
        // -------------------------------------------------------------------
        public static List<string> LimpiarIdsTarjetas(IEnumerable<string>? ids)
        {
            if (ids == null) return new List<string>();
            return ids.Where(i => IdsTarjetas.Contains(i)).Distinct().ToList();
        }

        // -------------------------------------------------------------------
        // Marca como ocultas (atributo data-off) las tarjetas indicadas. La
        // plantilla las esconde con CSS y el modo "Personalizar" las muestra
        // atenuadas para poder volver a activarlas.
        // -------------------------------------------------------------------
        public static string AplicarOcultas(string html, IEnumerable<string>? ocultas)
        {
            foreach (string id in LimpiarIdsTarjetas(ocultas))
                html = html.Replace("data-c='" + id + "'", "data-c='" + id + "' data-off='1'");
            return html;
        }

        private static readonly string[] FormatosFecha =
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
            "dd.MM.yyyy", "MM/dd/yyyy", "yyyy/MM/dd"
        };

        private static readonly CultureInfo Es = new CultureInfo("es-ES");

        // -------------------------------------------------------------------
        // Convierte un texto de fecha a DateTime (null si no se puede leer).
        // -------------------------------------------------------------------
        public static DateTime? ParsearFecha(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            string t = texto.Trim();
            if (DateTime.TryParseExact(t, FormatosFecha, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime f)) return f;
            if (DateTime.TryParse(t, Es, DateTimeStyles.None, out f)) return f;
            return null;
        }

        // -------------------------------------------------------------------
        // Convierte un importe en texto ("1.234,56 €", "34.20", "12,5") a
        // double. Si hay '.' y ',' el último separador es el decimal; con un
        // solo tipo de separador, ',' es decimal y '.' solo si tiene <=2
        // decimales (si no se toma como separador de miles). 0 si no se puede.
        // -------------------------------------------------------------------
        public static double ParsearImporte(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return 0;

            // Se dejan solo dígitos, separadores y signo
            string s = new string(texto.Where(c => char.IsDigit(c) || c == ',' || c == '.' || c == '-').ToArray());
            if (s.Length == 0) return 0;

            int ultimaComa = s.LastIndexOf(',');
            int ultimoPunto = s.LastIndexOf('.');

            if (ultimaComa >= 0 && ultimoPunto >= 0)
            {
                // El separador que aparece más a la derecha es el decimal
                if (ultimaComa > ultimoPunto) s = s.Replace(".", "").Replace(',', '.');
                else s = s.Replace(",", "");
            }
            else if (ultimaComa >= 0)
            {
                s = s.Replace(".", "").Replace(',', '.');
            }
            else if (ultimoPunto >= 0)
            {
                int decimales = s.Length - ultimoPunto - 1;
                bool variosPuntos = s.IndexOf('.') != ultimoPunto;
                if (variosPuntos || decimales == 3) s = s.Replace(".", ""); // miles
            }

            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
        }

        // -------------------------------------------------------------------
        // Recorre todos los datos.json bajo carpetaFacturas y calcula el
        // resumen. Mes/trimestre/año "actuales" se toman de hoy. Los albaranes
        // no cuentan (se guardan aparte y no suman gasto ni IVA).
        // -------------------------------------------------------------------
        // 'progreso' (opcional) recibe, cada 'cadaN' archivos leídos y al terminar, los
        // valores parciales de las tarjetas (ver ValoresTarjetas) para pintarlos
        // progresivamente; se invoca desde el hilo que calcula. 'cancelado' (opcional)
        // permite abortar el recorrido si ya no hace falta el resultado. 'reglasDup'
        // (opcional) son las reglas con las que se buscan posibles duplicados
        // (null = Nº + fecha + total, las de por defecto).
        // -------------------------------------------------------------------
        public static EstadisticasMenu CalcularEstadisticas(string carpetaFacturas, DateTime? hoy = null,
            Action<Dictionary<string, string>>? progreso = null, int cadaN = 15, Func<bool>? cancelado = null,
            ReglasDuplicados? reglasDup = null)
        {
            var est = new EstadisticasMenu();
            if (!Directory.Exists(carpetaFacturas)) return est;

            DateTime h = hoy ?? DateTime.Today;
            int trimActual = (h.Month - 1) / 3;
            DateTime ultimaGuardada = DateTime.MinValue;
            var porEmpresa = new Dictionary<string, (string nombre, int n)>(StringComparer.OrdinalIgnoreCase);
            int leidos = 0;

            // Posibles duplicados: nº de facturas por clave (valores normalizados de las reglas activas)
            var reglas = reglasDup ?? new ReglasDuplicados();
            var clavesDup = new Dictionary<string, int>();

            // Trimestre presentado más reciente: clave numérica (año*10+trimestre), etiqueta y fechas de cierre
            int mejorTrim = -1;
            string mejorEtiqueta = "";
            DateTime mejorCierreReal = DateTime.MinValue;    // máx. FechaPresentado de ese trimestre
            DateTime mejorCierreArchivo = DateTime.MinValue; // máx. fecha del archivo (solo si falta FechaPresentado)

            // Calcula los campos derivados (empresas, top, último guardado, duplicados, cierre) con lo leído hasta ahora
            void Resumir()
            {
                est.DuplicadosGrupos = clavesDup.Count(kv => kv.Value > 1);
                est.DuplicadosFacturas = clavesDup.Where(kv => kv.Value > 1).Sum(kv => kv.Value);

                if (mejorEtiqueta.Length > 0)
                {
                    bool real = mejorCierreReal != DateTime.MinValue;
                    DateTime cierre = real ? mejorCierreReal : mejorCierreArchivo;
                    est.TrimestrePresentado = mejorEtiqueta;
                    est.CierreAproximado = !real;
                    if (cierre != DateTime.MinValue)
                    {
                        est.CierreFecha = cierre.ToString("dd/MM/yyyy", Es);
                        est.CierreDias = Math.Max(0, (h.Date - cierre.Date).Days);
                    }
                }

                est.Empresas = porEmpresa.Count;
                if (porEmpresa.Count > 0)
                {
                    var top = porEmpresa.Values.OrderByDescending(x => x.n).First();
                    est.EmpresaTop = top.nombre;
                    est.EmpresaTopFacturas = top.n;
                }
                if (ultimaGuardada != DateTime.MinValue)
                    est.UltimaFecha = ultimaGuardada.ToString("dd/MM HH:mm", Es);
            }

            foreach (string rutaJson in Directory.GetFiles(carpetaFacturas, "datos.json", SearchOption.AllDirectories))
            {
                if (cancelado != null && cancelado()) return est;

                // Informe parcial cada 'cadaN' archivos: las tarjetas se rellenan poco a poco
                if (progreso != null && leidos > 0 && leidos % Math.Max(1, cadaN) == 0)
                {
                    Resumir();
                    progreso(ValoresTarjetas(est));
                }
                leidos++;

                DatosTicket? t = DatosTicket.CargarUnico(rutaJson);
                if (t == null) continue;
                if (string.Equals(t.TipoDocumento, "albaran", StringComparison.OrdinalIgnoreCase)) continue;

                est.Facturas++;

                string empresa = (t.Empresa ?? "").Trim();
                double total = ParsearImporte(t.Total);
                DateTime? fecha = ParsearFecha(t.Fecha);

                if (empresa.Length > 0)
                {
                    porEmpresa.TryGetValue(empresa, out var actual);
                    porEmpresa[empresa] = (actual.nombre ?? empresa, actual.n + 1);
                }

                if (!t.Presentado) est.Pendientes++;
                if (empresa.Length == 0 || total <= 0 || fecha == null) est.PorRevisar++;

                // Trimestre presentado más reciente y cuándo se cerró (FechaPresentado; si falta, fecha del archivo)
                if (t.Presentado && TryParsearEtiquetaTrimestre(t.TrimestrePresentado, out int claveTrim))
                {
                    bool hayFecha = DateTime.TryParse(t.FechaPresentado, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime fp);
                    DateTime fa = hayFecha ? DateTime.MinValue : File.GetLastWriteTime(rutaJson);

                    if (claveTrim > mejorTrim) // trimestre más reciente: se reinician las fechas
                    {
                        mejorTrim = claveTrim;
                        mejorEtiqueta = t.TrimestrePresentado.Trim();
                        mejorCierreReal = DateTime.MinValue;
                        mejorCierreArchivo = DateTime.MinValue;
                    }
                    if (claveTrim == mejorTrim)
                    {
                        if (hayFecha && fp > mejorCierreReal) mejorCierreReal = fp;
                        if (fa > mejorCierreArchivo) mejorCierreArchivo = fa;
                    }
                }

                // Posibles duplicados (mismas reglas que el buscador de duplicados)
                string? claveDup = ClaveDuplicado(t, reglas);
                if (claveDup != null)
                {
                    clavesDup.TryGetValue(claveDup, out int cuantas);
                    clavesDup[claveDup] = cuantas + 1;
                }

                // Archivos que faltan en disco (imagen o PDF)
                string? motivoFalta = MotivoArchivoFaltante(carpetaFacturas, rutaJson, t);
                if (motivoFalta != null)
                {
                    est.SinArchivos++;
                    if (est.SinArchivosDetalle.Count < 200)
                    {
                        string carpetaFactura = Path.GetRelativePath(carpetaFacturas, Path.GetDirectoryName(rutaJson) ?? "");
                        est.SinArchivosDetalle.Add(
                            (empresa.Length > 0 ? empresa : "(sin empresa)") + " · " +
                            (string.IsNullOrWhiteSpace(t.Fecha) ? "sin fecha" : t.Fecha.Trim()) + " · " +
                            motivoFalta + " · " + carpetaFactura);
                    }
                }

                // Fecha de guardado: facturas de hoy y la última guardada
                if (DateTime.TryParse(t.FechaGuardado, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fg))
                {
                    if (fg.Date == h.Date) est.GuardadasHoy++;
                    if (fg > ultimaGuardada)
                    {
                        ultimaGuardada = fg;
                        est.UltimaEmpresa = empresa;
                    }
                }

                // Facturas y total por trimestre de cualquier año (tarjeta "Trimestre" configurable)
                if (fecha != null)
                {
                    string claveQ = fecha.Value.Year + "-" + ((fecha.Value.Month - 1) / 3 + 1);
                    est.PorTrimestre.TryGetValue(claveQ, out var acumQ);
                    est.PorTrimestre[claveQ] = (acumQ.Facturas + 1, acumQ.Total + total);
                }

                if (fecha == null || fecha.Value.Year != h.Year) continue;

                double iva = ParsearImporte(t.Iva);

                est.FacturasAnio++;
                est.TotalAnio += total;
                est.IvaAnio += iva;

                if (fecha.Value.Month == h.Month)
                {
                    est.FacturasMes++;
                    est.TotalMes += total;
                    est.IvaMes += iva;
                }

                if ((fecha.Value.Month - 1) / 3 == trimActual)
                {
                    est.FacturasTrimestre++;
                    est.TotalTrimestre += total;
                    est.IvaTrimestre += iva;
                    est.BaseTrimestre += BaseImponible(t, total, iva);

                    string tipoIva = TipoIva(t.IvaPorcentaje);
                    est.IvaPorTipo.TryGetValue(tipoIva, out var acumIva);
                    est.IvaPorTipo[tipoIva] = (acumIva.Facturas + 1, acumIva.Iva + iva);
                    if (total > est.MasCaraImporte)
                    {
                        est.MasCaraImporte = total;
                        est.MasCaraEmpresa = empresa;
                    }
                }
            }

            Resumir();
            progreso?.Invoke(ValoresTarjetas(est)); // valores definitivos
            return est;
        }

        // -------------------------------------------------------------------
        // Base imponible de una factura: el campo "base" si tiene valor; si no,
        // total - IVA (con IVA 0 coincide con el total). 0 si no se puede deducir.
        // -------------------------------------------------------------------
        public static double BaseImponible(DatosTicket t, double total, double iva)
        {
            double b = ParsearImporte(t.Base);
            if (b > 0) return b;
            return total > iva ? total - iva : 0;
        }

        // -------------------------------------------------------------------
        // Agrupa el tipo de IVA (21, 10, 4 %) para el reparto del menú; cualquier
        // otro valor, incluido 0, va a "otros".
        // -------------------------------------------------------------------
        public static string TipoIva(double porcentaje)
        {
            int p = (int)Math.Round(porcentaje);
            return p == 21 ? "21" : p == 10 ? "10" : p == 4 ? "4" : "otros";
        }

        // -------------------------------------------------------------------
        // Convierte una etiqueta de trimestre ("2026-2T" o "2026-2") en un número
        // comparable (año*10 + trimestre). false si no tiene ese formato.
        // -------------------------------------------------------------------
        public static bool TryParsearEtiquetaTrimestre(string? etiqueta, out int clave)
        {
            clave = 0;
            if (string.IsNullOrWhiteSpace(etiqueta)) return false;
            Match m = Regex.Match(etiqueta.Trim(), @"^(\d{4})-([1-4])T?$", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            clave = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 10 +
                    int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return true;
        }

        // -------------------------------------------------------------------
        // Clave para agrupar posibles duplicados: valores normalizados de las
        // reglas activas (Nº, fecha, total, empresa), con la misma normalización
        // que ReglasDuplicados. null si el documento no tiene al menos
        // ReglasDuplicados.MinimoReglas valores con los que comparar.
        // -------------------------------------------------------------------
        public static string? ClaveDuplicado(DatosTicket t, ReglasDuplicados reglas)
        {
            var partes = new List<string>();
            if (reglas.Numero) partes.Add(ReglasDuplicados.NormalizarNumero(t.Numero));
            if (reglas.Fecha) partes.Add(ReglasDuplicados.NormalizarFecha(t.Fecha));
            if (reglas.Total) partes.Add(ReglasDuplicados.NormalizarTotal(t.Total));
            if (reglas.Empresa) partes.Add(ReglasDuplicados.NormalizarTexto(t.Empresa));

            if (partes.Count(p => p.Length > 0) < ReglasDuplicados.MinimoReglas) return null;
            return string.Join("|", partes);
        }

        // -------------------------------------------------------------------
        // Comprueba los archivos de una factura en disco. Devuelve null si está
        // todo (o si no se guardó imagen ni PDF a propósito, pero existe el
        // original.jpg junto a datos.json) y, si no, el motivo en texto.
        // Las rutas relativas de DatosTicket cuelgan de carpetaFacturas.
        // -------------------------------------------------------------------
        public static string? MotivoArchivoFaltante(string carpetaFacturas, string rutaJson, DatosTicket t)
        {
            bool hayImagen = !string.IsNullOrWhiteSpace(t.ImagenRelativa);
            bool hayPdf = !string.IsNullOrWhiteSpace(t.PdfRelativa);

            if (hayImagen && !ExisteArchivo(carpetaFacturas, t.ImagenRelativa)) return "falta la imagen";
            if (hayPdf && !ExisteArchivo(carpetaFacturas, t.PdfRelativa)) return "falta el PDF";

            if (!hayImagen && !hayPdf)
            {
                string carpeta = Path.GetDirectoryName(rutaJson) ?? "";
                if (!File.Exists(Path.Combine(carpeta, "original.jpg"))) return "sin imagen ni PDF";
            }
            return null;
        }

        // true si el archivo (ruta relativa a carpetaBase) existe; false ante cualquier error de ruta.
        private static bool ExisteArchivo(string carpetaBase, string rutaRelativa)
        {
            try { return File.Exists(Path.Combine(carpetaBase, rutaRelativa)); }
            catch { return false; }
        }

        private static string N0(int n) => n.ToString("N0", Es);
        private static string Eur(double v) => v.ToString("N2", Es) + " €";
        private static string Enc(string t, string vacio = "—") => string.IsNullOrWhiteSpace(t) ? vacio : WebUtility.HtmlEncode(t);

        // -------------------------------------------------------------------
        // Devuelve el documento HTML completo del menú, con las estadísticas
        // ya insertadas. 'version' se muestra en la barra lateral (ej. "1.80 beta").
        // 'ocultas' = ids de tarjetas que el usuario ha ocultado (null = todas visibles).
        // 'trimestreTarjeta' = trimestre elegido en la tarjeta "Trimestre" ("AAAA-N"; ""
        // o formato inválido = el trimestre anterior al actual).
        // -------------------------------------------------------------------
        public static string Generar(EstadisticasMenu est, string version = "",
            IEnumerable<string>? ocultas = null, string trimestreTarjeta = "")
        {
            string html = PlantillaBase(version, trimestreTarjeta);
            foreach (var kv in ValoresTarjetas(est))
                html = html.Replace("{{" + kv.Key + "}}", kv.Value);

            // Los datos por trimestre no tienen marcador en el HTML: se entregan a la tarjeta con su función
            html = html.Replace("</body>", "<script>cargarTrim(" + TrimestresJson(est) + ");</script></body>");
            return AplicarOcultas(html, ocultas);
        }

        // -------------------------------------------------------------------
        // Plantilla con la versión y el trimestre elegido ya insertados. El
        // trimestre solo se acepta con formato "AAAA-N" (1 a 4): así no se
        // puede colar texto en el HTML.
        // -------------------------------------------------------------------
        private static string PlantillaBase(string version, string trimestreTarjeta)
        {
            string tsel = Regex.IsMatch(trimestreTarjeta ?? "", @"^\d{4}-[1-4]$") ? trimestreTarjeta! : "";
            return Plantilla.Replace("{{VERSION}}", WebUtility.HtmlEncode(version))
                            .Replace("{{TSEL}}", tsel);
        }

        // -------------------------------------------------------------------
        // Devuelve el menú "esqueleto": mismo HTML, pero cada tarjeta lleva un
        // marcador "…" (span con data-k=CLAVE) que se rellena después enviando a
        // la página un mensaje JSON {CLAVE:"valor"} (PostWebMessageAsJson con el
        // resultado de ValoresTarjetas). Permite mostrar el menú al instante, sin
        // esperar al cálculo de estadísticas. 'ocultas' y 'trimestreTarjeta' igual que en Generar.
        // -------------------------------------------------------------------
        public static string GenerarEsqueleto(string version = "", IEnumerable<string>? ocultas = null,
            string trimestreTarjeta = "")
        {
            string html = PlantillaBase(version, trimestreTarjeta);
            foreach (string clave in ValoresTarjetas(new EstadisticasMenu()).Keys)
                html = html.Replace("{{" + clave + "}}", "<span data-k='" + clave + "' class='ld'>…</span>");
            return AplicarOcultas(html, ocultas);
        }

        // -------------------------------------------------------------------
        // Filas HTML del reparto por tipo de IVA del trimestre actual: una por
        // 21 / 10 / 4 % (siempre) y "Otros" solo si hay facturas. Cada fila
        // muestra nº de facturas, su porcentaje sobre el total y el IVA en €.
        // -------------------------------------------------------------------
        private static string FilasIvaPorTipo(EstadisticasMenu est)
        {
            int totalFacturas = est.IvaPorTipo.Values.Sum(v => v.Facturas);
            var sb = new StringBuilder();
            foreach (string tipo in new[] { "21", "10", "4", "otros" })
            {
                est.IvaPorTipo.TryGetValue(tipo, out var v);
                if (tipo == "otros" && v.Facturas == 0) continue;

                int pct = totalFacturas > 0 ? (int)Math.Round(v.Facturas * 100.0 / totalFacturas) : 0;
                string etiqueta = tipo == "otros" ? "Otros" : tipo + " %";
                sb.Append("<span class='tl'><b>").Append(etiqueta).Append("</b><span>")
                  .Append(N0(v.Facturas)).Append(v.Facturas == 1 ? " factura (" : " facturas (")
                  .Append(pct).Append(" %) · ").Append(Eur(v.Iva)).Append("</span></span>");
            }
            return sb.ToString();
        }

        // -------------------------------------------------------------------
        // Texto bajo el "último trimestre presentado": fecha del cierre y días
        // transcurridos. "(aprox.)" cuando la fecha sale de la del archivo.
        // -------------------------------------------------------------------
        private static string TextoCierre(EstadisticasMenu est)
        {
            if (est.TrimestrePresentado.Length == 0) return "ningún trimestre presentado";
            if (est.CierreFecha.Length == 0) return "fecha de cierre desconocida";
            string dias = est.CierreDias == 0 ? "hoy"
                : "hace " + N0(est.CierreDias) + (est.CierreDias == 1 ? " día" : " días");
            return est.CierreFecha + (est.CierreAproximado ? " (aprox.)" : "") + " · " + dias;
        }

        // -------------------------------------------------------------------
        // JSON {"AAAA-N":[facturas,total], ...} con todos los trimestres que
        // tienen facturas. La página lo usa para mostrar el trimestre elegido
        // en la tarjeta "Trimestre" sin volver a pedir datos a la aplicación.
        // -------------------------------------------------------------------
        private static string TrimestresJson(EstadisticasMenu est)
        {
            var datos = est.PorTrimestre.ToDictionary(
                kv => kv.Key,
                kv => new[] { (double)kv.Value.Facturas, Math.Round(kv.Value.Total, 2) });
            return System.Text.Json.JsonSerializer.Serialize(datos);
        }

        // -------------------------------------------------------------------
        // Valores (ya formateados y codificados en HTML) de cada tarjeta, por
        // clave. Las claves coinciden con los marcadores {{CLAVE}} de la plantilla.
        // -------------------------------------------------------------------
        public static Dictionary<string, string> ValoresTarjetas(EstadisticasMenu est)
        {
            return new Dictionary<string, string>
            {
                ["HOY"] = N0(est.GuardadasHoy),
                ["MES"] = N0(est.FacturasMes),
                ["MESTOTAL"] = Eur(est.TotalMes),
                ["TRIM"] = N0(est.FacturasTrimestre),
                ["TRIMTOTAL"] = Eur(est.TotalTrimestre),
                ["ANIO"] = N0(est.FacturasAnio),
                ["ANIOTOTAL"] = Eur(est.TotalAnio),
                ["PEND"] = N0(est.Pendientes),
                ["REVISAR"] = N0(est.PorRevisar),
                ["CARA"] = est.MasCaraImporte > 0 ? Eur(est.MasCaraImporte) : "—",
                ["CARAEMP"] = Enc(est.MasCaraEmpresa, "sin datos"),
                ["IVAMES"] = Eur(est.IvaMes),
                ["IVAANIO"] = Eur(est.IvaAnio),
                ["BASETRIM"] = Eur(est.BaseTrimestre),
                ["IVATIPOS"] = FilasIvaPorTipo(est),
                ["CIERRE"] = Enc(est.TrimestrePresentado, "—"),
                ["CIERRESUB"] = TextoCierre(est),
                ["DUP"] = N0(est.DuplicadosGrupos),
                ["DUPSUB"] = N0(est.DuplicadosFacturas) + (est.DuplicadosFacturas == 1 ? " factura" : " facturas"),
                ["FALT"] = N0(est.SinArchivos),
                ["_TRIM"] = TrimestresJson(est), // sin marcador en la plantilla: lo recoge la función cargarTrim
                ["MEDIA"] = est.FacturasTrimestre > 0 ? Eur(est.MediaTrimestre) : "—",
                ["IVA"] = "IVA " + Eur(est.IvaTrimestre),
                ["FACTURAS"] = N0(est.Facturas),
                ["EMPRESAS"] = N0(est.Empresas) + (est.Empresas == 1 ? " empresa" : " empresas"),
                ["TOP"] = Enc(est.EmpresaTop),
                ["TOPSUB"] = est.EmpresaTopFacturas > 0 ? N0(est.EmpresaTopFacturas) + " facturas" : "sin datos",
                ["ULT"] = Enc(est.UltimaEmpresa),
                ["ULTFECHA"] = Enc(est.UltimaFecha, "sin datos")
            };
        }

        // Plantilla a pantalla completa: barra lateral con los accesos + resumen en tarjetas.
        // Cada tarjeta lleva data-c='ID' (ver IdsTarjetas); las ocultas llevan además data-off.
        // En ventanas estrechas (<900 px) la barra lateral pasa a la parte superior.
        private const string Plantilla = @"<!DOCTYPE html>
<html lang='es'><head><meta charset='utf-8'><title>FACTicket Scanner</title>
<style>
:root{--bg:#f2f4f7;--card:#fff;--side:#101c2e;--st:#d3dcea;--st2:#8493ad;--sh:#1b2c46;--bd:#dde2ea;--bds:#aab4c5;--tx:#17202e;--tx2:#566277;--tx3:#7a869b;--ac:#1b64b8;--acfg:#fff;--acbg:#e5eefa;--wn:#8a5300;--wnbg:#fbeed6;--wnbd:#e6b765}
@media (prefers-color-scheme:dark){:root{--bg:#0e1520;--card:#162030;--side:#0a111b;--st:#d3dcea;--st2:#7d8ba4;--sh:#16233a;--bd:#27344a;--bds:#43526c;--tx:#e8edf5;--tx2:#a5b1c4;--tx3:#74829a;--ac:#6fb0f2;--acfg:#0a111b;--acbg:#16304f;--wn:#f2c378;--wnbg:#3a2a0e;--wnbd:#7a5a1e}}
*{box-sizing:border-box}
html{font-size:clamp(14px,0.7vw + 7px,20px)}
html,body{height:100%}
body{margin:0;background:var(--bg);color:var(--tx);font-family:'Segoe UI',system-ui,sans-serif;font-size:1rem;user-select:none}
.app{display:grid;grid-template-columns:16rem minmax(0,1fr);min-height:100%}
.side{background:var(--side);color:var(--st);padding:1.3rem .9rem;display:flex;flex-direction:column;gap:.25rem;position:sticky;top:0;height:100vh;align-self:start}
.brand{display:flex;align-items:center;gap:.7rem;padding:.2rem .5rem 1.2rem}
.logo{width:2.5rem;height:2.5rem;border-radius:.6rem;background:var(--ac);color:var(--acfg);display:flex;align-items:center;justify-content:center;font-size:1.35rem}
.brand b{display:block;font-size:1rem;font-weight:600;color:#fff}
.brand small{font-size:.76rem;color:var(--st2)}
.grp{font-size:.7rem;letter-spacing:.09em;text-transform:uppercase;color:var(--st2);padding:.9rem .6rem .35rem}
.nav{display:flex;align-items:center;gap:.7rem;padding:.6rem .7rem;border-radius:.5rem;cursor:pointer;font-size:.93rem}
.nav .i{width:1.5rem;text-align:center;font-size:1.1rem}
.nav:hover{background:var(--sh)}
.nav.main{background:var(--ac);color:var(--acfg);font-weight:600}
.nav.main:hover{filter:brightness(1.08)}
.sp{flex:1}
.ver{font-size:.74rem;color:var(--st2);padding:.5rem .7rem 0}
.content{padding:1.6rem 2.2rem 1.4rem;min-width:0;display:flex;flex-direction:column;gap:1.3rem}
.head{display:flex;justify-content:space-between;align-items:flex-end;gap:1rem;flex-wrap:wrap}
.head h1{margin:0;font-size:1.6rem;font-weight:600;letter-spacing:-.01em}
.head p{margin:.2rem 0 0;color:var(--tx2);font-size:.9rem}
.hb{display:flex;gap:.6rem;flex-wrap:wrap}
.btn{display:inline-flex;align-items:center;gap:.5rem;background:var(--card);border:1px solid var(--bd);border-radius:.55rem;padding:.55rem .95rem;font-size:.9rem;color:var(--tx2);font-weight:600;cursor:pointer}
.btn.pri{color:var(--ac)}
.btn:hover{border-color:var(--ac);color:var(--ac)}
.hint{display:none;align-items:center;gap:.8rem;flex-wrap:wrap;background:var(--acbg);border:1px solid var(--ac);color:var(--ac);border-radius:.6rem;padding:.6rem .9rem;font-size:.88rem}
body.edit .hint{display:flex}
.hint b{cursor:pointer;text-decoration:underline}
.sec{font-size:.78rem;letter-spacing:.08em;text-transform:uppercase;color:var(--tx3);margin:0 0 .6rem}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(11.5rem,1fr));gap:.75rem}
.m{background:var(--card);border:1px solid var(--bd);border-radius:.7rem;padding:.85rem 1rem;min-width:0}
.m .l{font-size:.82rem;color:var(--tx2)}
.m .v{font-size:1.55rem;font-weight:600;margin-top:.25rem;font-variant-numeric:tabular-nums;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.m .v.s{font-size:1.1rem;margin-top:.5rem}
.m .s2{font-size:.8rem;color:var(--tx3);margin-top:.15rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.m.big .v{font-size:2.4rem;color:var(--ac)}
.w2{grid-column:span 2}
.m .l select{font:inherit;font-size:.8rem;margin-left:.3rem;background:var(--card);color:var(--tx);border:1px solid var(--bd);border-radius:.35rem;padding:.1rem .25rem}
.tls{margin-top:.35rem}
.tl{display:flex;justify-content:space-between;gap:.8rem;font-size:.92rem;margin-top:.3rem;font-variant-numeric:tabular-nums}
.tl b{font-weight:600}
.tl span{color:var(--tx2);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.m.al{background:var(--wnbg);border-color:var(--wnbd)}
.m.al .l,.m.al .v,.m.al .s2{color:var(--wn)}
.m[data-a]{cursor:pointer}
.m[data-a]:hover{border-color:var(--wn)}
[data-off]{display:none}
[data-c]{position:relative}
body.edit [data-c]{cursor:pointer;outline:2px dashed var(--bds);outline-offset:2px}
body.edit [data-c][data-off]{display:block;opacity:.4}
body.edit [data-c]::after{content:'✓ visible';position:absolute;top:.4rem;right:.6rem;font-size:.7rem;font-weight:600;color:var(--ac)}
body.edit [data-c][data-off]::after{content:'+ oculta';color:var(--tx3)}
[role=button]:focus-visible{outline:2px solid var(--ac);outline-offset:2px}
[data-k]{transition:opacity .3s}
.ld{opacity:.35;animation:pl .9s ease-in-out infinite alternate}
@keyframes pl{to{opacity:.7}}
@media (max-width:900px){
.app{grid-template-columns:minmax(0,1fr)}
.side{position:static;height:auto;padding:1rem 1.2rem}
.sp{display:none}
.content{padding:1.2rem}
.w2{grid-column:auto}
}
</style></head><body><div class='app'>
<aside class='side'>
  <div class='brand'><div class='logo'>🧾</div><div><b>FACTicket Scanner</b><small>Escanea, ordena y exporta</small></div></div>
  <div class='grp'>Trabajar</div>
  <div class='nav main' data-a='escanear' tabindex='0' role='button'><span class='i'>📷</span>Escanear con cámara</div>
  <div class='nav' data-a='importar' tabindex='0' role='button'><span class='i'>📂</span>Importar archivos</div>
  <div class='nav' data-a='panel' tabindex='0' role='button'><span class='i'>📊</span>Panel web</div>
  <div class='grp'>Gestión y utilidades</div>
  <div class='nav' data-a='trimestre' tabindex='0' role='button'><span class='i'>📅</span>Cierre trimestral</div>
  <div class='nav' data-a='exportar' tabindex='0' role='button'><span class='i'>📦</span>Exportar</div>
  <div class='nav' data-a='duplicados' tabindex='0' role='button'><span class='i'>🔍</span>Duplicados</div>
  <div class='nav' data-a='conversor' tabindex='0' role='button'><span class='i'>📄</span>IMG a PDF</div>
  <div class='sp'></div>
  <div class='nav' data-a='ajustes' tabindex='0' role='button'><span class='i'>⚙️</span>Ajustes</div>
  <div class='ver'>Versión {{VERSION}}</div>
</aside>
<main class='content'>
  <div class='head'>
    <div><h1>Resumen</h1><p id='fecha'></p></div>
    <div class='hb'>
      <span class='btn' id='btnEdit' tabindex='0' role='button'>✏️ Personalizar</span>
      <span class='btn pri' data-a='panel' tabindex='0' role='button'>Abrir panel web →</span>
    </div>
  </div>
  <div class='hint'>Pulsa una tarjeta para ocultarla o volver a mostrarla. <b id='btnTodas' tabindex='0' role='button'>Mostrar todas</b></div>

  <section data-sec>
    <h2 class='sec'>Hoy</h2>
    <div class='grid'>
      <div class='m big' data-c='hoy'><div class='l'>⏱️ Guardadas hoy</div><div class='v'>{{HOY}}</div><div class='s2'>facturas nuevas</div></div>
      <div class='m' data-c='ultima'><div class='l'>🕘 Último guardado</div><div class='v s'>{{ULT}}</div><div class='s2'>{{ULTFECHA}}</div></div>
      <div class='m' data-c='top'><div class='l'>🏪 Empresa top</div><div class='v s'>{{TOP}}</div><div class='s2'>{{TOPSUB}}</div></div>
    </div>
  </section>

  <section data-sec>
    <h2 class='sec'>Actividad</h2>
    <div class='grid'>
      <div class='m' data-c='mes'><div class='l'>🗓️ Este mes</div><div class='v'>{{MES}}</div><div class='s2'>{{MESTOTAL}}</div></div>
      <div class='m' data-c='trim'><div class='l'>📅 Este trimestre</div><div class='v'>{{TRIM}}</div><div class='s2'>{{TRIMTOTAL}}</div></div>
      <div class='m' data-c='anio'><div class='l'>📆 Este año</div><div class='v'>{{ANIO}}</div><div class='s2'>{{ANIOTOTAL}}</div></div>
      <div class='m' data-c='facturas'><div class='l'>🧾 Facturas guardadas</div><div class='v'>{{FACTURAS}}</div><div class='s2'>{{EMPRESAS}}</div></div>
      <div class='m' data-c='trimsel'><div class='l'>🗂️ Trimestre <select id='selTrim' title='Elegir el trimestre que muestra esta tarjeta'></select></div><div class='v ld' id='tqN'>…</div><div class='s2' id='tqS'>&nbsp;</div></div>
    </div>
  </section>

  <section data-sec>
    <h2 class='sec'>Impuestos</h2>
    <div class='grid'>
      <div class='m' data-c='iva'><div class='l'>🧮 IVA este mes</div><div class='v'>{{IVAMES}}</div><div class='s2'>este año: {{IVAANIO}}</div></div>
      <div class='m' data-c='base'><div class='l'>🧾 Base imponible (trim.)</div><div class='v'>{{BASETRIM}}</div><div class='s2'>total con IVA {{TRIMTOTAL}}</div></div>
      <div class='m' data-c='cierre'><div class='l'>✅ Último trim. presentado</div><div class='v'>{{CIERRE}}</div><div class='s2'>{{CIERRESUB}}</div></div>
      <div class='m w2' data-c='ivatipos'><div class='l'>📊 IVA por tipo (trim.)</div><div class='tls'>{{IVATIPOS}}</div></div>
    </div>
  </section>

  <section data-sec>
    <h2 class='sec'>Control</h2>
    <div class='grid'>
      <div class='m al' data-c='pend' data-a='pendientes' tabindex='0' role='button'><div class='l'>⏳ Sin presentar</div><div class='v'>{{PEND}}</div><div class='s2'>ver facturas pendientes →</div></div>
      <div class='m al' data-c='revisar' data-a='revisar' tabindex='0' role='button'><div class='l'>⚠️ Por revisar</div><div class='v'>{{REVISAR}}</div><div class='s2'>ver facturas incompletas →</div></div>
      <div class='m al' data-c='dup' data-a='duplicados' tabindex='0' role='button'><div class='l'>👯 Posibles duplicados</div><div class='v'>{{DUP}}</div><div class='s2'>{{DUPSUB}} · abrir buscador →</div></div>
      <div class='m al' data-c='falt' data-a='faltantes' tabindex='0' role='button'><div class='l'>📎 Archivos faltantes</div><div class='v'>{{FALT}}</div><div class='s2'>sin imagen o PDF · ver lista →</div></div>
      <div class='m' data-c='media'><div class='l'>💶 Media trimestre</div><div class='v'>{{MEDIA}}</div><div class='s2'>{{IVA}}</div></div>
      <div class='m' data-c='cara'><div class='l'>💎 Más cara (trim.)</div><div class='v'>{{CARA}}</div><div class='s2'>{{CARAEMP}}</div></div>
    </div>
  </section>
</main></div>
<script>
function ir(a){try{window.chrome.webview.postMessage({accion:a});}catch(e){}}
var edit=false;
function ocultas(){var r=[],l=document.querySelectorAll('[data-c][data-off]');for(var i=0;i<l.length;i++)r.push(l[i].getAttribute('data-c'));return r;}
function secciones(){var s=document.querySelectorAll('[data-sec]');for(var i=0;i<s.length;i++){var n=s[i].querySelectorAll('[data-c]:not([data-off])').length;s[i].style.display=(n||edit)?'':'none';}}
function setEdit(on){edit=on;document.body.classList.toggle('edit',on);document.getElementById('btnEdit').textContent=on?'✔ Listo':'✏️ Personalizar';if(!on){try{window.chrome.webview.postMessage({accion:'tarjetas',ocultas:ocultas()});}catch(e){}}secciones();}
document.addEventListener('click',function(e){
  if(e.target.closest('#btnEdit')){setEdit(!edit);return;}
  if(edit){
    if(e.target.closest('select'))return;
    if(e.target.closest('#btnTodas')){var o=document.querySelectorAll('[data-c][data-off]');for(var i=0;i<o.length;i++)o[i].removeAttribute('data-off');return;}
    var c=e.target.closest('[data-c]');
    if(c){if(c.hasAttribute('data-off'))c.removeAttribute('data-off');else c.setAttribute('data-off','1');}
    return;
  }
  var t=e.target.closest('[data-a]');if(t)ir(t.getAttribute('data-a'));
});
document.addEventListener('keydown',function(e){if(e.key==='Enter'||e.key===' '){var t=e.target.closest('[role=button]');if(t){e.preventDefault();t.click();}}});
document.addEventListener('contextmenu',function(e){e.preventDefault();});
try{document.getElementById('fecha').textContent=new Date().toLocaleDateString('es-ES',{weekday:'long',day:'numeric',month:'long',year:'numeric'});}catch(x){}
/* Tarjeta Trimestre: el desplegable elige el trimestre; los datos de todos llegan en un solo mensaje (_TRIM) */
var trimData=null,tsel='{{TSEL}}';
function trimAnterior(){var d=new Date(),q=Math.floor(d.getMonth()/3),y=d.getFullYear();if(q===0){q=4;y--;}return y+'-'+q;}
function eur(v){return v.toLocaleString('es-ES',{minimumFractionDigits:2,maximumFractionDigits:2})+' €';}
function renderTrim(){
  var s=document.getElementById('selTrim');if(!s)return;
  var ant=trimAnterior(),ef=tsel||ant,ks=trimData?Object.keys(trimData):[];
  if(ks.indexOf(ant)<0)ks.push(ant);
  if(ks.indexOf(ef)<0)ks.push(ef);
  ks.sort().reverse();
  s.innerHTML='';
  var o=document.createElement('option');o.value='';o.textContent='Anterior (auto)';s.appendChild(o);
  for(var i=0;i<ks.length;i++){var p=ks[i].split('-');o=document.createElement('option');o.value=ks[i];o.textContent=p[1]+'T '+p[0];s.appendChild(o);}
  s.value=tsel;
  if(!trimData)return;
  var v=trimData[ef]||[0,0],q=ef.split('-'),n=document.getElementById('tqN');
  n.textContent=v[0];n.classList.remove('ld');
  document.getElementById('tqS').textContent=q[1]+'T '+q[0]+' · '+(v[0]===1?'1 factura':v[0]+' facturas')+' · '+eur(v[1]);
}
function cargarTrim(x){try{trimData=typeof x==='string'?JSON.parse(x):x;}catch(e){return;}renderTrim();}
document.getElementById('selTrim').addEventListener('change',function(){tsel=this.value;renderTrim();try{window.chrome.webview.postMessage({accion:'trimestreTarjeta',valor:tsel});}catch(e){}});
renderTrim();
secciones();
try{window.chrome.webview.addEventListener('message',function(e){var d=e.data;if(!d)return;for(var k in d){if(k==='_TRIM'){cargarTrim(d[k]);continue;}var l=document.querySelectorAll('[data-k='+k+']');for(var i=0;i<l.length;i++){l[i].innerHTML=d[k];l[i].classList.remove('ld');}}});}catch(x){}
</script></body></html>";
    }
}
