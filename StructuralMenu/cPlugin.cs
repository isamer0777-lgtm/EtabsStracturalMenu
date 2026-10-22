// =====================================================================
//  StructuralMenu - ETABS 22 Plug-in
//  الملف ده: تابة "Structural" + أوامرها + استدعاء الأوامر
//  Plug-In Name في ETABS لازم يساوي اسم الـ namespace = StructuralMenu
// =====================================================================
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ETABSv1;

namespace StructuralMenu
{
    public class cPlugin
    {
        internal static cSapModel Model;
        private static bool _added;              // يمنع إضافة التابة مرتين في نفس الجلسة

        public int Info(ref string Text)
        {
            Text = "Adds a 'Structural' menu next to Help.";
            return 0;
        }


        public void Main(ref cSapModel SapModel, ref cPluginCallback ISapPlugin)
        {
            Model = SapModel;
            int ret = 0;
            try
            {
                if (!_added)
                {
                    IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                    _added = TryAddToMenuStrip(hwnd) || NativeMenu.TryAdd(hwnd);
                    if (!_added)
                        MessageBox.Show("تعذر إضافة تابة Structural لشريط القوائم.", "Structural");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Structural");
                ret = 1;
            }
            // لازم نقول لـ ETABS إن البلاجن خلص، والتابة هتفضل شغالة طول الجلسة
            ISapPlugin.Finish(ret);
        }

        // ---------- الطريقة 1: لو قائمة ETABS من نوع WinForms MenuStrip ----------
        private static bool TryAddToMenuStrip(IntPtr hwnd)
        {
            if (!(Control.FromHandle(hwnd) is Form form)) return false;
            MenuStrip strip = form.MainMenuStrip ?? FindStrip(form);
            if (strip == null) return false;

            Action add = () =>
            {
                var menu = BuildMenu();
                int help = strip.Items.Cast<ToolStripItem>()
                    .ToList().FindIndex(i => i.Text.Replace("&", "") == "Help");
                if (help >= 0) strip.Items.Insert(help + 1, menu);
                else strip.Items.Add(menu);
            };
            if (strip.InvokeRequired) strip.Invoke(add); else add();
            return true;
        }

        private static MenuStrip FindStrip(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is MenuStrip m) return m;
                var inner = FindStrip(c);
                if (inner != null) return inner;
            }
            return null;
        }

