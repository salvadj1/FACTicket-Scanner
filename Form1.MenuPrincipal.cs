using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Menú principal en HTML (ver MenuPrincipalHtml.cs). Se muestra al
    // iniciar y con el botón "🏠 Inicio" de la barra de menús. Los botones del
    // HTML llaman a los mismos handlers que el menú clásico. Las tarjetas se
    // pueden ocultar desde "Personalizar" (se guardan en AjustesEscaner) y las
    // de aviso (Sin presentar / Por revisar) abren el panel web ya filtrado.
    // -----------------------------------------------------------------------
    public partial class Form1
    {
        private Panel panelMenuPrincipal = null!;
        private WebView2 webViewMenu = null!;
        private bool menuWebInicializado = false;

        // true si el panel web se abrió desde el menú: al pulsar ✕ se vuelve al menú
        private bool volverAlMenuAlCerrarVisor = false;

        // Últimas estadísticas calculadas para el menú (para la lista de archivos faltantes).
        // null mientras se calculan o si el menú se refrescó antes de terminar.
        private EstadisticasMenu? estadisticasMenuActual = null;

        // -------------------------------------------------------------------
        // Crea el panel + WebView2 (a pantalla completa, oculto) y engancha
        // los eventos que necesitan convivir con el menú. Se llama una vez
        // desde el constructor, tras InitializeComponent.
        // -------------------------------------------------------------------
        private void ConstruirMenuPrincipal()
        {
            panelMenuPrincipal = new Panel { Dock = DockStyle.Fill, Visible = false, Name = "panelMenuPrincipal" };
            webViewMenu = new WebView2 { Dock = DockStyle.Fill, Name = "webViewMenu" };
            panelMenuPrincipal.Controls.Add(webViewMenu);
            this.Controls.Add(panelMenuPrincipal);
            panelMenuPrincipal.BringToFront(); // Fill va en primer plano para ocupar el espacio restante

            // Botón "Inicio" al principio de la barra de menús
            var itemInicio = new ToolStripMenuItem("🏠 Inicio");
            itemInicio.Click += (s, e) =>
            {
                if (guardadoEnCurso) return; // no interrumpir un guardado
                volverAlMenuAlCerrarVisor = false;
                MostrarMenuPrincipal();
            };
            menuStrip1.Items.Insert(0, itemInicio);

            // Si el usuario entra por el menú clásico, el menú HTML no debe quedar debajo
            visorToolStripMenuItem.Click += (s, e) => panelMenuPrincipal.Visible = false;
            abrirToolStripMenuItem.Click += (s, e) => { if (panelMenuPrincipal.Visible) OcultarMenuPrincipal(true); };
            desdeCamaraToolStripMenuItem.Click += (s, e) => { if (panelMenuPrincipal.Visible) OcultarMenuPrincipal(true); };

            // ✕ del panel web: si venía del menú, se vuelve a él (solo clic del usuario;
            // las llamadas directas a btnCerrarVisor_Click del flujo de edición no pasan por aquí)
            btnCerrarVisor.Click += (s, e) =>
            {
                if (!volverAlMenuAlCerrarVisor) return;
                volverAlMenuAlCerrarVisor = false;
                MostrarMenuPrincipal();
            };
        }

        // Identifica la última petición de refresco del menú: las tareas de peticiones
        // anteriores (p. ej. pulsar "Inicio" varias veces) se descartan al ver que ya no coincide.
        private int menuRefrescoId = 0;

        // -------------------------------------------------------------------
        // Muestra el menú (ocultando escáner y visor) al instante: primero el
        // esqueleto con las tarjetas en "…", y después las estadísticas se
        // calculan en segundo plano y se envían a la página por tandas, de modo
        // que las tarjetas se rellenan progresivamente sin bloquear la interfaz.
        // -------------------------------------------------------------------
        private async void MostrarMenuPrincipal()
        {
            int id = ++menuRefrescoId;
            try
            {
                panelIzquierdo.Visible = false;
                panelDerecho.Visible = false;
                panelVisor.Visible = false;
                btnCerrarVisor.Visible = false;
                panelNavModal.Visible = false;
                panelMenuPrincipal.Visible = true;
                panelMenuPrincipal.BringToFront();

                await webViewMenu.EnsureCoreWebView2Async();
                if (id != menuRefrescoId) return; // llegó una petición más reciente
                if (!menuWebInicializado)
                {
                    menuWebInicializado = true;
                    webViewMenu.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    webViewMenu.CoreWebView2.Settings.IsZoomControlEnabled = false;
                    webViewMenu.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    webViewMenu.CoreWebView2.WebMessageReceived += WebViewMenu_WebMessageReceived;
                }

                // 1) Menú ya visible, tarjetas en "…"
                await NavegarMenuAsync(MenuPrincipalHtml.GenerarEsqueleto(
                    Version.Trim(' ', '-'), ajustes.TarjetasMenuOcultas, ajustes.TrimestreTarjeta));
                if (id != menuRefrescoId) return;

                // 2) Estadísticas en segundo plano, enviadas a la página por tandas
                string carpeta = Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
                estadisticasMenuActual = null;

                // Los posibles duplicados se buscan con las mismas reglas que el buscador de duplicados
                var reglasDup = new ReglasDuplicados
                {
                    Numero = ajustes.DupNumero,
                    Fecha = ajustes.DupFecha,
                    Total = ajustes.DupTotal,
                    Empresa = ajustes.DupEmpresa
                };

                var est = await Task.Run(() => MenuPrincipalHtml.CalcularEstadisticas(
                    carpeta, null, valores => EnviarValoresMenu(valores, id), 15, () => id != menuRefrescoId, reglasDup));
                if (id == menuRefrescoId) estadisticasMenuActual = est;
            }
            catch (Exception ex)
            {
                Log("Menú principal: error al mostrar - " + ex.Message);
            }
        }

        // -------------------------------------------------------------------
        // Carga un HTML en el WebView2 del menú y espera a que termine de
        // cargarse (así los mensajes posteriores no se pierden). Reutilizable
        // con cualquier WebView2 cambiando la referencia.
        // -------------------------------------------------------------------
        private async Task NavegarMenuAsync(string html)
        {
            var terminado = new TaskCompletionSource<bool>();
            void AlTerminar(object? s, CoreWebView2NavigationCompletedEventArgs a) => terminado.TrySetResult(a.IsSuccess);

            webViewMenu.CoreWebView2.NavigationCompleted += AlTerminar;
            try
            {
                webViewMenu.CoreWebView2.NavigateToString(html);
                await terminado.Task;
            }
            finally
            {
                webViewMenu.CoreWebView2.NavigationCompleted -= AlTerminar;
            }
        }

        // -------------------------------------------------------------------
        // Envía a la página del menú los valores de las tarjetas ({clave:valor}).
        // Se llama desde el hilo de cálculo: pasa al hilo de la interfaz y
        // descarta el envío si el menú se refrescó o la ventana se cerró.
        // -------------------------------------------------------------------
        private void EnviarValoresMenu(Dictionary<string, string> valores, int id)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(valores);
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || id != menuRefrescoId) return;
                    webViewMenu.CoreWebView2.PostWebMessageAsJson(json);
                }));
            }
            catch { /* ventana cerrándose: nada que actualizar */ }
        }

        // -------------------------------------------------------------------
        // Oculta el menú. Con mostrarEscaner=true vuelve a mostrar la vista
        // de escaneo (cámara/imagen); con false solo lo oculta (p. ej. cuando
        // a continuación se abre el panel web, que gestiona sus propios paneles).
        // -------------------------------------------------------------------
        private void OcultarMenuPrincipal(bool mostrarEscaner)
        {
            panelMenuPrincipal.Visible = false;
            if (!mostrarEscaner) return;
            volverAlMenuAlCerrarVisor = false;
            panelIzquierdo.Visible = true;
            panelDerecho.Visible = true;
        }

        // -------------------------------------------------------------------
        // Mensaje {accion:"..."} enviado por un botón del HTML. Se pasa al
        // hilo de la interfaz antes de ejecutar nada. El mensaje especial
        // {accion:"tarjetas", ocultas:[ids]} guarda las tarjetas ocultadas desde
        // "Personalizar" y {accion:"trimestreTarjeta", valor:"AAAA-N"} el trimestre
        // elegido en su tarjeta, en lugar de ejecutar una acción.
        // -------------------------------------------------------------------
        private void WebViewMenu_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string accion = "";
            List<string>? ocultas = null;
            string valor = "";
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                if (doc.RootElement.TryGetProperty("accion", out var ac)) accion = ac.GetString() ?? "";
                if (doc.RootElement.TryGetProperty("valor", out var va) &&
                    va.ValueKind == System.Text.Json.JsonValueKind.String) valor = va.GetString() ?? "";

                if (doc.RootElement.TryGetProperty("ocultas", out var oc) &&
                    oc.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    ocultas = new List<string>();
                    foreach (var x in oc.EnumerateArray())
                        if (x.ValueKind == System.Text.Json.JsonValueKind.String) ocultas.Add(x.GetString() ?? "");
                }
            }
            catch (Exception ex)
            {
                Log("Menú principal: mensaje no válido - " + ex.Message);
                return;
            }

            BeginInvoke(new Action(() =>
            {
                if (accion == "tarjetas") GuardarTarjetasOcultas(ocultas ?? new List<string>());
                else if (accion == "trimestreTarjeta") GuardarTrimestreTarjeta(valor);
                else EjecutarAccionMenu(accion);
            }));
        }

        // -------------------------------------------------------------------
        // Guarda en ajustes.json qué tarjetas del menú están ocultas. Solo se
        // aceptan ids conocidos (MenuPrincipalHtml.IdsTarjetas). No hace falta
        // refrescar el menú: la página ya muestra el estado elegido.
        // -------------------------------------------------------------------
        private void GuardarTarjetasOcultas(List<string> ids)
        {
            ajustes.TarjetasMenuOcultas = MenuPrincipalHtml.LimpiarIdsTarjetas(ids);
            album.GuardarAjustes(ajustes);
        }

        // -------------------------------------------------------------------
        // Guarda el trimestre elegido en la tarjeta "Trimestre" ("AAAA-N", p. ej.
        // "2026-2"). Un valor vacío o con otro formato deja el trimestre anterior
        // automático. No refresca el menú: la página ya muestra la elección.
        // -------------------------------------------------------------------
        private void GuardarTrimestreTarjeta(string valor)
        {
            ajustes.TrimestreTarjeta = Regex.IsMatch(valor ?? "", @"^\d{4}-[1-4]$") ? valor! : "";
            album.GuardarAjustes(ajustes);
        }

        // -------------------------------------------------------------------
        // Tarjeta "Archivos faltantes": muestra qué facturas tienen la imagen o el
        // PDF ausentes en disco (hasta 25 líneas; el resto se resume en un número).
        // Usa las estadísticas ya calculadas para el menú.
        // -------------------------------------------------------------------
        private void MostrarArchivosFaltantes()
        {
            var est = estadisticasMenuActual;
            if (est == null)
            {
                MessageBox.Show("Las estadísticas aún se están calculando. Inténtalo de nuevo en unos segundos.",
                    "Archivos faltantes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (est.SinArchivos == 0)
            {
                MessageBox.Show("Todas las facturas tienen su imagen o PDF en disco.",
                    "Archivos faltantes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            const int maxLineas = 25;
            var lineas = est.SinArchivosDetalle.Take(maxLineas).ToList();
            string resto = est.SinArchivos > lineas.Count
                ? "\n… y " + (est.SinArchivos - lineas.Count) + " más."
                : "";
            MessageBox.Show(est.SinArchivos + " factura(s) con archivos faltantes:\n\n" +
                string.Join("\n", lineas) + resto,
                "Archivos faltantes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Script común (se ejecuta dentro del panel web, ver HtmlBuilder.cs): vuelve a la
        // pestaña Facturas y quita búsqueda, selectores, filtros avanzados y filtro especial.
        private const string JsPanelLimpiar =
            "if(tipoActual!=='facturas'){var b=[].slice.call(document.querySelectorAll('.tab-tipo'))" +
            ".filter(function(x){return (x.getAttribute('onclick')||'').indexOf('facturas')>=0;})[0];" +
            "if(b)cambiarTipo('facturas',b);}" +
            "limpiarAvanzado(true);filtroEspecial=null;document.getElementById('buscar').value='';" +
            "['filtroAnio','filtroTrimestre','filtroEmpresa'].forEach(function(i){document.getElementById(i).value='';});";

        // Panel web mostrando solo las facturas NO presentadas (filtro avanzado "Presentada = No").
        private const string JsPanelPendientes =
            "try{" + JsPanelLimpiar + "document.getElementById('avPres').value='no';filtrar();}catch(e){}";

        // Panel web mostrando solo las facturas "por revisar" (sin empresa, sin importe o sin fecha legible).
        private const string JsPanelPorRevisar =
            "try{" + JsPanelLimpiar + "filtroEspecial='porRevisar';filtrar();}catch(e){}";

        // -------------------------------------------------------------------
        // Abre el panel web desde el menú y, cuando la página termina de cargar,
        // ejecuta 'script' sobre ella (p. ej. para aplicar un filtro). Un solo
        // uso: el manejador se quita en la primera carga. Reutilizable con
        // cualquier script que la página del panel entienda.
        // -------------------------------------------------------------------
        private async void AbrirPanelConFiltro(string script)
        {
            volverAlMenuAlCerrarVisor = true;
            OcultarMenuPrincipal(false);
            try
            {
                await webViewAlbum.EnsureCoreWebView2Async();

                async void AlCargar(object? s, CoreWebView2NavigationCompletedEventArgs a)
                {
                    webViewAlbum.CoreWebView2.NavigationCompleted -= AlCargar;
                    try
                    {
                        await Task.Delay(300); // deja que el JS de la página construya el listado
                        await webViewAlbum.CoreWebView2.ExecuteScriptAsync(script);
                    }
                    catch { /* ventana cerrándose: nada que filtrar */ }
                }
                webViewAlbum.CoreWebView2.NavigationCompleted += AlCargar;

                visorToolStripMenuItem_Click(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log("Menú principal: error al abrir el panel filtrado - " + ex.Message);
            }
        }

        // -------------------------------------------------------------------
        // Ejecuta la acción pedida reutilizando los handlers del menú clásico.
        // Las utilidades (diálogos) dejan el menú visible y lo refrescan al
        // volver, porque pueden haber cambiado las estadísticas.
        // -------------------------------------------------------------------
        private void EjecutarAccionMenu(string accion)
        {
            switch (accion)
            {
                case "panel":
                    volverAlMenuAlCerrarVisor = true;
                    OcultarMenuPrincipal(false);
                    visorToolStripMenuItem_Click(this, EventArgs.Empty);
                    break;

                case "pendientes": // tarjeta "Sin presentar": panel con esas facturas
                    AbrirPanelConFiltro(JsPanelPendientes);
                    break;

                case "revisar": // tarjeta "Por revisar": panel con las facturas incompletas
                    AbrirPanelConFiltro(JsPanelPorRevisar);
                    break;

                case "faltantes": // tarjeta "Archivos faltantes": lista de facturas sin imagen o PDF
                    MostrarArchivosFaltantes();
                    break;

                case "escanear":
                    OcultarMenuPrincipal(true);
                    desdeCamaraToolStripMenuItem_Click(this, EventArgs.Empty);
                    break;

                case "importar":
                    // El diálogo se abre sobre el menú; solo se pasa a la vista de
                    // escaneo si el usuario eligió archivos (modoCaptura = imagen cargada).
                    abrirToolStripMenuItem_Click(this, EventArgs.Empty);
                    if (modoCaptura) OcultarMenuPrincipal(true);
                    break;

                case "trimestre":
                    cerrarTrimestreToolStripMenuItem_Click(this, EventArgs.Empty);
                    MostrarMenuPrincipal();
                    break;

                case "exportar":
                    exportarToolStripMenuItem_Click(this, EventArgs.Empty);
                    break;

                case "duplicados":
                    buscarDuplicadosToolStripMenuItem_Click(this, EventArgs.Empty);
                    albumPendiente = true; // pudo borrar facturas: se regenerará al abrir el panel
                    MostrarMenuPrincipal();
                    break;

                case "conversor":
                    conversorIMGPDFToolStripMenuItem_Click(this, EventArgs.Empty);
                    break;

                case "ajustes":
                    generalToolStripMenuItem_Click(this, EventArgs.Empty);
                    break;
            }
        }
    }
}
