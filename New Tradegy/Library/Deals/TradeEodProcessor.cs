using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class TradeEodProcessor
{
    // ===== 경로 설정 =====
    private static readonly string RootDir = @"C:\BJS\Z Log\매수매도체결내역";
    private static readonly string DoneDir = Path.Combine(RootDir, "_done");

    // 날짜별로 만들지 않는 "누적 요약 파일"
    private static readonly string SummaryAppendFile = @"C:\BJS\Z Log\매수매도체결요약_누적.txt";

    private static readonly object _lock = new object();

    // ===== KST 시간 =====
    private static DateTime GetKstNow()
    {
        try
        {
            var kst = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kst);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    // ===== 다음 "거래일" 계산: 토요일만 건너뜀 (일요일 고려 불필요) =====
    private static DateTime NextTradingDay(DateTime d)
    {
        DateTime n = d.Date.AddDays(1);          // 다음날
        if (n.DayOfWeek == DayOfWeek.Saturday)   // 토요일이면 월요일로
            n = n.AddDays(2);
        return n;
    }

    // ===== 외부에서 호출할 메인 엔트리 =====
    // - 15:30 이후 프로그램 시작 시 1회 호출하면 됨
    public static void RunEodIfAfter1530()
    {
        var now = GetKstNow();
        var cutoff = now.Date.AddHours(15).AddMinutes(30);
        if (now < cutoff) return;

        RunEodProcessAllPending();
    }

    // ===== 남아있는 yyyyMMdd.txt 전부 처리 =====
    // 장 종료 후 자동 실행
    public static void RunEodProcessAllPending()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(DoneDir);

            // yyyyMMdd.txt만 대상으로 (이미 처리된 _done 하위 폴더 제외), RootDir 바로 아래의 .txt 파일만 대상
            var files = Directory.EnumerateFiles(RootDir, "*.txt", SearchOption.TopDirectoryOnly)
                                 .Where(f =>
                                 {
                                     var name = Path.GetFileName(f);
                                     // 요약 누적파일이 RootDir 밖에 있으면 상관없지만,
                                     // 혹시 RootDir에 같이 있으면 제외
                                     if (string.Equals(f, SummaryAppendFile, StringComparison.OrdinalIgnoreCase))
                                         return false;

                                     // 파일명이 정확히 8자리 날짜 + .txt 인 것만
                                     if (name.Length != 12) return false; // "yyyyMMdd.txt" = 8 + 4
                                     if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return false;

                                     string d8 = name.Substring(0, 8);
                                     return d8.All(char.IsDigit);
                                 })
                                 .OrderBy(f => f) // 날짜순
                                 .ToList();

            foreach (var file in files)
            {
                ProcessOneDailyFile(file);
            }
        }
    }

    // ===== 하루 파일 1개 처리 =====
    private static void ProcessOneDailyFile(string dailyPath)
    {
        // 파일 비어있으면 done으로 이동
        var lines = File.ReadAllLines(dailyPath);
        if (lines.Length == 0)
        {
            MoveToDone(dailyPath);
            return;
        }

        // 파일명에서 날짜 파싱 (yyyyMMdd.txt)
        string ymd = Path.GetFileNameWithoutExtension(dailyPath);
        if (!DateTime.TryParseExact(ymd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
        {
            // 날짜 파일이 아니면 건너뜀
            return;
        }

        // 1) 원본 라인 -> Fill 목록 파싱
        var fills = new List<Fill>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("#")) continue; // 마커/주석

            if (TryParseFillLine(line, out var f))
                fills.Add(f);
        }

        if (fills.Count == 0)
        {
            MoveToDone(dailyPath);
            return;
        }

        // 2) 요약(B): 종목별 매수/매도 합산 + 실현손익(이월 포함 가능하도록 "보유 평단"을 적용)
        //    - CARRY 라인(전일 잔량)도 매수로 들어오므로 자연스럽게 다음날 기준이 됨
        var perStock = BuildDailySummaryByStock(fills);

        // 3) 누적 요약 파일에 append
        AppendSummaryBlock(fileDate, perStock);

        // 4) 다음날 파일에 잔량(CARRY) 심기 (토요일만 스킵)
        var endPos = ComputeEndPositions(perStock);
        if (endPos.Count > 0)
        {
            var nextDay = NextTradingDay(fileDate);
            PrependCarryToNextDayFile(fileDate, nextDay, endPos);
        }

        // 5) 처리 완료: 원본 파일 이동(_done)
        MoveToDone(dailyPath);
    }

    // ===== 요약 구조 =====
    private sealed class StockSummary
    {
        public string Stock = "";
        public long BuyQty;
        public long BuyAmount;   // Σ(price*qty)
        public long SellQty;
        public long SellAmount;

        // "보유/이월 계산용" 평단 재고
        public long PosQty;
        public long PosCostSum;  // 평단*보유수량 누적

        public int BuyAvg => BuyQty <= 0 ? 0 : (int)Math.Round((double)BuyAmount / BuyQty);
        public int SellAvg => SellQty <= 0 ? 0 : (int)Math.Round((double)SellAmount / SellQty);

        // 실현손익(평단 재고 방식) 결과
        public long RealizedPnl;
    }

    // ===== 하루 요약 만들기 (CARRY 포함: 보유평단 기반으로 실현손익 계산) =====
    private static Dictionary<string, StockSummary> BuildDailySummaryByStock(List<Fill> fills)
    {
        var dict = new Dictionary<string, StockSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in fills.OrderBy(x => x.TimeKst))
        {
            if (!dict.TryGetValue(f.Code, out var s))
                dict[f.Code] = s = new StockSummary { Stock = f.Code };

            if (f.Side == Side.Buy)
            {
                s.BuyQty += f.Qty;
                s.BuyAmount += (long)f.Price * f.Qty;

                // 재고 반영(보유평단)
                s.PosQty += f.Qty;
                s.PosCostSum += (long)f.Price * f.Qty;
            }
            else // Sell
            {
                s.SellQty += f.Qty;
                s.SellAmount += (long)f.Price * f.Qty;

                // 실현손익: "현재 보유 평단"으로 청산분 계산
                long sellQty = Math.Min(s.PosQty, f.Qty);
                if (sellQty > 0)
                {
                    int avg = s.PosQty <= 0 ? 0 : (int)Math.Round((double)s.PosCostSum / s.PosQty);
                    // 실현손익 += (매도가 - 보유평단) * 청산수량
                    s.RealizedPnl += (long)(f.Price - avg) * sellQty;

                    // 재고 감소
                    s.PosQty -= sellQty;
                    s.PosCostSum -= (long)avg * sellQty;
                }
                // 보유보다 더 판 경우는(공매도 등) 여기서는 무시/경고로 남길 수도 있음
            }
        }

        return dict;
    }

    // ===== 장 마감 후 잔량을 다음날로 넘기기 위한 End Position 계산 =====
    // perStock의 PosQty/PosCostSum이 이미 "마지막 보유"를 들고 있음
    private static Dictionary<string, (long qty, int avgPrice)> ComputeEndPositions(Dictionary<string, StockSummary> perStock)
    {
        var endPos = new Dictionary<string, (long qty, int avgPrice)>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in perStock)
        {
            var s = kv.Value;
            if (s.PosQty <= 0) continue;

            int avg = s.PosQty <= 0 ? 0 : (int)Math.Round((double)s.PosCostSum / s.PosQty);
            if (avg <= 0) continue;

            endPos[s.Stock] = (s.PosQty, avg);
        }

        return endPos;
    }

    // ===== 누적 요약 파일 Append (날짜별 파일 X, 하나에 계속 쌓기) =====
    private static void AppendSummaryBlock(DateTime date, Dictionary<string, StockSummary> perStock)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{date:yyyy MM dd}");

        long daySum = 0;

        // 출력: 종목  매수 평균가  매수수량 / 매도 평균가 매도수량  손익
        foreach (var s in perStock.Values.OrderBy(x => x.Stock))
        {
            // 매수/매도 모두 없는 건 스킵
            if (s.BuyQty == 0 && s.SellQty == 0) continue;

            // 네가 원한 “합쳐서 평균가 매수/매도”
            if (s.BuyQty > 0)
                sb.AppendLine($"{s.Stock}\t매수\t{s.BuyAvg:N0}\t{s.BuyQty:N0}");

            if (s.SellQty > 0)
                sb.AppendLine($"{s.Stock}\t매도\t{s.SellAvg:N0}\t{s.SellQty:N0}\t{s.RealizedPnl:+#,0;-#,0;0}");

            daySum += s.RealizedPnl;
        }

        sb.AppendLine($"계\t\t\t\t{daySum:+#,0;-#,0;0}");
        sb.AppendLine(new string('-', 48));
        sb.AppendLine();

        var dir = Path.GetDirectoryName(SummaryAppendFile);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }


        File.AppendAllText(SummaryAppendFile, sb.ToString(), Encoding.UTF8);
    }

    // ===== 다음날 파일 맨 앞에 CARRY 라인 삽입 (중복 방지 마커 포함) =====
    private static void PrependCarryToNextDayFile(DateTime todayDate, DateTime nextDay, Dictionary<string, (long qty, int avgPrice)> endPos)
    {
        string nextFile = Path.Combine(RootDir, nextDay.ToString("yyyyMMdd") + ".txt");
        Directory.CreateDirectory(RootDir);

        // 중복 방지 마커
        string marker = $"#CARRYOVER_APPLIED:{todayDate:yyyyMMdd}";

        // 이미 들어가 있으면 재삽입 방지
        if (File.Exists(nextFile))
        {
            var head = File.ReadLines(nextFile).Take(40);
            if (head.Any(l => l.Contains(marker)))
                return;
        }

        // CARRY 라인은 다음날 09:00로 기록(가짜 체결)
        DateTime carryTime = nextDay.Date.AddHours(9);

        var prepend = new List<string>
        {
            marker
        };

        foreach (var kv in endPos.OrderBy(k => k.Key))
        {
            string code = kv.Key;
            long qty = kv.Value.qty;
            int price = kv.Value.avgPrice;

            // 네 원본 포맷과 동일하게 저장
            // 시간,tag,orderId=...,code=...,qty=...,price=...,exchTime=
            prepend.Add($"{carryTime:yyyy-MM-dd HH:mm:ss.fff},CARRY,orderId=0,code={code},qty={qty},price={price},exchTime=");
        }

        if (File.Exists(nextFile))
        {
            var old = File.ReadAllLines(nextFile);
            File.WriteAllLines(nextFile, prepend.Concat(old));
        }
        else
        {
            File.WriteAllLines(nextFile, prepend);
        }
    }

    private static void MoveToDone(string path)
    {
        Directory.CreateDirectory(DoneDir);

        string name = Path.GetFileName(path);
        string dest = Path.Combine(DoneDir, name);

        // 이미 있으면 덮어쓰기 위해 삭제
        if (File.Exists(dest))
            File.Delete(dest);

        File.Move(path, dest);
    }

    // ===== 원본 라인 파싱 =====
    // 기대 포맷(예):
    // 2025-01-08 10:11:12.123,EXEC,orderId=123,code=005930,qty=20,price=65400,exchTime=101112
    private static bool TryParseFillLine(string line, out Fill f)
    {
        f = null;

        // 콤마 기준으로 나누고, 뒤 key=value를 읽는 방식
        var parts = line.Split(',');
        if (parts.Length < 2) return false;

        // 1) 시간
        if (!DateTime.TryParseExact(parts[0].Trim(), "yyyy-MM-dd HH:mm:ss.fff",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return false;

        // 2) tag (EXEC / CARRY 등) - side는 key/value에서 결정(아래)
        string tag = parts[1].Trim();

        // 3) key=value 파싱
        string code = "";
        long qty = 0;
        int price = 0;
        string sideStr = ""; // "매수"/"매도"가 원본에 없다면 tag로 판단하거나, 외부에서 넣어도 됨

        for (int i = 2; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            int eq = p.IndexOf('=');
            if (eq <= 0) continue;

            string key = p.Substring(0, eq).Trim();
            string val = p.Substring(eq + 1).Trim();

            if (key.Equals("code", StringComparison.OrdinalIgnoreCase)) code = val;
            else if (key.Equals("qty", StringComparison.OrdinalIgnoreCase) && long.TryParse(val, out var q)) qty = q;
            else if (key.Equals("price", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var pr)) price = pr;
            else if (key.Equals("side", StringComparison.OrdinalIgnoreCase)) sideStr = val; // 옵션
        }

        if (string.IsNullOrWhiteSpace(code) || qty <= 0 || price <= 0)
            return false;

        // side 결정 규칙:
        // - 원본에 side=매수/매도 넣고 있으면 그걸 사용
        // - 아니면 tag가 EXEC일 때는 "원래 체결 이벤트에서 매수/매도 여부를 같이 저장"하는 게 가장 확실함
        //   (아래 "삽입 위치"에서 side=를 추가하는 방식 추천)
        var side = Side.Unknown;
        if (sideStr.Contains("매수")) side = Side.Buy;
        else if (sideStr.Contains("매도")) side = Side.Sell;

        // CARRY는 무조건 Buy로 간주(전일 잔량 이월 매수)
        if (tag.Equals("CARRY", StringComparison.OrdinalIgnoreCase))
            side = Side.Buy;

        // side를 못 정하면 일단 실패로 처리(=원본에 side 기록하자)
        if (side == Side.Unknown) return false;

        f = new Fill
        {
            TimeKst = t,
            Tag = tag,
            Code = code,
            Qty = (int)qty,
            Price = price,
            Side = side
        };
        return true;
    }

    // ===== 내부 구조 =====
    private enum Side { Unknown, Buy, Sell }

    private sealed class Fill
    {
        public DateTime TimeKst;
        public string Tag = "";
        public string Code = "";
        public int Qty;
        public int Price;
        public Side Side;
    }
}
