using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Reglas configurables para decidir si un documento (factura, albarán,
    // ticket...) es duplicado de otro. Un par de documentos se considera
    // duplicado si coinciden TODAS las reglas activas que tienen valor en el
    // documento nuevo, y hay al menos MinimoReglas reglas comparables.
    //
    // Clase autónoma y reutilizable: no depende del resto de la aplicación,
    // solo trabaja con cadenas (número, fecha, total, empresa).
    //
    // Ejemplo:
    //   var reglas = new ReglasDuplicados { Empresa = false };
    //   bool dup = reglas.SonDuplicados("002379", "11/07/2026", "961,95", "",
    //                                   "2379",   "11-07-2026", "961.95", "");
    //   // dup == true (Nº, fecha y total normalizados coinciden)
    // -----------------------------------------------------------------------
    public sealed class ReglasDuplicados
    {
        // Mínimo de reglas comparables necesarias para poder afirmar duplicado.
        public const int MinimoReglas = 2;

        // Reglas activas (por defecto: Nº + Fecha + Total; empresa desactivada).
        public bool Numero { get; set; } = true;
        public bool Fecha { get; set; } = true;
        public bool Total { get; set; } = true;
        public bool Empresa { get; set; } = false;

        // Número de reglas marcadas actualmente.
        public int CantidadActivas =>
            (Numero ? 1 : 0) + (Fecha ? 1 : 0) + (Total ? 1 : 0) + (Empresa ? 1 : 0);

        // -------------------------------------------------------------------
        // Compara un documento nuevo (A) con uno existente (B).
        //  - Una regla activa cuyo valor en A está vacío se ignora.
        //  - Si el valor de A existe y el de B difiere o falta → NO duplicado.
        //  - Devuelve true solo si coinciden al menos MinimoReglas reglas.
        // -------------------------------------------------------------------
        public bool SonDuplicados(
            string? numeroA, string? fechaA, string? totalA, string? empresaA,
            string? numeroB, string? fechaB, string? totalB, string? empresaB)
        {
            int comparadas = 0;
            if (!Coincide(Numero, NormalizarNumero(numeroA), NormalizarNumero(numeroB), ref comparadas)) return false;
            if (!Coincide(Fecha, NormalizarFecha(fechaA), NormalizarFecha(fechaB), ref comparadas)) return false;
            if (!Coincide(Total, NormalizarTotal(totalA), NormalizarTotal(totalB), ref comparadas)) return false;
            if (!Coincide(Empresa, NormalizarTexto(empresaA), NormalizarTexto(empresaB), ref comparadas)) return false;
            return comparadas >= MinimoReglas;
        }

        // Evalúa una regla: true = no contradice (ignorada o coincide).
        private static bool Coincide(bool activa, string a, string b, ref int comparadas)
        {
            if (!activa || a.Length == 0) return true; // regla ignorada
            if (a != b) return false;
            comparadas++;
            return true;
        }

        // -------------------------------------------------------------------
        // Nº de factura: solo letras/dígitos en mayúsculas, sin ceros a la
        // izquierda. "0023-79" y "2379" → "2379".
        // -------------------------------------------------------------------
        public static string NormalizarNumero(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";
            var sb = new StringBuilder();
            foreach (char c in valor)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString().TrimStart('0');
        }

        // Formatos de fecha admitidos (día antes que mes, como en España).
        private static readonly string[] FormatosFecha =
        {
            "d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "d/M/yy", "d-M-yy", "d.M.yy",
            "yyyy-M-d", "yyyy/M/d", "yyyyMMdd"
        };

        // -------------------------------------------------------------------
        // Fecha: se convierte a "yyyy-MM-dd". Si no se puede interpretar, se
        // usan solo sus dígitos como último recurso.
        // -------------------------------------------------------------------
        public static string NormalizarFecha(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";
            string v = valor.Trim();
            if (DateTime.TryParseExact(v, FormatosFecha, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime fecha))
                return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new string(v.Where(char.IsDigit).ToArray());
        }

        // -------------------------------------------------------------------
        // Total: se convierte a número con 2 decimales. Entiende "961,95",
        // "961.95", "1.234,50", "1,234.50" y símbolos de moneda.
        // Un único separador con 3 dígitos detrás se toma como de miles.
        // -------------------------------------------------------------------
        public static string NormalizarTotal(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";

            var sb = new StringBuilder();
            foreach (char c in valor)
                if (char.IsDigit(c) || c == ',' || c == '.' || c == '-') sb.Append(c);
            string s = sb.ToString();
            if (s.Length == 0) return "";

            int ultimaComa = s.LastIndexOf(',');
            int ultimoPunto = s.LastIndexOf('.');
            char? decimalSep = null;

            if (ultimaComa >= 0 && ultimoPunto >= 0)
                decimalSep = ultimaComa > ultimoPunto ? ',' : '.';
            else if (ultimaComa >= 0 || ultimoPunto >= 0)
            {
                char sep = ultimaComa >= 0 ? ',' : '.';
                int pos = s.LastIndexOf(sep);
                int apariciones = s.Count(ch => ch == sep);
                int decimales = s.Length - pos - 1;
                if (apariciones == 1 && decimales != 3) decimalSep = sep;
            }

            var limpio = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsDigit(c) || c == '-') limpio.Append(c);
                else if (decimalSep.HasValue && c == decimalSep.Value && limpio.ToString().IndexOf('.') < 0)
                    limpio.Append('.');
            }

            if (decimal.TryParse(limpio.ToString(), NumberStyles.Number | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out decimal d))
                return d.ToString("F2", CultureInfo.InvariantCulture);
            return limpio.ToString();
        }

        // Texto libre (empresa): recorta, colapsa espacios y pasa a mayúsculas.
        public static string NormalizarTexto(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";
            return string.Join(" ", valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                         .ToUpperInvariant();
        }
    }
}
