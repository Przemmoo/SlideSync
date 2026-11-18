using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SlideSync
{
    /// <summary>
    /// Globalny hak klawiatury (działa także, gdy okno programu jest zminimalizowane).
    /// Klawisze NIE są blokowane - komputer 1 przełącza slajdy tak samo jak dotychczas.
    /// Zdarzenie jest wywoływane w wątku interfejsu.
    /// </summary>
    internal sealed class KeyHook : IDisposable
    {
        private Native.LowLevelKeyboardProc proc;   // pole, żeby GC nie zebrał delegata
        private IntPtr hook = IntPtr.Zero;

        /// <summary>(vk, true = wciśnięty / false = puszczony)</summary>
        public event Action<int, bool> Key;

        public void Start()
        {
            if (hook != IntPtr.Zero) return;
            proc = Callback;
            using (Process p = Process.GetCurrentProcess())
            using (ProcessModule m = p.MainModule)
            {
                hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, proc, Native.GetModuleHandle(m.ModuleName), 0);
            }
            if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Stop()
        {
            if (hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    Native.KBDLLHOOKSTRUCT k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                    // pomijamy klawisze, które sami wstrzyknęliśmy (ochrona przed pętlą)
                    if (k.dwExtraInfo != Native.Magic)
                    {
                        int msg = wParam.ToInt32();
                        bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                        bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
                        Action<int, bool> h = Key;
                        if ((down || up) && h != null) h((int)k.vkCode, down);
                    }
                }
                catch (Exception)
                {
                    // wyjątek w haku nie może zablokować klawiatury
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }
    }
}
