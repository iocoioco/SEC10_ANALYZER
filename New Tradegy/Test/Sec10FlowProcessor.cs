using New_Tradegy.Library;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.PostProcessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
public static class Sec10FlowProcessor
{
    private static List<int[]> LoadSec10HistoryForConvert(
    string file)
    {
        const int cols = 26;

        var history =
            new List<int[]>();

        if (!File.Exists(file))
            return history;

        string[] lines =
            File.ReadAllLines(file);

        foreach (string line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] sp =
                line.Split(',');

            // 구버전 9컬럼 또는 신버전 26컬럼만 허용
            if (sp.Length != 9 &&
                sp.Length != 26)
            {
                continue;
            }

            int[] row =
                new int[cols];

            bool valid = true;

            for (int k = 0; k < sp.Length; k++)
            {
                int value;

                if (!int.TryParse(
                        sp[k].Trim(),
                        out value))
                {
                    valid = false;
                    break;
                }

                row[k] = value;
            }

            if (!valid)
                continue;

            if (row[(int)Sec10Col.Time] == 0)
                continue;

            history.Add(row);
        }

        return history;
    }
    private static void SaveConvertedSec10History(
    string file,
    List<int[]> history)
    {
        if (history == null ||
            history.Count == 0)
        {
            return;
        }

        const string header =
            "time,etf,nq," +
            "pro,for,inst,indi," +
            "diff10,sum10," +
            "diff20,sum20," +
            "diff30,sum30," +
            "heat1,heat25,heat5," +
            "heatZ1,heatZ25,heatZ5," +
            "a3,a6,a9," +
            "az3,az6,az9," +
            "score";

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(header);

        foreach (int[] row in history)
        {
            if (row == null ||
                row.Length < 26)
            {
                continue;
            }

            for (int k = 0;
                 k < 26;
                 k++)
            {
                if (k > 0)
                    sb.Append(',');

                sb.Append(row[k]);
            }

            sb.AppendLine();
        }

        File.WriteAllText(
            file,
            sb.ToString(),
            Encoding.UTF8);
    }

    public static void ConvertAllSec10OldFiles()
    {
        const string root =
            @"C:\BJS\Study\지수10초";

        if (!Directory.Exists(root))
            return;

        // --------------------------------------------------
        // yyyyMMdd 형식의 날짜 디렉토리만 추출
        // --------------------------------------------------
        var directories =
            Directory.GetDirectories(root)
                .Where(dir =>
                {
                    string name =
                        Path.GetFileName(dir);

                    DateTime date;

                    return DateTime.TryParseExact(
                        name,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out date);
                })
                .OrderBy(dir => dir)
                .ToArray();

        foreach (string directory in directories)
        {
            ConvertOneSec10File(
                directory,
                "KODEX 레버리지.txt");

            ConvertOneSec10File(
                directory,
                "KODEX 코스닥150레버리지.txt");
        }
    }

    private static void ConvertOneSec10File(
    string directory,
    string fileName)
    {
        string path =
            Path.Combine(
                directory,
                fileName);

        if (!File.Exists(path))
            return;

        string nameWithoutExtension =
            Path.GetFileNameWithoutExtension(
                fileName);

        string oldPath =
            Path.Combine(
                directory,
                nameWithoutExtension + "_old.txt");

        // --------------------------------------------------
        // 이미 _old가 있으면
        // 변환 완료된 날짜로 판단
        // --------------------------------------------------
        if (File.Exists(oldPath))
            return;

        // --------------------------------------------------
        // 1. 원본 보존
        // --------------------------------------------------
        File.Move(
            path,
            oldPath);

        // --------------------------------------------------
        // 2. 원본 읽기
        // --------------------------------------------------
        List<int[]> history =
            LoadSec10HistoryForConvert(
                oldPath);

        if (history == null ||
            history.Count == 0)
        {
            // 읽기 실패 시 원상복구
            File.Move(
                oldPath,
                path);

            return;
        }

        // --------------------------------------------------
        // 3. 누적 → delta
        //    기관/개인 → delayed distribute
        // --------------------------------------------------
        PostProcessAccumulatedFlows(
            history);

        // --------------------------------------------------
        // 4. 기존 파일명으로 새 표준파일 저장
        // --------------------------------------------------
        SaveConvertedSec10History(
            path,
            history);

        // ★ 첫 실행 때 여기에 breakpoint
        // _old.txt와 새 .txt를 직접 비교
    }

    public static void PostProcessAccumulatedFlows(
    List<int[]> history)
    {
        if (history == null || history.Count == 0)
            return;

        // --------------------------------------------
        // 0) 원본 누적값 저장
        // --------------------------------------------
        SaveFlowHistory(
            history,
            "Flow_RAW.txt");

        // --------------------------------------------
        // 1) 누적값 → 10초 증감값
        // --------------------------------------------
        ConvertAccumulatedColumnToDelta(
            history,
            (int)Sec10Col.ProAcc);

        ConvertAccumulatedColumnToDelta(
            history,
            (int)Sec10Col.ForAcc);

        ConvertAccumulatedColumnToDelta(
            history,
            (int)Sec10Col.InstAcc);

        ConvertAccumulatedColumnToDelta(
            history,
            (int)Sec10Col.IndiAcc);

        // 차감 완료 상태 저장
        SaveFlowHistory(
            history,
            "Flow_DELTA.txt");

        // --------------------------------------------
        // 2) 기관 / 개인 지연 발표값 분배
        // --------------------------------------------
        // 기관 + 개인은 7222 발표 단위로 함께 분배
        DistributeDelayedFlows(history);

        // 분배 완료 상태 저장
        SaveFlowHistory(
            history,
            "Flow_DIST.txt");
    }

    private static void SaveFlowHistory(
     List<int[]> history,
     string fileName)
    {
        if (history == null || history.Count == 0)
            return;

        string directory =
            $@"C:\BJS\Study\지수10초\{g.date}";

        Directory.CreateDirectory(directory);

        string path =
            Path.Combine(directory, fileName);

        var sb = new StringBuilder();

        sb.AppendLine(
            "time\tpro\tfor\tinst\tretail");

        foreach (int[] row in history)
        {
            if (row == null ||
                row.Length <= (int)Sec10Col.IndiAcc)
            {
                continue;
            }

            sb.Append(row[(int)Sec10Col.Time]);
            sb.Append('\t');
            sb.Append(row[(int)Sec10Col.ProAcc]);
            sb.Append('\t');
            sb.Append(row[(int)Sec10Col.ForAcc]);
            sb.Append('\t');
            sb.Append(row[(int)Sec10Col.InstAcc]);
            sb.Append('\t');
            sb.Append(row[(int)Sec10Col.IndiAcc]);
            sb.AppendLine();
        }

        File.WriteAllText(
            path,
            sb.ToString(),
            Encoding.UTF8);
    }

    private static void ConvertAccumulatedColumnToDelta(
        List<int[]> history,
        int col)
    {
        if (history == null || history.Count < 2)
            return;

        // 뒤에서부터 해야 원 누적값이 보존됨
        for (int i = history.Count - 1; i >= 1; i--)
        {
            history[i][col] =
                history[i][col] -
                history[i - 1][col];
        }

        // 첫 행은 비교 대상이 없으므로 0
        history[0][col] = 0;
    }

    
    private static void DistributeDelayedFlows(
    List<int[]> history)
    {
        if (history == null || history.Count == 0)
            return;

        int instCol =
            (int)Sec10Col.InstAcc;

        int retailCol =
            (int)Sec10Col.IndiAcc;

        // --------------------------------------------------
        // DELTA 원본 보존
        // --------------------------------------------------
        int[] instOriginal =
            new int[history.Count];

        int[] retailOriginal =
            new int[history.Count];

        for (int i = 0; i < history.Count; i++)
        {
            instOriginal[i] =
                history[i][instCol];

            retailOriginal[i] =
                history[i][retailCol];

            // 새 분배값을 넣기 위해 초기화
            history[i][instCol] = 0;
            history[i][retailCol] = 0;
        }

        // --------------------------------------------------
        // 기관 또는 개인 중 하나라도 변하면
        // 하나의 7222 발표로 판단
        // --------------------------------------------------
        for (int i = 0; i < history.Count; i++)
        {
            int instDelta =
                instOriginal[i];

            int retailDelta =
                retailOriginal[i];

            if (instDelta == 0 &&
                retailDelta == 0)
            {
                continue;
            }

            int start = i;

            // --------------------------------------------------
            // 직전 발표 이후 0 구간을 뒤로 탐색
            // 최대 9개 Sec10 ≈ 90초
            // --------------------------------------------------
            while (start > 0)
            {
                bool previousHasAnnouncement =
                    instOriginal[start - 1] != 0 ||
                    retailOriginal[start - 1] != 0;

                if (previousHasAnnouncement)
                    break;

                start--;

                if (i - start + 1 >= 9)
                    break;
            }

            int count =
                i - start + 1;

            if (count <= 0)
                continue;

            int instValue =
                (int)Math.Round(
                    (double)instDelta / count,
                    MidpointRounding.AwayFromZero);

            int retailValue =
                (int)Math.Round(
                    (double)retailDelta / count,
                    MidpointRounding.AwayFromZero);

            // --------------------------------------------------
            // 같은 발표구간에 기관 / 개인 함께 분배
            // --------------------------------------------------
            for (int k = start; k <= i; k++)
            {
                history[k][instCol] =
                    instValue;

                history[k][retailCol] =
                    retailValue;
            }
        }
    }


}

