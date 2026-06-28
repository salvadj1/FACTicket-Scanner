using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using OpenCvSharp;

namespace FACTicket_Scanner
{
    public class Gemini
    {
       

        private static readonly HttpClient _httpClient = new HttpClient();

        // -----------------------------------------------------------------------
        // Extrae datos de una factura/ticket usando Gemini Vision
        // Devuelve un DatosTicket con todos los campos posibles rellenos
        // -----------------------------------------------------------------------
        public static async Task<DatosTicket> ExtraerDatosFactura(Mat imagen)
        {
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
                                text = @"Eres un experto en procesamiento de facturas y tickets.
Extrae TODA la información visible en esta imagen de forma estructurada.
Devuelve ÚNICAMENTE un JSON válido sin texto adicional ni bloques de código markdown, con esta estructura exacta:
{
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
  ""iva"": """",
  ""total"": """",
  ""metodo_pago"": """"
}
Si un campo no aparece en el documento, déjalo vacío o a 0. No inventes datos."
                            }
                        }
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{DatosSecretos. ApiUrl}?key={DatosSecretos.ApiKey}", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error API Gemini: {response.StatusCode} - {responseBody}");

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

            return MapearADatosTicket(rawJson);
        }

        // -----------------------------------------------------------------------
        // Mapea el JSON de Gemini a DatosTicket
        // -----------------------------------------------------------------------
        private static DatosTicket MapearADatosTicket(string rawJson)
        {
            var datos = new DatosTicket();
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                datos.Numero = LeerString(root, "numero_factura");
                datos.Fecha = LeerString(root, "fecha_emision");
                datos.FechaVencimiento = LeerString(root, "fecha_vencimiento");
                datos.Total = LeerString(root, "total");
                datos.Base = LeerString(root, "base_imponible");
                datos.Iva = LeerString(root, "iva");
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
            if (el.TryGetProperty(propiedad, out var prop) &&
                prop.ValueKind == JsonValueKind.String)
                return prop.GetString()?.Trim() ?? "";
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
    }
}
