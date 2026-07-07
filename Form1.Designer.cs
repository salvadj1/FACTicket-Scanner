using System;

namespace FACTicket_Scanner
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.archivoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.abrirToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.guardarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.separadorArchivoToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.salirToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.camaraMenuToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.camaraToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.camarasIpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.separadorCamaraToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.reconectarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.verToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.visorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.carpetaToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.utilidadesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.conversorIMGPDFToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.analizarPhashDeTodasLasFacturasToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.buscarDuplicadosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aPIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editarClavesAPIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ayudaToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.separadorMenuToolbarToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.btnAbrirRapido = new System.Windows.Forms.ToolStripButton();
            this.btnGuardarRapido = new System.Windows.Forms.ToolStripButton();
            this.separadorToolbar2ToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.btnCarpetaRapida = new System.Windows.Forms.ToolStripButton();
            this.btnVisorRapido = new System.Windows.Forms.ToolStripButton();
            this.separadorToolbarToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.cmbTipoCamara = new System.Windows.Forms.ToolStripComboBox();
            this.txtUrlCamara = new System.Windows.Forms.ToolStripTextBox();
            this.cmbResultadoCamara = new System.Windows.Forms.ToolStripComboBox();
            this.btnBuscarCamara = new System.Windows.Forms.ToolStripButton();
            this.btnReconectarRapido = new System.Windows.Forms.ToolStripButton();
            this.btnCerrarVisor = new System.Windows.Forms.Button();
            this.panelNavModal = new System.Windows.Forms.FlowLayoutPanel();
            this.btnAnteriorVisor = new System.Windows.Forms.Button();
            this.lblTituloModal = new System.Windows.Forms.Label();
            this.btnSiguienteVisor = new System.Windows.Forms.Button();
            this.btnEditarVisor = new System.Windows.Forms.Button();
            this.btnEliminarVisor = new System.Windows.Forms.Button();
            this.btnCerrarModalVisor = new System.Windows.Forms.Button();
            this.panelBarraVisor = new System.Windows.Forms.Panel();
            this.lblEventosVisor = new System.Windows.Forms.Label();
            this.lblEstadoVisor = new System.Windows.Forms.Label();
            this.panelIzquierdo = new System.Windows.Forms.Panel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.panelDerecho = new System.Windows.Forms.Panel();
            this.panelScrollable = new System.Windows.Forms.Panel();
            this.panelBotones = new System.Windows.Forms.Panel();
            this.panelVisor = new System.Windows.Forms.Panel();
            this.webViewAlbum = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.menuStrip1.SuspendLayout();
            this.panelNavModal.SuspendLayout();
            this.panelBarraVisor.SuspendLayout();
            this.panelIzquierdo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.panelDerecho.SuspendLayout();
            this.panelVisor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webViewAlbum)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(22, 22);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.archivoToolStripMenuItem,
            this.camaraMenuToolStripMenuItem,
            this.verToolStripMenuItem,
            this.utilidadesToolStripMenuItem,
            this.aPIToolStripMenuItem,
            this.ayudaToolStripMenuItem,
            this.separadorMenuToolbarToolStripMenuItem,
            this.btnAbrirRapido,
            this.btnGuardarRapido,
            this.separadorToolbar2ToolStripMenuItem,
            this.btnCarpetaRapida,
            this.btnVisorRapido,
            this.separadorToolbarToolStripMenuItem,
            this.cmbTipoCamara,
            this.txtUrlCamara,
            this.cmbResultadoCamara,
            this.btnBuscarCamara,
            this.btnReconectarRapido});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.ShowItemToolTips = true;
            this.menuStrip1.Size = new System.Drawing.Size(1280, 27);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // archivoToolStripMenuItem
            // 
            this.archivoToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.abrirToolStripMenuItem,
            this.guardarToolStripMenuItem,
            this.exportarToolStripMenuItem,
            this.separadorArchivoToolStripMenuItem,
            this.salirToolStripMenuItem});
            this.archivoToolStripMenuItem.Name = "archivoToolStripMenuItem";
            this.archivoToolStripMenuItem.Size = new System.Drawing.Size(67, 23);
            this.archivoToolStripMenuItem.Text = "Archivo";
            // 
            // abrirToolStripMenuItem
            // 
            this.abrirToolStripMenuItem.Name = "abrirToolStripMenuItem";
            this.abrirToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.abrirToolStripMenuItem.Size = new System.Drawing.Size(235, 24);
            this.abrirToolStripMenuItem.Text = "📂  Abrir archivo";
            this.abrirToolStripMenuItem.Click += new System.EventHandler(this.abrirToolStripMenuItem_Click);
            // 
            // guardarToolStripMenuItem
            // 
            this.guardarToolStripMenuItem.Name = "guardarToolStripMenuItem";
            this.guardarToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.guardarToolStripMenuItem.Size = new System.Drawing.Size(235, 24);
            this.guardarToolStripMenuItem.Text = "💾  Guardar";
            this.guardarToolStripMenuItem.Click += new System.EventHandler(this.guardarToolStripMenuItem_Click);
            // 
            // exportarToolStripMenuItem
            // 
            this.exportarToolStripMenuItem.Name = "exportarToolStripMenuItem";
            this.exportarToolStripMenuItem.Size = new System.Drawing.Size(235, 24);
            this.exportarToolStripMenuItem.Text = "Exportar";
            this.exportarToolStripMenuItem.Click += new System.EventHandler(this.exportarToolStripMenuItem_Click);
            // 
            // separadorArchivoToolStripMenuItem
            // 
            this.separadorArchivoToolStripMenuItem.Name = "separadorArchivoToolStripMenuItem";
            this.separadorArchivoToolStripMenuItem.Size = new System.Drawing.Size(232, 6);
            // 
            // salirToolStripMenuItem
            // 
            this.salirToolStripMenuItem.Name = "salirToolStripMenuItem";
            this.salirToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4)));
            this.salirToolStripMenuItem.Size = new System.Drawing.Size(235, 24);
            this.salirToolStripMenuItem.Text = "🚪  Salir";
            this.salirToolStripMenuItem.Click += new System.EventHandler(this.salirToolStripMenuItem_Click);
            // 
            // camaraMenuToolStripMenuItem
            // 
            this.camaraMenuToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.camaraToolStripMenuItem,
            this.camarasIpToolStripMenuItem,
            this.separadorCamaraToolStripMenuItem,
            this.reconectarToolStripMenuItem});
            this.camaraMenuToolStripMenuItem.Name = "camaraMenuToolStripMenuItem";
            this.camaraMenuToolStripMenuItem.Size = new System.Drawing.Size(68, 23);
            this.camaraMenuToolStripMenuItem.Text = "Cámara";
            // 
            // camaraToolStripMenuItem
            // 
            this.camaraToolStripMenuItem.Name = "camaraToolStripMenuItem";
            this.camaraToolStripMenuItem.Size = new System.Drawing.Size(243, 24);
            this.camaraToolStripMenuItem.Text = "📷  Cámara interna / USB";
            this.camaraToolStripMenuItem.Click += new System.EventHandler(this.camaraToolStripMenuItem_Click);
            // 
            // camarasIpToolStripMenuItem
            // 
            this.camarasIpToolStripMenuItem.Name = "camarasIpToolStripMenuItem";
            this.camarasIpToolStripMenuItem.Size = new System.Drawing.Size(243, 24);
            this.camarasIpToolStripMenuItem.Text = "🌐  Cámaras IP detectadas";
            this.camarasIpToolStripMenuItem.Click += new System.EventHandler(this.camarasIpToolStripMenuItem_Click);
            // 
            // separadorCamaraToolStripMenuItem
            // 
            this.separadorCamaraToolStripMenuItem.Name = "separadorCamaraToolStripMenuItem";
            this.separadorCamaraToolStripMenuItem.Size = new System.Drawing.Size(240, 6);
            // 
            // reconectarToolStripMenuItem
            // 
            this.reconectarToolStripMenuItem.Name = "reconectarToolStripMenuItem";
            this.reconectarToolStripMenuItem.Size = new System.Drawing.Size(243, 24);
            this.reconectarToolStripMenuItem.Text = "🔁  Reconectar cámara";
            this.reconectarToolStripMenuItem.Click += new System.EventHandler(this.reconectarToolStripMenuItem_Click);
            // 
            // verToolStripMenuItem
            // 
            this.verToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.visorToolStripMenuItem,
            this.carpetaToolStripMenuItem});
            this.verToolStripMenuItem.Name = "verToolStripMenuItem";
            this.verToolStripMenuItem.Size = new System.Drawing.Size(41, 23);
            this.verToolStripMenuItem.Text = "Ver";
            // 
            // visorToolStripMenuItem
            // 
            this.visorToolStripMenuItem.Name = "visorToolStripMenuItem";
            this.visorToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.visorToolStripMenuItem.Size = new System.Drawing.Size(268, 24);
            this.visorToolStripMenuItem.Text = "🌍  Visor web (álbum)";
            this.visorToolStripMenuItem.Click += new System.EventHandler(this.visorToolStripMenuItem_Click);
            // 
            // carpetaToolStripMenuItem
            // 
            this.carpetaToolStripMenuItem.Name = "carpetaToolStripMenuItem";
            this.carpetaToolStripMenuItem.Size = new System.Drawing.Size(268, 24);
            this.carpetaToolStripMenuItem.Text = "📁  Abrir carpeta facturas";
            this.carpetaToolStripMenuItem.Click += new System.EventHandler(this.carpetaToolStripMenuItem_Click);
            // 
            // utilidadesToolStripMenuItem
            // 
            this.utilidadesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.conversorIMGPDFToolStripMenuItem,
            this.analizarPhashDeTodasLasFacturasToolStripMenuItem,
            this.buscarDuplicadosToolStripMenuItem});
            this.utilidadesToolStripMenuItem.Name = "utilidadesToolStripMenuItem";
            this.utilidadesToolStripMenuItem.Size = new System.Drawing.Size(81, 23);
            this.utilidadesToolStripMenuItem.Text = "Utilidades";
            // 
            // conversorIMGPDFToolStripMenuItem
            // 
            this.conversorIMGPDFToolStripMenuItem.Name = "conversorIMGPDFToolStripMenuItem";
            this.conversorIMGPDFToolStripMenuItem.Size = new System.Drawing.Size(296, 24);
            this.conversorIMGPDFToolStripMenuItem.Text = "Conversor IMG > PDF";
            this.conversorIMGPDFToolStripMenuItem.Click += new System.EventHandler(this.conversorIMGPDFToolStripMenuItem_Click);
            // 
            // analizarPhashDeTodasLasFacturasToolStripMenuItem
            // 
            this.analizarPhashDeTodasLasFacturasToolStripMenuItem.Name = "analizarPhashDeTodasLasFacturasToolStripMenuItem";
            this.analizarPhashDeTodasLasFacturasToolStripMenuItem.Size = new System.Drawing.Size(296, 24);
            this.analizarPhashDeTodasLasFacturasToolStripMenuItem.Text = "Analizar Phash de todas las facturas";
            this.analizarPhashDeTodasLasFacturasToolStripMenuItem.Click += new System.EventHandler(this.analizarPhashDeTodasLasFacturasToolStripMenuItem_Click);
            // 
            // buscarDuplicadosToolStripMenuItem
            // 
            this.buscarDuplicadosToolStripMenuItem.Name = "buscarDuplicadosToolStripMenuItem";
            this.buscarDuplicadosToolStripMenuItem.Size = new System.Drawing.Size(296, 24);
            this.buscarDuplicadosToolStripMenuItem.Text = "Buscar Duplicados";
            this.buscarDuplicadosToolStripMenuItem.Click += new System.EventHandler(this.buscarDuplicadosToolStripMenuItem_Click);
            // 
            // aPIToolStripMenuItem
            // 
            this.aPIToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editarClavesAPIToolStripMenuItem});
            this.aPIToolStripMenuItem.Name = "aPIToolStripMenuItem";
            this.aPIToolStripMenuItem.Size = new System.Drawing.Size(42, 23);
            this.aPIToolStripMenuItem.Text = "API";
            // 
            // editarClavesAPIToolStripMenuItem
            // 
            this.editarClavesAPIToolStripMenuItem.Name = "editarClavesAPIToolStripMenuItem";
            this.editarClavesAPIToolStripMenuItem.Size = new System.Drawing.Size(178, 24);
            this.editarClavesAPIToolStripMenuItem.Text = "Editar claves API";
            this.editarClavesAPIToolStripMenuItem.Click += new System.EventHandler(this.editarClavesAPIToolStripMenuItem_Click);
            // 
            // ayudaToolStripMenuItem
            // 
            this.ayudaToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem,
            this.logToolStripMenuItem});
            this.ayudaToolStripMenuItem.Name = "ayudaToolStripMenuItem";
            this.ayudaToolStripMenuItem.Size = new System.Drawing.Size(60, 23);
            this.ayudaToolStripMenuItem.Text = "Ayuda";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(239, 24);
            this.aboutToolStripMenuItem.Text = "ℹ️  Acerca de FACTicket";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // logToolStripMenuItem
            // 
            this.logToolStripMenuItem.Name = "logToolStripMenuItem";
            this.logToolStripMenuItem.Size = new System.Drawing.Size(239, 24);
            this.logToolStripMenuItem.Text = "🐛  Ver log de depuración";
            this.logToolStripMenuItem.Click += new System.EventHandler(this.logToolStripMenuItem_Click);
            // 
            // separadorMenuToolbarToolStripMenuItem
            // 
            this.separadorMenuToolbarToolStripMenuItem.AutoSize = false;
            this.separadorMenuToolbarToolStripMenuItem.Name = "separadorMenuToolbarToolStripMenuItem";
            this.separadorMenuToolbarToolStripMenuItem.Size = new System.Drawing.Size(76, 6);
            // 
            // btnAbrirRapido
            // 
            this.btnAbrirRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnAbrirRapido.Name = "btnAbrirRapido";
            this.btnAbrirRapido.Size = new System.Drawing.Size(23, 20);
            this.btnAbrirRapido.Text = "📂";
            this.btnAbrirRapido.ToolTipText = "Abrir imagen desde archivo (Ctrl+O)";
            this.btnAbrirRapido.Click += new System.EventHandler(this.abrirToolStripMenuItem_Click);
            // 
            // btnGuardarRapido
            // 
            this.btnGuardarRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnGuardarRapido.Name = "btnGuardarRapido";
            this.btnGuardarRapido.Size = new System.Drawing.Size(23, 20);
            this.btnGuardarRapido.Text = "💾";
            this.btnGuardarRapido.ToolTipText = "Guardar factura procesada (Ctrl+S)";
            this.btnGuardarRapido.Click += new System.EventHandler(this.guardarToolStripMenuItem_Click);
            // 
            // separadorToolbar2ToolStripMenuItem
            // 
            this.separadorToolbar2ToolStripMenuItem.Name = "separadorToolbar2ToolStripMenuItem";
            this.separadorToolbar2ToolStripMenuItem.Size = new System.Drawing.Size(6, 23);
            // 
            // btnCarpetaRapida
            // 
            this.btnCarpetaRapida.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnCarpetaRapida.Name = "btnCarpetaRapida";
            this.btnCarpetaRapida.Size = new System.Drawing.Size(23, 20);
            this.btnCarpetaRapida.Text = "🗂️";
            this.btnCarpetaRapida.ToolTipText = "Abrir carpeta de facturas";
            this.btnCarpetaRapida.Click += new System.EventHandler(this.carpetaToolStripMenuItem_Click);
            // 
            // btnVisorRapido
            // 
            this.btnVisorRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnVisorRapido.Name = "btnVisorRapido";
            this.btnVisorRapido.Size = new System.Drawing.Size(23, 20);
            this.btnVisorRapido.Text = "🌐";
            this.btnVisorRapido.ToolTipText = "Abrir visor web de facturas (Ctrl+W)";
            this.btnVisorRapido.Click += new System.EventHandler(this.visorToolStripMenuItem_Click);
            // 
            // separadorToolbarToolStripMenuItem
            // 
            this.separadorToolbarToolStripMenuItem.Name = "separadorToolbarToolStripMenuItem";
            this.separadorToolbarToolStripMenuItem.Size = new System.Drawing.Size(6, 23);
            // 
            // cmbTipoCamara
            // 
            this.cmbTipoCamara.AutoSize = false;
            this.cmbTipoCamara.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTipoCamara.Name = "cmbTipoCamara";
            this.cmbTipoCamara.Size = new System.Drawing.Size(80, 23);
            this.cmbTipoCamara.ToolTipText = "Tipo de cámara";
            this.cmbTipoCamara.SelectedIndexChanged += new System.EventHandler(this.CmbTipoCamara_SelectedIndexChanged);
            // 
            // txtUrlCamara
            // 
            this.txtUrlCamara.AutoSize = false;
            this.txtUrlCamara.Name = "txtUrlCamara";
            this.txtUrlCamara.ReadOnly = true;
            this.txtUrlCamara.Size = new System.Drawing.Size(180, 23);
            this.txtUrlCamara.ToolTipText = "Fuente de la cámara activa";
            // 
            // cmbResultadoCamara
            // 
            this.cmbResultadoCamara.AutoSize = false;
            this.cmbResultadoCamara.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbResultadoCamara.Name = "cmbResultadoCamara";
            this.cmbResultadoCamara.Size = new System.Drawing.Size(120, 23);
            this.cmbResultadoCamara.ToolTipText = "Seleccionar cámara encontrada";
            this.cmbResultadoCamara.SelectedIndexChanged += new System.EventHandler(this.CmbResultadoCamara_SelectedIndexChanged);
            // 
            // btnBuscarCamara
            // 
            this.btnBuscarCamara.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnBuscarCamara.Name = "btnBuscarCamara";
            this.btnBuscarCamara.Size = new System.Drawing.Size(23, 20);
            this.btnBuscarCamara.Text = "🔍";
            this.btnBuscarCamara.ToolTipText = "Buscar cámaras";
            this.btnBuscarCamara.Click += new System.EventHandler(this.BtnBuscarCamara_Click);
            // 
            // btnReconectarRapido
            // 
            this.btnReconectarRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnReconectarRapido.Name = "btnReconectarRapido";
            this.btnReconectarRapido.Size = new System.Drawing.Size(23, 20);
            this.btnReconectarRapido.Text = "🔁";
            this.btnReconectarRapido.ToolTipText = "Reconectar última cámara";
            this.btnReconectarRapido.Click += new System.EventHandler(this.BtnReconectarRapido_Click);
            // 
            // btnCerrarVisor
            // 
            this.btnCerrarVisor.BackColor = System.Drawing.Color.Firebrick;
            this.btnCerrarVisor.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCerrarVisor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCerrarVisor.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnCerrarVisor.ForeColor = System.Drawing.Color.White;
            this.btnCerrarVisor.Location = new System.Drawing.Point(1240, 0);
            this.btnCerrarVisor.Name = "btnCerrarVisor";
            this.btnCerrarVisor.Size = new System.Drawing.Size(40, 38);
            this.btnCerrarVisor.TabIndex = 2;
            this.btnCerrarVisor.Text = "✕";
            this.btnCerrarVisor.UseVisualStyleBackColor = false;
            this.btnCerrarVisor.Click += new System.EventHandler(this.btnCerrarVisor_Click);
            // 
            // panelNavModal
            // 
            this.panelNavModal.AutoSize = true;
            this.panelNavModal.BackColor = System.Drawing.Color.Transparent;
            this.panelNavModal.Controls.Add(this.btnAnteriorVisor);
            this.panelNavModal.Controls.Add(this.lblTituloModal);
            this.panelNavModal.Controls.Add(this.btnSiguienteVisor);
            this.panelNavModal.Controls.Add(this.btnEditarVisor);
            this.panelNavModal.Controls.Add(this.btnEliminarVisor);
            this.panelNavModal.Controls.Add(this.btnCerrarModalVisor);
            this.panelNavModal.Location = new System.Drawing.Point(490, 3);
            this.panelNavModal.Name = "panelNavModal";
            this.panelNavModal.Size = new System.Drawing.Size(307, 32);
            this.panelNavModal.TabIndex = 3;
            this.panelNavModal.Visible = false;
            this.panelNavModal.WrapContents = false;
            // 
            // btnAnteriorVisor
            // 
            this.btnAnteriorVisor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAnteriorVisor.ForeColor = System.Drawing.Color.White;
            this.btnAnteriorVisor.Location = new System.Drawing.Point(2, 0);
            this.btnAnteriorVisor.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.btnAnteriorVisor.Name = "btnAnteriorVisor";
            this.btnAnteriorVisor.Size = new System.Drawing.Size(32, 30);
            this.btnAnteriorVisor.TabIndex = 0;
            this.btnAnteriorVisor.Text = "◀";
            this.btnAnteriorVisor.UseVisualStyleBackColor = true;
            this.btnAnteriorVisor.Click += new System.EventHandler(this.btnAnteriorVisor_Click);
            // 
            // lblTituloModal
            // 
            this.lblTituloModal.AutoSize = true;
            this.lblTituloModal.ForeColor = System.Drawing.Color.White;
            this.lblTituloModal.Location = new System.Drawing.Point(42, 8);
            this.lblTituloModal.Margin = new System.Windows.Forms.Padding(6, 8, 6, 0);
            this.lblTituloModal.Name = "lblTituloModal";
            this.lblTituloModal.Size = new System.Drawing.Size(99, 15);
            this.lblTituloModal.TabIndex = 1;
            this.lblTituloModal.Text = "(sin empresa) · —";
            // 
            // btnSiguienteVisor
            // 
            this.btnSiguienteVisor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSiguienteVisor.ForeColor = System.Drawing.Color.White;
            this.btnSiguienteVisor.Location = new System.Drawing.Point(149, 0);
            this.btnSiguienteVisor.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.btnSiguienteVisor.Name = "btnSiguienteVisor";
            this.btnSiguienteVisor.Size = new System.Drawing.Size(32, 30);
            this.btnSiguienteVisor.TabIndex = 2;
            this.btnSiguienteVisor.Text = "▶";
            this.btnSiguienteVisor.UseVisualStyleBackColor = true;
            this.btnSiguienteVisor.Click += new System.EventHandler(this.btnSiguienteVisor_Click);
            // 
            // btnEditarVisor
            // 
            this.btnEditarVisor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEditarVisor.Location = new System.Drawing.Point(193, 0);
            this.btnEditarVisor.Margin = new System.Windows.Forms.Padding(10, 0, 2, 0);
            this.btnEditarVisor.Name = "btnEditarVisor";
            this.btnEditarVisor.Size = new System.Drawing.Size(32, 30);
            this.btnEditarVisor.TabIndex = 3;
            this.btnEditarVisor.Text = "✏️";
            this.btnEditarVisor.UseVisualStyleBackColor = true;
            this.btnEditarVisor.Click += new System.EventHandler(this.btnEditarVisor_Click);
            // 
            // btnEliminarVisor
            // 
            this.btnEliminarVisor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEliminarVisor.ForeColor = System.Drawing.Color.Firebrick;
            this.btnEliminarVisor.Location = new System.Drawing.Point(229, 0);
            this.btnEliminarVisor.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.btnEliminarVisor.Name = "btnEliminarVisor";
            this.btnEliminarVisor.Size = new System.Drawing.Size(32, 30);
            this.btnEliminarVisor.TabIndex = 4;
            this.btnEliminarVisor.Text = "➖";
            this.btnEliminarVisor.UseVisualStyleBackColor = true;
            this.btnEliminarVisor.Click += new System.EventHandler(this.btnEliminarVisor_Click);
            // 
            // btnCerrarModalVisor
            // 
            this.btnCerrarModalVisor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCerrarModalVisor.Location = new System.Drawing.Point(273, 0);
            this.btnCerrarModalVisor.Margin = new System.Windows.Forms.Padding(10, 0, 2, 0);
            this.btnCerrarModalVisor.Name = "btnCerrarModalVisor";
            this.btnCerrarModalVisor.Size = new System.Drawing.Size(32, 30);
            this.btnCerrarModalVisor.TabIndex = 5;
            this.btnCerrarModalVisor.Text = "✕";
            this.btnCerrarModalVisor.UseVisualStyleBackColor = true;
            this.btnCerrarModalVisor.Click += new System.EventHandler(this.btnCerrarModalVisor_Click);
            // 
            // panelBarraVisor
            // 
            this.panelBarraVisor.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(26)))), ((int)(((byte)(115)))), ((int)(((byte)(232)))));
            this.panelBarraVisor.Controls.Add(this.panelNavModal);
            this.panelBarraVisor.Controls.Add(this.lblEventosVisor);
            this.panelBarraVisor.Controls.Add(this.lblEstadoVisor);
            this.panelBarraVisor.Controls.Add(this.btnCerrarVisor);
            this.panelBarraVisor.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelBarraVisor.Location = new System.Drawing.Point(0, 0);
            this.panelBarraVisor.Name = "panelBarraVisor";
            this.panelBarraVisor.Size = new System.Drawing.Size(1280, 38);
            this.panelBarraVisor.TabIndex = 0;
            this.panelBarraVisor.Resize += new System.EventHandler(this.panelBarraVisor_Resize);
            // 
            // lblEventosVisor
            // 
            this.lblEventosVisor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEventosVisor.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblEventosVisor.ForeColor = System.Drawing.Color.White;
            this.lblEventosVisor.Location = new System.Drawing.Point(174, 0);
            this.lblEventosVisor.Name = "lblEventosVisor";
            this.lblEventosVisor.Padding = new System.Windows.Forms.Padding(0, 9, 0, 0);
            this.lblEventosVisor.Size = new System.Drawing.Size(1066, 38);
            this.lblEventosVisor.TabIndex = 0;
            this.lblEventosVisor.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblEstadoVisor
            // 
            this.lblEstadoVisor.AutoSize = true;
            this.lblEstadoVisor.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblEstadoVisor.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblEstadoVisor.ForeColor = System.Drawing.Color.White;
            this.lblEstadoVisor.Location = new System.Drawing.Point(0, 0);
            this.lblEstadoVisor.Name = "lblEstadoVisor";
            this.lblEstadoVisor.Padding = new System.Windows.Forms.Padding(10, 9, 8, 0);
            this.lblEstadoVisor.Size = new System.Drawing.Size(174, 29);
            this.lblEstadoVisor.TabIndex = 1;
            this.lblEstadoVisor.Text = "📊 Panel de Facturas";
            // 
            // panelIzquierdo
            // 
            this.panelIzquierdo.Controls.Add(this.pictureBox1);
            this.panelIzquierdo.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelIzquierdo.Location = new System.Drawing.Point(0, 27);
            this.panelIzquierdo.Name = "panelIzquierdo";
            this.panelIzquierdo.Padding = new System.Windows.Forms.Padding(4);
            this.panelIzquierdo.Size = new System.Drawing.Size(200, 773);
            this.panelIzquierdo.TabIndex = 1;
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.Black;
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(4, 4);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(192, 765);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // panelDerecho
            // 
            this.panelDerecho.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.panelDerecho.Controls.Add(this.panelScrollable);
            this.panelDerecho.Controls.Add(this.panelBotones);
            this.panelDerecho.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDerecho.Location = new System.Drawing.Point(200, 27);
            this.panelDerecho.Name = "panelDerecho";
            this.panelDerecho.Padding = new System.Windows.Forms.Padding(8);
            this.panelDerecho.Size = new System.Drawing.Size(1080, 773);
            this.panelDerecho.TabIndex = 0;
            // 
            // panelScrollable
            // 
            this.panelScrollable.AutoScroll = true;
            this.panelScrollable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelScrollable.Location = new System.Drawing.Point(8, 8);
            this.panelScrollable.Name = "panelScrollable";
            this.panelScrollable.Size = new System.Drawing.Size(1064, 377);
            this.panelScrollable.TabIndex = 0;
            // 
            // panelBotones
            // 
            this.panelBotones.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
            this.panelBotones.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBotones.Location = new System.Drawing.Point(8, 385);
            this.panelBotones.Name = "panelBotones";
            this.panelBotones.Padding = new System.Windows.Forms.Padding(8);
            this.panelBotones.Size = new System.Drawing.Size(1064, 380);
            this.panelBotones.TabIndex = 1;
            // 
            // panelVisor
            // 
            this.panelVisor.Controls.Add(this.webViewAlbum);
            this.panelVisor.Controls.Add(this.panelBarraVisor);
            this.panelVisor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelVisor.Location = new System.Drawing.Point(0, 27);
            this.panelVisor.Name = "panelVisor";
            this.panelVisor.Size = new System.Drawing.Size(1280, 773);
            this.panelVisor.TabIndex = 2;
            this.panelVisor.Visible = false;
            // 
            // webViewAlbum
            // 
            this.webViewAlbum.AllowExternalDrop = true;
            this.webViewAlbum.CreationProperties = null;
            this.webViewAlbum.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webViewAlbum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webViewAlbum.Location = new System.Drawing.Point(0, 38);
            this.webViewAlbum.Name = "webViewAlbum";
            this.webViewAlbum.Size = new System.Drawing.Size(1280, 735);
            this.webViewAlbum.TabIndex = 0;
            this.webViewAlbum.ZoomFactor = 1D;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 800);
            this.Controls.Add(this.panelDerecho);
            this.Controls.Add(this.panelIzquierdo);
            this.Controls.Add(this.panelVisor);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "FACTicket Scanner";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.panelNavModal.ResumeLayout(false);
            this.panelNavModal.PerformLayout();
            this.panelBarraVisor.ResumeLayout(false);
            this.panelBarraVisor.PerformLayout();
            this.panelIzquierdo.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.panelDerecho.ResumeLayout(false);
            this.panelVisor.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webViewAlbum)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem archivoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abrirToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem guardarToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator separadorArchivoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem salirToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem camaraMenuToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem camaraToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem camarasIpToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator separadorCamaraToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reconectarToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem verToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem visorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem carpetaToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ayudaToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator separadorMenuToolbarToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logToolStripMenuItem;
        private System.Windows.Forms.Panel panelIzquierdo;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Panel panelDerecho;
        private System.Windows.Forms.Panel panelScrollable;
        private System.Windows.Forms.Panel panelBotones;
        private System.Windows.Forms.Panel panelVisor;
        private Microsoft.Web.WebView2.WinForms.WebView2 webViewAlbum;
        private System.Windows.Forms.ToolStripComboBox cmbTipoCamara;
        private System.Windows.Forms.ToolStripTextBox txtUrlCamara;
        private System.Windows.Forms.ToolStripComboBox cmbResultadoCamara;
        private System.Windows.Forms.ToolStripButton btnBuscarCamara;
        private System.Windows.Forms.ToolStripButton btnReconectarRapido;
        private System.Windows.Forms.ToolStripSeparator separadorToolbarToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton btnVisorRapido;
        private System.Windows.Forms.ToolStripButton btnCarpetaRapida;
        private System.Windows.Forms.ToolStripSeparator separadorToolbar2ToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton btnGuardarRapido;
        private System.Windows.Forms.ToolStripButton btnAbrirRapido;
        private System.Windows.Forms.Panel panelBarraVisor;
        private System.Windows.Forms.Label lblEstadoVisor;
        private System.Windows.Forms.Label lblEventosVisor;
        private System.Windows.Forms.Button btnCerrarVisor;
        private System.Windows.Forms.FlowLayoutPanel panelNavModal;
        private System.Windows.Forms.Button btnAnteriorVisor;
        private System.Windows.Forms.Label lblTituloModal;
        private System.Windows.Forms.Button btnSiguienteVisor;
        private System.Windows.Forms.Button btnEditarVisor;
        private System.Windows.Forms.Button btnEliminarVisor;
        private System.Windows.Forms.Button btnCerrarModalVisor;
        private System.Windows.Forms.ToolStripMenuItem aPIToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editarClavesAPIToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem utilidadesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem conversorIMGPDFToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem analizarPhashDeTodasLasFacturasToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportarToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem buscarDuplicadosToolStripMenuItem;
    }
}