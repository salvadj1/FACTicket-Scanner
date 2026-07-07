using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FACTicket_Scanner
{
    internal static class HtmlBuilder
    {
        internal static void GenerarAlbum(string carpetaTickets, List<DatosTicket> lista, string nombreAlbum, List<string>? empresasCarpetas = null,
            List<DatosTicket>? listaAlbaranes = null, List<string>? empresasCarpetasAlbaranes = null)
        {
            string rutaHtml = System.IO.Path.Combine(carpetaTickets, nombreAlbum);
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>Panel de Facturas</title><style>");
            sb.AppendLine(Css());
            sb.AppendLine("</style></head><body>");
            sb.AppendLine(Html());
            sb.AppendLine("<script>");
            sb.AppendLine("let facturasData=" + JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = false }) + ";");
            sb.AppendLine("let albaranesData=" + JsonSerializer.Serialize(listaAlbaranes ?? new List<DatosTicket>(), new JsonSerializerOptions { WriteIndented = false }) + ";");
            // Empresas obtenidas de las carpetas en disco (no del JSON de cada ticket).
            sb.AppendLine("let empresasCarpetasFacturas=" + JsonSerializer.Serialize(empresasCarpetas ?? new List<string>()) + ";");
            sb.AppendLine("let empresasCarpetasAlbaranes=" + JsonSerializer.Serialize(empresasCarpetasAlbaranes ?? new List<string>()) + ";");
            sb.AppendLine("let tipoActual='facturas';");
            sb.AppendLine("let tickets=facturasData;");
            sb.AppendLine("let empresasCarpetas=empresasCarpetasFacturas;");
            sb.AppendLine($"const generado=\"{DateTime.Now:dd/MM/yyyy HH:mm}\";");
            sb.AppendLine(Js());
            sb.AppendLine("</script></body></html>");

            System.IO.File.WriteAllText(rutaHtml, sb.ToString(), System.Text.Encoding.UTF8);
        }

        private static string Html() => @"
<div id=""layout"">

  <!-- ═══════════════════════════════════════════════════════════
       PANEL IZQUIERDO — estadísticas fijas
  ═══════════════════════════════════════════════════════════ -->
  <aside id=""panel-izq"">

    <!-- Estadísticas -->
    <div class=""bloque"">
      <div class=""bloque-titulo"">Resumen</div>
      <div id=""stats""></div>
    </div>

    <!-- Gráfico trimestral -->
    <div class=""bloque"">
      <div class=""bloque-titulo"" style=""display:flex;justify-content:space-between;align-items:center"">
        <span>Gasto trimestral</span>
        <div style=""display:flex;gap:4px"">
          <select id=""anioSel"" onchange=""sincronizarAnio(this.value);dibujarGrafico();filtrarTrimestre()""></select>
          <select id=""trimSel"" onchange=""filtrarTrimestre()"">
            <option value="""">Año completo</option>
            <option value=""1"">T1 (Ene-Mar)</option>
            <option value=""2"">T2 (Abr-Jun)</option>
            <option value=""3"">T3 (Jul-Sep)</option>
            <option value=""4"">T4 (Oct-Dic)</option>
          </select>
        </div>
      </div>
      <canvas id=""grafico"" height=""160""></canvas>
    </div>

    <!-- Gráfico por empresa (top 5) -->
    <div class=""bloque"">
      <div class=""bloque-titulo"">Top empresas</div>
      <div id=""grafico-empresas""></div>
    </div>

    <!-- IVA acumulado por trimestre (Modelo 303), mismo año/trimestre que arriba -->
    <div class=""bloque"">
      <div class=""bloque-titulo"">IVA soportado por trimestre</div>
      <canvas id=""grafico-iva-trim"" height=""120""></canvas>
    </div>
  </aside>

  <!-- ═══════════════════════════════════════════════════════════
       PANEL DERECHO — controles + listado scrollable
  ═══════════════════════════════════════════════════════════ -->
  <main id=""panel-der"">

    <!-- Controles -->
    <div id=""controles"">
      <div id=""tabs-tipo"">
        <button class=""tab-tipo activo"" onclick=""cambiarTipo('facturas',this)"">📄 Facturas</button>
        <button class=""tab-tipo"" onclick=""cambiarTipo('albaranes',this)"">📦 Albaranes</button>
      </div>
      <input type=""text"" id=""buscar"" placeholder=""🔍 Buscar empresa, número, CIF, fecha..."" oninput=""filtrar()"">
      <div id=""controles-fila2"">
        <div class=""ctrl-grupo"">
          <label>Vista</label>
          <div id=""btns-vista"">
            <button class=""btn-vista activo"" onclick=""setVista('empresa',this)"" title=""Por empresa"">🏢</button>
            <button class=""btn-vista"" onclick=""setVista('total_desc',this)"" title=""Mayor importe"">💰↓</button>
            <button class=""btn-vista"" onclick=""setVista('total_asc',this)"" title=""Menor importe"">💰↑</button>
            <button class=""btn-vista"" onclick=""setVista('fecha_desc',this)"" title=""Más recientes"">📅↓</button>
            <button class=""btn-vista"" onclick=""setVista('fecha_asc',this)"" title=""Más antiguos"">📅↑</button>
            <button class=""btn-vista"" onclick=""setVista('lista',this)"" title=""Vista lista"">☰</button>
          </div>
        </div>
        <div class=""ctrl-grupo"">
          <label>Año</label>
          <select id=""filtroAnio"" onchange=""sincronizarAnio(this.value,true);filtrar()""><option value="""">Todos</option></select>
        </div>
        <div class=""ctrl-grupo"">
          <label>Empresa</label>
          <select id=""filtroEmpresa"" onchange=""filtrar()""><option value="""">Todas</option></select>
        </div>
        <span id=""contador""></span>
      </div>
    </div>

    <!-- Listado scrollable -->
    <div id=""listado"">
      <div id=""contenido""></div>
    </div>
  </main>
</div>

<!-- ═══════════════════════════════════════════════════════════
     MODAL — pantalla completa: imagen + datos JSON
═══════════════════════════════════════════════════════════ -->
<div id=""modal"">
  <div id=""modal-inner"">
    <div id=""modal-header"">
      <!-- modal-nav eliminado: los controles nativos viven ahora en
           panelBarraVisor (WinForms): btnAnteriorVisor/btnSiguienteVisor/
           btnEditarVisor/btnEliminarVisor/btnCerrarModalVisor en Form1. -->
      <div id=""modal-nav"" style=""display:none""></div>
    </div>
    <div id=""modal-body"">
      <div id=""modal-img-wrap"">
        <img id=""modal-foto"" src="""" />
        <div id=""modal-sin-img"">Sin imagen</div>
      </div>
      <div id=""modal-datos"">
        <div id=""modal-tabs"">
          <button class=""tab activo"" onclick=""setTab('datos',this)"">Datos</button>
          <button class=""tab"" onclick=""setTab('json',this)"">JSON</button>
        </div>
        <div id=""tab-datos""></div>
        <pre id=""tab-json"" style=""display:none""></pre>
      </div>
    </div>
  </div>
