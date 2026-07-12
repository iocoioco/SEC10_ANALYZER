using CPSYSDIBLib;
using CPTRADELib;
using New_Tradegy.Library;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Trackers.Charting;
using New_Tradegy.Library.UI;
using New_Tradegy.Library.UI;
using New_Tradegy.Library.UI.ChartClickHandlers;
using New_Tradegy.Library.UI.KeyBindings;
using New_Tradegy.Library.Utils;
using New_Tradegy.Test;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static New_Tradegy.Library.g;
using static OpenQA.Selenium.BiDi.Modules.Script.EvaluateResult;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

// PLAN
// 코스피 코스닥 온도계
// 섹터 동향(하락/상승) + 프돈 동시유입, 프돈순위 동시 2,3,5 등

// 장중 및 장마감 프돈 손실종목 Hud 표시
// 상관 & 절친 정리 및 평균가격 및 프돈유입
// 배차(강한 머리), 대형주 배차 100 이상
// 시초 지속강세 (프돈유입 및 배차강세), 장중 프돈 장난 종목
// 횡보 후 급상
// Reversion
// 프돈 3 - 5분 지속동향
// 시장의 돈이 몰리는 섹터 또는 종목
// 톱니 종목 제외
// 지수 급상/급락하는 날
// 지수 인버스 표시
// 호가 대량 종목 집중
// 장마감 미국선물 익일영향
// 상하한가 로직
// 강한 종목 자동 수익비 매수/매도 (특히, 시초)
// 시초 강한 또는 뉴스있는 종목 하락 후 반등(특히, 돌파시)
// 장전계획 및 뉴스scrap(특히, 네이버 새로운 뉴스 sorting)
// 보조차트 표시 자동화(예, 코스피 강, 코스피종목, 섹터 강, 섹터 표시 등)
// 지수 베팅(급상, 급락 또는 프돈 직선 상/하 베팅) Hud 
// 약수익의 문제점
// 호가 덤퍼
// 0113 휴림로봇 70억 매수 상태 안 올라가니 114억으로 증액, 그리고 매도 억 단위 베팅 가능
// 0113 나스닥 선물 -0.3 -> -0.43, 코스피레벌지 230 -> 86
// 0113 현대차 그룹 시초 공략 가능성 점검 급등락
// 분거/호가/종거 dgv 표시
// 자동매도 : 갑자기 밀어버리는 경우 대비 프로그램이 올리다 던지는 경우 대비



// 라인식별
//Gener 3 : 체(100) 4 : 푀, 5 : 외, 6 : 기, 7 : 누, 8,9 : 배수, 10,11 : 연속
//Index 3 : 푀, 4 : 기, 5 : 외, 6 : 개, 7 : 누, 8,9 : 배수, 10 : 나,  11 : 연
//    배차, 배합, 푀, 기, 외, 연 vs 개

// 사용예
//var text = "this is a \n test \n to check the working process";
//CenterHudForm.Show(
//    text: text // g.date.ToString("yyyy-MM-dd"),
//               //durationMs: 5000,
//               //fontSize : 43
//);

//MouseHudForm.Show(
//    text: text // g.date.ToString("yyyy-MM-dd"),
//               //durationMs: 3000,
//               //fontSize : 31
//);

// 표준환산표 ,
// 시총 \t
// 상관 ' '
// Correlation \t
// 삼성_코스피_코스닥_전체종목 \t
// 일 ' '
// 절친 \t

namespace New_Tradegy // added for test on 20241020 0300
{

    public partial class Form1 : Form
    {
        private static CPUTILLib.CpCybos _cpcybos;
        //private static CPSYSDIBLib.CpSvrNew7222 _cpsvrnew7222;
        private DSCBO1Lib.CpSvr8091S _cpsvr8091s;

        private System.Timers.Timer _timerConnection;
        private int _timerCount;

