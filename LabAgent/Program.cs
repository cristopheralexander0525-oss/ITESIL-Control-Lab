using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.AspNetCore.SignalR.Client;

namespace LabAgent
{
    class Program
    {
        private static readonly string SERVER_URL =
            ConfigurationManager.AppSettings["ServidorUrl"] ?? "http://10.0.252.225:5000";
        private static readonly string API_KEY = "LabAgent_2026_Secure_Key_9f4e7d2a1b8c";
        private static readonly string HOSTNAME = Environment.MachineName;
        private static readonly string LOG_PATH = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt");

        private static readonly HttpClient httpClient = new HttpClient();
        private static HubConnection? hubConnection;
        private static NotifyIcon? trayIcon;

        // Bloqueo de pantalla — igual que ITESILControlLab usa AllowClose
        private static BlockForm? blockForm = null;
        private static Thread? blockThread = null;
        private static KeyboardHook? keyboardHook = null;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Log($"Agente iniciado. Servidor: {SERVER_URL}  |  Hostname: {HOSTNAME}");

            httpClient.DefaultRequestHeaders.Add("X-API-KEY", API_KEY);

            // Bandeja del sistema
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "ITESIL Lab - Agente",
                Visible = true
            };
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add("Estado: Iniciando...", null, null).Enabled = false;

            // Registrar auto-inicio (con ruta entre comillas, igual que ITESILControlLab)
            AutoInicio.Configurar(true);

            // Iniciar SignalR en hilo de fondo
            Task.Run(async () =>
            {
                await ConnectSignalR();
                while (true)
                {
                    try { await CheckIn(); } catch (Exception ex) { Log($"CheckIn error: {ex.Message}"); }
                    await Task.Delay(10000);
                }
            });

