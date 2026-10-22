// =====================================================================
//  StructuralMenu - ETABS 22 Plug-in
//  ملف Define Material: يفتح Define > Material Properties...
//  ويختار 4000Psi ويضغط Add Copy of Material... تلقائيًا (من غير ماوس)
// =====================================================================
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace StructuralMenu
{
    internal static class MaterialAutomation
    {
        const string DIALOG = "Define Materials";      // عنوان الواجهة
        const string MATERIAL = "4000Psi";               // الخامة اللي هتتعلّم
        const string BUTTON = "Add Copy of Material";  // الزرار اللي هيتضغط

        public static void AddCopyOf4000Psi()
        {
            var form = Control.FromHandle(Process.GetCurrentProcess().MainWindowHandle) as Form;
            if (form == null) return;
            form.BeginInvoke(new Action(() =>
            {
                // مراقب بيستنى واجهة Define Materials تفتح (الواجهة Modal فبتوقف الكود لحد ما تتقفل)
                int ticks = 0;
                var timer = new Timer { Interval = 30 };
                timer.Tick += (s, e) =>
                {
                    if (++ticks > 200 || Act()) { timer.Stop(); timer.Dispose(); }
                };
                timer.Start();

                if (!OpenMenu(form, "Define", "Material Properties"))
                {
                    timer.Stop();
                    MessageBox.Show("مش لاقي أمر Define > Material Properties...", "Define Material");
                }
            }));
        }

        // ---------- جوه واجهة Define Materials: تعليم 4000Psi ثم Add Copy of Material... ----------
        static bool Act()
        {
            IntPtr dlg = IntPtr.Zero, list = IntPtr.Zero, btn = IntPtr.Zero;
            EnumThreadWindows(GetCurrentThreadId(), (h, l) =>
            {
                if (IsWindowVisible(h) && Text(h) == DIALOG) { dlg = h; return false; }
                return true;
            }, IntPtr.Zero);
            if (dlg == IntPtr.Zero) return false;

            EnumChildWindows(dlg, (h, l) =>
            {
                if (list == IntPtr.Zero && Class(h).ToUpper().Contains("LISTBOX")) list = h;
                if (btn == IntPtr.Zero && Text(h).Replace("&", "").StartsWith(BUTTON)) btn = h;
                return true;
            }, IntPtr.Zero);
            if (list == IntPtr.Zero || btn == IntPtr.Zero) return false;

            int index = (int)SendMessage(list, 0x01A2, (IntPtr)(-1), MATERIAL);       // LB_FINDSTRINGEXACT
            if (index < 0) { MessageBox.Show("الخامة " + MATERIAL + " مش موجودة", "Define Material"); return true; }
            SendMessage(list, 0x0186, (IntPtr)index, IntPtr.Zero);                    // LB_SETCURSEL
            int id = GetDlgCtrlID(list);
            SendMessage(GetParent(list), 0x0111, (IntPtr)((1 << 16) | (id & 0xFFFF)), list);   // LBN_SELCHANGE
            PostMessage(btn, 0x00F5, IntPtr.Zero, IntPtr.Zero);                        // BM_CLICK
            return true;
        }

        // ---------- فتح أمر من قوائم الإيتابس من غير ما القائمة تظهر ----------
        static bool OpenMenu(Form form, string menu, string item)
        {
            MenuStrip strip = form.MainMenuStrip ?? FindStrip(form);
            var top = strip?.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => Starts(i.Text, menu));
            if (top != null)
            {
                typeof(ToolStripDropDownItem).GetMethod("OnDropDownShow", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(top, new object[] { EventArgs.Empty });          // تجهيز عناصر القائمة
                var cmd = top.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(i => Starts(i.Text, item));
                if (cmd != null) { cmd.PerformClick(); return true; }
            }
            // احتياطي: قوائم Win32 أصلية
            IntPtr bar = GetMenu(form.Handle);
            for (int i = 0; bar != IntPtr.Zero && i < GetMenuItemCount(bar); i++)
            {
                if (!Starts(MenuText(bar, i), menu)) continue;
                IntPtr sub = GetSubMenu(bar, i);
                for (int j = 0; j < GetMenuItemCount(sub); j++)
                    if (Starts(MenuText(sub, j), item))
                    {
                        SendMessage(form.Handle, 0x0111, (IntPtr)GetMenuItemID(sub, j), IntPtr.Zero);   // WM_COMMAND
                        return true;
                    }
            }
            return false;
        }

        static MenuStrip FindStrip(Control p)
        {
            foreach (Control c in p.Controls)
                if (c is MenuStrip m) return m;
                else if (FindStrip(c) is MenuStrip inner) return inner;
            return null;
        }

        static bool Starts(string t, string p) =>
            (t ?? "").Replace("&", "").Trim().StartsWith(p, StringComparison.OrdinalIgnoreCase);

        static string Text(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString().Trim(); }
        static string Class(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
        static string MenuText(IntPtr m, int i) { var sb = new StringBuilder(256); GetMenuString(m, (uint)i, sb, 256, 0x400); return sb.ToString(); }

        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint id, EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int max);
        [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr m, int pos);
        [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr m);
        [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr m, int pos);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetMenuString(IntPtr m, uint i, StringBuilder s, int max, uint f);
    }
}