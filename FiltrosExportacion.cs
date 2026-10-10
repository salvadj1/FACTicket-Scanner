using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Estado de presentación (declaración trimestral) a filtrar.
    // -----------------------------------------------------------------------
    public enum FiltroPresentacion { Todas = 0, Presentadas = 1, NoPresentadas = 2 }

    // -----------------------------------------------------------------------
    // Situación respecto a la fecha de vencimiento del documento.
    // -----------------------------------------------------------------------
    public enum FiltroVencimiento { Todas = 0, Vencidas = 1, NoVencidas = 2, SinVencimiento = 3 }

    // -----------------------------------------------------------------------
    // Condición sobre la existencia de un archivo asociado (PDF, imagen...).
    // -----------------------------------------------------------------------
    public enum FiltroArchivo { Indiferente = 0, Con = 1, Sin = 2 }

    // -----------------------------------------------------------------------
    // Conjunto de criterios de filtrado para exportar documentos.
    //
    // Clase REUTILIZABLE y sin dependencias de interfaz: se rellenan las
    // propiedades, se llama a Cumple() por cada documento y listo. Todo
    // criterio que se deja en su valor por defecto (null, vacío, "Todas",
    // "Indiferente") NO filtra nada.
    // -----------------------------------------------------------------------
    public class FiltrosExportacion
    {
        // Rango de fechas del documento (null = sin límite por ese lado).
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }

        // true = los documentos sin fecha legible pasan el filtro de fechas;
        // false = se excluyen cuando hay un rango definido.
        public bool IncluirSinFecha { get; set; } = true;

        // Nombre exacto de empresa (sin distinguir mayúsculas). Vacío = todas.
        public string Empresa { get; set; } = "";

        // Estado de presentación y, opcionalmente, trimestre concreto ("2026-2T").
        // Indicar un trimestre implica que el documento esté presentado en él.
        public FiltroPresentacion Presentacion { get; set; } = FiltroPresentacion.Todas;
        public string Trimestre { get; set; } = "";

        // Tipos de documento permitidos ("factura", "albaran", "ticket").
        // Vacío = todos los tipos.
        public HashSet<string> Tipos { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Rango del importe total (null = sin límite).
        public double? ImporteMin { get; set; }
        public double? ImporteMax { get; set; }

        // Texto libre: se busca (sin tildes ni mayúsculas) en empresa, CIF,
        // número, receptor y descripción de las líneas. Vacío = no filtra.
        public string Texto { get; set; } = "";

        // Método de pago exacto (sin distinguir mayúsculas). Vacío = todos.
        public string MetodoPago { get; set; } = "";

        // Tipo de IVA aplicado, p. ej. 21. null = cualquiera.
        public double? IvaPorcentaje { get; set; }

        // Situación respecto al vencimiento, tomando "Hoy" como referencia.
        public FiltroVencimiento Vencimiento { get; set; } = FiltroVencimiento.Todas;
        public DateTime Hoy { get; set; } = DateTime.Today;

        // Presencia de PDF / imagen procesada en disco.
        public FiltroArchivo Pdf { get; set; } = FiltroArchivo.Indiferente;
        public FiltroArchivo Imagen { get; set; } = FiltroArchivo.Indiferente;

        // -------------------------------------------------------------------
        // Indica si un documento cumple TODOS los criterios activos.
        //
        // Parámetros:
        //   t            : datos del documento.
        //   fechaTicket  : fecha ya interpretada del documento (null si ilegible);
        //                  se puede obtener con ParsearFecha(t.Fecha).
        //   carpetaBase  : carpeta raíz contra la que se resuelven las rutas
        //                  relativas de PDF/imagen (solo se usa si se filtra
        //                  por presencia de archivos).
        // -------------------------------------------------------------------
        public bool Cumple(DatosTicket t, DateTime? fechaTicket, string carpetaBase)
        {
            // --- Fechas ---
            if (Desde.HasValue || Hasta.HasValue)
            {
                if (!fechaTicket.HasValue)
                {
                    if (!IncluirSinFecha) return false;
                }
                else
                {
                    DateTime f = fechaTicket.Value.Date;
                    if (Desde.HasValue && f < Desde.Value.Date) return false;
                    if (Hasta.HasValue && f > Hasta.Value.Date) return false;
                }
            }
            else if (!fechaTicket.HasValue && !IncluirSinFecha) return false;

            // --- Empresa ---
            if (!string.IsNullOrWhiteSpace(Empresa) &&
                !string.Equals((t.Empresa ?? "").Trim(), Empresa.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            // --- Presentación / trimestre ---
            if (Presentacion == FiltroPresentacion.Presentadas && !t.Presentado) return false;
            if (Presentacion == FiltroPresentacion.NoPresentadas && t.Presentado) return false;
            if (!string.IsNullOrWhiteSpace(Trimestre) &&
                !(t.Presentado && string.Equals((t.TrimestrePresentado ?? "").Trim(), Trimestre.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
                return false;

            // --- Tipo de documento (vacío en el JSON = "factura") ---
            if (Tipos.Count > 0)
            {
                string tipo = string.IsNullOrWhiteSpace(t.TipoDocumento) ? "factura" : t.TipoDocumento.Trim();
                if (!Tipos.Contains(tipo)) return false;
            }

            // --- Importe ---
            if (ImporteMin.HasValue || ImporteMax.HasValue)
            {
                double total = ParsearImporte(t.Total);
                if (ImporteMin.HasValue && total < ImporteMin.Value) return false;
                if (ImporteMax.HasValue && total > ImporteMax.Value) return false;
            }

            // --- Método de pago ---
            if (!string.IsNullOrWhiteSpace(MetodoPago) &&
                !string.Equals((t.MetodoPago ?? "").Trim(), MetodoPago.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            // --- Tipo de IVA ---
            if (IvaPorcentaje.HasValue && Math.Abs(t.IvaPorcentaje - IvaPorcentaje.Value) > 0.01) return false;

            // --- Vencimiento ---
            if (Vencimiento != FiltroVencimiento.Todas)
            {
                DateTime? venc = ParsearFecha(t.FechaVencimiento);
                switch (Vencimiento)
                {
                    case FiltroVencimiento.SinVencimiento: if (venc.HasValue) return false; break;
                    case FiltroVencimiento.Vencidas: if (!venc.HasValue || venc.Value.Date >= Hoy.Date) return false; break;
                    case FiltroVencimiento.NoVencidas: if (!venc.HasValue || venc.Value.Date < Hoy.Date) return false; break;
                }
            }

            // --- Archivos asociados ---
            if (Pdf != FiltroArchivo.Indiferente && (ExisteArchivo(carpetaBase, t.PdfRelativa) != (Pdf == FiltroArchivo.Con)))
                return false;
            if (Imagen != FiltroArchivo.Indiferente && (ExisteArchivo(carpetaBase, t.ImagenRelativa) != (Imagen == FiltroArchivo.Con)))
                return false;

            // --- Texto libre ---
            if (!string.IsNullOrWhiteSpace(Texto) && !CoincideTexto(t, Texto)) return false;

            return true;
        }

        // -------------------------------------------------------------------
        // Número de criterios "avanzados" activos (todo salvo el rango de
        // fechas y la empresa). Útil para mostrar un indicador en la interfaz.
        // -------------------------------------------------------------------
        public int ContarAvanzadosActivos()
        {
            int n = 0;
            if (Presentacion != FiltroPresentacion.Todas) n++;
            if (!string.IsNullOrWhiteSpace(Trimestre)) n++;
            if (Tipos.Count > 0) n++;
            if (ImporteMin.HasValue || ImporteMax.HasValue) n++;
            if (!string.IsNullOrWhiteSpace(Texto)) n++;
            if (!string.IsNullOrWhiteSpace(MetodoPago)) n++;
            if (IvaPorcentaje.HasValue) n++;
            if (Vencimiento != FiltroVencimiento.Todas) n++;
            if (Pdf != FiltroArchivo.Indiferente) n++;
            if (Imagen != FiltroArchivo.Indiferente) n++;
            if (!IncluirSinFecha) n++;
            return n;
        }

        // -------------------------------------------------------------------
        // true si el archivo (ruta relativa a carpetaBase) existe en disco.
        // -------------------------------------------------------------------
        public static bool ExisteArchivo(string carpetaBase, string? rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa)) return false;
            try { return File.Exists(Path.Combine(carpetaBase, rutaRelativa)); }
            catch { return false; }
        }

        // -------------------------------------------------------------------
        // Busca el texto (todas sus palabras deben aparecer) en los campos
        // principales del documento, ignorando tildes y mayúsculas.
        // -------------------------------------------------------------------
        private static bool CoincideTexto(DatosTicket t, string texto)
        {
            var sb = new StringBuilder();
            sb.Append(t.Empresa).Append(' ').Append(t.Cif).Append(' ').Append(t.Numero).Append(' ')
              .Append(t.ReceptorNombre).Append(' ').Append(t.ReceptorCif).Append(' ').Append(t.Direccion);
            if (t.Items != null)
                foreach (var it in t.Items) sb.Append(' ').Append(it.Descripcion);

            string pajar = Normalizar(sb.ToString());
            foreach (string palabra in Normalizar(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (!pajar.Contains(palabra)) return false;
            return true;
        }

        // -------------------------------------------------------------------
        // Minúsculas y sin tildes/diacríticos ("Camión" -> "camion").
        // -------------------------------------------------------------------
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            string d = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private static readonly string[] FormatosFecha =
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "yyyy/MM/dd"
        };

        // -------------------------------------------------------------------
        // Interpreta una fecha en texto con los formatos habituales de los
        // documentos. Devuelve null si está vacía o no se puede leer.
        // -------------------------------------------------------------------
        public static DateTime? ParsearFecha(string? fecha)
        {
            if (string.IsNullOrWhiteSpace(fecha)) return null;
            string s = fecha.Trim();
            if (DateTime.TryParseExact(s, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime f))
                return f;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out f))
                return f;
            return null;
        }

        // -------------------------------------------------------------------
        // Convierte un importe en texto ("1.234,56 €", "34.20", "12,5") a
        // double. Con '.' y ',' a la vez, el último separador es el decimal;
        // con un solo tipo, ',' es decimal y '.' solo si tiene <=2 decimales
        // (si no, se toma como separador de miles). Devuelve 0 si no se puede.
        // -------------------------------------------------------------------
        public static double ParsearImporte(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return 0;

            string s = new string(texto.Where(c => char.IsDigit(c) || c == ',' || c == '.' || c == '-').ToArray());
            if (s.Length == 0) return 0;

            int ultimaComa = s.LastIndexOf(',');
            int ultimoPunto = s.LastIndexOf('.');

            if (ultimaComa >= 0 && ultimoPunto >= 0)
            {
                if (ultimaComa > ultimoPunto) s = s.Replace(".", "").Replace(',', '.');
                else s = s.Replace(",", "");
            }
            else if (ultimaComa >= 0)
            {
                s = s.Replace(".", "").Replace(',', '.');
            }
            else if (ultimoPunto >= 0)
            {
                int decimales = s.Length - ultimoPunto - 1;
                bool variosPuntos = s.IndexOf('.') != ultimoPunto;
                if (variosPuntos || decimales == 3) s = s.Replace(".", "");
            }

            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
        }
    }
}
