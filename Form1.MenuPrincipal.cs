using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Menú principal en HTML (ver MenuPrincipalHtml.cs). Se muestra al
    // iniciar y con el botón "🏠 Inicio" de la barra de menús. Los botones del
    // HTML llaman a los mismos handlers que el menú clásico.
    // -----------------------------------------------------------------------
    public partial class Form1
    {
        private Panel panelMenuPrincipal = null!;
        private WebView2 webViewMenu = null!;
        private bool menuWebInicializado = false;

        // true si el panel web se abrió desde el menú: al pulsar ✕ se vuelve al menú
        private bool volverAlMenuAlCerrarVisor = false;

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
                await NavegarMenuAsync(MenuPrincipalHtml.GenerarEsqueleto(Version.Trim(' ', '-')));
                if (id != menuRefrescoId) return;

                // 2) Estadísticas en segundo plano, enviadas a la página por tandas
                string carpeta = Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
                await Task.Run(() => MenuPrincipalHtml.CalcularEstadisticas(
                    carpeta, null, valores => EnviarValoresMenu(valores, id), 15, () => id != menuRefrescoId));
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
        // hilo de la interfaz antes de ejecutar nada.
        // -------------------------------------------------------------------
        private void WebViewMenu_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string accion = "";
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                if (doc.RootElement.TryGetProperty("accion", out var ac)) accion = ac.GetString() ?? "";
            }
            catch (Exception ex)
            {
                Log("Menú principal: mensaje no válido - " + ex.Message);
                return;
            }
            BeginInvoke(new Action(() => EjecutarAccionMenu(accion)));
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
