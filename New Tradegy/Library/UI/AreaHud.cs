// AreaHud.cs  (HudBaseForm 기반, MouseHudForm 스타일)
// C# 7.3

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy.Library.UI
{
    public static class AreaHud
    {
        private const int MAX_HUD = 10;

        private const int DEFAULT_DURATION_MS = 3000;
        private const float DEFAULT_FONT_SIZE = 15f;

        // HudBaseForm 스타일 파라미터(고정 정책)
        private const int PADDING = 8;
        private const int CORNER_RADIUS = 10;
        private const int BG_ALPHA = 0; // 완전 투명(글자만)
        private static readonly Color DEFAULT_FORE = Color.Black;

        private static readonly object _gate = new object();

        private static readonly Dictionary<string, Item> _items = new Dictionary<string, Item>();
        private static readonly List<string> _fifo = new List<string>();
        private static readonly Stack<AreaHudForm> _pool = new Stack<AreaHudForm>();

        public static int GetVisibleHudCount()
        {
            lock (_gate)
            {
                int cnt = 0;
                foreach (var kv in _items)
                {
                    var f = kv.Value?.Form;
                    if (f != null && !f.IsDisposed && f.Visible)
                        cnt++;
                }
                return cnt;
            }
        }

        public static void ShowHud(
            Chart chart,
            string areaName,
            string text,
            int durationMs = DEFAULT_DURATION_MS,
            float fontSize = DEFAULT_FONT_SIZE,
            Color? foreColor = null,
            int cellOffsetX = 0)
        {
            if (chart == null || chart.IsDisposed) return;
            if (string.IsNullOrWhiteSpace(areaName)) return;

            if (chart.InvokeRequired)
            {
                try
                {
                    chart.BeginInvoke(new Action(() =>
                        ShowHud(chart, areaName, text, durationMs, fontSize, foreColor)));
                }
                catch { }
                return;
            }

            if (chart.IsDisposed) return;

            if (durationMs <= 0) durationMs = DEFAULT_DURATION_MS;
            if (fontSize <= 1f) fontSize = DEFAULT_FONT_SIZE;

            ChartArea ca = null;
            try { ca = chart.ChartAreas.FirstOrDefault(a => a.Name == areaName); }
            catch { ca = null; }
            if (ca == null) return;

            Rectangle areaClientRect = GetChartAreaClientRect(chart, ca);
            if (areaClientRect.Width <= 0 || areaClientRect.Height <= 0)
                areaClientRect = chart.ClientRectangle;

            Rectangle areaScreenRect;
            try { areaScreenRect = chart.RectangleToScreen(areaClientRect); }
            catch { return; }

            string displayText = (text ?? "").Trim();
            if (displayText.StartsWith("SECTOR:", StringComparison.OrdinalIgnoreCase))
                displayText = displayText.Substring("SECTOR:".Length).Trim();

            string key = MakeKey(chart, areaName);
            var fc = foreColor ?? DEFAULT_FORE;

            Item item;
            AreaHudForm form;

            lock (_gate)
            {
                if (_items.TryGetValue(key, out item) && item != null && item.Form != null && !item.Form.IsDisposed)
                {
                    form = item.Form;
                }
                else
                {
                    if (item != null)
                    {
                        _items.Remove(key);
                        _fifo.Remove(key);
                    }

                    EnsureCapacity_NoLock();
                    form = Acquire_NoLock();

                    item = new Item
                    {
                        Key = key,
                        Chart = chart,
                        AreaName = areaName,
                        Form = form
                    };

                    _items[key] = item;
                    _fifo.Add(key);
                }
            }

            // area 중앙점
            Point center = new Point(
                areaScreenRect.Left + areaScreenRect.Width / 2,
                areaScreenRect.Top + areaScreenRect.Height / 2
            );

            form.SetFont("맑은 고딕", fontSize, FontStyle.Bold);

            // 만료시 딕셔너리에서 제거 + 풀 반환
            form.ShowCenteredAt(
                centerPoint: center,
                text: displayText,
                durationMs: durationMs,
                foreColor: fc,
                bgAlpha: BG_ALPHA,
                padding: PADDING,
                cornerRadius: CORNER_RADIUS,
                onExpired: () => RemoveByKey(key)
            );


            
        }

        public static void RemoveHud(Chart chart, string areaName)
        {
            if (chart == null || chart.IsDisposed) return;
            if (string.IsNullOrWhiteSpace(areaName)) return;

            if (chart.InvokeRequired)
            {
                try { chart.BeginInvoke(new Action(() => RemoveHud(chart, areaName))); }
                catch { }
                return;
            }

            RemoveByKey(MakeKey(chart, areaName));
        }

        public static void ClearAll()
        {
            List<string> keys;
            lock (_gate) { keys = _items.Keys.ToList(); }
            foreach (var k in keys) RemoveByKey(k);
        }

        // ---------------- internals ----------------

        private static string MakeKey(Chart chart, string areaName)
        {
            int h = 0;
            try { h = chart.Handle.ToInt32(); } catch { h = 0; }
            int rh = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(chart);
            return h.ToString() + ":" + rh.ToString() + ":" + areaName;
        }

        private static void EnsureCapacity_NoLock()
        {
            while (_fifo.Count >= MAX_HUD)
            {
                string oldestKey = _fifo[0];
                _fifo.RemoveAt(0);

                Item it;
                if (_items.TryGetValue(oldestKey, out it))
                {
                    _items.Remove(oldestKey);
                    Release_NoLock(it?.Form);
                }
            }
        }

        private static void RemoveByKey(string key)
        {
            Item it = null;
            lock (_gate)
            {
                if (_items.TryGetValue(key, out it))
                {
                    _items.Remove(key);
                    _fifo.Remove(key);
                }

                if (it != null && it.Form != null)
                    Release_NoLock(it.Form);
            }
        }

        private static AreaHudForm Acquire_NoLock()
        {
            while (_pool.Count > 0)
            {
                var f = _pool.Pop();
                if (f != null && !f.IsDisposed)
                    return f;
            }
            return new AreaHudForm();
        }

        private static void Release_NoLock(AreaHudForm form)
        {
            if (form == null) return;
            if (form.IsDisposed) return;

            try { form.Hide(); } catch { }
            form.ResetState();
            _pool.Push(form);
        }

        private static Rectangle GetChartAreaClientRect(Chart chart, ChartArea ca)
        {
            if (chart == null || chart.IsDisposed || ca == null)
                return Rectangle.Empty;

            ElementPosition p = ca.Position;
            if (p == null || p.Width <= 0 || p.Height <= 0)
                return chart.ClientRectangle;

            Rectangle cr = chart.ClientRectangle;

            int x = cr.Left + (int)Math.Round(cr.Width * p.X / 100f);
            int y = cr.Top + (int)Math.Round(cr.Height * p.Y / 100f);
            int w = (int)Math.Round(cr.Width * p.Width / 100f);
            int h = (int)Math.Round(cr.Height * p.Height / 100f);

            if (w < 1) w = 1;
            if (h < 1) h = 1;

            return new Rectangle(x, y, w, h);
        }

        private sealed class Item
        {
            public string Key;
            public Chart Chart;
            public string AreaName;
            public AreaHudForm Form;
        }

        private sealed class AreaHudForm : HudBaseForm
        {
            private Font _font;
            private Timer _expireTimer;
            private Action _onExpired;

            public AreaHudForm() : base()
            {
                _expireTimer = new Timer();
                _expireTimer.Tick += (s, e) =>
                {
                    _expireTimer.Stop();
                    try { Hide(); } catch { }
                    try { _onExpired?.Invoke(); } catch { }
                };
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { _expireTimer?.Stop(); } catch { }
                    try { _expireTimer?.Dispose(); } catch { }
                    _expireTimer = null;

                    try { _font?.Dispose(); } catch { }
                    _font = null;
                }
                base.Dispose(disposing);
            }

            public void SetFont(string family, float size, FontStyle style)
            {
                if (size <= 1f) size = DEFAULT_FONT_SIZE;

                if (_font != null)
                {
                    if (Math.Abs(_font.Size - size) < 0.01f &&
                        _font.Style == style &&
                        string.Equals(_font.FontFamily.Name, family, StringComparison.Ordinal))
                        return;

                    try { _font.Dispose(); } catch { }
                    _font = null;
                }

                _font = new Font(family, size, style);
            }

            public void ResetState()
            {
                _onExpired = null;
                try { _expireTimer.Stop(); } catch { }
            }

            public Point ShowCenteredAt(
    Point centerPoint,
    string text,
    int durationMs,
    Color foreColor,
    int bgAlpha,
    int padding,
    int cornerRadius,
    Action onExpired)
            {
                _onExpired = onExpired;

                var fontToUse = _font ?? new Font("맑은 고딕", DEFAULT_FONT_SIZE, FontStyle.Bold);

                Size size = this.MeasureHudSize(
                    text,
                    fontToUse,
                    padding,
                    new Size(800, 300)
                );

                int x = centerPoint.X - size.Width / 2;
                int y = centerPoint.Y - size.Height / 2;

                Rectangle wa = Screen.FromPoint(centerPoint).WorkingArea;

                x = Math.Max(wa.Left, Math.Min(wa.Right - size.Width, x));
                y = Math.Max(wa.Top, Math.Min(wa.Bottom - size.Height, y));

                Point finalLocation = new Point(x, y);

                ShowHudAt(
                    screenLocation: finalLocation,
                    text: text,
                    durationMs: durationMs,
                    font: fontToUse,
                    foreColor: foreColor,
                    bgAlpha: bgAlpha,
                    bgColor: null,
                    padding: padding,
                    cornerRadius: cornerRadius
                );

                try
                {
                    _expireTimer.Stop();
                    _expireTimer.Interval = Math.Max(1, durationMs);
                    _expireTimer.Start();
                }
                catch { }




                // 20260515
                this.TopMost = true;
                this.Show();
                this.BringToFront();



                return finalLocation;
            }
        }
    }
}