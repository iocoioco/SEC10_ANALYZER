using New_Tradegy.Library;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#region 날짜 폴더 유틸
static class DateDirHelper
{
    /// <summary>
    /// 주어진 기준 날짜에서 이전/다음 날짜를 찾아 반환.
    /// dir = -1 (이전), +1 (다음)
    /// </summary>
    public static int GetAdjacentDate(int dateInt, int dir, string basePath)
    {
        var dates = Directory.EnumerateDirectories(basePath)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n) && n.Length == 8 && n.All(char.IsDigit))
            .Select(int.Parse)
            .OrderBy(d => d)
            .ToList();

        if (dates.Count == 0 || dir == 0) return dateInt;

        int idx = dates.BinarySearch(dateInt);

        if (dir > 0) // 다음 날짜
        {
            int i = (idx >= 0) ? idx + 1 : ~idx;
            return (i < dates.Count) ? dates[i] : dateInt;
        }
        else // 이전 날짜
        {
            int i = (idx >= 0) ? idx - 1 : (~idx - 1);
            return (i >= 0) ? dates[i] : dateInt;
        }
    }
}
#endregion

#region 데이터 로더
static class DataLoader
{
    // 종목 데이터 파일 규칙 (예: YYYYMMDD\삼성전자.txt)
    private static string GetStockDataPath(string dateDir, string stock)
        => Path.Combine(dateDir, $"{stock}.txt");

    private static bool ExistsInDateDir(string dateDir, string stock)
        => File.Exists(GetStockDataPath(dateDir, stock));

    /// <summary>
    /// 현재 g.date 에 해당하는 디렉토리에서 Repo 종목만 읽어들이기.
    /// 디렉토리에 파일이 없으면 스킵.
    /// </summary>
    public static void ReadOrSetStocks()
    {
        string path = Path.Combine(@"C:\BJS\분", g.date.ToString());

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        foreach (var t in g.StockRepo.AllDatas
                .Where(d => d != null &&
                !string.IsNullOrWhiteSpace(d.Stock) &&
                (d.Kind == StockData.InstrumentKind.Stock ||
                 d.Kind == StockData.InstrumentKind.Index)))
        {
            // 날짜 폴더에 해당 종목 데이터가 없으면 스킵
            if (!ExistsInDateDir(path, t.Stock))
                continue;

            FileInStockData.LoadStockData(t, path);
        }
        // CompositeZ / RankingList / 차트 갱신은 AfterLoad 쪽에서 처리
    }
    

}
#endregion

#region 날짜 네비게이터
static class DateNavigator
{
    public static string BasePath = @"C:\BJS\분"; // 날짜 폴더 루트
    public static Action AfterLoad = null;        // 차트/패널 갱신 콜백

    private static int? _originDate;              // 탐색 시작 날짜 저장

    private static void EnsureOriginCaptured()
    {
        if (_originDate == null) _originDate = g.date;
    }

    // < : 이전 날짜
    public static void GoPrevDay()
    {
        EnsureOriginCaptured();
        int next = DateDirHelper.GetAdjacentDate(g.date, -1, BasePath);
        if (next != g.date)
        {
            g.date = next;
            int month = g.date % 1000 / 100;
            int day = g.date % 100;
            g.controlPane.SetCellValue(0, 0, $"{month}/{day}");
            DataLoader.ReadOrSetStocks();
        }
    }

    // > : 다음 날짜
    public static void GoNextDay()
    {
        EnsureOriginCaptured();
        int next = DateDirHelper.GetAdjacentDate(g.date, +1, BasePath);
        if (next != g.date)
        {
            g.date = next;
            int month = g.date % 1000 / 100;
            int day = g.date % 100;
            g.controlPane.SetCellValue(0, 0, $"{month}/{day}");
            DataLoader.ReadOrSetStocks();
        }
    }

    // = : 원래 날짜로 복귀
    public static void GoBackToOrigin()
    {
        if (_originDate != null)
        {
            g.date = _originDate.Value;
            int month = g.date % 1000 / 100;
            int day = g.date % 100;
            g.controlPane.SetCellValue(0, 0, $"{month}/{day}");
            _originDate = null;
            DataLoader.ReadOrSetStocks();
        }
    }
}
#endregion
