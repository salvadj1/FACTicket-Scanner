using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Configuración de una API (nombre, URL de endpoint, clave)
    // -----------------------------------------------------------------------
    public class ApiConfig
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = "";

        [JsonPropertyName("ultimoAcceso")]
        public string UltimoAcceso { get; set; } = "";

        [JsonPropertyName("ultimoFallo")]
        public string UltimoFallo { get; set; } = "";

        [JsonPropertyName("peticionesOk")]
        public int PeticionesOk { get; set; } = 0;
    }

    // -----------------------------------------------------------------------
    // Contenedor persistido en apis.json: lista de APIs + cuál está activa
    // -----------------------------------------------------------------------
    public class ApiListaGuardada
    {
        [JsonPropertyName("apis")]
        public List<ApiConfig> Apis { get; set; } = new List<ApiConfig>();

        [JsonPropertyName("activaNombre")]
        public string ActivaNombre { get; set; } = "";
    }

    public class GeminiAPI
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        // -----------------------------------------------------------------------
        // Persistencia de apis.json (misma carpeta que ajustes.json)
        // -----------------------------------------------------------------------
        public static string RutaApis()
        {
            string c = AppDomain.CurrentDomain.BaseDirectory;
            return System.IO.Path.Combine(c, "apis.json");
        }

        public static ApiListaGuardada CargarApis()
        {
            try
            {
                string ruta = RutaApis();
                if (File.Exists(ruta))
                {
                    var datos = JsonSerializer.Deserialize<ApiListaGuardada>(File.ReadAllText(ruta));
                    if (datos != null) return datos;
                }
            }
            catch { }
            return new ApiListaGuardada();
        }

        public static void GuardarApis(ApiListaGuardada lista)
        {
            var opciones = new JsonSerializerOptions { WriteIndented = true };
            try
            {
                File.WriteAllText(RutaApis(), JsonSerializer.Serialize(lista, opciones));
            }
            catch (UnauthorizedAccessException)
            {
                DialogoAutoConfirmar.Aviso(
                    "No hay permisos para guardar en la carpeta del programa.\nEjecuta la aplicación como administrador e inténtalo de nuevo.",
                    "Permiso denegado");
            }
        }

        public static ApiConfig? ObtenerApiActiva()
        {
            var lista = CargarApis();
            return lista.Apis.FirstOrDefault(a => a.Nombre == lista.ActivaNombre) ?? lista.Apis.FirstOrDefault();
        }

        // -----------------------------------------------------------------------
        // Extrae datos de una factura/ticket usando la API activa
        // Devuelve un DatosTicket con todos los campos posibles rellenos
        // -----------------------------------------------------------------------
        public static async Task<DatosTicket> ExtraerDatosFactura(Mat imagen)
        {
            var apiActiva = ObtenerApiActiva();
            if (apiActiva == null || string.IsNullOrWhiteSpace(apiActiva.Url) || string.IsNullOrWhiteSpace(apiActiva.ApiKey))
                throw new Exception("No hay ninguna API configurada. Añade una desde el menú de gestión de APIs.");

            // Convertir Mat a byte[] base64
            Cv2.ImEncode(".jpg", imagen, out byte[] imageBytes);
            string base64Image = Convert.ToBase64String(imageBytes);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/jpeg",
                                    data = base64Image
                                }
                            },
                            new
                            {
                                text = @"Eres un experto en procesamiento de facturas, albaranes y tickets.
Extrae TODA la información visible en esta imagen de forma estructurada.
Primero identifica el TIPO de documento:
- ""factura"": lleva desglose de base imponible, IVA y total a pagar.
- ""albaran"": es un documento de entrega/recepción de mercancía, normalmente SIN desglose de IVA ni importe total a pagar (puede decir ""Albarán"", ""Nota de entrega"", etc.).
- ""ticket"": recibo simplificado de compra (comercio, gasolinera...).
Si el documento se titula literalmente ""Albarán"" o ""Nota de entrega"", es ""albaran"" aunque incluya precios, importes o un total (muchos albaranes sí los llevan, sin ser por eso una factura formal).
Solo es ""factura"" si además lleva un desglose formal de base imponible + IVA (o se titula ""Factura""/""Factura simplificada"").
Devuelve ÚNICAMENTE un JSON válido sin texto adicional ni bloques de código markdown, con esta estructura exacta:
{
  ""tipo_documento"": ""factura"",
  ""numero_factura"": """",
  ""fecha_emision"": """",
  ""fecha_vencimiento"": """",
  ""emisor"": {
    ""nombre"": """",
    ""cif_nif"": """",
    ""direccion"": """",
    ""telefono"": """"
  },
  ""receptor"": {
    ""nombre"": """",
    ""cif_nif"": """",
    ""direccion"": """"
  },
  ""items"": [
    {
      ""descripcion"": """",
      ""cantidad"": 0,
      ""precio_unitario"": 0.0,
      ""subtotal"": 0.0
    }
  ],
  ""base_imponible"": """",
  ""iva_importe"": """",
  ""iva_porcentaje"": """",
  ""total"": """",
  ""metodo_pago"": """"
}
Importante: ""iva_importe"" es SIEMPRE la cantidad en euros de IVA (no el %). ""iva_porcentaje"" es el tipo aplicado (ej. 21), si aparece.
Si un campo no aparece en el documento, déjalo vacío o a 0. No inventes datos."
                            }
                        }
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{apiActiva.Url}?key={apiActiva.ApiKey}", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                RegistrarAccesoApi(apiActiva.Nombre, exito: false);
                throw new Exception($"Error API Gemini: {response.StatusCode} - {responseBody}");
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                string rawJson = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "{}";

                // Limpiar posibles bloques de código que Gemini pueda devolver
                rawJson = rawJson.Trim();
                if (rawJson.StartsWith("```"))
                {
                    rawJson = rawJson.Substring(rawJson.IndexOf('\n') + 1);
                    rawJson = rawJson.Substring(0, rawJson.LastIndexOf("```")).Trim();
                }

                var resultado = MapearADatosTicket(rawJson);
                RegistrarAccesoApi(apiActiva.Nombre, exito: true);
                return resultado;
            }
            catch
            {
                RegistrarAccesoApi(apiActiva.Nombre, exito: false);
                throw;
            }
        }

        // -----------------------------------------------------------------------
        // Anota en apis.json la fecha de último acceso (éxito) o último fallo
        // de la API indicada por nombre.
        // -----------------------------------------------------------------------
        private static void RegistrarAccesoApi(string nombreApi, bool exito)
        {
            try
            {
                var lista = CargarApis();
                var api = lista.Apis.FirstOrDefault(a => a.Nombre == nombreApi);
                if (api == null) return;

                string ahora = DateTime.Now.ToString("o");
                if (exito) { api.UltimoAcceso = ahora; api.PeticionesOk++; }
                else api.UltimoFallo = ahora;

                GuardarApis(lista);
            }
            catch { }
        }

        // -----------------------------------------------------------------------
        // Mapea el JSON de Gemini a DatosTicket
        // -----------------------------------------------------------------------
        // -----------------------------------------------------------------------
        // Normaliza cualquier fecha (con puntos, guiones o barras, año de 2 o 4
        // dígitos) al formato dd/MM/yyyy usado en el resto de la aplicación.
        // -----------------------------------------------------------------------
        private static string NormalizarFecha(string fecha)
        {
            if (string.IsNullOrWhiteSpace(fecha)) return fecha;

            var m = System.Text.RegularExpressions.Regex.Match(fecha, @"^(\d{2})[-/.](\d{2})[-/.](\d{2,4})$");
            if (m.Success)
            {
                string dia = m.Groups[1].Value;
                string mes = m.Groups[2].Value;
                string anio = m.Groups[3].Value;
                if (anio.Length == 2) anio = "20" + anio;
                return $"{dia}/{mes}/{anio}";
            }

            // Formato "23 Abril 2026" / "23 de Abril de 2026" (Gemini a veces
            // devuelve el mes en texto en vez de numérico).
            var mTexto = System.Text.RegularExpressions.Regex.Match(
                fecha, @"^(\d{1,2})\s*(?:de\s+)?([A-Za-zÁÉÍÓÚáéíóú]+)\s*(?:de\s+)?(\d{4})$");
            if (mTexto.Success)
            {
                var meses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {"enero","01"},{"febrero","02"},{"marzo","03"},{"abril","04"},
                    {"mayo","05"},{"junio","06"},{"julio","07"},{"agosto","08"},
                    {"septiembre","09"},{"setiembre","09"},{"octubre","10"},
                    {"noviembre","11"},{"diciembre","12"}
                };
                if (meses.TryGetValue(mTexto.Groups[2].Value, out string? mesNum))
                {
                    string dia = mTexto.Groups[1].Value.PadLeft(2, '0');
                    return $"{dia}/{mesNum}/{mTexto.Groups[3].Value}";
                }
            }

            return fecha;
        }

        private static DatosTicket MapearADatosTicket(string rawJson)
        {
            var datos = new DatosTicket();
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                datos.Numero = LeerString(root, "numero_factura");
                string tipo = LeerString(root, "tipo_documento").ToLowerInvariant();
                datos.TipoDocumento = (tipo == "albaran" || tipo == "ticket") ? tipo : "factura";
                datos.Fecha = NormalizarFecha(LeerString(root, "fecha_emision"));
                datos.FechaVencimiento = NormalizarFecha(LeerString(root, "fecha_vencimiento"));
                datos.Total = LeerString(root, "total");
                datos.Base = LeerString(root, "base_imponible");
                string ivaImporteStr = LeerString(root, "iva_importe");
                double ivaPct = LeerDouble(root, "iva_porcentaje");
                double baseNum = LeerDouble(root, "base_imponible");
                if (!string.IsNullOrWhiteSpace(ivaImporteStr))
                    datos.Iva = ivaImporteStr;
                else if (ivaPct > 0 && baseNum > 0)
                    datos.Iva = (baseNum * ivaPct / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                else
                    datos.Iva = "";
                datos.IvaPorcentaje = ivaPct;
                datos.MetodoPago = LeerString(root, "metodo_pago");

                // Emisor
                if (root.TryGetProperty("emisor", out var emisor))
                {
                    datos.Empresa = LeerString(emisor, "nombre");
                    datos.Cif = LeerString(emisor, "cif_nif");
                    datos.Direccion = LeerString(emisor, "direccion");
                    datos.Telefono = LeerString(emisor, "telefono");
                }

                // Receptor
                if (root.TryGetProperty("receptor", out var receptor))
                {
                    datos.ReceptorNombre = LeerString(receptor, "nombre");
                    datos.ReceptorCif = LeerString(receptor, "cif_nif");
                    datos.ReceptorDireccion = LeerString(receptor, "direccion");
                }

                // Items
                if (root.TryGetProperty("items", out var itemsEl) &&
                    itemsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsEl.EnumerateArray())
                    {
                        var it = new ItemFactura
                        {
                            Descripcion = LeerString(item, "descripcion"),
                            Cantidad = LeerDouble(item, "cantidad"),
                            PrecioUnitario = LeerDouble(item, "precio_unitario"),
                            Subtotal = LeerDouble(item, "subtotal")
                        };
                        // Solo añadir items con descripción real
                        if (!string.IsNullOrWhiteSpace(it.Descripcion))
                            datos.Items.Add(it);
                    }
                }
            }
            catch (Exception ex)
            {
                datos.ErrorDiagnostico = $"Error mapeando respuesta Gemini: {ex.Message}";
            }
            return datos;
        }

        // -----------------------------------------------------------------------
        // Helpers de lectura segura de JSON
        // -----------------------------------------------------------------------
        private static string LeerString(JsonElement el, string propiedad)
        {
            if (!el.TryGetProperty(propiedad, out var prop)) return "";
            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString()?.Trim() ?? "";
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "";
        }

        private static double LeerDouble(JsonElement el, string propiedad)
        {
            if (el.TryGetProperty(propiedad, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetDouble();
                if (prop.ValueKind == JsonValueKind.String &&
                    double.TryParse(prop.GetString()?.Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double v))
                    return v;
            }
            return 0;
        }

        // =========================================================================
        // FORMULARIO DE GESTIÓN DE APIs (mismo patrón que los diálogos de cámara:
        // Form construido dinámicamente, sin designer)
        // =========================================================================
        public static void AbrirGestionApis(Control? owner = null)
        {
            var lista = CargarApis();

            using var dlg = new Form
            {
                Text = "Gestión de APIs",
                Width = 820,
                Height = 420,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lstApis = new ListView { Left = 20, Top = 20, Width = 520, Height = 300, View = View.Details, FullRowSelect = true, GridLines = true };
            lstApis.Columns.Add("Nombre", 140);
            lstApis.Columns.Add("Último acceso", 170);
            lstApis.Columns.Add("Último fallo", 170);
            lstApis.Columns.Add("Peticiones OK", 100);
            void RefrescarLista(string? seleccionar = null)
            {
                lstApis.Items.Clear();
                foreach (var a in lista.Apis)
                {
                    string etiqueta = a.Nombre == lista.ActivaNombre ? $"● {a.Nombre} (activa)" : a.Nombre;
                    var item = new ListViewItem(etiqueta);
                    item.SubItems.Add(FormatearFechaRelativa(a.UltimoAcceso));
                    item.SubItems.Add(FormatearFechaRelativa(a.UltimoFallo));
                    item.SubItems.Add(a.PeticionesOk.ToString());
                    lstApis.Items.Add(item);
                }
                if (seleccionar != null)
                {
                    int idx = lista.Apis.FindIndex(a => a.Nombre == seleccionar);
                    if (idx >= 0) lstApis.Items[idx].Selected = true;
                }
            }
            RefrescarLista();
            dlg.Controls.Add(lstApis);

            var btnActivar = new Button { Text = "Usar esta", Left = 560, Top = 20, Width = 180, Height = 30 };
            var btnAnadir = new Button { Text = "Añadir nueva", Left = 560, Top = 60, Width = 180, Height = 30 };
            var btnEditar = new Button { Text = "Editar", Left = 560, Top = 100, Width = 180, Height = 30 };
            var btnEliminar = new Button { Text = "Eliminar", Left = 560, Top = 140, Width = 180, Height = 30 };
            dlg.Controls.Add(btnActivar);
            dlg.Controls.Add(btnAnadir);
            dlg.Controls.Add(btnEditar);
            dlg.Controls.Add(btnEliminar);

            var btnCerrar = new Button { Text = "Cerrar", Left = 560, Top = 330, Width = 180, Height = 30, DialogResult = DialogResult.Cancel };
            dlg.Controls.Add(btnCerrar);
            dlg.CancelButton = btnCerrar;

            btnActivar.Click += (s, e) =>
            {
                if (lstApis.SelectedIndices.Count == 0) return;
                lista.ActivaNombre = lista.Apis[lstApis.SelectedIndices[0]].Nombre;
                GuardarApis(lista);
                RefrescarLista(lista.ActivaNombre);
            };

            btnAnadir.Click += (s, e) =>
            {
                var nueva = MostrarDialogoEditarApi(null);
                if (nueva == null) return;
                if (lista.Apis.Any(a => a.Nombre == nueva.Nombre))
                {
                    MessageBox.Show("Ya existe una API con ese nombre.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                lista.Apis.Add(nueva);
                if (lista.Apis.Count == 1) lista.ActivaNombre = nueva.Nombre;
                GuardarApis(lista);
                RefrescarLista(nueva.Nombre);
            };

            btnEditar.Click += (s, e) =>
            {
                if (lstApis.SelectedIndices.Count == 0) return;
                var actual = lista.Apis[lstApis.SelectedIndices[0]];
                var editada = MostrarDialogoEditarApi(actual);
                if (editada == null) return;
                string nombreActivaPrevia = lista.ActivaNombre;
                lista.Apis[lstApis.SelectedIndices[0]] = editada;
                if (nombreActivaPrevia == actual.Nombre) lista.ActivaNombre = editada.Nombre;
                GuardarApis(lista);
                RefrescarLista(editada.Nombre);
            };

            btnEliminar.Click += (s, e) =>
            {
                if (lstApis.SelectedIndices.Count == 0) return;
                var actual = lista.Apis[lstApis.SelectedIndices[0]];
                var confirmar = MessageBox.Show($"¿Eliminar la API \"{actual.Nombre}\"?", "Confirmar",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirmar != DialogResult.Yes) return;

                lista.Apis.RemoveAt(lstApis.SelectedIndices[0]);
                if (lista.ActivaNombre == actual.Nombre)
                    lista.ActivaNombre = lista.Apis.FirstOrDefault()?.Nombre ?? "";
                GuardarApis(lista);
                RefrescarLista(lista.ActivaNombre);
            };

            dlg.ShowDialog(owner);
        }

        // -----------------------------------------------------------------------
        // Formatea una fecha ISO guardada como "dd/MM/yyyy - hace X días/horas/
        // minutos/segundos". Devuelve "—" si está vacía o no es parseable.
        // -----------------------------------------------------------------------
        private static string FormatearFechaRelativa(string fechaIso)
        {
            if (string.IsNullOrWhiteSpace(fechaIso)) return "—";
            if (!DateTime.TryParse(fechaIso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime fecha))
                return "—";

            TimeSpan transcurrido = DateTime.Now - fecha;
            string relativo;
            if (transcurrido.TotalDays >= 1)
                relativo = $"hace {(int)transcurrido.TotalDays} día{((int)transcurrido.TotalDays == 1 ? "" : "s")}";
            else if (transcurrido.TotalHours >= 1)
                relativo = $"hace {(int)transcurrido.TotalHours} hora{((int)transcurrido.TotalHours == 1 ? "" : "s")}";
            else if (transcurrido.TotalMinutes >= 1)
                relativo = $"hace {(int)transcurrido.TotalMinutes} minuto{((int)transcurrido.TotalMinutes == 1 ? "" : "s")}";
            else
                relativo = $"hace {Math.Max(0, (int)transcurrido.TotalSeconds)} segundo{((int)transcurrido.TotalSeconds == 1 ? "" : "s")}";

            return $"{fecha:dd/MM/yyyy} - {relativo}";
        }

        // -----------------------------------------------------------------------
        // Diálogo para añadir/editar una API concreta (Nombre, Url, ApiKey)
        // -----------------------------------------------------------------------
        private static ApiConfig? MostrarDialogoEditarApi(ApiConfig? existente)
        {
            using var dlg = new Form
            {
                Text = existente == null ? "Añadir API" : "Editar API",
                Width = 460,
                Height = 260,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            dlg.Controls.Add(new Label { Text = "Nombre:", Left = 20, Top = 20, Width = 100 });
            var txtNombre = new TextBox { Left = 130, Top = 18, Width = 290, Text = existente?.Nombre ?? "" };
            dlg.Controls.Add(txtNombre);

            dlg.Controls.Add(new Label { Text = "URL endpoint:", Left = 20, Top = 60, Width = 100 });
            var txtUrl = new TextBox { Left = 130, Top = 58, Width = 290, Text = existente?.Url ?? "" };
            dlg.Controls.Add(txtUrl);

            dlg.Controls.Add(new Label { Text = "API Key:", Left = 20, Top = 100, Width = 100 });
            var txtKey = new TextBox { Left = 130, Top = 98, Width = 290, Text = existente?.ApiKey ?? "", UseSystemPasswordChar = true };
            dlg.Controls.Add(txtKey);

            var chkVerKey = new CheckBox { Text = "Mostrar clave", Left = 130, Top = 128, Width = 150 };
            chkVerKey.CheckedChanged += (s, e) => txtKey.UseSystemPasswordChar = !chkVerKey.Checked;
            dlg.Controls.Add(chkVerKey);

            var btnAceptar = new Button { Text = "Aceptar", Left = 245, Top = 175, Width = 85, Height = 30, DialogResult = DialogResult.OK };
            var btnCancelar = new Button { Text = "Cancelar", Left = 340, Top = 175, Width = 85, Height = 30, DialogResult = DialogResult.Cancel };
            dlg.Controls.Add(btnAceptar);
            dlg.Controls.Add(btnCancelar);
            dlg.AcceptButton = btnAceptar;
            dlg.CancelButton = btnCancelar;

            if (dlg.ShowDialog() != DialogResult.OK) return null;

            if (string.IsNullOrWhiteSpace(txtNombre.Text) || string.IsNullOrWhiteSpace(txtUrl.Text) || string.IsNullOrWhiteSpace(txtKey.Text))
            {
                MessageBox.Show("Todos los campos son obligatorios.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            return new ApiConfig
            {
                Nombre = txtNombre.Text.Trim(),
                Url = txtUrl.Text.Trim(),
                ApiKey = txtKey.Text.Trim(),
                UltimoAcceso = existente?.UltimoAcceso ?? "",
                UltimoFallo = existente?.UltimoFallo ?? "",
                PeticionesOk = existente?.PeticionesOk ?? 0
            };
        }
    }
}