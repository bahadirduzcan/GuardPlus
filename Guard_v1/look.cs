using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using MessagingToolkit.QRCode.Codec;
using System.Net;
using System.Text;

namespace svchost
{
    public partial class look : Form
    {
        public look()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            ProcessModule objCurrentModule = Process.GetCurrentProcess().MainModule;
            objKeyboardProcess = new LowLevelKeyboardProc(captureKey);
            ptrHook = SetWindowsHookEx(13, objKeyboardProcess, GetModuleHandle(objCurrentModule.ModuleName), 0);
            AdminRelauncher();
        }

        private void AdminRelauncher()
        {
            if (!IsRunAsAdmin())
            {
                ProcessStartInfo proc = new ProcessStartInfo();
                proc.UseShellExecute = true;
                proc.WorkingDirectory = Environment.CurrentDirectory;
                proc.FileName = Assembly.GetEntryAssembly().CodeBase;

                proc.Verb = "runas";

                try
                {
                    Process.Start(proc);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("This program must be run as an administrator! \n\n" + ex.ToString());
                }
            }
        }

        private bool IsRunAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80;  // Turn on WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public static string processName = "notepad";

        delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        const uint EVENT_SYSTEM_FOREGROUND = 3;
        const uint WINEVENT_OUTOFCONTEXT = 0;

        static WinEventDelegate procDelegate = new WinEventDelegate(WinEventProc);

