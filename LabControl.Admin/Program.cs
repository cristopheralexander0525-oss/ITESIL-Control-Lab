using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.InteropServices;
using System.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace LabControl.Admin
{
    static class Program
    {
        public static string Token = "";
        public static string Role = "";
        public static string Username = "";
        
        // Lee la IP directamente del archivo de configuración sin recompilar
        public static string BASE_URL = ConfigurationManager.AppSettings["ServidorUrl"] ?? "http://localhost:5000";

        [STAThread]
        static void Main()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LoginForm login = new LoginForm();
            if (login.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new MainForm());
            }
        }
    }

    // =====================================
    // LOGIN FORM
    // =====================================
    public class LoginForm : Form
    {
        private TextBox txtUser;
        private TextBox txtPass;
        private Label lblError;
        private static readonly HttpClient httpClient = new HttpClient();

        public LoginForm()
        {
            this.Size = new Size(400, 550);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;

            // Drag support
            this.MouseDown += Form_MouseDown;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Color.FromArgb(29, 78, 216) }; // Azul principal
            header.MouseDown += Form_MouseDown;
            Button btnClose = new Button { Text = "✕", Size = new Size(40, 40), Location = new Point(360, 0), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            
            Label lblHeaderTitle = new Label { Text = "🛡️ ITESIL LAB", ForeColor = Color.White, Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true, Location = new Point(90, 30) };
            header.Controls.Add(btnClose);
            header.Controls.Add(lblHeaderTitle);
            header.MouseDown += Form_MouseDown;
            lblHeaderTitle.MouseDown += Form_MouseDown;
            this.Controls.Add(header);

            Label lblSub = new Label { Text = "Control de Acceso Administrativo", ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoSize = true, Location = new Point(80, 120) };
            this.Controls.Add(lblSub);

            Label lblU = new Label { Text = "USUARIO", ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(50, 180), AutoSize = true };
            txtUser = new TextBox { Location = new Point(50, 205), Size = new Size(300, 35), Font = new Font("Segoe UI", 12), BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.FromArgb(15, 23, 42), BorderStyle = BorderStyle.FixedSingle };
            
            Label lblP = new Label { Text = "CONTRASEÑA", ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(50, 260), AutoSize = true };
            txtPass = new TextBox { Location = new Point(50, 285), Size = new Size(270, 35), Font = new Font("Segoe UI", 12), PasswordChar = '•', BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.FromArgb(15, 23, 42), BorderStyle = BorderStyle.FixedSingle };

            Button btnToggle = new Button { Text = "👁", Location = new Point(320, 285), Size = new Size(30, 35), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(71, 85, 105), BackColor = Color.FromArgb(241, 245, 249), Cursor = Cursors.Hand };
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Click += (s, e) => {
                if (txtPass.PasswordChar == '•') {
                    txtPass.PasswordChar = '\0';
                    btnToggle.ForeColor = Color.White;
                } else {
                    txtPass.PasswordChar = '•';
                    btnToggle.ForeColor = Color.Gray;
                }
            };

            lblError = new Label { Text = "", ForeColor = Color.FromArgb(239, 68, 68), Font = new Font("Segoe UI", 9), Location = new Point(50, 330), Size = new Size(300, 25), TextAlign = ContentAlignment.MiddleCenter };

            Button btnLogin = new Button { Text = "INICIAR SESIÓN", Location = new Point(50, 370), Size = new Size(300, 50), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += BtnLogin_Click;

            LinkLabel linkForgot = new LinkLabel { Text = "¿Olvidaste tu contraseña?", LinkColor = Color.FromArgb(37, 99, 235), ActiveLinkColor = Color.FromArgb(29, 78, 216), Location = new Point(50, 440), Size = new Size(300, 25), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10) };
            linkForgot.Click += LinkForgot_Click;

            this.Controls.Add(lblU); this.Controls.Add(txtUser);
            this.Controls.Add(lblP); this.Controls.Add(txtPass); this.Controls.Add(btnToggle);
            this.Controls.Add(lblError); this.Controls.Add(btnLogin); this.Controls.Add(linkForgot);
        }

        private async void LinkForgot_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUser.Text))
            {
                lblError.Text = "Escribe tu usuario arriba para solicitar cambio.";
                return;
            }
            
            using var rndDialog = new ResetPasswordDialog();
            if (rndDialog.ShowDialog() == DialogResult.OK)
            {
                string promptValue = rndDialog.NewPassword;
                if (!string.IsNullOrWhiteSpace(promptValue))
                {
                    lblError.Text = "Enviando solicitud...";
                    try
                    {
                        var payload = new { username = txtUser.Text.Trim(), newPassword = promptValue };
                        var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                        var response = await httpClient.PostAsync($"{Program.BASE_URL}/api/auth/request-reset", content);
                        if (response.IsSuccessStatusCode) lblError.Text = "Solicitud enviada al Administrador.";
                        else lblError.Text = "Error al enviar solicitud.";
                    }
                    catch { lblError.Text = "Error de red."; }
                }
            }
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            lblError.Text = "Conectando...";
            try
            {
                var payload = new { username = txtUser.Text.Trim(), password = txtPass.Text };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{Program.BASE_URL}/api/auth/login", content);
                if (response.IsSuccessStatusCode)
                {
                    var resString = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<dynamic>(resString);
                    if (data != null)
                    {
                        string role = data.role?.ToString() ?? "";
                        // Bloquear estudiantes: esta aplicacion es solo para personal
                        if (role.ToLower() == "student") {
                            lblError.Text = "Acceso denegado. Esta consola es solo para administradores y técnicos.";
                            return;
                        }
                        Program.Token = data.token;
                        Program.Role = role;
                        Program.Username = data.username;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
                else
                {
                    lblError.Text = "Usuario o contraseña incorrectos.";
                }
            }
            catch (Exception ex)
            {
                lblError.Text = "Error de conexión al servidor.";
            }
        }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();
        private void Form_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        }
    }

    public class ResetPasswordDialog : Form
    {
        public string NewPassword { get; private set; } = "";
        
        public ResetPasswordDialog()
        {
            this.Text = "Solicitud de Cambio";
            this.Size = new Size(350, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252); // AliceBlue / Slate-50

            Label lbl = new Label { Text = "Introduce la NUEVA contraseña:", ForeColor = Color.FromArgb(15, 23, 42), Font = new Font("Segoe UI", 10), Location = new Point(20, 20), AutoSize = true };
            TextBox txt = new TextBox { Location = new Point(20, 50), Size = new Size(290, 30), Font = new Font("Segoe UI", 11), PasswordChar = '•' };
            Button btnOk = new Button { Text = "Enviar", Location = new Point(20, 100), Size = new Size(140, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, Cursor = Cursors.Hand };
            Button btnCancel = new Button { Text = "Cancelar", Location = new Point(170, 100), Size = new Size(140, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(148, 163, 184), ForeColor = Color.White, Cursor = Cursors.Hand };

            btnOk.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.BorderSize = 0;
            
            btnOk.Click += (s, e) => { NewPassword = txt.Text; this.DialogResult = DialogResult.OK; this.Close(); };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lbl); this.Controls.Add(txt); this.Controls.Add(btnOk); this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }

    // =====================================
    // MAIN FORM
    // =====================================
    public class MainForm : Form
    {
        private Panel sidebar, mainPanel, header;
        private Label lblHeaderTitle;
        private static readonly HttpClient httpClient = new HttpClient();
        private HubConnection? hubConnection;
        private System.Windows.Forms.Timer refreshTimer;
        
        // Sidebar Toggling logic variables
        private bool isSidebarExpanded = true;
        private Label lblBrand;
        private Button btnHamburger;
        private Button btnLogout;
        private ToolTip sidebarToolTip = new ToolTip();
        private List<Button> sidebarButtons = new List<Button>();
        private TableLayoutPanel root;

        // Controllers for views
        private FlowLayoutPanel computersView;
        private Panel dashboardView;
        private Panel passwordRequestsView;
        private FlowLayoutPanel reqFlow;
        private Panel loansView;
        private DataGridView loansGrid;
        private Panel usersView;
        private DataGridView usersGrid;
        private Panel reportsView;
        private DataGridView reportsGrid;

        public MainForm()
        {
            this.Text = "ITESIL Lab Control";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.MinimumSize = new Size(1000, 700);

            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Program.Token}");

            // 1. EL CONTENEDOR MAESTRO (Indestructible)
            root = new TableLayoutPanel { 
                Dock = DockStyle.Fill, 
                ColumnCount = 2, 
                RowCount = 1,
                BackColor = Color.White
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250)); // Columna para Sidebar
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Columna para el resto

            // 2. CREAR PANELES
            sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(29, 78, 216) }; // Royal Blue
            mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 245, 249) }; // Background light
            header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.White }; 

            // 3. MONTAR ESTRUCTURA
            root.Controls.Add(sidebar, 0, 0);
            root.Controls.Add(mainPanel, 1, 0);
            this.Controls.Add(root);

            SetupSidebar(); 

            // 4. ESTRUCTURA INTERNA DE mainPanel (Header y Vistas)
            TableLayoutPanel mainLayout = new TableLayoutPanel { 
                Dock = DockStyle.Fill, 
                ColumnCount = 1, 
                RowCount = 2,
                BackColor = Color.Transparent
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); // Fila Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Fila Vistas
            mainPanel.Controls.Add(mainLayout);

            SetupHeader(); // lblHeaderTitle se prepara aquí
            mainLayout.Controls.Add(header, 0, 0);

            Panel viewHost = new Panel { Dock = DockStyle.Fill, Name = "viewHost" };
            mainLayout.Controls.Add(viewHost, 0, 1);

            computersView = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20), Visible = false };
            passwordRequestsView = CreatePasswordRequestsView();
            loansView = CreateLoansView();
            usersView = CreateUsersView();
            reportsView = CreateReportsView();
            dashboardView = CreateDashboardView();

            viewHost.Controls.Add(dashboardView);
            viewHost.Controls.Add(loansView);
            viewHost.Controls.Add(passwordRequestsView);
            viewHost.Controls.Add(computersView);
            viewHost.Controls.Add(usersView);
            viewHost.Controls.Add(reportsView);

            ShowView("Dashboard");

            // SignalR
            hubConnection = new HubConnectionBuilder()
                .WithUrl($"{Program.BASE_URL}/labHub")
                .WithAutomaticReconnect()
                .Build();

            hubConnection.On("PCStatusChanged", async () => await RefreshComputers());

            refreshTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            refreshTimer.Tick += async (s, e) => { if (computersView.Visible) await RefreshComputers(); };
            refreshTimer.Start();

            this.Load += async (s, e) => {
                await RefreshComputers();
                try { await hubConnection.StartAsync(); await hubConnection.InvokeAsync("Register", "ADMIN_CONSOLE"); } catch { }
            };
        }

        private void SetupSidebar()
        {
            lblBrand = new Label { Text = "🛡️ ITESIL LAB", ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold), AutoSize = false, Size = new Size(200, 70), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top };
            sidebar.Controls.Add(lblBrand);
            
            int yPos = 80;
            // Boton Hamburguesa como opcion del menu
            btnHamburger = CreateMenuButton("☰ Contraer Menú", yPos, () => ToggleSidebar());
            sidebar.Controls.Add(btnHamburger);
            yPos += 50;
            sidebar.Controls.Add(CreateMenuButton("🏠 Inicio", yPos, () => ShowView("Dashboard"))); yPos += 50;
            sidebar.Controls.Add(CreateMenuButton("🖥️ Control de Equipos", yPos, () => ShowView("Equipos"))); yPos += 50;
            
            if (Program.Role?.ToLower() == "admin" || Program.Role?.ToLower() == "technician")
            {
                sidebar.Controls.Add(CreateMenuButton("🔑 Gestionar Accesos", yPos, () => ShowView("PasswordRequests"))); yPos += 50;
                sidebar.Controls.Add(CreateMenuButton("🕒 Préstamos y Horarios", yPos, () => ShowView("Loans"))); yPos += 50;
                if (Program.Role?.ToLower() == "admin") {
                    sidebar.Controls.Add(CreateMenuButton("👥 Usuarios", yPos, () => ShowView("Users"))); yPos += 50;
                    sidebar.Controls.Add(CreateMenuButton("📄 Reportes de Actividad", yPos, () => ShowView("Reports"))); yPos += 50;
                }
            }

            btnLogout = new Button { Text = "Cerrar Sesión", Dock = DockStyle.Bottom, Height = 50, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(239, 68, 68), Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += async (s, e) => {
                try { await httpClient.PostAsync($"{Program.BASE_URL}/api/auth/logout", null); } catch { }
                Application.Restart();
            };
            sidebarToolTip.SetToolTip(btnLogout, "Cerrar Sesión");
            sidebar.Controls.Add(btnLogout);
        }

        private void ToggleSidebar()
        {
            isSidebarExpanded = !isSidebarExpanded;
            sidebar.Width = isSidebarExpanded ? 250 : 60;
            if (root != null) root.ColumnStyles[0].Width = isSidebarExpanded ? 250 : 60;
            
            lblBrand.Visible = isSidebarExpanded;
            btnHamburger.Text = isSidebarExpanded ? "☰ Contraer Menú" : "☰";
            btnLogout.Text = isSidebarExpanded ? "Cerrar Sesión" : "🚪";

            foreach(Button b in sidebarButtons)
            {
                if (b == btnHamburger) continue; // Lo manejamos arriba

                if (isSidebarExpanded)
                {
                    b.Text = b.Tag?.ToString(); // Restore full text
                    b.Padding = new Padding(15, 0, 0, 0);
                    b.Width = 230;
                }
                else
                {
                    // Just the emoji character
                    b.Text = b.Tag?.ToString()?.Substring(0, 2); 
                    b.Padding = new Padding(10, 0, 0, 0);
                    b.Width = 50;
                }
            }
        }

        private Button CreateMenuButton(string text, int y, Action onClick)
        {
            Button btn = new Button { Text = text, Tag = text, Location = new Point(10, y), Size = new Size(230, 45), FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15,0,0,0), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 64, 175);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 58, 138);
            
            string rawTooltipText = text.Substring(2).Trim(); 
            sidebarToolTip.SetToolTip(btn, rawTooltipText);
            btn.Click += (s, e) => onClick();
            sidebarButtons.Add(btn);
            return btn;
        }

        private void SetupHeader()
        {
            header.Controls.Clear();
            lblHeaderTitle = new Label { Text = "Vista de Inicio", ForeColor = Color.FromArgb(30, 41, 59), Font = new Font("Segoe UI", 18, FontStyle.Bold), Location = new Point(30, 20), AutoSize = true };
            header.Controls.Add(lblHeaderTitle);

            Label lblUser = new Label { Text = $"{Program.Username} ({Program.Role?.ToUpper()})", ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(400, 25), AutoSize = false, Size = new Size(300, 25), TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            header.Controls.Add(lblUser);
            
            // Separador sutil
            Panel line = new Panel { BackColor = Color.FromArgb(226, 232, 240), Height = 1, Dock = DockStyle.Bottom };
            header.Controls.Add(line);
        }

        private void ShowView(string view)
        {
            // Ocultar TODO
            if (dashboardView != null) dashboardView.Visible = false;
            if (computersView != null) computersView.Visible = false;
            if (passwordRequestsView != null) passwordRequestsView.Visible = false;
            if (loansView != null) loansView.Visible = false;
            if (usersView != null) usersView.Visible = false;
            if (reportsView != null) reportsView.Visible = false;

            Control target = null;

            if (view == "Dashboard") {
                target = dashboardView;
                lblHeaderTitle.Text = "Vista de Inicio";
            }
            else if (view == "Equipos") {
                target = computersView;
                lblHeaderTitle.Text = "Control de Equipos";
                _ = RefreshComputers();
            }
            else if (view == "PasswordRequests") {
                target = passwordRequestsView;
                lblHeaderTitle.Text = "Solicitudes de Acceso";
                _ = RefreshPasswordRequests();
            }
            else if (view == "Loans") {
                target = loansView;
                lblHeaderTitle.Text = "Historial de Préstamos";
                _ = RefreshLoans();
            }
            else if (view == "Users") {
                target = usersView;
                lblHeaderTitle.Text = "Administración de Usuarios";
                _ = RefreshUsers();
            }
            else if (view == "Reports") {
                target = reportsView;
                lblHeaderTitle.Text = "Reportes del Sistema";
            }

            if (target != null) {
                target.Visible = true;
                target.BringToFront();
            }
        }

        private void ApplyGridTheme(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Color.White;
            grid.GridColor = Color.FromArgb(226, 232, 240);
            grid.RowTemplate.Height = 40;
            grid.ColumnHeadersHeight = 45;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.ReadOnly = true;

            // Header Style
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle {
                BackColor = Color.FromArgb(30, 58, 138), // Navy Blue
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                SelectionBackColor = Color.FromArgb(30, 58, 138),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };

            // Rows Style
            grid.DefaultCellStyle = new DataGridViewCellStyle {
                BackColor = Color.White,
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 10),
                SelectionBackColor = Color.FromArgb(219, 234, 254), // Soft Blue
                SelectionForeColor = Color.FromArgb(30, 58, 138)
            };

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        }

        // =====================================
        // REPORTS VIEW
        // =====================================
        private Panel CreateReportsView()
        {
            reportsView = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), Visible = false };

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Selector de tipo
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // Filtros
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Grid

            // ROW 0 — TIPO DE REPORTE
            Panel pnlType = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 5) };
            Label lblType = new Label { Text = "Tipo:", ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 10), Location = new Point(20, 18), AutoSize = true };
            ComboBox cmbType = new ComboBox { Location = new Point(65, 13), Size = new Size(280, 32), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11), Name = "cmbType" };
            cmbType.Items.AddRange(new object[] { "Historial de Reservas", "Uso de Equipos", "Actividad de Usuarios" });
            cmbType.SelectedIndex = 0;
            pnlType.Controls.Add(lblType); pnlType.Controls.Add(cmbType);

            // ROW 1 — FILTROS
            Panel pnlFilters = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 5) };

            Label lblFrom = new Label { Text = "Desde:", ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 10), Location = new Point(20, 22), AutoSize = true };
            DateTimePicker dtpFrom = new DateTimePicker { Location = new Point(75, 17), Size = new Size(170, 28), Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10), Name = "dtpFrom", Value = DateTime.Today.AddMonths(-1) };

            Label lblTo = new Label { Text = "Hasta:", ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 10), Location = new Point(260, 22), AutoSize = true };
            DateTimePicker dtpTo = new DateTimePicker { Location = new Point(315, 17), Size = new Size(170, 28), Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10), Name = "dtpTo", Value = DateTime.Today };

            Label lblStatus = new Label { Text = "Estado:", ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 10), Location = new Point(500, 22), AutoSize = true };
            ComboBox cmbStatus = new ComboBox { Location = new Point(560, 17), Size = new Size(140, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10), Name = "cmbStatus" };
            cmbStatus.Items.AddRange(new object[] { "all", "active", "completed", "reserved", "cancelled" });
            cmbStatus.SelectedIndex = 0;

            Button btnGenerate = new Button { Text = "Generar", Location = new Point(690, 13), Size = new Size(110, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(79, 70, 229), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnGenerate.FlatAppearance.BorderSize = 0;

            Button btnExport = new Button { Text = "⬇ CSV", Location = new Point(810, 13), Size = new Size(80, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnExport.FlatAppearance.BorderSize = 0;

            Button btnPdf = new Button { Text = "📄 PDF", Location = new Point(900, 13), Size = new Size(80, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnPdf.FlatAppearance.BorderSize = 0;

            pnlFilters.Controls.Add(lblFrom); pnlFilters.Controls.Add(dtpFrom);
            pnlFilters.Controls.Add(lblTo);   pnlFilters.Controls.Add(dtpTo);
            pnlFilters.Controls.Add(lblStatus); pnlFilters.Controls.Add(cmbStatus);
            pnlFilters.Controls.Add(btnGenerate); pnlFilters.Controls.Add(btnExport); pnlFilters.Controls.Add(btnPdf);

            // ROW 2 — GRID
            reportsGrid = new DataGridView {
                Dock = DockStyle.Fill, Margin = new Padding(20, 0, 20, 20)
            };
            ApplyGridTheme(reportsGrid);

            // Eventos
            btnGenerate.Click += async (s, e) => await GenerateReport(cmbType, dtpFrom, dtpTo, cmbStatus);
            btnExport.Click   += (s, e) => ExportToCsv();
            btnPdf.Click      += (s, e) => ExportToPdf();
            cmbType.SelectedIndexChanged += (s, e) => {
                bool isReservations = cmbType.SelectedIndex == 0;
                lblStatus.Visible = isReservations;
                cmbStatus.Visible = isReservations;
            };

            layout.Controls.Add(pnlType, 0, 0);
            layout.Controls.Add(pnlFilters, 0, 1);
            layout.Controls.Add(reportsGrid, 0, 2);
            reportsView.Controls.Add(layout);
            return reportsView;
        }

        private async Task GenerateReport(ComboBox cmbType, DateTimePicker dtpFrom, DateTimePicker dtpTo, ComboBox cmbStatus)
        {
            reportsGrid.Columns.Clear();
            reportsGrid.Rows.Clear();

            string fromStr   = dtpFrom.Value.ToString("yyyy-MM-dd");
            string toStr     = dtpTo.Value.ToString("yyyy-MM-dd");
            string statusStr = cmbStatus.SelectedItem?.ToString() ?? "all";
            int reportType   = cmbType.SelectedIndex;

            string url = reportType switch {
                0 => $"{Program.BASE_URL}/api/admin/reports/reservations?from={fromStr}&to={toStr}&status={statusStr}",
                1 => $"{Program.BASE_URL}/api/admin/reports/equipment-usage?from={fromStr}&to={toStr}",
                2 => $"{Program.BASE_URL}/api/admin/reports/user-activity?from={fromStr}&to={toStr}",
                _ => ""
            };

            try {
                var res = await httpClient.GetAsync(url);
                if (!res.IsSuccessStatusCode) { MessageBox.Show("Error al obtener reporte del servidor."); return; }
                var json = await res.Content.ReadAsStringAsync();
                var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                if (rows == null || rows.Count == 0) { MessageBox.Show("No hay datos para los filtros seleccionados."); return; }

                // Build columns from first row
                var first = rows[0];
                foreach (var key in first.Keys) reportsGrid.Columns.Add(key, key.Replace("_", " ").ToUpper());

                foreach (var row in rows) {
                    int idx = reportsGrid.Rows.Add();
                    int col = 0;
                    foreach (var val in row.Values) {
                        reportsGrid.Rows[idx].Cells[col].Value = val?.ToString() ?? "";
                        col++;
                    }
                }
            } catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void ExportToCsv()
        {
            if (reportsGrid.Columns.Count == 0) { MessageBox.Show("Genera un reporte primero."); return; }

            using var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"reporte_{DateTime.Now:yyyyMMdd_HHmm}.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            var sb = new System.Text.StringBuilder();
            // Headers
            var headers = reportsGrid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText);
            sb.AppendLine(string.Join(",", headers));
            // Rows
            foreach (DataGridViewRow row in reportsGrid.Rows) {
                var cells = row.Cells.Cast<DataGridViewCell>().Select(c => $"\"{c.Value?.ToString()?.Replace("\"", "'")}\"");
                sb.AppendLine(string.Join(",", cells));
            }
            System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            MessageBox.Show($"Reporte exportado correctamente:\n{dlg.FileName}", "Exportado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportToPdf()
        {
            if (reportsGrid.Columns.Count == 0) { MessageBox.Show("Genera un reporte primero."); return; }

            using var dlg = new SaveFileDialog { Filter = "PDF File (*.pdf)|*.pdf", FileName = $"reporte_{DateTime.Now:yyyyMMdd_HHmm}.pdf" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try {
                Document.Create(container => {
                    container.Page(page => {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                        page.PageColor(QuestPDF.Helpers.Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Element(ComposeHeader);
                        page.Content().Element(ComposeContent);
                        page.Footer().AlignCenter().Text(x => {
                            x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages();
                        });
                    });
                }).GeneratePdf(dlg.FileName);

                MessageBox.Show($"Reporte exportado como PDF:\n{dlg.FileName}", "Exportado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) { MessageBox.Show($"Error generando PDF: {ex.Message}"); }
        }

        private void ComposeHeader(QuestPDF.Infrastructure.IContainer container)
        {
            var title = reportsGrid.Columns.Count > 0 && reportsGrid.Columns[0].HeaderText.StartsWith("Usuario") 
                ? (reportsGrid.Columns.Count == 9 ? "Historial de Reservas" : "Actividad de Usuarios") 
                : "Uso de Equipos";

            container.Row(row => {
                row.RelativeItem().Column(column => {
                    column.Item().Text($"Sistema de Control - ITESIL").FontSize(18).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                    column.Item().Text($"Reporte: {title}").FontSize(14);
                    column.Item().Text($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });
            });
        }

        private void ComposeContent(QuestPDF.Infrastructure.IContainer container)
        {
            container.PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre).Table(table => {
                table.ColumnsDefinition(columns => {
                    for (int i = 0; i < reportsGrid.Columns.Count; i++) {
                        columns.RelativeColumn();
                    }
                });

                table.Header(header => {
                    foreach (DataGridViewColumn col in reportsGrid.Columns) {
                        header.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black).Padding(3).Text(col.HeaderText).SemiBold();
                    }
                });

                foreach (DataGridViewRow row in reportsGrid.Rows) {
                    foreach (DataGridViewCell cell in row.Cells) {
                        table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(3).Text(cell.Value?.ToString() ?? "");
                    }
                }
            });
        }

        private Panel CreatePasswordRequestsView()
        {
            Panel pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 245, 249), Padding = new Padding(30), Visible = false };
            Label title = new Label { Text = "Usuarios que piden restablecer su contraseña:", ForeColor = Color.FromArgb(30, 41, 59), Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Location = new Point(30,20) };
            
            Button btnReload = new Button { Text = "⟳ Recargar Solicitudes", Location = new Point(30, 55), Size = new Size(200, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(29, 78, 216), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btnReload.FlatAppearance.BorderSize = 0;
            
            reqFlow = new FlowLayoutPanel { Location = new Point(30, 100), Size = new Size(800, 600), AutoScroll = true, BackColor = Color.Transparent };
            
            btnReload.Click += async (s, e) => await RefreshPasswordRequests();

            pnl.Controls.Add(title); pnl.Controls.Add(btnReload); pnl.Controls.Add(reqFlow);
            return pnl;
        }

        private async Task RefreshPasswordRequests()
        {
            if (reqFlow == null) return;
            reqFlow.Controls.Clear();
            try {
                var res = await httpClient.GetAsync($"{Program.BASE_URL}/api/admin/reset-requests");
                if (!res.IsSuccessStatusCode) return;

                var content = await res.Content.ReadAsStringAsync();
                var reqs = JsonConvert.DeserializeObject<List<dynamic>>(content);
                if (reqs == null || reqs.Count == 0) {
                    reqFlow.Controls.Add(new Label { Text = "No hay solicitudes pendientes.", ForeColor = Color.Gray, AutoSize = true, Font = new Font("Segoe UI", 11) });
                    return;
                }

                foreach (var r in reqs) {
                    string capturedId = r.id.ToString();
                    Panel card = new Panel { Size = new Size(600, 90), BackColor = Color.White, Margin = new Padding(0,0,0,15) };
                    
                    // Acento lateral azul
                    Panel accent = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = Color.FromArgb(37, 99, 235) };
                    card.Controls.Add(accent);

                    Label lblUser = new Label { Text = $"Usuario: {r.username}", ForeColor = Color.FromArgb(30, 41, 59), Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(20, 15), AutoSize = true };
                    Label lblTime = new Label { Text = $"Fecha Solicitud: {r.created_at}", ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 9), Location = new Point(20, 45), AutoSize = true };
 
                    Button btnOk = new Button { Text = "Aprobar", Location = new Point(380, 25), Size = new Size(100, 40), BackColor = Color.FromArgb(5, 150, 105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand }; 
                    btnOk.FlatAppearance.BorderSize = 0;
                    
                    Button btnNo = new Button { Text = "Rechazar", Location = new Point(490, 25), Size = new Size(100, 40), BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand }; 
                    btnNo.FlatAppearance.BorderSize = 0;

                    btnOk.Click += async (s, e) => { await ResolveRequest(capturedId, true); await RefreshPasswordRequests(); };
                    btnNo.Click += async (s, e) => { await ResolveRequest(capturedId, false); await RefreshPasswordRequests(); };

                    card.Controls.Add(lblUser); card.Controls.Add(lblTime); card.Controls.Add(btnOk); card.Controls.Add(btnNo);
                    reqFlow.Controls.Add(card);
                }
            } catch { }
        }

        private async Task ResolveRequest(string reqId, bool approve)
        {
            try {
                var payload = new { requestId = reqId, approve = approve };
                var cnt = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                await httpClient.PostAsync($"{Program.BASE_URL}/api/admin/resolve-reset", cnt);
            } catch { }
        }

        // =====================================
        // USERS VIEW
        // =====================================
        private Panel CreateUsersView()
        {
            usersView = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), Visible = false };

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel pnlButtons = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 20, 10) };
            Button btnNew = new Button { Text = "+ Nuevo Usuario", Size = new Size(180, 45), Location = new Point(20, 20), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(79, 70, 229), ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand };
            btnNew.FlatAppearance.BorderSize = 0;
            btnNew.Click += async (s, e) => {
                using var dlg = new UserDialog(Program.BASE_URL, httpClient);
                if (dlg.ShowDialog() == DialogResult.OK) { await RefreshUsers(); }
            };

            Button btnRefresh = new Button { Text = "⟳ Recargar", Size = new Size(130, 45), Location = new Point(220, 20), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(51, 65, 85), ForeColor = Color.White, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (s, e) => await RefreshUsers();

            pnlButtons.Controls.Add(btnNew); pnlButtons.Controls.Add(btnRefresh);

            usersGrid = new DataGridView {
                Dock = DockStyle.Fill, Margin = new Padding(20, 0, 20, 20)
            };
            ApplyGridTheme(usersGrid);
            
            usersGrid.Columns.Add("Id", "ID"); usersGrid.Columns["Id"].Visible = false;
            usersGrid.Columns.Add("Usuario", "Usuario");
            usersGrid.Columns.Add("Nombre", "Nombre Completo");
            usersGrid.Columns.Add("Email", "Correo");
            usersGrid.Columns.Add("Rol", "Rol");
            usersGrid.Columns.Add("Estado", "Estado");

            DataGridViewButtonColumn btnEdit = new DataGridViewButtonColumn { Name = "Editar", HeaderText = "Acción", Text = "Editar", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat };
            btnEdit.DefaultCellStyle.BackColor = Color.FromArgb(37, 99, 235); // Blue
            btnEdit.DefaultCellStyle.ForeColor = Color.White;
            btnEdit.DefaultCellStyle.SelectionBackColor = Color.FromArgb(29, 78, 216);
            btnEdit.DefaultCellStyle.SelectionForeColor = Color.White;
            usersGrid.Columns.Add(btnEdit);

            usersGrid.CellContentClick += async (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex == usersGrid.Columns["Editar"].Index) {
                    var row = usersGrid.Rows[e.RowIndex];
                    var uData = new {
                        Id = row.Cells["Id"].Value?.ToString(),
                        Username = row.Cells["Usuario"].Value?.ToString(),
                        FullName = row.Cells["Nombre"].Value?.ToString(),
                        Email = row.Cells["Email"].Value?.ToString(),
                        Role = row.Cells["Rol"].Value?.ToString(),
                        IsActive = row.Cells["Estado"].Value?.ToString() == "Activo"
                    };
                    using var dlg = new UserDialog(Program.BASE_URL, httpClient, uData);
                    if (dlg.ShowDialog() == DialogResult.OK) { await RefreshUsers(); }
                }
            };

            layout.Controls.Add(pnlButtons, 0, 0); layout.Controls.Add(usersGrid, 0, 1);
            usersView.Controls.Add(layout);
            return usersView;
        }

        private async Task RefreshUsers() 
        { 
            try {
                var res = await httpClient.GetAsync($"{Program.BASE_URL}/api/admin/users");
                if (!res.IsSuccessStatusCode) return;
                var content = await res.Content.ReadAsStringAsync();
                var list = JsonConvert.DeserializeObject<List<dynamic>>(content);

                usersGrid.Invoke(new Action(() => {
                    usersGrid.Rows.Clear();
                    if (list == null) return;
                    foreach (var u in list) {
                        int idx = usersGrid.Rows.Add();
                        var row = usersGrid.Rows[idx];
                        row.Cells["Id"].Value = u.id;
                        row.Cells["Usuario"].Value = u.username;
                        row.Cells["Nombre"].Value = u.full_name;
                        row.Cells["Email"].Value = u.email;
                        row.Cells["Rol"].Value = u.role ?? "Sin Rol";
                        bool act = (bool?)u.is_active ?? false;
                        row.Cells["Estado"].Value = act ? "Activo" : "Inactivo";
                        if (!act) { row.DefaultCellStyle.ForeColor = Color.Gray; }
                    }
                }));
            } catch { }
        }

        private Panel CreateLoansView()
        {
            loansView = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), Visible = false };

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ROW 0: TOOLBAR
            Panel pnlButtons = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 20, 10) };
            Button btnNew = new Button { 
                Text = "+ Nueva Reserva", Size = new Size(200, 45), Location = new Point(20, 20),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(79, 70, 229), ForeColor = Color.White, 
                Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand 
            };
            btnNew.FlatAppearance.BorderSize = 0;
            btnNew.Click += async (s, e) => {
                using var dlg = new NewLoanDialog(Program.BASE_URL, httpClient);
                if (dlg.ShowDialog() == DialogResult.OK) { await RefreshLoans(); }
            };

            Button btnRefresh = new Button { 
                Text = "⟳ Recargar", Size = new Size(130, 45), Location = new Point(240, 20),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(51, 65, 85), ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (s, e) => await RefreshLoans();

            pnlButtons.Controls.Add(btnNew);
            pnlButtons.Controls.Add(btnRefresh);

            // ROW 1: GRID
            loansGrid = new DataGridView {
                Dock = DockStyle.Fill, Margin = new Padding(20, 0, 20, 20)
            };
            ApplyGridTheme(loansGrid);
            
            loansGrid.Columns.Add("Id", "ID"); loansGrid.Columns["Id"].Visible = false;
            loansGrid.Columns.Add("Usuario", "Usuario/Resp");
            loansGrid.Columns.Add("Equipo", "Recurso");
            loansGrid.Columns.Add("Proposito", "Propósito");
            loansGrid.Columns.Add("Inicio", "Inicio");
            loansGrid.Columns.Add("Fin", "Fin Previsto");
            loansGrid.Columns.Add("Estado", "Estado");

            DataGridViewButtonColumn btnCol = new DataGridViewButtonColumn {
                Name = "Accion", HeaderText = "Acción", Text = "Devolver", UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.BackColor = Color.FromArgb(5, 150, 105); // Green-600
            btnCol.DefaultCellStyle.ForeColor = Color.White;
            btnCol.DefaultCellStyle.SelectionBackColor = Color.FromArgb(4, 120, 87);
            btnCol.DefaultCellStyle.SelectionForeColor = Color.White;
            loansGrid.Columns.Add(btnCol);

            loansGrid.CellContentClick += async (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex == loansGrid.Columns["Accion"].Index) {
                    string st = loansGrid.Rows[e.RowIndex].Cells["Estado"].Value?.ToString() ?? "";
                    if (st.ToLower() == "active") {
                        if (MessageBox.Show("¿Confirmar devolución de recurso?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                            string id = loansGrid.Rows[e.RowIndex].Cells["Id"].Value?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(id)) {
                                await ReturnLoan(id);
                                await RefreshLoans();
                            } else {
                                MessageBox.Show("ID de reserva inválido.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    } else {
                        MessageBox.Show("El recurso ya fue devuelto.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            layout.Controls.Add(pnlButtons, 0, 0);
            layout.Controls.Add(loansGrid, 0, 1);
            loansView.Controls.Add(layout);
            return loansView;
        }

        private async Task RefreshLoans() 
        { 
            try {
                var res = await httpClient.GetAsync($"{Program.BASE_URL}/api/admin/loans");
                if (!res.IsSuccessStatusCode) return;

                var content = await res.Content.ReadAsStringAsync();
                var loans = JsonConvert.DeserializeObject<List<dynamic>>(content);

                loansGrid.Invoke(new Action(() => {
                    loansGrid.Rows.Clear();
                    if (loans == null) return;
                    foreach (var l in loans) {
                        int idx = loansGrid.Rows.Add();
                        var row = loansGrid.Rows[idx];
                        row.Cells["Id"].Value = l.id;
                        row.Cells["Usuario"].Value = l.username;
                        row.Cells["Equipo"].Value = l.hostname;
                        row.Cells["Proposito"].Value = l.purpose;
                        row.Cells["Inicio"].Value = Convert.ToDateTime(l.start_at).ToString("dd/MM/yyyy HH:mm");
                        row.Cells["Fin"].Value = Convert.ToDateTime(l.end_at).ToString("dd/MM/yyyy HH:mm");
                        row.Cells["Estado"].Value = l.status;
                        
                        if (l.status.ToString().ToLower() == "completed" || l.status.ToString().ToLower() == "returned") {
                            row.DefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
                            row.DefaultCellStyle.ForeColor = Color.Gray;
                        }
                    }
                }));
            } catch { }
        }

        private async Task ReturnLoan(string loanId)
        {
            try {
                var payload = new { reservationId = loanId };
                var cnt = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var res = await httpClient.PostAsync($"{Program.BASE_URL}/api/admin/loans/return", cnt);
                if (!res.IsSuccessStatusCode) {
                    MessageBox.Show($"Error al devolver. Status: {res.StatusCode}", "Error del Servidor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            } catch (Exception ex) { 
                MessageBox.Show($"Error de conexión: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Panel CreateDashboardView()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30), BackColor = Color.Transparent };
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            p.Controls.Add(flow);

            flow.Controls.Add(CreateStatCard("Equipos Activos", "0", Color.FromArgb(37, 99, 235))); // Blue
            flow.Controls.Add(CreateStatCard("Reservas Hoy", "0", Color.FromArgb(5, 150, 105)));   // Green
            flow.Controls.Add(CreateStatCard("Usuarios Online", "0", Color.FromArgb(217, 119, 6))); // Amber
            flow.Controls.Add(CreateStatCard("Alertas", "0", Color.FromArgb(220, 38, 38)));       // Red

            return p;
        }

        private Panel CreateStatCard(string title, string val, Color color)
        {
            Panel card = new Panel { Size = new Size(240, 120), BackColor = Color.White, Margin = new Padding(0, 0, 20, 20) };
            
            // Borde coloreado a la izquierda para dar estilo
            Panel accent = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = color };
            card.Controls.Add(accent);

            Label lblT = new Label { Text = title, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            Label lblV = new Label { Text = val, ForeColor = Color.FromArgb(15, 23, 42), Font = new Font("Segoe UI", 24, FontStyle.Bold), Location = new Point(20, 45), AutoSize = true, Name = $"lblStat_{title.Replace(" ", "")}" };
            
            card.Controls.Add(lblT);
            card.Controls.Add(lblV);
            return card;
        }

        // =====================================
        // PC CONTROL LOGIC
        // =====================================
        private async Task RefreshComputers()
        {
            try
            {
                var response = await httpClient.GetAsync($"{Program.BASE_URL}/api/admin/computers");
                if (!response.IsSuccessStatusCode) return;

                var content = await response.Content.ReadAsStringAsync();
                var computers = JsonConvert.DeserializeObject<List<ComputerDto>>(content);
                if (computers == null) return;

                int onlineCount = 0;
                computersView.Invoke(new Action(() => {
                    foreach (var pc in computers)
                    {
                        if (pc.Status == "online") onlineCount++;
                        var existingCard = FindCard(pc.Hostname);
                        if (existingCard != null) UpdateCardStatus(existingCard, pc.Status == "online");
                        else computersView.Controls.Add(CreatePcCard(pc));
                    }
                    UpdateDashboardStats(onlineCount, computers.Count);
                }));
            }
            catch { }
        }

        private void UpdateDashboardStats(int online, int total)
        {
            foreach(Control c in dashboardView.Controls)
            {
                if (c is Panel p && p.Controls.ContainsKey("valLabel"))
                {
                    if (p.Controls[1].Text == "Equipos Conectados") p.Controls["valLabel"].Text = $"{online} / {total}";
                }
            }
        }

        private Panel? FindCard(string hostname)
        {
            foreach (Control ctrl in computersView.Controls)
                if (ctrl is Panel p && p.Tag?.ToString() == hostname) return p;
            return null;
        }

        private void UpdateCardStatus(Panel card, bool online)
        {
            foreach (Control ctrl in card.Controls)
            {
                if (ctrl.Name == "indicator") { ctrl.BackColor = online ? Color.LimeGreen : Color.FromArgb(239, 68, 68); break; }
            }
        }

        private Panel CreatePcCard(ComputerDto pc)
        {
            Panel card = new Panel { Size = new Size(220, 215), BackColor = Color.FromArgb(30, 41, 59), Margin = new Padding(15), Tag = pc.Hostname };
            Panel statusIndicator = new Panel { Name = "indicator", Size = new Size(12, 12), Location = new Point(190, 15), BackColor = pc.Status == "online" ? Color.LimeGreen : Color.FromArgb(239, 68, 68) };
            
            // Round indicator
            GraphicsPath path = new GraphicsPath(); path.AddEllipse(0,0,12,12); statusIndicator.Region = new Region(path);

            card.Controls.Add(statusIndicator);

            Label lblName = new Label { Text = pc.Hostname, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(0, 30), Size = new Size(220, 30), TextAlign = ContentAlignment.MiddleCenter };
            card.Controls.Add(lblName);

            if (Program.Role == "Administrador" || Program.Role == "admin" || Program.Role == "Docente" || Program.Role == "technician")
            {
                card.Controls.Add(CreateCmdButton(pc.Hostname, "block", "Bloquear", Color.FromArgb(245, 158, 11), new Point(15, 75)));
                card.Controls.Add(CreateCmdButton(pc.Hostname, "unblock", "Desbloquear", Color.FromArgb(16, 185, 129), new Point(115, 75)));
                if (Program.Role == "Administrador" || Program.Role == "admin")
                {
                    card.Controls.Add(CreateCmdButton(pc.Hostname, "restart", "Reiniciar", Color.FromArgb(59, 130, 246), new Point(15, 120)));
                    card.Controls.Add(CreateCmdButton(pc.Hostname, "shutdown", "Apagar", Color.FromArgb(239, 68, 68), new Point(115, 120)));

                    // Botón para eliminar el equipo de la base de datos
                    Button btnDel = new Button { Text = "🗑 Eliminar Equipo", Size = new Size(190, 35), Location = new Point(15, 165), FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.FromArgb(220, 38, 38), Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand };
                    btnDel.FlatAppearance.BorderColor = Color.FromArgb(220, 38, 38);
                    btnDel.FlatAppearance.BorderSize = 1;
                    btnDel.MouseEnter += (s, e) => { btnDel.BackColor = Color.FromArgb(220, 38, 38); btnDel.ForeColor = Color.White; };
                    btnDel.MouseLeave += (s, e) => { btnDel.BackColor = Color.Transparent; btnDel.ForeColor = Color.FromArgb(220, 38, 38); };
                    btnDel.Click += async (s, e) => await DeleteComputer(pc.Hostname);
                    card.Controls.Add(btnDel);
                }
            }
            return card;
        }

        private Button CreateCmdButton(string hostname, string cmd, string text, Color color, Point loc)
        {
            Button btn = new Button { Text = text, Size = new Size(90, 35), Location = loc, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = color, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = color;
            btn.FlatAppearance.BorderSize = 1;
            btn.MouseEnter += (s, e) => { btn.BackColor = color; btn.ForeColor = Color.White; };
            btn.MouseLeave += (s, e) => { btn.BackColor = Color.Transparent; btn.ForeColor = color; };
            btn.Click += async (s, e) => await SendCommand(hostname, cmd);
            return btn;
        }

        private async Task SendCommand(string hostname, string command)
        {
            try
            {
                var payload = new { hostname = hostname, command = command };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"{Program.BASE_URL}/api/admin/command", content);
                if (!response.IsSuccessStatusCode) MessageBox.Show("No autorizado o error", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private async Task DeleteComputer(string hostname)
        {
            if (MessageBox.Show($"¿Estás seguro de que deseas eliminar el equipo '{hostname}' de los registros?\nSe borrará todo su historial.", "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    var response = await httpClient.DeleteAsync($"{Program.BASE_URL}/api/admin/computers/{hostname}");
                    if (response.IsSuccessStatusCode)
                    {
                        var card = FindCard(hostname);
                        if (card != null) computersView.Controls.Remove(card);
                        await RefreshComputers();
                    }
                    else
                    {
                        MessageBox.Show("No se pudo eliminar el equipo. Verifica tus permisos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error de red: " + ex.Message); }
            }
        }
    }


    public class ComputerDto
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("hostname")] public string Hostname { get; set; } = "";
        [JsonProperty("status")] public string Status { get; set; } = "";
    }

    public class UserDialog : Form
    {
        private TextBox txtUser, txtName, txtEmail, txtPass;
        private ComboBox cmbRole;
        private CheckBox chkActive;
        private Label lblError;
        private readonly string baseUrl;
        private readonly HttpClient http;
        private readonly dynamic? existingUser;

        public UserDialog(string baseUrl, HttpClient http, dynamic? user = null)
        {
            this.baseUrl = baseUrl; this.http = http; this.existingUser = user;
            this.Text = user == null ? "Nuevo Usuario" : "Editar Usuario";
            this.Size = new Size(450, 560);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.MaximizeBox = false; this.MinimizeBox = false;

            int y = 20;
            this.Controls.Add(MakeLabel("Nombre de Usuario (Login):", y)); y += 22;
            txtUser = MakeInput(y); txtUser.Text = user?.Username ?? ""; this.Controls.Add(txtUser);
            if (user != null) { txtUser.Enabled = false; txtUser.BackColor = Color.FromArgb(241, 245, 249); }
            y += 38;

            this.Controls.Add(MakeLabel("Nombre Completo:", y)); y += 22;
            txtName = MakeInput(y); txtName.Text = user?.FullName ?? ""; this.Controls.Add(txtName); y += 38;

            this.Controls.Add(MakeLabel("Correo Electrónico:", y)); y += 22;
            txtEmail = MakeInput(y); txtEmail.Text = user?.Email ?? ""; this.Controls.Add(txtEmail); y += 38;

            this.Controls.Add(MakeLabel(user == null ? "Contraseña:" : "Nueva Contraseña (vacío para no cambiar):", y)); y += 22;
            txtPass = MakeInput(y); txtPass.UseSystemPasswordChar = true; this.Controls.Add(txtPass); y += 38;

            this.Controls.Add(MakeLabel("Rol:", y)); y += 22;
            cmbRole = new ComboBox { Location = new Point(20, y), Size = new Size(400, 30), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.FromArgb(15, 23, 42), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11) };
            this.Controls.Add(cmbRole); y += 45;

            chkActive = new CheckBox { Text = "Usuario Activo", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 11), Checked = user?.IsActive ?? true };
            this.Controls.Add(chkActive); y += 35;

            lblError = new Label { Location = new Point(20, y), Size = new Size(400, 20), ForeColor = Color.FromArgb(220, 38, 38), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            this.Controls.Add(lblError); y += 25;

            Button btnSave = new Button { Text = "Guardar Cambios", Location = new Point(20, y), Size = new Size(190, 45), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0; btnSave.Click += BtnSave_Click;
            
            Button btnCancel = new Button { Text = "Cancelar", Location = new Point(220, y), Size = new Size(190, 45), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(148, 163, 184), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnCancel.FlatAppearance.BorderSize = 0; btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            
            this.Controls.Add(btnSave); this.Controls.Add(btnCancel);
            _ = LoadRoles();
        }

        private async Task LoadRoles()
        {
            try {
                var res = await http.GetAsync($"{baseUrl}/api/admin/roles");
                if (res.IsSuccessStatusCode) {
                    var json = await res.Content.ReadAsStringAsync();
                    var roles = JsonConvert.DeserializeObject<List<dynamic>>(json);
                    if (roles != null) {
                        foreach (var r in roles) cmbRole.Items.Add(r.name.ToString());
                        if (existingUser != null && !string.IsNullOrEmpty(existingUser.Role)) {
                            cmbRole.SelectedItem = existingUser.Role;
                        } else if (cmbRole.Items.Count > 0) cmbRole.SelectedIndex = 0;
                    }
                }
            } catch { }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUser.Text)) { lblError.Text = "El nombre de usuario es obligatorio."; return; }
            if (string.IsNullOrWhiteSpace(txtName.Text)) { lblError.Text = "El nombre completo es obligatorio."; return; }
            if (string.IsNullOrWhiteSpace(txtEmail.Text)) { lblError.Text = "El correo es obligatorio."; return; }
            if (!txtEmail.Text.Contains("@") || !txtEmail.Text.Contains(".")) { lblError.Text = "El formato del correo no es válido."; return; }
            if (existingUser == null && string.IsNullOrWhiteSpace(txtPass.Text)) { lblError.Text = "La contraseña es obligatoria para nuevos usuarios."; return; }
            if (cmbRole.SelectedIndex == -1) { lblError.Text = "Debes seleccionar un rol."; return; }

            try {
                HttpResponseMessage res;
                if (existingUser == null) {
                    var payload = new { username = txtUser.Text.Trim(), email = txtEmail.Text.Trim(), password = txtPass.Text, fullName = txtName.Text.Trim(), role = cmbRole.SelectedItem?.ToString() ?? "", isActive = chkActive.Checked };
                    var cnt = new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
                    res = await http.PostAsync($"{baseUrl}/api/admin/users", cnt);
                } else {
                    var payload = new { email = txtEmail.Text.Trim(), password = txtPass.Text, fullName = txtName.Text.Trim(), role = cmbRole.SelectedItem?.ToString() ?? "", isActive = chkActive.Checked };
                    var cnt = new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
                    res = await http.PutAsync($"{baseUrl}/api/admin/users/{existingUser.Id}", cnt);
                }
                
                if (res.IsSuccessStatusCode) { this.DialogResult = DialogResult.OK; this.Close(); }
                else { lblError.Text = "Error al guardar el usuario."; }
            } catch { lblError.Text = "Error de conexión."; }
        }

        private Label MakeLabel(string text, int y) => new Label { Text = text, ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(20, y), AutoSize = true };
        private TextBox MakeInput(int y) => new TextBox { Location = new Point(20, y), Size = new Size(400, 30), BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.FromArgb(15, 23, 42), Font = new Font("Segoe UI", 11), BorderStyle = BorderStyle.FixedSingle };
    }

    // ============================
    // NEW LOAN DIALOG
    // ============================
    public class NewLoanDialog : Form
    {
        private TextBox txtUser, txtPurpose;
        private ComboBox cmbHostname;
        private DateTimePicker dtpStart, dtpEnd;
        private Label lblError;
        private readonly string baseUrl;
        private readonly HttpClient http;

        public NewLoanDialog(string baseUrl, HttpClient http)
        {
            this.baseUrl = baseUrl; this.http = http;
            this.Text = "Nueva Reserva (Laboratorio o Equipo)";
            this.Size = new Size(450, 480);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(15, 23, 42);
            this.MaximizeBox = false; this.MinimizeBox = false;

            int y = 20;
            this.Controls.Add(MakeLabel("Usuario / Responsable:", y)); y += 22;
            txtUser = MakeInput(y); this.Controls.Add(txtUser); y += 38;

            this.Controls.Add(MakeLabel("¿Qué desea reservar?", y)); y += 22;
            cmbHostname = new ComboBox { 
                Location = new Point(20, y), 
                Size = new Size(400, 30), 
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11)
            };
            cmbHostname.Items.Add("Laboratorio Completo");
            cmbHostname.SelectedIndex = 0;
            this.Controls.Add(cmbHostname); y += 45;

            this.Controls.Add(MakeLabel("Propósito / Evento:", y)); y += 22;
            txtPurpose = MakeInput(y); this.Controls.Add(txtPurpose); y += 38;

            this.Controls.Add(MakeLabel("Fecha y Hora Inicio:", y)); y += 22;
            dtpStart = new DateTimePicker { Location = new Point(20, y), Size = new Size(400, 28), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", Font = new Font("Segoe UI", 11) };
            this.Controls.Add(dtpStart); y += 40;

            this.Controls.Add(MakeLabel("Fecha y Hora Fin:", y)); y += 22;
            dtpEnd = new DateTimePicker { Location = new Point(20, y), Size = new Size(400, 28), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", Font = new Font("Segoe UI", 11), Value = DateTime.Now.AddHours(2) };
            this.Controls.Add(dtpEnd); y += 45;

            lblError = new Label { Location = new Point(20, y), Size = new Size(400, 20), ForeColor = Color.Salmon, Font = new Font("Segoe UI", 9) };
            this.Controls.Add(lblError); y += 25;

            Button btnSave = new Button { Text = "Confirmar Reserva", Location = new Point(20, y), Size = new Size(190, 45), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(79, 70, 229), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            
            Button btnCancel = new Button { Text = "Cancelar", Location = new Point(220, y), Size = new Size(190, 45), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnSave); this.Controls.Add(btnCancel);

            _ = LoadComputers();
        }

        private async Task LoadComputers()
        {
            try {
                var res = await http.GetAsync($"{baseUrl}/api/admin/computers");
                if (res.IsSuccessStatusCode) {
                    var json = await res.Content.ReadAsStringAsync();
                    var pcs = JsonConvert.DeserializeObject<List<ComputerDto>>(json);
                    if (pcs != null) {
                        foreach (var pc in pcs) cmbHostname.Items.Add(pc.Hostname);
                    }
                }
            } catch { }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUser.Text)) {
                lblError.Text = "El campo Usuario/Responsable es obligatorio."; return;
            }
            try {
                string selectedHost = cmbHostname.SelectedItem?.ToString() ?? "Laboratorio Completo";
                
                var payload = new { 
                    username = txtUser.Text.Trim(), 
                    hostname = selectedHost, 
                    purpose = txtPurpose.Text.Trim(), 
                    startAt = dtpStart.Value, 
                    endAt = dtpEnd.Value 
                };
                var cnt = new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
                var res = await http.PostAsync($"{baseUrl}/api/admin/loans", cnt);
                
                if (res.IsSuccessStatusCode) { 
                    this.DialogResult = DialogResult.OK; 
                    this.Close(); 
                }
                else { 
                    var err = await res.Content.ReadAsStringAsync(); 
                    if (err.Contains("not found") || err.Contains("encontr")) {
                        lblError.Text = "Error: El usuario o equipo no existen.";
                    } else {
                        lblError.Text = "Error al guardar la reserva.";
                    }
                }
            } catch { lblError.Text = "Error de conexión con el servidor."; }
        }

        private Label MakeLabel(string text, int y) =>
            new Label { Text = text, Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 9, FontStyle.Bold) };

        private TextBox MakeInput(int y) =>
            new TextBox { Location = new Point(20, y), Size = new Size(400, 28), Font = new Font("Segoe UI", 11), BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    }
}