        private static ToolStripMenuItem BuildMenu()
        {
            var root = new ToolStripMenuItem("&Structural");
            foreach (var cmd in Commands.All)
            {
                var item = new ToolStripMenuItem(cmd.Text);
                var run = cmd.Run;
                item.Click += (s, e) => Commands.Safe(run);
                root.DropDownItems.Add(item);
            }
            return root;
        }
    }

    // ---------- أوامر التابة (عدّل / زوّد هنا) ----------
    internal class Command
    {
        public string Text; public Action Run;
        public Command(string text, Action run) { Text = text; Run = run; }
    }

    internal static class Commands
    {
        public static readonly Command[] All =
        {
            new Command("Define Units", DefineUnits),
            new Command("Define Material", DefineMaterial),
            new Command("Define Sections", DefineSections),
            new Command("Draw", Draw),
            new Command("Assign", Assign),
            new Command("Modify", Modify),
        };

        // قيم مبدئية - عدّلها حسب مشروعك
        const string MAT = "C25";          // اسم الخرسانة
        const string SEC = "B25x60";       // اسم قطاع الكمرة

        public static void Safe(Action a)
        {
            try { a(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Structural"); }
        }

        static void Check(int ret, string what)
        {
            if (ret != 0) throw new Exception("فشل: " + what);
        }

        // 1) استدعاء أمر Define Units (الكود بتاعه في ملف Define Units.cs)
        private static void DefineUnits()
        {
            UnitsAutomation.OpenConsistentUnits();
        }

        // 2) استدعاء أمر Define Material (الكود بتاعه في ملف Define Material.cs)
        private static void DefineMaterial()
        {
            MaterialAutomation.AddCopyOf4000Psi();
        }

        // خامة خرسانة C25 بالـ API (بيستخدمها Define Sections لو الخامة مش موجودة)
        private static void CreateMaterialC25()
        {
            var m = cPlugin.Model;
            Check(m.SetPresentUnits(eUnits.kN_m_C), "Units");
            Check(m.PropMaterial.SetMaterial(MAT, eMatType.Concrete), "SetMaterial");
            // E = 4400 x sqrt(fcu) = 22000 MPa  |  v = 0.2  |  a = 1e-5
            Check(m.PropMaterial.SetMPIsotropic(MAT, 22000000, 0.2, 0.00001), "SetMPIsotropic");
            Check(m.PropMaterial.SetWeightAndMass(MAT, 1, 25), "SetWeightAndMass");   // 25 kN/m3
        }

        // 3) قطاع كمرة مستطيل 25 x 60 cm
        private static void DefineSections()
        {
            var m = cPlugin.Model;
            Check(m.SetPresentUnits(eUnits.kN_m_C), "Units");
            int n = 0; string[] names = new string[0];
            if (m.PropMaterial.GetNameList(ref n, ref names) != 0 || Array.IndexOf(names, MAT) < 0)
                CreateMaterialC25();
            Check(m.PropFrame.SetRectangle(SEC, MAT, 0.60, 0.25), "SetRectangle");   // T3 = العمق , T2 = العرض
            MessageBox.Show("تم تعريف القطاع " + SEC, "Define Sections");
        }

        // 4) رسم كمرة 5 m عند منسوب 3 m بالقطاع المعرّف
        private static void Draw()
        {
            var m = cPlugin.Model;
            Check(m.SetPresentUnits(eUnits.kN_m_C), "Units");
            string name = "";
            Check(m.FrameObj.AddByCoord(0, 0, 3, 5, 0, 3, ref name, SEC), "AddByCoord (اتأكد إن القطاع " + SEC + " متعرّف)");
            m.View.RefreshView(0, false);
            MessageBox.Show("تم رسم الكمرة " + name, "Draw");
        }

        // 5) Assign - اكتب هنا الكود الخاص بالأمر
        private static void Assign()
        {
            MessageBox.Show("أمر Assign - لسه مفيش كود متعرّف له.", "Assign");
        }

        // 6) Modify - اكتب هنا الكود الخاص بالأمر
        private static void Modify()
        {
            MessageBox.Show("أمر Modify - لسه مفيش كود متعرّف له.", "Modify");
        }
    }

    // ---------- الطريقة 2: لو قائمة ETABS قائمة Win32 أصلية (HMENU) ----------
    internal sealed class NativeMenu : NativeWindow
    {
        [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool AppendMenu(IntPtr hMenu, uint flags, UIntPtr id, string text);
        [DllImport("user32.dll")] static extern bool DrawMenuBar(IntPtr hWnd);

        const uint MF_STRING = 0x0, MF_POPUP = 0x10;
        const int WM_COMMAND = 0x0111, BASE_ID = 0x7A00;   // أرقام أوامر بعيدة عن أوامر ETABS
        static NativeMenu _hook;

        public static bool TryAdd(IntPtr hwnd)
        {
            IntPtr bar = GetMenu(hwnd);
            if (bar == IntPtr.Zero) return false;
            IntPtr popup = CreatePopupMenu();
            for (int i = 0; i < Commands.All.Length; i++)
                AppendMenu(popup, MF_STRING, (UIntPtr)(uint)(BASE_ID + i), Commands.All[i].Text);
            AppendMenu(bar, MF_POPUP, (UIntPtr)(ulong)popup.ToInt64(), "&Structural");
            DrawMenuBar(hwnd);
            _hook = new NativeMenu();
            _hook.AssignHandle(hwnd);          // نلتقط ضغطات أوامر التابة
            return true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_COMMAND)
            {
                int id = (int)((long)m.WParam & 0xFFFF);
                int idx = id - BASE_ID;
                if (idx >= 0 && idx < Commands.All.Length)
                {
                    Commands.Safe(Commands.All[idx].Run);
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}