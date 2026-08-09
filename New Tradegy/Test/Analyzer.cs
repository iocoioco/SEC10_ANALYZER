using New_Tradegy.Library;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace New_Tradegy.Library.Listeners
{
    public static class Analyzer
    {
        public static void RunStatAnalyzer()
        {
            // 기존
            g.Sec10Kospi = new Sec10Engine();
            g.Sec10Kosdaq = new Sec10Engine();

            string root =
                @"C:\BJS\Study\지수10초";

            // -----------------------------------------
            // Z 통계 수집 시작
            // -----------------------------------------
            Sec10ZStatistics.Reset();

            foreach (string dir
                in Directory.GetDirectories(root))
            {
                string name =
                    Path.GetFileName(dir);

                int date;

                if (!int.TryParse(
                    name,
                    out date))
                {
                    continue;
                }

                g.date = date;

                // 날짜별 Sec10 원본 읽기
                FileLoader.LoadIndex10SecData();

                // -------------------------------------
                // Z 통계용 데이터 수집
                // -------------------------------------

                Sec10ZStatistics.Collect(
                    Sec10Store.Kospi,
                    Sec10Store.KospiRow,
                    true);

                Sec10ZStatistics.Collect(
                    Sec10Store.Kosdaq,
                    Sec10Store.KosdaqRow,
                    false);

                // 기존 분석 파일 생성
                // Analyzer.CreateAnalyzedFilesForDate(date);
            }

            // -----------------------------------------
            // 모든 날짜 통합 후 mean/std 생성
            // -----------------------------------------
            Sec10ZStatistics.CalculateAndSave();

            // 기존 통계
            // StatAnalyzer.CreateStatFilesAllDates();

            MessageBox.Show("통계 완료");
        }

        public static void CreateAnalyzedFilesForDate(int date)
        {
            CreateOne(date, Sec10Store.Kospi, Sec10Store.KospiRow, "KOSPI_ANALYZED_V1.txt");
            CreateOne(date, Sec10Store.Kosdaq, Sec10Store.KosdaqRow, "KOSDAQ_ANALYZED_V1.txt");
        }

        private static void CreateOne(int date, int[,] a, int count, string outputFileName)
        {
            string dir = Path.Combine(@"C:\BJS\Study\지수10초", date.ToString());
            string path = Path.Combine(dir, outputFileName);

            var sb = new StringBuilder();

            sb.AppendLine(
                "time,nq,etf," +
                "H1,H25,H5,HZ1,HZ25,HZ5," +
                "A1,A25,A5," +
                "A20,A30,A40,A60,A90,A120,A180," +
                "AZ1,AZ25,AZ5," +
                "M10Diff,M10Sum,M20Diff,M20Sum,M30Diff,M30Sum," +
                "pro,for,inst,indi");

            var work = new Sec10Engine();
            var nqState = new NqMotionState();

            for (int i = 0; i < count; i++)
            {
                int hhmmssfff = a[i, 0];

                if (i > 0)
                {
                    int prev = a[i - 1, 0];

                    double gapMs = TimeUtils.ElapsedMillisecondsDouble(
                        prev,
                        hhmmssfff);

                    if (gapMs < 8000 || gapMs > 12000)
                        continue;
                }

                double etf = a[i, 1];
                double nq = a[i, 2];

                int pro = a[i, 3];
                int foreign = a[i, 4];
                int inst = a[i, 5];
                int indi = a[i, 6];



                int diff = a[i, 7];
                int sum = a[i, 8];

                int m10Diff = diff;
                int m10Sum = sum;

                int m20Diff = diff;
                int m20Sum = sum;

                if (i >= 1)
                {
                    m20Diff += a[i - 1, 7];
                    m20Sum += a[i - 1, 8];
                }

                int m30Diff = m20Diff;
                int m30Sum = m20Sum;

                if (i >= 2)
                {
                    m30Diff += a[i - 2, 7];
                    m30Sum += a[i - 2, 8];
                }

                int[] row = new int[12];

                row[0] = hhmmssfff;
                row[1] = (int)etf;
                row[2] = (int)nq;
                row[3] = pro;
                row[4] = foreign;
                row[5] = inst;
                row[6] = indi;
                row[7] = diff;
                row[8] = sum;

                work.AddRow(row);

                if (work.Count < 30)
                    continue;

                HeatResult h1 = work.CalcHeat(6, 2, 1);
                HeatResult h25 = work.CalcHeat(15, 2, 1);
                HeatResult h5 = work.CalcHeat(30, 2, 1);

                nqState.UpdateOne(work, 6, 2, nqState.M1);
                nqState.UpdateOne(work, 15, 2, nqState.M25);
                nqState.UpdateOne(work, 30, 2, nqState.M5);



                // 통계용
                //nqState.UpdateOne(work, 1, 2, nqState.M10);
                nqState.UpdateOne(work, 2, 2, nqState.M20);
                nqState.UpdateOne(work, 3, 2, nqState.M30);
                nqState.UpdateOne(work, 4, 2, nqState.M40);
                nqState.UpdateOne(work, 6, 2, nqState.M60);
                nqState.UpdateOne(work, 9, 2, nqState.M90);
                nqState.UpdateOne(work, 12, 2, nqState.M120);
                nqState.UpdateOne(work, 18, 2, nqState.M180);





                sb.AppendLine(string.Join(",",
                    hhmmssfff,
                    etf,
                    nq,

                    h1.Heat,
                    h25.Heat,
                    h5.Heat,

                    h1.Z,
                    h25.Z,
                    h5.Z,

                    nqState.M1.A,
                    nqState.M25.A,
                    nqState.M5.A,

                    //nqState.M10.A,
                    nqState.M20.A,
                    nqState.M30.A,
                    nqState.M40.A,
                    nqState.M60.A,
                    nqState.M90.A,
                    nqState.M120.A,
                    nqState.M180.A,

                    nqState.M1.Z,
                    nqState.M25.Z,
                    nqState.M5.Z,

                    m10Diff, m10Sum,
                    m20Diff, m20Sum,
                    m30Diff, m30Sum,

                    pro,
                    foreign,
                    inst,
                    indi));

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);


            }
        }
    }
}