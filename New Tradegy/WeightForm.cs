using global::New_Tradegy.Library.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace New_Tradegy
{
    public partial class WeightForm : Form
    {
        private const string WeightPath = @"C:\BJS\data work\RankRuntimeWeights.txt";

        private readonly FlowLayoutPanel _panel = new FlowLayoutPanel();
        private readonly List<Row> _rows = new List<Row>();
        private bool _loading;

        private sealed class Row
        {
            public string Key;
            public Label Lbl;
            public TrackBar Bar;
            public NumericUpDown Num;
        }

        public WeightForm()
        {
            InitializeComponent();
            Text = "Rank Runtime Weights";
            Width = 520; Height = 450;
            StartPosition = FormStartPosition.CenterScreen;

            _panel.Dock = DockStyle.Fill;
            _panel.FlowDirection = FlowDirection.TopDown;
            _panel.WrapContents = false;
            _panel.AutoScroll = true;
            Controls.Add(_panel);

            Load += WeightForm_Load;
            KeyPreview = true;
        }

        private void WeightForm_Load(object sender, EventArgs e)
        {
            _loading = true;

            //var list = LoadPairsOrCreate();
            //BuildRows(list);

            //// 파일값 → RankRuntime 즉시 반영
            //for (int i = 0; i < list.Count; i++)
            //    CompositeWeights.SetByKey(list[i].Key, list[i].Value);

            //_loading = false;
        }
    }

        //    private struct Pair { public string Key; public double Value; }

        //    private List<Pair> LoadPairsOrCreate()
        //    {
        //        var list = new List<Pair>();

        //        // 1) Load if file exists
        //        if (File.Exists(WeightPath))
        //        {
        //            var lines = File.ReadAllLines(WeightPath, Encoding.UTF8);
        //            foreach (var raw in lines)
        //            {
        //                var line = (raw ?? "").Trim();
        //                if (line.Length == 0) continue;
        //                if (line.StartsWith("#") || line.StartsWith("//")) continue;

        //                int sp = line.IndexOf(' ');
        //                if (sp <= 0 || sp >= line.Length - 1) continue;

        //                string key = line.Substring(0, sp).Trim();
        //                string val = line.Substring(sp + 1).Trim();

        //                // 구버전 '배합' -> '호가'
        //                if (key == "배합") key = "호가";

        //                if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        //                    v = 0.0;

        //                v = Clamp01(v);
        //                list.Add(new Pair { Key = key, Value = v });
        //            }
        //        }

        //        // 2) If nothing loaded, build from CompositeWeights (and save)
        //        if (list.Count == 0)
        //        {
        //            CompositeWeights.TryLoadOnce();

        //            double GetW(string key)
        //            {
        //                var w = CompositeWeights.W;
        //                switch (key)
        //                {
        //                    case "푀분": return w.푀분;
        //                    case "거분": return w.거분;
        //                    case "배차": return w.배차;
        //                    case "호가": return w.호가;
        //                    case "동조": return w.동조;
        //                    case "푀누": return w.푀누;
        //                    case "종누": return w.종누;
        //                    default: return 0.0;
        //                }
        //            }


        //            var defaults = new[] { "푀분", "거분", "배차", "호가", "동조", "푀누", "종누" };
        //            foreach (var k in defaults)
        //                list.Add(new Pair { Key = k, Value = Clamp01(GetW(k)) });

        //            // 모두 0이면 초기 프리셋 적용 (푀분=1)
        //            if (list.TrueForAll(p => p.Value == 0))
        //            {
        //                for (int i = 0; i < list.Count; i++)
        //                    list[i] = new Pair { Key = list[i].Key, Value = (list[i].Key == "푀분") ? 1.0 : 0.0 };
        //            }

        //            SaveAll(list);
        //        }

        //        // 3) Normalize: map legacy, clamp, dedupe, and order consistently
        //        var order = new Dictionary<string, int>
        //        {
        //            ["푀분"] = 0,
        //            ["거분"] = 1,
        //            ["배차"] = 2,
        //            ["호가"] = 3,
        //            ["동조"] = 4,
        //            ["푀누"] = 5,
        //            ["종누"] = 6
        //        };

        //        list = list
        //            .Select(p => new Pair { Key = (p.Key == "배합") ? "호가" : p.Key, Value = Clamp01(p.Value) })
        //            .Where(p => order.ContainsKey(p.Key))
        //            .GroupBy(p => p.Key)                           // 마지막 값 우선
        //            .Select(g => g.Last())
        //            .OrderBy(p => order[p.Key])
        //            .ToList();

        //        // 4) Always return here (fixes CS0161)
        //        return list;
        //    }

        //    private void SaveAll(IEnumerable<Pair> pairs)
        //    {
        //        try
        //        {
        //            string dir = Path.GetDirectoryName(WeightPath);
        //            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        //            var sb = new StringBuilder();
        //            foreach (var p in pairs)
        //                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1:0.##}", p.Key, Clamp01(p.Value)));

        //            File.WriteAllText(WeightPath, sb.ToString(), Encoding.UTF8);
        //        }
        //        catch { }
        //    }

        //    private static double Clamp01(double v) { if (v < 0) return 0; if (v > 1) return 1; return v; }
        //    private static int RoundToStepInt(int value, int step) { return (int)Math.Round(value / (double)step) * step; }

        //    private void BuildRows(List<Pair> pairs)
        //    {
        //        _panel.SuspendLayout();
        //        _panel.Controls.Clear();
        //        _rows.Clear();

        //        for (int i = 0; i < pairs.Count; i++)
        //        {
        //            var p = pairs[i];
        //            var row = new Row { Key = p.Key };

        //            var lbl = new Label
        //            {
        //                AutoSize = true,
        //                Width = 70,
        //                Text = p.Key
        //            };
        //            row.Lbl = lbl;

        //            var bar = new TrackBar
        //            {
        //                Minimum = 0,
        //                Maximum = 100,
        //                TickFrequency = 5,
        //                SmallChange = 1,
        //                LargeChange = 5,
        //                Width = 300
        //            };

        //            int iv = (int)Math.Round(p.Value * 100.0);
        //            iv = RoundToStepInt(iv, 5);
        //            iv = Math.Max(0, Math.Min(100, iv));
        //            bar.Value = iv;
        //            row.Bar = bar;

        //            var num = new NumericUpDown
        //            {
        //                Minimum = 0,
        //                Maximum = 1,
        //                DecimalPlaces = 2,
        //                Increment = 0.05M,
        //                Width = 60,
        //                Value = (decimal)Math.Round(iv / 100.0, 2)
        //            };
        //            row.Num = num;

        //            var line = new FlowLayoutPanel
        //            {
        //                FlowDirection = FlowDirection.LeftToRight,
        //                Width = _panel.ClientSize.Width - 30,
        //                AutoSize = true,
        //                WrapContents = false
        //            };
        //            line.Controls.Add(lbl);
        //            line.Controls.Add(bar);
        //            line.Controls.Add(num);

        //            _panel.Controls.Add(line);
        //            _rows.Add(row);

        //            // 이벤트
        //            bar.Scroll += (s, e) =>
        //            {
        //                if (_loading) return;
        //                int v = RoundToStepInt(((TrackBar)s).Value, 5);
        //                if (((TrackBar)s).Value != v) ((TrackBar)s).Value = v;
        //                num.Value = (decimal)Math.Round(v / 100.0, 2);
        //            };
        //            bar.MouseUp += (s, e) => CommitRow(row);
        //            bar.KeyUp += (s, e) => { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) CommitRow(row); };

        //            num.ValueChanged += (s, e) =>
        //            {
        //                if (_loading) return;
        //                int v = RoundToStepInt((int)Math.Round((double)num.Value * 100.0), 5);
        //                if (bar.Value != v) bar.Value = v;
        //            };
        //            num.Leave += (s, e) => CommitRow(row);
        //            num.KeyUp += (s, e) => { if (e.KeyCode == Keys.Enter) CommitRow(row); };
        //        }

        //        _panel.ResumeLayout();
        //    }

        //    private void CommitRow(Row row)
        //    {
        //        if (_loading) return;

        //        int iv = RoundToStepInt(row.Bar.Value, 5);
        //        double v = Math.Round(iv / 100.0, 2);
        //        row.Num.Value = (decimal)v;

        //        CompositeWeights.SetByKey(row.Key, v);

        //        var pairs = new List<Pair>(_rows.Count);
        //        for (int i = 0; i < _rows.Count; i++)
        //        {
        //            var r = _rows[i];
        //            int iv2 = RoundToStepInt(r.Bar.Value, 5);
        //            double vv = Math.Round(iv2 / 100.0, 2);
        //            pairs.Add(new Pair { Key = r.Key, Value = vv });
        //        }
        //        SaveAll(pairs);
        //        try { CompositeWeights.ReloadNow(); } catch { }
        //    }

        //    public static void ApplyFromFileToRankRuntime(Action<string, double> setFunc)
        //    {
        //        var lines = File.ReadLines(WeightPath);
        //        foreach (var line in lines)
        //        {
        //            if (string.IsNullOrWhiteSpace(line)) continue;
        //            var parts = line.Split(' ');
        //            if (parts.Length < 2) continue;

        //            var key = parts[0].Trim();
        //            if (key == "배합") key = "호가"; // ★ 구버전 호환

        //            if (double.TryParse(parts[1], out var val))
        //                setFunc(key, val);
        //        }
        //    }
        //}
    }
