using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SlideSync
{
    internal enum LinkState
    {
        Stopped,
        Connecting,
        Connected,
        Denied
    }

    /// <summary>
    /// Klient po stronie komputera 2/3: łączy się z komputerem 1 (z automatycznym ponawianiem)
    /// i wciska otrzymane klawisze w aktywnym oknie.
    /// </summary>
    internal sealed class ReceiverClient
    {
        private readonly object sync = new object();
        private readonly HashSet<int> pressed = new HashSet<int>();
        private Thread thread;
        private TcpClient current;
        private volatile bool running;
        private string host;
        private int port;
        private string password;

        public event Action<string> Log;
        public event Action<LinkState> StateChanged;
        public event Action<KeyInfo> KeyReceived;

        public void Start(string host, int port, string password)
        {
            this.host = host;
            this.port = port;
            this.password = password ?? "";
            running = true;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "SlideSync-receiver";
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            lock (sync)
            {
                if (current != null)
                {
                    try { current.Close(); } catch (Exception) { }
                }
            }
            if (thread != null) thread.Join(3000);
            SetState(LinkState.Stopped);
        }

        private void Run()
        {
            string lastError = null;
            while (running)
            {
                TcpClient c = new TcpClient();
                lock (sync)
                {
                    current = c;
                }
                try
                {
                    SetState(LinkState.Connecting);
                    IAsyncResult ar = c.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(3000)) throw new TimeoutException("brak odpowiedzi");
                    c.EndConnect(ar);

                    c.NoDelay = true;
                    c.ReceiveTimeout = 5000;   // serwer wysyła PING co sekundę, więc 5 s ciszy = zerwane łącze
                    c.SendTimeout = 3000;
                    NetworkStream stream = c.GetStream();
                    StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false));
                    writer.NewLine = "\n";
                    writer.WriteLine("HELLO\t1\t" + password);
                    writer.Flush();

                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    string resp = reader.ReadLine();
                    if (resp == "DENY")
                    {
                        SetState(LinkState.Denied);
                        if (lastError != "deny") OnLog("Komputer sterujący odrzucił połączenie - sprawdź hasło.");
                        lastError = "deny";
                        WaitBeforeRetry(3000);
                        continue;
                    }
                    if (resp != "OK") throw new IOException("nieprawidłowa odpowiedź serwera");

                    lastError = null;
                    SetState(LinkState.Connected);
                    OnLog("Połączono z " + host + ":" + port);

                    string line;
                    while (running && (line = reader.ReadLine()) != null)
                    {
                        Handle(line);
                    }
                    if (running) OnLog("Połączenie zakończone przez komputer sterujący.");
                }
                catch (Exception ex)
                {
                    if (running)
                    {
                        string msg = (ex is SocketException || ex is TimeoutException || ex is IOException)
                            ? ex.Message : ex.GetType().Name + ": " + ex.Message;
                        if (lastError != msg) OnLog("Brak połączenia (" + msg + "). Ponawiam...");
                        lastError = msg;
                    }
                }
                finally
                {
                    ReleaseAll();
                    try { c.Close(); } catch (Exception) { }
                }
                if (running)
                {
                    SetState(LinkState.Connecting);
                    WaitBeforeRetry(1500);
                }
            }
        }

        private void WaitBeforeRetry(int ms)
        {
            int waited = 0;
            while (running && waited < ms)
            {
                Thread.Sleep(100);
                waited += 100;
            }
        }

        private void Handle(string line)
        {
            if (line == "PING") return;
            string[] p = line.Split(' ');
            if (p.Length != 3 || p[0] != "K") return;
            int vk;
            if (!int.TryParse(p[1], out vk)) return;
            KeyInfo key = KeyCatalog.Find(vk);
            if (key == null) return;                  // klawisz spoza białej listy - ignorujemy
            bool down = p[2] == "D";
            if (!down && p[2] != "U") return;

            if (down) pressed.Add(vk); else pressed.Remove(vk);
            Native.SendKey(key, down);

            if (down)
            {
                Action<KeyInfo> h = KeyReceived;
                if (h != null) h(key);
            }
        }

        /// <summary>Po zerwaniu łącza zwalnia klawisze, które mogły zostać "wciśnięte".</summary>
        private void ReleaseAll()
        {
            foreach (int vk in pressed)
            {
                KeyInfo k = KeyCatalog.Find(vk);
                if (k != null) Native.SendKey(k, false);
            }
            pressed.Clear();
        }

        private void SetState(LinkState s)
        {
            Action<LinkState> h = StateChanged;
            if (h != null) h(s);
        }

        private void OnLog(string s)
        {
            Action<string> h = Log;
            if (h != null) h(s);
        }
    }
}