        //private PingAndSpeedMonitor networkMonitor;
        public static Form1 Instance { get; private set; } // Form1.Instance.SomeMethod();

        // used in Form1_Load()
        private CancellationTokenSource _cts;
        //private Thread thread;
        //private System.Windows.Forms.Timer timer;  // or System.Timers.Timer if you're using that

        // used in async tasks
        //private static TaskTimer _timer_maFjor_indices = new TaskTimer("task_major_indices");
        //private static TaskTimer _timer_KospiUpdater = new TaskTimer("task_KospiUpdater");
        //private static TaskTimer _timer_KosdaqUpdater = new TaskTimer("task_KosdaqUpdater");
        //private static TaskTimer _timer_Nasdaq = new TaskTimer("task_Nasdaq");


        // used in GetRemainRQ() and GetRemainTR()
        //private static DateTime _lastNonTradeAlertTime = DateTime.MinValue;
        //private static DateTime _lastTradeAlertTime = DateTime.MinValue;
        //private static readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(5);
        //private PersistentSigmaManager _sigma;

        //CancellationTokenSource _cts;

        // Rithmic 세션 관리 \\\
        private CancellationTokenSource _rithmicCts;
        private Task _rithmicTask;
        private readonly object _rithmicLock = new object();

        private int _rithmicStopping = 0;

        private bool _mouseHudStarted;

        private const bool UseBoardForm = false;

        //private ETF_NQ _etfNq;

        private IndexHudLabels _indexHud;

        //private static readonly SimplePostRealConfig _cfg = new SimplePostRealConfig
        //{
        //    W10 = 0.3,   // 10초 가중치
        //    W20 = 0.3,   // 20초 가중치
        //    W30 = 0.4,   // 30초 가중치

        //    RankThreshold = 0.8,  // 점수 임계
        //    CohortBonus = 0.0,  // 동조 보너스(미사용)
        //    SectorWeight = 0.0,  // 섹터 가중(미사용)

        //    StockCooldownSec = 60,  // 종목 쿨다운
        //    GlobalPerMinute = 60   // 분당 최대 알림
        //};

        private FormPreOpen _preOpenForm;
        public FormPreOpen PreOpenForm => _preOpenForm;
        private CancellationTokenSource _preOpenCts;
        private bool _preOpenStopped = false;

        static int warmupRemaining = 3;   // 시초 및 재시작 후 제외할 정상 10초 데이터 수
        public Form1()
        {
            InitializeComponent();
            g.MainForm = this; // for Form1.Instance
            this.KeyPreview = true;
            Instance = this;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            g.chart1 = chart1;

            dataGridView1.Columns.Clear();

            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dataGridView1.Columns.Add("Task", "Task");

            dataGridView1.Rows.Add("Multiple Regression Raw Data Preparation");
            dataGridView1.Rows.Add("Build Statistics");
            dataGridView1.Rows.Add("Multiple Regression Run Summary");
            dataGridView1.Rows.Add("NQ Lead/Lag Analysis");
            dataGridView1.Rows.Add("  ");
            dataGridView1.Rows.Add(" ");
        }
        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            switch (e.RowIndex)
            {
                case 0:
                    MulRegressionAnalyzer.RawDataForAllDates();  // 다중회귀용 RAW 데이터 생성
                    break;

                case 1:
                    Analyzer.RunStatAnalyzer(); // H, Z, A, Z, diff10, mul10, diff20, sum20, diff30, sum30, pro, for, ins, ind
                    break;

                case 2:
                    int method = 5;
                    MulRegressionAnalyzer.RunSummary(method);  // 다중회귀 실행 및 Summary 생성
                    break;

                case 3:
                    NqAnalyzer.NQLeadLagAnalysis(); // NQ Threshold / Lead - Lag 분석
                    break;

                case 4:
                    break;
                case 5:
                    break;
            }
        }

       





















        private BoardForm _boardForm;

