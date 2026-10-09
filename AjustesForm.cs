using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace FACTicket_Scanner
{
    // -----------------------------------------------------------------------
    // Ventana "Ajustes > General": agrupa todas las secciones de ajustes en
    // una sola ventana con navegación lateral (Generales, Cámara, Duplicados,
    // Escaneo y Exportación). Claves API sigue siendo un item aparte del menú.
    //
    // Diseño reutilizable:
    //   - Trabaja sobre una COPIA de AjustesEscaner: si el usuario cancela, el
    //     objeto original no se toca. Al aceptar, el resultado queda en
    //     'Resultado' y es el llamador quien decide si lo persiste.
    //   - Cada sección es un Panel construido en código (patrón del proyecto)
    //     y se registra con AgregarSeccion(): añadir una sección nueva es
    //     crear su panel y llamar a ese método.
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

        // Acciones opcionales de la sección Cámara: reciben la copia de ajustes que
        // edita la ventana para que el diálogo de selección la modifique. Si son
        // null, el botón correspondiente no se muestra.
        private readonly Action<AjustesEscaner>? seleccionarUsb;
        private readonly Action<AjustesEscaner>? seleccionarIp;

        private readonly ListBox lstSecciones = new ListBox();
        private readonly Panel panelContenido = new Panel();
        private readonly Dictionary<string, Control> secciones = new Dictionary<string, Control>();

        // Controles de la sección Cámara
        private ComboBox cmbTipoCamara = null!;
        private NumericUpDown numIndiceUsb = null!;
        private TextBox txtUrlIp = null!;

        // Controles de la sección Duplicados
        private CheckBox chkDupNumero = null!;
        private CheckBox chkDupFecha = null!;
        private CheckBox chkDupTotal = null!;
        private CheckBox chkDupEmpresa = null!;

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

            Text = "Ajustes";
            Font = new Font("Segoe UI", 9.5F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(600, 360);

            ConstruirEstructura();

            AgregarSeccion("Generales", ConstruirSeccionVacia());
            AgregarSeccion("Cámara", ConstruirSeccionCamara());
            AgregarSeccion("Duplicados", ConstruirSeccionDuplicados());
            AgregarSeccion("Escaneo", ConstruirSeccionVacia());
            AgregarSeccion("Exportación", ConstruirSeccionVacia());

            lstSecciones.SelectedIndex = 0;
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
        // Esqueleto de la ventana: lista de secciones a la izquierda, contenido
        // a la derecha y botones Aceptar / Cancelar abajo.
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

            lstSecciones.Dock = DockStyle.Left;
            lstSecciones.Width = 150;
            lstSecciones.BorderStyle = BorderStyle.None;
            lstSecciones.IntegralHeight = false;
            lstSecciones.ItemHeight = 28;
            lstSecciones.DrawMode = DrawMode.OwnerDrawFixed;
            lstSecciones.DrawItem += LstSecciones_DrawItem;
            lstSecciones.SelectedIndexChanged += (s, e) => MostrarSeccionSeleccionada();

            panelContenido.Dock = DockStyle.Fill;
            panelContenido.Padding = new Padding(16);

            // Orden de adición: Fill primero, luego Left y Bottom (el docking
            // se resuelve de atrás hacia delante).
            Controls.Add(panelContenido);
            Controls.Add(lstSecciones);
            Controls.Add(panelBotones);
        }

        // -------------------------------------------------------------------
        // Dibuja cada entrada de la lista con relleno propio (altura 28 px).
        // -------------------------------------------------------------------
        private void LstSecciones_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            using var brocha = new SolidBrush(e.ForeColor);
            string texto = lstSecciones.Items[e.Index]?.ToString() ?? "";
            var formato = new StringFormat { LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(texto, e.Font ?? Font, brocha,
                new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), formato);
            e.DrawFocusRectangle();
        }

        // -------------------------------------------------------------------
        // Registra una sección: la añade a la lista lateral y guarda su panel.
        // -------------------------------------------------------------------
        private void AgregarSeccion(string nombre, Control panel)
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            secciones[nombre] = panel;
            panelContenido.Controls.Add(panel);
            lstSecciones.Items.Add(nombre);
        }

        // -------------------------------------------------------------------
        // Muestra solo el panel de la sección elegida en la lista.
        // -------------------------------------------------------------------
        private void MostrarSeccionSeleccionada()
        {
            string? elegida = lstSecciones.SelectedItem?.ToString();
            foreach (var par in secciones)
                par.Value.Visible = par.Key == elegida;
        }

        // -------------------------------------------------------------------
        // Sección sin opciones todavía (Generales, Escaneo, Exportación).
        // -------------------------------------------------------------------
        private static Control ConstruirSeccionVacia()
        {
            var panel = new Panel();
            panel.Controls.Add(new Label
            {
                Text = "Esta sección aún no tiene opciones.",
                AutoSize = true,
                Left = 0,
                Top = 4,
                ForeColor = SystemColors.GrayText
            });
            return panel;
        }

        // -------------------------------------------------------------------
        // Sección Cámara: tipo recordado, índice USB y URL de la cámara IP.
        // -------------------------------------------------------------------
        private Control ConstruirSeccionCamara()
        {
            var panel = new Panel();

            panel.Controls.Add(CrearEtiqueta("Tipo de cámara recordada", 0, 4));
            cmbTipoCamara = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Left = 0,
                Top = 26,
                Width = 220
            };
            cmbTipoCamara.Items.AddRange(TextosTipoCamara);
            int idx = Array.IndexOf(ValoresTipoCamara, Resultado.UltimoTipoCamara ?? "");
            cmbTipoCamara.SelectedIndex = idx >= 0 ? idx : 0;
            panel.Controls.Add(cmbTipoCamara);

            panel.Controls.Add(CrearEtiqueta("Índice USB", 0, 66));
            numIndiceUsb = new NumericUpDown
            {
                Left = 0,
                Top = 88,
                Width = 80,
                Minimum = 0,
                Maximum = 20,
                Value = Math.Min(20, Math.Max(0, Resultado.UltimoIndiceCamaraUsb))
            };
            panel.Controls.Add(numIndiceUsb);

            panel.Controls.Add(CrearEtiqueta("URL de la cámara IP", 0, 128));
            txtUrlIp = new TextBox
            {
                Left = 0,
                Top = 150,
                Width = 380,
                Text = Resultado.UltimaUrlCamaraIp
            };
            panel.Controls.Add(txtUrlIp);

            // Botones de selección (los diálogos conectan la cámara al aceptar).
            if (seleccionarUsb != null)
            {
                var btnUsb = new Button { Text = "Seleccionar cámara USB…", Left = 0, Top = 200, Width = 190, Height = 28 };
                btnUsb.Click += (s, e) => EjecutarSeleccionCamara(seleccionarUsb);
                panel.Controls.Add(btnUsb);
            }
            if (seleccionarIp != null)
            {
                var btnIp = new Button { Text = "Cámaras IP detectadas…", Left = 200, Top = 200, Width = 190, Height = 28 };
                btnIp.Click += (s, e) => EjecutarSeleccionCamara(seleccionarIp);
                panel.Controls.Add(btnIp);
            }

            return panel;
        }

        // -------------------------------------------------------------------
        // Vuelca los controles de la sección Cámara en 'Resultado'.
        // -------------------------------------------------------------------
        private void VolcarCamara()
        {
            Resultado.UltimoTipoCamara = ValoresTipoCamara[Math.Max(0, cmbTipoCamara.SelectedIndex)];
            Resultado.UltimoIndiceCamaraUsb = (int)numIndiceUsb.Value;
            Resultado.UltimaUrlCamaraIp = txtUrlIp.Text.Trim();
        }

        // -------------------------------------------------------------------
        // Carga en los controles de la sección Cámara los valores de 'Resultado'.
        // -------------------------------------------------------------------
        private void RefrescarCamara()
        {
            int idx = Array.IndexOf(ValoresTipoCamara, Resultado.UltimoTipoCamara ?? "");
            cmbTipoCamara.SelectedIndex = idx >= 0 ? idx : 0;
            numIndiceUsb.Value = Math.Min(20, Math.Max(0, Resultado.UltimoIndiceCamaraUsb));
            txtUrlIp.Text = Resultado.UltimaUrlCamaraIp;
        }

        // -------------------------------------------------------------------
        // Lanza un diálogo de selección de cámara: primero vuelca lo editado para
        // no perderlo, deja que el diálogo modifique 'Resultado' y refresca los
        // controles con el resultado.
        // -------------------------------------------------------------------
        private void EjecutarSeleccionCamara(Action<AjustesEscaner> accion)
        {
            VolcarCamara();
            accion(Resultado);
            RefrescarCamara();
        }

        // -------------------------------------------------------------------
        // Sección Duplicados: qué campos se comparan al detectar duplicados
        // (mismas reglas que los checkboxes del panel de guardado).
        // -------------------------------------------------------------------
        private Control ConstruirSeccionDuplicados()
        {
            var panel = new Panel();

            panel.Controls.Add(CrearEtiqueta(
                $"Campos a comparar (mínimo {ReglasDuplicados.MinimoReglas})", 0, 4));

            chkDupNumero = CrearCheck("Coincidir por número", 0, 30, Resultado.DupNumero);
            chkDupFecha = CrearCheck("Coincidir por fecha", 0, 58, Resultado.DupFecha);
            chkDupTotal = CrearCheck("Coincidir por total", 0, 86, Resultado.DupTotal);
            chkDupEmpresa = CrearCheck("Coincidir por empresa", 0, 114, Resultado.DupEmpresa);

            panel.Controls.AddRange(new Control[] { chkDupNumero, chkDupFecha, chkDupTotal, chkDupEmpresa });
            return panel;
        }

        // Crea una etiqueta simple de formulario.
        private static Label CrearEtiqueta(string texto, int x, int y) =>
            new Label { Text = texto, AutoSize = true, Left = x, Top = y };

        // Crea un CheckBox con su estado inicial.
        private static CheckBox CrearCheck(string texto, int x, int y, bool marcado) =>
            new CheckBox { Text = texto, AutoSize = true, Left = x, Top = y, Checked = marcado };

        // -------------------------------------------------------------------
        // Aceptar: valida, vuelca los controles en 'Resultado' y cierra con OK.
        // -------------------------------------------------------------------
        private void BtnAceptar_Click(object? sender, EventArgs e)
        {
            int activas = (chkDupNumero.Checked ? 1 : 0) + (chkDupFecha.Checked ? 1 : 0)
                        + (chkDupTotal.Checked ? 1 : 0) + (chkDupEmpresa.Checked ? 1 : 0);
            if (activas < ReglasDuplicados.MinimoReglas)
            {
                lstSecciones.SelectedItem = "Duplicados";
                DialogoAutoConfirmar.Aviso(
                    $"Marca al menos {ReglasDuplicados.MinimoReglas} campos para detectar duplicados.",
                    "Ajustes");
                return;
            }

            string tipo = ValoresTipoCamara[Math.Max(0, cmbTipoCamara.SelectedIndex)];
            if (tipo == "IP" && string.IsNullOrWhiteSpace(txtUrlIp.Text))
            {
                lstSecciones.SelectedItem = "Cámara";
                DialogoAutoConfirmar.Aviso("Escribe la URL de la cámara IP.", "Ajustes");
                return;
            }

            VolcarCamara();
            Resultado.DupNumero = chkDupNumero.Checked;
            Resultado.DupFecha = chkDupFecha.Checked;
            Resultado.DupTotal = chkDupTotal.Checked;
            Resultado.DupEmpresa = chkDupEmpresa.Checked;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
