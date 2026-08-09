using New_Tradegy.Library;
using New_Tradegy.Library.PostProcessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class Sec10FlowProcessor
{
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
        DistributeDelayedDeltaColumn(
            history,
            (int)Sec10Col.InstAcc);

        DistributeDelayedDeltaColumn(
            history,
            (int)Sec10Col.IndiAcc);

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

    private static void DistributeDelayedDeltaColumn(
        List<int[]> history,
        int col)
    {
        if (history == null || history.Count == 0)
            return;

        // 분배 전 DELTA 원본 보존
        int[] original =
            new int[history.Count];

        for (int i = 0; i < history.Count; i++)
            original[i] = history[i][col];

        for (int i = 0; i < history.Count; i++)
        {
            int announcedDelta =
                original[i];

            if (announcedDelta == 0)
                continue;

            int start = i;

            while (start > 0 &&
                   original[start - 1] == 0)
            {
                start--;
            }

            int distributeCount =
                i - start + 1;

            if (distributeCount <= 1)
                continue;

            double average =
                (double)announcedDelta /
                distributeCount;

            int distributedValue =
                (int)Math.Round(
                    average,
                    MidpointRounding.AwayFromZero);

            for (int k = start; k <= i; k++)
            {
                history[k][col] =
                    distributedValue;
            }
        }
    }
}