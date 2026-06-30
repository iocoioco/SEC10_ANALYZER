using MathNet.Numerics.LinearAlgebra.Factorization;
using New_Tradegy;
using New_Tradegy.Library;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.UI;
using New_Tradegy.Library.Utils;
using OpenQA.Selenium.BiDi.Modules.Script;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;


namespace New_Tradegy.Library.Core
{
    public class RankLogic
    {
        public static void RankSector(IReadOnlyList<StockData> sectors)
        {
            foreach (var s in sectors)
            {
                double m = s.Score.GetPct(ScoreKey.MinMoney_10M);
                double p = s.Score.GetPct(ScoreKey.MinPro_10M);
                double f = s.Score.GetPct(ScoreKey.MinFor_10M);
                double d = s.Score.GetPct(ScoreKey.MinDiff);

                double composite =
                      0.30 * m
                    + 0.70 * p
                    + 0.50 * d
                    + 0.00 * f;

                s.Score.Raw[ScoreKey.MinSum] = composite; // 또는 별도 key
            }

            var ordered = sectors
                .OrderByDescending(s => s.Score.GetRaw(ScoreKey.MinSum))
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
                ordered[i].Score.SectorRank = i + 1;
        }
             
        public static void RankGeneral(IEnumerable<StockData> passed)
        {
            var resultList = new List<(double value, string code)>();

            var specialGroupKeys = new HashSet<string> {"푀누", "종누", "닥올", "피올", "편차", "평균", "상순", "저순", "이평", "증순" };
            
            string mode = g.v.MainChartDisplayMode;
           

            string text = "this\nis\ntest\nfor\nmouse\nhud";


            if (specialGroupKeys.Contains(mode))
            {
                foreach (var data in g.StockRepo.AllGeneralStocks)
                {
                    var api = data.Api; if (api == null) continue;
                    var stat = data?.Statistics; if (stat == null) continue;
                    var post = data.Post; if (post == null) continue;
                    if (!TryGetLastRow(data, out int lastRow)) continue;

                    double value;
                    switch (mode)
                    {
                        case "푀누":
                            value = post.프누천 + post.외누천;
                            break;

                        case "종누":
                            value = post.종누천;
                            break;

                        case "피올":
                            if (stat.시장구분 != 'S') continue;
                            value = stat.시총;
                            break;

                        case "닥올":
                            if (stat.시장구분 != 'D') continue;
                            value = stat.시총;
                            break;

                        case "편차":
                            value = stat.일간변동편차;
                            break;

                        case "평균":
                            value = stat.일간변동평균;
                            break;

                        case "상순":
                            value = api.x[lastRow, 1];
                            break;

                        case "저순":
                            value = -api.x[lastRow, 1];
                            break;

                        case "이평":
                            value = data.Statistics.AvgDailyTurnover_10M;
                            break;

                        case "증순":
                            value = api.x[lastRow, 1] - api.x[lastRow - 1, 1];
                            break;

                        default:
                            continue;
                    }

                    if (IsBad(value)) continue;
                    resultList.Add((value, data.Stock));
                }

          
            }
            else
            {
                // ✅ passed는 가능하면 스냅샷을 넣어라 (g.PassedSnapshotStocks)
                var passedList = passed?.Where(d => d != null).ToList() ?? new List<StockData>();
          

                foreach (var data in passedList)
                {
                    if (!TryGetLastRow(data, out int lastRow)) continue;

                    var api = data.Api;
                    var post = data.Post;
                    if (post == null) continue;

                    double value;

                    switch (mode)
                    {
                       case "푀분":
                            if (api.분프로천?.Length > 0 && api.분외인천?.Length > 0)
                                value = api.분프로천[0] + api.분외인천[0];
                            else
                                continue;
                            break;

                        case "등합":
                        default:
                            {
                                double m = data.Score.GetPct(ScoreKey.MinMoney_10M);
                                double p = data.Score.GetPct(ScoreKey.MinPro_10M);
                                double f = data.Score.GetPct(ScoreKey.MinFor_10M);
                                double d = data.Score.GetPct(ScoreKey.MinDiff);

                                value =
                                    p  // 프분
                                   + 0.5 * d; // 배차

                                double cumPro = data.Score.GetPct(ScoreKey.CumPro_10M);

                                if (cumPro < 40) // 종누 < 40% 
                                    value *= 0.80;
                                break;
                            }

                        case "배차":
                            if (api.분배수차?.Length > 0) value = api.분배수차[0];
                            else continue;
                            break;

                        case "분거":
                            
                            value = ChartGeneral.CalcVolumePctInt(data, lastRow);
                     
                   
                            break;

                        
                    }

                    if (IsBad(value)) continue;
                    resultList.Add((value, data.Stock));
                }
            }

            // 빠르고 예측가능: List.Sort
            resultList.Sort((a, b) => b.value.CompareTo(a.value));

            lock (g.lockObject)
            {
                var ranking = g.StockManager?.StockRankingList;
                if (ranking == null) return;

                ranking.Clear();
                var seen = new HashSet<string>();

                foreach (var item in resultList)
                {
                    var stock = item.code;
                    if (!string.IsNullOrWhiteSpace(stock) && seen.Add(stock))
                        ranking.Add(stock);
                }

                int totalCount = g.StockRepo.AllGeneralStocks.Count;
                int rankCount = ranking.Count;

                string newValue = $"{rankCount}/{totalCount}";

                if (g.controlPane.GetCellValue(1, 0) != newValue)
                    g.controlPane.SetCellValue(1, 0, newValue);
            }
        }