</div>
";

        private static string Css() => @"
:root{
  --azul:#1a73e8;--azul-s:#e8f0fe;
  --verde:#137333;--verde-s:#e6f4ea;
  --rojo:#c5221f;--rojo-s:#fce8e6;
  --naranja:#e37400;--naranja-s:#fef3e2;
  --gris:#5f6368;--borde:#e0e0e0;
  --iz-w:340px;
}
*{box-sizing:border-box;margin:0;padding:0;}
body{font-family:'Segoe UI',Arial,sans-serif;background:#f0f2f5;color:#202124;height:100vh;overflow:hidden;}

/* ── Layout principal ── */
#layout{display:flex;height:100vh;overflow:hidden;}

/* ── Panel izquierdo ── */
#panel-izq{
  width:var(--iz-w);min-width:var(--iz-w);
  background:#fff;border-right:1px solid var(--borde);
  display:flex;flex-direction:column;overflow-y:auto;
  box-shadow:2px 0 8px rgba(0,0,0,.06);
}
#panel-header{
  background:linear-gradient(135deg,#1a73e8,#1559b3);
  color:#fff;padding:18px 16px 14px;flex-shrink:0;
}
#panel-title{font-size:1.1em;font-weight:700;}
#panel-gen{font-size:.75em;opacity:.8;margin-top:3px;}

.bloque{padding:14px 14px 10px;border-bottom:1px solid var(--borde);}
.bloque-titulo{font-size:.72em;font-weight:700;text-transform:uppercase;
  letter-spacing:.06em;color:var(--gris);margin-bottom:10px;}

/* Stats */
#stats{display:flex;flex-direction:column;gap:7px;}
.stat-row{display:flex;justify-content:space-between;align-items:baseline;
  padding:5px 8px;border-radius:7px;background:#f8f9fa;}
.stat-row .lbl{font-size:.8em;color:var(--gris);}
.stat-row .val{font-size:.95em;font-weight:700;color:#1a1a1a;}
.stat-row.verde .val{color:var(--verde);}
.stat-row.rojo .val{color:var(--rojo);}
.stat-row.azul .val{color:var(--azul);}
.stat-row.clicable{cursor:pointer;}
.stat-row.clicable:hover{background:#eef1f4;}
.stat-row.clicable.activo{background:#dbe7fb;}

/* Gráfico barras trimestral */
#grafico{width:100%;display:block;}
#anioSel{padding:3px 6px;border:1px solid var(--borde);border-radius:6px;font-size:.8em;}
#tabs-tipo{display:flex;gap:6px;margin-bottom:8px;}
.tab-tipo{padding:6px 14px;border:1px solid var(--borde);border-radius:8px;background:#f8f9fa;
  cursor:pointer;font-size:.85em;font-weight:600;color:var(--gris);}
.tab-tipo.activo{background:var(--azul);color:#fff;border-color:var(--azul);}

/* Top empresas barras horizontales */
#grafico-empresas{display:flex;flex-direction:column;gap:6px;}
.emp-bar-wrap{display:flex;flex-direction:column;gap:2px;}
.emp-bar-lbl{display:flex;justify-content:space-between;font-size:.75em;color:#333;}
.emp-bar-track{height:8px;background:#eee;border-radius:4px;overflow:hidden;}
.emp-bar-fill{height:100%;border-radius:4px;background:var(--azul);transition:width .4s;}

/* Gráfico IVA trimestral */
#grafico-iva-trim{width:100%;display:block;}

/* ── Panel derecho ── */
#panel-der{flex:1;display:flex;flex-direction:column;overflow:hidden;min-width:0;}

/* Controles */
#controles{
  background:#fff;border-bottom:1px solid var(--borde);
  padding:10px 16px;flex-shrink:0;
  box-shadow:0 1px 4px rgba(0,0,0,.05);
}
#buscar{
  width:100%;padding:9px 13px;border:1px solid var(--borde);
  border-radius:8px;font-size:.92em;margin-bottom:8px;
  background:#f8f9fa;transition:border .2s;
}
#buscar:focus{outline:none;border-color:var(--azul);background:#fff;}
#controles-fila2{display:flex;flex-wrap:wrap;gap:8px;align-items:center;}
.ctrl-grupo{display:flex;align-items:center;gap:5px;}
.ctrl-grupo label{font-size:.75em;color:var(--gris);white-space:nowrap;}
.ctrl-grupo select{padding:5px 8px;border:1px solid var(--borde);border-radius:6px;font-size:.82em;}
#btns-vista{display:flex;gap:3px;}
.btn-vista{
  padding:5px 9px;border:1px solid var(--borde);border-radius:6px;
  background:#f8f9fa;cursor:pointer;font-size:.82em;transition:all .15s;
}
.btn-vista:hover{background:var(--azul-s);border-color:var(--azul);}
.btn-vista.activo{background:var(--azul);color:#fff;border-color:var(--azul);}
#contador{margin-left:auto;padding:4px 12px;background:var(--azul-s);
  border-radius:20px;font-size:.8em;color:var(--azul);white-space:nowrap;}

/* ── Listado ── */
#listado{flex:1;overflow-y:auto;padding:12px 16px;}

/* Vista por empresa / agrupada */
.empresa-grupo{margin-bottom:14px;}
.empresa-cab{
  display:flex;align-items:center;gap:8px;
  background:#fff;border-radius:10px 10px 0 0;
  padding:10px 14px;border-bottom:2px solid var(--azul);
  font-weight:600;color:var(--azul);font-size:.9em;
}
.empresa-cab .count{background:var(--azul-s);color:var(--azul);
  border-radius:12px;padding:1px 9px;font-size:.75em;}
.empresa-cab .suma{margin-left:auto;font-size:.82em;color:var(--gris);font-weight:500;}
.galeria{
  display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));
  gap:10px;padding:12px;
  background:#fff;border-radius:0 0 10px 10px;
  box-shadow:0 1px 4px rgba(0,0,0,.06);
}

/* Tarjeta */
.tarjeta{
  border-radius:8px;overflow:hidden;cursor:pointer;
  border:1px solid #d5d8dc;transition:transform .15s,box-shadow .15s;
  background:#e9ebee;box-shadow:0 1px 3px rgba(0,0,0,.08);
}
.tarjeta:hover{transform:translateY(-3px);box-shadow:0 6px 16px rgba(0,0,0,.13);}
.tarjeta img{
  width:100%;height:110px;object-fit:cover;object-position:top;display:block;
  filter:contrast(1.15) saturate(1.05);
  box-shadow:inset 0 0 0 1px rgba(0,0,0,.08);
}
.img-wrap{position:relative;}
.lineas-txt{position:relative;height:110px;padding:6px 8px;background:#f8f9fa;
  overflow:hidden;display:flex;align-items:flex-start;}
.lineas-desc{font-size:.72em;color:#444;line-height:1.3;}
.badge-lineas{
  position:absolute;top:4px;right:4px;z-index:1;
  background:rgba(0,0,0,.55);color:#fff;font-size:.68em;
  padding:2px 6px;border-radius:10px;
}
.tarjeta .ph{height:110px;background:#eee;display:flex;align-items:center;
  justify-content:center;color:#aaa;font-size:.78em;}
.tarjeta .resumen{padding:8px;}
.tarjeta .fecha{font-size:.72em;color:#888;}
.tarjeta .numero{font-size:.76em;color:#555;white-space:nowrap;
  overflow:hidden;text-overflow:ellipsis;}
.badge{display:inline-block;margin-top:5px;border-radius:10px;
  padding:2px 7px;font-size:.78em;font-weight:600;
  background:var(--verde-s);color:var(--verde);}
.badge.vacio{background:var(--rojo-s);color:var(--rojo);}

/* Vista lista (tabla) */
#tabla-lista{width:100%;border-collapse:collapse;background:#fff;
  border-radius:10px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,.06);}
#tabla-lista th{background:#f5f7fa;padding:9px 12px;text-align:left;
  font-size:.78em;color:var(--gris);text-transform:uppercase;
  border-bottom:2px solid var(--borde);}
#tabla-lista td{padding:8px 12px;font-size:.83em;border-bottom:1px solid #f0f0f0;vertical-align:middle;}
#tabla-lista tr:hover td{background:#f8f9fa;cursor:pointer;}

#vacio{text-align:center;padding:60px;color:#aaa;}

/* ── Modal ── */
#modal{
  display:none;position:fixed;inset:0;
  background:rgba(0,0,0,.8);z-index:1000;
}
#modal.activo{display:flex;align-items:stretch;}
#modal-inner{
  background:#fff;width:100%;height:100%;
  display:flex;flex-direction:column;
}
#modal-header{
  display:flex;justify-content:center;align-items:center;
  padding:12px 20px;border-bottom:1px solid var(--borde);
  background:#fff;flex-shrink:0;
}
/* Columnas de ancho fijo: los botones nunca cambian de sitio, solo
   el título (columna central) se trunca con ellipsis si no cabe. */
