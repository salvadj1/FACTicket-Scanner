using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Generador de un ÁLBUM HTML independiente (un único archivo, sin
    // dependencias externas ni del programa) para acompañar a una exportación.
    //
    // El álbum es un listado en tabla (fecha, nº, empresa, tipo, base, IVA, total,
    // estado) con cabeceras ordenables, búsqueda, filtros, fila de totales y panel
    // de vista previa (imagen/PDF). No ofrece descargas. Los datos van embebidos como JSON
    // dentro del propio HTML; los archivos se enlazan por ruta RELATIVA (por
    // eso Album.html debe quedarse junto a la carpeta de archivos).
    //
    // Clase REUTILIZABLE: solo depende de DatosTicket para CrearEntrada();
    // GenerarHtml() trabaja únicamente con EntradaAlbum.
    // -----------------------------------------------------------------------
    internal static class AlbumExportador
    {
        // -------------------------------------------------------------------
        // Registro de un documento tal y como se serializa dentro del álbum.
        // Las propiedades se escriben en camelCase (empresa, fechaIso, ...).
        // Las rutas de archivo van vacías si ese formato no se exportó.
        // -------------------------------------------------------------------
        public sealed class EntradaAlbum
        {
            public string Empresa { get; set; } = "";
            public string Cif { get; set; } = "";
            public string Direccion { get; set; } = "";
            public string Telefono { get; set; } = "";
            public string Fecha { get; set; } = "";
            public string FechaIso { get; set; } = "";
            public string FechaVencimiento { get; set; } = "";
            public string Numero { get; set; } = "";
            public string Tipo { get; set; } = "factura";
            public string ReceptorNombre { get; set; } = "";
            public string ReceptorCif { get; set; } = "";
            public string ReceptorDireccion { get; set; } = "";
            public string MetodoPago { get; set; } = "";
            public double BaseNum { get; set; }
            public double IvaNum { get; set; }
            public double IvaPorcentaje { get; set; }
            public double TotalNum { get; set; }
            public bool Presentado { get; set; }
            public string Trimestre { get; set; } = "";
            public List<ItemFactura> Items { get; set; } = new List<ItemFactura>();
            public string ArchivoPdf { get; set; } = "";
            public string ArchivoImagen { get; set; } = "";
            public string ArchivoOriginal { get; set; } = "";
            public string ArchivoJson { get; set; } = "";
        }

        // -------------------------------------------------------------------
        // Crea la entrada del álbum para un documento.
        // Cada archivoXxx es la ruta relativa DENTRO del ZIP (p. ej.
        // "Facturas/Makro_2026-04-12_A1021.pdf") o null/vacío si no se exportó.
        // -------------------------------------------------------------------
        public static EntradaAlbum CrearEntrada(DatosTicket t, string? archivoPdf, string? archivoImagen,
            string? archivoOriginal, string? archivoJson)
        {
            DateTime? fecha = FiltrosExportacion.ParsearFecha(t.Fecha);
            return new EntradaAlbum
            {
                Empresa = t.Empresa ?? "",
                Cif = t.Cif ?? "",
                Direccion = t.Direccion ?? "",
                Telefono = t.Telefono ?? "",
                Fecha = t.Fecha ?? "",
                FechaIso = fecha.HasValue ? fecha.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                FechaVencimiento = t.FechaVencimiento ?? "",
                Numero = t.Numero ?? "",
                Tipo = string.IsNullOrWhiteSpace(t.TipoDocumento) ? "factura" : t.TipoDocumento.Trim().ToLowerInvariant(),
                ReceptorNombre = t.ReceptorNombre ?? "",
                ReceptorCif = t.ReceptorCif ?? "",
                ReceptorDireccion = t.ReceptorDireccion ?? "",
                MetodoPago = t.MetodoPago ?? "",
                BaseNum = FiltrosExportacion.ParsearImporte(t.Base),
                IvaNum = FiltrosExportacion.ParsearImporte(t.Iva),
                IvaPorcentaje = t.IvaPorcentaje,
                TotalNum = FiltrosExportacion.ParsearImporte(t.Total),
                Presentado = t.Presentado,
                Trimestre = t.TrimestrePresentado ?? "",
                Items = t.Items ?? new List<ItemFactura>(),
                ArchivoPdf = archivoPdf ?? "",
                ArchivoImagen = archivoImagen ?? "",
                ArchivoOriginal = archivoOriginal ?? "",
                ArchivoJson = archivoJson ?? ""
            };
        }

        // -------------------------------------------------------------------
        // Nombre base (sin extensión) "Empresa_AAAA-MM-DD_Numero" para los
        // archivos exportados. Solo letras, dígitos y '-'; el resto pasa a '_'.
        // -------------------------------------------------------------------
        public static string NombreBase(DatosTicket t)
        {
            DateTime? f = FiltrosExportacion.ParsearFecha(t.Fecha);
            string fecha = f.HasValue ? f.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "sin-fecha";
            return Compactar(t.Empresa, "SinEmpresa", 40) + "_" + fecha + "_" + Compactar(t.Numero, "sn", 30);
        }

        // -------------------------------------------------------------------
        // Devuelve un nombre de archivo que no esté en 'usados' (sin distinguir
        // mayúsculas), añadiendo _2, _3... antes de la extensión si hace falta,
        // y lo registra en 'usados'.
        // -------------------------------------------------------------------
        public static string NombreUnico(string nombreConExtension, ISet<string> usados)
        {
            if (usados.Add(nombreConExtension)) return nombreConExtension;

            string ext = System.IO.Path.GetExtension(nombreConExtension);
            string raiz = nombreConExtension.Substring(0, nombreConExtension.Length - ext.Length);
            for (int i = 2; ; i++)
            {
                string candidato = raiz + "_" + i + ext;
                if (usados.Add(candidato)) return candidato;
            }
        }

        // Letras/dígitos/'-' se conservan; lo demás -> '_' (sin repetir ni en los extremos).
        private static string Compactar(string? texto, string porDefecto, int maxLongitud)
        {
            if (string.IsNullOrWhiteSpace(texto)) return porDefecto;
            var sb = new StringBuilder();
            foreach (char c in texto.Trim())
            {
                char r = (char.IsLetterOrDigit(c) || c == '-') ? c : '_';
                if (r == '_' && sb.Length > 0 && sb[sb.Length - 1] == '_') continue;
                sb.Append(r);
                if (sb.Length >= maxLongitud) break;
            }
            string s = sb.ToString().Trim('_');
            return s.Length == 0 ? porDefecto : s;
        }

        // -------------------------------------------------------------------
        // Genera el HTML completo del álbum.
        //   entradas : documentos a mostrar.
        //   titulo   : título principal de la página.
        //   subtitulo: línea secundaria (rango de fechas, fecha de generación...).
        // -------------------------------------------------------------------
        public static string GenerarHtml(IEnumerable<EntradaAlbum> entradas, string titulo, string subtitulo)
        {
            var opciones = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            // El codificador por defecto escapa <, >, & y ' => seguro dentro de <script>.
            string json = JsonSerializer.Serialize(entradas.ToList(), opciones);

            return Plantilla
                .Replace("{{TITULO}}", WebUtility.HtmlEncode(titulo))
                .Replace("{{SUBTITULO}}", WebUtility.HtmlEncode(subtitulo))
                .Replace("{{DATOS}}", json);
        }

        // -------------------------------------------------------------------
        // Plantilla del álbum. IMPORTANTE: no usar comillas dobles dentro (el
        // texto es una cadena literal de C#); todo el HTML/CSS/JS usa simples.
        // -------------------------------------------------------------------
        private const string Plantilla = @"<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>{{TITULO}}</title>
<style>
:root{--bg:#eef0f2;--panel:#fff;--ink:#16232b;--mut:#5c6b75;--line:#d8dfe4;--acc:#1d6a55;--acc-t:#dff0ea;--warn:#8a5a00;--warn-t:#fbefd5;--sel:#e8f4ef;--head:#f4f6f7}
@media(prefers-color-scheme:dark){:root{--bg:#10171b;--panel:#182126;--ink:#e6edf0;--mut:#93a3ad;--line:#2a373e;--acc:#5fc2a4;--acc-t:#173a30;--warn:#f0c36a;--warn-t:#3b2d0f;--sel:#1b2b29;--head:#1d292f}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.4 'Segoe UI',system-ui,sans-serif}
header{display:flex;align-items:baseline;gap:14px;flex-wrap:wrap;padding:16px 24px 6px}
h1{font:600 24px Georgia,'Times New Roman',serif;margin:0}
.sub{color:var(--mut)}
.bar{display:flex;gap:8px;flex-wrap:wrap;padding:8px 24px 12px}
.bar input,.bar select{height:34px;border:1px solid var(--line);border-radius:6px;background:var(--panel);color:var(--ink);padding:0 10px;font:inherit}
.bar input{flex:1;min-width:200px;max-width:380px}
.bar .cnt{margin-left:auto;align-self:center;color:var(--mut)}
input:focus-visible,select:focus-visible,th:focus-visible{outline:2px solid var(--acc);outline-offset:1px}
main{display:grid;grid-template-columns:minmax(0,1.6fr) minmax(320px,1fr);gap:14px;padding:0 24px 24px;align-items:start}
.tabla{background:var(--panel);border:1px solid var(--line);border-radius:8px;overflow:auto;max-height:calc(100vh - 130px)}
table{width:100%;border-collapse:collapse;white-space:nowrap}
th,td{padding:7px 12px;border-bottom:1px solid var(--line);text-align:left}
thead th{position:sticky;top:0;background:var(--head);font-weight:600;font-size:12.5px;color:var(--mut);cursor:pointer;user-select:none;z-index:1}
thead th:hover{color:var(--ink)}
thead th.asc::after{content:' \25B2';font-size:10px}
thead th.desc::after{content:' \25BC';font-size:10px}
tbody tr{cursor:pointer}
tbody tr:hover{background:var(--head)}
tbody tr.sel{background:var(--sel);box-shadow:inset 3px 0 0 var(--acc)}
tbody tr:last-child td{border-bottom:0}
.r{text-align:right;font-variant-numeric:tabular-nums}
tfoot td{position:sticky;bottom:0;background:var(--head);font-weight:600;border-top:1px solid var(--line);border-bottom:0}
.nota{font-weight:400;color:var(--mut);font-size:12px}
.chip{display:inline-block;padding:0 8px;border-radius:10px;font-size:12px;background:var(--warn-t);color:var(--warn)}
.chip.ok{background:var(--acc-t);color:var(--acc)}
.vacio{padding:34px 18px;text-align:center;color:var(--mut)}
#prev{position:sticky;top:12px;background:var(--panel);border:1px solid var(--line);border-radius:8px;height:calc(100vh - 130px);display:flex;flex-direction:column;overflow:hidden}
.ph{padding:12px 16px;border-bottom:1px solid var(--line)}
.ph b{font:600 16px Georgia,serif;display:block}
.ph span{color:var(--mut);font-size:13px}
.tabs{display:flex;gap:4px;padding:8px 16px 0;background:var(--head)}
.tabs button{border:1px solid var(--line);border-bottom:0;border-radius:6px 6px 0 0;background:transparent;color:var(--mut);padding:5px 14px;font:inherit;cursor:pointer}
.tabs button.on{background:var(--panel);color:var(--ink);font-weight:600}
.pv{flex:1;min-height:0;overflow:auto;background:var(--bg);display:flex;align-items:flex-start;justify-content:center}
.pv img{max-width:100%;max-height:100%;margin:auto;cursor:zoom-in}
.pv img.z{max-width:none;max-height:none;margin:0;cursor:zoom-out}
.pv iframe{width:100%;height:100%;border:0;background:#fff}
@media(max-width:900px){main{grid-template-columns:1fr;padding:0 12px 16px}header,.bar{padding-left:12px;padding-right:12px}.tabla{max-height:60vh}#prev{position:static;height:420px}}
</style>
</head>
<body>
<header><h1>{{TITULO}}</h1><span class='sub'>{{SUBTITULO}}</span></header>
<div class='bar'>
<input id='q' type='search' placeholder='Buscar empresa, CIF, número o concepto' aria-label='Buscar'>
<select id='fp' aria-label='Presentación'><option value=''>Presentación: todas</option><option value='p'>Presentadas</option><option value='n'>No presentadas</option></select>
<select id='ft' aria-label='Tipo'><option value=''>Tipo: todos</option><option value='factura'>Facturas</option><option value='albaran'>Albaranes</option><option value='ticket'>Tickets</option></select>
<select id='fe' aria-label='Empresa'></select>
<span class='cnt' id='cnt'></span>
</div>
<main>
<div class='tabla'><table>
<thead><tr>
<th data-k='fechaIso' tabindex='0'>Fecha</th>
<th data-k='numero' tabindex='0'>Nº factura</th>
<th data-k='empresa' tabindex='0'>Empresa</th>
<th data-k='tipo' tabindex='0'>Tipo</th>
<th data-k='baseNum' class='r' tabindex='0'>Base</th>
<th data-k='ivaNum' class='r' tabindex='0'>IVA</th>
<th data-k='totalNum' class='r' tabindex='0'>Total</th>
<th data-k='presentado' tabindex='0'>Estado</th>
</tr></thead>
<tbody id='cuerpo'></tbody>
<tfoot><tr><td colspan='4'>Totales <span class='nota'>(sin albaranes)</span></td><td class='r' id='tb'></td><td class='r' id='ti'></td><td class='r' id='tt'></td><td></td></tr></tfoot>
</table></div>
<div id='prev'></div>
</main>
<script id='datos' type='application/json'>{{DATOS}}</script>
<script>
(function(){
var D=JSON.parse(document.getElementById('datos').textContent);
var $=function(id){return document.getElementById(id)};
var eur=function(n){return Number(n||0).toLocaleString('es-ES',{style:'currency',currency:'EUR'})};
var esc=function(s){return String(s==null?'':s).replace(/[&<>']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','\'':'&#39;'}[c]})};
var url=function(p){return p.split('/').map(encodeURIComponent).join('/')};
var norm=function(s){return String(s||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase()};
var fechaTxt=function(d){return d.fechaIso?d.fechaIso.split('-').reverse().join('/'):(d.fecha||'sin fecha')};
var sum=function(a,k){return a.reduce(function(s,d){return s+(d[k]||0)},0)};
var vista=[],cur=null,tab='img',sk='fechaIso',sd=-1;
D.forEach(function(d,i){d._n=i;d._s=norm([d.empresa,d.cif,d.numero,d.receptorNombre,d.receptorCif,(d.items||[]).map(function(x){return x.descripcion}).join(' ')].join(' '))});
var emp={};D.forEach(function(d){if(d.empresa)emp[d.empresa]=1});
$('fe').innerHTML='<option value=\'\'>Empresa: todas</option>'+Object.keys(emp).sort(function(a,b){return a.localeCompare(b,'es')}).map(function(v){return '<option>'+esc(v)+'</option>'}).join('');
function cmp(a,b){
var x=a[sk],y=b[sk];
var r=(typeof x==='string')?x.localeCompare(y,'es',{numeric:true}):(Number(x)-Number(y));
return r*sd||a._n-b._n}
function filtrar(){
var q=norm($('q').value).split(' ').filter(Boolean),p=$('fp').value,t=$('ft').value,e=$('fe').value;
vista=D.filter(function(d){return q.every(function(w){return d._s.indexOf(w)>=0})&&(!p||(p==='p')===!!d.presentado)&&(!t||d.tipo===t)&&(!e||d.empresa===e)});
vista.sort(cmp);
if(vista.indexOf(cur)<0)cur=vista[0]||null;
pintar()}
function pintar(){
$('cuerpo').innerHTML=vista.length?vista.map(function(d,i){
return '<tr class=\''+(d===cur?'sel':'')+'\' data-i=\''+i+'\'><td>'+fechaTxt(d)+'</td><td>'+esc(d.numero||'-')+'</td><td>'+esc(d.empresa||'(sin empresa)')+'</td><td>'+esc(d.tipo)+'</td><td class=\'r\'>'+eur(d.baseNum)+'</td><td class=\'r\'>'+eur(d.ivaNum)+'</td><td class=\'r\'>'+eur(d.totalNum)+'</td><td><span class=\'chip'+(d.presentado?' ok':'')+'\'>'+(d.presentado?esc(d.trimestre||'presentada'):'pendiente')+'</span></td></tr>'}).join(''):'<tr><td colspan=\'8\' class=\'vacio\'>Sin resultados con estos filtros.</td></tr>';
var c=vista.filter(function(d){return d.tipo!=='albaran'});
$('tb').textContent=eur(sum(c,'baseNum'));$('ti').textContent=eur(sum(c,'ivaNum'));$('tt').textContent=eur(sum(c,'totalNum'));
$('cnt').textContent=vista.length+' de '+D.length+' documentos';
Array.prototype.forEach.call(document.querySelectorAll('th[data-k]'),function(h){
var on=h.getAttribute('data-k')===sk;
h.className=(h.className.replace(/\b(asc|desc)\b/g,'').trim()+(on?(sd>0?' asc':' desc'):'')).trim();
h.setAttribute('aria-sort',on?(sd>0?'ascending':'descending'):'none')});
previa()}
function previa(){
var X=$('prev'),d=cur;
if(!d){X.innerHTML='<div class=\'vacio\'>Selecciona un documento.</div>';return}
var vs=[['img','Procesado',d.archivoImagen],['ori','Original',d.archivoOriginal],['pdf','PDF',d.archivoPdf]].filter(function(v){return v[2]});
if(vs.length&&!vs.some(function(v){return v[0]===tab}))tab=vs[0][0];
var act=vs.filter(function(v){return v[0]===tab})[0];
var cab='<div class=\'ph\'><b>'+esc(d.empresa||'(sin empresa)')+'</b><span>'+fechaTxt(d)+' &middot; '+esc(d.numero||'s/n')+' &middot; '+eur(d.totalNum)+'</span></div>';
if(!act){X.innerHTML=cab+'<div class=\'vacio\'>Este documento no incluye imagen ni PDF.</div>';return}
var tabs=vs.length>1?'<div class=\'tabs\'>'+vs.map(function(v){return '<button type=\'button\' data-t=\''+v[0]+'\' class=\''+(v[0]===tab?'on':'')+'\'>'+v[1]+'</button>'}).join('')+'</div>':'';
var pv=act[0]==='pdf'?'<iframe src=\''+url(act[2])+'#toolbar=0&navpanes=0\' title=\'PDF\'></iframe>':'<img src=\''+url(act[2])+'\' alt=\'Documento\'>';
X.innerHTML=cab+tabs+'<div class=\'pv\'>'+pv+'</div>'}
$('cuerpo').addEventListener('click',function(e){var f=e.target.closest('tr[data-i]');if(f){cur=vista[+f.getAttribute('data-i')];pintar()}});
$('prev').addEventListener('click',function(e){
var b=e.target.closest('button[data-t]');
if(b){tab=b.getAttribute('data-t');previa();return}
if(e.target.tagName==='IMG')e.target.classList.toggle('z')});
function ordenar(h){
var k=h.getAttribute('data-k');
if(sk===k)sd=-sd;else{sk=k;sd=(k==='fechaIso'||k==='totalNum'||k==='baseNum'||k==='ivaNum'||k==='presentado')?-1:1}
filtrar()}
Array.prototype.forEach.call(document.querySelectorAll('th[data-k]'),function(h){
h.addEventListener('click',function(){ordenar(h)});
h.addEventListener('keydown',function(e){if(e.key==='Enter'||e.key===' '){e.preventDefault();ordenar(h)}})});
['q','fp','ft','fe'].forEach(function(id){$(id).addEventListener('input',filtrar)});
document.addEventListener('keydown',function(e){
if(/INPUT|SELECT|TEXTAREA|TH/.test(document.activeElement.tagName))return;
var i=vista.indexOf(cur);
if(e.key==='ArrowDown'&&i<vista.length-1)cur=vista[i+1];else if(e.key==='ArrowUp'&&i>0)cur=vista[i-1];else return;
e.preventDefault();pintar();
var s=document.querySelector('tr.sel');if(s)s.scrollIntoView({block:'nearest'})});
filtrar();
})();
</script>
</body>
</html>";
    }
}
