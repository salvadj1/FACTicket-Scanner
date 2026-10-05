using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Utilidades REUTILIZABLES para exportar contenido a disco desde un
    // proyecto WinForms. No depende de ninguna otra clase de la aplicación.
    // -----------------------------------------------------------------------
    internal static class ExportadorArchivos
    {
        // -------------------------------------------------------------------
        // Muestra un diálogo "Guardar como..." y escribe el texto indicado.
        //
        // Parámetros:
        //   propietario    : ventana propietaria del diálogo (puede ser null).
        //   nombreSugerido : nombre de archivo propuesto (se limpia de caracteres inválidos).
        //   contenido      : texto a guardar.
        //   filtro         : filtro del diálogo, formato WinForms
        //                    ("Archivo de texto (*.txt)|*.txt").
        //   conBom         : true = UTF-8 con BOM (recomendado para que Excel y el
        //                    Bloc de notas detecten bien las tildes).
        //
        // Devuelve la ruta del archivo guardado, o null si el usuario canceló
        // o se produjo un error (el error se muestra al usuario).
        // -------------------------------------------------------------------
        public static string? GuardarTextoConDialogo(IWin32Window? propietario, string nombreSugerido,
            string contenido, string filtro = "Todos los archivos (*.*)|*.*", bool conBom = true)
        {
            using var sfd = new SaveFileDialog
            {
                FileName = LimpiarNombreArchivo(nombreSugerido),
                Filter = filtro,
                OverwritePrompt = true,
                RestoreDirectory = true
            };

            if (sfd.ShowDialog(propietario) != DialogResult.OK) return null;

            try
            {
                File.WriteAllText(sfd.FileName, contenido, new UTF8Encoding(conBom));
                return sfd.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(propietario, "No se pudo guardar el archivo:\n" + ex.Message,
                    "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        // -------------------------------------------------------------------
        // Sustituye por '_' los caracteres no válidos en nombres de archivo.
        // Devuelve "archivo" si el nombre queda vacío.
        // -------------------------------------------------------------------
        public static string LimpiarNombreArchivo(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "archivo";
            var sb = new StringBuilder(nombre.Length);
            char[] invalidos = Path.GetInvalidFileNameChars();
            foreach (char c in nombre)
                sb.Append(Array.IndexOf(invalidos, c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
