using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Utilidades REUTILIZABLES para traer ventanas al frente en WinForms,
    // incluso si la aplicación está minimizada o detrás de otras ventanas
    // (Windows normalmente bloquea que una app "robe" el foco; aquí se usa
    // el truco de AttachThreadInput para conseguirlo igualmente).
    // No depende de ninguna otra clase del proyecto.
    // -----------------------------------------------------------------------
    internal static class VentanaHelper
    {
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

        // -------------------------------------------------------------------
        // True si la ventana en primer plano de Windows pertenece a ESTE proceso.
        // -------------------------------------------------------------------
        public static bool AplicacionEnPrimerPlano()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            GetWindowThreadProcessId(fg, out uint pid);
            return pid == (uint)Environment.ProcessId;
        }

        // -------------------------------------------------------------------
        // Trae un formulario al frente y le da el foco.
        //   f         : formulario (puede llamarse desde cualquier hilo).
        //   maximizar : true = lo deja maximizado; false = respeta su tamaño
        //               (si estaba minimizado, lo restaura).
        // -------------------------------------------------------------------
        public static void TraerAlFrente(Form? f, bool maximizar)
        {
            if (f == null || f.IsDisposed) return;
            if (f.InvokeRequired)
            {
                f.Invoke(new Action(() => TraerAlFrente(f, maximizar)));
                return;
            }

            IntPtr h = f.Handle;
            if (IsIconic(h)) ShowWindow(h, SW_RESTORE);                       // sale de minimizado
            if (maximizar && f.WindowState != FormWindowState.Maximized)
                f.WindowState = FormWindowState.Maximized;

            // Truco de primer plano: se acopla momentáneamente al hilo de la
            // ventana que tiene el foco para que Windows permita el cambio.
            IntPtr fg = GetForegroundWindow();
            uint hiloFg = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out _);
            uint miHilo = GetCurrentThreadId();
            bool acoplado = hiloFg != 0 && hiloFg != miHilo && AttachThreadInput(miHilo, hiloFg, true);
            try
            {
                bool topPrevio = f.TopMost;
                f.TopMost = true;            // sube sobre las demás ventanas...
                f.TopMost = topPrevio;       // ...sin quedarse "siempre encima"
                BringWindowToTop(h);
                SetForegroundWindow(h);
            }
            finally
            {
                if (acoplado) AttachThreadInput(miHilo, hiloFg, false);
            }
            f.Activate();
        }

        // -------------------------------------------------------------------
        // Trae al frente, MAXIMIZADA, la ventana principal de la aplicación,
        // pero solo si está minimizada o detrás de otra aplicación (si el
        // usuario ya está trabajando en ella no se toca nada).
        // Devuelve la ventana principal (útil como propietario de un diálogo).
        // -------------------------------------------------------------------
        public static Form? TraerPrincipalAlFrente()
        {
            Form? principal = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (principal == null) return null;
            if (principal.WindowState == FormWindowState.Minimized || !AplicacionEnPrimerPlano())
                TraerAlFrente(principal, maximizar: true);
            return principal;
        }
    }
}
