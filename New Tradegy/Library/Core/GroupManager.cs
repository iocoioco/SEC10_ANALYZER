using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace New_Tradegy.Library.Core
{
    public class GroupManager
    {
        // Naming Convention
        // Class/Struct/Enum/Property/Public PascalCase
        // Method PascalCase
        // Field(private)	_camelCase
        // Local variable/Parameter camelCase
        private List<GroupData> _groups;
        public List<GroupData> Groups => _groups;
        public List<GroupData> GroupRankingList { get; private set; } = new List<GroupData>();

        public List<string> GetTopStocksFromTopGroups(int groupLimit = 5, int stockPerGroup = 3, List<string> existing = null)
        {
            var result = new List<string>();
            if (existing == null)
                existing = new List<string>();

            int count = Math.Min(groupLimit, GroupRankingList.Count);

            for (int i = 0; i < count; i++)
            {
                var group = GroupRankingList[i];
                int added = 0;
               
                foreach (var stock in group.Stocks)
                {
                    if (!existing.Contains(stock))
                    {
                        result.Add(stock);
                        added++;
                    }
                    else
                    {
                        result.Add(""); // placeholder for alignment
                    }

                    if (added == stockPerGroup)
                        break;
                }

                // Fill with empty strings if fewer than required stocks
                while (added++ < stockPerGroup)
                {
                    result.Add("");
                }
            }

            return result;
        }

        public GroupManager()
        {
            _groups = GroupRepository.LoadGroups();
        }

        //public void Save()
        //{
        //    GroupRepository.SaveGroups(_groups);
        //}

        public List<GroupData> GetAll() => _groups;

        public static GroupData FindByTitle(GroupManager gm, string title)
        {
            return gm?._groups?.FirstOrDefault(x => x.Title == title);
        }

        public List<string> GetStocksByTitle(string title, List<string> existing)
        {
            var result = new List<string>();
            var group = FindByTitle(this, title);
            if (group != null)
            {
                foreach (var stock in group.Stocks)
                {
                    if (!existing.Contains(stock))
                        result.Add(stock);
                }
            }
            return result;
        }

        public void AddGroup(GroupData group)
        {
            if (!_groups.Any(g => g.Title == group.Title))
                _groups.Add(group);
        }

        public void ReplaceGroups(List<GroupData> newGroups)
        {
            _groups = newGroups;
        }

        public int Count => _groups.Count;

        public void SortByDescending(Func<GroupData, double> selector)
        {
            GroupRankingList = _groups.OrderByDescending(selector).ToList();
        }

        public void OrderBy(Func<GroupData, double> selector)
        {
            GroupRankingList = _groups.OrderBy(selector).ToList();

        }
        public GroupData FindGroupByStock(string stockName) /////
        {
            return _groups.FirstOrDefault(g => g.Stocks.Contains(stockName));
        }

        public static void gen_oGL_data()
        {
            var groupList = new List<GroupData>();

            // Reset all oGL_sequence_id tags (stocks only)
            foreach (var data in g.StockRepo.Stocks())
                data.Misc.oGL_sequence_id = -1;

            int groupIndex = 0;

            foreach (var groupData in g.GroupManager.Groups)
            {
                if (groupData?.Stocks == null || groupData.Stocks.Count < 2)
                    continue;

                var items = new List<Tuple<double, string>>();

                foreach (var stockName in groupData.Stocks)
                {
                    if (string.IsNullOrWhiteSpace(stockName))
                        continue;

                    var data = g.StockRepo.TryGetDataOrNull(stockName);
                    if (data == null)
                    {
                        // repo에 없으면 그룹에서 제외
                        // System.Diagnostics.Debug.WriteLine($"[gen_oGL_data] drop(no repo): {groupData.Title} / {stockName}");
                        continue;
                    }

                    data.Misc.oGL_sequence_id = groupIndex;

                    double mcap = 0;
                    if (data.Statistics != null)
                        mcap = data.Statistics.시총;

                    items.Add(Tuple.Create(mcap, data.Stock));
                }

                // repo에 있는 종목만 남기고 시총 내림차순 정렬
                var sorted = items
                    .OrderByDescending(t => t.Item1)
                    .Select(t => t.Item2)
                    .Distinct() // 혹시 상관.txt 중복 방어 (원하면 제거 가능)
                    .ToList();

                if (sorted.Count < 2)
                    continue;

                groupList.Add(new GroupData(groupData.Title) { Stocks = sorted });
                groupIndex++;
            }

            g.GroupManager.ReplaceGroups(groupList);

           

            SaveCorrelationFile(groupList);
        }



        private static readonly string CorrPath = @"C:\BJS\data work\상관.txt";

        public static void SaveCorrelationFile(List<GroupData> groups)
        {
            if (groups == null || groups.Count == 0)
                return;

            using (var sw = new StreamWriter(CorrPath, false, Encoding.Default))
            {
                foreach (var grp in groups)
                {
                    if (grp?.Stocks == null || grp.Stocks.Count < 2)
                        continue;

                    // 그룹 제목
                    sw.WriteLine($"[{grp.Title}]");

                    // 종목 목록 (공백 구분)
                    sw.WriteLine(string.Join(" ", grp.Stocks));

                    sw.WriteLine(); // 그룹 구분용 빈 줄
                }
            }
        }


    }
}
