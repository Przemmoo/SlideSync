using System.Collections.Generic;

namespace SlideSync
{
    internal sealed class KeyInfo
    {
        public readonly string Name;
        public readonly string Label;
        public readonly int Vk;
        public readonly bool Extended;
        public readonly bool DefaultOn;

        public KeyInfo(string name, string label, int vk, bool extended, bool defaultOn)
        {
            Name = name;
            Label = label;
            Vk = vk;
            Extended = extended;
            DefaultOn = defaultOn;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// Lista klawisków, które wolno przekazywać. Odbiornik wciska WYŁĄCZNIE klawisze z tej listy,
    /// więc nawet obcy "nadawca" w sieci nie może uruchomić np. Win+R.
    /// </summary>
    internal static class KeyCatalog
    {
        public static readonly KeyInfo[] All;
        private static readonly Dictionary<int, KeyInfo> ByVk = new Dictionary<int, KeyInfo>();

        static KeyCatalog()
        {
            List<KeyInfo> l = new List<KeyInfo>();
            l.Add(new KeyInfo("PageDown", "Page Down (następny slajd)", 0x22, true, true));
            l.Add(new KeyInfo("PageUp", "Page Up (poprzedni slajd)", 0x21, true, true));
            l.Add(new KeyInfo("Right", "Strzałka w prawo", 0x27, true, true));
            l.Add(new KeyInfo("Left", "Strzałka w lewo", 0x25, true, true));
            l.Add(new KeyInfo("Down", "Strzałka w dół", 0x28, true, false));
            l.Add(new KeyInfo("Up", "Strzałka w górę", 0x26, true, false));
            l.Add(new KeyInfo("Home", "Home (pierwszy slajd)", 0x24, true, true));
            l.Add(new KeyInfo("End", "End (ostatni slajd)", 0x23, true, true));
            l.Add(new KeyInfo("F5", "F5 (start pokazu)", 0x74, false, true));
            l.Add(new KeyInfo("Escape", "Esc (koniec pokazu)", 0x1B, false, true));
            l.Add(new KeyInfo("B", "B (czarny ekran)", 0x42, false, true));
            l.Add(new KeyInfo("Period", ". (czarny ekran)", 0xBE, false, true));
            l.Add(new KeyInfo("W", "W (biały ekran)", 0x57, false, false));
            l.Add(new KeyInfo("Comma", ", (biały ekran)", 0xBC, false, false));
            l.Add(new KeyInfo("Space", "Spacja", 0x20, false, false));
            l.Add(new KeyInfo("Enter", "Enter", 0x0D, false, false));
            l.Add(new KeyInfo("Backspace", "Backspace", 0x08, false, false));
            for (int d = 0; d <= 9; d++)
            {
                l.Add(new KeyInfo("D" + d, "Cyfra " + d + " (skok do slajdu + Enter)", 0x30 + d, false, false));
            }
            All = l.ToArray();
            foreach (KeyInfo k in All)
            {
                ByVk[k.Vk] = k;
            }
        }

        public static KeyInfo Find(int vk)
        {
            KeyInfo k;
            return ByVk.TryGetValue(vk, out k) ? k : null;
        }
    }
}
