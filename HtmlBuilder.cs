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
            sb.AppendLine("let albaranesData=" + JsonSerializer.Serialize(PrefijarRutasAlbaranes(listaAlbaranes), new JsonSerializerOptions { WriteIndented = false }) + ";");
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

        /// <summary>
        /// Los albaranes se guardan con rutas relativas a la raíz "Albaranes",
        /// pero el álbum HTML vive en la raíz "Facturas". Devuelve una COPIA de
        /// la lista con imagen/pdf/json prefijados con "../Albaranes/" para que
        /// el visor (vista previa y edición) encuentre los archivos.
        /// No modifica los objetos originales ni lo guardado en disco.
        /// </summary>
        /// <param name="albaranes">Lista de albaranes cargada desde disco (puede ser null).</param>
        /// <param name="carpetaRaiz">Nombre de la carpeta raíz de albaranes.</param>
        /// <returns>Lista nueva con las rutas ajustadas (vacía si la entrada es null).</returns>
        private static List<DatosTicket> PrefijarRutasAlbaranes(List<DatosTicket>? albaranes, string carpetaRaiz = "Albaranes")
        {
            var resultado = new List<DatosTicket>();
            if (albaranes == null) return resultado;

            string prefijo = "../" + carpetaRaiz + "/";
            foreach (var original in albaranes)
            {
                // Copia profunda vía JSON para no tocar el objeto compartido.
                var copia = JsonSerializer.Deserialize<DatosTicket>(JsonSerializer.Serialize(original));
                if (copia == null) continue;

                if (!string.IsNullOrEmpty(copia.ImagenRelativa)) copia.ImagenRelativa = prefijo + copia.ImagenRelativa;
                if (!string.IsNullOrEmpty(copia.PdfRelativa)) copia.PdfRelativa = prefijo + copia.PdfRelativa;
                if (!string.IsNullOrEmpty(copia.JsonRelativa)) copia.JsonRelativa = prefijo + copia.JsonRelativa;
                resultado.Add(copia);
            }
            return resultado;
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
        <button class=""tab-tipo"" onclick=""cambiarTipo('articulos',this)"">🛒 Artículos</button>
      </div>
      <input type=""text"" id=""buscar"" placeholder=""🔍 Buscar empresa, número, CIF, fecha..."" oninput=""filtrar()"">
      <div id=""controles-fila2"">
        <div class=""ctrl-grupo"">
          <label>Vista</label>
          <div id=""btns-vista"">
            <button class=""btn-vista"" onclick=""recargarPanel()"" title=""Actualizar / recargar panel"">⟳</button>
            <button class=""btn-vista solo-docs activo"" data-vista=""empresa"" onclick=""setVista('empresa',this)"" title=""Por empresa"">🏢</button>
            <button class=""btn-vista solo-docs"" data-vista=""guardado_desc"" onclick=""setVista('guardado_desc',this)"" title=""Últimas añadidas"">🕒</button>
            <button class=""btn-vista solo-docs"" id=""btnAvanzado"" onclick=""toggleAvanzado()"" title=""Filtros avanzados"">⚙<span id=""nbAvanzado"" class=""nb""></span></button>
            <span class=""menu-exp"">
              <button class=""btn-vista"" onclick=""toggleMenuExportar(event)"" title=""Exportar lo mostrado"">📤 ▾</button>
              <div id=""menuExportar"" class=""dd-exp"">
                <div onclick=""exportar('csv')"">📄 CSV (Excel ES)</div>
                <div onclick=""exportar('xls')"">📗 Excel (.xls)</div>
                <div onclick=""exportar('json')"">🧩 JSON</div>
                <div onclick=""exportar('html')"">🌐 Informe HTML</div>
                <div onclick=""exportar('pdf')"">📕 PDF (imprimir)</div>
              </div>
            </span>
          </div>
        </div>
        <div class=""ctrl-grupo solo-docs"">
          <label>Tamaño</label>
          <input type=""range"" id=""sliderMiniatura"" min=""60"" max=""400"" step=""5"" value=""120"" oninput=""ajustarTamanoMiniatura(this.value)"" title=""Mínimo = vista lista"">
          <span id=""lblTam""></span>
        </div>
        <div class=""ctrl-grupo"">
          <label>Año</label>
          <select id=""filtroAnio"" onchange=""sincronizarAnio(this.value,true);filtrar()""><option value="""">Todos</option></select>
        </div>
        <div class=""ctrl-grupo"">
          <label>Trimestre</label>
          <select id=""filtroTrimestre"" onchange=""filtrar()"">
            <option value="""">Todos</option>
            <option value=""1"">T1</option>
            <option value=""2"">T2</option>
            <option value=""3"">T3</option>
            <option value=""4"">T4</option>
          </select>
        </div>
        <div class=""ctrl-grupo"">
          <label>Empresa</label>
          <select id=""filtroEmpresa"" onchange=""filtrar()""><option value="""">Todas</option></select>
        </div>
        <span id=""contador""></span>
      </div>

      <!-- Filtros avanzados (botón ⚙) -->
      <div id=""panel-avanzado"" style=""display:none"">
        <div class=""av-g""><label>Fecha documento desde</label><input type=""date"" id=""avFechaDesde"" onchange=""filtrar()""></div>
        <div class=""av-g""><label>Fecha documento hasta</label><input type=""date"" id=""avFechaHasta"" onchange=""filtrar()""></div>
        <div class=""av-g""><label>Factura añadida</label>
          <select id=""avAddPreset"" onchange=""aplicarPresetAnadida()"">
            <option value="""">Cualquier momento</option>
            <option value=""hoy"">Hoy</option>
            <option value=""7"">Últimos 7 días</option>
            <option value=""30"">Últimos 30 días</option>
            <option value=""mes"">Este mes</option>
            <option value=""rango"">Rango personalizado…</option>
          </select></div>
        <div class=""av-g""><label>Añadida desde</label><input type=""date"" id=""avAddDesde"" onchange=""rangoAnadidaManual()""></div>
        <div class=""av-g""><label>Añadida hasta</label><input type=""date"" id=""avAddHasta"" onchange=""rangoAnadidaManual()""></div>
        <div class=""av-g""><label>Importe mín. (€)</label><input type=""number"" id=""avImpMin"" step=""0.01"" oninput=""filtrar()""></div>
        <div class=""av-g""><label>Importe máx. (€)</label><input type=""number"" id=""avImpMax"" step=""0.01"" oninput=""filtrar()""></div>
        <div class=""av-g""><label>IVA</label><select id=""avIva"" onchange=""filtrar()""><option value="""">Todos</option></select></div>
        <div class=""av-g""><label>Método de pago</label><select id=""avPago"" onchange=""filtrar()""><option value="""">Todos</option></select></div>
        <div class=""av-g""><label>Presentada</label><select id=""avPres"" onchange=""filtrar()""><option value="""">Todas</option></select></div>
        <div class=""av-g""><label>Ordenar por</label>
          <select id=""avOrden"" onchange=""filtrar()"">
            <option value="""">Por defecto</option>
            <option value=""fecha_desc"">Fecha documento ↓ (más recientes)</option>
            <option value=""fecha_asc"">Fecha documento ↑ (más antiguos)</option>
            <option value=""total_desc"">Importe ↓ (mayor)</option>
            <option value=""total_asc"">Importe ↑ (menor)</option>
            <option value=""guardado_desc"">Añadida ↓ (últimas)</option>
            <option value=""guardado_asc"">Añadida ↑ (primeras)</option>
          </select></div>
        <div class=""av-g av-emp""><label>Empresas (varias)</label><div id=""avEmpresas"" class=""av-emps""></div></div>
      </div>

      <!-- Filtros activos + vistas guardadas -->
      <div id=""fila-chips"" class=""solo-docs"">
        <span id=""chipsActivos""></span>
        <span class=""ctrl-grupo""><label>Vistas guardadas</label>
          <select id=""selVistas"" onchange=""cargarVistaGuardada(this.value)""><option value="""">— elegir —</option></select></span>
        <button class=""chip"" onclick=""guardarVistaActual()"" title=""Guarda búsqueda y filtros actuales con un nombre"">💾 Guardar vista</button>
        <button class=""chip"" id=""btnBorrarVista"" onclick=""borrarVistaGuardada()"" style=""display:none"" title=""Borrar la vista elegida"">🗑</button>
        <button class=""chip"" id=""btnLimpiar"" onclick=""limpiarAvanzado()"" style=""display:none"">✕ Limpiar filtros avanzados</button>
      </div>

      <!-- Filtros propios del listado de artículos -->
      <div id=""fila-art"">
        <span class=""ctrl-grupo""><label>Vista</label>
          <button class=""chip on"" id=""btnArtTabla"" onclick=""setArtVista('tabla')"" title=""Tabla por empresa con comparador integrado"">📋 Tabla</button>
          <button class=""chip"" id=""btnArtMatriz"" onclick=""setArtVista('matriz')"" title=""Matriz artículo × empresa"">🧮 Matriz</button></span>
        <button class=""chip btn-fus"" onclick=""abrirFus('sug')"" title=""Unir artículos que las empresas escriben distinto"">🔗 Fusiones<span id=""fusBadge""></span></button>
        <span class=""ctrl-grupo""><label>Comparar por</label>
          <select id=""artModo"" onchange=""filtrar()"" title=""Valor que se compara entre empresas"">
            <option value=""ultimo"">Último precio</option>
            <option value=""media"">Precio medio</option>
            <option value=""min"">Mínimo histórico</option>
            <option value=""primero"">Primer precio</option>
          </select></span>
        <span class=""ctrl-grupo""><label>Variación</label>
          <select id=""artVar"" onchange=""filtrar()"">
            <option value="""">Todas</option>
            <option value=""sube"">Solo suben ▲</option>
            <option value=""baja"">Solo bajan ▼</option>
            <option value=""igual"">Sin cambios</option>
          </select></span>
        <span class=""ctrl-grupo""><label>Cambio mínimo %</label>
          <input type=""number"" id=""artPct"" min=""0"" step=""1"" value=""0"" style=""width:70px"" oninput=""filtrar()""></span>
        <span class=""ctrl-grupo""><label>Registros</label>
          <select id=""artMin"" onchange=""filtrar()"">
            <option value=""1"">1+ (todos)</option>
            <option value=""2"">2+ (comparables)</option>
            <option value=""3"">3+</option>
            <option value=""5"">5+</option>
          </select></span>
        <span class=""ctrl-grupo""><label>Ordenar</label>
          <select id=""artOrden"" onchange=""filtrar()"">
            <option value=""alfa"">Nombre A-Z</option>
            <option value=""sube"">Mayor subida</option>
            <option value=""baja"">Mayor bajada</option>
            <option value=""ahorro"">Mayor ahorro potencial</option>
            <option value=""regs"">Más registros</option>
            <option value=""gasto"">Mayor gasto</option>
            <option value=""reciente"">Último registro</option>
          </select></span>
      </div>
    </div>

    <!-- Listado scrollable -->
    <div id=""listado"">
      <div id=""contenido""></div>
    </div>
  </main>
</div>

<!-- ═══════════════════════════════════════════════════════════
     DETALLE DE ARTÍCULO — gráfica comparada, histórico y fórmulas
═══════════════════════════════════════════════════════════ -->
<div id=""modal-art"" onclick=""if(event.target===this)cerrarArt()"">
  <div id=""modal-art-in"">
    <div id=""modal-art-head"">
      <button onclick=""navArt(-1)"" title=""Anterior (←)"">◀</button>
      <h3 id=""artT""></h3>
      <button onclick=""navArt(1)"" title=""Siguiente (→)"">▶</button>
      <button onclick=""cerrarArt()"" title=""Cerrar (Esc)"">✕</button>
    </div>
    <div id=""artB""></div>
  </div>
</div>

<!-- ═══════════════════════════════════════════════════════════
     FUSIÓN DE NOMBRES DE ARTÍCULOS — sugerencias, manual y activas
═══════════════════════════════════════════════════════════ -->
<div id=""modal-fus"" onclick=""if(event.target===this)cerrarFus()"">
  <div id=""modal-fus-in"">
    <div id=""modal-fus-head"">
      <h3>🔗 Fusionar artículos con nombres distintos</h3>
      <button onclick=""cerrarFus()"" title=""Cerrar (Esc)"">✕</button>
    </div>
    <div id=""fusT""></div>
    <div id=""fusB""></div>
  </div>
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
  display:grid;grid-template-columns:repeat(auto-fill,minmax(var(--mini-w,160px),1fr));
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
.tarjeta.presentada{border-bottom:4px solid var(--rojo);}
.tarjeta img{
  width:100%;height:var(--mini-h,110px);object-fit:cover;object-position:top;display:block;
  filter:contrast(1.15) saturate(1.05);
  box-shadow:inset 0 0 0 1px rgba(0,0,0,.08);
}
.img-wrap{position:relative;}
.lineas-txt{position:relative;height:var(--mini-h,110px);padding:6px 8px;background:#f8f9fa;
  overflow:hidden;display:flex;align-items:flex-start;}
.lineas-desc{font-size:.72em;color:#444;line-height:1.3;}
.badge-lineas{
  position:absolute;top:4px;right:4px;z-index:1;
  background:rgba(0,0,0,.55);color:#fff;font-size:.68em;
  padding:2px 6px;border-radius:10px;
}
.tarjeta .ph{height:var(--mini-h,110px);background:#eee;display:flex;align-items:center;
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

.btn-vista{position:relative;}
/* ── Listado: filtros avanzados, vistas guardadas, exportar y artículos ── */
.btn-vista .nb{position:absolute;top:-7px;right:-7px;background:var(--rojo);color:#fff;border-radius:10px;font-size:.72em;padding:0 5px;display:none;}
#controles.modo-art .solo-docs{display:none !important;}
#fila-art{display:none;flex-wrap:wrap;gap:12px;align-items:center;margin-top:8px;padding-top:8px;border-top:1px dashed var(--borde);}
#controles.modo-art #fila-art{display:flex;}
#controles.modo-art #panel-avanzado{display:none !important;}
#fila-chips{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-top:8px;padding-top:8px;border-top:1px dashed var(--borde);}
.chip{padding:4px 11px;border:1px solid var(--borde);border-radius:20px;background:#fff;cursor:pointer;font-size:.78em;font-family:inherit;}
.chip:hover{border-color:var(--azul);background:var(--azul-s);}
.cx-act{display:inline-flex;gap:5px;align-items:center;background:var(--azul-s);color:var(--azul);border-radius:20px;padding:3px 6px 3px 11px;font-size:.78em;margin-right:6px;}
.cx-act button{border:none;background:none;color:var(--azul);cursor:pointer;font-size:1em;}
#panel-avanzado{margin-top:8px;padding:12px;background:#f8f9fa;border:1px solid var(--borde);border-radius:10px;
  grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:10px;}
.av-g{display:flex;flex-direction:column;gap:4px;}
.av-g label{font-size:.72em;color:var(--gris);}
.av-g input,.av-g select{padding:5px 8px;border:1px solid var(--borde);border-radius:6px;font-size:.82em;background:#fff;width:100%;font-family:inherit;}
.av-emps{max-height:110px;overflow:auto;background:#fff;border:1px solid var(--borde);border-radius:6px;padding:4px 8px;}
.av-emps label{display:flex;gap:6px;font-size:.8em;color:#202124;padding:1px 0;align-items:center;}
.av-emps input{width:auto;}
#lblTam{font-size:.75em;color:var(--gris);min-width:48px;}
.menu-exp{position:relative;display:inline-block;}
.dd-exp{display:none;position:absolute;left:0;top:110%;background:#fff;border:1px solid var(--borde);border-radius:10px;min-width:190px;z-index:30;box-shadow:0 8px 24px rgba(0,0,0,.18);padding:6px;}
.dd-exp.abierto{display:block;}
.dd-exp div{padding:7px 10px;cursor:pointer;border-radius:6px;font-size:.85em;}
.dd-exp div:hover{background:var(--azul-s);}
/* Artículos */
.art-kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:8px;margin-bottom:12px;}
.art-kpi{background:#fff;border-radius:10px;padding:10px 12px;box-shadow:0 1px 4px rgba(0,0,0,.06);}
.art-kpi b{display:block;font-size:1em;margin-top:2px;}
.art-kpi span{font-size:.72em;color:var(--gris);}
.wrapx{overflow-x:auto;background:#fff;border-radius:0 0 10px 10px;box-shadow:0 1px 4px rgba(0,0,0,.06);}
table.arts{width:100%;border-collapse:collapse;font-size:.82em;}
table.arts th{background:#f5f7fa;padding:8px 10px;text-align:left;font-size:.85em;white-space:nowrap;}
table.arts td{padding:7px 10px;border-top:1px solid #f0f0f0;white-space:nowrap;}
table.arts td.n,table.arts th.n{text-align:right;}
tr.art-fila{cursor:pointer;}
tr.art-fila:hover td{background:var(--azul-s);}
tr.art-hist td{background:#fafbfc;white-space:normal;}
.var{font-weight:700;white-space:nowrap;margin-left:4px;}
.var.sube{color:var(--rojo);}
.var.baja{color:var(--verde);}
.var.igual{color:var(--gris);}
.art-panel{display:flex;gap:14px;flex-wrap:wrap;align-items:flex-start;}
.art-lista{flex:1;min-width:300px;max-height:170px;overflow-y:auto;}
.art-reg{display:grid;grid-template-columns:90px 1fr 60px 90px 80px 80px;gap:8px;font-size:.78em;padding:3px 6px;border-radius:4px;cursor:pointer;}
.art-reg:hover{background:var(--azul-s);}
.art-reg.cab{font-weight:700;color:var(--gris);cursor:default;position:sticky;top:0;background:#fafbfc;}
.art-reg.cab:hover{background:#fafbfc;}
.art-otras{font-size:.78em;color:#444;min-width:200px;}
.art-otras b{display:block;margin-bottom:3px;color:var(--gris);}
@media print{
  html,body{height:auto !important;overflow:visible !important;}
  #panel-izq,#controles,#modal{display:none !important;}
  #layout{display:block;height:auto;overflow:visible;}
  #panel-der,#listado{overflow:visible !important;height:auto !important;}
}

/* ── Artículos: KPIs clicables, galería, matriz y detalle ── */
.art-kpi{background:#fff;border-radius:10px;padding:10px 12px;box-shadow:0 1px 4px rgba(0,0,0,.06);border:2px solid transparent;}
.art-kpi.click{cursor:pointer;transition:all .15s;}
.art-kpi.click:hover{border-color:var(--azul);transform:translateY(-2px);}
.art-kpi.on{border-color:var(--azul);background:var(--azul-s);}
.art-kpi b{font-size:1.05em;}
.art-kpi small{display:block;color:var(--gris);}
.pill{display:inline-block;padding:1px 8px;border-radius:10px;font-size:.78em;font-weight:700;}
.pill.sube{background:var(--rojo-s);color:var(--rojo);}
.pill.baja,.pill.mej{background:var(--verde-s);color:var(--verde);}
.pill.igual{background:#f1f3f4;color:var(--gris);}
.sw{width:11px;height:11px;border-radius:3px;display:inline-block;}
.lk{color:var(--azul);cursor:pointer;text-decoration:underline dotted;}
.lk:hover{text-decoration:underline;}
.heatb{display:inline-block;height:6px;border-radius:3px;vertical-align:middle;margin-left:5px;}
.chipv{cursor:pointer;padding:2px 9px;border-radius:12px;border:1px solid transparent;font-weight:700;}
.chipv.sube{color:var(--rojo);}.chipv.baja{color:var(--verde);}
.chipv:hover{border-color:currentColor;background:#fff;}
.empresa-cab .suma{display:flex;gap:6px;align-items:center;}
tr.art-fila.sel td{background:var(--azul-s);}
table.arts th.clk{cursor:pointer;}
table.arts th.clk:hover{background:var(--azul-s);color:var(--azul);}
table.arts th.sel{background:var(--azul);color:#fff;}
td.cel{text-align:center;min-width:128px;cursor:pointer;}
td.cel:hover{outline:2px solid var(--azul);outline-offset:-2px;}
td.cel.mej{background:var(--verde-s);}
td.cel.mej .p{color:var(--verde);}
td.cel.sel{outline:2px solid var(--azul);outline-offset:-2px;}
td.cel .p{font-weight:700;}
td.cel .d{font-size:.78em;}
td.cel.vacio{color:#bbb;cursor:default;}
td.cel.vacio:hover{outline:none;}
table.arts tfoot td{font-weight:700;background:#f5f7fa;}
.art-ahorro{background:linear-gradient(90deg,var(--verde-s),#fff);border:1px solid #cfe8d6;border-radius:10px;padding:10px 14px;margin-bottom:12px;display:flex;flex-wrap:wrap;gap:16px;align-items:center;}
.art-ahorro b{color:var(--verde);font-size:1.1em;}
.art-nota{color:var(--gris);font-size:.78em;margin:6px 2px;}
.art-foco{display:flex;gap:8px;align-items:center;background:var(--azul);color:#fff;border-radius:10px;padding:8px 14px;margin-bottom:10px;flex-wrap:wrap;}
.art-foco button{border:1px solid #fff8;background:#fff2;color:#fff;border-radius:6px;padding:3px 10px;cursor:pointer;font:inherit;}
.art-gal{display:grid;grid-template-columns:repeat(auto-fill,minmax(270px,1fr));gap:10px;margin-bottom:14px;}
.art-gc{background:#fff;border-radius:10px;box-shadow:0 1px 4px rgba(0,0,0,.06);padding:10px;cursor:pointer;border:2px solid transparent;transition:all .15s;}
.art-gc:hover{border-color:var(--azul);transform:translateY(-2px);}
.art-gc .t{display:flex;justify-content:space-between;gap:6px;align-items:flex-start;}
.art-gc .t b{font-size:.9em;}
.art-gc small{color:var(--gris);display:block;font-size:.74em;}
.art-gc svg{width:100%;height:auto;margin-top:4px;}
.art-gc .pie{display:flex;justify-content:space-between;font-size:.76em;color:var(--gris);}
#btnArtTabla.on,#btnArtMatriz.on{background:var(--azul-s);border-color:var(--azul);color:var(--azul);font-weight:600;}
/* Detalle (diálogo) */
#modal-art{display:none;position:fixed;inset:0;background:rgba(0,0,0,.55);z-index:900;align-items:center;justify-content:center;}
#modal-art.activo{display:flex;}
#modal-art-in{background:#fff;border-radius:12px;width:min(980px,96vw);max-height:92vh;overflow:auto;box-shadow:0 12px 40px rgba(0,0,0,.35);}
#modal-art-head{display:flex;gap:8px;align-items:center;padding:12px 16px;border-bottom:1px solid var(--borde);position:sticky;top:0;background:#fff;z-index:2;}
#modal-art-head h3{flex:1;font-size:1.05em;}
#modal-art-head button{padding:5px 11px;border:1px solid var(--borde);border-radius:6px;background:#f8f9fa;cursor:pointer;font:inherit;}
#modal-art-head button:hover{background:var(--azul-s);border-color:var(--azul);}
#artB{padding:14px 16px;}
.art-dk{display:grid;grid-template-columns:repeat(auto-fit,minmax(135px,1fr));gap:8px;margin-bottom:10px;}
.art-leg{display:flex;flex-wrap:wrap;gap:10px;margin:6px 0;}
.art-leg label{display:flex;gap:5px;align-items:center;font-size:.8em;cursor:pointer;}
.art-dos{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:10px;}
@media(max-width:760px){.art-dos{grid-template-columns:1fr;}}
.art-formula{background:#f8f9fa;border:1px solid var(--borde);border-radius:10px;padding:10px 12px;font-size:.8em;}
.art-formula code{background:#e8f0fe;color:#174ea6;padding:1px 6px;border-radius:4px;font-family:Consolas,monospace;}
.art-formula .l{margin:2px 0;font-family:Consolas,monospace;font-size:.95em;white-space:normal;}
.art-reg{display:grid;grid-template-columns:84px 1fr 50px 80px 76px 76px;gap:6px;font-size:.76em;padding:3px 6px;border-radius:4px;cursor:pointer;}
.art-reg.h{font-weight:700;color:var(--gris);cursor:default;}
.art-reg:not(.h):hover{background:var(--azul-s);}
.art-reglst{max-height:230px;overflow:auto;}
@media print{#modal-art{display:none !important;}}

/* ── Fusión de nombres de artículos ── */
.btn-fus{position:relative;}
#fusBadge{background:var(--rojo);color:#fff;border-radius:10px;font-size:.78em;padding:0 6px;margin-left:5px;display:none;}
#modal-fus{display:none;position:fixed;inset:0;background:rgba(0,0,0,.55);z-index:950;align-items:center;justify-content:center;}
#modal-fus.activo{display:flex;}
#modal-fus-in{background:#fff;border-radius:12px;width:min(900px,96vw);max-height:92vh;display:flex;flex-direction:column;box-shadow:0 12px 40px rgba(0,0,0,.35);}
#modal-fus-head{display:flex;gap:8px;align-items:center;padding:12px 16px;border-bottom:1px solid var(--borde);}
#modal-fus-head h3{flex:1;font-size:1.05em;}
#modal-fus-head button{padding:5px 11px;border:1px solid var(--borde);border-radius:6px;background:#f8f9fa;cursor:pointer;font:inherit;}
#fusT{display:flex;border-bottom:1px solid var(--borde);}
#fusT button{flex:1;padding:9px;border:none;background:#f8f9fa;cursor:pointer;font:inherit;font-size:.85em;}
#fusT button.on{background:#fff;color:var(--azul);font-weight:700;box-shadow:inset 0 -2px 0 var(--azul);}
#fusB{padding:14px 16px;overflow:auto;}
.fus-ctl{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:10px;font-size:.82em;}
.fus-tit{font-weight:700;margin:14px 0 6px;font-size:.88em;}
.fus-vacio{color:var(--gris);font-size:.82em;padding:8px;}
.fus-fila{border:1px solid var(--borde);border-radius:10px;padding:10px 12px;margin-bottom:8px;background:#fff;}
.fus-fila.rev{border-color:#f3c9c6;background:#fffafa;}
.fus-nom{display:grid;grid-template-columns:1fr auto 1fr;gap:10px;align-items:start;font-size:.88em;font-weight:600;}
.fus-flecha{color:var(--gris);font-size:1.2em;align-self:center;}
.fus-inf{color:var(--gris);font-size:.78em;font-weight:400;}
.fus-pie{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-top:8px;}
.fus-sim{font-size:.78em;color:var(--gris);}
.chip.fus-si{border-color:var(--verde);color:var(--verde);}
.chip.fus-si:hover{background:var(--verde-s);}
.fus-man{display:flex;flex-direction:column;gap:6px;max-width:520px;font-size:.85em;}
.fus-man input{padding:7px 10px;border:1px solid var(--borde);border-radius:6px;font:inherit;}
.fus-man label{font-size:.78em;color:var(--gris);margin-top:4px;}
@media(max-width:700px){.fus-nom{grid-template-columns:1fr;}.fus-flecha{display:none;}}
@media print{#modal-fus{display:none !important;}}
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
// Fecha de guardado (yyyy-MM-dd HH:mm:ss) -> dd/MM/yyyy HH:mm. Vacía si no hay dato.
function fmtGuardado(t){
  const m=(t.fecha_guardado||'').trim().match(/^(\d{4})-(\d{2})-(\d{2})(?:[ T](\d{2}):(\d{2}))?/);
  if(!m) return '';
  return m[3]+'/'+m[2]+'/'+m[1]+(m[4]?' '+m[4]+':'+m[5]:'');
}
// Color estable y distinto para cada trimestre presentado (etiqueta tipo 2026-2T).
function colorTrimestre(lbl){
  const m=(lbl||'').match(/(\d{4}).*?(\d)/);
  if(!m) return null;
  const n=parseInt(m[1],10)*4+parseInt(m[2],10);
  return 'hsl('+Math.round((n*137.508)%360)+',70%,42%)';
}
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
  document.getElementById('filtroTrimestre').value='';
  document.getElementById('filtroEmpresa').innerHTML='<option value="""">Todas</option>';
  document.getElementById('anioSel').innerHTML='';
  document.getElementById('buscar').value='';
  filtroEspecial=null;
  limpiarAvanzado(true);
}
function cambiarTipo(tipo, btn){
  if(tipo===tipoActual) return;
  tipoActual=tipo;
  // 'articulos' trabaja sobre facturas + albaranes juntos (precios comparables entre ambos)
  tickets = tipo==='albaranes' ? albaranesData : tipo==='articulos' ? facturasData.concat(albaranesData) : facturasData;
  empresasCarpetas = tipo==='albaranes' ? empresasCarpetasAlbaranes
    : tipo==='articulos' ? [...new Set(empresasCarpetasFacturas.concat(empresasCarpetasAlbaranes))]
    : empresasCarpetasFacturas;
  document.getElementById('controles').classList.toggle('modo-art', tipo==='articulos');
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
  poblarAvanzado();
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

/* ─── Recargar panel: pide a la app que regenere el HTML y lo recargue ─── */
function recargarPanel(){
  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage({accion: 'recargar'});
  } else {
    location.reload();
  }
}

/* ─── Vista ─── */
function setVista(v, btn){
  vistaActual=v;
  document.querySelectorAll('.btn-vista[data-vista]').forEach(b=>b.classList.remove('activo'));
  btn.classList.add('activo');
  // En modo lista (slider al mínimo) estas vistas ordenan la tabla por columna
  if(modoLista){ ordenListaCol = v==='guardado_desc' ? 'guardado' : 'empresa'; ordenListaAsc = v!=='guardado_desc'; }
  renderizar(listaFiltrada);
}

// Slider de tamaño de miniatura: la altura se deriva del ancho (relación
// aproximada 1.45:1, la misma que ya tenían las tarjetas a 160x110).
function ajustarTamanoMiniatura(anchoPx){
  const w = parseInt(anchoPx, 10);
  const lista = w <= SLIDER_LISTA;   // slider al mínimo = vista lista
  if(!lista){
    const h = Math.round(w / 1.45);
    document.documentElement.style.setProperty('--mini-w', w+'px');
    document.documentElement.style.setProperty('--mini-h', h+'px');
  }
  document.getElementById('lblTam').textContent = lista ? '☰ Lista' : w+'px';
  if(lista !== modoLista){
    modoLista = lista;
    if(lista && vistaActual==='guardado_desc'){ ordenListaCol='guardado'; ordenListaAsc=false; }
    renderizar(listaFiltrada);
  }
}

/* ─── Filtrar ─Búsqueda por varias palabras clave separadas por comas: coincide si
  // la factura contiene AL MENOS UNA de ellas (ej. diesel,gasoleo a).── */
function filtrar(){
 const terminos=(document.getElementById('buscar').value||'').toLowerCase()
    .split(',').map(s=>s.trim()).filter(Boolean);
  const anio=document.getElementById('filtroAnio').value;
  const trimestre=document.getElementById('filtroTrimestre').value;
  const empresa=document.getElementById('filtroEmpresa').value;
  listaFiltrada = tickets.filter(t=>{
    const okQ = terminos.length===0 || terminos.some(q =>
      (t.empresa||'').toLowerCase().includes(q)
      ||(t.fecha||'').toLowerCase().includes(q)
      ||(t.numero||'').toLowerCase().includes(q)
      ||(t.cif||'').toLowerCase().includes(q)
      ||(t.receptor_nombre||'').toLowerCase().includes(q)
      ||(t.total||'').toLowerCase().includes(q)
      ||(t.metodo_pago||'').toLowerCase().includes(q)
      ||(t.items||[]).some(i=>(i.descripcion||'').toLowerCase().includes(q)));
    const okA = !anio || anioFecha(t)===anio;
    const okT = !trimestre || Math.ceil(mesFecha(t)/3)===parseInt(trimestre);
    const okE = !empresa || empresaCarpeta(t)===empresa;
    const okEsp = !filtroEspecial
      || (filtroEspecial==='sinTotal' && (!t.total||num(t.total)===0))
      || (filtroEspecial==='sinFecha' && mesFecha(t)===0);
    return okQ && okA && okT && okE && okEsp && pasaAvanzado(t);
  });
  document.getElementById('contador').textContent = listaFiltrada.length+' resultado(s)';
  actualizarChips();
  renderStats(listaFiltrada);
  dibujarTopEmpresas(listaFiltrada);
  dibujarIvaTrimestral();
  renderizar(listaFiltrada);
}

/* ─── Renderizar listado ─── */
function renderizar(lista){
  const c=document.getElementById('contenido');
  if(!lista.length){ c.innerHTML='<div id=""vacio"">No se encontraron documentos.</div>'; return; }

  if(tipoActual==='articulos'){ renderArticulos(lista,c); return; }
  if(modoLista){ renderLista(lista,c); return; }

  // Ordenar (selector ""Ordenar por"" de filtros avanzados) / agrupar
  let items=[...lista];
  ordenarItems(items);

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
  const clasePresentada=t.presentado?' presentada':'';
  const colTrim=t.presentado?colorTrimestre(t.trimestre_presentado):null;
  const estiloTrim=colTrim?` style=""border-bottom-color:${colTrim}"" title=""Presentada en ${t.trimestre_presentado}""`:'';
  const guardado=fmtGuardado(t);
  return `<div class=""tarjeta${clasePresentada}""${estiloTrim} onclick=""abrirModal(${idx})"">
    ${img}
    <div class=""resumen"">
      <div class=""fecha"">${t.fecha||'—'}</div>
      ${guardado?`<div class=""fecha"" title=""Fecha de guardado"">➕ ${guardado}</div>`:''}
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
  let ordenada=[...lista];
  if(!ordenListaCol) ordenarItems(ordenada);
  if(ordenListaCol){
    const getters={
      empresa:t=>(t.empresa||'').toLowerCase(),
      fecha:t=>claveOrden(t.fecha),
      numero:t=>(t.numero||'').toLowerCase(),
      cif:t=>(t.cif||'').toLowerCase(),
      total:t=>num(t.total),
      iva:t=>num(t.iva),
      tipo:t=>(t.tipo_documento||'').toLowerCase(),
      metodo_pago:t=>(t.metodo_pago||'').toLowerCase(),
      guardado:t=>t.fecha_guardado||''
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
    const colTrim=t.presentado?colorTrimestre(t.trimestre_presentado):null;
    const estiloTrim=colTrim?` style=""border-left:5px solid ${colTrim}"" title=""Presentada en ${t.trimestre_presentado}""`:'';
    return `<tr onclick=""abrirModal(${idx})"">
      <td${estiloTrim}>${t.empresa||'—'}</td>
      <td>${t.fecha||'—'}</td>
      <td>${t.numero||'—'}</td>
      <td>${t.cif||'—'}</td>
      <td>${t.tipo_documento||'—'}</td>
      <td style=""text-align:right"">${t.iva?eur(num(t.iva)):'—'}</td>
      <td style=""text-align:right;font-weight:600;color:${tieneTotal?'#137333':'#c5221f'}"">${tieneTotal?eur(num(t.total)):'—'}</td>
      <td>${t.metodo_pago||'—'}</td>
      <td>${fmtGuardado(t)||'—'}</td>
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
      <th onclick=""ordenarLista('guardado')"" style=""cursor:pointer"">Añadida${flecha('guardado')}</th>
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

/* ═══════════════════════════════════════════════════════════════════════
   Listado: filtros avanzados · vistas guardadas · exportación · artículos
   ═══════════════════════════════════════════════════════════════════════ */
const $id = id => document.getElementById(id);
const SLIDER_LISTA = 60;          // valor mínimo del slider = vista lista
let modoLista = false;            // true cuando el slider está al mínimo
let empresasSel = [];             // empresas marcadas en filtros avanzados (vacío = todas)
let vistasGuardadas = [];
const CLAVE_VISTAS = 'facticket_vistas_v1';
const IDS_AV = ['avFechaDesde','avFechaHasta','avAddPreset','avAddDesde','avAddHasta','avImpMin','avImpMax','avIva','avPago','avPres','avOrden'];
const IDS_BASE = ['buscar','filtroAnio','filtroTrimestre','filtroEmpresa'];

// Fecha en cualquier formato habitual -> 'yyyy-mm-dd' (o '' si no se reconoce).
function fechaCompleta(str){
  if(!str) return '';
  str=str.toString().trim();
  let m=str.match(/^(\d{4})[-\/.](\d{1,2})[-\/.](\d{1,2})/);
  if(m) return m[1]+'-'+m[2].padStart(2,'0')+'-'+m[3].padStart(2,'0');
  m=str.match(/^(\d{1,2})[-\/.](\d{1,2})[-\/.](\d{4})/);
  if(m) return m[3]+'-'+m[2].padStart(2,'0')+'-'+m[1].padStart(2,'0');
  m=str.match(/^(\d{1,2})[-\/.](\d{1,2})[-\/.](\d{2})$/);
  if(m) return '20'+m[3]+'-'+m[2].padStart(2,'0')+'-'+m[1].padStart(2,'0');
  return '';
}
function isoLocal(d){ return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0'); }

/* ─── Panel de filtros avanzados ─── */
function toggleAvanzado(){
  const p=$id('panel-avanzado');
  const abierto=p.style.display!=='none';
  p.style.display=abierto?'none':'grid';
  $id('btnAvanzado').classList.toggle('activo',!abierto);
}
// Rellena los desplegables del panel con los valores reales del conjunto actual.
function poblarAvanzado(){
  const vals=f=>[...new Set(tickets.map(f).filter(v=>v!==''&&v!==undefined&&v!==null))];
  const ivas=vals(t=>t.iva_porcentaje>0?String(t.iva_porcentaje):'').sort((a,b)=>a-b);
  $id('avIva').innerHTML='<option value="""">Todos</option>'+ivas.map(v=>`<option value=""${v}"">${v}%</option>`).join('');
  const pagos=vals(t=>(t.metodo_pago||'').trim()).sort();
  $id('avPago').innerHTML='<option value="""">Todos</option>'+pagos.map(v=>`<option>${v}</option>`).join('');
  // Presentada: Todas / Sí / No / cada trimestre con su color (el mismo de las tarjetas)
  const trims=vals(t=>t.presentado?(t.trimestre_presentado||''):'').sort();
  $id('avPres').innerHTML='<option value="""">Todas</option><option value=""si"">Sí (cualquier trimestre)</option><option value=""no"">No presentadas</option>'
    +trims.map(v=>`<option value=""${v}"" style=""color:${colorTrimestre(v)||'inherit'}"">■ Presentada en ${v}</option>`).join('');
  const emps=(empresasCarpetas&&empresasCarpetas.length)?[...empresasCarpetas].sort():[...new Set(tickets.map(empresaCarpeta))].sort();
  $id('avEmpresas').innerHTML=emps.map(e=>`<label><input type=""checkbox"" value=""${e.replace(/""/g,'&quot;')}"" onchange=""toggleEmpresaSel(this)""> ${e}</label>`).join('');
}
function toggleEmpresaSel(cb){
  empresasSel = cb.checked ? [...empresasSel, cb.value] : empresasSel.filter(x=>x!==cb.value);
  filtrar();
}
// ""Factura añadida"": atajos de fecha de guardado (hoy, 7 días, 30 días, este mes).
function aplicarPresetAnadida(){
  const p=$id('avAddPreset').value, hoy=new Date();
  let d1='', d2='';
  if(p==='hoy'){ d1=d2=isoLocal(hoy); }
  else if(p==='7'||p==='30'){ const ini=new Date(hoy); ini.setDate(ini.getDate()-parseInt(p,10)); d1=isoLocal(ini); d2=isoLocal(hoy); }
  else if(p==='mes'){ d1=isoLocal(new Date(hoy.getFullYear(),hoy.getMonth(),1)); d2=isoLocal(hoy); }
  if(p!=='rango'){ $id('avAddDesde').value=d1; $id('avAddHasta').value=d2; }
  filtrar();
}
function rangoAnadidaManual(){
  $id('avAddPreset').value = ($id('avAddDesde').value||$id('avAddHasta').value) ? 'rango' : '';
  filtrar();
}
// Devuelve true si el documento cumple TODOS los filtros avanzados activos.
function pasaAvanzado(t){
  const fd1=$id('avFechaDesde').value, fd2=$id('avFechaHasta').value;
  if(fd1||fd2){
    const f=fechaCompleta(t.fecha);
    if(!f || (fd1&&f<fd1) || (fd2&&f>fd2)) return false;
  }
  const ad1=$id('avAddDesde').value, ad2=$id('avAddHasta').value;
  if(ad1||ad2){
    const g=(t.fecha_guardado||'').slice(0,10);
    if(!/^\d{4}-\d{2}-\d{2}$/.test(g) || (ad1&&g<ad1) || (ad2&&g>ad2)) return false;
  }
  const imn=$id('avImpMin').value, imx=$id('avImpMax').value;
  if(imn!==''||imx!==''){
    const v=num(t.total);
    if((imn!==''&&v<parseFloat(imn)) || (imx!==''&&v>parseFloat(imx))) return false;
  }
  const iva=$id('avIva').value;
  if(iva!=='' && String(t.iva_porcentaje||0)!==iva) return false;
  const pago=$id('avPago').value;
  if(pago && (t.metodo_pago||'').trim()!==pago) return false;
  const pres=$id('avPres').value;
  if(pres==='si' && !t.presentado) return false;
  if(pres==='no' && t.presentado) return false;
  if(pres && pres!=='si' && pres!=='no' && (!t.presentado || t.trimestre_presentado!==pres)) return false;
  if(empresasSel.length && !empresasSel.includes(empresaCarpeta(t))) return false;
  return true;
}
function limpiarAvanzado(silencioso){
  ['avFechaDesde','avFechaHasta','avAddDesde','avAddHasta','avImpMin','avImpMax'].forEach(i=>$id(i).value='');
  ['avAddPreset','avIva','avPago','avPres','avOrden'].forEach(i=>$id(i).value='');
  empresasSel=[];
  document.querySelectorAll('#avEmpresas input').forEach(c=>c.checked=false);
  if(!silencioso) filtrar();
}
/* ─── Chips de filtros activos ─── */
function filtrosActivos(){
  const a=[], v=i=>$id(i).value;
  if(v('avFechaDesde')||v('avFechaHasta')) a.push({k:'fecha',t:'Fecha doc.: '+(v('avFechaDesde')||'…')+' → '+(v('avFechaHasta')||'…')});
  if(v('avAddDesde')||v('avAddHasta')) a.push({k:'add',t:'Añadida: '+(v('avAddDesde')||'…')+' → '+(v('avAddHasta')||'…')});
  if(v('avImpMin')!==''||v('avImpMax')!=='') a.push({k:'imp',t:'Importe: '+(v('avImpMin')||'0')+' – '+(v('avImpMax')||'∞')+' €'});
  if(v('avIva')) a.push({k:'iva',t:'IVA '+v('avIva')+'%'});
  if(v('avPago')) a.push({k:'pago',t:'Pago: '+v('avPago')});
  if(v('avPres')){ const x=v('avPres'); a.push({k:'pres',t:x==='si'?'Presentadas':x==='no'?'Sin presentar':'Presentada en '+x}); }
  if(empresasSel.length) a.push({k:'emps',t:'Empresas: '+empresasSel.length});
  return a;
}
function quitarFiltro(k){
  if(k==='fecha'){ $id('avFechaDesde').value=''; $id('avFechaHasta').value=''; }
  else if(k==='add'){ $id('avAddDesde').value=''; $id('avAddHasta').value=''; $id('avAddPreset').value=''; }
  else if(k==='imp'){ $id('avImpMin').value=''; $id('avImpMax').value=''; }
  else if(k==='iva') $id('avIva').value='';
  else if(k==='pago') $id('avPago').value='';
  else if(k==='pres') $id('avPres').value='';
  else if(k==='emps'){ empresasSel=[]; document.querySelectorAll('#avEmpresas input').forEach(c=>c.checked=false); }
  filtrar();
}
function actualizarChips(){
  const a=filtrosActivos();
  $id('chipsActivos').innerHTML=a.map(f=>`<span class=""cx-act"">${f.t}<button onclick=""quitarFiltro('${f.k}')"" title=""Quitar"">✕</button></span>`).join('');
  $id('btnLimpiar').style.display=a.length?'':'none';
  const nb=$id('nbAvanzado');
  nb.textContent=a.length||''; nb.style.display=a.length?'inline-block':'none';
}

/* ─── Vistas guardadas (localStorage; si no está disponible, solo en memoria) ─── */
function leerVistas(){ try{ return JSON.parse(localStorage.getItem(CLAVE_VISTAS)||'[]'); }catch(e){ return vistasGuardadas||[]; } }
function escribirVistas(v){ vistasGuardadas=v; try{ localStorage.setItem(CLAVE_VISTAS, JSON.stringify(v)); }catch(e){} }
function cargarVistasGuardadas(){
  vistasGuardadas=leerVistas();
  $id('selVistas').innerHTML='<option value="""">— elegir —</option>'+vistasGuardadas.map((v,i)=>`<option value=""${i}"">${v.nombre}</option>`).join('');
  $id('btnBorrarVista').style.display='none';
}
function guardarVistaActual(){
  const n=(prompt('Nombre de la vista:','')||'').trim();
  if(!n) return;
  const o={}; IDS_AV.concat(IDS_BASE).forEach(i=>o[i]=$id(i).value); o.empresasSel=[...empresasSel];
  const v=leerVistas().filter(x=>x.nombre!==n); v.push({nombre:n,datos:o});
  escribirVistas(v); cargarVistasGuardadas();
  $id('selVistas').value=String(v.length-1); $id('btnBorrarVista').style.display='';
}
function cargarVistaGuardada(i){
  $id('btnBorrarVista').style.display = i===''?'none':'';
  if(i==='') return;
  const v=vistasGuardadas[parseInt(i,10)]; if(!v) return;
  IDS_AV.concat(IDS_BASE).forEach(id=>{ const el=$id(id); if(el && v.datos[id]!==undefined) el.value=v.datos[id]; });
  empresasSel=[...(v.datos.empresasSel||[])];
  document.querySelectorAll('#avEmpresas input').forEach(c=>c.checked=empresasSel.includes(c.value));
  filtrar();
}
function borrarVistaGuardada(){
  const i=$id('selVistas').value; if(i==='') return;
  const v=vistasGuardadas[parseInt(i,10)];
  if(!v || !confirm('¿Borrar la vista ""'+v.nombre+'""?')) return;
  escribirVistas(vistasGuardadas.filter((x,j)=>j!==parseInt(i,10)));
  cargarVistasGuardadas();
}

/* ─── Orden de las tarjetas (sustituye a los iconos de importe/fecha) ─── */
function ordenarItems(items){
  const o=$id('avOrden').value || (vistaActual==='guardado_desc'?'guardado_desc':'');
  const f={
    total_desc:(a,b)=>num(b.total)-num(a.total),
    total_asc:(a,b)=>num(a.total)-num(b.total),
    fecha_desc:(a,b)=>(fechaCompleta(b.fecha)||'').localeCompare(fechaCompleta(a.fecha)||''),
    fecha_asc:(a,b)=>(fechaCompleta(a.fecha)||'').localeCompare(fechaCompleta(b.fecha)||''),
    guardado_desc:(a,b)=>(b.fecha_guardado||'').localeCompare(a.fecha_guardado||''),
    guardado_asc:(a,b)=>(a.fecha_guardado||'').localeCompare(b.fecha_guardado||'')
  }[o];
  if(f) items.sort(f);
}

/* ─── Exportación de lo mostrado ─── */
function toggleMenuExportar(ev){ ev.stopPropagation(); $id('menuExportar').classList.toggle('abierto'); }
function cerrarMenuExportar(){ $id('menuExportar').classList.remove('abierto'); }
document.addEventListener('click',cerrarMenuExportar);

const COLS_EXP=[
  ['Empresa',t=>empresaCarpeta(t)],['Fecha',t=>t.fecha||''],['Nº',t=>t.numero||''],['CIF',t=>t.cif||''],
  ['Tipo',t=>t.tipo_documento||''],['Base',t=>num(t.base)],['IVA %',t=>t.iva_porcentaje||0],['IVA',t=>num(t.iva)],
  ['Total',t=>num(t.total)],['Pago',t=>t.metodo_pago||''],['Añadida',t=>fmtGuardado(t)],
  ['Presentada',t=>t.presentado?(t.trimestre_presentado||'Sí'):'No']
];
// Devuelve {nombre, cab, filas} con exactamente lo que se ve en pantalla.
function datosExportacion(){
  if(tipoActual==='articulos'){
    const arts=articulosFiltrados(false);
    return {nombre:'articulos',
      cab:['Empresa','Artículo','Registros','Primer precio','Fecha primero','Último precio','Fecha último','Var. desde 1º %','Var. última compra %','Mín.','Máx.','Media','Cantidad','Gasto','Mejor precio','Más barato en','Dif. vs mejor %','Ahorro potencial'],
      filas:arts.map(({a,st,c,f,ahorro})=>[a.empresa,a.nombre,st.n,st.primero?st.primero.precio:'',st.primero?st.primero.fechaTxt:'',
        st.ultimo?st.ultimo.precio:'',st.ultimo?st.ultimo.fechaTxt:'',
        st.varTotal===null?'':Math.round(st.varTotal*10)/10, st.varUltima===null?'':Math.round(st.varUltima*10)/10,
        st.min,st.max,Math.round(st.media*100)/100,st.cant,Math.round(st.gasto*100)/100,
        c?Math.round(c.mej.v*100)/100:'', c?c.mej.emp:'', f&&f.pct?Math.round(f.pct*10)/10:0, Math.round(ahorro*100)/100])};
  }
  const items=[...listaFiltrada]; ordenarItems(items);
  return {nombre:tipoActual, cab:COLS_EXP.map(c=>c[0]), filas:items.map(t=>COLS_EXP.map(c=>c[1](t)))};
}
function celdaCsv(v){
  let s=typeof v==='number'?String(v).replace('.',','):String(v===null||v===undefined?'':v);
  if(/[;""\n\r]/.test(s)) s='""'+s.replace(/""/g,'""""')+'""';
  return s;
}
function tablaHtml(d){
  const esc=s=>String(s===null||s===undefined?'':s).replace(/&/g,'&amp;').replace(/</g,'&lt;');
  return '<table border=""1""><thead><tr>'+d.cab.map(c=>'<th>'+esc(c)+'</th>').join('')+'</tr></thead><tbody>'
    +d.filas.map(r=>'<tr>'+r.map(v=>'<td>'+esc(typeof v==='number'?String(v).replace('.',','):v)+'</td>').join('')+'</tr>').join('')
    +'</tbody></table>';
}
// En la app (WebView2) pide a WinForms el ""Guardar como...""; en un navegador normal descarga un Blob.
function guardarArchivo(nombre, contenido, mime, filtro){
  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage({accion:'guardarArchivo', nombre:nombre, contenido:contenido, filtro:filtro});
    return;
  }
  const blob=new Blob(['\ufeff'+contenido],{type:mime+';charset=utf-8'});
  const a=document.createElement('a'); a.href=URL.createObjectURL(blob); a.download=nombre;
  document.body.appendChild(a); a.click();
  setTimeout(()=>{ URL.revokeObjectURL(a.href); a.remove(); },500);
}
function exportar(fmt){
  cerrarMenuExportar();
  if(fmt==='pdf'){ window.print(); return; }
  const d=datosExportacion();
  if(!d.filas.length){ alert('No hay datos que exportar con los filtros actuales.'); return; }
  const f=new Date(), p2=n=>String(n).padStart(2,'0');
  const base='FACTicket_'+d.nombre+'_'+f.getFullYear()+p2(f.getMonth()+1)+p2(f.getDate())+'_'+p2(f.getHours())+p2(f.getMinutes());
  if(fmt==='csv') guardarArchivo(base+'.csv',[d.cab].concat(d.filas).map(r=>r.map(celdaCsv).join(';')).join('\r\n'),'text/csv','CSV (*.csv)|*.csv');
  else if(fmt==='json') guardarArchivo(base+'.json',JSON.stringify(d.filas.map(r=>Object.fromEntries(d.cab.map((c,i)=>[c,r[i]]))),null,2),'application/json','JSON (*.json)|*.json');
  else if(fmt==='xls') guardarArchivo(base+'.xls','<html xmlns:x=""urn:schemas-microsoft-com:office:excel""><head><meta charset=""UTF-8""></head><body>'+tablaHtml(d)+'</body></html>','application/vnd.ms-excel','Excel (*.xls)|*.xls');
  else if(fmt==='html') guardarArchivo(base+'.html','<!DOCTYPE html><html lang=""es""><head><meta charset=""UTF-8""><title>'+base+'</title><style>body{font-family:Segoe UI,Arial,sans-serif;padding:16px}table{border-collapse:collapse;font-size:13px}th,td{border:1px solid #ccc;padding:4px 8px}th{background:#f5f7fa}</style></head><body><h2>'+base+' · '+d.filas.length+' filas</h2>'+tablaHtml(d)+'</body></html>','text/html','Informe HTML (*.html)|*.html');
}

/* ─── Listado de artículos por empresa (tabla con comparador + matriz) ─── */
// Estado propio de la pestaña Artículos
let artVista='tabla';          // 'tabla' | 'matriz'
let artFoco='';                // '' | 'todos' | 'sube' | 'baja' | 'gasto' | 'ahorro' (KPI pulsado)
let artSel={clave:null, emp:null};
let artNav=[], artNavPos=0;    // lista para los botones ◀ ▶ del detalle
let artOcultas=new Set();      // empresas ocultas en la gráfica comparada
const COL_ART=['#1a73e8','#e37400','#137333','#8e24aa','#c5221f','#00838f'];
const encA=encodeURIComponent, decA=decodeURIComponent;
const escH=s=>(s===null||s===undefined?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;');
const n2=n=>n.toLocaleString('es-ES',{minimumFractionDigits:2,maximumFractionDigits:2});
const clsVar=p=>(p===null||p===undefined||!isFinite(p)||Math.abs(p)<0.05)?'igual':(p>0?'sube':'baja');

// Clave de comparación: minúsculas, sin tildes y sin espacios repetidos.
function normArt(s){ return (s||'').toString().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/\s+/g,' ').trim(); }
// Variación porcentual de n respecto a la base b (null si no hay base).
function pctVar(n,b){ return b>0 ? (n-b)/b*100 : null; }
// HTML de una variación: +17% en rojo (sube de precio), -17% en verde (baja).
function fmtVar(p){
  if(p===null||p===undefined||!isFinite(p)) return '<span class=""var igual"">—</span>';
  const r=Math.round(p*10)/10;
  if(Math.abs(r)<0.05) return '<span class=""var igual"">0%</span>';
  return `<span class=""var ${r>0?'sube':'baja'}"">${r>0?'+':'-'}${Math.abs(r).toLocaleString('es-ES',{maximumFractionDigits:1})}%</span>`;
}

// Índice de TODO el histórico cargado: (empresa + artículo) -> registros ordenados por fecha.
// La clave pasa por canonArt(): los nombres que el usuario ha FUNDIDO comparten historial.
// Se guardan las variantes de nombre original y se muestra la más frecuente.
function construirArticulos(){
  const mapa=new Map(), nombresRaw={};
  tickets.forEach((t,idx)=>{
    const emp=empresaCarpeta(t);
    (t.items||[]).forEach(it=>{
      const kRaw=normArt(it.descripcion); if(!kRaw) return;
      const k=canonArt(kRaw), desc=(it.descripcion||'').trim();
      nombresRaw[kRaw]=nombresRaw[kRaw]||desc;
      const precio=num(it.precio_unitario), cant=num(it.cantidad);
      const clave=emp+'\u0001'+k;
      let a=mapa.get(clave);
      if(!a){ a={empresa:emp, clave:k, nombre:desc, variantes:new Map(), regs:[]}; mapa.set(clave,a); }
      a.variantes.set(desc,(a.variantes.get(desc)||0)+1);
      a.regs.push({doc:t, idx:idx, fecha:fechaCompleta(t.fecha), guardado:(t.fecha_guardado||''), fechaTxt:t.fecha||'—',
        numero:t.numero||'—', cant:cant, precio:precio, subtotal:num(it.subtotal)||precio*cant});
    });
  });
  const fk=r=>r.fecha||r.guardado.slice(0,10)||'';
  mapa.forEach(a=>{
    a.regs.sort((x,y)=>fk(x).localeCompare(fk(y))||x.guardado.localeCompare(y.guardado));
    let best=0; a.variantes.forEach((n,d)=>{ if(n>best){ best=n; a.nombre=d; } });   // nombre más frecuente
  });
  window._artNombres=nombresRaw;
  return mapa;
}
// Estadísticas de un artículo en una empresa. La variación se mide SIEMPRE contra
// el primer registro histórico (aunque los filtros recorten lo que se muestra).
//   varTotal = (último - primero) / primero * 100
//   varUltima = (último - anterior) / anterior * 100
function estadisticasArticulo(a, per){
  const conPrecio=a.regs.filter(r=>r.precio>0), pp=per.filter(r=>r.precio>0);
  const primero=conPrecio[0]||null, ultimo=pp.length?pp[pp.length-1]:null;
  const i=ultimo?conPrecio.indexOf(ultimo):-1, previo=i>0?conPrecio[i-1]:null;
  const precios=pp.map(r=>r.precio);
  return {
    n:a.regs.length, primero, ultimo,
    varTotal:(conPrecio.length>=2&&primero&&ultimo)?pctVar(ultimo.precio,primero.precio):null,
    varUltima:(ultimo&&previo)?pctVar(ultimo.precio,previo.precio):null,
    min:precios.length?Math.min(...precios):0, max:precios.length?Math.max(...precios):0,
    media:precios.length?precios.reduce((s,v)=>s+v,0)/precios.length:0,
    cant:pp.reduce((s,r)=>s+r.cant,0), gasto:pp.reduce((s,r)=>s+r.subtotal,0)
  };
}
// Valor de una serie de registros según ""Comparar por"" (último, media, mínimo, primero).
function valorModo(regs){
  const p=regs.filter(r=>r.precio>0).map(r=>r.precio); if(!p.length) return null;
  const m=$id('artModo').value;
  return m==='media'?p.reduce((s,v)=>s+v,0)/p.length : m==='min'?Math.min(...p) : m==='primero'?p[0] : p[p.length-1];
}
// Comparación del MISMO artículo entre TODAS las empresas:
//   mejor = menor valor · pct = (precio - mejor) / mejor * 100
//   ahorro = (precio - mejor) * cantidad comprada
function cmpArticulo(claveArt){
  const filas=[];
  (window._artMapa||new Map()).forEach(o=>{
    if(o.clave!==claveArt) return;
    const v=valorModo(o.regs); if(v===null) return;
    filas.push({emp:o.empresa, v:v, a:o, st:estadisticasArticulo(o,o.regs)});
  });
  if(!filas.length) return null;
  const mej=filas.reduce((a,b)=>a.v<b.v?a:b);
  filas.forEach(f=>{ f.pct=f.emp===mej.emp?0:pctVar(f.v,mej.v); f.ahorro=(f.v-mej.v)*f.st.cant; f.esMejor=filas.length>1&&f.emp===mej.emp; });
  return {filas, mej, total:filas.reduce((s,f)=>s+f.ahorro,0), multi:filas.length>1, nombre:filas[0].a.nombre};
}

// Artículos que cumplen buscador + filtros de documento + filtros propios de la lista.
function articulosFiltrados(sinFoco){
  const mapa=construirArticulos(); window._artMapa=mapa;
  const setT=new Set(listaFiltrada);
  const terminos=($id('buscar').value||'').split(',').map(normArt).filter(Boolean);
  const fVar=$id('artVar').value, fPct=parseFloat($id('artPct').value)||0, fMin=parseInt($id('artMin').value,10)||1;
  let out=[];
  mapa.forEach(a=>{
    const per=a.regs.filter(r=>setT.has(r.doc));
    if(!per.length || a.regs.length<fMin) return;
    if(terminos.length && !terminos.some(q=>a.clave.includes(q)||normArt(a.empresa).includes(q))) return;
    const st=estadisticasArticulo(a,per), v=st.varTotal;
    if(!st.ultimo) return;
    if(fVar==='sube' && !(v!==null&&v>=0.05)) return;
    if(fVar==='baja' && !(v!==null&&v<=-0.05)) return;
    if(fVar==='igual' && !(v!==null&&Math.abs(v)<0.05)) return;
    if(fPct>0 && !(v!==null&&Math.abs(v)>=fPct)) return;
    const c=cmpArticulo(a.clave), f=c?c.filas.find(z=>z.emp===a.empresa):null;
    out.push({a,st,per,c,f,ahorro:f?Math.max(f.ahorro,0):0});
  });
  if(!sinFoco){
    if(artFoco==='sube') out=out.filter(x=>x.st.varTotal!==null&&x.st.varTotal>=0.05);
    else if(artFoco==='baja') out=out.filter(x=>x.st.varTotal!==null&&x.st.varTotal<=-0.05);
    else if(artFoco==='ahorro') out=out.filter(x=>x.ahorro>0.004);
  }
  const ult=x=>x.st.ultimo?(x.st.ultimo.fecha||x.st.ultimo.guardado.slice(0,10)):'';
  const cmp={
    alfa:(x,y)=>x.a.nombre.localeCompare(y.a.nombre,'es')||x.a.empresa.localeCompare(y.a.empresa),
    sube:(x,y)=>(y.st.varTotal===null?-1e9:y.st.varTotal)-(x.st.varTotal===null?-1e9:x.st.varTotal),
    baja:(x,y)=>(x.st.varTotal===null?1e9:x.st.varTotal)-(y.st.varTotal===null?1e9:y.st.varTotal),
    regs:(x,y)=>y.a.regs.length-x.a.regs.length,
    gasto:(x,y)=>y.st.gasto-x.st.gasto,
    ahorro:(x,y)=>y.ahorro-x.ahorro,
    reciente:(x,y)=>ult(y).localeCompare(ult(x))
  }[$id('artOrden').value]||((x,y)=>0);
  return out.sort(cmp);
}

/* ─── Gráficos SVG (sin dependencias) ─── */
function sparkSvg(vals,w,h){
  w=w||90; h=h||26;
  if(vals.length<2) return `<svg width=""${w}"" height=""${h}""><circle cx=""${w/2}"" cy=""${h/2}"" r=""3"" fill=""#9aa0a6""/></svg>`;
  const mn=Math.min(...vals), mx=Math.max(...vals), r=(mx-mn)||1;
  const pts=vals.map((v,i)=>[2+i/(vals.length-1)*(w-4), h-3-(v-mn)/r*(h-6)]);
  const col=vals[vals.length-1]>vals[0]*1.0005?'#c5221f':vals[vals.length-1]<vals[0]*0.9995?'#137333':'#5f6368';
  const l=pts[pts.length-1];
  return `<svg width=""${w}"" height=""${h}""><polyline fill=""none"" stroke=""${col}"" stroke-width=""1.8"" points=""${pts.map(p=>p.join(',')).join(' ')}""/><circle cx=""${l[0]}"" cy=""${l[1]}"" r=""2.6"" fill=""${col}""/></svg>`;
}
// Mini-gráfica de la galería: área + etiquetas del primer y último precio.
function miniSvg(regs){
  const w=260, h=86, v=regs.filter(r=>r.precio>0).map(r=>r.precio);
  if(v.length<2) return `<svg viewBox=""0 0 ${w} ${h}""><text x=""8"" y=""40"" font-size=""11"" fill=""#80868b"">Un solo registro</text></svg>`;
  const mn=Math.min(...v)*0.97, mx=Math.max(...v)*1.03, r=(mx-mn)||1, L=8, R=8, T=14, B=8;
  const X=i=>L+i/(v.length-1)*(w-L-R), Y=p=>h-B-(p-mn)/r*(h-T-B);
  const col=v[v.length-1]>v[0]*1.0005?'#c5221f':v[v.length-1]<v[0]*0.9995?'#137333':'#5f6368';
  const pts=v.map((p,i)=>X(i)+','+Y(p)).join(' ');
  return `<svg viewBox=""0 0 ${w} ${h}""><polygon points=""${X(0)},${h-B} ${pts} ${X(v.length-1)},${h-B}"" fill=""${col}"" opacity="".12""/>
    <polyline fill=""none"" stroke=""${col}"" stroke-width=""2"" points=""${pts}""/>
    <circle cx=""${X(0)}"" cy=""${Y(v[0])}"" r=""3"" fill=""#80868b""/><circle cx=""${X(v.length-1)}"" cy=""${Y(v[v.length-1])}"" r=""3.4"" fill=""${col}""/>
    <text x=""${X(0)}"" y=""${Y(v[0])-6}"" font-size=""10"" fill=""#5f6368"">${n2(v[0])}</text>
    <text x=""${X(v.length-1)}"" y=""${Y(v[v.length-1])-6}"" text-anchor=""end"" font-size=""10"" font-weight=""700"" fill=""${col}"">${n2(v[v.length-1])}</text></svg>`;
}
// Gráfica grande multi-serie. series=[{name,col,pts:[{t,v,f}]}]; hl = empresa resaltada.
function chartSvg(series,hl,w,h){
  w=w||640; h=h||240;
  const all=series.flatMap(s=>s.pts); if(!all.length) return '';
  const ts=all.map(p=>p.t), mnT=Math.min(...ts), mxT=Math.max(...ts)||mnT+1;
  const pr=all.map(p=>p.v), mnP=Math.min(...pr)*0.95, mxP=Math.max(...pr)*1.05, rp=(mxP-mnP)||1, rt=(mxT-mnT)||1;
  const L=46, B=24, T=10, R=12, X=t=>L+(t-mnT)/rt*(w-L-R), Y=v=>h-B-(v-mnP)/rp*(h-B-T);
  let g='';
  for(let i=0;i<=4;i++){ const v=mnP+rp*i/4, y=Y(v); g+=`<line x1=""${L}"" x2=""${w-R}"" y1=""${y}"" y2=""${y}"" stroke=""#eee""/><text x=""${L-6}"" y=""${y+3}"" text-anchor=""end"" font-size=""10"" fill=""#80868b"">${v.toFixed(2)}</text>`; }
  for(let i=0;i<=4;i++){ const t=mnT+rt*i/4, d=new Date(t); g+=`<text x=""${X(t)}"" y=""${h-6}"" text-anchor=""middle"" font-size=""10"" fill=""#80868b"">${String(d.getMonth()+1).padStart(2,'0')}/${String(d.getFullYear()).slice(2)}</text>`; }
  series.forEach(s=>{
    const sel=!hl||s.name===hl;
    if(s.pts.length>1) g+=`<polyline fill=""none"" stroke=""${s.col}"" stroke-width=""${sel?2.8:1.4}"" opacity=""${sel?1:.45}"" points=""${s.pts.map(p=>X(p.t)+','+Y(p.v)).join(' ')}""/>`;
    s.pts.forEach(p=>{ g+=`<circle cx=""${X(p.t)}"" cy=""${Y(p.v)}"" r=""${sel?3.8:2.6}"" fill=""${s.col}"" opacity=""${sel?1:.45}""><title>${escH(s.name)} · ${p.f} · ${eur(p.v)}</title></circle>`; });
  });
  return `<svg viewBox=""0 0 ${w} ${h}"" style=""width:100%;height:auto;max-height:270px"">${g}</svg>`;
}
function colEmpArt(e){ const l=window._artEmpresas||[]; const i=l.indexOf(e); return COL_ART[(i<0?0:i)%COL_ART.length]; }
const serieDe=o=>({name:o.empresa,col:colEmpArt(o.empresa),pts:o.regs.filter(r=>r.precio>0).map(r=>({t:new Date(r.fecha||r.guardado.slice(0,10)||'2000-01-01').getTime(),v:r.precio,f:r.fechaTxt}))});

/* ─── Interruptores y KPIs clicables ─── */
function setArtVista(v){ artVista=v; filtrar(); }
function artKpi(k){ artFoco=(artFoco===k&&k!=='todos')?'':k; filtrar(); }
function artCerrarGaleria(){ artFoco=''; filtrar(); }
// Chip ▲/▼ de la cabecera de empresa: filtra por esa empresa y por dirección.
function artChip(emp,vr){
  const se=$id('filtroEmpresa'); if([...se.options].some(o=>o.value===emp)) se.value=emp;
  $id('artVar').value=vr; filtrar();
}
function artColEmp(emp){
  const se=$id('filtroEmpresa'); if(![...se.options].some(o=>o.value===emp)) return;
  se.value = se.value===emp ? '' : emp; filtrar();
}
const TIT_ART={todos:'Todos los artículos',sube:'Artículos que han SUBIDO ▲',baja:'Artículos que han BAJADO ▼',gasto:'Mayor gasto',ahorro:'Dónde se puede ahorrar (comprar al más barato)'};
function kpisArt(L0){
  const su=L0.filter(x=>x.st.varTotal!==null&&x.st.varTotal>=0.05), ba=L0.filter(x=>x.st.varTotal!==null&&x.st.varTotal<=-0.05);
  const ms=[...su].sort((x,y)=>y.st.varTotal-x.st.varTotal)[0], mb=[...ba].sort((x,y)=>x.st.varTotal-y.st.varTotal)[0];
  const claves=[...new Set(L0.map(x=>x.a.clave))];
  const aho=claves.reduce((s,k)=>{ const c=cmpArticulo(k); return s+(c&&c.multi?c.total:0); },0);
  const k=(key,l,v,s)=>`<div class=""art-kpi click${artFoco===key?' on':''}"" onclick=""artKpi('${key}')"" title=""Clic para ver las gráficas""><span>${l}</span><b>${v}</b>${s?`<small>${s}</small>`:''}</div>`;
  const op=x=>x?`data-ac=""open"" data-clave=""${encA(x.a.clave)}"" data-emp=""${encA(x.a.empresa)}""`:'';
  return `<div class=""art-kpis"">${k('todos','Artículos',L0.length,'ver todas las gráficas')}${k('sube','Han subido ▲',su.length,'ver gráficas')}${k('baja','Han bajado ▼',ba.length,'ver gráficas')}
    <div class=""art-kpi click"" ${op(ms)}><span>Mayor subida</span><b>${ms?fmtVar(ms.st.varTotal):'—'}</b>${ms?`<small>${escH(ms.a.nombre)} · ${escH(ms.a.empresa)}</small>`:''}</div>
    <div class=""art-kpi click"" ${op(mb)}><span>Mayor bajada</span><b>${mb?fmtVar(mb.st.varTotal):'—'}</b>${mb?`<small>${escH(mb.a.nombre)} · ${escH(mb.a.empresa)}</small>`:''}</div>
    ${k('gasto','Gasto en artículos',eur(L0.reduce((s,x)=>s+x.st.gasto,0)),'ver los de mayor gasto')}
    ${k('ahorro','Ahorro potencial',`<span style=""color:#137333"">${eur(aho)}</span>`,'ver dónde se ahorra')}</div>`;
}

/* ─── Galería de gráficas (al pulsar un KPI) ─── */
function galeriaArt(L0){
  if(!artFoco) return '';
  let html='', n=0;
  if(artFoco==='ahorro'){
    const arts=[...new Set(L0.map(x=>x.a.clave))].map(k=>({k,c:cmpArticulo(k)})).filter(x=>x.c&&x.c.multi&&x.c.total>0.004).sort((a,b)=>b.c.total-a.c.total);
    n=arts.length;
    html=arts.map(({k,c})=>`<div class=""art-gc"" data-ac=""open"" data-clave=""${encA(k)}"" data-emp=""${encA(c.mej.emp)}"">
      <div class=""t""><b>${escH(c.nombre)}</b><span class=""pill mej"">ahorro ${eur(c.total)}</span></div>
      <small>🏆 más barato: ${escH(c.mej.emp)} (${eur(c.mej.v)})</small>${chartSvg(c.filas.map(f=>serieDe(f.a)),null,300,120)}</div>`).join('');
  } else {
    let G=[...L0];
    if(artFoco==='sube') G=G.filter(x=>x.st.varTotal!==null&&x.st.varTotal>=0.05).sort((a,b)=>b.st.varTotal-a.st.varTotal);
    else if(artFoco==='baja') G=G.filter(x=>x.st.varTotal!==null&&x.st.varTotal<=-0.05).sort((a,b)=>a.st.varTotal-b.st.varTotal);
    else if(artFoco==='gasto') G=G.sort((a,b)=>b.st.gasto-a.st.gasto).slice(0,12);
    n=G.length;
    html=G.map(x=>`<div class=""art-gc"" data-ac=""open"" data-clave=""${encA(x.a.clave)}"" data-emp=""${encA(x.a.empresa)}"">
      <div class=""t""><b>${escH(x.a.nombre)}</b><span class=""pill ${clsVar(x.st.varTotal)}"">${x.st.varTotal===null?'—':(x.st.varTotal>0?'+':'')+x.st.varTotal.toFixed(1)+'%'}</span></div>
      <small>${escH(x.a.empresa)} · ${x.st.n} registros</small>${miniSvg(x.a.regs)}
      <div class=""pie""><span>1º ${x.st.primero?eur(x.st.primero.precio):'—'}</span><span>${artFoco==='gasto'?'Gasto '+eur(x.st.gasto):'último '+eur(x.st.ultimo.precio)}</span></div></div>`).join('');
  }
  return `<div class=""art-foco""><b>${TIT_ART[artFoco]}</b><span>${n} gráfica(s) · clic en cualquiera para ver el detalle</span><button onclick=""artCerrarGaleria()"">✕ Cerrar galería</button></div>
    <div class=""art-gal"">${html||'<div id=""vacio"" style=""grid-column:1/-1"">Sin resultados</div>'}</div>`;
}

/* ─── Detalle de un artículo: gráfica comparada, histórico y fórmulas ─── */
function detalleArtHTML(clave,empSel){
  const c=cmpArticulo(clave); if(!c) return '';
  const fSel=c.filas.find(f=>f.emp===empSel)||c.filas[0];
  const ser=c.filas.filter(f=>!artOcultas.has(f.emp)).map(f=>serieDe(f.a));
  const todos=c.filas.flatMap(f=>f.a.regs.filter(r=>r.precio>0).map((r,i,arr)=>({r,i,arr,emp:f.emp})))
    .sort((a,b)=>(b.r.fecha||b.r.guardado).localeCompare(a.r.fecha||a.r.guardado)).slice(0,20);
  const modoTxt={ultimo:'último precio',media:'precio medio',min:'mínimo histórico',primero:'primer precio'}[$id('artModo').value];
  const dk=c.filas.map(f=>`<div class=""art-kpi click"" style=""${f.emp===fSel.emp?'border-color:'+colEmpArt(f.emp):''}"" data-ac=""selEmp"" data-emp=""${encA(f.emp)}"" data-clave=""${encA(clave)}"">
    <span><i class=""sw"" style=""background:${colEmpArt(f.emp)}""></i> ${escH(f.emp)}</span><b>${f.st.ultimo?eur(f.st.ultimo.precio):'—'}</b> ${fmtVar(f.st.varTotal)}
    <small>${f.st.n} regs · gasto ${eur(f.st.gasto)}</small>${f.esMejor?'<span class=""pill mej"">🏆 más barato</span>':(f.pct?`<span class=""pill sube"">+${f.pct.toFixed(1)}% vs mejor</span>`:'')}</div>`).join('');
  const leg=c.filas.map(f=>`<label><input type=""checkbox"" data-ac=""leg"" data-emp=""${encA(f.emp)}"" ${artOcultas.has(f.emp)?'':'checked'}><span class=""sw"" style=""background:${colEmpArt(f.emp)}""></span>${escH(f.emp)}</label>`).join('');
  const hist=todos.map(x=>`<div class=""art-reg"" data-ac=""doc"" data-idx=""${x.r.idx}"" title=""Abrir el documento"">
    <span>${escH(x.r.fechaTxt)}</span><span>${escH(x.emp)}</span><span>${x.r.cant}</span><span>${eur(x.r.precio)}</span>
    <span>${x.i?fmtVar(pctVar(x.r.precio,x.arr[x.i-1].precio)):'—'}</span><span>${x.i?fmtVar(pctVar(x.r.precio,x.arr[0].precio)):'—'}</span></div>`).join('');
  const ln=t=>`<div class=""l"">${t}</div>`;
  const formula=`<div class=""art-formula""><b>🧮 Cómo se calcula (con los datos de este artículo)</b>
    ${ln('Variación desde el 1º = <code>(último − primero) / primero × 100</code>')}
    ${c.filas.map(f=>ln(`· ${escH(f.emp)}: (${f.st.ultimo?n2(f.st.ultimo.precio):'—'} − ${f.st.primero?n2(f.st.primero.precio):'—'}) / ${f.st.primero?n2(f.st.primero.precio):'—'} = ${fmtVar(f.st.varTotal)}`)).join('')}
    ${ln(`<br>Comparación (${modoTxt}) = <code>(precio − mejor) / mejor × 100</code> · mejor = ${eur(c.mej.v)} (${escH(c.mej.emp)})`)}
    ${c.filas.map(f=>ln(`· ${escH(f.emp)}: (${n2(f.v)} − ${n2(c.mej.v)}) / ${n2(c.mej.v)} = ${f.emp===c.mej.emp?'<span class=""var baja"">mejor</span>':fmtVar(f.pct)}`)).join('')}
    ${ln('<br>Ahorro potencial = <code>(precio − mejor) × cantidad comprada</code>')}
    ${c.filas.map(f=>ln(`· ${escH(f.emp)}: (${n2(f.v)} − ${n2(c.mej.v)}) × ${f.st.cant} = ${eur(f.ahorro)}`)).join('')}
    ${ln(`<b>Total: ${eur(c.total)}</b>`)}</div>`;
  const vars=c.filas.filter(f=>f.a.variantes&&f.a.variantes.size>1).map(f=>`${escH(f.emp)}: `+[...f.a.variantes.entries()].map(([d,n])=>escH(d)+' ('+n+')').join(' · '));
  const lineaVar=vars.length?`<div class=""art-nota"">🔗 Nombres agrupados en este artículo — ${vars.join(' &nbsp;|&nbsp; ')} <span class=""lk"" onclick=""abrirFus('act')"">Gestionar fusiones</span></div>`:'';
  return `<div class=""art-dk"">${dk}</div>${lineaVar}<div class=""art-leg"">${leg}</div>${chartSvg(ser,fSel.emp)}
    <div class=""art-dos""><div><b style=""font-size:.85em"">Histórico de compras</b><div class=""art-reglst""><div class=""art-reg h""><span>Fecha</span><span>Empresa</span><span>Cant.</span><span>Precio</span><span>vs ant.</span><span>vs 1º</span></div>${hist}</div></div>${formula}</div>`;
}
function pintarArt(){
  const c=cmpArticulo(artSel.clave);
  $id('artT').textContent=c?c.nombre:'';
  $id('artB').innerHTML=detalleArtHTML(artSel.clave,artSel.emp);
}
function abrirArt(clave,emp){
  artSel={clave,emp};
  artNav=[...new Set(articulosFiltrados(true).map(x=>x.a.clave))];
  if(!artNav.includes(clave)) artNav.push(clave);
  artNavPos=artNav.indexOf(clave);
  pintarArt(); $id('modal-art').classList.add('activo');
}
function cerrarArt(){ $id('modal-art').classList.remove('activo'); }
function navArt(d){
  if(!artNav.length) return;
  artNavPos=(artNavPos+d+artNav.length)%artNav.length;
  artSel={clave:artNav[artNavPos],emp:null}; pintarArt();
}
// Un único manejador delegado para todo lo clicable (evita problemas de comillas en nombres).
document.addEventListener('click',function(e){
  const t=e.target.closest('[data-ac]'); if(!t) return;
  const a=t.dataset.ac, clave=t.dataset.clave?decA(t.dataset.clave):null, emp=t.dataset.emp?decA(t.dataset.emp):null;
  if(a==='open'){ abrirArt(clave,emp); }
  else if(a==='selEmp'){ artSel={clave:clave||artSel.clave,emp:emp}; pintarArt(); }
  else if(a==='chip'){ artChip(emp,t.dataset.vr); }
  else if(a==='colEmp'){ artColEmp(emp); }
  else if(a==='doc'){ abrirModal(parseInt(t.dataset.idx,10)); }
});
document.addEventListener('change',function(e){
  const t=e.target; if(!t.dataset||t.dataset.ac!=='leg') return;
  const n=decA(t.dataset.emp); t.checked?artOcultas.delete(n):artOcultas.add(n); pintarArt();
});
document.addEventListener('keydown',function(e){
  if(!$id('modal-art').classList.contains('activo')) return;
  if($id('modal').classList.contains('activo')) return;   // el modal de documento tiene prioridad
  if($id('modal-fus').classList.contains('activo')) return; // y el de fusiones
  if(e.key==='Escape') cerrarArt();
  if(e.key==='ArrowLeft') navArt(-1);
  if(e.key==='ArrowRight') navArt(1);
});

// 🔗 N: el artículo agrupa varios nombres originales (tooltip con la lista y cuántas veces aparece cada uno).
function insigniaVariantes(a){
  if(!a.variantes||a.variantes.size<2) return '';
  const l=[...a.variantes.entries()].sort((x,y)=>y[1]-x[1]).map(([d,n])=>d+' ('+n+')').join(' · ');
  return ` <span class=""pill igual"" title=""${escH('Nombres agrupados: '+l)}"">🔗 ${a.variantes.size}</span>`;
}
/* ─── Vista TABLA (A): por empresa, con el comparador integrado ─── */
function tablaArt(L){
  const g={}; L.forEach(x=>(g[x.a.empresa]=g[x.a.empresa]||[]).push(x));
  return Object.keys(g).sort().map(emp=>{
    const G=g[emp], su=G.filter(x=>x.st.varTotal>=0.05).length, ba=G.filter(x=>x.st.varTotal<=-0.05).length;
    const mx=Math.max(...G.map(x=>Math.abs(x.st.varTotal||0)),1);
    const filas=G.map(x=>{
      const st=x.st, c=x.c, f=x.f, k=clsVar(st.varTotal), w=Math.round(Math.abs(st.varTotal||0)/mx*40);
      const ac=`data-ac=""open"" data-clave=""${encA(x.a.clave)}"" data-emp=""${encA(x.a.empresa)}""`;
      const vs=!c||!c.multi?'<span class=""var igual"">solo aquí</span>':f.esMejor?'<span class=""pill mej"">🏆 mejor precio</span>':fmtVar(f.pct);
      const barato=!c||!c.multi||f.esMejor?'—':`<span class=""lk"" data-ac=""open"" data-clave=""${encA(x.a.clave)}"" data-emp=""${encA(c.mej.emp)}"">${escH(c.mej.emp)} · ${eur(c.mej.v)}</span>`;
      return `<tr class=""art-fila${artSel.clave===x.a.clave&&artSel.emp===x.a.empresa?' sel':''}"" ${ac}>
        <td><b>${escH(x.a.nombre)}</b>${insigniaVariantes(x.a)}</td><td class=""n"">${st.n}</td><td class=""n"">${st.primero?eur(st.primero.precio):'—'}</td>
        <td class=""n""><b>${eur(st.ultimo.precio)}</b>${fmtVar(st.varTotal)}</td>
        <td>${sparkSvg(x.a.regs.filter(r=>r.precio>0).map(r=>r.precio))}<span class=""heatb"" style=""width:${w}px;background:${k==='sube'?'#c5221f':k==='baja'?'#137333':'#bbb'}""></span></td>
        <td class=""n"">${vs}</td><td>${barato}</td>
        <td class=""n"">${x.ahorro>0.004?`<b style=""color:#137333"">${eur(x.ahorro)}</b>`:'—'}</td><td class=""n"">${eur(st.gasto)}</td></tr>`;
    }).join('');
    return `<div class=""empresa-grupo""><div class=""empresa-cab"">🏢 ${escH(emp)}<span class=""count"">${G.length}</span>
      <span class=""suma""><span class=""chipv sube"" data-ac=""chip"" data-emp=""${encA(emp)}"" data-vr=""sube"" title=""Filtrar: solo los que suben en esta empresa"">▲ ${su}</span>
      <span class=""chipv baja"" data-ac=""chip"" data-emp=""${encA(emp)}"" data-vr=""baja"" title=""Filtrar: solo los que bajan en esta empresa"">▼ ${ba}</span>
      <span>${eur(G.reduce((s,x)=>s+x.st.gasto,0))}</span></span></div>
      <div class=""wrapx""><table class=""arts""><thead><tr><th>Artículo</th><th class=""n"">Regs.</th><th class=""n"">1er precio</th><th class=""n"" title=""Último precio y variación desde el primer registro"">Último (var. desde 1º)</th><th>Evolución</th><th class=""n"">vs mejor precio</th><th>Más barato en</th><th class=""n"">Ahorro pot.</th><th class=""n"">Gasto</th></tr></thead><tbody>${filas}</tbody></table></div></div>`;
  }).join('')+'<div class=""art-nota"">Clic en una fila → detalle con gráfica comparada, histórico y fórmulas · clic en ▲/▼ de la empresa → filtra · clic en ""Más barato en"" → abre esa empresa.</div>';
}

/* ─── Vista MATRIZ (B): artículo × empresa ─── */
function matrizArt(L){
  const claves=[...new Set(L.map(x=>x.a.clave))];
  const nombres={}; L.forEach(x=>nombres[x.a.clave]=x.a.nombre);
  claves.sort((a,b)=>nombres[a].localeCompare(nombres[b],'es'));
  const se=$id('filtroEmpresa').value;
  const emps=(se?[se]:(window._artEmpresas||[])).filter(e=>L.some(x=>x.a.empresa===e));
  const idx={}; L.forEach(x=>idx[x.a.empresa+'|'+x.a.clave]=x);
  const gan={}; emps.forEach(e=>gan[e]=0); let aho=0;
  const filas=claves.map(k=>{ const c=cmpArticulo(k); if(c&&c.multi){ aho+=c.total; c.filas.forEach(f=>{ if(f.esMejor&&gan[f.emp]!==undefined) gan[f.emp]++; }); } return {k,c}; });
  const hdr=emps.map(e=>`<th class=""n clk${se===e?' sel':''}"" data-ac=""colEmp"" data-emp=""${encA(e)}"" title=""Clic: ver solo ${escH(e)}"">${escH(e)}</th>`).join('');
  const body=filas.map(({k,c})=>`<tr><td><span class=""lk"" data-ac=""open"" data-clave=""${encA(k)}"" data-emp=""${encA(c.mej.emp)}""><b>${escH(nombres[k])}</b></span></td>${emps.map(e=>{
      const x=idx[e+'|'+k], f=c?c.filas.find(z=>z.emp===e):null;
      if(!x||!f) return '<td class=""cel vacio"">—</td>';
      const sel=artSel.clave===k&&artSel.emp===e;
      return `<td class=""cel${f.esMejor?' mej':''}${sel?' sel':''}"" data-ac=""open"" data-clave=""${encA(k)}"" data-emp=""${encA(e)}"">
        <div class=""p"">${f.esMejor?'🏆 ':''}${eur(f.v)}</div>
        <div class=""d"">${f.esMejor?'<span class=""var baja"">mejor</span>':(c.multi?fmtVar(f.pct):'')} ${fmtVar(x.st.varTotal)}</div>${sparkSvg(x.a.regs.filter(r=>r.precio>0).map(r=>r.precio),70,18)}</td>`;
    }).join('')}<td class=""n"">${c&&c.multi&&c.total>0.004?`<b style=""color:#137333"">${eur(c.total)}</b>`:'—'}</td></tr>`).join('');
  const top=Object.entries(gan).sort((a,b)=>b[1]-a[1])[0];
  return `<div class=""art-ahorro""><span>💡 Ahorro potencial comprando siempre al más barato:</span><b>${eur(aho)}</b>
      <span>🏆 Más competitiva: <b>${top&&top[1]?escH(top[0]):'—'}</b></span></div>
    <div class=""wrapx"" style=""border-radius:10px""><table class=""arts""><thead><tr><th>Artículo</th>${hdr}<th class=""n"">Ahorro potencial</th></tr></thead><tbody>${body}</tbody>
    <tfoot><tr><td>Artículos más baratos</td>${emps.map(e=>`<td class=""n"">${gan[e]}</td>`).join('')}<td class=""n"">${eur(aho)}</td></tr></tfoot></table></div>
    <div class=""art-nota"">Clic en una celda → detalle · clic en el nombre → detalle del más barato · clic en la cabecera de empresa → filtra · % rojo = más caro que el mejor · 2º % = variación desde su 1er registro.</div>`;
}

function renderArticulos(lista,c){
  const L0=articulosFiltrados(true), L=articulosFiltrados(false);
  window._artEmpresas=[...new Set([...(window._artMapa||new Map()).values()].map(o=>o.empresa))].sort();
  $id('contador').textContent=L.length+' artículo(s)'+(artFoco?' · foco: '+artFoco:'');
  $id('btnArtTabla').classList.toggle('on',artVista==='tabla'); $id('btnArtMatriz').classList.toggle('on',artVista==='matriz');
  if(!L0.length){ c.innerHTML='<div id=""vacio"">No se encontraron artículos con estos filtros.</div>'; actualizarBadgeFus(); return; }
  c.innerHTML=kpisArt(L0)+galeriaArt(L0)+(L.length?(artVista==='matriz'?matrizArt(L):tablaArt(L)):'<div id=""vacio"">Sin artículos con el foco actual.</div>');
  actualizarBadgeFus();
}


/* ═══════════════════════════════════════════════════════════════════════
   Fusión de nombres de artículos (el usuario decide; se recuerda en localStorage)
   - Nunca se funde automáticamente: solo se SUGIERE y se pide ✔ / ✖.
   - Color, volumen, peso, medidas, packs y códigos distintos => no se sugiere.
   ═══════════════════════════════════════════════════════════════════════ */
const CLAVE_FUS='facticket_fusiones_v1';
let fusiones={alias:{}, rechazadas:[]};   // alias: claveOrigen -> claveDestino
let fusUmbral=75;                          // similitud mínima (%) de la lista ""revisar""
let fusTab='sug';
let fusCache=null;
// Palabras que se ignoran al comparar nombres (artículos, preposiciones y envases genéricos).
const STOP_ART=new Set(['de','del','la','el','los','las','y','con','para','en','un','una','por']);
const ENVASE_ART=new Set(['saco','sacos','bote','botes','caja','cajas','paquete','paquetes','pack','packs','unidad','unidades','ud','uds','botella','botellas','lata','latas','bolsa','bolsas','envase','garrafa']);
const COLORES_ART=['blanco','negro','rojo','azul','verde','amarillo','gris','marron','naranja','rosa','beige','transparente','plata','dorado','cromado','morado','violeta','crema','antracita'];
// Grupos de sinónimos conocidos (editable): sus palabras se tratan como la misma.
const SINONIMOS_ART=[['gasoil','gasoleo']];

function leerFusiones(){
  try{ const o=JSON.parse(localStorage.getItem(CLAVE_FUS)||'null'); if(o&&o.alias) fusiones={alias:o.alias,rechazadas:o.rechazadas||[]}; }catch(e){}
}
function guardarFusiones(){ try{ localStorage.setItem(CLAVE_FUS,JSON.stringify(fusiones)); }catch(e){} fusCache=null; }
// Clave canónica: sigue la cadena de alias (con tope por seguridad).
function canonArt(k){ let i=0; while(fusiones.alias[k] && i++<30) k=fusiones.alias[k]; return k; }
const parKey=(a,b)=>a<b?a+'\u0001'+b:b+'\u0001'+a;

// Plural simple: tornillos -> tornillo.
function stemA(w){ return (w.length>3&&w.endsWith('s'))?w.slice(0,-1):w; }
function colorCanon(w){
  let x=w.length>3&&w.endsWith('s')?w.slice(0,-1):w;
  if(COLORES_ART.includes(x)) return x;
  if(x.endsWith('a')&&COLORES_ART.includes(x.slice(0,-1)+'o')) return x.slice(0,-1)+'o';
  return null;
}
function sinCanon(w){ for(const g of SINONIMOS_ART) if(g.includes(w)) return g[0]; return w; }
// Unidades a una base común: kg->g, l/cl->ml, cm/m->mm, ud.
function uniMed(v,u){
  v=parseFloat(v); u=u.toLowerCase();
  if(u==='kg') return Math.round(v*1000)+'g';
  if(/^(g|gr|grs)$/.test(u)) return Math.round(v)+'g';
  if(/^(l|lt|lts|litro|litros)$/.test(u)) return Math.round(v*1000)+'ml';
  if(u==='cl') return Math.round(v*10)+'ml';
  if(u==='ml') return Math.round(v)+'ml';
  if(u==='cm') return Math.round(v*100)/10+'mm';
  if(u==='mm') return Math.round(v*100)/100+'mm';
  if(/^(m|mts|metro|metros)$/.test(u)) return Math.round(v*1000)+'mm';
  if(/^(ud|uds|unidad|unidades)$/.test(u)) return Math.round(v)+'ud';
  return v+u;
}
// Descompone un nombre ya normalizado (normArt) en: palabras, colores, medidas y códigos.
function parseArt(norm){
  let t=' '+norm.replace(/(\d),(\d)/g,'$1.$2')+' ';
  const med=new Set(), cod=new Set(), colores=new Set(), words=[];
  const U='kg|g|gr|grs|l|lt|lts|litro|litros|ml|cl|mm|cm|m|mts|metro|metros';
  // Packs: 6x1.5l
  t=t.replace(new RegExp('(\\d+)\\s*[x×]\\s*(\\d+(?:\\.\\d+)?)\\s*('+U+')\\b','g'),(m,n,v,u)=>{ med.add('pack'+n+'x'+uniMed(v,u)); return ' '; });
  // Dimensiones: 5x40 o 10x20x30 (con unidad opcional)
  t=t.replace(/(\d+(?:\.\d+)?)\s*[x×]\s*(\d+(?:\.\d+)?)(?:\s*[x×]\s*(\d+(?:\.\d+)?))?(?:\s*(?:mm|cm))?/g,(m,a,b,c)=>{ med.add('dim'+[a,b,c].filter(Boolean).map(Number).join('x')); return ' '; });
  // Número + unidad: 25kg, 15l, 2.5mm, 9w...
  t=t.replace(new RegExp('(\\d+(?:\\.\\d+)?)\\s*('+U+'|w|v|ud|uds|unidad|unidades)\\b','g'),(m,v,u)=>{ med.add(uniMed(v,u)); return ' '; });
  t.split(/[^a-z0-9]+/).filter(Boolean).forEach(tok=>{
    if(/\d/.test(tok)){ cod.add(tok); return; }
    if(STOP_ART.has(tok)||ENVASE_ART.has(tok)) return;
    const c=colorCanon(tok); if(c){ colores.add(c); return; }
    words.push(stemA(tok));
  });
  return {words,colores,med,cod};
}
function levA(a,b){
  const m=a.length,n=b.length; if(!m) return n; if(!n) return m;
  let prev=Array.from({length:n+1},(_,j)=>j);
  for(let i=1;i<=m;i++){ const cur=[i]; for(let j=1;j<=n;j++) cur[j]=Math.min(prev[j]+1,cur[j-1]+1,prev[j-1]+(a[i-1]===b[j-1]?0:1)); prev=cur; }
  return prev[n];
}
// Dos palabras ""iguales"": idénticas, sinónimas, o muy parecidas (typos) si son largas.
function palabraIgual(a,b){
  if(a===b||sinCanon(a)===sinCanon(b)) return true;
  if(Math.min(a.length,b.length)<5) return false;
  return 1-levA(a,b)/Math.max(a.length,b.length)>=0.82;
}
const igualesSet=(a,b)=>a.size===b.size&&[...a].every(x=>b.has(x));
// Compara dos nombres parseados. Devuelve null si NO deben sugerirse (medidas, códigos o
// colores distintos) o {score, tipo:'seguro'|'revisar', difiere:[...]}.
function compararNombres(pa,pb){
  if(!igualesSet(pa.med,pb.med)) return null;                 // volumen/peso/medida/pack distinto
  if(!igualesSet(pa.cod,pb.cod)) return null;                 // códigos distintos (E27, F10...)
  if(pa.colores.size&&pb.colores.size&&!igualesSet(pa.colores,pb.colores)) return null;  // colores distintos
  if(!pa.words.length||!pb.words.length) return null;
  const ub=[...pb.words], exA=[]; let m=0;
  pa.words.forEach(w=>{ const i=ub.findIndex(x=>palabraIgual(w,x)); if(i>=0){ m++; ub.splice(i,1); } else exA.push(w); });
  if(!m) return null;
  const difColor=[...pa.colores].filter(c=>!pb.colores.has(c)).concat([...pb.colores].filter(c=>!pa.colores.has(c)));
  const difiere=exA.concat(ub,difColor);
  return {score:2*m/(pa.words.length+pb.words.length), tipo:difiere.length?'revisar':'seguro', difiere};
}
// Sugerencias de fusión entre los nombres canónicos actuales (con caché).
function sugerenciasFusion(){
  const mapa=window._artMapa||new Map();
  const sig=mapa.size+'|'+tickets.length+'|'+Object.keys(fusiones.alias).length+'|'+fusiones.rechazadas.length+'|'+fusUmbral;
  if(fusCache&&fusCache.sig===sig) return fusCache.res;
  const nom=new Map();
  mapa.forEach(a=>{
    let o=nom.get(a.clave); if(!o){ o={k:a.clave,nombre:a.nombre,emps:new Map(),n:0}; nom.set(a.clave,o); }
    const pr=a.regs.filter(r=>r.precio>0), u=pr.length?pr[pr.length-1]:null;
    o.emps.set(a.empresa,{precio:u?u.precio:0,n:a.regs.length}); o.n+=a.regs.length;
  });
  const lista=[...nom.values()]; lista.forEach(o=>o.p=parseArt(o.k));
  const buckets=new Map();
  lista.forEach(o=>{ new Set(o.p.words.map(w=>sinCanon(w).slice(0,4))).forEach(b=>{ if(!buckets.has(b)) buckets.set(b,[]); buckets.get(b).push(o); }); });
  const vistos=new Set(), rej=new Set(fusiones.rechazadas), seguras=[], revisar=[];
  buckets.forEach(arr=>{
    if(arr.length>300) return;
    for(let i=0;i<arr.length;i++) for(let j=i+1;j<arr.length;j++){
      const A=arr[i], B=arr[j], pk=parKey(A.k,B.k);
      if(vistos.has(pk)||rej.has(pk)) continue; vistos.add(pk);
      const r=compararNombres(A.p,B.p); if(!r) continue;
      const par=A.n>=B.n?{dest:A,orig:B,r}:{dest:B,orig:A,r};
      if(r.tipo==='seguro') seguras.push(par); else if(r.score*100>=fusUmbral) revisar.push(par);
    }
  });
  const ord=(a,b)=>b.r.score-a.r.score||b.dest.n-a.dest.n;
  const res={seguras:seguras.sort(ord), revisar:revisar.sort(ord), nombres:lista};
  fusCache={sig,res}; return res;
}
// ─── Acciones ───
function fusionarArt(origen,destino){
  const o=canonArt(origen), d=canonArt(destino); if(!o||!d||o===d) return false;
  fusiones.alias[o]=d; guardarFusiones(); return true;
}
function deshacerFusion(origen){ delete fusiones.alias[origen]; guardarFusiones(); }
function rechazarPar(a,b){ const pk=parKey(a,b); if(!fusiones.rechazadas.includes(pk)) fusiones.rechazadas.push(pk); guardarFusiones(); }
function refrescarTrasFusion(){ filtrar(); pintarFus(); if($id('modal-art').classList.contains('activo')){ const ok=cmpArticulo(artSel.clave); if(ok) pintarArt(); else cerrarArt(); } }
function actualizarBadgeFus(){
  const n=sugerenciasFusion().seguras.length, b=$id('fusBadge'); if(!b) return;
  b.textContent=n?n:''; b.style.display=n?'inline-block':'none';
}
// ─── Diálogo ───
function abrirFus(tab){ if(tab) fusTab=tab; $id('modal-fus').classList.add('activo'); pintarFus(); }
function cerrarFus(){ $id('modal-fus').classList.remove('activo'); }
function infoNombre(o){ return [...o.emps.entries()].map(([e,v])=>`${escH(e)}: <b>${v.precio?eur(v.precio):'—'}</b> <small>(${v.n} reg.)</small>`).join(' · '); }
function filaSug(p,seguro){
  const ar=p.orig.nombre, br=p.dest.nombre;
  return `<div class=""fus-fila ${seguro?'':'rev'}"">
    <div class=""fus-nom""><div>«${escH(ar)}»<br><span class=""fus-inf"">${infoNombre(p.orig)}</span></div>
      <div class=""fus-flecha"">↔</div>
      <div>«${escH(br)}»<br><span class=""fus-inf"">${infoNombre(p.dest)}</span></div></div>
    <div class=""fus-pie""><span class=""fus-sim"">Similitud ${Math.round(p.r.score*100)}%</span>
      ${seguro?'<span class=""pill baja"">✅ mismas medidas y color</span>':`<span class=""pill sube"">⚠ difiere: ${escH(p.r.difiere.join(', '))}</span>`}
      <span class=""fus-inf"">Se unificará como «${escH(br)}»</span>
      <button class=""chip fus-si"" data-ac=""fus-si"" data-a=""${encA(p.orig.k)}"" data-b=""${encA(p.dest.k)}"">✔ Fundir</button>
      <button class=""chip"" data-ac=""fus-no"" data-a=""${encA(p.orig.k)}"" data-b=""${encA(p.dest.k)}"">✖ Mantener separados</button></div></div>`;
}
function pintarFus(){
  const s=sugerenciasFusion(), nAl=Object.keys(fusiones.alias).length;
  const tabs=[['sug',`💡 Sugerencias (${s.seguras.length+s.revisar.length})`],['man','✍ Manual'],['act',`🔗 Activas (${nAl})`]];
  let cuerpo='';
  if(fusTab==='sug'){
    cuerpo=`<div class=""fus-ctl""><label>Similitud mínima para «revisar»</label><input type=""number"" min=""40"" max=""100"" step=""5"" value=""${fusUmbral}"" data-ac=""fus-umbral"" style=""width:70px""> %
      <span class=""fus-inf"">Las sugerencias con distinta medida, peso, volumen, pack o color <b>nunca</b> aparecen.</span></div>
      <div class=""fus-tit"">✅ Seguras — mismo producto escrito distinto (${s.seguras.length})</div>
      ${s.seguras.map(p=>filaSug(p,true)).join('')||'<div class=""fus-vacio"">No hay sugerencias seguras.</div>'}
      <div class=""fus-tit"">⚠ Con diferencias — revisa antes de fundir (${s.revisar.length})</div>
      ${s.revisar.map(p=>filaSug(p,false)).join('')||'<div class=""fus-vacio"">Nada que revisar.</div>'}`;
  } else if(fusTab==='man'){
    const opts=s.nombres.slice().sort((a,b)=>a.nombre.localeCompare(b.nombre,'es')).map(o=>`<option value=""${escH(o.nombre)}""></option>`).join('');
    cuerpo=`<div class=""fus-man""><p>Une dos nombres que el programa no relaciona (ej. «Gasoil A» y «Diésel»). Escribe o elige de la lista:</p>
      <label>Mantener el nombre</label><input id=""fusA"" list=""fusLista"" placeholder=""Nombre que se conserva"">
      <label>Unificar con</label><input id=""fusB"" list=""fusLista"" placeholder=""Nombre que pasa a llamarse como el anterior"">
      <datalist id=""fusLista"">${opts}</datalist>
      <div><button class=""chip fus-si"" data-ac=""fus-man"">🔗 Fundir</button> <span id=""fusMsg"" class=""fus-inf""></span></div>
      <p class=""fus-inf"">El histórico de precios de ambos se une y se compara entre empresas. Puedes deshacerlo en «Activas».</p></div>`;
  } else {
    const nombreDe=k=>{ const o=s.nombres.find(z=>z.k===k); return o?o.nombre:((window._artNombres||{})[k]||k); };
    const al=Object.keys(fusiones.alias).map(o=>`<div class=""fus-fila""><div class=""fus-nom""><div>«${escH(nombreDe(o))}»</div><div class=""fus-flecha"">→</div><div>«${escH(nombreDe(canonArt(o)))}»</div></div>
      <div class=""fus-pie""><button class=""chip"" data-ac=""fus-undo"" data-a=""${encA(o)}"">↩ Deshacer</button></div></div>`).join('');
    cuerpo=`<div class=""fus-tit"">Fusiones activas (${nAl})</div>${al||'<div class=""fus-vacio"">Aún no has fundido ningún artículo.</div>'}
      <div class=""fus-tit"">Sugerencias rechazadas (${fusiones.rechazadas.length})</div>
      <div class=""fus-pie""><button class=""chip"" data-ac=""fus-reset"" ${fusiones.rechazadas.length?'':'disabled'}>Volver a mostrar las rechazadas</button></div>`;
  }
  $id('fusT').innerHTML=tabs.map(([k,l])=>`<button class=""${fusTab===k?'on':''}"" data-ac=""fus-tab"" data-a=""${k}"">${l}</button>`).join('');
  $id('fusB').innerHTML=cuerpo;
}
document.addEventListener('click',function(e){
  const t=e.target.closest('[data-ac]'); if(!t) return;
  const a=t.dataset.ac, A=t.dataset.a?decA(t.dataset.a):null, B=t.dataset.b?decA(t.dataset.b):null;
  if(a==='fus-tab'){ fusTab=A; pintarFus(); }
  else if(a==='fus-si'){ if(fusionarArt(A,B)) refrescarTrasFusion(); }
  else if(a==='fus-no'){ rechazarPar(A,B); pintarFus(); actualizarBadgeFus(); }
  else if(a==='fus-undo'){ deshacerFusion(A); refrescarTrasFusion(); }
  else if(a==='fus-reset'){ fusiones.rechazadas=[]; guardarFusiones(); pintarFus(); actualizarBadgeFus(); }
  else if(a==='fus-man'){
    const nA=normArt($id('fusA').value), nB=normArt($id('fusB').value), mapa=window._artMapa||new Map();
    const existe=n=>[...mapa.values()].some(o=>normArt(o.nombre)===n||o.clave===n);
    const clv=n=>{ for(const o of mapa.values()) if(normArt(o.nombre)===n||o.clave===n) return o.clave; return null; };
    const msg=$id('fusMsg');
    if(!nA||!nB||!existe(nA)||!existe(nB)){ msg.textContent='⚠ Elige dos nombres existentes de la lista.'; return; }
    if(clv(nA)===clv(nB)){ msg.textContent='⚠ Ya son el mismo artículo.'; return; }
    if(fusionarArt(clv(nB),clv(nA))){ refrescarTrasFusion(); }
  }
});
document.addEventListener('input',function(e){
  const t=e.target; if(t.dataset&&t.dataset.acU!==undefined) return;
  if(t.dataset&&t.dataset.ac==='fus-umbral'){ fusUmbral=Math.max(40,Math.min(100,parseInt(t.value,10)||75)); fusCache=null; clearTimeout(window._fusT); window._fusT=setTimeout(()=>{ pintarFus(); actualizarBadgeFus(); },350); }
});
document.addEventListener('keydown',function(e){
  if(e.key==='Escape'&&$id('modal-fus').classList.contains('activo')&&!$id('modal').classList.contains('activo')) cerrarFus();
});
/* ─── Init ─── */
poblarFiltros();
poblarSelectorAnios();
try{ dibujarGrafico(); filtrarTrimestre(); } catch(e){ console.error(e); }
leerFusiones();
cargarVistasGuardadas();
document.getElementById('lblTam').textContent = document.getElementById('sliderMiniatura').value+'px';
filtrar();
window.addEventListener('resize',()=>{ try{ dibujarGrafico(); dibujarIvaTrimestral(); }catch(e){} });
";
    }
}