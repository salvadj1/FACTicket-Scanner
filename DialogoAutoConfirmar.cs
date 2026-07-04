using System;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Diálogos de aviso/confirmación con cuenta atrás propia y auto-cierre.
    // Extraído de AlbumGenerator para poder reutilizarlo también desde
    // Form1 y GeminiAPI: ningún diálogo debe bloquear la app indefinidamente
    // durante guardado/extracción si el usuario no está delante.
    // -----------------------------------------------------------------------
    internal static class DialogoAutoConfirmar
    {
        // -----------------------------------------------------------------------
        // Diálogo Sí/No con cuenta atrás propia. resultadoPorDefecto se aplica
        // si el usuario no responde a tiempo.
        // -----------------------------------------------------------------------
        public static bool Confirmar(string mensaje, string titulo, bool resultadoPorDefecto, int segundos = Form1.Timeout_Dialogos)
        {
            using var dlg = new Form
            {
                Text = titulo,
                Width = 420,
                Height = 245,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblMensaje = new Label { Text = mensaje, Left = 15, Top = 15, Width = 380, Height = 100 };
            var lblContador = new Label { Left = 15, Top = 123, Width = 380, ForeColor = System.Drawing.Color.DimGray };
            var btnSi = new Button { Text = "Sí", Left = 130, Top = 153, Width = 100, Height = 34, DialogResult = DialogResult.Yes };
            var btnNo = new Button { Text = "No", Left = 240, Top = 153, Width = 100, Height = 34, DialogResult = DialogResult.No };
            var btnX = new Button { Text = "✕", Left = 350, Top = 123, Width = 24, Height = 24, FlatStyle = FlatStyle.Flat };
            dlg.Controls.AddRange(new Control[] { lblMensaje, lblContador, btnSi, btnNo, btnX });
            dlg.AcceptButton = resultadoPorDefecto ? btnSi : btnNo;

            int restantes = segundos;
            lblContador.Text = $"Se autoconfirmará en {restantes}s...";
            using var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                restantes--;
                if (restantes <= 0)
                {
                    timer.Stop();
                    dlg.DialogResult = resultadoPorDefecto ? DialogResult.Yes : DialogResult.No;
                    dlg.Close();
                    return;
                }
                lblContador.Text = $"Se autoconfirmará en {restantes}s...";
            };
            dlg.Shown += (s, e) => timer.Start();
            btnSi.Click += (s, e) => timer.Stop();
            btnNo.Click += (s, e) => timer.Stop();
            btnX.Click += (s, e) => { timer.Stop(); dlg.DialogResult = resultadoPorDefecto ? DialogResult.Yes : DialogResult.No; dlg.Close(); };

            return dlg.ShowDialog() == DialogResult.Yes;
        }

        // -----------------------------------------------------------------------
        // Aviso simple (solo Aceptar) con cuenta atrás propia.
        // -----------------------------------------------------------------------
        public static void Aviso(string mensaje, string titulo, int segundos = Form1.Timeout_Dialogos)
        {
            using var dlg = new Form
            {
                Text = titulo,
                Width = 420,
                Height = 280,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblMensaje = new Label { Text = mensaje, Left = 15, Top = 15, Width = 380, Height = 85 };
            var lblContador = new Label { Left = 15, Top = 108, Width = 380, ForeColor = System.Drawing.Color.DimGray };
            var btnOk = new Button { Text = "Aceptar", Left = 150, Top = 138, Width = 100, Height = 34, DialogResult = DialogResult.OK };
            var btnX2 = new Button { Text = "✕", Left = 350, Top = 108, Width = 24, Height = 24, FlatStyle = FlatStyle.Flat };
            dlg.Controls.AddRange(new Control[] { lblMensaje, lblContador, btnOk, btnX2 });
            dlg.AcceptButton = btnOk;

            int restantes = segundos;
            lblContador.Text = $"Se cerrará en {restantes}s...";
            using var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                restantes--;
                if (restantes <= 0)
                {
                    timer.Stop();
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                    return;
                }
                lblContador.Text = $"Se cerrará en {restantes}s...";
            };
            dlg.Shown += (s, e) => timer.Start();
            btnOk.Click += (s, e) => timer.Stop();
            btnX2.Click += (s, e) => { timer.Stop(); dlg.DialogResult = DialogResult.OK; dlg.Close(); };

            dlg.ShowDialog();
        }
    }
}