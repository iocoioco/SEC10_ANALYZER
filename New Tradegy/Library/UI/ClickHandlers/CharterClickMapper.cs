using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms;



namespace New_Tradegy.Library.UI.ChartClickHandlers
{
    internal class ChartClickMapper
    {
        public static string CoordinateMapping(
    Chart chart, int nRow, int nCol, List<string> displayList,
    MouseEventArgs e, ref string selection, ref int cellX, ref int cellY)
        {
            double x_max = chart.Bounds.Width;
            double y_max = chart.Bounds.Height;

            // Normalize to 0–100 coordinates
            double norm_x = Math.Max(0, Math.Min(100, (double)e.X / x_max * 100.0));
            double norm_y = Math.Max(0, Math.Min(100, (double)e.Y / y_max * 100.0));

            // Base cell size
            double cellWidth = 100.0 / nCol;
            double cellHeight = 100.0 / nRow;

            // Special case for chart1 first column
            if (chart.Name == "chart1" && norm_x <= 10)
                cellHeight = 100.0 / 2.0;

            // Determine which cell was clicked
            int rawX = (int)(norm_x / cellWidth);
            int rawY = (int)(norm_y / cellHeight);
            rawX = Math.Min(rawX, nCol - 1);
            rawY = Math.Min(rawY, nRow - 1);

            cellX = rawX;
            cellY = rawY;

            // Determine 3x3 zone within cell (visual layout)
            double pctX = (norm_x % cellWidth) / cellWidth * 100.0;
            double pctY = (norm_y % cellHeight) / cellHeight * 100.0;

            int zoneX = pctX > 66.66 ? 0 : pctX > 33.33 ? 1 : 2;
            int zoneY = pctY < 33.33 ? 0 : pctY < 66.66 ? 1 : 2;

            int[,] zoneMap = new int[3, 3] {
        { 1, 2, 3 },  // top row: left, center, right
        { 4, 5, 6 },  // middle row
        { 7, 8, 9 }   // bottom row
    };

            int finalZone = zoneMap[zoneY, zoneX];

            // Set left/right click + zone number
            selection = (e.Button == MouseButtons.Left ? "l" : "r") + finalZone.ToString();

            // Resolve clicked stock
            string clickedStock = null;

            if (chart.Name == "chart1")
            {
                if (g.q == "h&s")
                {
                    clickedStock = g.clickedStock;
                }
                else if (cellX == 0)
                {
                    clickedStock = (cellY == 0) ? displayList[0] : displayList[1];
                }
                //else if (cellX == 1)
                //{
                //    clickedStock = (cellY == 0) ? g.StockManager.IndexList[1] : g.StockManager.IndexList[3];
                //}
                else if (cellX >= 2)
                {
                    int seq = (cellX - 1) * nRow + cellY - 1;
                    if (seq >= 0 && seq < displayList.Count)
                        clickedStock = displayList[seq];
                }
            }
            else // chart2
            {
                if (g.q == "h&s")
                {
                    clickedStock = g.clickedStock;
                }
                else
                {
                    int seq = cellX * nRow + cellY;
                    if (seq >= 0 && seq < displayList.Count)
                        clickedStock = displayList[seq];
                }
            }

            return clickedStock;
        }


        

    }
}
