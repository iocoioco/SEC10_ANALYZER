using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace New_Tradegy.Library.Utils
{
    public static class WinApiUtils
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;

        public static void BringToFront(Form form)
        {
            IntPtr handle = form.Handle;
            ShowWindow(handle, SW_RESTORE); // Restore if minimized
            SetForegroundWindow(handle);    // Bring to front
        }

        public static void Minimize(Form form)
        {
            ShowWindow(form.Handle, SW_MINIMIZE);
        }

        public static void Flash(Form form)
        {
            FlashWindow(form.Handle, true);
        }

        public static bool IsAppFocused(Form form)
        {
            return form.ContainsFocus || Form.ActiveForm == form;
        }
    }
}
