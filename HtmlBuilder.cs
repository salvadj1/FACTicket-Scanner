using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FACTicket_Scanner
{
    internal static class HtmlBuilder
    {
        internal static void GenerarAlbum(string carpetaTickets, List<DatosTicket> lista, string nombreAlbum)
        {
            string rutaHtml = System.IO.Path.Combine(carpetaTickets, nombreAlbum);
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>Panel de Facturas</title><style>");
            sb.AppendLine(Css());
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<script src=\"https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js\"></script>");
            sb.AppendLine(Html());
            sb.AppendLine("<script>");
            sb.AppendLine("const tickets=" + JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = false }) + ";");
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
    <div id=""panel-header"">
      <div id=""panel-title"">📊 Panel de Facturas</div>
      <div id=""panel-gen""></div>
    </div>

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
          <select id=""anioSel"" onchange=""dibujarGrafico();filtrarTrimestre()""></select>
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

    <!-- Distribución por mes -->
    <div class=""bloque"">
      <div class=""bloque-titulo"">Distribución mensual</div>
      <canvas id=""grafico-meses"" height=""120""></canvas>
    </div>
  </aside>

  <!-- ═══════════════════════════════════════════════════════════
       PANEL DERECHO — controles + listado scrollable
  ═══════════════════════════════════════════════════════════ -->
  <main id=""panel-der"">

    <!-- Controles -->
    <div id=""controles"">
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
          <select id=""filtroAnio"" onchange=""filtrar()""><option value="""">Todos</option></select>
        </div>
        <div class=""ctrl-grupo"">
          <label>Empresa</label>
          <select id=""filtroEmpresa"" onchange=""filtrar()""><option value="""">Todas</option></select>
        </div>
        <div class=""ctrl-grupo"">
          <button id=""btnExportar"" onclick=""abrirExportar()"" title=""Exportar documentos"">📦 Exportar</button>
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
      <div id=""modal-nav"">
        <button onclick=""navModal(-1)"" title=""Anterior"">◀</button>
        <span id=""modal-titulo""></span>
        <button onclick=""navModal(1)"" title=""Siguiente"">▶</button>
        <button id=""modal-cerrar"" onclick=""cerrarModal()"" title=""Cerrar"">✕</button>
      </div>
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

<!-- ═══════════════════════════════════════════════════════════
     PANEL EXPORTAR — genera ZIP con documentos según ámbito
═══════════════════════════════════════════════════════════ -->
<div id=""exportPanel"" style=""display:none"">
  <div id=""exportInner"">
    <div id=""exportTitulo"">📦 Exportar documentos</div>

    <div class=""exp-bloque"">
      <div class=""exp-etiqueta"">Ámbito</div>
      <label><input type=""radio"" name=""ambitoExp"" value=""trimestre"" checked> Trimestre actual (según panel izquierdo)</label>
      <label><input type=""radio"" name=""ambitoExp"" value=""anio""> Año actual (filtro superior)</label>
      <label><input type=""radio"" name=""ambitoExp"" value=""empresa""> Empresa filtrada</label>
      <label><input type=""radio"" name=""ambitoExp"" value=""factura""> Factura abierta en el visor</label>
    </div>

    <div class=""exp-bloque"">
      <div class=""exp-etiqueta"">Incluir</div>
      <label><input type=""checkbox"" id=""expPdf"" checked> PDF</label>
      <label><input type=""checkbox"" id=""expJson"" checked> JSON</label>
      <label><input type=""checkbox"" id=""expJpg"" checked> JPG procesado</label>
      <label><input type=""checkbox"" id=""expOriginal""> JPG original</label>
    </div>

    <div id=""exportEstado""></div>

    <div id=""exportBotones"">
      <button onclick=""cerrarExportar()"">Cancelar</button>
      <button id=""btnGenerarZip"" onclick=""generarZip()"">Descargar ZIP</button>
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

/* Gráfico barras trimestral */
#grafico{width:100%;display:block;}
#anioSel{padding:3px 6px;border:1px solid var(--borde);border-radius:6px;font-size:.8em;}

