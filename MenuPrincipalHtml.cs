using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

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
    // conversor, ajustes.
    // -----------------------------------------------------------------------
    public static class MenuPrincipalHtml
    {
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
        public static EstadisticasMenu CalcularEstadisticas(string carpetaFacturas, DateTime? hoy = null)
        {
            var est = new EstadisticasMenu();
            if (!Directory.Exists(carpetaFacturas)) return est;

            DateTime h = hoy ?? DateTime.Today;
            int trimActual = (h.Month - 1) / 3;
            DateTime ultimaGuardada = DateTime.MinValue;
            var porEmpresa = new Dictionary<string, (string nombre, int n)>(StringComparer.OrdinalIgnoreCase);

            foreach (string rutaJson in Directory.GetFiles(carpetaFacturas, "datos.json", SearchOption.AllDirectories))
            {
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

                if (fecha == null || fecha.Value.Year != h.Year) continue;

                est.FacturasAnio++;
                est.TotalAnio += total;

                if (fecha.Value.Month == h.Month)
                {
                    est.FacturasMes++;
                    est.TotalMes += total;
                }

                if ((fecha.Value.Month - 1) / 3 == trimActual)
                {
                    est.FacturasTrimestre++;
                    est.TotalTrimestre += total;
                    est.IvaTrimestre += ParsearImporte(t.Iva);
                    if (total > est.MasCaraImporte)
                    {
                        est.MasCaraImporte = total;
                        est.MasCaraEmpresa = empresa;
                    }
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
            return est;
        }

        private static string N0(int n) => n.ToString("N0", Es);
        private static string Eur(double v) => v.ToString("N2", Es) + " €";
        private static string Enc(string t, string vacio = "—") => string.IsNullOrWhiteSpace(t) ? vacio : WebUtility.HtmlEncode(t);

        // -------------------------------------------------------------------
        // Devuelve el documento HTML completo del menú, con las estadísticas
        // ya insertadas. 'version' se muestra en el pie (ej. "1.80 beta").
        // -------------------------------------------------------------------
        public static string Generar(EstadisticasMenu est, string version = "")
        {
            return Plantilla
                .Replace("{{HOY}}", N0(est.GuardadasHoy))
                .Replace("{{MES}}", N0(est.FacturasMes))
                .Replace("{{MESTOTAL}}", Eur(est.TotalMes))
                .Replace("{{TRIM}}", N0(est.FacturasTrimestre))
                .Replace("{{TRIMTOTAL}}", Eur(est.TotalTrimestre))
                .Replace("{{ANIO}}", N0(est.FacturasAnio))
                .Replace("{{ANIOTOTAL}}", Eur(est.TotalAnio))
                .Replace("{{PEND}}", N0(est.Pendientes))
                .Replace("{{REVISAR}}", N0(est.PorRevisar))
                .Replace("{{CARA}}", est.MasCaraImporte > 0 ? Eur(est.MasCaraImporte) : "—")
                .Replace("{{CARAEMP}}", Enc(est.MasCaraEmpresa, "sin datos"))
                .Replace("{{MEDIA}}", est.FacturasTrimestre > 0 ? Eur(est.MediaTrimestre) : "—")
                .Replace("{{IVA}}", "IVA " + Eur(est.IvaTrimestre))
                .Replace("{{FACTURAS}}", N0(est.Facturas))
                .Replace("{{EMPRESAS}}", N0(est.Empresas) + (est.Empresas == 1 ? " empresa" : " empresas"))
                .Replace("{{TOP}}", Enc(est.EmpresaTop))
                .Replace("{{TOPSUB}}", est.EmpresaTopFacturas > 0 ? N0(est.EmpresaTopFacturas) + " facturas" : "sin datos")
                .Replace("{{ULT}}", Enc(est.UltimaEmpresa))
                .Replace("{{ULTFECHA}}", Enc(est.UltimaFecha, "sin datos"))
                .Replace("{{VERSION}}", WebUtility.HtmlEncode(version));
        }

        // Plantilla a pantalla completa: izquierda = accesos, derecha = resumen.
        // En ventanas estrechas (<900 px) pasa a una sola columna.
        private const string Plantilla = @"<!DOCTYPE html>
<html lang='es'><head><meta charset='utf-8'><title>FACTicket Scanner</title>
<style>
:root{--bg:#f4f4f1;--card:#fff;--bd:#dcdcd6;--bds:#b9b9b0;--tx:#1f1f1c;--tx2:#5f5e5a;--tx3:#8a8983;--ac:#185fa5;--acbg:#e6f1fb;--acbd:#378add;--wn:#854f0b;--wnbg:#faeeda}
@media (prefers-color-scheme:dark){:root{--bg:#1c1c1a;--card:#262624;--bd:#3a3a37;--bds:#55554f;--tx:#f1efe8;--tx2:#b4b2a9;--tx3:#888780;--ac:#85b7eb;--acbg:#0c447c;--acbd:#378add;--wn:#fac775;--wnbg:#633806}}
*{box-sizing:border-box}
html{font-size:clamp(14px,0.7vw + 7px,20px)}
html,body{height:100%}
body{margin:0;background:var(--bg);color:var(--tx);font-family:'Segoe UI',system-ui,sans-serif;font-size:1rem;user-select:none}
.wrap{min-height:100%;display:flex;flex-direction:column;padding:1.6rem 2.2rem 1.1rem}
.top{display:flex;align-items:center;justify-content:space-between;gap:1rem;margin-bottom:1.4rem;flex-wrap:wrap}
.brand{display:flex;align-items:center;gap:.9rem}
.logo{width:3.1rem;height:3.1rem;border-radius:.8rem;background:var(--acbg);color:var(--ac);display:flex;align-items:center;justify-content:center;font-size:1.7rem}
.brand h1{margin:0;font-size:1.45rem;font-weight:500}
.brand p{margin:.15rem 0 0;font-size:.85rem;color:var(--tx2)}
.chip{display:inline-flex;align-items:center;gap:.4rem;background:var(--card);border:1px solid var(--bd);border-radius:.55rem;padding:.5rem .9rem;font-size:.85rem;color:var(--tx2);cursor:pointer}
.chip:hover,.ui:hover,.row:hover{border-color:var(--bds)}
.main{flex:1;display:grid;grid-template-columns:minmax(0,5fr) minmax(0,6fr);gap:2.2rem;align-content:start}
@media (max-width:900px){.main{grid-template-columns:minmax(0,1fr)}}
.panel{display:flex;align-items:center;gap:1rem;background:var(--acbg);border:1px solid var(--acbd);border-radius:.85rem;padding:1.15rem 1.3rem;margin-bottom:1rem;cursor:pointer}
.panel .ico{font-size:2rem}
.panel h2{margin:0;font-size:1.1rem;font-weight:500;color:var(--ac)}
.panel p{margin:.15rem 0 0;font-size:.85rem;color:var(--tx2)}
.go{margin-left:auto;background:var(--card);color:var(--ac);border:1px solid var(--acbd);border-radius:.55rem;padding:.6rem 1.1rem;font-size:.95rem;font-weight:500;white-space:nowrap}
.row{display:flex;align-items:center;gap:1rem;background:var(--card);border:1px solid var(--bd);border-radius:.85rem;padding:1rem 1.3rem;margin-bottom:.7rem;cursor:pointer}
.row.hot{border:2px solid var(--acbd)}
.tile{width:3rem;height:3rem;border-radius:.7rem;background:var(--acbg);display:flex;align-items:center;justify-content:center;font-size:1.55rem;flex:none}
.row h3{margin:0;font-size:1.05rem;font-weight:500}
.row p{margin:.15rem 0 0;font-size:.85rem;color:var(--tx2)}
.arrow{margin-left:auto;color:var(--tx3);font-size:1.4rem}
.sec{font-size:.85rem;color:var(--tx2);margin:1.3rem 0 .6rem}
.sec:first-child{margin-top:0}
.u{display:grid;grid-template-columns:repeat(auto-fit,minmax(12rem,1fr));gap:.65rem}
.ui{background:var(--card);border:1px solid var(--bd);border-radius:.55rem;padding:.85rem 1rem;display:flex;align-items:center;gap:.65rem;cursor:pointer}
.ui span:first-child{font-size:1.2rem}
.st{display:grid;grid-template-columns:repeat(auto-fit,minmax(11rem,1fr));gap:.65rem}
.m{background:var(--card);border:1px solid var(--bd);border-radius:.55rem;padding:.75rem .9rem;min-width:0}
.m.al{background:var(--wnbg);border-color:var(--wn)}
.m.al .l,.m.al .v,.m.al .s2{color:var(--wn)}
.m .l{font-size:.82rem;color:var(--tx2)}
.m .v{font-size:1.5rem;font-weight:500;margin-top:.2rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.m .v.s{font-size:1.05rem;margin-top:.55rem}
.m .s2{font-size:.78rem;color:var(--tx3);margin-top:.1rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.ft{margin-top:1.2rem;text-align:center;font-size:.78rem;color:var(--tx3)}
[data-a]:focus-visible{outline:2px solid var(--acbd);outline-offset:2px}
</style></head><body><div class='wrap'>
<div class='top'>
  <div class='brand'><div class='logo'>🧾</div><div><h1>FACTicket Scanner</h1><p>Escanea, ordena y exporta tus facturas</p></div></div>
  <span class='chip' data-a='ajustes' tabindex='0' role='button'>⚙️ Ajustes</span>
</div>

<div class='main'>
<div class='left'>
  <div class='panel' data-a='panel' tabindex='0' role='button'>
    <span class='ico'>📊</span>
    <div><h2>Panel web</h2><p>Visor completo con filtros, búsqueda y edición</p></div>
    <span class='go'>Abrir panel →</span>
  </div>
  <div class='row hot' data-a='escanear' tabindex='0' role='button'><div class='tile'>📷</div><div><h3>Escanear con cámara</h3><p>USB o IP, captura y procesa al momento</p></div><span class='arrow'>›</span></div>
  <div class='row' data-a='importar' tabindex='0' role='button'><div class='tile'>📂</div><div><h3>Importar archivos</h3><p>Imágenes y PDF, en lote</p></div><span class='arrow'>›</span></div>
  <div class='sec'>Gestión y utilidades</div>
  <div class='u'>
    <div class='ui' data-a='trimestre' tabindex='0' role='button'><span>📅</span>Cierre trimestral</div>
    <div class='ui' data-a='exportar' tabindex='0' role='button'><span>📦</span>Exportar</div>
    <div class='ui' data-a='duplicados' tabindex='0' role='button'><span>🔍</span>Duplicados</div>
    <div class='ui' data-a='conversor' tabindex='0' role='button'><span>📄</span>IMG a PDF</div>
  </div>
</div>

<div class='right'>
  <div class='sec'>Actividad</div>
  <div class='st'>
    <div class='m'><div class='l'>⏱️ Guardadas hoy</div><div class='v'>{{HOY}}</div><div class='s2'>última: {{ULT}}</div></div>
    <div class='m'><div class='l'>🗓️ Este mes</div><div class='v'>{{MES}}</div><div class='s2'>{{MESTOTAL}}</div></div>
    <div class='m'><div class='l'>📅 Este trimestre</div><div class='v'>{{TRIM}}</div><div class='s2'>{{TRIMTOTAL}}</div></div>
    <div class='m'><div class='l'>📆 Este año</div><div class='v'>{{ANIO}}</div><div class='s2'>{{ANIOTOTAL}}</div></div>
  </div>
  <div class='sec'>Control</div>
  <div class='st'>
    <div class='m al'><div class='l'>⏳ Sin presentar</div><div class='v'>{{PEND}}</div><div class='s2'>facturas pendientes</div></div>
    <div class='m al'><div class='l'>⚠️ Por revisar</div><div class='v'>{{REVISAR}}</div><div class='s2'>faltan datos clave</div></div>
    <div class='m'><div class='l'>💶 Media trimestre</div><div class='v'>{{MEDIA}}</div><div class='s2'>{{IVA}}</div></div>
    <div class='m'><div class='l'>💎 Más cara (trim.)</div><div class='v'>{{CARA}}</div><div class='s2'>{{CARAEMP}}</div></div>
  </div>
  <div class='sec'>General</div>
  <div class='st'>
    <div class='m'><div class='l'>🧾 Facturas</div><div class='v'>{{FACTURAS}}</div><div class='s2'>{{EMPRESAS}}</div></div>
    <div class='m'><div class='l'>🏪 Empresa top</div><div class='v s'>{{TOP}}</div><div class='s2'>{{TOPSUB}}</div></div>
    <div class='m'><div class='l'>🕘 Último guardado</div><div class='v s'>{{ULTFECHA}}</div><div class='s2'>{{ULT}}</div></div>
  </div>
</div>
</div>
<div class='ft'>FACTicket Scanner {{VERSION}}</div>
</div>
<script>
function ir(a){try{window.chrome.webview.postMessage({accion:a});}catch(e){}}
document.addEventListener('click',function(e){var t=e.target.closest('[data-a]');if(t)ir(t.getAttribute('data-a'));});
document.addEventListener('keydown',function(e){if(e.key==='Enter'||e.key===' '){var t=e.target.closest('[data-a]');if(t){e.preventDefault();ir(t.getAttribute('data-a'));}}});
document.addEventListener('contextmenu',function(e){e.preventDefault();});
</script></body></html>";
    }
}