        public void ShowBoard()
        {
            if (!UseBoardForm) return;

            if (_boardForm == null || _boardForm.IsDisposed)
                _boardForm = new BoardForm();

            var screens = Screen.AllScreens.OrderBy(s => s.Bounds.Left).ToArray();
            if (screens.Length == 0) return;

            // 가장 왼쪽 모니터
            Screen leftScreen = screens[0];
            Rectangle wa = leftScreen.WorkingArea;

            // 좌측 모니터의 우측 절반
            _boardForm.StartPosition = FormStartPosition.Manual;
            _boardForm.SetBounds(
                wa.Left + wa.Width / 2,
                wa.Top,
                wa.Width / 2,
                wa.Height
            );

            if (!_boardForm.Visible)
                _boardForm.Show();

            _boardForm.RefreshBoard();
        }

        public void ShowBoardSafe()
        {
            if (!UseBoardForm) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(ShowBoardSafe));
                return;
            }
            ShowBoard();
        }

        public void RefreshBoardSafe()
        {
            if (!UseBoardForm) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(RefreshBoardSafe));
                return;
            }

            if (_boardForm == null || _boardForm.IsDisposed)
                ShowBoard();

            _boardForm?.RefreshBoard();
        }

        public void UpdateHud()
        {
            var kospiData = g.StockRepo.TryGetDataOrNull("KODEX 레버리지");
            var kosdaqData = g.StockRepo.TryGetDataOrNull("KODEX 코스닥150레버리지");

            _indexHud.Update(kospiData, kosdaqData);
        }

        private static double _nqBid;
        private static double _nqAsk;
        private static double _nqPrint;
        private static int _nqBidSize;
        private static int _nqAskSize;
        private static int _nqPrintSize;

        // Form1.cs 안에 추가
        private void StopRithmicPipeReceiver(string reason = "")
        {
            if (System.Threading.Interlocked.Exchange(ref _rithmicStopping, 1) == 1)
                return;

            try
            {
                try { System.Diagnostics.Debug.WriteLine($"[RITHMIC] Stop: {reason}"); } catch { }

                try { _rithmicCts?.Cancel(); } catch { }
                try { _rithmicCts?.Dispose(); } catch { }
                _rithmicCts = null;
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _rithmicStopping, 0);
            }
        }

        public static int GetRemainRQ()
        {
            if (_cpcybos == null)
                return 60;

            int remain = _cpcybos.GetLimitRemainCount(CPUTILLib.LIMIT_TYPE.LT_NONTRADE_REQUEST);

            // 현재 시간 + 잔여 요청 수 출력
            if (remain < 10)
                Debug.WriteLine($"{DateTime.Now:HH:mm:ss.fff} ▶ RemainRQ = {remain}"); // Sound로 대체

            return remain;
        }
       
        private void chart1_MouseClick(object sender, MouseEventArgs e)
        {
            string selection = "";



            int row_id = 0, col_id = 0;


            var DisplayList = g.ChartMain.DisplayList;
            g.clickedStock = ChartClickMapper.CoordinateMapping(chart1, g.nRow, g.nCol, DisplayList, e, ref selection, ref col_id, ref row_id);
            if (g.clickedStock == null)
            {
                return;
            }

            if (Control.ModifierKeys == Keys.Control)
            {
                ChartClickHandler.HandleControlClick(chart1, selection, row_id, col_id);
            }
            else
            {
                ChartClickHandler.HandleClick(chart1, selection, row_id, col_id);
            }

            //SetFocusAndReturn();
        }

        private System.Windows.Forms.Timer _cybosRetryTimer;


        [DllImport("user32.dll")]

        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetFocus(IntPtr hWnd);

        private async Task RunFocusLoopAsync()
        {
            while (true)
            {
                await Task.Delay(10_000); // Wait 10 seconds

                // Marshal to UI thread safely
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        // Only refocus if form lost focus
                        if (!this.ContainsFocus || Form.ActiveForm != this)
                        {
                            SetFocusAndReturn();
                        }
                    }));
                }
            }
        }

        public void SetFocusAndReturn()
        {
            return;
            if (!g.chart1Focus)
            {
                return;
            }

            IntPtr handle = this.Handle;

            // Optional delay for browser switches, etc.
            System.Threading.Thread.Sleep(100);

            // Restore window if minimized
            if (IsIconic(handle))
            {
                ShowWindow(handle, SW_RESTORE);
            }

            // Bring the window to the foreground and give it focus
            SetForegroundWindow(handle);
            SetFocus(handle);
            this.Activate();  // Ensure it's the active window

            // 🔹 Focus the "KODEX 레버리지" grid to allow ProcessCmdKey to work
            var grid = this.Controls.Find("KODEX 레버리지", true).FirstOrDefault() as DataGridView;
            if (grid != null && grid.CanFocus)
            {
                grid.Focus();
                // Console.WriteLine("Focus set to: " + grid.Name);
            }
            else
            {
                // Console.WriteLine("KODEX 레버리지 grid not found or cannot be focused.");
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        private int _closing = 0;

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_mouseHudStarted)
            {
                _mouseHudStarted = false;
                MouseHud.Stop();    // ✅ 타이머 종료 + HUD 숨김(또는 dispose)
            }

            // 이미 닫는 중이면 재진입 차단
            if (System.Threading.Interlocked.Exchange(ref _closing, 1) == 1)
                return;

            // 여기서는 "추가로 Close/Exit 호출" 절대 하지 말고 정리만
            try
            {
                StopRithmicPipeReceiver("FormClosing");

                // 타이머/백그라운드에서 Close 다시 부르는 것들 먼저 끄기
                // myTimer?.Stop();
                // myTimer?.Dispose();
                // notifyIcon.Visible = false; notifyIcon.Dispose();
            }
            catch { }
        }

        private void EnablePreviewKeyDown(Control c)
        {
            c.PreviewKeyDown += (s, e) => e.IsInputKey = true;

            foreach (Control child in c.Controls)
                EnablePreviewKeyDown(child); // Recursively apply to all child controls
        }

       
    }
}

