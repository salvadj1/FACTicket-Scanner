using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace FACTicket_Scanner
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // --- Captura de excepciones detalladas (temporal, para diagnóstico) ---
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show(e.Exception.ToString(), "Excepción detallada (hilo UI)",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show(e.ExceptionObject?.ToString() ?? "desconocido",
                    "Excepción no controlada (otro hilo)",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                MessageBox.Show(e.Exception.ToString(), "Excepción no observada (Task)",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.SetObserved();
            };
            // --- Fin captura de excepciones detalladas ---

            if (!VcRuntimeInstalado())
            {
                var resultado = MessageBox.Show(
                    "Esta aplicación requiere el paquete Visual C++ Redistributable 2019 x64,\n" +
                    "que no está instalado en este equipo.\n\n" +
                    "Sin él, el OCR (detección de empresa) no funcionará.\n\n" +
                    "¿Deseas descargarlo ahora? (es gratuito, ~25 MB)",
                    "Requisito faltante – VC++ Redistributable",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (resultado == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                        UseShellExecute = true
                    });

                    MessageBox.Show(
                        "Una vez instalado el paquete, reinicia la aplicación.",
                        "Instalación pendiente",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    return;
                }
            }

            Application.Run(new Form1());
        }

        private static bool VcRuntimeInstalado()
        {
            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return System.IO.File.Exists(System.IO.Path.Combine(system32, "vcruntime140.dll"));
        }
    }
}