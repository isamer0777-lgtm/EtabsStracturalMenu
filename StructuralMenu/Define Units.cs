// =====================================================================
//  StructuralMenu - ETABS 22 Plug-in
//  ملف Define Units: يفتح نافذة Consistent Units... في الإيتابس مباشرة
//  من غير ماوس ومن غير ما القوائم تظهر للمستخدم
// =====================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StructuralMenu
{
    internal static class UnitsAutomation
    {
        const string BUTTON = "Units";              // زرار شريط الحالة: Units...
        const string ITEM = "Consistent Units";     // عنصر القائمة: Consistent Units...

        static ToolStripItem _item;                 // بيتحفظ بعد أول مرة فالمرات اللي بعدها فورية

        static readonly MethodInfo OnShow = typeof(ToolStripDropDownItem)
            .GetMethod("OnDropDownShow", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo OnClick = typeof(Control)
            .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void OpenConsistentUnits()
        {
            var form = Control.FromHandle(Process.GetCurrentProcess().MainWindowHandle) as Form;
            if (form == null) { Fail(); return; }
            // BeginInvoke: نسيب قائمة Structural تقفل الأول، وبعدين نفتح النافذة
            form.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (_item == null || _item.IsDisposed) _item = Locate(form);
                    if (_item == null) { Fail(); return; }
                    (_item.Owner as ToolStripDropDown)?.Close();
                    _item.PerformClick();                    // نفس الضغط على Consistent Units...
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Define Units"); }
            }));
        }

        // ---------- إيجاد أمر Consistent Units... (أول مرة بس) ----------
        static ToolStripItem Locate(Form form)
        {
            object units = FindUnits(form);
            if (units == null) return null;

            // 1) لو القائمة تبع الزرار نفسه: نجهزها من غير ما تظهر ونمسك العنصر
            if (units is ToolStripDropDownItem dd)
            {
                OnShow?.Invoke(dd, new object[] { EventArgs.Empty });
                var hit = Find(dd.DropDownItems);
                if (hit != null) return hit;
            }
            if (units is Control c && c.ContextMenuStrip != null)
            {
                var hit = Find(c.ContextMenuStrip.Items);
                if (hit != null) return hit;
            }

            // 2) لو القائمة بتتعمل وقت الضغط: نضغط الزرار برمجيًا ونمسك القائمة ونقفلها قبل ما تترسم
            if (units is ToolStripDropDownItem d2) d2.ShowDropDown();
            else if (units is ToolStripItem ti) ti.PerformClick();
            else if (units is Control ctl) OnClick?.Invoke(ctl, new object[] { EventArgs.Empty });

            foreach (ToolStripDropDown menu in OpenMenus())
            {
                var hit = Find(menu.Items);
                if (hit != null) { menu.Close(); return hit; }
            }
            return null;
        }

        // بيدوّر على زرار Units... بس (في الأزرار وشرائط الأدوات زي شريط الحالة)
        static object FindUnits(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is ToolStrip ts)
                {
                    var item = ts.Items.OfType<ToolStripItem>().FirstOrDefault(i => Starts(i.Text, BUTTON));
                    if (item != null) return item;
                }
                else if (c.Controls.Count == 0 && Starts(c.Text, BUTTON)) return c;

                var inner = FindUnits(c);
                if (inner != null) return inner;
            }
            return null;
        }

        static ToolStripItem Find(ToolStripItemCollection items) =>
            items.OfType<ToolStripItem>().FirstOrDefault(i => Starts(i.Text, ITEM));

        static bool Starts(string text, string prefix) =>
            (text ?? "").Replace("&", "").Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        // القوائم المفتوحة حاليًا في الإيتابس
        static List<ToolStripDropDown> OpenMenus()
        {
            var list = new List<ToolStripDropDown>();
            EnumThreadWindows(GetCurrentThreadId(), (h, l) =>
            {
                if (Control.FromHandle(h) is ToolStripDropDown d && d.Visible) list.Add(d);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        static void Fail() =>
            MessageBox.Show("مش لاقي أمر Consistent Units... في الإيتابس.", "Define Units");

        delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint threadId, EnumProc cb, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    }
}