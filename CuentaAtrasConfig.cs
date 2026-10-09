using System;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Configuración global y REUTILIZABLE de las cuentas atrás de los diálogos.
    // No depende de ninguna otra clase: basta con asignar Activa/Segundos al
    // arrancar (y al cambiar los ajustes) y llamar a SegundosEfectivos().
    //
    // Ejemplo:
    //   CuentaAtrasConfig.Aplicar(true, 8);
    //   int s = CuentaAtrasConfig.SegundosEfectivos(5, 5); // -> 8
    // -----------------------------------------------------------------------
    internal static class CuentaAtrasConfig
    {
        // true = al agotarse la cuenta atrás se aplica la opción por defecto del diálogo.
        // false = los diálogos esperan la respuesta del usuario.
        public static bool Activa { get; private set; } = true;

        // Duración configurada (segundos) de la cuenta atrás estándar.
        public static int Segundos { get; private set; } = 5;

        // -------------------------------------------------------------------
        // Actualiza la configuración. Los segundos se limitan a 1..120.
        // -------------------------------------------------------------------
        public static void Aplicar(bool activa, int segundos)
        {
            Activa = activa;
            Segundos = Math.Min(120, Math.Max(1, segundos));
        }

        // -------------------------------------------------------------------
        // Devuelve la duración a usar: si 'solicitados' coincide con el valor
        // estándar del proyecto ('estandar') se usa el configurado; si el
        // llamador pidió un valor propio (p. ej. 10 s o 30 s) se respeta.
        // -------------------------------------------------------------------
        public static int SegundosEfectivos(int solicitados, int estandar) =>
            solicitados == estandar ? Segundos : solicitados;
    }
}