        // 공통: lastRow 구하기
        static bool TryGetLastRow(StockData data, out int lastRow)
        {
            lastRow = -1;
            if (data == null) return false;

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return false;

            lastRow = end - 1;
            if (lastRow < 1) return false;

            var api = data.Api;
            if (api?.x == null) return false;
            if (api.x[lastRow, 0] == 0) return false; // hhmmss==0 방어

            return true;
        }

        static bool IsBad(double v) => double.IsNaN(v) || double.IsInfinity(v);

        static int MaxRiseWithinHorizon(int[,] x, int curRow, int priceCol, int horizon,
                                                            out int peakRow, out int curPrice, out int peakPrice)
        {
            int rows = x.GetLength(0);
            int last = Math.Min(curRow + horizon, rows - 1);

            curPrice = x[curRow, priceCol];
            peakPrice = curPrice;
            peakRow = curRow;

            int maxDiff = 0; // 상승 없으면 0 유지(하락만 있었던 경우)

            for (int r = curRow + 1; r <= last; r++)
            {
                int p = x[r, priceCol];
                if (p <= 0) continue;               // 결측/이상치 스킵(필요 없으면 제거)
                int diff = p - curPrice;            // 절대 상승폭
                if (diff > maxDiff)
                {
                    maxDiff = diff;
                    peakRow = r;
                    peakPrice = p;
                }
            }
            return maxDiff;
        }




        public static bool EvalInclusion(StockData data)
        {
            if (data == null) return false;

            string stock = data.Stock;
            var score = data.Score;
            var stat = data.Statistics;
            var post = data.Post;
            var api = data.Api;

            // 1) ETF/Index 제외
            if (string.IsNullOrEmpty(stock)) return false;
            if (g.StockManager.IndexList.Contains(stock)) return false;

            int now = int.Parse(DateTime.Now.ToString("HHmmss"));

            // 2) 누적모드 무조건 포함
            if (g.v.MainChartDisplayMode == "푀누" || g.v.MainChartDisplayMode == "종누")
                return true;

            // 3) 시초 5초 안정화: 거래액/호가/점수 컷 적용 안 함
            // 단, null/ETF/Index는 위에서 이미 제외
            if (wk.isWorkingHour() && now >= 90000 && now < 90005)
                return true;

            // 4) 유동성 컷 - 분당거래액
            if(api.분거래천[0] != 0)
            {
                if (api?.분거래천 == null || api.분거래천.Length == 0) return false;
                if (api.분거래천[0] < g.v.분당거래액이상_천만원) return false;
            }
            

            // 5) 장중 호가 유동성 컷
            if (wk.isWorkingHour() && !g.test)
            {
                if (post == null) return false;

                bool early = now >= 90000 && now < 90030;

                if (!early)
                {
                    if (post.매도호가거래액_백만원 + post.매수호가거래액_백만원
                        < 2 * g.v.호가거래액이상_백만원)
                        return false;
                }
            }

            // 6) 종누 거래액 컷
            if (post == null) return false;
            if (g.v.종가기준추정거래액이상_천만원 > (int)post.종누천) 
                return false;

            // 7) 점수 기반 필터
            if (api == null) return false;
            if (g.v.푀플 == 1 || g.v.배플 == 1)
            {
                int minFo = 1;
                int minBa = 0;

                if (api.분프로천[0] + api.분외인천[0] < minFo ||
                    api.분배수차[0] < minBa)
                    return false;
            }

            // 8) 편차 컷
            if (stat == null) return false;
            if (stat.일간변동편차 < g.v.편차이상) 
                return false;

            // 9) 시총 컷
            if (g.v.시총이상 >= 0 && stat.시총 < g.v.시총이상 - 0.01)
                return false;

            return true;
        }
    }
}


