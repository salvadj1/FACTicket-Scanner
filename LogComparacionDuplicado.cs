using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Candidato evaluado durante la búsqueda de duplicados por pHash.
    // Clase de datos simple, reutilizable en cualquier proyecto.
    // -----------------------------------------------------------------------
    internal sealed class CandidatoComparacion
    {
        public string Empresa { get; set; } = "";
        public string Numero { get; set; } = "";
        public string Fecha { get; set; } = "";
        public string FechaGuardado { get; set; } = "";
        public string PHash { get; set; } = "";

        /// <summary>Bits distintos respecto a la imagen nueva (distancia de Hamming).</summary>
        public int Distancia { get; set; }
    }

    // -----------------------------------------------------------------------
    // Registro de una comparación de imágenes por hash perceptual (pHash).
    //
    // Guarda TODOS los datos usados en la comparación (hashes, distancia,
    // umbral, tamaños, ruta de archivos, mejores candidatos...) y genera un
    // texto ordenado por secciones, pensado para mostrarse en pantalla y para
    // exportarse a .txt con el fin de investigar falsos positivos/negativos.
    //
    // Reutilizable: no depende de OpenCV ni de WinForms; solo de System.*.
    // Uso típico:
    //   var log = new LogComparacionDuplicado { HashNuevo = h, Umbral = 8, ... };
    //   string texto = log.Construir();
    // -----------------------------------------------------------------------
    internal sealed class LogComparacionDuplicado
    {
        // --- Datos generales ---
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string Algoritmo { get; set; } =
            "pHash: gris -> 32x32 -> DCT -> bloque 8x8 sin DC -> bit=1 si > mediana (63 bits)";
        public int Umbral { get; set; }
        public int TotalFacturasExistentes { get; set; }
        public int FacturasSinPHash { get; set; }

        // --- Imagen nueva (la DUPLICADA recién añadida) ---
        public string RutaImagenNueva { get; set; } = "";
        public int AnchoNueva { get; set; }
        public int AltoNueva { get; set; }
        public int CanalesNueva { get; set; }
        public string HashNuevo { get; set; } = "";

        // --- Imagen original (la ya guardada) ---
        public string EmpresaExistente { get; set; } = "";
        public string NumeroExistente { get; set; } = "";
        public string FechaExistente { get; set; } = "";
        public string TotalExistente { get; set; } = "";
        public string GuardadaExistente { get; set; } = "";
        public string RutaImagenExistente { get; set; } = "";
        public bool? ImagenExistenteCargada { get; set; }
        public int AnchoExistente { get; set; }
        public int AltoExistente { get; set; }
        public string HashExistente { get; set; } = "";

        // --- Resultado ---
        /// <summary>Distancia de Hamming elegida (-1 si no hubo coincidencia).</summary>
        public int Distancia { get; set; } = -1;

        /// <summary>Mejores candidatos ordenados de menor a mayor distancia.</summary>
        public List<CandidatoComparacion> Candidatos { get; set; } = new List<CandidatoComparacion>();

        // -------------------------------------------------------------------
        // Convierte una cadena de bits ("0101...") a hexadecimal (4 bits por
        // dígito, rellenando con ceros a la derecha). Devuelve "" si está vacía.
        // -------------------------------------------------------------------
        public static string BitsAHex(string bits)
        {
            if (string.IsNullOrEmpty(bits)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < bits.Length; i += 4)
            {
                string grupo = bits.Substring(i, Math.Min(4, bits.Length - i)).PadRight(4, '0');
                sb.Append(Convert.ToInt32(grupo, 2).ToString("X"));
            }
            return sb.ToString();
        }

        // -------------------------------------------------------------------
        // Devuelve las posiciones (base 0) en las que dos cadenas de bits
        // difieren. Si tienen distinta longitud, las posiciones sobrantes
        // también cuentan como distintas.
        // -------------------------------------------------------------------
        public static List<int> PosicionesDistintas(string a, string b)
        {
            var res = new List<int>();
            int max = Math.Max(a.Length, b.Length);
            for (int i = 0; i < max; i++)
            {
                char ca = i < a.Length ? a[i] : '?';
                char cb = i < b.Length ? b[i] : '?';
                if (ca != cb) res.Add(i);
            }
            return res;
        }

        // Parte una cadena en grupos de 'n' caracteres separados por espacio.
        private static string Agrupar(string s, int n = 8)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i += n)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(s.Substring(i, Math.Min(n, s.Length - i)));
            }
            return sb.ToString();
        }

        // Texto o "(no disponible)" cuando está vacío.
        private static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "(no disponible)" : s!;

        // -------------------------------------------------------------------
        // Construye el texto completo del log, organizado en secciones.
        // Usa saltos de línea de Windows (\r\n) para que se vea bien tanto en
        // un TextBox como en el Bloc de notas.
        // -------------------------------------------------------------------
        public string Construir()
        {
            var ci = CultureInfo.GetCultureInfo("es-ES");
            var sb = new StringBuilder();
            void L(string t = "") => sb.Append(t).Append("\r\n");

            int totalBits = Math.Max(HashNuevo.Length, HashExistente.Length);
            bool hayResultado = Distancia >= 0 && totalBits > 0;

            L("=== LOG DE COMPARACION DE IMAGENES (POSIBLE DUPLICADO) ===");
            L("Generado : " + FechaHora.ToString("dd/MM/yyyy HH:mm:ss", ci));
            L("Algoritmo: " + Algoritmo);
            L();

            L("--- IMAGEN NUEVA (DUPLICADA, recien anadida) ---");
            L("Archivo  : " + V(RutaImagenNueva));
            L("Tamano   : " + (AnchoNueva > 0 ? $"{AnchoNueva} x {AltoNueva} px, {CanalesNueva} canal(es)" : "(no disponible)"));
            L("pHash    : " + Agrupar(HashNuevo));
            L("pHash hex: " + BitsAHex(HashNuevo));
            L();

            L("--- IMAGEN ORIGINAL (ya guardada) ---");
            L("Empresa  : " + V(EmpresaExistente));
            L("N. Fact. : " + V(NumeroExistente));
            L("Fecha    : " + V(FechaExistente));
            L("Total    : " + V(TotalExistente));
            L("Guardada : " + V(GuardadaExistente));
            L("Archivo  : " + V(RutaImagenExistente));
            string estado = ImagenExistenteCargada == null ? "(sin comprobar)"
                          : ImagenExistenteCargada.Value ? "cargada correctamente" : "NO se pudo cargar (archivo ausente o danado)";
            L("Imagen   : " + estado);
            L("Tamano   : " + (AnchoExistente > 0 ? $"{AnchoExistente} x {AltoExistente} px" : "(no disponible)"));
            L("pHash    : " + Agrupar(HashExistente));
            L("pHash hex: " + BitsAHex(HashExistente));
            L();

            L("--- RESULTADO DE LA COMPARACION ---");
            if (hayResultado)
            {
                int coincide = totalBits - Distancia;
                double pct = totalBits > 0 ? coincide * 100.0 / totalBits : 0;
                L($"Distancia Hamming: {Distancia} bit(s) distintos de {totalBits}");
                L($"Coincidencia     : {coincide}/{totalBits} bits ({pct.ToString("0.0", ci)} %)");
                L($"Umbral           : <= {Umbral} bits => " + (Distancia <= Umbral ? "DUPLICADO" : "NO duplicado"));

                var pos = PosicionesDistintas(HashNuevo, HashExistente);
                L("Posiciones distintas (base 0): " + (pos.Count == 0 ? "ninguna" : string.Join(", ", pos)));
                if (pos.Count > 0 && HashNuevo.Length == HashExistente.Length)
                {
                    // Mapa visual: '^' bajo cada bit que difiere
                    var marca = new StringBuilder(new string(' ', HashNuevo.Length));
                    foreach (int p in pos) marca[p] = '^';
                    L("  nueva    : " + Agrupar(HashNuevo));
                    L("  original : " + Agrupar(HashExistente));
                    L("  diferen. : " + Agrupar(marca.ToString()));
                }
            }
            else
            {
                L("Sin coincidencia por debajo del umbral (<= " + Umbral + " bits).");
            }
            L();

            L("--- MEJORES CANDIDATOS EVALUADOS ---");
            if (Candidatos.Count == 0)
            {
                L("(ninguno: no hay facturas con pHash calculado)");
            }
            else
            {
                L(" #  Dist  Empresa / N. Fact. / Fecha / Guardada");
                int n = 1;
                foreach (var c in Candidatos.OrderBy(x => x.Distancia))
                    L($"{n++,2}  {c.Distancia,4}  {V(c.Empresa)} / {V(c.Numero)} / {V(c.Fecha)} / {V(c.FechaGuardado)}");
            }
            L();

            L("--- ESTADISTICAS ---");
            L("Facturas cargadas              : " + TotalFacturasExistentes);
            L("Sin pHash (no comparables)     : " + FacturasSinPHash);
            L("Comparadas efectivamente       : " + (TotalFacturasExistentes - FacturasSinPHash));
            L();
            L("Consejo: si hay muchas 'sin pHash', usa el menu Utilidades > Analizar pHash de todas las facturas.");
            return sb.ToString();
        }
    }
}