        static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0 || idChild != 0)
                return;

            if (GetForegroundProcessName() == processName)
                HideTaskbar();
            else ShowTaskbar();
        }

        [StructLayout(LayoutKind.Sequential)]

        private struct KeyboardDLLStruct
        {
            public Keys key;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr extra;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short GetAsyncKeyState(Keys key);

        private IntPtr ptrHook;
        private LowLevelKeyboardProc objKeyboardProcess;

        private IntPtr captureKey(int nCode, IntPtr wp, IntPtr lp)
        {
            if (nCode >= 0)
            {
                KeyboardDLLStruct objKeyInfo = (KeyboardDLLStruct)Marshal.PtrToStructure(lp, typeof(KeyboardDLLStruct));

                if (objKeyInfo.key == Keys.RWin || objKeyInfo.key == Keys.LWin)
                    return (IntPtr)1;
            }
            return CallNextHookEx(ptrHook, nCode, wp, lp);
        }


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        private const int APPCOMMAND_VOLUME_UP = 0xA0000;
        private const int APPCOMMAND_VOLUME_DOWN = 0x90000;
        private const int WM_APPCOMMAND = 0x319;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private void Mute()
        {
            SendMessageW(this.Handle, WM_APPCOMMAND, this.Handle,
                (IntPtr)APPCOMMAND_VOLUME_MUTE);
        }

        private void VolDown()
        {
            SendMessageW(this.Handle, WM_APPCOMMAND, this.Handle,
                (IntPtr)APPCOMMAND_VOLUME_DOWN);
        }

        private void VolUp()
        {
            SendMessageW(this.Handle, WM_APPCOMMAND, this.Handle,
                (IntPtr)APPCOMMAND_VOLUME_UP);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern Int32 GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        static private string GetForegroundProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();

            if (hwnd == null)
                return "Unknown";

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);

            foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())
            {
                if (p.Id == pid)
                    return p.ProcessName;
            }

            return "Unknown";
        }

        static public void ShowTaskbar()
        {
            Taskbar.Show();
            SetTaskbarState(AppBarStates.AlwaysOnTop);
        }

        static public void HideTaskbar()
        {
            SetTaskbarState(AppBarStates.AutoHide);
            Taskbar.Hide();
            System.Threading.Thread.Sleep(100);
            Taskbar.Hide();
        }

        private static WINDOWPLACEMENT GetPlacement(IntPtr hwnd)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hwnd, ref placement);
            return placement;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(
            IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public ShowWindowCommands showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public Rectangle rcNormalPosition;
        }

        internal enum ShowWindowCommands : int
        {
            Hide = 0,
            Normal = 1,
            Minimized = 2,
            Maximized = 3,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);

        [DllImport("shell32.dll")]
        public static extern UInt32 SHAppBarMessage(UInt32 dwMessage, ref APPBARDATA pData);

        public enum AppBarMessages
        {
            New = 0x00,
            Remove = 0x01,
            QueryPos = 0x02,
            SetPos = 0x03,
            GetState = 0x04,
            GetTaskBarPos = 0x05,
            Activate = 0x06,
            GetAutoHideBar = 0x07,
            SetAutoHideBar = 0x08,
            WindowPosChanged = 0x09,
            SetState = 0x0a
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public UInt32 cbSize;
            public IntPtr hWnd;
            public UInt32 uCallbackMessage;
            public UInt32 uEdge;
            public Rectangle rc;
            public Int32 lParam;
        }

        public enum AppBarStates
        {
            AutoHide = 0x01,
            AlwaysOnTop = 0x02
        }

        static public void SetTaskbarState(AppBarStates option)
        {
            APPBARDATA msgData = new APPBARDATA();
            msgData.cbSize = (UInt32)Marshal.SizeOf(msgData);
            msgData.hWnd = FindWindow("System_TrayWnd", null);
            msgData.lParam = (Int32)(option);
            SHAppBarMessage((UInt32)AppBarMessages.SetState, ref msgData);
        }

        public AppBarStates GetTaskbarState()
        {
            APPBARDATA msgData = new APPBARDATA();
            msgData.cbSize = (UInt32)Marshal.SizeOf(msgData);
            msgData.hWnd = FindWindow("System_TrayWnd", null);
            return (AppBarStates)SHAppBarMessage((UInt32)AppBarMessages.GetState, ref msgData);
        }

        bool flashTakili = false, flashCikarili = false;
        string dosyaYolu;
        bool DosyaKontrol = false;
        string kullaniciAdi = null;

        private void Form1_Load(object sender, EventArgs e)
        {
            using (StreamReader reader = new StreamReader(tools.pcKlasor + @"\Svchost\users.dat"))
                kullaniciAdi = reader.ReadLine();

            textBox1.Focus();

            label1.Text = DateTime.Now.ToLongTimeString();

            qr_kod();

            string x;
            x = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 10);
            this.Text = x;

            Mute();
            this.TopMost = true;

            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            rk.SetValue("guard.exe", tools.pcKlasor + @"\Svchost\guard.exe");

            timer1.Enabled = true;
            timer2.Enabled = true;

            kilitli();

            IntPtr hook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                procDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            UnhookWinEvent(hook);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (this.WindowState != FormWindowState.Normal)
            {
                e.Handled = true;
                if (e.KeyCode == Keys.Control)
                    e.Handled = true;
                if (e.KeyCode == Keys.Escape)
                    e.Handled = true;
                if (e.KeyCode == Keys.Alt)
                    e.Handled = true;
                if (e.Alt && e.KeyCode == Keys.F4)
                    e.Handled = true;
                if (e.Control)
                    e.Handled = true;
                if (e.KeyCode == Keys.F4)
                    e.Handled = true;
                if (e.Alt && e.KeyCode == Keys.F4 && e.Control && e.KeyCode == Keys.Escape)
                    e.Handled = true;
                if (e.KeyCode == Keys.Delete)
                    e.Handled = true;
                if (e.KeyCode == Keys.Back)
                    e.Handled = true;
                if (e.KeyCode == Keys.D)
                    e.Handled = true;
                if (e.KeyCode == Keys.LWin)
                    e.Handled = true;
                if (e.KeyCode == Keys.RWin)
                    e.Handled = true;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Process.Start(tools.pcKlasor + @"\Svchost\guard.exe");
        }

        static string GetSourceCode(string url)
        {
            if (tools.internetSorgusu)
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);

                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();

                using (StreamReader sRead = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return sRead.ReadToEnd();
                }
            }
            else
            {
                return null;
            }
        }

        private void kilitli()//Flash Belleği Bulamadığı Zaman Çalışacak Olan Kodlar
        {
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;

            if (tools.internetSorgusu)
                GetSourceCode("https://guard.bahadirduzcan.com.tr/api.php?nick=" + kullaniciAdi + "&pass=8" + kod + DateTime.Now.ToString("HH") + "&ip=" + tools.IpAdresi + "&kod=159753Guard");

            timer4.Enabled = true;
            timer2.Enabled = true;
            
            if (flashCikarili)
            {
                Flash_Kontrol flash = new Flash_Kontrol();
                Flash_Kontrol.FlashKontrol(true);
                flash.Show();
                flashCikarili = false;
                Mute();
                this.TopMost = true;

                SetTaskbarState(AppBarStates.AutoHide);
                Taskbar.Hide();
                System.Threading.Thread.Sleep(100);
                Taskbar.Hide();


                RegistryKey rkey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies", true);
                rkey.CreateSubKey("System", RegistryKeyPermissionCheck.Default);
                rkey.Close();
                RegistryKey rkey2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true);
                rkey2.SetValue("DisableTaskMgr", 1);
                rkey2.Close();
            }

            flashTakili = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            textBox1.Focus();
            dosyaYolu = null;

            foreach (ManagementObject item in new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType LIKE 'USB%'").Get())
            {
                try
                {
                    foreach (ManagementObject partition in new ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + item.Properties["DeviceID"].Value + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition").Get())
                        foreach (ManagementObject disk in new ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + partition["DeviceID"] + "'} WHERE AssocClass = Win32_LogicalDiskToPartition").Get())
                            dosyaYolu = disk["Name"].ToString();
                }
                catch (Exception)
                {

                }
            }

            if (tools.internetSorgusu && uzaktanAc)
            {
                kilitsiz();
                qrkod = false;
                textBox1.Text = "";
            }
            else if (tools.internetSorgusu && !uzaktanAc)
            {
                kilitli();
            }
            else
            {
                if (dosyaYolu != null)
                {
                    DosyaKontrol = File.Exists(dosyaYolu + tools.dosya);
                    if (DosyaKontrol)
                    {
                        string line = null;
                        using (StreamReader reader = new StreamReader(dosyaYolu + tools.dosya))
                            line = reader.ReadLine();
                        if (line == tools.bilgisayarId || qrkod)
                        {
                            kilitsiz();
                            qrkod = false;
                            textBox1.Text = "";
                        }
                        else
                        {
                            if (qrkod)
                                kilitsiz();
                            else
                                kilitli();
                        }
                    }
                    else
                    {
                        if (qrkod)
                            kilitsiz();
                        else
                            kilitli();
                    }
                }
                else
                {
                    if (qrkod)
                        kilitsiz();
                    else
                        kilitli();
                }
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Normal)
            {
                SetForegroundWindow(this.Handle);
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((int)e.KeyChar >= 48 && (int)e.KeyChar <= 57)
                e.Handled = false;
            else if ((int)e.KeyChar == 8)
                e.Handled = false;
            else
                e.Handled = true;
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            Process[] programKapat = Process.GetProcessesByName("taskmgr");
            if (programKapat.Length > 0)
            {
                foreach (Process p in programKapat)
                {
                    p.Kill();
                }
            }
        }

        string harfler = "0123456789";
        string uret;
        string kod;
        Random rastgele = new Random();
        bool qrkod = false;
        bool uzaktanAc = false;

        private void qr_kod()
        {
            uret = "";
            for (int i = 0; i < 6; i++)
            {
                if (i == 0)
                    uret += harfler[rastgele.Next(1, harfler.Length)];
                else
                    uret += harfler[rastgele.Next(harfler.Length)];
            }
            kod = uret.ToString();
            QRCodeEncoder encoder = new QRCodeEncoder();
            Bitmap qrcode = encoder.Encode(kod);
            pictureBox1.Image = qrcode as Image;
        }

        sbyte a = 60;

        private void timer4_Tick(object sender, EventArgs e)
        {
            label1.Text = DateTime.Now.ToLongTimeString();
            a--;
            label3.Text = a.ToString();
            if (a > 10 && a < 30)
                label3.ForeColor = Color.Orange;
            else if (a <= 10)
            {
                if (a % 2 == 0)
                    label3.ForeColor = Color.Black;
                else
                    label3.ForeColor = Color.Red;
            }
            else
                label3.ForeColor = Color.Black;

            if (a == 0)
            {
                a = 60;
                qr_kod();
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text == "8" + kod + DateTime.Now.ToString("HH"))
                qrkod = true;

            switch (textBox1.Text.Length)
            {
                case 1: label2.Text = "* - - - - - - - -"; break;
                case 2: label2.Text = "* * - - - - - - -"; break;
                case 3: label2.Text = "* * * - - - - - -"; break;
                case 4: label2.Text = "* * * * - - - - -"; break;
                case 5: label2.Text = "* * * * * - - - -"; break;
                case 6: label2.Text = "* * * * * * - - -"; break;
                case 7: label2.Text = "* * * * * * * - -"; break;
                case 8: label2.Text = "* * * * * * * * -"; break;
                case 9: label2.Text = "* * * * * * * * *"; break;
                default: label2.Text = "- - - - - - - - -"; break;
            }
        }

        private void look_Click(object sender, EventArgs e)
        {
            textBox1.Focus();
        }

        string document;

        private void timer5_Tick(object sender, EventArgs e)
        {
            if (tools.internetSorgusu)
                document = GetSourceCode("https://guard.bahadirduzcan.com.tr/api.php?nick=" + kullaniciAdi + "&kod=159753Guard");
            if (document == "1")
                uzaktanAc = true;
            else
                uzaktanAc = false;

            tools.internetSorgusu = tools.internetSorgu();
        }

        private void kilitsiz()//Flash Belleği Bulduğu Zaman Çalışacak Olan Kodlar
        {
            a = 60;
            qr_kod();
            timer4.Enabled = true;

            if (tools.internetSorgusu)
                GetSourceCode("https://guard.bahadirduzcan.com.tr/api.php?nick=" + kullaniciAdi + "&pass=0&ip=" + tools.IpAdresi + "&kod=159753Guard");

            this.WindowState = FormWindowState.Normal;
            this.TopMost = false;
            this.Width = 1;
            this.Height = 1;
            this.Location = new Point(99999, 99999);
            timer2.Enabled = false;
            
            if (flashTakili)
            {
                Flash_Kontrol flash = new Flash_Kontrol();
                Flash_Kontrol.FlashKontrol(false);
                flash.Show();
                flashTakili = false;
                Mute();
                VolUp();
                VolDown();

                Taskbar.Show();
                SetTaskbarState(AppBarStates.AlwaysOnTop);

                RegistryKey rkey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies", true);
                rkey.CreateSubKey("System", RegistryKeyPermissionCheck.Default);
                rkey.Close();
                RegistryKey rkey2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true);
                rkey2.SetValue("DisableTaskMgr", 0);
                rkey2.Close();
            }

            flashCikarili = true;
        }
    }
}
