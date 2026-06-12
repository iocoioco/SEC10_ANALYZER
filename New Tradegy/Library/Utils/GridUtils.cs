using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy.Library.Utils
{
    public static class GridUtils
    {

        public static DataGridView FindDgvRecursive(Control root, string symbol)
        {
            if (root == null)
                return null;

            if (root is DataGridView dgv)
            {
                if (string.Equals(dgv.Name, symbol, StringComparison.OrdinalIgnoreCase))
                    return dgv;
            }

            foreach (Control c in root.Controls)
            {
                var r = FindDgvRecursive(c, symbol);
                if (r != null)
                    return r;
            }

            return null;
        }


        // ----------------------------
        // Set
        // ----------------------------
        public static void SetCellText(DataGridView dgv, int row, int col, string text)
            {
                if (dgv == null || dgv.IsDisposed) return;

                if (dgv.InvokeRequired)
                {
                    try
                    {
                        dgv.BeginInvoke((MethodInvoker)(() => TrySetCellText(dgv, row, col, text)));
                    }
                    catch { /* ignore */ }
                }
                else
                {
                    TrySetCellText(dgv, row, col, text);
                }
            }

            private static void TrySetCellText(DataGridView dgv, int row, int col, string text)
            {
                if (dgv == null || dgv.IsDisposed) return;
                if (row < 0 || col < 0) return;
                if (row >= dgv.Rows.Count || col >= dgv.Columns.Count) return;

                dgv.Rows[row].Cells[col].Value = text;
            }

            // ----------------------------
            // Get (string)
            // ----------------------------
            public static string GetCellText(DataGridView dgv, int row, int col)
            {
                if (dgv == null || dgv.IsDisposed) return null;

                if (dgv.InvokeRequired)
                {
                    try
                    {
                        return (string)dgv.Invoke(new Func<string>(() => TryGetCellText(dgv, row, col)));
                    }
                    catch
                    {
                        return null;
                    }
                }

                return TryGetCellText(dgv, row, col);
            }

            private static string TryGetCellText(DataGridView dgv, int row, int col)
            {
                if (dgv == null || dgv.IsDisposed) return null;
                if (row < 0 || col < 0) return null;
                if (row >= dgv.Rows.Count || col >= dgv.Columns.Count) return null;

                return dgv.Rows[row].Cells[col].Value?.ToString();
            }

            // ----------------------------
            // Get (int) : "12,345" / "매도1 12,345" 같은 것도 대응
            // ----------------------------
            public static int GetCellInt(DataGridView dgv, int row, int col)
            {
                var s = GetCellText(dgv, row, col);
                if (string.IsNullOrEmpty(s)) return 0;

                s = s.Replace(",", "");
                int n;
                if (int.TryParse(s, out n)) return n;

                return StringUtils.ExtractIntFromString(s);
            }
        
    }
}