/* Top empresas barras horizontales */
#grafico-empresas{display:flex;flex-direction:column;gap:6px;}
.emp-bar-wrap{display:flex;flex-direction:column;gap:2px;}
.emp-bar-lbl{display:flex;justify-content:space-between;font-size:.75em;color:#333;}
.emp-bar-track{height:8px;background:#eee;border-radius:4px;overflow:hidden;}
.emp-bar-fill{height:100%;border-radius:4px;background:var(--azul);transition:width .4s;}

/* Gráfico meses */
#grafico-meses{width:100%;display:block;}

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
  border:1px solid #eaeaea;transition:transform .15s,box-shadow .15s;
  background:#fafafa;
}
.tarjeta:hover{transform:translateY(-3px);box-shadow:0 6px 16px rgba(0,0,0,.13);}
.tarjeta img{width:100%;height:110px;object-fit:cover;display:block;}
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
#modal-nav{display:flex;align-items:center;gap:12px;}
#modal-nav button{
  padding:5px 12px;border:1px solid var(--borde);border-radius:6px;
  background:#f8f9fa;cursor:pointer;font-size:.9em;
}
#modal-nav button:hover{background:var(--azul-s);}
#modal-titulo{font-size:1em;font-weight:700;color:var(--azul);}
#modal-cerrar{
  font-size:1.3em;color:#888;border:none;background:#f0f0f0;
  cursor:pointer;padding:4px 10px;border-radius:6px;border:1px solid var(--borde);
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
.fila{display:flex;justify-content:space-between;font-size:.83em;
  padding:5px 0;border-bottom:1px solid #f0f0f0;}
.fila .e{color:#888;flex-shrink:0;margin-right:8px;}
.fila .v{font-weight:500;text-align:right;word-break:break-word;}
.seccion{font-size:.7em;font-weight:700;color:var(--azul);
  margin:12px 0 4px;text-transform:uppercase;letter-spacing:.05em;}
table.items{width:100%;border-collapse:collapse;font-size:.78em;margin-top:6px;}
table.items th{background:#f5f5f5;padding:4px 8px;text-align:left;font-weight:600;}
table.items td{padding:4px 8px;border-bottom:1px solid #f0f0f0;}
#exportPanel{position:fixed;inset:0;background:rgba(0,0,0,.5);z-index:200;
  display:flex;align-items:center;justify-content:center;}
#exportInner{background:#fff;border-radius:10px;padding:20px 24px;width:360px;
  max-width:90vw;box-shadow:0 8px 30px rgba(0,0,0,.25);}
#exportTitulo{font-weight:700;font-size:1.05em;margin-bottom:14px;}
.exp-bloque{margin-bottom:14px;}
.exp-etiqueta{font-size:.75em;font-weight:700;color:var(--azul);
  text-transform:uppercase;letter-spacing:.05em;margin-bottom:6px;}
.exp-bloque label{display:block;font-size:.87em;padding:3px 0;cursor:pointer;}
#exportEstado{font-size:.82em;color:#666;min-height:1.2em;margin-bottom:10px;}
#exportBotones{display:flex;justify-content:flex-end;gap:8px;}
#exportBotones button{padding:7px 16px;border:none;border-radius:6px;cursor:pointer;font-size:.87em;}
#btnGenerarZip{background:var(--azul);color:#fff;}
#exportBotones button:not(#btnGenerarZip){background:#eee;}
#btnExportar{padding:6px 12px;border:1px solid #ddd;border-radius:6px;background:#fff;
  cursor:pointer;font-size:.87em;}
";

        private static string Js() => @"
const num = v => parseFloat((v||'0').toString().replace(',','.')) || 0;
const eur = v => '€ ' + v.toLocaleString('es-ES',{minimumFractionDigits:2,maximumFractionDigits:2});
function isoFecha(t){ return (t.fecha||t.fecha_guardado||''); }
function anioFecha(t){ const m=(isoFecha(t)||'').match(/^(\d{4})/); return m?m[1]:''; }
function mesFecha(t){ const m=(isoFecha(t)||'').match(/^\d{4}-(\d{2})/); return m?parseInt(m[1],10):0; }

let vistaActual = 'empresa';
let idxModal = -1;
let listaFiltrada = [];

/* ─── Filtros desplegables ─── */
function poblarFiltros(){
  const anios = [...new Set(tickets.map(anioFecha).filter(Boolean))].sort();
  const empresas = [...new Set(tickets.map(t=>(t.empresa||'').trim()).filter(Boolean))].sort();
  const sa = document.getElementById('filtroAnio');
  anios.forEach(a=>{ const o=document.createElement('option'); o.value=o.textContent=a; sa.appendChild(o); });
  const se = document.getElementById('filtroEmpresa');
  empresas.forEach(e=>{ const o=document.createElement('option'); o.value=o.textContent=e; se.appendChild(o); });
}

/* ─── Stats ─── */
function renderStats(lista){
  const total = lista.reduce((s,t)=>s+num(t.total),0);
  const empresas = new Set(lista.map(t=>(t.empresa||'').trim()).filter(Boolean));
  const media = lista.length ? total/lista.length : 0;
  const sinTotal = lista.filter(t=>!t.total||num(t.total)===0).length;
  let top=null; lista.forEach(t=>{ if(!top||num(t.total)>num(top.total)) top=t; });
  const rows = [
    ['Gasto total', eur(total), 'azul'],
    ['Documentos', lista.length, ''],
    ['Empresas', empresas.size, ''],
    ['Importe medio', eur(media), 'verde'],
    ['Sin importe', sinTotal, sinTotal>0?'rojo':''],
    ['Mayor gasto', top ? eur(num(top.total)) : '—', 'verde'],
  ];
  document.getElementById('stats').innerHTML = rows.map(([l,v,c])=>
    `<div class=""stat-row ${c}""><span class=""lbl"">${l}</span><span class=""val"">${v}</span></div>`
  ).join('');
  document.getElementById('panel-gen').textContent = 'Actualizado '+generado;
}

/* ─── Gráfico trimestral (canvas) ─── */
function aniosDisponibles(){
  return [...new Set(tickets.map(anioFecha).filter(Boolean))].sort();
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
  sumas.forEach((val,i)=>{
    const h=(val/max)*110, x=i*barW+barW*.15, bw=barW*.7;
    const grad=ctx.createLinearGradient(0,130-h,0,130);
    grad.addColorStop(0,'#1a73e8'); grad.addColorStop(1,'#6faef8');
    ctx.fillStyle=grad;
    ctx.beginPath(); ctx.roundRect(x,130-h,bw,h,3); ctx.fill();
    ctx.fillStyle='#555'; ctx.font='11px Arial'; ctx.textAlign='center';
    ctx.fillText('T'+(i+1),x+bw/2,148);
    if(val>0){ ctx.fillStyle='#1a73e8'; ctx.font='bold 10px Arial'; ctx.fillText(eur(val),x+bw/2,125-h); }
  });
}

/* ─── Top empresas barras horizontales ─── */
function dibujarTopEmpresas(lista){
  const sumas={};
  lista.forEach(t=>{ const e=(t.empresa||'(sin empresa)').trim(); sumas[e]=(sumas[e]||0)+num(t.total); });
  const sorted=Object.entries(sumas).sort((a,b)=>b[1]-a[1]).slice(0,5);
  const max=sorted.length?sorted[0][1]:1;
  document.getElementById('grafico-empresas').innerHTML = sorted.map(([e,v])=>`
    <div class=""emp-bar-wrap"">
      <div class=""emp-bar-lbl""><span>${e.length>22?e.slice(0,20)+'…':e}</span><span>${eur(v)}</span></div>
      <div class=""emp-bar-track""><div class=""emp-bar-fill"" style=""width:${Math.round(v/max*100)}%""></div></div>
    </div>`).join('');
}

/* ─── Gráfico mensual ─── */
function dibujarGraficoMeses(lista){
  const sumas=new Array(12).fill(0);
  lista.forEach(t=>{ const m=mesFecha(t); if(m>0) sumas[m-1]+=num(t.total); });
  const cv=document.getElementById('grafico-meses');
  const ctx=cv.getContext&&cv.getContext('2d'); if(!ctx) return;
  const w=cv.clientWidth||280; cv.width=w; cv.height=120;
  ctx.clearRect(0,0,w,120);
  const max=Math.max(...sumas,1);
  const barW=w/12;
  const meses=['E','F','M','A','M','J','J','A','S','O','N','D'];
  sumas.forEach((val,i)=>{
    const h=(val/max)*80, x=i*barW+1, bw=barW-2;
    ctx.fillStyle=val>0?'#34a853':'#e8eaed';
    ctx.beginPath(); ctx.roundRect(x,90-h,bw,h,2); ctx.fill();
    ctx.fillStyle='#777'; ctx.font='9px Arial'; ctx.textAlign='center';
    ctx.fillText(meses[i],x+bw/2,108);
  });
}

/* ─── Filtro trimestre (panel izquierdo) ─── */
function filtrarTrimestre(){
  const anio = document.getElementById('anioSel').value;
  const trim = document.getElementById('trimSel').value;
  const lista = trim
    ? tickets.filter(t => anioFecha(t)===anio && Math.ceil(mesFecha(t)/3)===parseInt(trim))
    : tickets.filter(t => anioFecha(t)===anio);
  renderStats(lista);
  dibujarTopEmpresas(lista);
  dibujarGraficoMeses(lista);
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
    const okE = !empresa || (t.empresa||'').trim()===empresa;
    return okQ && okA && okE;
  });
  document.getElementById('contador').textContent = listaFiltrada.length+' resultado(s)';
  renderStats(listaFiltrada);
  dibujarTopEmpresas(listaFiltrada);
  dibujarGraficoMeses(listaFiltrada);
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
    items.forEach(t=>{ const e=(t.empresa||'(sin empresa)').trim(); (grupos[e]=grupos[e]||[]).push(t); });
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
  const img=t.imagen
    ?`<img src=""${t.imagen}"" loading=""lazy"" onerror=""this.outerHTML='<div class=ph>Sin imagen</div>'""/>`
    :`<div class=""ph"">Sin imagen</div>`;
  return `<div class=""tarjeta"" onclick=""abrirModal(${idx})"">
    ${img}
    <div class=""resumen"">
      <div class=""fecha"">${t.fecha||'—'}</div>
      <div class=""numero"">Nº ${t.numero||'—'}</div>
      ${badge}
    </div>
  </div>`;
}

