using System;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Ventana "Ajustes > General": una sola lista con scroll dividida en
    // secciones (General, Cámara, Duplicados, Escaneo, Exportación y Claves
    // API). Cada ajuste lleva debajo un texto pequeño que explica qué hace.
    //
    // Diseño reutilizable:
    //   - Trabaja sobre una COPIA de AjustesEscaner: si el usuario cancela, el
    //     objeto original no se toca. Al aceptar, el resultado queda en
    //     'Resultado' y es el llamador quien decide si lo persiste.
    //   - La clave y el modelo de Gemini se guardan al aceptar mediante
    //     GeminiAPI.GuardarClaveYModeloGemini (viven en apis.json, no en
    //     ajustes.json).
    //   - La lista se construye con un cursor vertical (AgregarSeccion /
    //     AgregarOpcion): añadir un ajuste nuevo es una llamada más.
    //
    // Ejemplo de uso:
    //   using var f = new AjustesForm(ajustes,
    //       a => camara.IniciarSeleccionUsb(a, this, x => album.GuardarAjustes(x)),
    //       a => camara.IniciarSeleccionIp(a, this, x => album.GuardarAjustes(x)));
    //   if (f.ShowDialog(this) == DialogResult.OK)
    //   {
    //       ajustes = f.Resultado;
    //       album.GuardarAjustes(ajustes);
    //   }
    // -----------------------------------------------------------------------
    public sealed class AjustesForm : Form
    {
        // Ajustes ya editados (copia); válido cuando DialogResult == OK.
        public AjustesEscaner Resultado { get; private set; }

        // Valores guardados en AjustesEscaner.UltimoTipoCamara y su texto visible.
        private static readonly string[] ValoresTipoCamara = { "", "USB", "IP", "FILE" };
        private static readonly string[] TextosTipoCamara =
            { "(ninguna recordada)", "USB / interna", "IP", "Archivo" };

        // Valores guardados en AjustesEscaner.FormatoExportacion y su texto visible.
        private static readonly string[] ValoresFormatoExport = { "TODO", "PDF", "JSON", "JPG", "ORIGINAL" };
        private static readonly string[] TextosFormatoExport =
            { "Todo (PDF + JSON + JPG procesado)", "Solo PDF", "Solo JSON", "Solo JPG procesado", "Solo JPG original" };

        // Modelos de Gemini sugeridos (el desplegable también admite escribir otro).
        private static readonly string[] ModelosGemini =
            { "gemini-2.0-flash", "gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-2.5-pro" };

        // Acciones opcionales de la sección Cámara: reciben la copia de ajustes que
        // edita la ventana para que el diálogo de selección la modifique. Si son
        // null, el botón correspondiente no se muestra.
        private readonly Action<AjustesEscaner>? seleccionarUsb;
        private readonly Action<AjustesEscaner>? seleccionarIp;

        // Clave y modelo de Gemini tal como estaban al abrir (para guardar solo si cambian).
        private readonly string claveGeminiInicial;
        private readonly string modeloGeminiInicial;

        // Lista con scroll y cursor vertical para ir añadiendo filas.
        private readonly Panel panelLista = new Panel();
        private int cursorY = 8;
        private const int MargenX = 20;
        private int AnchoUtil => 520;

        // General
        private CheckBox chkVisorAlIniciar = null!;
        private CheckBox chkConfirmarSalir = null!;

        // Cámara
        private ComboBox cmbTipoCamara = null!;
        private NumericUpDown numIndiceUsb = null!;
        private TextBox txtUrlIp = null!;
        private CheckBox chkReconectar = null!;

        // Duplicados
        private CheckBox chkDupNumero = null!;
        private CheckBox chkDupFecha = null!;
        private CheckBox chkDupTotal = null!;
        private CheckBox chkDupEmpresa = null!;

        // Escaneo
        private CheckBox chkAutoguardadoLote = null!;
        private CheckBox chkAutoConfirmar = null!;
        private NumericUpDown numSegundos = null!;
        private CheckBox chkContrasteBrillo = null!;

        // Exportación
        private ComboBox cmbFormatoExport = null!;
        private TextBox txtCarpetaExport = null!;
        private CheckBox chkAbrirCarpeta = null!;

        // Claves API
        private TextBox txtClaveGemini = null!;
        private ComboBox cmbModeloGemini = null!;
        private Label lblResultadoPrueba = null!;
        private Button btnProbar = null!;

        // -------------------------------------------------------------------
        // Crea la ventana con una copia de 'ajustes' para editar.
        //  - seleccionarUsb / seleccionarIp (opcionales): abren los diálogos de
        //    selección de cámara sobre la copia de ajustes.
        // -------------------------------------------------------------------
        public AjustesForm(AjustesEscaner ajustes,
                           Action<AjustesEscaner>? seleccionarUsb = null,
                           Action<AjustesEscaner>? seleccionarIp = null)
        {
            Resultado = CopiarAjustes(ajustes);
            this.seleccionarUsb = seleccionarUsb;
            this.seleccionarIp = seleccionarIp;
            (claveGeminiInicial, modeloGeminiInicial) = GeminiAPI.ObtenerClaveYModeloGemini();

            Text = "Ajustes";
            Font = new Font("Segoe UI", 9.5F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(600, 520);

            ConstruirEstructura();

            ConstruirSeccionGeneral();
            ConstruirSeccionCamara();
            ConstruirSeccionDuplicados();
            ConstruirSeccionEscaneo();
            ConstruirSeccionExportacion();
            ConstruirSeccionClavesApi();

            // Hueco final para que la última descripción no quede pegada al borde.
            panelLista.Controls.Add(new Label { Left = 0, Top = cursorY + 10, Width = 1, Height = 1 });
        }

        // -------------------------------------------------------------------
        // Copia profunda de los ajustes (vía JSON) para no tocar el original
        // si el usuario cancela.
        // -------------------------------------------------------------------
        private static AjustesEscaner CopiarAjustes(AjustesEscaner origen)
        {
            try
            {
                var copia = JsonSerializer.Deserialize<AjustesEscaner>(JsonSerializer.Serialize(origen));
                if (copia != null) return copia;
            }
            catch { }
            return new AjustesEscaner();
        }

        // -------------------------------------------------------------------
        // Esqueleto: lista con scroll arriba y botones Aceptar / Cancelar abajo.
        // -------------------------------------------------------------------
        private void ConstruirEstructura()
        {
            var panelBotones = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            var btnAceptar = new Button
            {
                Text = "Aceptar",
                Width = 90,
                Height = 28,
                Left = ClientSize.Width - 200,
                Top = 9,
                DialogResult = DialogResult.None
            };
            var btnCancelar = new Button
            {
                Text = "Cancelar",
                Width = 90,
                Height = 28,
                Left = ClientSize.Width - 100,
                Top = 9,
                DialogResult = DialogResult.Cancel
            };
            btnAceptar.Click += BtnAceptar_Click;
            panelBotones.Controls.Add(btnAceptar);
            panelBotones.Controls.Add(btnCancelar);
            AcceptButton = btnAceptar;
            CancelButton = btnCancelar;

            panelLista.Dock = DockStyle.Fill;
            panelLista.AutoScroll = true;

            // Orden de adición: Fill primero, luego Bottom (el docking se resuelve
            // de atrás hacia delante).
            Controls.Add(panelLista);
            Controls.Add(panelBotones);
        }

        // ===================================================================
        // Ayudantes de maquetación (reutilizables para cualquier lista de ajustes)
        // ===================================================================

        // Añade el título de una sección (texto en negrita y una línea fina debajo).
        private void AgregarSeccion(string titulo)
        {
            if (cursorY > 8) cursorY += 14; // separación respecto a la sección anterior
            var lbl = new Label
            {
                Text = titulo,
                AutoSize = true,
                Left = MargenX,
                Top = cursorY,
                Font = new Font(Font.FontFamily, 11F, FontStyle.Bold)
            };
            panelLista.Controls.Add(lbl);
            cursorY += lbl.PreferredHeight + 4;

            panelLista.Controls.Add(new Label
            {
                Left = MargenX,
                Top = cursorY,
                Width = AnchoUtil,
                Height = 1,
                BorderStyle = BorderStyle.Fixed3D
            });
            cursorY += 10;
        }

        // Añade una opción: etiqueta opcional, control y texto pequeño explicativo.
        //  - etiqueta : título sobre el control (null si el propio control ya lleva texto, como un CheckBox).
        //  - control  : se coloca debajo de la etiqueta con el ancho indicado.
        //  - descripcion : texto gris pequeño que explica qué cambia el ajuste.
        private void AgregarOpcion(string? etiqueta, Control control, string descripcion, int ancho = 0)
        {
            if (etiqueta != null)
            {
                var lbl = new Label { Text = etiqueta, AutoSize = true, Left = MargenX, Top = cursorY };
                panelLista.Controls.Add(lbl);
                cursorY += lbl.PreferredHeight + 2;
            }

            control.Left = MargenX;
            control.Top = cursorY;
            if (ancho > 0) control.Width = ancho;
            panelLista.Controls.Add(control);
            cursorY += control.Height + 2;

            AgregarDescripcion(descripcion);
        }

        // Texto pequeño y gris con ajuste de línea automático.
        private void AgregarDescripcion(string texto)
        {
            var lbl = new Label
            {
                Text = texto,
                Font = new Font(Font.FontFamily, 8F),
                ForeColor = SystemColors.GrayText,
                Left = MargenX + 2,
                Top = cursorY,
                Width = AnchoUtil,
                AutoSize = false
            };
            // Altura según el texto con el ancho fijo.
            Size medida = TextRenderer.MeasureText(texto, lbl.Font, new Size(AnchoUtil, 0),
                TextFormatFlags.WordBreak);
            lbl.Height = medida.Height + 2;
            panelLista.Controls.Add(lbl);
            cursorY += lbl.Height + 10;
        }

        private static CheckBox CrearCheck(string texto, bool marcado) =>
            new CheckBox { Text = texto, AutoSize = true, Checked = marcado };

        private static NumericUpDown CrearNumero(int min, int max, int valor) =>
            new NumericUpDown { Minimum = min, Maximum = max, Width = 80, Value = Math.Min(max, Math.Max(min, valor)) };

        // ===================================================================
        // Secciones
        // ===================================================================

        private void ConstruirSeccionGeneral()
        {
            AgregarSeccion("General");

            chkVisorAlIniciar = CrearCheck("Abrir el visor web al iniciar", Resultado.AbrirVisorAlIniciar);
            AgregarOpcion(null, chkVisorAlIniciar,
                "Al arrancar muestra directamente el listado de facturas en lugar de la pantalla de escaneo. " +
                "Si lo desmarcas puedes abrirlo cuando quieras desde el menú Visor.");

            chkConfirmarSalir = CrearCheck("Confirmar antes de salir", Resultado.ConfirmarAlSalir);
            AgregarOpcion(null, chkConfirmarSalir,
                "Pregunta si de verdad quieres cerrar la aplicación al pulsar la X de la ventana. " +
                "Evita cierres accidentales con un lote a medias.");
        }

        private void ConstruirSeccionCamara()
        {
            AgregarSeccion("Cámara");

            cmbTipoCamara = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTipoCamara.Items.AddRange(TextosTipoCamara);
            cmbTipoCamara.SelectedIndex = IndiceTipoCamara(Resultado.UltimoTipoCamara);
            AgregarOpcion("Tipo de cámara por defecto", cmbTipoCamara,
                "Tipo de cámara que se recuerda y se usa al reconectar. Se actualiza solo cuando eliges otra cámara.", 220);

            numIndiceUsb = CrearNumero(0, 20, Resultado.UltimoIndiceCamaraUsb);
            AgregarOpcion("Índice USB", numIndiceUsb,
                "Número de puerto de la cámara USB o interna (0 es la primera). Cámbialo si tienes varias conectadas.");

            txtUrlIp = new TextBox { Text = Resultado.UltimaUrlCamaraIp };
            AgregarOpcion("URL de cámara IP", txtUrlIp,
                "Dirección del vídeo de la cámara IP o del móvil usado como cámara, por ejemplo http://192.168.1.50:8080/video.", 400);

            chkReconectar = CrearCheck("Reconectar la última al iniciar", Resultado.ReconectarCamaraAlIniciar);
            AgregarOpcion(null, chkReconectar,
                "Al arrancar conecta sola la última cámara usada, sin tener que pulsar Reconectar. " +
                "Si la cámara no está disponible simplemente no se conecta.");

            // Botones de selección (los diálogos conectan la cámara al aceptar).
            if (seleccionarUsb != null || seleccionarIp != null)
            {
                int x = MargenX;
                if (seleccionarUsb != null)
                {
                    var btnUsb = new Button { Text = "Seleccionar cámara USB…", Left = x, Top = cursorY, Width = 190, Height = 28 };
                    btnUsb.Click += (s, e) => EjecutarSeleccionCamara(seleccionarUsb);
                    panelLista.Controls.Add(btnUsb);
                    x += 200;
                }
                if (seleccionarIp != null)
                {
                    var btnIp = new Button { Text = "Cámaras IP detectadas…", Left = x, Top = cursorY, Width = 190, Height = 28 };
                    btnIp.Click += (s, e) => EjecutarSeleccionCamara(seleccionarIp);
                    panelLista.Controls.Add(btnIp);
                }
                cursorY += 32;
                AgregarDescripcion("Abren la búsqueda de cámaras y conectan la elegida en el momento.");
            }
        }

        private void ConstruirSeccionDuplicados()
        {
            AgregarSeccion("Duplicados");
            AgregarDescripcion($"Campos que se comparan para decidir si una factura ya existe (mínimo {ReglasDuplicados.MinimoReglas}). " +
                               "Cuantos más marques, más estricta es la coincidencia.");

            chkDupNumero = CrearCheck("Coincidir por número", Resultado.DupNumero);
            AgregarOpcion(null, chkDupNumero, "Compara el número de factura o ticket.");

            chkDupFecha = CrearCheck("Coincidir por fecha", Resultado.DupFecha);
            AgregarOpcion(null, chkDupFecha, "Compara la fecha del documento.");

            chkDupTotal = CrearCheck("Coincidir por total", Resultado.DupTotal);
            AgregarOpcion(null, chkDupTotal, "Compara el importe total.");

            chkDupEmpresa = CrearCheck("Coincidir por empresa", Resultado.DupEmpresa);
            AgregarOpcion(null, chkDupEmpresa, "Compara el nombre de la empresa emisora.");
        }

        private void ConstruirSeccionEscaneo()
        {
            AgregarSeccion("Escaneo");

            chkAutoguardadoLote = CrearCheck("Autoguardado en lote", Resultado.AutoguardadoLote);
            AgregarOpcion(null, chkAutoguardadoLote,
                "Al procesar varias imágenes seguidas, guarda cada una sola si no tocas nada durante la cuenta atrás. " +
                "Cualquier cambio en los controles la cancela.");

            chkAutoConfirmar = CrearCheck("Auto-confirmar tras la cuenta atrás", Resultado.AutoConfirmarTrasCuentaAtras);
            AgregarOpcion(null, chkAutoConfirmar,
                "En los diálogos con cuenta atrás (p. ej. posible duplicado), al llegar a cero se aplica la opción por defecto. " +
                "Desmarcado, el diálogo espera hasta que decidas.");

            numSegundos = CrearNumero(1, 120, Resultado.SegundosCuentaAtras);
            AgregarOpcion("Segundos de cuenta atrás", numSegundos,
                "Duración de la cuenta atrás del autoguardado en lote y de los diálogos estándar. " +
                "Los diálogos con tiempo propio (duplicados con vista previa) mantienen el suyo.");

            chkContrasteBrillo = CrearCheck("Contraste y brillo automáticos", Resultado.ContrasteBrilloAutomaticos);
            AgregarOpcion(null, chkContrasteBrillo,
                "Al cargar o capturar una imagen calcula el contraste y el brillo más adecuados para esa foto. " +
                "Desmarcado, empieza con valores neutros y los ajustas tú con los deslizadores.");
        }

        private void ConstruirSeccionExportacion()
        {
            AgregarSeccion("Exportación");

            cmbFormatoExport = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFormatoExport.Items.AddRange(TextosFormatoExport);
            int idx = Array.IndexOf(ValoresFormatoExport, Resultado.FormatoExportacion ?? "TODO");
            cmbFormatoExport.SelectedIndex = idx >= 0 ? idx : 0;
            AgregarOpcion("Formato por defecto", cmbFormatoExport,
                "Qué archivos vienen marcados al abrir Exportar documentos. Siempre puedes cambiarlo en esa ventana.", 260);

            txtCarpetaExport = new TextBox { Text = Resultado.CarpetaExportacion ?? "" };
            var btnExaminar = new Button { Text = "Examinar…", Width = 90, Height = 24 };
            btnExaminar.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog { SelectedPath = txtCarpetaExport.Text };
                if (dlg.ShowDialog(this) == DialogResult.OK) txtCarpetaExport.Text = dlg.SelectedPath;
            };
            AgregarOpcion("Carpeta de exportación", txtCarpetaExport,
                "Carpeta que se muestra al guardar una exportación. Vacío = la última carpeta usada por Windows.", 300);
            // El botón Examinar va a la derecha del cuadro de texto (misma fila).
            btnExaminar.Left = txtCarpetaExport.Right + 8;
            btnExaminar.Top = txtCarpetaExport.Top - 1;
            panelLista.Controls.Add(btnExaminar);

            chkAbrirCarpeta = CrearCheck("Abrir la carpeta al terminar", Resultado.AbrirCarpetaAlExportar);
            AgregarOpcion(null, chkAbrirCarpeta,
                "Cuando acaba una exportación abre el Explorador con el archivo creado seleccionado.");
        }

        private void ConstruirSeccionClavesApi()
        {
            AgregarSeccion("Claves API");

            txtClaveGemini = new TextBox { Text = claveGeminiInicial, UseSystemPasswordChar = true };
            AgregarOpcion("Clave de Gemini", txtClaveGemini,
                "Clave de la API de Google AI Studio que se usa para leer las facturas. Se guarda en apis.json " +
                "junto al programa. Para otras APIs usa el menú Claves API.", 360);
            var chkVer = new CheckBox { Text = "Mostrar", AutoSize = true, Left = txtClaveGemini.Right + 8, Top = txtClaveGemini.Top + 2 };
            chkVer.CheckedChanged += (s, e) => txtClaveGemini.UseSystemPasswordChar = !chkVer.Checked;
            panelLista.Controls.Add(chkVer);

            cmbModeloGemini = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Text = modeloGeminiInicial };
            cmbModeloGemini.Items.AddRange(ModelosGemini);
            AgregarOpcion("Modelo", cmbModeloGemini,
                "Modelo de Gemini que procesa las imágenes. Los «flash» son rápidos y baratos; «pro» es más preciso pero más lento. " +
                "Puedes escribir el nombre de otro modelo.", 260);

            btnProbar = new Button { Text = "Probar conexión", Width = 130, Height = 28 };
            btnProbar.Click += BtnProbar_Click;
            AgregarOpcion(null, btnProbar,
                "Envía una petición mínima con la clave y el modelo indicados (aún sin guardar) y muestra si funciona.");
            // Resultado de la prueba, a la derecha del botón.
            lblResultadoPrueba = new Label
            {
                AutoSize = false,
                Left = btnProbar.Right + 10,
                Top = btnProbar.Top + 5,
                Width = AnchoUtil - btnProbar.Width - 10,
                Height = 20
            };
            panelLista.Controls.Add(lblResultadoPrueba);
        }

        // ===================================================================
        // Lógica
        // ===================================================================

        private static int IndiceTipoCamara(string? valor)
        {
            int idx = Array.IndexOf(ValoresTipoCamara, valor ?? "");
            return idx >= 0 ? idx : 0;
        }

        // Vuelca los controles de la sección Cámara en 'Resultado'.
        private void VolcarCamara()
        {
            Resultado.UltimoTipoCamara = ValoresTipoCamara[Math.Max(0, cmbTipoCamara.SelectedIndex)];
            Resultado.UltimoIndiceCamaraUsb = (int)numIndiceUsb.Value;
            Resultado.UltimaUrlCamaraIp = txtUrlIp.Text.Trim();
        }

        // Carga en los controles de la sección Cámara los valores de 'Resultado'.
        private void RefrescarCamara()
        {
            cmbTipoCamara.SelectedIndex = IndiceTipoCamara(Resultado.UltimoTipoCamara);
            numIndiceUsb.Value = Math.Min(20, Math.Max(0, Resultado.UltimoIndiceCamaraUsb));
            txtUrlIp.Text = Resultado.UltimaUrlCamaraIp;
        }

        // Lanza un diálogo de selección de cámara: primero vuelca lo editado para
        // no perderlo, deja que el diálogo modifique 'Resultado' y refresca los
        // controles con el resultado.
        private void EjecutarSeleccionCamara(Action<AjustesEscaner> accion)
        {
            VolcarCamara();
            accion(Resultado);
            RefrescarCamara();
        }

        // Prueba la conexión con Gemini usando lo escrito en pantalla.
        private async void BtnProbar_Click(object? sender, EventArgs e)
        {
            btnProbar.Enabled = false;
            lblResultadoPrueba.ForeColor = SystemColors.GrayText;
            lblResultadoPrueba.Text = "Probando…";
            try
            {
                var (ok, mensaje) = await GeminiAPI.ProbarConexionGemini(txtClaveGemini.Text, cmbModeloGemini.Text);
                if (IsDisposed) return;
                lblResultadoPrueba.ForeColor = ok ? Color.SeaGreen : Color.Firebrick;
                lblResultadoPrueba.Text = (ok ? "✔ " : "✖ ") + mensaje;
            }
            finally
            {
                if (!IsDisposed) btnProbar.Enabled = true;
            }
        }

        // Aceptar: valida, vuelca los controles en 'Resultado' y cierra con OK.
        private void BtnAceptar_Click(object? sender, EventArgs e)
        {
            int activas = (chkDupNumero.Checked ? 1 : 0) + (chkDupFecha.Checked ? 1 : 0)
                        + (chkDupTotal.Checked ? 1 : 0) + (chkDupEmpresa.Checked ? 1 : 0);
            if (activas < ReglasDuplicados.MinimoReglas)
            {
                panelLista.ScrollControlIntoView(chkDupNumero);
                DialogoAutoConfirmar.Aviso(
                    $"Marca al menos {ReglasDuplicados.MinimoReglas} campos para detectar duplicados.",
                    "Ajustes");
                return;
            }

            string tipo = ValoresTipoCamara[Math.Max(0, cmbTipoCamara.SelectedIndex)];
            if (tipo == "IP" && string.IsNullOrWhiteSpace(txtUrlIp.Text))
            {
                panelLista.ScrollControlIntoView(txtUrlIp);
                DialogoAutoConfirmar.Aviso("Escribe la URL de la cámara IP.", "Ajustes");
                return;
            }

            string carpetaExport = txtCarpetaExport.Text.Trim();
            if (carpetaExport.Length > 0 && !System.IO.Directory.Exists(carpetaExport))
            {
                panelLista.ScrollControlIntoView(txtCarpetaExport);
                DialogoAutoConfirmar.Aviso("La carpeta de exportación no existe.", "Ajustes");
                return;
            }

            // General
            Resultado.AbrirVisorAlIniciar = chkVisorAlIniciar.Checked;
            Resultado.ConfirmarAlSalir = chkConfirmarSalir.Checked;

            // Cámara
            VolcarCamara();
            Resultado.ReconectarCamaraAlIniciar = chkReconectar.Checked;

            // Duplicados
            Resultado.DupNumero = chkDupNumero.Checked;
            Resultado.DupFecha = chkDupFecha.Checked;
            Resultado.DupTotal = chkDupTotal.Checked;
            Resultado.DupEmpresa = chkDupEmpresa.Checked;

            // Escaneo
            Resultado.AutoguardadoLote = chkAutoguardadoLote.Checked;
            Resultado.AutoConfirmarTrasCuentaAtras = chkAutoConfirmar.Checked;
            Resultado.SegundosCuentaAtras = (int)numSegundos.Value;
            Resultado.ContrasteBrilloAutomaticos = chkContrasteBrillo.Checked;

            // Exportación
            Resultado.FormatoExportacion = ValoresFormatoExport[Math.Max(0, cmbFormatoExport.SelectedIndex)];
            Resultado.CarpetaExportacion = carpetaExport;
            Resultado.AbrirCarpetaAlExportar = chkAbrirCarpeta.Checked;

            // Claves API: solo se toca apis.json si cambió la clave o el modelo.
            string clave = txtClaveGemini.Text.Trim();
            string modelo = cmbModeloGemini.Text.Trim();
            bool cambio = clave != claveGeminiInicial.Trim() || modelo != modeloGeminiInicial;
            if (cambio && (clave.Length > 0 || claveGeminiInicial.Length > 0))
                GeminiAPI.GuardarClaveYModeloGemini(clave, modelo);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