            Application.Run();
        }

        // =============================================
        // SIGNALR
        // =============================================
        private static async Task ConnectSignalR()
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl($"{SERVER_URL}/labHub")
                .WithAutomaticReconnect(new[] {
                    TimeSpan.Zero, TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            // Escuchar comandos
            hubConnection.On<JsonElement>("ReceiveCommand", (data) =>
            {
                string id = data.GetProperty("id").GetString() ?? "";
                string cmd = data.GetProperty("command").GetString() ?? "";
                Log($"Comando recibido: {cmd}");
                bool ok = ExecuteCommand(cmd);
                _ = ReportResult(id, ok);
            });

            hubConnection.Reconnecting += error =>
            {
                Log($"SignalR reconectando: {error?.Message}");
                SetTrayText("⚠ Reconectando...");
                return Task.CompletedTask;
            };
            hubConnection.Reconnected += id =>
            {
                Log($"SignalR reconectado");
                SetTrayText($"✅ Conectado");
                _ = hubConnection.InvokeAsync("Register", HOSTNAME);
                return Task.CompletedTask;
            };
            hubConnection.Closed += error =>
            {
                Log($"SignalR cerrado: {error?.Message}");
                SetTrayText("❌ Desconectado");
                _ = Task.Run(async () => { await Task.Delay(10000); await ConnectSignalR(); });
                return Task.CompletedTask;
            };

            while (true)
            {
                try
                {
                    await hubConnection.StartAsync();
                    await hubConnection.InvokeAsync("Register", HOSTNAME);
                    Log($"SignalR conectado a {SERVER_URL}");
                    SetTrayText($"✅ Conectado a {SERVER_URL}");
                    return;
                }
                catch (Exception ex)
                {
                    Log($"Error al conectar: {ex.Message}. Reintentando en 5s...");
                    SetTrayText("❌ Sin conexión...");
                    await Task.Delay(5000);
                }
            }
        }

        // =============================================
        // EJECUTAR COMANDOS
        // =============================================
        private static bool ExecuteCommand(string command)
        {
            try
            {
                switch (command)
                {
                    case "shutdown":
                        Process.Start("shutdown", "/s /t 0");
                        break;
                    case "restart":
                        Process.Start("shutdown", "/r /t 0");
                        break;
                    case "block":
                        // Ejecutar en hilo STA para la UI
                        if (blockForm == null)
                        {
                            blockThread = new Thread(() =>
                            {
                                // Desactivar Win+L (bloquear sesión)
                                SetLockWorkstationPolicy(true);

                                // Activar hook de teclado (bloquea Win, Alt+F4, etc.)
                                keyboardHook = new KeyboardHook();
                                keyboardHook.Hook();

                                blockForm = new BlockForm();
                                Application.Run(blockForm);

                                // Cuando el form cierra, limpiar
                                keyboardHook.Unhook();
                                keyboardHook = null;
                                blockForm = null;
                                SetLockWorkstationPolicy(false);
                            });
                            blockThread.SetApartmentState(ApartmentState.STA);
                            blockThread.IsBackground = true;
                            blockThread.Start();
                        }
                        break;
                    case "unblock":
                        if (blockForm != null && !blockForm.IsDisposed)
                        {
                            blockForm.Invoke(new Action(() =>
                            {
                                blockForm.AllowClose = true;  // Bandera para permitir cierre
                                blockForm.Close();
                            }));
                            SetLockWorkstationPolicy(false);
                        }
                        break;
                    default:
                        if (command.StartsWith("open "))
                            Process.Start(new ProcessStartInfo(command.Replace("open ", "")) { UseShellExecute = true });
                        break;
                }
                return true;
            }
            catch (Exception ex) { Log($"Error ejecutando '{command}': {ex.Message}"); return false; }
        }

        // =============================================
        // DESACTIVAR WIN+L (Bloqueo de Sesión)
        // =============================================
        private static void SetLockWorkstationPolicy(bool disable)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true);
                if (key != null)
                {
                    if (disable)
                        key.SetValue("DisableLockWorkstation", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    else
                        key.DeleteValue("DisableLockWorkstation", false);
                }
            }
            catch (Exception ex) { Log($"Policy Error: {ex.Message}"); }
        }

        // =============================================
        // CHECK-IN HTTP
        // =============================================
        private static async Task CheckIn()
        {
            var payload = new { hostname = HOSTNAME };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await httpClient.PostAsync($"{SERVER_URL}/api/agent/checkin", content);
            Log($"CheckIn: {resp.StatusCode}");
        }

        // =============================================
        // REPORTAR RESULTADO
        // =============================================
        private static async Task ReportResult(string commandId, bool success)
        {
            try
            {
                var payload = new { command_id = commandId, status = success ? "executed" : "failed" };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await httpClient.PostAsync($"{SERVER_URL}/api/agent/result", content);
            }
            catch (Exception ex) { Log($"Error reportando resultado: {ex.Message}"); }
        }

        // =============================================
        // HELPERS
        // =============================================
        private static void Log(string msg)
        {
            try { File.AppendAllText(LOG_PATH, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}"); }
            catch { }
        }

        private static void SetTrayText(string text)
        {
            if (trayIcon == null) return;
            try
            {
                var safe = text.Length > 63 ? text[..63] : text;
                trayIcon.Text = safe;
                if (trayIcon.ContextMenuStrip?.Items.Count > 0)
                    trayIcon.ContextMenuStrip.Items[0].Text = $"Estado: {safe}";
            }
            catch { }
        }
    }

    // =============================================
    // PANTALLA DE BLOQUEO — con bandera AllowClose
    // (igual que FormBloqueo en ITESILControlLab)
    // =============================================
    public class BlockForm : Form
    {
        // Bandera para permitir el cierre programático — igual que ITESILControlLab
        private bool _allowClose = false;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool AllowClose
        {
            get => _allowClose;
            set => _allowClose = value;
        }

        public BlockForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(15, 23, 42); // Navy Dark
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Cursor = Cursors.No;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen?.Bounds ?? this.Bounds;

            // Panel para degradado o efectos visuales
            var pnlCenter = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            this.Controls.Add(pnlCenter);

            var label = new Label
            {
                Text = "🔒 SISTEMA BLOQUEADO\n\nEste equipo ha sido restringido por el Administrador del Laboratorio.\nPor favor, contacte con el personal encargado.",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(800, 300),
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlCenter.Controls.Add(label);

            this.Load += (s, e) =>
            {
                label.Location = new Point((this.Width - label.Width) / 2, (this.Height - label.Height) / 2);
            };

            // Animación suave de opacidad (opcional)
            this.Opacity = 0;
            var timer = new System.Windows.Forms.Timer { Interval = 10 };
            timer.Tick += (s, e) => { if (this.Opacity < 0.95) this.Opacity += 0.05; else timer.Stop(); };
            timer.Start();
        }

        // Usar bandera AllowClose como ITESILControlLab usa form.AllowClose
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        // Bloquear Alt+F4 a nivel de mensajes del formulario
        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;
            if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_CLOSE)
                return;
            base.WndProc(ref m);
        }
    }

    // =============================================
    // KEYBOARD HOOK — copiado de ITESILControlLab
    // Bloquea TODAS las teclas incluyendo Win key
    // =============================================
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public KeyboardHook() { _proc = HookCallback; }

        public void Hook()
        {
            if (_hookID == IntPtr.Zero)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule!)
                {
                    _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        public void Unhook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        // Bloqueo TOTAL del teclado — Intercepta Win, Alt+Tab, Ctrl+Esc, etc.
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                // LWin (91), RWin (92), Alt (164/165), Tab (9), Esc (27), Ctrl (162/163), Delete (46)
                // En realidad aquí devolvemos 1 SIEMPRE para bloquear TODO el teclado
                // mientras la pantalla de bloqueo esté activa.
                return (IntPtr)1; 
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose() => Unhook();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }


    // =============================================
    // AUTO-INICIO — igual que ITESILControlLab
    // Ruta entre comillas para manejar espacios
    // =============================================
    static class AutoInicio
    {
        private const string REG_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string REG_VALUE = "ITESIL_LabAgent";

        public static void Configurar(bool habilitar)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REG_KEY, true);
                if (key == null) return;
                if (habilitar)
                    // Comillas alrededor de la ruta — igual que ITESILControlLab
                    key.SetValue(REG_VALUE, $"\"{Application.ExecutablePath}\"");
                else
                    key.DeleteValue(REG_VALUE, false);
            }
            catch { }
        }
    }
}