#modal-nav{
  display:grid;grid-template-columns:42px 380px 42px 42px 42px;
  align-items:center;gap:8px;
}
#modal-nav button{
  padding:5px 0;border:1px solid var(--borde);border-radius:6px;
  background:#f8f9fa;cursor:pointer;font-size:.9em;width:100%;
}
#modal-nav button:hover{background:var(--azul-s);}
#modal-titulo{
  font-size:1em;font-weight:700;color:var(--azul);
  text-align:center;overflow:hidden;text-overflow:ellipsis;
  white-space:nowrap;
}
#modal-editar{background:#f8f9fa;}
#modal-editar:hover{background:#fef3e2;}
#modal-cerrar{
  font-size:1.3em;color:#888;border:none;background:#f0f0f0;
  cursor:pointer;padding:4px 0;border-radius:6px;border:1px solid var(--borde);
}
#modal-cerrar:hover{background:var(--rojo-s);color:var(--rojo);}
#trimSel{padding:3px 6px;border:1px solid var(--borde);border-radius:6px;font-size:.8em;}
#modal-body{
  display:flex;flex:1;overflow:hidden;
}
#modal-img-wrap{
  width:50%;border-right:1px solid var(--borde);
  display:flex;align-items:center;justify-content:center;
  background:#f8f9fa;overflow:hidden;padding:16px;
}
#modal-foto{max-width:100%;max-height:100%;object-fit:contain;border-radius:6px;}
#modal-sin-img{color:#aaa;font-size:.9em;display:none;}
#modal-datos{width:50%;display:flex;flex-direction:column;overflow:hidden;}
#modal-tabs{display:flex;border-bottom:1px solid var(--borde);flex-shrink:0;}
.tab{
  padding:10px 20px;border:none;background:none;cursor:pointer;
  font-size:.85em;color:var(--gris);border-bottom:2px solid transparent;
  margin-bottom:-1px;
}
.tab.activo{color:var(--azul);border-bottom-color:var(--azul);font-weight:600;}
#tab-datos{flex:1;overflow-y:auto;padding:14px 18px;}
#tab-json{
  flex:1;overflow-y:auto;padding:14px 18px;
  font-size:.78em;line-height:1.5;background:#1e1e1e;color:#d4d4d4;
  white-space:pre-wrap;word-break:break-all;
}
.fila{display:flex;justify-content:space-between;font-size:.85em;
  padding:6px 4px;border-bottom:1px solid #f2f2f2;}
.fila .e{color:#888;flex-shrink:0;margin-right:10px;}
.fila .v{font-weight:600;text-align:right;word-break:break-word;color:#1a1a1a;}
.seccion{font-size:.72em;font-weight:700;color:#fff;background:var(--azul);
  margin:14px 0 6px;padding:4px 10px;border-radius:6px;
  text-transform:uppercase;letter-spacing:.05em;display:inline-block;}
table.items{width:100%;border-collapse:collapse;font-size:.78em;margin-top:6px;
  border-radius:6px;overflow:hidden;box-shadow:0 0 0 1px #eee;}
table.items th{background:#f5f5f5;padding:6px 8px;text-align:left;font-weight:600;color:#555;}
table.items td{padding:6px 8px;border-bottom:1px solid #f0f0f0;}
table.items tr:hover td{background:#f8f9fa;}
.btn-hist{border:none;background:var(--azul-s);color:var(--azul);
  font-size:.75em;padding:2px 7px;border-radius:10px;cursor:pointer;}
.btn-hist:hover{background:#d2e3fc;}
.fila-historico td{background:#fafbfc;padding:10px !important;}
.hist-panel{display:flex;gap:12px;align-items:flex-start;}
.hist-canvas{flex-shrink:0;background:#fff;border-radius:6px;box-shadow:0 0 0 1px #eee;}
.hist-lista{flex:1;display:flex;flex-direction:column;gap:3px;max-height:90px;overflow-y:auto;}
.hist-item{display:flex;justify-content:space-between;gap:8px;font-size:.76em;
  padding:3px 6px;border-radius:4px;cursor:pointer;}
.hist-item:hover{background:var(--azul-s);color:var(--azul);}
";

        private static string Js() => @"
const num = v => {
  let s=(v||'0').toString().trim();
  if(s.includes(',') && s.includes('.')) s = s.lastIndexOf(',')>s.lastIndexOf('.') ? s.replace(/\./g,'').replace(',','.') : s.replace(/,/g,'');
  else if(s.includes(',')) s = s.replace(',','.');
  return parseFloat(s) || 0;
};
const eur = v => '€ ' + v.toLocaleString('es-ES',{minimumFractionDigits:2,maximumFractionDigits:2});
function isoFecha(t){ return (t.fecha||t.fecha_guardado||''); }
// Reconoce yyyy-MM-dd, yyyy/MM/dd, dd/MM/yyyy y dd-MM-yyyy (igual que ExportarForm.ParsearFecha en C#).
function parsearFechaFlexible(str){
  if(!str) return null;
  str=str.trim();
  let m=str.match(/^(\d{4})[-\/.](\d{2})[-\/.]\d{2}/);
  if(m) return {anio:m[1], mes:parseInt(m[2],10)};
  m=str.match(/^(\d{2})[-\/.](\d{2})[-\/.](\d{4})/);
  if(m) return {anio:m[3], mes:parseInt(m[2],10)};
  m=str.match(/^(\d{2})[-\/.](\d{2})[-\/.](\d{2})$/);
  if(m) return {anio:'20'+m[3], mes:parseInt(m[2],10)};
  return null;
}
function anioFecha(t){ const f=parsearFechaFlexible(isoFecha(t)); return f?f.anio:''; }
function mesFecha(t){ const f=parsearFechaFlexible(isoFecha(t)); return f?f.mes:0; }
// Nombre de empresa deducido de la ruta real (Año/Empresa/Factura_x/...),
// no del texto libre t.empresa (puede variar aunque sea la misma carpeta).
function empresaCarpeta(t){
  const ruta=t.json||t.imagen||t.pdf||'';
  const partes=ruta.split('/');
  return partes.length>=2 ? partes[1] : (t.empresa||'(sin empresa)').trim();
}

let vistaActual = 'empresa';
let idxModal = -1;
let listaFiltrada = [];
let filtroEspecial = null; // null | 'sinTotal' | 'sinFecha' — activado desde Resumen

/* ─── Cambio de pestaña Facturas/Albaranes ─── */
function resetSelectores(){
  document.getElementById('filtroAnio').innerHTML='<option value="""">Todos</option>';
  document.getElementById('filtroEmpresa').innerHTML='<option value="""">Todas</option>';
  document.getElementById('anioSel').innerHTML='';
  document.getElementById('buscar').value='';
  filtroEspecial=null;
}
function cambiarTipo(tipo, btn){
  if(tipo===tipoActual) return;
  tipoActual=tipo;
  tickets = tipo==='albaranes' ? albaranesData : facturasData;
  empresasCarpetas = tipo==='albaranes' ? empresasCarpetasAlbaranes : empresasCarpetasFacturas;
  document.querySelectorAll('.tab-tipo').forEach(b=>b.classList.remove('activo'));
  if(btn) btn.classList.add('activo');
  resetSelectores();
  poblarFiltros();
  poblarSelectorAnios();
  try{ dibujarGrafico(); filtrarTrimestre(); } catch(e){ console.error(e); }
  filtrar();
}

/* ─── Filtros desplegables ─── */
function poblarFiltros(){
  const anios = [...new Set(tickets.map(anioFecha).filter(Boolean))].sort();
  // Empresas = carpetas reales en disco (empresasCarpetas), no el campo
  // empresa de cada datos.json (que puede faltar o no coincidir).
  const empresas = (empresasCarpetas && empresasCarpetas.length)
    ? [...empresasCarpetas].sort()
    : [...new Set(tickets.map(t=>(t.empresa||'').trim()).filter(Boolean))].sort();
  const sa = document.getElementById('filtroAnio');
  anios.forEach(a=>{ const o=document.createElement('option'); o.value=o.textContent=a; sa.appendChild(o); });
  const se = document.getElementById('filtroEmpresa');
  empresas.forEach(e=>{ const o=document.createElement('option'); o.value=o.textContent=e; se.appendChild(o); });
}

/* ─── Stats ─── */
function renderStats(lista){
  const total = lista.reduce((s,t)=>s+num(t.total),0);
  const empresas = new Set(lista.map(empresaCarpeta).filter(Boolean));
  const sinTotal = lista.filter(t=>!t.total||num(t.total)===0).length;
  const sinFecha = lista.filter(t=>mesFecha(t)===0).length; // no aparecen en gráficos trimestrales
  const rows = [
    ['Gasto total', eur(total), 'azul', null],
    ['Documentos', lista.length, '', null],
    ['Empresas', empresas.size, '', null],
    ['Sin importe', sinTotal, sinTotal>0?'rojo':'', sinTotal>0?'sinTotal':null],
    ['Sin fecha (excl. gráficos)', sinFecha, sinFecha>0?'naranja':'', sinFecha>0?'sinFecha':null],
  ];
  document.getElementById('stats').innerHTML = rows.map(([l,v,c,accion])=>{
    const clic = accion ? ` clicable${filtroEspecial===accion?' activo':''}"" onclick=""filtrarEspecial('${accion}')""` : '""';
    return `<div class=""stat-row ${c}${clic}><span class=""lbl"">${l}</span><span class=""val"">${v}</span></div>`;
  }).join('');
}

// Alterna el filtro especial (sinTotal/sinFecha) desde Resumen: un segundo
// clic sobre la misma fila lo quita.
function filtrarEspecial(tipo){
  filtroEspecial = (filtroEspecial===tipo) ? null : tipo;
  filtrar();
}

/* ─── Gráfico trimestral (canvas) ─── */
function aniosDisponibles(){
  return [...new Set(tickets.map(anioFecha).filter(Boolean))].sort();
}
// Mantiene alineados los dos selectores de año (izq. anioSel / der. filtroAnio)
// para que ""Resumen"" y los gráficos siempre muestren el mismo año.
function sincronizarAnio(valor, desdeDerecho){
  if(!valor) return; // filtroAnio vacío (Todos) no tiene equivalente en anioSel
  if(desdeDerecho) document.getElementById('anioSel').value = valor;
  else document.getElementById('filtroAnio').value = valor;
}
function poblarSelectorAnios(){
  const anios = aniosDisponibles();
  const sel = document.getElementById('anioSel');
  if(!anios.length){ sel.innerHTML='<option>—</option>'; return; }
  sel.innerHTML = anios.map(a=>`<option value=""${a}"">${a}</option>`).join('');
  sel.value = anios[anios.length-1];
}
function dibujarGrafico(){
  const anio = document.getElementById('anioSel').value;
  const trimActivo = document.getElementById('trimSel').value;
  const sumas = [0,0,0,0];
  tickets.forEach(t=>{
    const a=anioFecha(t), m=mesFecha(t);
    if(a===anio && m>0) sumas[Math.ceil(m/3)-1]+=num(t.total);
  });
  const cv=document.getElementById('grafico');
  const ctx=cv.getContext&&cv.getContext('2d'); if(!ctx) return;
  const w=cv.clientWidth||280; cv.width=w; cv.height=160;
  ctx.clearRect(0,0,w,160);
  const max=Math.max(...sumas,1);
  const barW=w/4;
  cv.style.cursor='pointer';
  sumas.forEach((val,i)=>{
    const activo = trimActivo && parseInt(trimActivo)===i+1;
    const h=(val/max)*110, x=i*barW+barW*.15, bw=barW*.7;
    const grad=ctx.createLinearGradient(0,130-h,0,130);
    grad.addColorStop(0, activo?'#0d47a1':'#1a73e8'); grad.addColorStop(1, activo?'#1a73e8':'#6faef8');
    ctx.fillStyle=grad;
    ctx.beginPath(); ctx.roundRect(x,130-h,bw,h,3); ctx.fill();
    if(activo){ ctx.strokeStyle='#0d47a1'; ctx.lineWidth=2; ctx.beginPath(); ctx.roundRect(x,130-h,bw,h,3); ctx.stroke(); }
    ctx.fillStyle='#555'; ctx.font=activo?'bold 11px Arial':'11px Arial'; ctx.textAlign='center';
    ctx.fillText('T'+(i+1),x+bw/2,148);
    if(val>0){ ctx.fillStyle='#1a73e8'; ctx.font='bold 10px Arial'; ctx.fillText(eur(val),x+bw/2,125-h); }
  });
}

// Click sobre una barra del gráfico trimestral = seleccionar ese trimestre
// (equivalente a elegirlo en el desplegable trimSel).
document.getElementById('grafico').addEventListener('click', function(e){
  const w=this.clientWidth||280;
  const barW=w/4;
  const idx=Math.min(3, Math.max(0, Math.floor(e.offsetX/barW)));
  const trimSel=document.getElementById('trimSel');
  const nuevoValor=String(idx+1);
  trimSel.value = trimSel.value===nuevoValor ? '' : nuevoValor; // click de nuevo = deseleccionar
  dibujarGrafico();
  filtrarTrimestre();
});

/* ─── Top empresas barras horizontales ─── */
function dibujarTopEmpresas(lista){
  const sumas={};
  lista.forEach(t=>{ const e=empresaCarpeta(t); sumas[e]=(sumas[e]||0)+num(t.total); });
  const sorted=Object.entries(sumas).sort((a,b)=>b[1]-a[1]).slice(0,5);
  const max=sorted.length?sorted[0][1]:1;
  document.getElementById('grafico-empresas').innerHTML = sorted.map(([e,v])=>`
    <div class=""emp-bar-wrap"">
      <div class=""emp-bar-lbl""><span>${e.length>22?e.slice(0,20)+'…':e}</span><span>${eur(v)}</span></div>
      <div class=""emp-bar-track""><div class=""emp-bar-fill"" style=""width:${Math.round(v/max*100)}%""></div></div>
    </div>`).join('');
}

/* ─── IVA soportado por trimestre (Modelo 303) ───
   Usa el mismo año seleccionado en anioSel. Si además hay un trimestre
   elegido en trimSel, se resalta esa barra igual que en el gráfico de gasto. */
function dibujarIvaTrimestral(){
  const anio = document.getElementById('anioSel').value;
  const trimActivo = document.getElementById('trimSel').value;
  const sumas=[0,0,0,0];
  tickets.forEach(t=>{
    const a=anioFecha(t), m=mesFecha(t);
    if(a===anio && m>0) sumas[Math.ceil(m/3)-1]+=num(t.iva);
  });
  const cv=document.getElementById('grafico-iva-trim');
  const ctx=cv.getContext&&cv.getContext('2d'); if(!ctx) return;
  const w=cv.clientWidth||280; cv.width=w; cv.height=120;
  ctx.clearRect(0,0,w,120);
  const max=Math.max(...sumas,1);
  const barW=w/4;
  cv.style.cursor='pointer';
  sumas.forEach((val,i)=>{
    const activo = trimActivo && parseInt(trimActivo)===i+1;
    const h=(val/max)*80, x=i*barW+barW*.15, bw=barW*.7;
    ctx.fillStyle = activo ? '#0d6b30' : (val>0?'#34a853':'#e8eaed');
    ctx.beginPath(); ctx.roundRect(x,90-h,bw,h,3); ctx.fill();
    ctx.fillStyle='#555'; ctx.font=activo?'bold 10px Arial':'10px Arial'; ctx.textAlign='center';
    ctx.fillText('T'+(i+1),x+bw/2,108);
    if(val>0){ ctx.fillStyle='#137333'; ctx.font='bold 9px Arial'; ctx.fillText(eur(val),x+bw/2,90-h-4); }
  });
}
document.getElementById('grafico-iva-trim').addEventListener('click', function(e){
  const w=this.clientWidth||280;
  const barW=w/4;
  const idx=Math.min(3, Math.max(0, Math.floor(e.offsetX/barW)));
  const trimSel=document.getElementById('trimSel');
  const nuevoValor=String(idx+1);
  trimSel.value = trimSel.value===nuevoValor ? '' : nuevoValor;
  dibujarGrafico();
  filtrarTrimestre();
});

/* ─── Filtro trimestre (panel izquierdo) ─── */
function filtrarTrimestre(){
  const anio = document.getElementById('anioSel').value;
  const trim = document.getElementById('trimSel').value;
  const lista = trim
    ? tickets.filter(t => anioFecha(t)===anio && Math.ceil(mesFecha(t)/3)===parseInt(trim))
    : tickets.filter(t => anioFecha(t)===anio);
  renderStats(lista);
  dibujarTopEmpresas(lista);
  dibujarGrafico();
  dibujarIvaTrimestral();
}

/* ─── Vista ─── */
function setVista(v, btn){
  vistaActual=v;
  document.querySelectorAll('.btn-vista').forEach(b=>b.classList.remove('activo'));
  btn.classList.add('activo');
  renderizar(listaFiltrada);
}

/* ─── Filtrar ─── */
function filtrar(){
  const q=(document.getElementById('buscar').value||'').toLowerCase();
  const anio=document.getElementById('filtroAnio').value;
  const empresa=document.getElementById('filtroEmpresa').value;
  listaFiltrada = tickets.filter(t=>{
    const okQ = !q || (t.empresa||'').toLowerCase().includes(q)
      ||(t.fecha||'').toLowerCase().includes(q)
      ||(t.numero||'').toLowerCase().includes(q)
      ||(t.cif||'').toLowerCase().includes(q)
      ||(t.receptor_nombre||'').toLowerCase().includes(q)
      ||(t.total||'').toLowerCase().includes(q)
      ||(t.metodo_pago||'').toLowerCase().includes(q)
      ||(t.items||[]).some(i=>(i.descripcion||'').toLowerCase().includes(q));
    const okA = !anio || anioFecha(t)===anio;
    const okE = !empresa || empresaCarpeta(t)===empresa;
    const okEsp = !filtroEspecial
      || (filtroEspecial==='sinTotal' && (!t.total||num(t.total)===0))
      || (filtroEspecial==='sinFecha' && mesFecha(t)===0);
    return okQ && okA && okE && okEsp;
  });
  document.getElementById('contador').textContent = listaFiltrada.length+' resultado(s)';
  renderStats(listaFiltrada);
  dibujarTopEmpresas(listaFiltrada);
  dibujarIvaTrimestral();
  renderizar(listaFiltrada);
}

/* ─── Renderizar listado ─── */
function renderizar(lista){
  const c=document.getElementById('contenido');
  if(!lista.length){ c.innerHTML='<div id=""vacio"">No se encontraron documentos.</div>'; return; }

  if(vistaActual==='lista'){ renderLista(lista,c); return; }

  // Ordenar / agrupar
  let items=[...lista];
  if(vistaActual==='total_desc') items.sort((a,b)=>num(b.total)-num(a.total));
  else if(vistaActual==='total_asc') items.sort((a,b)=>num(a.total)-num(b.total));
  else if(vistaActual==='fecha_desc') items.sort((a,b)=>isoFecha(b).localeCompare(isoFecha(a)));
  else if(vistaActual==='fecha_asc') items.sort((a,b)=>isoFecha(a).localeCompare(isoFecha(b)));

  if(vistaActual==='empresa'){
    // Agrupar por empresa
    const grupos={};
    items.forEach(t=>{ const e=empresaCarpeta(t); (grupos[e]=grupos[e]||[]).push(t); });
    c.innerHTML = Object.keys(grupos).sort().map(emp=>{
      const its=grupos[emp];
      const suma=its.reduce((s,t)=>s+num(t.total),0);
      return `<div class=""empresa-grupo"">
        <div class=""empresa-cab"">🏢 ${emp}
          <span class=""count"">${its.length}</span>
          <span class=""suma"">${eur(suma)}</span>
        </div>
        <div class=""galeria"">${its.map(t=>tarjetaHtml(t)).join('')}</div>
      </div>`;
    }).join('');
  } else {
    // Vista plana con galería
    c.innerHTML = `<div class=""galeria"" style=""border-radius:10px;box-shadow:0 1px 4px rgba(0,0,0,.06)"">
      ${items.map(t=>tarjetaHtml(t)).join('')}
    </div>`;
  }
}

function tarjetaHtml(t){
  const idx=tickets.indexOf(t);
  const tieneTotal=t.total&&t.total.toString().trim()!=='';
  const badge=tieneTotal?`<span class=""badge"">${eur(num(t.total))}</span>`:`<span class=""badge vacio"">Sin total</span>`;
  const nLineas=(t.items||[]).length;
  const badgeLineas=nLineas>0?`<span class=""badge-lineas"">${nLineas} línea${nLineas===1?'':'s'}</span>`:'';
  const lineasTexto=(t.items||[]).map(i=>i.descripcion).filter(Boolean).join(' · ');
  const img=lineasTexto
    ?`<div class=""lineas-txt"">${badgeLineas}<span class=""lineas-desc"">${lineasTexto}</span></div>`
    :`<div class=""ph"">Sin líneas</div>`;
  return `<div class=""tarjeta"" onclick=""abrirModal(${idx})"">
    ${img}
    <div class=""resumen"">
      <div class=""fecha"">${t.fecha||'—'}</div>
      <div class=""numero"">Nº ${t.numero||'—'}</div>
      ${badge}
    </div>
  </div>`;
}

let ordenListaCol=null, ordenListaAsc=true;
function ordenarLista(col){
  if(ordenListaCol===col) ordenListaAsc=!ordenListaAsc; else { ordenListaCol=col; ordenListaAsc=true; }
  renderizar(listaFiltrada);
}
function renderLista(lista,c){
  listaFiltrada=lista;
  let ordenada=lista;
  if(ordenListaCol){
    const getters={
      empresa:t=>(t.empresa||'').toLowerCase(),
      fecha:t=>claveOrden(t.fecha),
      numero:t=>(t.numero||'').toLowerCase(),
      cif:t=>(t.cif||'').toLowerCase(),
      total:t=>num(t.total),
      iva:t=>num(t.iva),
      tipo:t=>(t.tipo_documento||'').toLowerCase(),
      metodo_pago:t=>(t.metodo_pago||'').toLowerCase()
    };
    const get=getters[ordenListaCol];
    ordenada=[...lista].sort((a,b)=>{
      const va=get(a), vb=get(b);
      const cmp= va<vb?-1:va>vb?1:0;
      return ordenListaAsc?cmp:-cmp;
    });
  }
  const filas=ordenada.map(t=>{
    const idx=tickets.indexOf(t);
    const tieneTotal=t.total&&t.total.toString().trim()!=='';
    return `<tr onclick=""abrirModal(${idx})"">
      <td>${t.empresa||'—'}</td>
      <td>${t.fecha||'—'}</td>
      <td>${t.numero||'—'}</td>
      <td>${t.cif||'—'}</td>
      <td>${t.tipo_documento||'—'}</td>
      <td style=""text-align:right"">${t.iva?eur(num(t.iva)):'—'}</td>
      <td style=""text-align:right;font-weight:600;color:${tieneTotal?'#137333':'#c5221f'}"">${tieneTotal?eur(num(t.total)):'—'}</td>
      <td>${t.metodo_pago||'—'}</td>
    </tr>`;
  }).join('');
  const flecha=col=> ordenListaCol===col ? (ordenListaAsc?' ▲':' ▼') : '';
  c.innerHTML=`<table id=""tabla-lista"">
    <thead><tr>
      <th onclick=""ordenarLista('empresa')"" style=""cursor:pointer"">Empresa${flecha('empresa')}</th>
      <th onclick=""ordenarLista('fecha')"" style=""cursor:pointer"">Fecha${flecha('fecha')}</th>
      <th onclick=""ordenarLista('numero')"" style=""cursor:pointer"">Nº Factura${flecha('numero')}</th>
      <th onclick=""ordenarLista('cif')"" style=""cursor:pointer"">CIF${flecha('cif')}</th>
      <th onclick=""ordenarLista('tipo')"" style=""cursor:pointer"">Tipo${flecha('tipo')}</th>
      <th onclick=""ordenarLista('iva')"" style=""text-align:right;cursor:pointer"">IVA${flecha('iva')}</th>
      <th onclick=""ordenarLista('total')"" style=""text-align:right;cursor:pointer"">Total${flecha('total')}</th>
      <th onclick=""ordenarLista('metodo_pago')"" style=""cursor:pointer"">Pago${flecha('metodo_pago')}</th>
    </tr></thead>
    <tbody>${filas}</tbody>
  </table>`;
}

/* ─── Modal ─── */
function setTab(tab, btn){
  document.querySelectorAll('.tab').forEach(b=>b.classList.remove('activo'));
  btn.classList.add('activo');
  document.getElementById('tab-datos').style.display = tab==='datos'?'':'none';
  document.getElementById('tab-json').style.display  = tab==='json'?'':'none';
}

function claveOrden(fecha){
  const f=parsearFechaFlexible(fecha);
  return f ? f.anio*100+f.mes : -1;
}
function coincidenciasArticulo(desc, idxActual){
  const d=(desc||'').trim().toLowerCase(); if(!d) return [];
  const out=[];
  tickets.forEach((t,i)=>{
    if(i===idxActual) return;
    (t.items||[]).forEach(it=>{
      if((it.descripcion||'').trim().toLowerCase()===d){
        out.push({idx:i, fecha:t.fecha||'—', empresa:empresaCarpeta(t), precio:num(it.precio_unitario)});
      }
    });
  });
  return out.sort((a,b)=>claveOrden(a.fecha)-claveOrden(b.fecha));
}
function toggleHistorico(uid){
  const fila=document.getElementById(uid);
  const visible=fila.style.display!=='none';
  fila.style.display=visible?'none':'';
  if(visible || fila.dataset.render) return;
  fila.dataset.render='1';
  const data=window._histData[uid];
  const celda=fila.querySelector('td');
  celda.innerHTML=`<div class=""hist-panel"">
    <canvas class=""hist-canvas"" width=""260"" height=""70""></canvas>
    <div class=""hist-lista"">${data.map(d=>`<div class=""hist-item"" onclick=""cerrarModal();setTimeout(()=>abrirModal(${d.idx}),50)"">
      <span>${d.fecha}</span><span>${d.empresa}</span><span>${eur(d.precio)}</span>
    </div>`).join('')}</div>
  </div>`;
  dibujarHistorico(celda.querySelector('canvas'), data);
}
function dibujarHistorico(cv, data){
  const ctx=cv.getContext('2d'); if(!ctx||!data.length) return;
  const w=cv.width, h=cv.height, pad=8;
  const precios=data.map(d=>d.precio);
  const max=Math.max(...precios,0.01), min=Math.min(...precios,0);
  const rango=(max-min)||1;
  ctx.clearRect(0,0,w,h);
  ctx.strokeStyle='#1a73e8'; ctx.lineWidth=2; ctx.beginPath();
  data.forEach((d,i)=>{
    const x=pad+(i/(Math.max(data.length-1,1)))*(w-pad*2);
    const y=h-pad-((d.precio-min)/rango)*(h-pad*2);
    i===0?ctx.moveTo(x,y):ctx.lineTo(x,y);
  });
  ctx.stroke();
  ctx.fillStyle='#1a73e8';
  data.forEach((d,i)=>{
    const x=pad+(i/(Math.max(data.length-1,1)))*(w-pad*2);
    const y=h-pad-((d.precio-min)/rango)*(h-pad*2);
    ctx.beginPath(); ctx.arc(x,y,2.5,0,7); ctx.fill();
  });
}
window._histData={};

function abrirModal(idx){
  idxModal=idx;
  renderModal(tickets[idx], idx);
  // Avisa a WinForms (panelBarraVisor) qué factura está abierta, para que
  // los botones nativos ◀ ▶ ✏️ sepan sobre qué imagen/json actuar.
  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage({
      accion: 'abrir',
      imagen: tickets[idx].imagen||'', json: tickets[idx].json||'',
      empresa: tickets[idx].empresa||'(sin empresa)', fecha: tickets[idx].fecha||'—'
    });
  }
  // Relee el JSON real del disco por si se editó fuera de la app; si falla
  // (bloqueo file://, borrado, etc.) se queda con los datos ya cacheados.
  const ruta=tickets[idx].json;
  if(ruta){
    fetch(ruta).then(r=>r.json()).then(actual=>{
      if(idxModal===idx){ tickets[idx]=Object.assign({}, tickets[idx], actual); renderModal(tickets[idx], idx); }
    }).catch(()=>{});
  }
}

function renderModal(t, idx){
  // título ahora lo pinta lblTituloModal (Form1), vía postMessage en abrirModal()
  const foto=document.getElementById('modal-foto');
  const sinImg=document.getElementById('modal-sin-img');
  if(t.imagen){ foto.src=t.imagen; foto.style.display=''; sinImg.style.display='none'; }
  else { foto.style.display='none'; sinImg.style.display=''; }

  // Tab datos
  let html='';
  html+=sec('Emisor');
  html+=fi('Empresa',t.empresa); html+=fi('Fecha emisión',t.fecha);
  html+=fi('Fecha vencimiento',t.fecha_vencimiento); html+=fi('Nº Factura',t.numero);
  html+=fi('CIF/NIF',t.cif); html+=fi('Dirección',t.direccion); html+=fi('Teléfono',t.telefono);
  if(t.receptor_nombre){
    html+=sec('Receptor');
    html+=fi('Nombre',t.receptor_nombre); html+=fi('CIF/NIF',t.receptor_cif); html+=fi('Dirección',t.receptor_direccion);
  }
  html+=sec('Importes');
  html+=fi('Base imponible',t.base); html+=fi('IVA',t.iva);
  html+=fi('Total',t.total?eur(num(t.total)):''); html+=fi('Método de pago',t.metodo_pago);
  if(t.items&&t.items.length){
    html+=sec('Líneas ('+t.items.length+')');
    html+='<table class=""items""><thead><tr><th>Descripción</th><th>Cant.</th><th>P.Unit.</th><th>Subtotal</th><th></th></tr></thead><tbody>';
    t.items.forEach((i,ii)=>{
      const coincidencias=coincidenciasArticulo(i.descripcion, idx);
      const uid='hist-'+idx+'-'+ii;
      window._histData[uid]=coincidencias;
      const btn=coincidencias.length
        ?`<button class=""btn-hist"" onclick=""toggleHistorico('${uid}')"" title=""Ver histórico de precio"">📈 ${coincidencias.length}</button>`
        :'';
      html+=`<tr><td>${i.descripcion}</td><td>${i.cantidad}</td><td>${i.precio_unitario}</td><td>${i.subtotal}</td><td>${btn}</td></tr>`;
      html+=`<tr class=""fila-historico"" id=""${uid}"" style=""display:none""><td colspan=""5""></td></tr>`;
    });
    html+='</tbody></table>';
  }
  html+=fi('Guardado',t.fecha_guardado);
  document.getElementById('tab-datos').innerHTML=html;

  // Tab JSON
  document.getElementById('tab-json').textContent=JSON.stringify(t,null,2);

  // Resetear a tab datos
  document.querySelectorAll('.tab').forEach((b,i)=>b.classList.toggle('activo',i===0));
  document.getElementById('tab-datos').style.display='';
  document.getElementById('tab-json').style.display='none';

  document.getElementById('modal').classList.add('activo');
}

// Índices (dentro de tickets) de las facturas de la misma empresa que
// la que está abierta ahora mismo, en el orden original.
function indicesMismaEmpresa(){
  const empActual = empresaCarpeta(tickets[idxModal]);
  const out=[];
  tickets.forEach((t,i)=>{ if(empresaCarpeta(t)===empActual) out.push(i); });
  return out;
}

function navModal(dir){
  const grupo = indicesMismaEmpresa();
  const pos = grupo.indexOf(idxModal);
  const nuevaPos = pos+dir;
  if(nuevaPos>=0 && nuevaPos<grupo.length) abrirModal(grupo[nuevaPos]);
}

// Consulta directa (sin depender del postMessage) de qué factura está
// realmente abierta ahora mismo en el modal. La usan btnEditarVisor/
// btnEliminarVisor desde WinForms para no fiarse de un estado cacheado
// que puede no haberse actualizado si el postMessage de abrirModal() se
// perdió o llegó tarde.
function datosModalActual(){
  if(idxModal===undefined || idxModal===null || idxModal<0 || !tickets[idxModal]) return '';
  return JSON.stringify({
    imagen: tickets[idxModal].imagen||'', json: tickets[idxModal].json||'',
    empresa: tickets[idxModal].empresa||'', fecha: tickets[idxModal].fecha||'',
    numero: tickets[idxModal].numero||'', total: tickets[idxModal].total||''
  });
}

// Abre la imagen original en una pestaña nueva para poder editarla. Un
// navegador no puede lanzar un editor externo directamente: desde ahí el
// usuario puede usar ""Guardar como"" o ""Abrir con"" de su sistema.
function cerrarModal(){
  document.getElementById('modal').classList.remove('activo');
  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage({accion: 'cerrar'});
  }
}
document.getElementById('modal').addEventListener('click',function(e){ if(e.target===this) cerrarModal(); });
document.addEventListener('keydown',function(e){
  if(!document.getElementById('modal').classList.contains('activo')) return;
  if(e.key==='Escape') cerrarModal();
  if(e.key==='ArrowLeft') navModal(-1);
  if(e.key==='ArrowRight') navModal(1);
});

function fi(l,v){ if(!v||v.toString().trim()==='') return ''; return `<div class=""fila""><span class=""e"">${l}</span><span class=""v"">${v}</span></div>`; }
function sec(t){ return `<div class=""seccion"">${t}</div>`; }

/* ─── Init ─── */
poblarFiltros();
poblarSelectorAnios();
try{ dibujarGrafico(); filtrarTrimestre(); } catch(e){ console.error(e); }
filtrar();
window.addEventListener('resize',()=>{ try{ dibujarGrafico(); dibujarIvaTrimestral(); }catch(e){} });
";
    }
}