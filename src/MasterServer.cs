using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SlideSync
{
    /// <summary>
    /// Serwer po stronie komputera 1. Odbiorniki (komputery 2, 3, ...) łączą się do niego,
    /// a każdy klawisz z <see cref="Broadcast"/> trafia do wszystkich podłączonych.
    ///
    /// Protokół (tekst, linie zakończone \n):
    ///   odbiornik -> serwer:  HELLO \t 1 \t hasło
    ///   serwer -> odbiornik:  OK  albo  DENY
    ///   serwer -> odbiornik:  K &lt;vk&gt; D|U   (klawisz wciśnięty / puszczony)
    ///                         PING             (co sekundę przy bezczynności - wykrywanie zerwanego łącza)
    /// </summary>
    internal sealed class MasterServer
    {
        private sealed class Peer
        {
            public readonly TcpClient Tcp;
            public readonly string Name;
            public readonly BlockingCollection<string> Queue = new BlockingCollection<string>(1000);

            public Peer(TcpClient tcp, string name)
            {
                Tcp = tcp;
                Name = name;
            }
        }

        private readonly object sync = new object();
        private readonly List<Peer> peers = new List<Peer>();
        private TcpListener listener;
        private Thread acceptThread;
        private volatile bool running;
        private string password = "";

        public event Action<string> Log;
        public event Action PeersChanged;

        public void Start(int port, string pass)
        {
            password = pass ?? "";
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running = true;
            acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Name = "SlideSync-accept";
            acceptThread.Start();
        }

        public void Stop()
        {
            running = false;
            try { listener.Stop(); } catch (Exception) { }
            List<Peer> copy;
            lock (sync)
            {
                copy = new List<Peer>(peers);
            }
            foreach (Peer p in copy)
            {
                try { p.Tcp.Close(); } catch (Exception) { }
            }
        }

        public string[] PeerNames()
        {
            lock (sync)
            {
                string[] names = new string[peers.Count];
                for (int i = 0; i < peers.Count; i++) names[i] = peers[i].Name;
                return names;
            }
        }

        public int Broadcast(string line)
        {
            Peer[] copy;
            lock (sync)
            {
                copy = peers.ToArray();
            }
            int n = 0;
            foreach (Peer p in copy)
            {
                if (p.Queue.TryAdd(line)) n++;
            }
            return n;
        }

        private void AcceptLoop()
        {
            while (running)
            {
                TcpClient c;
                try
                {
                    c = listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    break;
                }
                Thread t = new Thread(delegate() { HandlePeer(c); });
                t.IsBackground = true;
                t.Name = "SlideSync-peer";
                t.Start();
            }
        }

        private void HandlePeer(TcpClient c)
        {
            string name = "?";
            Peer peer = null;
            try
            {
                IPEndPoint ep = c.Client.RemoteEndPoint as IPEndPoint;
                if (ep != null) name = ep.Address.ToString();

                c.NoDelay = true;
                c.ReceiveTimeout = 5000;
                c.SendTimeout = 3000;
                NetworkStream stream = c.GetStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.NewLine = "\n";

                string hello = reader.ReadLine();
                string[] parts = hello == null ? new string[0] : hello.Split(new char[] { '\t' }, 3);
                if (parts.Length < 2 || parts[0] != "HELLO" || parts[1] != "1")
                {
                    OnLog("Odrzucono połączenie z " + name + " (to nie jest SlideSync).");
                    return;
                }
                string pw = parts.Length > 2 ? parts[2] : "";
                if (pw != password)
                {
                    writer.WriteLine("DENY");
                    writer.Flush();
                    OnLog("Odrzucono " + name + ": nieprawidłowe hasło.");
                    return;
                }
                writer.WriteLine("OK");
                writer.Flush();

                peer = new Peer(c, name);
                lock (sync)
                {
                    peers.Add(peer);
                }
                OnLog("Podłączono komputer: " + name);
                OnPeersChanged();

                while (running)
                {
                    string line;
                    if (!peer.Queue.TryTake(out line, 1000)) line = "PING";
                    writer.WriteLine(line);
                    writer.Flush();
                }
            }
            catch (Exception)
            {
                // zerwane połączenie - sprzątamy poniżej
            }
            finally
            {
                if (peer != null)
                {
                    lock (sync)
                    {
                        peers.Remove(peer);
                    }
                    OnLog("Rozłączono komputer: " + name);
                    OnPeersChanged();
                }
                try { c.Close(); } catch (Exception) { }
            }
        }

        private void OnLog(string s)
        {
            Action<string> h = Log;
            if (h != null) h(s);
        }

        private void OnPeersChanged()
        {
            Action h = PeersChanged;
            if (h != null) h();
        }
    }
}
