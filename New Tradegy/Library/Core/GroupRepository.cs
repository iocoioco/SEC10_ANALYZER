using New_Tradegy.Library.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Core
{
    public static class GroupRepository
    {

        private static readonly string CorrPath = @"C:\BJS\data work\상관.txt";

        public static List<GroupData> LoadGroups()
        {
            var groups = new List<GroupData>();

            if (!File.Exists(CorrPath))
                return groups;

            var lines = File.ReadAllLines(CorrPath, Encoding.UTF8);

            GroupData current = null;
            var repo = g.StockRepo;

            foreach (var raw in lines)
            {
                var line = (raw ?? "").Trim();
                if (line.Length == 0) continue;

                // 그룹 시작: // 제목
                if (line.StartsWith("//"))
                {
                    // 이전 그룹 확정
                    if (current != null && current.Stocks.Count >= 2)
                        groups.Add(current);

                    var title = line.Substring(2).Trim();
                    var data = g.StockRepo.TryGetDataOrNull(title);
                    if (data != null && !title.EndsWith("_g"))
                    {
                        title = title + "G";
                    }
                    current = new GroupData(title);
                    continue;
                }
                if (current == null) continue; // 제목 나오기 전 내용 무시

                
                

                // 인라인 주석 제거(있으면)
                int cidx = line.IndexOf("//", StringComparison.Ordinal);
                if (cidx >= 0) line = line.Substring(0, cidx).Trim();
                if (line.Length == 0) continue;

                // 토큰 파싱 (공백/탭 혼용)
                var tokens = line
                    .Split(new[] { ' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Replace('_', ' ').Trim());

                foreach (var stock in tokens)
                {
                    if (stock.Length == 0) continue;

                    // ✅ repo에 없는 종목은 자동 제외
                    if (repo.TryGetDataOrNull(stock) == null) continue;

                    if (!current.Stocks.Contains(stock))
                        current.Stocks.Add(stock);
                }
            }

            // 마지막 그룹 확정    
            if (current != null && current.Stocks.Count >= 2)
                groups.Add(current);


            // 🔧 그룹 내 종목을 AvgDailyTurnover_10M 기준 내림차순 정렬 후 15개만 유지
            foreach (var grp in groups)
            {
                grp.Stocks = grp.Stocks
                    .Select(s => new
                    {
                        Stock = s,
                        Avg = g.StockRepo.TryGetDataOrNull(s)?.Statistics?.AvgDailyTurnover_10M ?? 0
                    })
                    .OrderByDescending(x => x.Avg)
                    .Take(15)
                    .Select(x => x.Stock)
                    .ToList();
            }

            return groups;
        }



        public static void EnsureDirectoryForFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(dir)) return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }



        //public static void SaveGroups(List<GroupData> groups)
        //{
        //    using (StreamWriter writer = new StreamWriter("상관.txt"))
        //    {
        //        foreach (var group in groups)
        //        {
        //            writer.WriteLine($"// {group.Title}");
        //            foreach (var chunk in ChunkList(group.Stocks, 5)) // optional: split long lines
        //            {
        //                writer.WriteLine(string.Join(" ", chunk));
        //            }
        //            writer.WriteLine();
        //        }
        //    }
        //}

        //public static void SaveFilteredGroups(List<GroupData> groups, string outputPath)
        //{
        //    using (StreamWriter writer = new StreamWriter(outputPath, false, Encoding.UTF8))
        //    {
        //        foreach (var group in groups)
        //        {
        //            writer.WriteLine($"// {group.Title}");

        //            foreach (var chunk in ChunkList(group.Stocks, 5)) // split long lines
        //            {
        //                var encoded = chunk.Select(name => name.Replace(' ', '_'));
        //                writer.WriteLine(string.Join(" ", encoded));
        //            }

        //            writer.WriteLine(); // empty line between groups
        //        }
        //    }
        //}

        //private static List<List<string>> ChunkList(List<string> list, int chunkSize)
        //{
        //    var chunks = new List<List<string>>();
        //    for (int i = 0; i < list.Count; i += chunkSize)
        //    {
        //        chunks.Add(list.GetRange(i, Math.Min(chunkSize, list.Count - i)));
        //    }
        //    return chunks;
        //}
    }
}
