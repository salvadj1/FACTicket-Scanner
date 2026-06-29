using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    internal class CameraManager : IDisposable
    {
        // -----------------------------------------------------------------------
        // Eventos
        // -----------------------------------------------------------------------
        public event EventHandler<Mat>? FrameReady;
        public event EventHandler<string>? Conectada;       // arg: descripción
        public event EventHandler? Desconectada;
        public event EventHandler<string>? ErrorConexion;   // arg: mensaje error

        // -----------------------------------------------------------------------
        // Estado interno
        // -----------------------------------------------------------------------
        private VideoCapture? capture;
        private Mat? frame;
        private System.Windows.Forms.Timer? timer;
        private int fallosConsecutivos = 0;
        private const int MAX_FALLOS_CONSECUTIVOS = 45;
        private string fuenteActual = "";

        public bool EstaConectada => capture != null && capture.IsOpened();
        public string FuenteActual => fuenteActual;

        // -----------------------------------------------------------------------
        // Conectar automáticamente a la última cámara recordada
        // -----------------------------------------------------------------------
        public void ConectarCamaraRecordada(AjustesEscaner ajustes)
        {
            if (string.IsNullOrEmpty(ajustes.UltimoTipoCamara))
            {
                ErrorConexion?.Invoke(this, "Sin cámara configurada – Configuracion > Camara");
                return;
            }

            switch (ajustes.UltimoTipoCamara)
            {
                case "USB":
                    fuenteActual = ajustes.UltimoIndiceCamaraUsb.ToString();
                    InicializarCamara(fuenteActual);
                    break;
                case "IP":
                    fuenteActual = ajustes.UltimaUrlCamaraIp;
                    InicializarCamara(fuenteActual);
                    break;
                case "FILE":
                    fuenteActual = "FILE";
                    Conectada?.Invoke(this, "FILE");
                    break;
            }
        }

        // -----------------------------------------------------------------------
        // Abre el diálogo de cámara USB y conecta si el usuario acepta.
        // -----------------------------------------------------------------------
        public void IniciarSeleccionUsb(AjustesEscaner ajustes, Control owner, Action<AjustesEscaner> guardarAjustes)
        {
            Detener();
            fuenteActual = "";
            string? fuente = DialogoUsb(ajustes, owner, guardarAjustes);
            if (fuente == null) return;          // usuario canceló
            fuenteActual = fuente;
            InicializarCamara(fuente);
        }

        // -----------------------------------------------------------------------
        // Abre el diálogo de cámara IP y conecta si el usuario acepta.
        // -----------------------------------------------------------------------
        public void IniciarSeleccionIp(AjustesEscaner ajustes, Control owner, Action<AjustesEscaner> guardarAjustes)
        {
            Detener();
            fuenteActual = "";
            string? fuente = DialogoIp(ajustes, owner, guardarAjustes);
            if (fuente == null) return;          // usuario canceló
            fuenteActual = fuente;
            InicializarCamara(fuente);
        }

        // Compatibilidad con llamadas antiguas: abre el diálogo USB.
        public void IniciarSeleccion(AjustesEscaner ajustes, Control owner, Action<AjustesEscaner> guardarAjustes)
            => IniciarSeleccionUsb(ajustes, owner, guardarAjustes);

        // -----------------------------------------------------------------------
        public async void InicializarCamara(string fuente)
        {
            try
            {
                capture = await Task.Run(() => int.TryParse(fuente, out int idx)
                    ? new VideoCapture(idx)
                    : new VideoCapture(fuente));

                if (!capture.IsOpened())
                {
                    capture?.Release(); capture = null;
                    ErrorConexion?.Invoke(this, "No se detectó ninguna cámara disponible.");
                    return;
                }

                capture.Set(VideoCaptureProperties.FrameWidth, 1920);
                capture.Set(VideoCaptureProperties.FrameHeight, 1080);

                string tipoStr = int.TryParse(fuente, out _) ? $"USB ({fuente})" : $"IP: {fuente}";
                Conectada?.Invoke(this, tipoStr);

                frame = new Mat();
                timer = new System.Windows.Forms.Timer { Interval = 33 };
                timer.Tick += Timer_Tick;
                timer.Start();
            }
            catch (Exception ex)
            {
                capture?.Release(); capture = null;
                ErrorConexion?.Invoke(this, ex.Message);
            }
        }

        // -----------------------------------------------------------------------
        public void PausarTimer() => timer?.Stop();

        public void ReanudarTimer()
        {
            if (capture != null && capture.IsOpened())
            {
                fallosConsecutivos = 0;
                try { frame?.Dispose(); frame = new Mat(); capture.Read(frame); } catch { }
                timer?.Start();
            }
            else
            {
                capture?.Release();
                capture = null;
                Desconectada?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ResetearFallos() => fallosConsecutivos = 0;

        // -----------------------------------------------------------------------
        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (capture == null || frame == null) return;
                if (!capture.Read(frame) || frame.Empty())
                {
                    if (++fallosConsecutivos >= MAX_FALLOS_CONSECUTIVOS) ManejarDesconexion();
                    return;
                }
                fallosConsecutivos = 0;

                Mat mostrar;
                if (!int.TryParse(fuenteActual, out _) && !string.IsNullOrEmpty(fuenteActual))
                {
                    mostrar = new Mat();
                    Cv2.Rotate(frame, mostrar, RotateFlags.Rotate90Clockwise);
                }
                else mostrar = frame.Clone();

                FrameReady?.Invoke(this, mostrar);
                // El suscriptor (Form1) es responsable de disponer el Mat tras usarlo
            }
            catch
            {
                if (++fallosConsecutivos >= MAX_FALLOS_CONSECUTIVOS) ManejarDesconexion();
            }
        }

        private void ManejarDesconexion()
        {
            timer?.Stop();
            capture?.Release();
            capture = null;
            Desconectada?.Invoke(this, EventArgs.Empty);
        }

        // -----------------------------------------------------------------------
        public void Detener()
        {
            timer?.Stop();
            capture?.Release();
            capture = null;
            fallosConsecutivos = 0;
            fuenteActual = "";
        }

        // -----------------------------------------------------------------------
        // Captura un frame puntual (para "Tomar foto")
        // -----------------------------------------------------------------------
        public Mat? CapturarFrame(Mat frameActual)
        {
            if (frameActual == null || frameActual.Empty()) return null;
            timer?.Stop();

            bool esCamaraIpOFile = !int.TryParse(fuenteActual, out _) && !string.IsNullOrEmpty(fuenteActual);
            if (esCamaraIpOFile)
            {
                Mat rotada = new Mat();
                Cv2.Rotate(frameActual, rotada, RotateFlags.Rotate90Clockwise);
                return rotada;
            }
            return frameActual.Clone();
        }

        // -----------------------------------------------------------------------
        // Diálogo exclusivo para cámara USB.
        // Devuelve la fuente (índice como string) o null si el usuario canceló.
        // -----------------------------------------------------------------------
        private string? DialogoUsb(AjustesEscaner ajustes, Control owner, Action<AjustesEscaner> guardarAjustes)
        {
            using var dlg = new Form
            {
                Text = "Cámara USB",
                Width = 420,
                Height = 185,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            dlg.Controls.Add(new Label
            {
                Text = "Cámara detectada:",
                Left = 20,
                Top = 18,
                Width = 150,
                Height = 20
            });

            var cmbUsb = new ComboBox
            {
                Left = 20,
                Top = 40,
                Width = 370,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            owner.UseWaitCursor = true;
            var indices = DetectarCamarasUsb();
            owner.UseWaitCursor = false;

            if (indices.Count == 0)
                cmbUsb.Items.Add("No se detectó ninguna cámara USB");
            else
                foreach (int idx in indices)
                    cmbUsb.Items.Add($"Cámara {idx}");
            cmbUsb.SelectedIndex = 0;
            dlg.Controls.Add(cmbUsb);

            var btnAceptar = new Button { Text = "Aceptar", Left = 205, Top = 100, Width = 85, Height = 30, DialogResult = DialogResult.OK };
            var btnCancelar = new Button { Text = "Cancelar", Left = 300, Top = 100, Width = 85, Height = 30, DialogResult = DialogResult.Cancel };
            dlg.Controls.Add(btnAceptar);
            dlg.Controls.Add(btnCancelar);
            dlg.AcceptButton = btnAceptar;
            dlg.CancelButton = btnCancelar;

            if (dlg.ShowDialog() != DialogResult.OK) return null;

            if (indices.Count == 0)
            {
                MessageBox.Show("No hay cámaras USB disponibles.", "Sin cámara",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            ajustes.UltimoTipoCamara = "USB";
            ajustes.UltimoIndiceCamaraUsb = indices[cmbUsb.SelectedIndex];
            guardarAjustes(ajustes);
            return indices[cmbUsb.SelectedIndex].ToString();
        }

        // -----------------------------------------------------------------------
        // Diálogo exclusivo para cámara IP.
        // Devuelve la URL o null si el usuario canceló.
        // -----------------------------------------------------------------------
        private string? DialogoIp(AjustesEscaner ajustes, Control owner, Action<AjustesEscaner> guardarAjustes)
        {
            using var dlg = new Form
            {
                Text = "Cámara IP",
                Width = 440,
                Height = 255,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            dlg.Controls.Add(new Label
            {
                Text = "1. Instala \"IP Webcam\" en tu móvil (Android).\n" +
                       "2. Abre la app y pulsa \"Iniciar servidor\".\n" +
                       "3. Busca en la red o escribe la URL manualmente.",
                Left = 20,
                Top = 15,
                Width = 390,
                Height = 52
            });

            var btnBuscar = new Button
            {
                Text = "🔍  Buscar cámaras en la red",
                Left = 20,
                Top = 75,
                Width = 210,
                Height = 30
            };
            var cmbIps = new ComboBox
            {
                Left = 238,
                Top = 75,
                Width = 172,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            dlg.Controls.Add(new Label
            {
                Text = "URL de la cámara:",
                Left = 20,
                Top = 118,
                Width = 130,
                Height = 20
            });
            var txtUrl = new TextBox
            {
                Left = 20,
                Top = 140,
                Width = 390,
                Text = ajustes.UltimaUrlCamaraIp
            };

            btnBuscar.Click += async (s, e) =>
            {
                btnBuscar.Enabled = false;
                btnBuscar.Text = "Buscando...";
                cmbIps.Items.Clear();
                var encontradas = await EscanearCamarasIpAsync();
                if (encontradas.Count == 0)
                    MessageBox.Show("No se encontraron cámaras IP (puerto 8080).",
                        "Sin resultados", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                {
                    foreach (var ip in encontradas) cmbIps.Items.Add(ip);
                    cmbIps.SelectedIndex = 0;
                }
                btnBuscar.Enabled = true;
                btnBuscar.Text = "🔍  Buscar cámaras en la red";
            };

            cmbIps.SelectedIndexChanged += (s, e) =>
            {
                if (cmbIps.SelectedItem != null)
                    txtUrl.Text = $"http://{cmbIps.SelectedItem}:8080/video";
            };

            dlg.Controls.Add(btnBuscar);
            dlg.Controls.Add(cmbIps);
            dlg.Controls.Add(txtUrl);

            var btnAceptar = new Button { Text = "Aceptar", Left = 225, Top = 178, Width = 85, Height = 30, DialogResult = DialogResult.OK };
            var btnCancelar = new Button { Text = "Cancelar", Left = 320, Top = 178, Width = 85, Height = 30, DialogResult = DialogResult.Cancel };
            dlg.Controls.Add(btnAceptar);
            dlg.Controls.Add(btnCancelar);
            dlg.AcceptButton = btnAceptar;
            dlg.CancelButton = btnCancelar;

            if (dlg.ShowDialog() != DialogResult.OK) return null;

            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                MessageBox.Show("Debes indicar la URL de la cámara IP.", "URL vacía",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            ajustes.UltimoTipoCamara = "IP";
            ajustes.UltimaUrlCamaraIp = txtUrl.Text.Trim();
            guardarAjustes(ajustes);
            return txtUrl.Text.Trim();
        }

        // -----------------------------------------------------------------------
        public async Task<List<string>> EscanearCamarasIpAsync()
        {
            var encontradas = new List<string>();
            const string prefijo = "192.168.1";
            using var semaforo = new SemaphoreSlim(40);
            var tareas = new List<Task>();
            for (int i = 1; i <= 254; i++)
            {
                string ip = $"{prefijo}.{i}";
                await semaforo.WaitAsync();
                tareas.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(400);
                        using var cliente = new TcpClient();
                        await cliente.ConnectAsync(ip, 8080, cts.Token);
                        if (cliente.Connected)
                            lock (encontradas) encontradas.Add(ip);
                    }
                    catch { }
                    finally { semaforo.Release(); }
                }));
            }
            foreach (var t in tareas)
                try { await t; } catch { }
            return encontradas;
        }

        public List<int> DetectarCamarasUsb()
        {
            var disponibles = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                try { using var t = new VideoCapture(i); if (t.IsOpened()) disponibles.Add(i); t.Release(); }
                catch { }
            }
            return disponibles;
        }

        // -----------------------------------------------------------------------
        // Conectar directamente sin diálogo (desde toolbar)
        // -----------------------------------------------------------------------
        public void ConectarUsb(int puerto, AjustesEscaner ajustes, Action<AjustesEscaner> guardarAjustes)
        {
            Detener();
            ajustes.UltimoTipoCamara = "USB";
            ajustes.UltimoIndiceCamaraUsb = puerto;
            guardarAjustes(ajustes);
            fuenteActual = puerto.ToString();
            InicializarCamara(fuenteActual);
        }

        public void ConectarIp(string url, AjustesEscaner ajustes, Action<AjustesEscaner> guardarAjustes)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            Detener();
            ajustes.UltimoTipoCamara = "IP";
            ajustes.UltimaUrlCamaraIp = url.Trim();
            guardarAjustes(ajustes);
            fuenteActual = url.Trim();
            InicializarCamara(fuenteActual);
        }

        // -----------------------------------------------------------------------
        public void Dispose()
        {
            Detener();
            frame?.Dispose();
        }
    }
}