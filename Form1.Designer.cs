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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.archivoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.abrirToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.guardarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.aPIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editarClavesAPIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ayudaToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnExportar = new System.Windows.Forms.ToolStripButton();
            this.btnCerrarVisor = new System.Windows.Forms.ToolStripButton();
            this.btnReconectarRapido = new System.Windows.Forms.ToolStripButton();
            this.btnBuscarCamara = new System.Windows.Forms.ToolStripButton();
            this.cmbResultadoCamara = new System.Windows.Forms.ToolStripComboBox();
            this.txtUrlCamara = new System.Windows.Forms.ToolStripTextBox();
            this.cmbTipoCamara = new System.Windows.Forms.ToolStripComboBox();
            this.separadorToolbarToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.btnVisorRapido = new System.Windows.Forms.ToolStripButton();
            this.btnCarpetaRapida = new System.Windows.Forms.ToolStripButton();
            this.separadorToolbar2ToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.btnGuardarRapido = new System.Windows.Forms.ToolStripButton();
            this.btnAbrirRapido = new System.Windows.Forms.ToolStripButton();
            this.panelIzquierdo = new System.Windows.Forms.Panel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.panelDerecho = new System.Windows.Forms.Panel();
            this.panelScrollable = new System.Windows.Forms.Panel();
            this.panelBotones = new System.Windows.Forms.Panel();
            this.panelVisor = new System.Windows.Forms.Panel();
            this.webViewAlbum = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.utilidadesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.conversorIMGPDFToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
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
            this.btnExportar,
            this.btnCerrarVisor,
            this.btnReconectarRapido,
            this.btnBuscarCamara,
            this.cmbResultadoCamara,
            this.txtUrlCamara,
            this.cmbTipoCamara,
            this.separadorToolbarToolStripMenuItem,
            this.btnVisorRapido,
            this.btnCarpetaRapida,
            this.separadorToolbar2ToolStripMenuItem,
            this.btnGuardarRapido,
            this.btnAbrirRapido});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.ShowItemToolTips = true;
            this.menuStrip1.Size = new System.Drawing.Size(1280, 30);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // archivoToolStripMenuItem
            // 
            this.archivoToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.abrirToolStripMenuItem,
            this.guardarToolStripMenuItem,
            this.separadorArchivoToolStripMenuItem,
            this.salirToolStripMenuItem});
            this.archivoToolStripMenuItem.Name = "archivoToolStripMenuItem";
            this.archivoToolStripMenuItem.Size = new System.Drawing.Size(67, 26);
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
            this.camaraMenuToolStripMenuItem.Size = new System.Drawing.Size(68, 26);
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
            this.verToolStripMenuItem.Size = new System.Drawing.Size(41, 26);
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
            // aPIToolStripMenuItem
            // 
            this.aPIToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editarClavesAPIToolStripMenuItem});
            this.aPIToolStripMenuItem.Name = "aPIToolStripMenuItem";
            this.aPIToolStripMenuItem.Size = new System.Drawing.Size(42, 26);
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
            this.ayudaToolStripMenuItem.Size = new System.Drawing.Size(60, 26);
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
            // btnExportar
            // 
            this.btnExportar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnExportar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnExportar.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnExportar.Name = "btnExportar";
            this.btnExportar.Size = new System.Drawing.Size(75, 23);
            this.btnExportar.Text = "📦 Exportar";
            this.btnExportar.ToolTipText = "Exportar documentos";
            this.btnExportar.Visible = false;
            this.btnExportar.Click += new System.EventHandler(this.btnExportar_Click);
            // 
            // btnCerrarVisor
            // 
            this.btnCerrarVisor.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnCerrarVisor.BackColor = System.Drawing.Color.Firebrick;
            this.btnCerrarVisor.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnCerrarVisor.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnCerrarVisor.ForeColor = System.Drawing.Color.White;
            this.btnCerrarVisor.Name = "btnCerrarVisor";
            this.btnCerrarVisor.Size = new System.Drawing.Size(25, 23);
            this.btnCerrarVisor.Text = "✕";
            this.btnCerrarVisor.ToolTipText = "Cerrar visor web";
            this.btnCerrarVisor.Visible = false;
            this.btnCerrarVisor.Click += new System.EventHandler(this.btnCerrarVisor_Click);
            // 
            // btnReconectarRapido
            // 
            this.btnReconectarRapido.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnReconectarRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnReconectarRapido.Name = "btnReconectarRapido";
            this.btnReconectarRapido.Size = new System.Drawing.Size(23, 23);
            this.btnReconectarRapido.Text = "🔁";
            this.btnReconectarRapido.ToolTipText = "Reconectar última cámara";
            this.btnReconectarRapido.Click += new System.EventHandler(this.BtnReconectarRapido_Click);
            // 
            // btnBuscarCamara
            // 
            this.btnBuscarCamara.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnBuscarCamara.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnBuscarCamara.Name = "btnBuscarCamara";
            this.btnBuscarCamara.Size = new System.Drawing.Size(23, 23);
            this.btnBuscarCamara.Text = "🔍";
            this.btnBuscarCamara.ToolTipText = "Buscar cámaras";
            this.btnBuscarCamara.Click += new System.EventHandler(this.BtnBuscarCamara_Click);
            // 
            // cmbResultadoCamara
            // 
            this.cmbResultadoCamara.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.cmbResultadoCamara.AutoSize = false;
            this.cmbResultadoCamara.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbResultadoCamara.Name = "cmbResultadoCamara";
            this.cmbResultadoCamara.Size = new System.Drawing.Size(120, 23);
            this.cmbResultadoCamara.ToolTipText = "Seleccionar cámara encontrada";
            this.cmbResultadoCamara.SelectedIndexChanged += new System.EventHandler(this.CmbResultadoCamara_SelectedIndexChanged);
            // 
            // txtUrlCamara
            // 
            this.txtUrlCamara.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.txtUrlCamara.AutoSize = false;
            this.txtUrlCamara.Name = "txtUrlCamara";
            this.txtUrlCamara.ReadOnly = true;
            this.txtUrlCamara.Size = new System.Drawing.Size(180, 23);
            this.txtUrlCamara.ToolTipText = "Fuente de la cámara activa";
            // 
            // cmbTipoCamara
            // 
            this.cmbTipoCamara.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.cmbTipoCamara.AutoSize = false;
            this.cmbTipoCamara.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTipoCamara.Name = "cmbTipoCamara";
            this.cmbTipoCamara.Size = new System.Drawing.Size(80, 23);
            this.cmbTipoCamara.ToolTipText = "Tipo de cámara";
            this.cmbTipoCamara.SelectedIndexChanged += new System.EventHandler(this.CmbTipoCamara_SelectedIndexChanged);
            // 
            // separadorToolbarToolStripMenuItem
            // 
            this.separadorToolbarToolStripMenuItem.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.separadorToolbarToolStripMenuItem.Name = "separadorToolbarToolStripMenuItem";
            this.separadorToolbarToolStripMenuItem.Size = new System.Drawing.Size(6, 26);
            // 
            // btnVisorRapido
            // 
            this.btnVisorRapido.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnVisorRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnVisorRapido.Name = "btnVisorRapido";
            this.btnVisorRapido.Size = new System.Drawing.Size(23, 23);
            this.btnVisorRapido.Text = "🌐";
            this.btnVisorRapido.ToolTipText = "Abrir visor web de facturas (Ctrl+W)";
            this.btnVisorRapido.Click += new System.EventHandler(this.visorToolStripMenuItem_Click);
            // 
            // btnCarpetaRapida
            // 
            this.btnCarpetaRapida.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnCarpetaRapida.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnCarpetaRapida.Name = "btnCarpetaRapida";
            this.btnCarpetaRapida.Size = new System.Drawing.Size(23, 23);
            this.btnCarpetaRapida.Text = "🗂️";
            this.btnCarpetaRapida.ToolTipText = "Abrir carpeta de facturas";
            this.btnCarpetaRapida.Click += new System.EventHandler(this.carpetaToolStripMenuItem_Click);
            // 
            // separadorToolbar2ToolStripMenuItem
            // 
            this.separadorToolbar2ToolStripMenuItem.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.separadorToolbar2ToolStripMenuItem.Name = "separadorToolbar2ToolStripMenuItem";
            this.separadorToolbar2ToolStripMenuItem.Size = new System.Drawing.Size(6, 26);
            // 
            // btnGuardarRapido
            // 
            this.btnGuardarRapido.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnGuardarRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnGuardarRapido.Name = "btnGuardarRapido";
            this.btnGuardarRapido.Size = new System.Drawing.Size(23, 23);
            this.btnGuardarRapido.Text = "💾";
            this.btnGuardarRapido.ToolTipText = "Guardar factura procesada (Ctrl+S)";
            this.btnGuardarRapido.Click += new System.EventHandler(this.guardarToolStripMenuItem_Click);
            // 
            // btnAbrirRapido
            // 
            this.btnAbrirRapido.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnAbrirRapido.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnAbrirRapido.Name = "btnAbrirRapido";
            this.btnAbrirRapido.Size = new System.Drawing.Size(23, 23);
            this.btnAbrirRapido.Text = "📂";
            this.btnAbrirRapido.ToolTipText = "Abrir imagen desde archivo (Ctrl+O)";
            this.btnAbrirRapido.Click += new System.EventHandler(this.abrirToolStripMenuItem_Click);
            // 
            // panelIzquierdo
            // 
            this.panelIzquierdo.Controls.Add(this.pictureBox1);
            this.panelIzquierdo.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelIzquierdo.Location = new System.Drawing.Point(0, 30);
            this.panelIzquierdo.Name = "panelIzquierdo";
            this.panelIzquierdo.Padding = new System.Windows.Forms.Padding(4);
            this.panelIzquierdo.Size = new System.Drawing.Size(200, 770);
            this.panelIzquierdo.TabIndex = 1;
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.Black;
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(4, 4);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(192, 762);
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
            this.panelDerecho.Location = new System.Drawing.Point(200, 30);
            this.panelDerecho.Name = "panelDerecho";
            this.panelDerecho.Padding = new System.Windows.Forms.Padding(8);
            this.panelDerecho.Size = new System.Drawing.Size(1080, 770);
            this.panelDerecho.TabIndex = 0;
            // 
            // panelScrollable
            // 
            this.panelScrollable.AutoScroll = true;
            this.panelScrollable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelScrollable.Location = new System.Drawing.Point(8, 8);
            this.panelScrollable.Name = "panelScrollable";
            this.panelScrollable.Size = new System.Drawing.Size(1064, 374);
            this.panelScrollable.TabIndex = 0;
            // 
            // panelBotones
            // 
            this.panelBotones.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
            this.panelBotones.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBotones.Location = new System.Drawing.Point(8, 382);
            this.panelBotones.Name = "panelBotones";
            this.panelBotones.Padding = new System.Windows.Forms.Padding(8);
            this.panelBotones.Size = new System.Drawing.Size(1064, 380);
            this.panelBotones.TabIndex = 1;
            // 
            // panelVisor
            // 
            this.panelVisor.Controls.Add(this.webViewAlbum);
            this.panelVisor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelVisor.Location = new System.Drawing.Point(0, 30);
            this.panelVisor.Name = "panelVisor";
            this.panelVisor.Size = new System.Drawing.Size(1280, 770);
            this.panelVisor.TabIndex = 2;
            this.panelVisor.Visible = false;
            // 
            // webViewAlbum
            // 
            this.webViewAlbum.AllowExternalDrop = true;
            this.webViewAlbum.CreationProperties = null;
            this.webViewAlbum.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webViewAlbum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webViewAlbum.Location = new System.Drawing.Point(0, 0);
            this.webViewAlbum.Name = "webViewAlbum";
            this.webViewAlbum.Size = new System.Drawing.Size(1280, 770);
            this.webViewAlbum.TabIndex = 0;
            this.webViewAlbum.ZoomFactor = 1D;
            // 
            // utilidadesToolStripMenuItem
            // 
            this.utilidadesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.conversorIMGPDFToolStripMenuItem});
            this.utilidadesToolStripMenuItem.Name = "utilidadesToolStripMenuItem";
            this.utilidadesToolStripMenuItem.Size = new System.Drawing.Size(81, 26);
            this.utilidadesToolStripMenuItem.Text = "Utilidades";
            // 
            // conversorIMGPDFToolStripMenuItem
            // 
            this.conversorIMGPDFToolStripMenuItem.Name = "conversorIMGPDFToolStripMenuItem";
            this.conversorIMGPDFToolStripMenuItem.Size = new System.Drawing.Size(215, 24);
            this.conversorIMGPDFToolStripMenuItem.Text = "Conversor IMG > PDF";
            this.conversorIMGPDFToolStripMenuItem.Click += new System.EventHandler(this.conversorIMGPDFToolStripMenuItem_Click);
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
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "FACTicket Scanner";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
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
        private System.Windows.Forms.ToolStripButton btnCerrarVisor;
        private System.Windows.Forms.ToolStripButton btnExportar;
        private System.Windows.Forms.ToolStripMenuItem aPIToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editarClavesAPIToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem utilidadesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem conversorIMGPDFToolStripMenuItem;
    }
}