/*
 * 자동 매수/매도
 *  - 상승 후 가격이 혼미상태 일정시간 유지시 매도 1호가 걸어둔다, 일정 액수 이상 추가 하락시 매도(특히 독립종목은 빠르게 처리)
 *  - 그룹 종목은 그룹의 행태 반영
 *  - 상승, 하락 예측 후 자동 매수/매도 ... 매수는 상승 관성이 형상된 최상의 조건일 경우 진행하고 매도는 일정 조건 탈락시 자동
 *  - 자동매도 분당 51 이상 하락시 또는 2분 연속 하락 51 이상 하락시 자동 보고 및 표시, 75 이상 급락시 보고/표시없이 자동매도
 *  - 추세가 형성된 구간에서 계속 현위치에서 상승 가능성 예측 후 추가 매수 (실험을 통해 앞으로 타당성 추가 보완 후)
 *  - 강이 충분히 높으면 사이안 꽃은 없어도 된다 ? 
 * 
 * */

// 꺾이는 종목은 미련없이 손절하라, 추세순응, 체결강도, 매수자재
// 보유종목 급락시 소리와 함께 자동 매도하는 기능
// 시장 종목 중 급등시 소리와 함께 검토 요구하는 기능
// 가는 놈 + 강한 상관, 단독 또는 그룹으로 표시 및 추천 기능 특히 901 근처에서 ?
// ETF 급한 상상시 매수창 제시 및 Confirm 요구하는 기능
// 외인매수 주도종목 상관관계 표시
// 선형성이 강한 종목인가 ? 
// 종목별 외인매수 갯수를 그래프의 종목이름 옆 표시 : StockMember
// CpSvr7037 : 시간대별 예상체결지수 제공
// CpSvr7254 : 투자주체별 일별 기관별 매수/매도
