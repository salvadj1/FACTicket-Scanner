using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FACTicket_Scanner
{
    public class ItemFactura
    {
        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = "";

        [JsonPropertyName("cantidad")]
        public double Cantidad { get; set; } = 0;

        [JsonPropertyName("precio_unitario")]
        public double PrecioUnitario { get; set; } = 0;

        [JsonPropertyName("subtotal")]
        public double Subtotal { get; set; } = 0;
    }

    public class DatosTicket
    {
        [JsonPropertyName("empresa")]
        public string Empresa { get; set; } = "";

        [JsonPropertyName("fecha")]
        public string Fecha { get; set; } = "";

        [JsonPropertyName("fecha_vencimiento")]
        public string FechaVencimiento { get; set; } = "";

        [JsonPropertyName("numero")]
        public string Numero { get; set; } = "";

        // "factura", "albaran" o "ticket". Determinado por Gemini al extraer
        // (ver GeminiAPI.MapearADatosTicket); editable en la revisión manual.
        // Los albaranes se guardan aparte y no cuentan en gasto/IVA.
        [JsonPropertyName("tipo_documento")]
        public string TipoDocumento { get; set; } = "factura";

        [JsonPropertyName("cif")]
        public string Cif { get; set; } = "";

        [JsonPropertyName("direccion")]
        public string Direccion { get; set; } = "";

        [JsonPropertyName("telefono")]
        public string Telefono { get; set; } = "";

        [JsonPropertyName("receptor_nombre")]
        public string ReceptorNombre { get; set; } = "";

        [JsonPropertyName("receptor_cif")]
        public string ReceptorCif { get; set; } = "";

        [JsonPropertyName("receptor_direccion")]
        public string ReceptorDireccion { get; set; } = "";

        [JsonPropertyName("base")]
        public string Base { get; set; } = "";

        [JsonPropertyName("iva")]
        public string Iva { get; set; } = "";

        // Tipo de IVA aplicado (ej. 21), informativo. El importe en € sigue
        // siendo el campo Iva de arriba (el que usa HtmlBuilder para sumar).
        [JsonPropertyName("iva_porcentaje")]
        public double IvaPorcentaje { get; set; } = 0;

        [JsonPropertyName("total")]
        public string Total { get; set; } = "";

        [JsonPropertyName("metodo_pago")]
        public string MetodoPago { get; set; } = "";

        [JsonPropertyName("items")]
        public List<ItemFactura> Items { get; set; } = new List<ItemFactura>();

        [JsonPropertyName("imagen")]
        public string ImagenRelativa { get; set; } = "";

        [JsonPropertyName("pdf")]
        public string PdfRelativa { get; set; } = "";

        [JsonPropertyName("json")]
        public string JsonRelativa { get; set; } = "";

        [JsonPropertyName("fecha_guardado")]
        public string FechaGuardado { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Huella perceptual (pHash) de la imagen original, para detectar
        // fotos repetidas sin llamar a Gemini. Opcional: los datos.json
        // guardados antes de esta propiedad simplemente la cargan vacía.
        [JsonPropertyName("phash")]
        public string PHash { get; set; } = "";

        // Uso interno: diagnóstico de fallo OCR (Bug C). No se serializa al
        // JSON final porque JsonIgnore lo excluye explícitamente.
        [JsonIgnore]
        public string? ErrorDiagnostico { get; set; } = null;

        // -----------------------------------------------------------------------
        // Cargar lista desde tickets.json
        // -----------------------------------------------------------------------
        public static List<DatosTicket> CargarLista(string rutaJson)
        {
            try
            {
                if (File.Exists(rutaJson))
                {
                    string json = File.ReadAllText(rutaJson);
                    var lista = JsonSerializer.Deserialize<List<DatosTicket>>(json);
                    if (lista != null) return lista;
                }
            }
            catch { }
            return new List<DatosTicket>();
        }

        // -----------------------------------------------------------------------
        // Guardar lista en tickets.json
        // -----------------------------------------------------------------------
        public static void GuardarLista(string rutaJson, List<DatosTicket> lista)
        {
            var opciones = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(rutaJson, JsonSerializer.Serialize(lista, opciones));
        }

        // -----------------------------------------------------------------------
        // Cargar/Guardar un único ticket (datos.json dentro de la carpeta de
        // cada factura, estructura Año/Empresa/Factura_x/datos.json)
        // -----------------------------------------------------------------------
        public static DatosTicket? CargarUnico(string rutaJson)
        {
            try
            {
                if (File.Exists(rutaJson))
                    return JsonSerializer.Deserialize<DatosTicket>(File.ReadAllText(rutaJson));
            }
            catch { }
            return null;
        }

        public static void GuardarUnico(string rutaJson, DatosTicket datos)
        {
            var opciones = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(rutaJson, JsonSerializer.Serialize(datos, opciones));
        }
    }
}