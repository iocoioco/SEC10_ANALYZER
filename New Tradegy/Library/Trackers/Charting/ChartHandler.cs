using OpenQA.Selenium.BiDi.Modules.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy.Library.Trackers
{

    //Low-level chart utility methods, e.g.:
    //ClearSeriesAndAnnotations()
    //ChartAreaExists()
    //AddSeries()
    //GetChartAreaByName()

    public class ChartHandler
    {
        private Chart _chart;

        public ChartHandler(Chart chart) // chart1 and chart2 defined
        {
            _chart = chart;
        }

        public void Clear()
        {
            _chart.Series.Clear();
            _chart.ChartAreas.Clear();
            _chart.Annotations.Clear();
        }

        public static bool ChartAreaExists(Chart chart, string stock) // can be moved to a utility class
        {
            return chart.ChartAreas.IndexOf(stock) >= 0;
        }

        public static void SeriesInfomation(Series t, ref string chartAreaName, ref string Stock, 
                                            ref int ColumnIndex, ref int seriesEndPoint)
        {
            // Get the last occurrence of the delimiter ' '
            string[] parts = t.Name.Split(' ');
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("Invalid format in t.Name. Expected format: <StockName> <Number>");
            }

            // Extract stock name and number
            Stock = string.Join(" ", parts.Take(parts.Length - 1)); // Join all parts except the last as stock name
            chartAreaName = t.ChartArea;

            ColumnIndex = int.Parse(parts[parts.Length - 1]); // Parse the last part as an integer

            seriesEndPoint = t.Points.Count - 1;

        }
        // You can add more methods later, like UpdateChart(), DrawLine(), etc.
    }

}
