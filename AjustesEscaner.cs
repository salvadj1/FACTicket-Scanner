using System;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // IMPORTANTE: esta clase ya solo almacena preferencias que tiene sentido
    // recordar ENTRE sesiones/capturas (URL de cámara, última carpeta usada).
    //
    // Los ajustes de PROCESADO DE IMAGEN (BlockSize, C, Contraste, Brillo,
    // Ruido, Nitidez, GruesoTexto, Umbral, márgenes, rotación) ya NO se cargan
    // desde aquí en cada foto nueva. Antes se reutilizaban entre capturas
    // distintas (foto 1 con rotación 90° y margen manual 20% contaminaba la
    // foto 2, que podía tener otro encuadre), lo que producía recortes
    // incorrectos. Ahora cada captura nueva arranca desde un estado neutro
    // recalculado por CalcularAjustesAutomaticos(), específico de esa foto.
    //
    // Las propiedades de procesado se mantienen aquí solo como DEFAULTS de
    // referencia (p. ej. para CargarAjustes() si el archivo no existe todavía
    // y para que el tipo siga siendo serializable sin romper compatibilidad
    // con ajustes.json ya guardados de versiones anteriores).
    // -----------------------------------------------------------------------
    public class AjustesEscaner
    {
        // --- Preferencias que SÍ deben persistir entre sesiones ---
        public string UltimaUrlCamaraIp { get; set; } = "http://192.168.1.50:8080/video";
        public string UltimaCarpetaGuardado { get; set; } = "";

        // --- Tipo de cámara recordada: "USB", "IP" o "FILE" ---
        public string UltimoTipoCamara { get; set; } = "";

        // --- Índice USB recordado ---
        public int UltimoIndiceCamaraUsb { get; set; } = 0;

        // --- Reglas de verificación de duplicados (ver ReglasDuplicados) ---
        public bool DupNumero { get; set; } = true;
        public bool DupFecha { get; set; } = true;
        public bool DupTotal { get; set; } = true;
        public bool DupEmpresa { get; set; } = false;

        // --- General ---
        // Abre el visor web de facturas nada más arrancar la aplicación.
        public bool AbrirVisorAlIniciar { get; set; } = true;
        // Pide confirmación al cerrar la ventana principal.
        public bool ConfirmarAlSalir { get; set; } = false;

        // --- Cámara ---
        // Conecta automáticamente la última cámara usada al arrancar.
        public bool ReconectarCamaraAlIniciar { get; set; } = false;

        // --- Escaneo ---
        // Guarda sola cada imagen del lote si no se toca nada durante la cuenta atrás.
        public bool AutoguardadoLote { get; set; } = true;
        // true = los diálogos aplican su opción por defecto al acabar la cuenta atrás;
        // false = esperan a que el usuario decida (sin cuenta atrás).
        public bool AutoConfirmarTrasCuentaAtras { get; set; } = true;
        // Duración (segundos) de la cuenta atrás de diálogos y autoguardado en lote.
        public int SegundosCuentaAtras { get; set; } = 5;
        // Calcula contraste y brillo al cargar/capturar cada imagen.
        public bool ContrasteBrilloAutomaticos { get; set; } = true;

        // --- Exportación ---
        // Contenido preseleccionado al exportar: "TODO", "PDF", "JSON", "JPG" u "ORIGINAL".
        public string FormatoExportacion { get; set; } = "TODO";
        // Carpeta inicial de los diálogos "Guardar como" de exportación ("" = la última usada por Windows).
        public string CarpetaExportacion { get; set; } = "";
        // Abre la carpeta en el Explorador al terminar de exportar.
        public bool AbrirCarpetaAlExportar { get; set; } = false;

        // --- Valores de referencia del pipeline (YA NO se cargan por foto) ---
        public int BlockSize { get; set; } = 15;
        public int C { get; set; } = 8;
        public int RotacionGrados { get; set; } = 0;
        public int Ruido { get; set; } = 1;
        public int GruesoTexto { get; set; } = 0;
        public int Nitidez { get; set; } = 0;
        public int Contraste { get; set; } = 2;
        public int Brillo { get; set; } = 0;
        public int Umbral { get; set; } = 0;
        public int MargenRecorte { get; set; } = 5;
        public int MargenSuperior { get; set; } = 0;
        public int MargenInferior { get; set; } = 0;
        public int MargenIzquierdo { get; set; } = 0;
        public int MargenDerecho { get; set; } = 0;
    }
}
