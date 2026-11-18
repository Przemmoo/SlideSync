using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace SlideSync
{
    internal sealed class MainForm : Form
    {
        private readonly Settings settings;

        // wspólne
        private RadioButton rbMaster, rbReceiver;
        private Label lblAddr;
        private TextBox txtHost, txtPassword;
        private NumericUpDown nudPort;
        private Label lblHostCaption;
        private Button btnStart, btnFirewall, btnPrev, btnNext;
        private Label lblStatus, lblReceiverHelp, lblKeys, lblPeers, lblTestHint;
        private CheckBox chkForward;
        private CheckedListBox clbKeys;
        private ListBox lstPeers;
        private TextBox txtLog;
        private NotifyIcon tray;
        private Icon icoIdle, icoWait, icoOk, icoBad;

        // stan
        private MasterServer server;
        private ReceiverClient receiver;
        private KeyHook hook;
        private bool running;
        private bool reallyQuit;
        private LinkState linkState = LinkState.Stopped;
        private readonly HashSet<int> enabledVks = new HashSet<int>();
        private readonly HashSet<int> forwardedDown = new HashSet<int>();

        public MainForm(Settings settings, bool autoStart)
        {
            this.settings = settings;
            BuildIcons();
            BuildUi();
            LoadSettingsToUi();
            UpdateModeUi();
            UpdateStatus();

            if (autoStart)
            {
                Shown += delegate { BeginInvoke(new Action(ToggleRun)); };
            }
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            Text = "SlideSync – synchronizacja slajdów";
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 610);
            Icon = icoIdle;

            GroupBox gb = new GroupBox();
            gb.Text = "Rola tego komputera";
            gb.SetBounds(12, 8, 496, 74);
            rbMaster = new RadioButton();
            rbMaster.Text = "Komputer sterujący  (tu jest podłączony pilot Logitech Spotlight)";
            rbMaster.SetBounds(12, 20, 470, 22);
            rbReceiver = new RadioButton();
            rbReceiver.Text = "Komputer sterowany  (slajdy przełączają się same)";
            rbReceiver.SetBounds(12, 44, 470, 22);
            rbMaster.CheckedChanged += delegate { UpdateModeUi(); };
            gb.Controls.Add(rbMaster);
            gb.Controls.Add(rbReceiver);
            Controls.Add(gb);

            // wiersz adresu: u nadawcy lista adresów IP, u odbiornika pole do wpisania adresu
            lblAddr = new Label();
            lblAddr.SetBounds(12, 92, 496, 34);
            Controls.Add(lblAddr);

            lblHostCaption = new Label();
            lblHostCaption.Text = "Adres IP komputera sterującego:";
            lblHostCaption.SetBounds(12, 96, 190, 20);
            Controls.Add(lblHostCaption);
            txtHost = new TextBox();
            txtHost.SetBounds(206, 92, 180, 23);
            Controls.Add(txtHost);

            Label lblPort = new Label();
            lblPort.Text = "Port:";
            lblPort.SetBounds(12, 134, 40, 20);
            Controls.Add(lblPort);
            nudPort = new NumericUpDown();
            nudPort.Minimum = 1;
            nudPort.Maximum = 65535;
            nudPort.SetBounds(56, 130, 80, 23);
            Controls.Add(nudPort);

            Label lblPass = new Label();
            lblPass.Text = "Hasło:";
            lblPass.SetBounds(160, 134, 50, 20);
            Controls.Add(lblPass);
            txtPassword = new TextBox();
            txtPassword.SetBounds(212, 130, 150, 23);
            Controls.Add(txtPassword);
            Label lblPassHint = new Label();
            lblPassHint.Text = "(takie samo na wszystkich komputerach; może być puste)";
            lblPassHint.ForeColor = SystemColors.GrayText;
            lblPassHint.SetBounds(12, 158, 496, 18);
            Controls.Add(lblPassHint);

            // --- tylko nadawca
            chkForward = new CheckBox();
            chkForward.Text = "Przekazuj klawisze do pozostałych komputerów (odznacz, aby wstrzymać)";
            chkForward.Checked = true;
            chkForward.SetBounds(12, 182, 496, 22);
            Controls.Add(chkForward);

            lblKeys = new Label();
            lblKeys.Text = "Przekazywane klawisze:";
            lblKeys.SetBounds(12, 210, 250, 18);
            Controls.Add(lblKeys);
            clbKeys = new CheckedListBox();
            clbKeys.CheckOnClick = true;
            clbKeys.SetBounds(12, 230, 262, 124);
            foreach (KeyInfo k in KeyCatalog.All) clbKeys.Items.Add(k, false);
            clbKeys.ItemCheck += delegate { if (IsHandleCreated) BeginInvoke(new Action(RebuildEnabledKeys)); };
            Controls.Add(clbKeys);

            lblPeers = new Label();
            lblPeers.Text = "Podłączone komputery: 0";
            lblPeers.SetBounds(286, 210, 222, 18);
            Controls.Add(lblPeers);
            lstPeers = new ListBox();
            lstPeers.SetBounds(286, 230, 222, 66);
            lstPeers.IntegralHeight = false;
            Controls.Add(lstPeers);
            btnPrev = new Button();
            btnPrev.Text = "◀ Test wstecz";
            btnPrev.SetBounds(286, 304, 106, 28);
            btnPrev.Click += delegate { SendTest(0x21); };
            Controls.Add(btnPrev);
            btnNext = new Button();
            btnNext.Text = "Test dalej ▶";
            btnNext.SetBounds(402, 304, 106, 28);
            btnNext.Click += delegate { SendTest(0x22); };
            Controls.Add(btnNext);
            lblTestHint = new Label();
            lblTestHint.Text = "Przyciski testowe wysyłają Page Up / Page Down do pozostałych komputerów.";
            lblTestHint.ForeColor = SystemColors.GrayText;
            lblTestHint.SetBounds(286, 335, 222, 32);
            Controls.Add(lblTestHint);

            // --- tylko odbiornik
            lblReceiverHelp = new Label();
            lblReceiverHelp.Text =
                "Jak używać:\r\n" +
                "1. Uruchom prezentację w trybie pokazu slajdów (F5).\r\n" +
                "2. Kliknij w okno prezentacji, aby było aktywne.\r\n" +
                "3. Slajdy będą przełączane razem z komputerem sterującym.\r\n\r\n" +
                "Program wciska klawisze w aktywnym oknie – okno z prezentacją musi być na wierzchu.";
            lblReceiverHelp.SetBounds(12, 190, 496, 120);
            Controls.Add(lblReceiverHelp);

            // --- dół
            btnStart = new Button();
            btnStart.Text = "Start";
            btnStart.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnStart.SetBounds(12, 372, 150, 36);
            btnStart.Click += delegate { ToggleRun(); };
            Controls.Add(btnStart);

            btnFirewall = new Button();
            btnFirewall.Text = "Dodaj regułę zapory…";
            btnFirewall.SetBounds(172, 372, 170, 36);
            btnFirewall.Click += delegate { AddFirewallRule(); };
            Controls.Add(btnFirewall);

            lblStatus = new Label();
            lblStatus.SetBounds(12, 416, 496, 36);
            lblStatus.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            Controls.Add(lblStatus);

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.SetBounds(12, 456, 496, 142);
            Controls.Add(txtLog);

            tray = new NotifyIcon();
            tray.Icon = icoIdle;
            tray.Visible = true;
            tray.Text = "SlideSync";
            tray.DoubleClick += delegate { RestoreWindow(); };
            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Pokaż okno", delegate { RestoreWindow(); });
            menu.MenuItems.Add("Zakończ", delegate { reallyQuit = true; Close(); });
            tray.ContextMenu = menu;
        }

        private void LoadSettingsToUi()
        {
            rbMaster.Checked = settings.Mode != "receiver";
            rbReceiver.Checked = settings.Mode == "receiver";
            nudPort.Value = settings.Port;
            txtPassword.Text = settings.Password;
            txtHost.Text = settings.Host;
            HashSet<string> on = settings.EnabledKeyNames();
            for (int i = 0; i < clbKeys.Items.Count; i++)
            {
                KeyInfo k = (KeyInfo)clbKeys.Items[i];
                clbKeys.SetItemChecked(i, on.Contains(k.Name));
            }
            RebuildEnabledKeys();
        }

        private void SaveSettingsFromUi()
        {
            settings.Mode = rbReceiver.Checked ? "receiver" : "master";
            settings.Port = (int)nudPort.Value;
            settings.Password = txtPassword.Text;
            settings.Host = txtHost.Text.Trim();
            List<string> names = new List<string>();
            for (int i = 0; i < clbKeys.Items.Count; i++)
            {
                if (clbKeys.GetItemChecked(i)) names.Add(((KeyInfo)clbKeys.Items[i]).Name);
            }
            settings.Keys = string.Join(",", names.ToArray());
            settings.Save();
        }

        private bool IsMaster
        {
            get { return rbMaster.Checked; }
        }

        private void UpdateModeUi()
        {
            bool m = IsMaster;
            lblAddr.Visible = m;
            lblHostCaption.Visible = !m;
            txtHost.Visible = !m;
            chkForward.Visible = m;
            lblKeys.Visible = m;
            clbKeys.Visible = m;
            lblPeers.Visible = m;
            lstPeers.Visible = m;
            btnPrev.Visible = m;
            btnNext.Visible = m;
            lblTestHint.Visible = m;
            btnFirewall.Visible = m;
            lblReceiverHelp.Visible = !m;
            if (m) lblAddr.Text = "Wpisz na pozostałych komputerach jeden z adresów tego komputera:\r\n" + LocalAddresses();
            UpdateStatus();
        }

        private void SetRunning(bool value)
        {
            running = value;
            btnStart.Text = running ? "Stop" : "Start";
            rbMaster.Enabled = !running;
            rbReceiver.Enabled = !running;
            nudPort.Enabled = !running;
            txtPassword.Enabled = !running;
            txtHost.Enabled = !running;
            UpdateStatus();
        }

        // ------------------------------------------------------------------ status

        private void UpdateStatus()
        {
            if (lblStatus == null || tray == null) return;
            string text;
            Icon icon;
            if (!running)
            {
                text = "Zatrzymano. Ustaw rolę komputera i kliknij Start.";
                icon = icoIdle;
            }
            else if (IsMaster)
            {
                int n = server == null ? 0 : server.PeerNames().Length;
                if (n == 0)
                {
                    text = "Nasłuchiwanie na porcie " + settings.Port + " – żaden komputer nie jest jeszcze podłączony.";
                    icon = icoWait;
                }
                else
                {
                    text = "Działa. Podłączone komputery: " + n + ". Klawisze pilota są przekazywane.";
                    icon = icoOk;
                }
            }
            else
            {
                switch (linkState)
                {
                    case LinkState.Connected:
                        text = "Połączono z " + settings.Host + ". Klawisze trafiają do aktywnego okna.";
                        icon = icoOk;
                        break;
                    case LinkState.Denied:
                        text = "Odrzucono przez komputer sterujący – sprawdź hasło.";
                        icon = icoBad;
                        break;
                    default:
                        text = "Łączenie z " + settings.Host + ":" + settings.Port + "…";
                        icon = icoWait;
                        break;
                }
            }
            lblStatus.Text = text;
            tray.Icon = icon;
            string tip = "SlideSync – " + text;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 60) + "..." : tip;
        }

        private void AppendLog(string s)
        {
            Ui(delegate
            {
                txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n");
            });
        }

        private void Ui(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                if (InvokeRequired) BeginInvoke(a); else a();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        // ------------------------------------------------------------------ start / stop

        private void ToggleRun()
        {
            if (running) { StopAll(); return; }

            if (!IsMaster && txtHost.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "Wpisz adres IP komputera sterującego (komputera 1).", "SlideSync",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtHost.Focus();
                return;
            }

            SaveSettingsFromUi();
            try
            {
                if (IsMaster) StartMaster(); else StartReceiver();
            }
            catch (SocketException ex)
            {
                MessageBox.Show(this, "Nie można uruchomić nasłuchiwania na porcie " + settings.Port + ":\r\n" + ex.Message +
                    "\r\n\r\nMoże port jest zajęty – spróbuj innego.", "SlideSync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                StopAll();
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "SlideSync", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopAll();
                return;
            }
            SetRunning(true);
            // minimalizacja oddaje fokus oknu z prezentacją
            WindowState = FormWindowState.Minimized;
        }

        private void StartMaster()
        {
            server = new MasterServer();
            server.Log += AppendLog;
            server.PeersChanged += delegate { Ui(RefreshPeers); };
            server.Start(settings.Port, settings.Password);

            hook = new KeyHook();
            hook.Key += OnHookKey;
            hook.Start();
            AppendLog("Start – nasłuchiwanie na porcie " + settings.Port + ".");
        }

        private void StartReceiver()
        {
            linkState = LinkState.Connecting;
            receiver = new ReceiverClient();
            receiver.Log += AppendLog;
            receiver.StateChanged += delegate(LinkState s) { Ui(delegate { linkState = s; UpdateStatus(); }); };
            receiver.KeyReceived += delegate(KeyInfo k) { AppendLog("← " + k.Label); };
            receiver.Start(settings.Host, settings.Port, settings.Password);
            AppendLog("Start – łączenie z " + settings.Host + ":" + settings.Port + ".");
        }

        private void StopAll()
        {
            if (hook != null) { hook.Stop(); hook = null; }
            forwardedDown.Clear();
            if (server != null) { server.Stop(); server = null; }
            if (receiver != null) { receiver.Stop(); receiver = null; }
            linkState = LinkState.Stopped;
            lstPeers.Items.Clear();
            lblPeers.Text = "Podłączone komputery: 0";
            if (running) AppendLog("Zatrzymano.");
            SetRunning(false);
        }

        private void RefreshPeers()
        {
            string[] names = server == null ? new string[0] : server.PeerNames();
            lstPeers.Items.Clear();
            foreach (string n in names) lstPeers.Items.Add(n);
            lblPeers.Text = "Podłączone komputery: " + names.Length;
            UpdateStatus();
        }

        // ------------------------------------------------------------------ nadawca: klawisze

        private void RebuildEnabledKeys()
        {
            enabledVks.Clear();
            for (int i = 0; i < clbKeys.Items.Count; i++)
            {
                if (clbKeys.GetItemChecked(i)) enabledVks.Add(((KeyInfo)clbKeys.Items[i]).Vk);
            }
        }

        private static bool ModifierDown()
        {
            // Ctrl, Alt, Win, Shift: przekazujemy tylko "gołe" klawisze, żeby nie wysyłać
            // na pozostałe komputery skrótów w rodzaju Ctrl+B czy Alt+F5.
            return Native.IsDown(0x11) || Native.IsDown(0x12) || Native.IsDown(0x5B) || Native.IsDown(0x5C) || Native.IsDown(0x10);
        }

        private void OnHookKey(int vk, bool down)
        {
            if (server == null) return;
            if (!down)
            {
                // puszczenie klawisza zawsze doganiamy, jeśli wcześniej wysłaliśmy jego wciśnięcie
                if (forwardedDown.Remove(vk)) server.Broadcast("K " + vk + " U");
                return;
            }
            if (!chkForward.Checked || !enabledVks.Contains(vk) || ModifierDown()) return;

            bool first = forwardedDown.Add(vk);    // autopowtarzanie wysyła kolejne "down" - przekazujemy je dalej
            int n = server.Broadcast("K " + vk + " D");
            if (first)
            {
                KeyInfo k = KeyCatalog.Find(vk);
                AppendLog("→ " + (k != null ? k.Label : "?") + "  (komputerów: " + n + ")");
            }
        }

        private void SendTest(int vk)
        {
            if (server == null)
            {
                MessageBox.Show(this, "Najpierw kliknij Start.", "SlideSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int n = server.Broadcast("K " + vk + " D");
            server.Broadcast("K " + vk + " U");
            KeyInfo k = KeyCatalog.Find(vk);
            AppendLog("→ [test] " + (k != null ? k.Label : "?") + "  (komputerów: " + n + ")");
        }

        // ------------------------------------------------------------------ pomocnicze

        private static string LocalAddresses()
        {
            List<string> list = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254.")) continue;
                        list.Add(ip + " (" + ni.Name + ")");
                    }
                }
            }
            catch (Exception) { }
            return list.Count == 0 ? "(nie wykryto połączenia sieciowego)" : string.Join("   ", list.ToArray());
        }

        private void AddFirewallRule()
        {
            DialogResult r = MessageBox.Show(this,
                "Dodać regułę zapory Windows, która pozwoli komputerom 2 i 3 połączyć się z tym programem (sieć prywatna/domenowa)?\r\n\r\n" +
                "Windows poprosi o zgodę administratora.\r\n" +
                "Jeśli Twoja sieć ma w Windows profil „Publiczna”, zmień ją na „Prywatna” w Ustawienia → Sieć.",
                "SlideSync", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (r != DialogResult.OK) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh",
                    "advfirewall firewall add rule name=\"SlideSync\" dir=in action=allow protocol=TCP profile=private,domain program=\"" +
                    Application.ExecutablePath + "\"");
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                AppendLog("Dodano regułę zapory (jeśli zatwierdzono okno UAC).");
            }
            catch (Exception ex)
            {
                AppendLog("Nie dodano reguły zapory: " + ex.Message);
            }
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        // ------------------------------------------------------------------ ikony

        private void BuildIcons()
        {
            icoIdle = MakeIcon(Color.FromArgb(120, 130, 145));
            icoWait = MakeIcon(Color.FromArgb(235, 150, 20));
            icoOk = MakeIcon(Color.FromArgb(40, 170, 80));
            icoBad = MakeIcon(Color.FromArgb(205, 50, 50));
        }

        private static Icon MakeIcon(Color c)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (Brush b = new SolidBrush(c)) g.FillEllipse(b, 1, 1, 30, 30);
                    using (Brush w = new SolidBrush(Color.White))
                    {
                        g.FillPolygon(w, new Point[] { new Point(12, 8), new Point(12, 24), new Point(25, 16) });
                    }
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(h).Clone();
                }
                finally
                {
                    Native.DestroyIcon(h);
                }
            }
        }

        // ------------------------------------------------------------------ zamykanie

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyQuit && e.CloseReason == CloseReason.UserClosing && running)
            {
                // podczas pracy "X" tylko minimalizuje - żeby nie przerwać prezentacji przypadkiem
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
                return;
            }
            SaveSettingsFromUi();
            StopAll();
            tray.Visible = false;
            tray.Dispose();
            base.OnFormClosing(e);
        }
    }
}
