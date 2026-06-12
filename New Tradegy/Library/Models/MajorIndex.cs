using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Models
{
    public sealed partial class MajorIndex
    {
        private static readonly Lazy<MajorIndex> _instance = new Lazy<MajorIndex>(() => new MajorIndex());
        public static MajorIndex Instance => _instance.Value;

        private MajorIndex() { }

        public const int TickArraySize = 60; // const static implicitly
        public const int MinuteArraySize = 5; // const static implicitly

        // KOSPI
        public double KospiBuyPower { get; set; }
        public double KospiSellPower { get; set; }
        public int KospiProgramNetBuy { get; set; }
        public int KospiRetailNetBuy { get; set; }
        public int KospiForeignNetBuy { get; set; }
        public int KospiInstitutionNetBuy { get; set; }
        public int KospiInvestmentNetBuy { get; set; } // 금투
        public int KospiPensionNetBuy { get; set; }
        public double[] KospiTickBuyPower { get; } = new double[TickArraySize];
        public double[] KospiTickSellPower { get; } = new double[TickArraySize];

        // KOSDAQ
        public double KosdaqBuyPower { get; set; }
        public double KosdaqSellPower { get; set; }
        public int KosdaqProgramNetBuy { get; set; }
        public int KosdaqRetailNetBuy { get; set; }
        public int KosdaqForeignNetBuy { get; set; }
        public int KosdaqInstitutionNetBuy { get; set; }
        public int KosdaqInvestmentNetBuy { get; set; } // 금투
        public int KosdaqPensionNetBuy { get; set; }
        public double[] KosdaqTickBuyPower { get; } = new double[TickArraySize];
        public double[] KosdaqTickSellPower { get; } = new double[TickArraySize];

        // Indexes
        public int KospiIndex { get; set; }
        public int KosdaqIndex { get; set; }
        public float ShanghaiIndex { get; set; }
        public float HangSengIndex { get; set; }
        public float NikkeiIndex { get; set; }
        public float Snp500Index { get; set; }
        public float NasdaqIndex { get; set; }

        // Scores
        public int KospiScore { get; set; }
        public int KosdaqScore { get; set; }
    }












        public class IndexInflectionPoint
        {
            public int Time;          // HHmmss
            public string Name;       // KOSPI / KOSDAQ / NQ

            // ETF : 1.23% -> 123
            // NQ  : 0.21% -> 210
            public double RawValue;

            // 실제 %
            public double Rate;

            // +1 = LOW
            // -1 = HIGH
            public int Type;
        }

        public partial class MajorIndex
        {
            public List<IndexInflectionPoint> KospiInflections { get; }
                = new List<IndexInflectionPoint>();

            public List<IndexInflectionPoint> KosdaqInflections { get; }
                = new List<IndexInflectionPoint>();

            public List<IndexInflectionPoint> NasdaqInflections { get; }
                = new List<IndexInflectionPoint>();

            private int _lastInflectionHHmmss = -1;

            private double _lastKospiPivot = double.NaN;
            private double _lastKosdaqPivot = double.NaN;
            private double _lastNasdaqPivot = double.NaN;

        private double _k0 = double.NaN, _k1 = double.NaN;
        private double _d0 = double.NaN, _d1 = double.NaN;
        private double _n0 = double.NaN, _n1 = double.NaN;


        public void UpdateInflections(int hhmmss)
            {
                if (hhmmss == _lastInflectionHHmmss)
                    return;

                _lastInflectionHHmmss = hhmmss;

            DetectOneValue(
                "KOSPI",
                this.KospiIndex,
                100.0,
                10.0,
                ref _k0,
                ref _k1,
                ref _lastKospiPivot,
                KospiInflections,
                hhmmss);

            DetectOneValue(
                "KOSDAQ",
                this.KosdaqIndex,
                100.0,
                10.0,
                ref _d0,
                ref _d1,
                ref _lastKosdaqPivot,
                KosdaqInflections,
                hhmmss);

            DetectOneValue(
                "NQ",
                this.NasdaqIndex,
                1000.0,
                20.0,
                ref _n0,
                ref _n1,
                ref _lastNasdaqPivot,
                NasdaqInflections,
                hhmmss);
        }
        private void DetectOneValue(
            string name,
            double current,
            double divisor,
            double minMoveRaw,
            ref double p0,
            ref double p1,
            ref double lastPivot,
            List<IndexInflectionPoint> list,
            int hhmmss)
        {
            if (current == 0)
                return;

            if (double.IsNaN(p0))
            {
                p0 = current;
                return;
            }

            if (double.IsNaN(p1))
            {
                p1 = current;
                return;
            }

            double p2 = current;

            double d1 = p1 - p0;
            double d2 = p2 - p1;

            int type = 0;

            if (d1 > 0 && d2 < 0)
                type = -1;     // HIGH
            else if (d1 < 0 && d2 > 0)
                type = +1;     // LOW

            if (type != 0)
            {
                if (double.IsNaN(lastPivot) ||
                    Math.Abs(p1 - lastPivot) >= minMoveRaw)
                {
                    lastPivot = p1;

                    var p = new IndexInflectionPoint
                    {
                        Time = hhmmss,
                        Name = name,
                        RawValue = p1,
                        Rate = p1 / divisor,
                        Type = type
                    };

                    list.Add(p);
                    AppendInflectionToFile(p);

                    System.Diagnostics.Debug.Print(
                        $"[INFLECT] {p.Time:D6} | {p.Name,-7} | {(p.Type > 0 ? "LOW " : "HIGH")} | raw={p.RawValue:F0} | rate={p.Rate:F2}%");
                }
            }

            p0 = p1;
            p1 = p2;
        }


            private void AppendInflectionToFile(
                IndexInflectionPoint p)
            {
                try
                {
                    string dir =
                        @"C:\BJS\data work\IndexInflection";

                    Directory.CreateDirectory(dir);

                    string path =
                        Path.Combine(
                            dir,
                            $"{DateTime.Now:yyyyMMdd}.txt");

                    string line =
                        $"{DateTime.Now:yyyyMMdd} " +
                        $"{p.Time:D6} | " +
                        $"{p.Name,-7} | " +
                        $"{(p.Type > 0 ? "LOW " : "HIGH")} | " +
                        $"raw={p.RawValue,7:F0} | " +
                        $"rate={p.Rate,7:F2}%";

                    File.AppendAllText(
                        path,
                        line + Environment.NewLine);
                }
                catch
                {
                }
            }
        }
    }