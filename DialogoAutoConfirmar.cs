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
        // Diálogo Sí/No para posibles facturas duplicadas, con vista previa de
        // las dos imágenes a pantalla partida (mitad izquierda = ORIGINAL ya
        // guardada, mitad derecha = DUPLICADA recién añadida) y cuenta atrás.
        //
        // Parámetros:
        //   imagenNueva          : imagen recién cargada (no se libera aquí).
        //   rutaImagenExistente  : ruta de la imagen ya guardada (null = no disponible).
        //   mensaje              : texto resumen mostrado bajo las imágenes.
        //   titulo               : título de la ventana.
        //   resultadoPorDefecto  : valor devuelto si se agota la cuenta atrás.
        //   segundos             : duración de la cuenta atrás (30 por defecto).
        // Devuelve true si el usuario elige "Sí" (continuar), false en caso contrario.
        // Reutilizable en cualquier proyecto con OpenCvSharp (solo depende de
        // ImageProcessor.MatToBitmap para convertir la imagen nueva).
        // -----------------------------------------------------------------------
        public static bool ConfirmarDuplicadoConVistaPrevia(OpenCvSharp.Mat imagenNueva, string? rutaImagenExistente,
            string mensaje, string titulo, bool resultadoPorDefecto, int segundos = 30)
        {
            // Convierte a Bitmap sin bloquear el archivo en disco
            System.Drawing.Bitmap? bmpExistente = null;
            if (!string.IsNullOrEmpty(rutaImagenExistente) && System.IO.File.Exists(rutaImagenExistente))
            {
                try
                {
                    using var fs = System.IO.File.OpenRead(rutaImagenExistente);
                    using var tmp = System.Drawing.Image.FromStream(fs);
                    bmpExistente = new System.Drawing.Bitmap(tmp);
                }
                catch { bmpExistente = null; }
            }
            System.Drawing.Bitmap bmpNueva = ImageProcessor.MatToBitmap(imagenNueva);

            // Ventana grande: ocupa el 90% del área de trabajo
            var area = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 800);
            using var dlg = new Form
            {
                Text = titulo,
                Width = (int)(area.Width * 0.9),
                Height = (int)(area.Height * 0.9),
                FormBorderStyle = FormBorderStyle.Sizable,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = true,
                MinimizeBox = false,
                ShowInTaskbar = false,
                KeyPreview = true
            };

            // --- Zona inferior: mensaje, contador y botones ---
            var panelInferior = new Panel { Dock = DockStyle.Bottom, Height = 190 };
            var lblMensaje = new Label
            {
                Text = mensaje,
                Left = 15,
                Top = 8,
                AutoSize = false,
                Width = 900,
                Height = 110,
                Font = new System.Drawing.Font(dlg.Font.FontFamily, 9.5f)
            };
            var lblContador = new Label
            {
                Left = 15,
                Top = 122,
                Width = 500,
                Height = 20,
                ForeColor = System.Drawing.Color.DimGray,
                Font = new System.Drawing.Font(dlg.Font, System.Drawing.FontStyle.Bold)
            };
            var btnSi = new Button { Text = "Sí, continuar", Width = 140, Height = 34, DialogResult = DialogResult.Yes, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            var btnNo = new Button { Text = "No, descartar", Width = 140, Height = 34, DialogResult = DialogResult.No, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            panelInferior.Controls.AddRange(new Control[] { lblMensaje, lblContador, btnSi, btnNo });
            // Reposiciona botones (esquina inferior derecha) y ajusta el ancho del mensaje
            void Reposicionar()
            {
                btnNo.Location = new System.Drawing.Point(panelInferior.ClientSize.Width - btnNo.Width - 15, panelInferior.Height - btnNo.Height - 15);
                btnSi.Location = new System.Drawing.Point(btnNo.Left - btnSi.Width - 10, btnNo.Top);
                lblMensaje.Width = Math.Max(200, btnSi.Left - 30);
            }
            panelInferior.Resize += (s, e) => Reposicionar();
            dlg.AcceptButton = resultadoPorDefecto ? btnSi : btnNo;

            // --- Zona de imágenes: 2 columnas del 50% cada una ---
            var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label Titulo(string texto, System.Drawing.Color fondo) => new Label
            {
                Text = texto,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                BackColor = fondo,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font(dlg.Font.FontFamily, 11f, System.Drawing.FontStyle.Bold)
            };
            PictureBox Imagen(System.Drawing.Image? img) => new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = System.Drawing.Color.FromArgb(40, 40, 40),
                Image = img
            };

            tabla.Controls.Add(Titulo("ORIGINAL (ya guardada)", System.Drawing.Color.SeaGreen), 0, 0);
            tabla.Controls.Add(Titulo("DUPLICADA (recién añadida)", System.Drawing.Color.Firebrick), 1, 0);
            tabla.Controls.Add(Imagen(bmpExistente), 0, 1);
            tabla.Controls.Add(Imagen(bmpNueva), 1, 1);

            dlg.Controls.Add(tabla);
            dlg.Controls.Add(panelInferior);
            dlg.Shown += (s, e) =>
            {
                // Reposicionado inicial de botones y foco en el valor por defecto
                Reposicionar();
                (resultadoPorDefecto ? btnSi : btnNo).Focus();
            };

            // --- Cuenta atrás (misma mecánica que Confirmar) ---
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

            // Escape o clic derecho: cancela solo la cuenta atrás, sin cerrar el diálogo
            void CancelarCuentaAtras()
            {
                if (!timer.Enabled) return;
                timer.Stop();
                lblContador.Text = "Cuenta atrás cancelada.";
            }
            dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) CancelarCuentaAtras(); };
            dlg.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) CancelarCuentaAtras(); };
            tabla.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) CancelarCuentaAtras(); };
            foreach (Control c in tabla.Controls) c.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) CancelarCuentaAtras(); };

            bool resultado = dlg.ShowDialog() == DialogResult.Yes;

            // Libera los bitmaps creados para la vista previa
            bmpExistente?.Dispose();
            bmpNueva.Dispose();
            return resultado;
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