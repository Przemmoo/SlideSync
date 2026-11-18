using System;
using System.Collections.Generic;
using System.IO;

namespace SlideSync
{
    internal sealed class Settings
    {
        public const int DefaultPort = 5599;

        public string Mode = "master";           // "master" albo "receiver"
        public int Port = DefaultPort;
        public string Password = "";
        public string Host = "";
        public string Keys = null;               // null = domyślny zestaw; inaczej lista nazw po przecinku

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlideSync");
                return Path.Combine(dir, "settings.ini");
            }
        }

        public HashSet<string> EnabledKeyNames()
        {
            HashSet<string> set = new HashSet<string>();
            if (Keys == null)
            {
                foreach (KeyInfo k in KeyCatalog.All)
                {
                    if (k.DefaultOn) set.Add(k.Name);
                }
            }
            else
            {
                foreach (string n in Keys.Split(','))
                {
                    string t = n.Trim();
                    if (t.Length > 0) set.Add(t);
                }
            }
            return set;
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string key = line.Substring(0, i).Trim();
                    string val = line.Substring(i + 1).Trim();
                    switch (key)
                    {
                        case "Mode": s.Mode = (val == "receiver") ? "receiver" : "master"; break;
                        case "Port":
                            int p;
                            if (int.TryParse(val, out p) && p >= 1 && p <= 65535) s.Port = p;
                            break;
                        case "Password": s.Password = val; break;
                        case "Host": s.Host = val; break;
                        case "Keys": s.Keys = val; break;
                    }
                }
            }
            catch (Exception)
            {
                // uszkodzony plik ustawień - zostają domyślne
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                List<string> lines = new List<string>();
                lines.Add("Mode=" + Mode);
                lines.Add("Port=" + Port);
                lines.Add("Password=" + Password);
                lines.Add("Host=" + Host);
                if (Keys != null) lines.Add("Keys=" + Keys);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception)
            {
                // brak możliwości zapisu nie powinien psuć działania programu
            }
        }
    }
}
