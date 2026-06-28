using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    internal class AlbumGenerator
    {
        private readonly string NombreCarpeta;
        private readonly string NombreAlbum;
        private readonly string NombreDatos;

        public AlbumGenerator(string nombreCarpeta, string nombreAlbum, string nombreDatos)
        {
            NombreCarpeta = nombreCarpeta;
            NombreAlbum = nombreAlbum;
            NombreDatos = nombreDatos;
        }

        // -----------------------------------------------------------------------
        public void RegenerarAlbumInicial()
        {
            try
            {
                string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
                System.IO.Directory.CreateDirectory(carpetaTickets);
                var lista = DatosTicket.CargarLista(System.IO.Path.Combine(carpetaTickets, NombreDatos));
                HtmlBuilder.GenerarAlbum(carpetaTickets, lista, NombreAlbum);
            }
            catch { }
        }

        // -----------------------------------------------------------------------
        // Guardar imagen — usa Gemini para extracción de datos
        // -----------------------------------------------------------------------
        public async void GuardarImagen(Mat imagenProcesada, Mat original, int rotacion,
            AjustesEscaner ajustes, Action<AjustesEscaner> guardarAjustes,
            Action<string> actualizarEstado, Action habilitarCapturar, Action guardadoTerminado)
        {
            using Mat _imagenProcesada = imagenProcesada;
            using Mat _original = original;

            try
            {
                actualizarEstado("⏳ Analizando con Gemini...");

                DatosTicket datos = await System.Threading.Tasks.Task.Run(() => Gemini.ExtraerDatosFactura(original));

                habilitarCapturar();

                if (!string.IsNullOrEmpty(datos.ErrorDiagnostico))
                    MessageBox.Show(
                        $"Extracción incompleta:\n\n{datos.ErrorDiagnostico}\n\nPuedes rellenar los campos manualmente.",
                        "Aviso Gemini", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                string carpetaTickets = System.IO.Path.Combine(AppContext.BaseDirectory, NombreCarpeta);
                string rutaJson = System.IO.Path.Combine(carpetaTickets, NombreDatos);
                var listaExistente = DatosTicket.CargarLista(rutaJson);

                DatosTicket? duplicado = BuscarPosibleDuplicado(datos, listaExistente);
                if (duplicado != null && !ConfirmarContinuarConDuplicado(duplicado))
                {
                    actualizarEstado("Guardado cancelado (factura duplicada)");
                    return;
                }

                DatosTicket? datosRevisados = MostrarDialogoRevision(datos);
                if (datosRevisados == null) { actualizarEstado("Guardado cancelado"); return; }

                string subcarpeta = !string.IsNullOrWhiteSpace(datosRevisados.Empresa)
                    ? SanearNombreCarpeta(datosRevisados.Empresa) : "Sin_empresa";
                string carpetaDestino = System.IO.Path.Combine(carpetaTickets, subcarpeta);
                System.IO.Directory.CreateDirectory(carpetaDestino);

                string nombreArchivo = $"escaneo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string rutaImagen = System.IO.Path.Combine(carpetaDestino, nombreArchivo);

                try
                {
                    Cv2.ImWrite(rutaImagen, imagenProcesada);
                    ajustes.UltimaCarpetaGuardado = carpetaDestino;
                    guardarAjustes(ajustes);

                    datosRevisados.ImagenRelativa = System.IO.Path.GetRelativePath(
                        carpetaTickets, rutaImagen).Replace('\\', '/');
                    datosRevisados.FechaGuardado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // Reutilizamos la lista ya cargada al comprobar duplicados,
                    // en vez de releerla del disco otra vez.
                    listaExistente.Add(datosRevisados);
                    DatosTicket.GuardarLista(rutaJson, listaExistente);
                    HtmlBuilder.GenerarAlbum(carpetaTickets, listaExistente, NombreAlbum);

                    actualizarEstado($"✅ Guardado: {rutaImagen}");
                    MessageBox.Show($"Guardado en:\n{rutaImagen}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                habilitarCapturar();
                guardadoTerminado();
            }
        }

        // -----------------------------------------------------------------------
        // Diálogo de revisión de datos (ampliado con campos Gemini)
        // -----------------------------------------------------------------------
        private DatosTicket? MostrarDialogoRevision(DatosTicket datos)
        {
            using var dlg = new Form
            {
                Text = "Revisar datos del ticket",
                Width = 480,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            int y = 15, xLbl = 10, wLbl = 150, xTxt = 170, wTxt = 270, rowH = 32;

            var campos = new List<(string etiqueta, string valor)>
            {
                ("Empresa (emisor):",     datos.Empresa),
                ("Fecha emisión:",        datos.Fecha),
                ("Fecha vencimiento:",    datos.FechaVencimiento),
                ("Nº Factura:",           datos.Numero),
                ("CIF/NIF emisor:",       datos.Cif),
                ("Dirección emisor:",     datos.Direccion),
                ("Teléfono:",             datos.Telefono),
                ("Receptor nombre:",      datos.ReceptorNombre),
                ("Receptor CIF/NIF:",     datos.ReceptorCif),
                ("Receptor dirección:",   datos.ReceptorDireccion),
                ("Base imponible:",       datos.Base),
                ("IVA:",                  datos.Iva),
                ("Total:",                datos.Total),
                ("Método de pago:",       datos.MetodoPago),
            };

            var textBoxes = new List<TextBox>();
            foreach (var (etiqueta, valor) in campos)
            {
                dlg.Controls.Add(new Label { Text = etiqueta, Left = xLbl, Top = y + 5, Width = wLbl });
                var txt = new TextBox { Left = xTxt, Top = y, Width = wTxt, Text = valor };
                dlg.Controls.Add(txt);
                textBoxes.Add(txt);
                y += rowH;
            }

            // Mostrar items (solo lectura en el diálogo, son muchos campos)
            if (datos.Items.Count > 0)
            {
                dlg.Controls.Add(new Label
                {
                    Text = $"Líneas ({datos.Items.Count}):",
                    Left = xLbl,
                    Top = y + 5,
                    Width = wLbl,
                    ForeColor = System.Drawing.Color.DarkSlateBlue
                });
                var txtItems = new TextBox
                {
                    Left = xTxt,
                    Top = y,
                    Width = wTxt,
                    Height = 80,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    ReadOnly = true,
                    Text = string.Join("\r\n", datos.Items.Select(
                        it => $"{it.Descripcion} x{it.Cantidad} = {it.Subtotal}"))
                };
                dlg.Controls.Add(txtItems);
                y += 88;
            }

            var btnOk = new Button { Text = "Guardar", Left = 80, Top = y, Width = 120, Height = 34, DialogResult = DialogResult.OK };
            var btnCan = new Button { Text = "Cancelar", Left = 250, Top = y, Width = 120, Height = 34, DialogResult = DialogResult.Cancel };
            dlg.Controls.Add(btnOk);
            dlg.Controls.Add(btnCan);
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCan;
            dlg.Height = y + 80;

            if (dlg.ShowDialog() != DialogResult.OK) return null;

            datos.Empresa = textBoxes[0].Text.Trim();
            datos.Fecha = textBoxes[1].Text.Trim();
            datos.FechaVencimiento = textBoxes[2].Text.Trim();
            datos.Numero = textBoxes[3].Text.Trim();
            datos.Cif = textBoxes[4].Text.Trim();
            datos.Direccion = textBoxes[5].Text.Trim();
            datos.Telefono = textBoxes[6].Text.Trim();
            datos.ReceptorNombre = textBoxes[7].Text.Trim();
            datos.ReceptorCif = textBoxes[8].Text.Trim();
            datos.ReceptorDireccion = textBoxes[9].Text.Trim();
            datos.Base = textBoxes[10].Text.Trim();
            datos.Iva = textBoxes[11].Text.Trim();
            datos.Total = textBoxes[12].Text.Trim();
            datos.MetodoPago = textBoxes[13].Text.Trim();
            return datos;
        }

        // -----------------------------------------------------------------------
        // Detección de facturas duplicadas.
        // Criterio: misma Empresa + mismo Nº de factura. Si no hay número de
        // factura (campo vacío en cualquiera de las dos), se usa como
        // respaldo Empresa + Fecha + Total. La comparación es insensible a
        // mayúsculas/minúsculas y a espacios sobrantes.
        // -----------------------------------------------------------------------
        private static DatosTicket? BuscarPosibleDuplicado(DatosTicket nuevo, List<DatosTicket> existentes)
        {
            string empresaNueva = NormalizarComparable(nuevo.Empresa);
            if (string.IsNullOrEmpty(empresaNueva)) return null; // sin empresa no hay base fiable de comparación

            string numeroNuevo = NormalizarComparable(nuevo.Numero);

            foreach (var existente in existentes)
            {
                string empresaExistente = NormalizarComparable(existente.Empresa);
                if (empresaExistente != empresaNueva) continue;

                string numeroExistente = NormalizarComparable(existente.Numero);

                if (!string.IsNullOrEmpty(numeroNuevo) && !string.IsNullOrEmpty(numeroExistente))
                {
                    if (numeroNuevo == numeroExistente) return existente;
                    continue; // misma empresa pero número distinto y ambos lo tienen → no es duplicado
                }

                // Respaldo: sin número de factura en alguno de los dos lados,
                // comparamos Empresa + Fecha + Total.
                string fechaNueva = NormalizarComparable(nuevo.Fecha);
                string totalNuevo = NormalizarComparable(nuevo.Total);
                string fechaExistente = NormalizarComparable(existente.Fecha);
                string totalExistente = NormalizarComparable(existente.Total);

                if (!string.IsNullOrEmpty(fechaNueva) && !string.IsNullOrEmpty(totalNuevo)
                    && fechaNueva == fechaExistente && totalNuevo == totalExistente)
                {
                    return existente;
                }
            }

            return null;
        }

        private static string NormalizarComparable(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";
            return valor.Trim().ToUpperInvariant();
        }

        // -----------------------------------------------------------------------
        // Aviso al detectar una posible factura duplicada. Devuelve true si el
        // usuario quiere continuar guardando igualmente, false si cancela.
        // -----------------------------------------------------------------------
        private static bool ConfirmarContinuarConDuplicado(DatosTicket existente)
        {
            string resumen =
                $"Empresa: {existente.Empresa}\n" +
                $"Nº Factura: {(string.IsNullOrWhiteSpace(existente.Numero) ? "(sin número)" : existente.Numero)}\n" +
                $"Fecha: {existente.Fecha}\n" +
                $"Total: {existente.Total}\n" +
                $"Guardada el: {existente.FechaGuardado}";

            var resultado = MessageBox.Show(
                $"Parece que esta factura ya se guardó anteriormente:\n\n{resumen}\n\n¿Quieres continuar y guardarla de nuevo?",
                "Posible factura duplicada", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            return resultado == DialogResult.Yes;
        }

        // -----------------------------------------------------------------------
        private static string SanearNombreCarpeta(string nombre)
        {
            foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(ch, '_');
            return nombre.Trim();
        }

        // -----------------------------------------------------------------------
        // Ajustes
        // -----------------------------------------------------------------------
        public string RutaAjustes()
        {
            string c = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FACTicket_Scanner");
            return System.IO.Path.Combine(c, "ajustes.json");
        }

        public AjustesEscaner CargarAjustes()
        {
            try
            {
                string r = RutaAjustes();
                if (System.IO.File.Exists(r))
                {
                    var v = JsonSerializer.Deserialize<AjustesEscaner>(System.IO.File.ReadAllText(r));
                    if (v != null) return v;
                }
            }
            catch { }
            return new AjustesEscaner();
        }

        public void GuardarAjustes(AjustesEscaner d)
        {
            try
            {
                string r = RutaAjustes();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(r)!);
                System.IO.File.WriteAllText(r, JsonSerializer.Serialize(d));
            }
            catch { }
        }
    }
}