function renderLista(lista,c){
  const filas=lista.map(t=>{
    const idx=tickets.indexOf(t);
    const tieneTotal=t.total&&t.total.toString().trim()!=='';
    return `<tr onclick=""abrirModal(${idx})"">
      <td>${t.empresa||'—'}</td>
      <td>${t.fecha||'—'}</td>
      <td>${t.numero||'—'}</td>
      <td>${t.cif||'—'}</td>
      <td style=""text-align:right;font-weight:600;color:${tieneTotal?'#137333':'#c5221f'}"">${tieneTotal?eur(num(t.total)):'—'}</td>
      <td>${t.metodo_pago||'—'}</td>
    </tr>`;
  }).join('');
  c.innerHTML=`<table id=""tabla-lista"">
    <thead><tr>
      <th>Empresa</th><th>Fecha</th><th>Nº Factura</th>
      <th>CIF</th><th style=""text-align:right"">Total</th><th>Pago</th>
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

function abrirModal(idx){
  idxModal=idx;
  const t=tickets[idx];
  document.getElementById('modal-titulo').textContent=(t.empresa||'(sin empresa)')+' · '+(t.fecha||'—');
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
    html+='<table class=""items""><thead><tr><th>Descripción</th><th>Cant.</th><th>P.Unit.</th><th>Subtotal</th></tr></thead><tbody>';
    html+=t.items.map(i=>`<tr><td>${i.descripcion}</td><td>${i.cantidad}</td><td>${i.precio_unitario}</td><td>${i.subtotal}</td></tr>`).join('');
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

function navModal(dir){
  const newIdx = idxModal+dir;
  if(newIdx>=0 && newIdx<tickets.length) abrirModal(newIdx);
}

function cerrarModal(){ document.getElementById('modal').classList.remove('activo'); }
document.getElementById('modal').addEventListener('click',function(e){ if(e.target===this) cerrarModal(); });
document.addEventListener('keydown',function(e){
  if(!document.getElementById('modal').classList.contains('activo')) return;
  if(e.key==='Escape') cerrarModal();
  if(e.key==='ArrowLeft') navModal(-1);
  if(e.key==='ArrowRight') navModal(1);
});

function fi(l,v){ if(!v||v.toString().trim()==='') return ''; return `<div class=""fila""><span class=""e"">${l}</span><span class=""v"">${v}</span></div>`; }
function sec(t){ return `<div class=""seccion"">${t}</div>`; }

/* ─── Exportar ZIP ─── */
function abrirExportar(){
  document.getElementById('exportEstado').textContent='';
  document.getElementById('exportPanel').style.display='flex';
}
function cerrarExportar(){ document.getElementById('exportPanel').style.display='none'; }

function carpetaDe(t){
  const ref = t.json || t.pdf || t.imagen || '';
  const i = ref.lastIndexOf('/');
  return i>=0 ? ref.substring(0,i+1) : '';
}

function ticketsAmbito(ambito){
  if(ambito==='factura') return idxModal>=0 ? [tickets[idxModal]] : [];
  if(ambito==='empresa'){
    const empresa=document.getElementById('filtroEmpresa').value;
    return tickets.filter(t=>!empresa || (t.empresa||'').trim()===empresa);
  }
  if(ambito==='anio'){
    const anio=document.getElementById('anioSel').value;
    return tickets.filter(t=>anioFecha(t)===anio);
  }
  // trimestre (por defecto)
  const anio=document.getElementById('anioSel').value;
  const trim=document.getElementById('trimSel').value;
  return tickets.filter(t=>anioFecha(t)===anio && (!trim || Math.ceil(mesFecha(t)/3)===parseInt(trim)));
}

async function generarZip(){
  const ambito=document.querySelector('input[name=""ambitoExp""]:checked').value;
  const incluirPdf=document.getElementById('expPdf').checked;
  const incluirJson=document.getElementById('expJson').checked;
  const incluirJpg=document.getElementById('expJpg').checked;
  const incluirOriginal=document.getElementById('expOriginal').checked;
  const estado=document.getElementById('exportEstado');

  const lista=ticketsAmbito(ambito);
  if(!lista.length){ estado.textContent='No hay documentos para ese ámbito.'; return; }
  if(!incluirPdf && !incluirJson && !incluirJpg && !incluirOriginal){ estado.textContent='Selecciona al menos un tipo de archivo.'; return; }

  const zip=new JSZip();
  let ok=0, fallos=0;

  const archivos=[];
  lista.forEach(t=>{
    if(incluirPdf && t.pdf) archivos.push({ruta:t.pdf, carpeta:carpetaDe(t)});
    if(incluirJson && t.json) archivos.push({ruta:t.json, carpeta:carpetaDe(t)});
    if(incluirJpg && t.imagen) archivos.push({ruta:t.imagen, carpeta:carpetaDe(t)});
    if(incluirOriginal) archivos.push({ruta:carpetaDe(t)+'original.jpg', carpeta:carpetaDe(t)});
  });

  for(let i=0;i<archivos.length;i++){
    const a=archivos[i];
    estado.textContent=`Descargando ${i+1}/${archivos.length}...`;
    try{
      const resp=await fetch(a.ruta);
      if(!resp.ok) throw new Error();
      const blob=await resp.blob();
      zip.file(a.ruta, blob);
      ok++;
    }catch(e){ fallos++; }
  }

  if(ok===0){
    estado.textContent='No se pudo acceder a ningún archivo. Si abriste el álbum con doble clic, sírvelo desde un servidor local (el navegador bloquea la lectura de archivos locales).';
    return;
  }

  estado.textContent='Comprimiendo...';
  const contenido=await zip.generateAsync({type:'blob'});
  const url=URL.createObjectURL(contenido);
  const a=document.createElement('a');
  a.href=url; a.download=`export_${ambito}_${Date.now()}.zip`;
  document.body.appendChild(a); a.click(); a.remove();
  URL.revokeObjectURL(url);

  estado.textContent = fallos>0
    ? `Listo, con ${fallos} archivo(s) no encontrado(s).`
    : 'Descarga completada.';
}

/* ─── Init ─── */
poblarFiltros();
poblarSelectorAnios();
try{ dibujarGrafico(); filtrarTrimestre(); } catch(e){ console.error(e); }
filtrar();
window.addEventListener('resize',()=>{ try{ dibujarGrafico(); dibujarGraficoMeses(listaFiltrada); }catch(e){} });
";
    }
}