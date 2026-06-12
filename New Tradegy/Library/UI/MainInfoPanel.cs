using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using New_Tradegy.Library.Listeners;

namespace New_Tradegy.Library.UI
{
    public static class MainInfoPanel
    {
        private const string AreaName = "Main Info";

        // 기존 3줄
        private const string Anno1 = "MainInfo_NQ";
        private const string Anno2 = "MainInfo_KP";
        private const string Anno3 = "MainInfo_KQ";

        // 추가 2줄 (Z 표시)
        private const string AnnoZ_KP = "MainInfo_Z_KP";
        private const string AnnoZ_KQ = "MainInfo_Z_KQ";

        private const float CellW = 100f / 10f;
        private const float CellH = 100f / 3f;

        private const float BoxX = 2f * CellW;   // col 2
        private const float BoxY = 0f;           // row 0
        private const float BoxW = CellW;
        private const float BoxH = CellH;

        static bool _mainInfoCreated = false;

        public static void EnsureCreated()
        {
            var chart = g.ChartManager?.Chart1;
            if (chart == null) return;

            EnsureArea(chart);

            EnsureText(chart, Anno1, BoxX + 0.8f, BoxY + 2.5f);
            EnsureText(chart, Anno2, BoxX + 0.8f, BoxY + 8.5f);
            EnsureText(chart, Anno3, BoxX + 0.8f, BoxY + 14.5f);

            // 🔽 Z 줄 (안전 위치)
            EnsureText(chart, AnnoZ_KP, BoxX + 0.8f, BoxY + 20.5f);
            EnsureText(chart, AnnoZ_KQ, BoxX + 0.8f, BoxY + 26.0f);
        }

        public static void UpdateUpper(
            string g1, string g2, string g3,
            Color c1, Color c2, Color c3)
        {
            var chart = g.ChartManager?.Chart1;
            if (chart == null) return;

            EnsureCreated();
            _mainInfoCreated = true;

            Set(chart, Anno1, g1, c1);
            Set(chart, Anno2, g2, c2);
            Set(chart, Anno3, g3, c3);

            chart.Invalidate();
        }

        public static void UpdateZ()
        {
            var chart = g.ChartManager?.Chart1;
            if (chart == null) return;

            EnsureCreated();
            _mainInfoCreated = true;

            string kp = FormatZ("KP", g.KospiMinuteZ);
            string kq = FormatZ("KQ", g.KosdaqMinuteZ);

            Color kpColor = GetZColor(g.KospiMinuteZ);
            Color kqColor = GetZColor(g.KosdaqMinuteZ);

            Set(chart, AnnoZ_KP, kp, kpColor);
            Set(chart, AnnoZ_KQ, kq, kqColor);

            chart.Invalidate();
        }



     





        public static void Clear()
        {
            var chart = g.ChartManager?.Chart1;
            if (chart == null) return;

            Set(chart, Anno1, "", Color.Gray);
            Set(chart, Anno2, "", Color.Gray);
            Set(chart, Anno3, "", Color.Gray);

            Set(chart, AnnoZ_KP, "", Color.Gray);
            Set(chart, AnnoZ_KQ, "", Color.Gray);
        }

        private static string FormatZ(string tag, MinuteZEngine z)
        {
            if (z == null)
                return $"{tag}  0.0  0.0  0.0  0.0";

            return string.Format(
                "{0} {1,5:0.0} {2,5:0.0} {3,5:0.0} {4,5:0.0}",
                tag, z.Z1, z.Z3, z.Z6, z.Z10);
        }

        private static Color GetZColor(MinuteZEngine z)
        {
            if (z == null) return Color.DimGray;

            // 상승 강도
            if (z.Z3 >= 2.0 && z.Z6 >= 2.5)
                return Color.Red;

            // 하락 강도
            if (z.Z3 <= -2.0 && z.Z6 <= -2.5)
                return Color.Blue;

            // 약한 상승/하락
            if (z.Z3 > 0.5 && z.Z6 > 0.5)
                return Color.DarkRed;

            if (z.Z3 < -0.5 && z.Z6 < -0.5)
                return Color.DarkBlue;

            return Color.DimGray;
        }

        private static void EnsureArea(Chart chart)
        {
            ChartArea area;

            if (chart.ChartAreas.IndexOf(AreaName) >= 0)
                area = chart.ChartAreas[AreaName];
            else
            {
                area = new ChartArea(AreaName);
                chart.ChartAreas.Add(area);
            }

            area.Position = new ElementPosition(BoxX, BoxY, BoxW, BoxH);
            area.InnerPlotPosition = new ElementPosition(0, 0, 100, 100);

            area.BackColor = Color.Transparent;
            area.BorderColor = Color.Transparent;

            area.AxisX.Enabled = AxisEnabled.False;
            area.AxisY.Enabled = AxisEnabled.False;

            area.AxisX.LabelStyle.Enabled = false;
            area.AxisY.LabelStyle.Enabled = false;

            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.Enabled = false;

            area.Visible = true;
        }

        private static void EnsureText(Chart chart, string name, float x, float y)
        {
            var anno = chart.Annotations
                .FirstOrDefault(a => a.Name == name) as TextAnnotation;

            bool isNew = false;

            if (anno == null)
            {
                anno = new TextAnnotation();
                anno.Name = name;
                chart.Annotations.Add(anno);
                isNew = true;
            }

            anno.ClipToChartArea = AreaName;
            anno.IsSizeAlwaysRelative = true;

            anno.X = x;
            anno.Y = y;
            anno.Width = BoxW - 1.2f;
            anno.Height = 5.2f;

            // 새로 만들 때만 기본값 넣기
            if (isNew)
            {
                anno.Text = "";
                anno.ForeColor = Color.Gray;
                anno.Font = new Font("Consolas", 14, FontStyle.Bold);
                anno.BackColor = Color.Transparent;
                anno.LineColor = Color.Transparent;
                anno.Alignment = ContentAlignment.MiddleLeft;
                anno.Visible = true;
            }
        }
        private static void Set(Chart chart, string name, string text, Color color)
        {
            var anno = chart.Annotations
                .FirstOrDefault(a => a.Name == name) as TextAnnotation;

            if (anno == null) return;

            anno.Text = text ?? "";
            anno.ForeColor = color;
        }
    }
}