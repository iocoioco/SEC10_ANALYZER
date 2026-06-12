using New_Tradegy.Library.Models;
using System;
using System.Drawing;

namespace New_Tradegy.Library.Utils
{
    public static class ChartLayoutUtils
    {
        public static int ScreenWidth => g.ChartManager.Chart1.Width;
        public static int ScreenHeight => g.ChartManager.Chart1.Height;

        public static int ChartRows => g.nRow;
        public static int ChartCols => g.nCol;

        public static Size GetChartSize()
        {
            int width = ScreenWidth / ChartCols;
            int height = ScreenHeight / ChartRows;

            return new Size(width, height);
        }

        public static Point GetChartLocation(int row, int col, int xGap = 5, int yGap = 5)
        {
            var size = GetChartSize();
            int x = size.Width * col + xGap;
            int y = size.Height * row + yGap;
            return new Point(x, y);
        }

        public static Point GetBookBidLocation(int row, int col)
        {
            // BookBids appear to the right of chart area
            var baseLoc = GetChartLocation(row, col);
            return new Point(baseLoc.X + GetChartSize().Width + 10, baseLoc.Y);
        }

        public static (int row, int col) GetGridIndexFromPoint(Point p)
        {
            var size = GetChartSize();
            int col = p.X / size.Width;
            int row = p.Y / size.Height;
            return (row, col);
        }

        // ✅ 차트 그릴 구간(start/end) 결정
        // - test: g.Npts[0]~g.Npts[1] 기반 + end-1부터 유효 row(가격= x[k,1]) 역탐색
        // - shrink: end 기준으로 최근 NptsForShrinkDraw만
        // - live: start=0, end=nrow (단, 최소 유효성 체크)


        public static bool TryGetDrawRange(StockData data, out int start, out int end)
        {
            start = 0;
            end = 0;

            var api = data?.Api;
            var x = api?.x;
            if (x == null) return false;

            int nrow = api.nrow;
            if (nrow <= 1) return false;

            if (g.test)
            {
                // 1) 테스트 후보 범위 (EndExclusive)
                start = Math.Max(0, g.Npts[0]);
                if (start >= nrow) return false;

                end = Math.Min(g.Npts[1], nrow);
                if (end <= start) return false;

                // 2) 후보 범위 안에서 마지막 유효 row 찾기 (time != 0)
                int last = end - 1;
                while (last >= start && x[last, 0] == 0)
                    last--;

                if (last < start) return false;

                // 3) end는 항상 last+1 (EndExclusive)
                end = last + 1;

                // 4) shrink: end 기준 최근 N개, 단 테스트 start는 침범하지 않기
                if (data.Misc.ShrinkDraw)
                    start = Math.Max(start, end - g.NptsForShrinkDraw);

                return end > start;
            }
            else
            {
                // ===== Real =====
                start = 0;
                end = nrow;

                // 네 불변식 유지: 마지막 row는 항상 유효해야 함
                if (x[end - 1, 0] == 0) return false;

                // shrink: end 기준 최근 N개
                if (data.Misc.ShrinkDraw)
                    start = Math.Max(0, end - g.NptsForShrinkDraw);

                return end > start;
            }
        }

    }
}
