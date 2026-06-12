namespace New_Tradegy.Library.Utils
{
    public static class StockUtils
    {
        public static bool IsLeverage(string stock)
        {
            return stock.Contains("레버리지") && stock.Contains("KODEX");
        }

        public static bool IsInverse(string stock)
        {
            return stock.Contains("인버스") && stock.Contains("KODEX");
        }

        public static bool IsIndex(string stock)
        {
            return IsLeverage(stock) || IsInverse(stock);
        }

        // not correct
        //public static bool IsKospi(string stock)
        //{
        //    return g.StockManager.IndexList.Contains(stock) || stock.StartsWith("KOSPI");
        //}

        //public static bool IsKosdaq(string stock)
        //{
        //    return g.StockManager.IndexList.Contains(stock) || stock.StartsWith("KOSDAQ");
        //}

        public static string StripETFPrefix(string stock)
        {
            if (stock.StartsWith("KODEX "))
                return stock.Substring(6);
            return stock;
        }

        public static string FormatName(string name)
        {
            if (name.Length > 10)
                return name.Substring(0, 9) + "…";
            return name;
        }

        public static bool IsPoliticalTheme(string name)
        {
            return name.Contains("상지") || name.Contains("이재명") || name.Contains("KH");
        }
    }
}
