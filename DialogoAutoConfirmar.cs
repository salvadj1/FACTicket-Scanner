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
                MinimizeBox = false,
                KeyPreview = true
            };

            // AutoSize + MaximumSize: el texto se ajusta a varias líneas sin
            // cortarse, en vez de quedar fijo a una altura de 100px.
            var lblMensaje = new Label
            {
                Text = mensaje,
                Left = 15,
                Top = 15,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(380, 0),
                Font = new System.Drawing.Font(dlg.Font.FontFamily, 9.5f)
            };
            var lblContador = new Label
            {
                Left = 15,
                Width = 380,
                ForeColor = System.Drawing.Color.DimGray,
                Font = new System.Drawing.Font(dlg.Font, System.Drawing.FontStyle.Bold),
                AutoSize = false,
                Height = 20
            };
            var btnSi = new Button { Text = "Sí", Width = 100, Height = 34, DialogResult = DialogResult.Yes };
            var btnNo = new Button { Text = "No", Width = 100, Height = 34, DialogResult = DialogResult.No };
            var btnX = new Button { Text = "✕", Width = 24, Height = 24, FlatStyle = FlatStyle.Flat };
            dlg.Controls.AddRange(new Control[] { lblMensaje, lblContador, btnSi, btnNo, btnX });
            dlg.AcceptButton = resultadoPorDefecto ? btnSi : btnNo;

            // Reposiciona todo debajo del mensaje ya medido (alto variable) y
            // ajusta el alto del diálogo para que quepa siempre completo.
            int yTrasMensaje = lblMensaje.Bottom + 12;
            lblContador.Top = yTrasMensaje;
            int yBotones = yTrasMensaje + 30;
            btnX.Location = new System.Drawing.Point(350, yTrasMensaje - 2);
            btnSi.Location = new System.Drawing.Point(130, yBotones);
            btnNo.Location = new System.Drawing.Point(240, yBotones);
            dlg.ClientSize = new System.Drawing.Size(dlg.ClientSize.Width, yBotones + 34 + 15);

            dlg.Shown += (s, e) => (resultadoPorDefecto ? btnSi : btnNo).Focus();

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

            // Escape o clic derecho en cualquier punto: cancela solo la
            // cuenta atrás (igual que btnX), sin cerrar el diálogo.
            void CancelarCuentaAtras()
            {
                if (!timer.Enabled) return;
                timer.Stop();
                lblContador.Text = "Cuenta atrás cancelada.";
            }
            dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) CancelarCuentaAtras(); };
            dlg.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) CancelarCuentaAtras(); };

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
                MinimizeBox = false,
                KeyPreview = true
            };

            // AutoSize + MaximumSize: el texto se ajusta a varias líneas sin
            // cortarse, en vez de quedar fijo a una altura de 85px.
            var lblMensaje = new Label
            {
                Text = mensaje,
                Left = 15,
                Top = 15,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(380, 0),
                Font = new System.Drawing.Font(dlg.Font.FontFamily, 9.5f)
            };
            var lblContador = new Label
            {
                Left = 15,
                Width = 380,
                ForeColor = System.Drawing.Color.DimGray,
                Font = new System.Drawing.Font(dlg.Font, System.Drawing.FontStyle.Bold),
                AutoSize = false,
                Height = 20
            };
            var btnOk = new Button { Text = "Aceptar", Width = 100, Height = 34, DialogResult = DialogResult.OK };
            var btnX2 = new Button { Text = "✕", Width = 24, Height = 24, FlatStyle = FlatStyle.Flat };
            dlg.Controls.AddRange(new Control[] { lblMensaje, lblContador, btnOk, btnX2 });
            dlg.AcceptButton = btnOk;

            // Reposiciona todo debajo del mensaje ya medido (alto variable) y
            // ajusta el alto del diálogo para que quepa siempre completo.
            int yTrasMensaje = lblMensaje.Bottom + 12;
            lblContador.Top = yTrasMensaje;
            int yBoton = yTrasMensaje + 30;
            btnX2.Location = new System.Drawing.Point(350, yTrasMensaje - 2);
            btnOk.Location = new System.Drawing.Point(150, yBoton);
            dlg.ClientSize = new System.Drawing.Size(dlg.ClientSize.Width, yBoton + 34 + 15);

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

            // Escape o clic derecho: cancela solo la cuenta atrás.
            void CancelarCuentaAtras()
            {
                if (!timer.Enabled) return;
                timer.Stop();
                lblContador.Text = "Cuenta atrás cancelada.";
            }
            dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) CancelarCuentaAtras(); };
            dlg.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) CancelarCuentaAtras(); };

            dlg.ShowDialog();
        }
    }
}