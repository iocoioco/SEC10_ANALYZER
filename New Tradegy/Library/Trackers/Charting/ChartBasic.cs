using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy.Library.Trackers
{
    internal class ChartBasic
    {
        public static void RelocateChartArea(ChartArea area, int row, int col, int nRow, int nCol)
        {
            float width = 100f / nCol;
            float height = 100f / nRow;

            area.Position.X = col * width;
            area.Position.Y = row * height;
            area.Position.Width = width;
            area.Position.Height = height;
        }

        public static void RemoveChartBlock(Chart chart, string stockName)
        {
            stockName = stockName?.Trim();

            // Remove related series (Name starts with stockName)
            var seriesToRemove = chart.Series
                .Where(s => s.ChartArea == stockName || s.Name.StartsWith(stockName + " "))
                .ToList();

            foreach (var s in seriesToRemove)
                chart.Series.Remove(s);

            // Remove chart area (Name == stockName)
            var area = chart.ChartAreas.FindByName(stockName);
            if (area != null)
                chart.ChartAreas.Remove(area);
        }

        public static ElementPosition CalculateInnerPlotPosition(float cellWidth, float cellHeight)
        {
            // 전체 영역 기준으로 상대적 여백 계산
            float horizontalMargin = 100f * 6f / cellWidth;   // 예: 좌우 각 6px → 전체에서 차지하는 %
            float verticalMargin = 100f * 5f / cellHeight;    // 예: 위아래 각 5px

            // clamp 범위 제한 (최소 0, 최대 20% 여백)
            horizontalMargin = Clamp(horizontalMargin, 2f, 15f);
            verticalMargin = Clamp(verticalMargin, 2f, 15f);

            float innerX = horizontalMargin;
            float innerY = verticalMargin;
            float innerWidth = 100f - 2 * horizontalMargin;
            float innerHeight = 100f - 2 * verticalMargin;

            return new ElementPosition(innerX, innerY, innerWidth, innerHeight);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }

    

}
