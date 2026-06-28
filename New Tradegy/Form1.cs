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
using Newtonsoft.Json.Linq;
using System;
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

        public Form1()
        {
            InitializeComponent();


            g.MainForm = this; // for Form1.Instance
            this.KeyPreview = true;

            Instance = this;

            //ms.Speech("testing");
            //ts.지수합계점검();
            //return;
            //EtfOpenThrustTester.Run();

            SoundUtils.Sound("일반", "by 2032");
        }

        //private void StartNetworkMonitor()
        //{
        //    networkMonitor = new PingAndSpeedMonitor(
        //    host: "daishin.co.kr",
        //    logFilePath: @"C:\BJS\Z Log\ping_log.txt",
        //    wavFilePath: @"Resources\alert.wav",
        //    logIntervalSeconds: 600,         // log every 10 min
        //    pingThresholdMs: 300,            // if ping > 300ms
        //    speedThresholdMbps: 10.0);       // if speed < 10 Mbps

        //    networkMonitor.Start();
        //}

        //private void Form1_Resize(object sender, EventArgs e)
        //{

        //    //_indexHud?.Relocate(this.ClientSize.Width, this.ClientSize.Height);
        //}
        private void FormInitializeBasics()
        {
            //this.KeyPreview = true; // to use Form1.KeyDown or Form1.PreviewKeyDown

            //EnablePreviewKeyDown(this);
            this.AutoScaleMode = AutoScaleMode.None;

            this.Name = "Form1"; // for debugging

            this.FormClosing += Form1_FormClosing;
            // this.Resize += Form1_Resize;

            // 0) 환경/설정 (가벼운 것만)
            FileIn.read_제어();

            //if (!g.test)
            //{
            _cpcybos = new CPUTILLib.CpCybos();
            _cpcybos.OnDisconnect += CpCybos_OnDisconnect;

            if (_cpcybos.IsConnect == 0)
            {
                g.connected = false;
                ChangeMainTitleConnection();
                StartCybosRetry();
            }
            else
            {
                g.connected = true;
            }
            //}
            //else
            //{
            //    g.connected = false;
            //}

            // 1) 어떤 모니터를 기준으로 할지 결정 (폼이 있는 화면 기준)
            var screen = Screen.FromControl(this);

            // 2) “좌표계”는 WorkingArea
            var work = screen.WorkingArea;

            //this.ClientSize.Width, 
            //int a = this.ClientSize.Height;


            // 3) 폼 배치: WorkingArea에 정확히 맞추기 (예측 가능)
            this.StartPosition = FormStartPosition.Manual;
            this.WindowState = FormWindowState.Normal;
            this.Bounds = work;

            // 4) 차트 배치
            // (디자이너에 chart1이 이미 올라가 있으면 Controls.Add는 빼도 됨)
            chart1.Dock = DockStyle.Fill;
            chart1.Margin = Padding.Empty;
            //chart1.BackColor = Color.FromArgb(30, 30, 30); // 20260328
            // 부모 컨테이너가 패널이면 이것도 같이 (있다면)
            this.Padding = Padding.Empty;


            this.Text = g.v.MainChartDisplayMode; // 시초에는 푀분

            g.q = "o&s";

            g.gid = 0;
            g.Gid = 0;
        }
        private void FormInitializeAsyncsIfConnected()
        {
            if (!g.test && g.connected) // for market trading
            {
                OrderItemCybosListener.Init_CpConclusion();

                DealManager.DealProcessing();
                DealManager.DealHold(); // 
                DealManager.DealDeposit(); // button1 tr(1)

                //_cts = new CancellationTokenSource();
                //var hogaTask = HogaDumper.RunAsync(_cts.Token);
                //hogaTask.ContinueWith(t =>
                //{
                //    //System.Diagnostics.Debug.WriteLine("HogaTask ended: " + t.Status);
                //    if (t.Exception != null)
                //      System.Diagnostics.Debug.WriteLine(t.Exception.ToString());
                //}, TaskScheduler.Default);


                subscribe_8091S(); // 종목별 당일외인순매수량, (회원사별 종목 세부 매수현황 가능)

                _preOpenCts = new CancellationTokenSource();
                int hhmm = Convert.ToInt32(DateTime.Now.ToString("HHmm"));

                if (hhmm >= 840 && hhmm < 900)
                {
                    _preOpenForm = new FormPreOpen();
                    _preOpenForm.Show();

                    Task.Run(() =>
                        PreMarketEyeBatchDownloader.RunDownloaderLoop(_preOpenCts.Token));
                }

                Task.Run(() => MarketEyeBatchDownloader.RunDownloaderLoop());

                Task.Run(() => task_RQTRSB()); // runs in background

                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            //Console.WriteLine($"▶️ Starting task_major_indices at {DateTime.Now:HH:mm:ss}");
                            //await _timer_major_indices.TryMeasureAndLogAsync(async () =>
                            //{
                            await Scraper.task_major_indices(); // investing major indices
                            //});
                        }
                        catch (Exception ex)
                        {
                            //Console.WriteLine($"🔥 Fatal error in task_major_indices: {ex.Message}\n{ex.StackTrace}");
                        }

                        // Wait 10 seconds before retrying
                        //Console.WriteLine($"🔁 Restarting task_major_indices in 10 seconds...");
                        await Task.Delay(10000);
                    }
                });

                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            //Console.WriteLine($"▶️ Starting runKOSPIUpdater at {DateTime.Now:HH:mm:ss}");
                            //await _timer_KospiUpdater.TryMeasureAndLogAsync(async () =>
                            //{
                            await runKOSPIUpdater();
                            //});
                        }
                        catch (Exception ex)
                        {
                            //Console.WriteLine($"🔥 runKOSPIUpdater crashed: {ex.Message}\n{ex.StackTrace}");
                        }
                        //Console.WriteLine("🔁 Restarting runKOSPIUpdater in 10 seconds...");
                        await Task.Delay(10000);
                    }
                });

                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            //Console.WriteLine($"▶️ Starting runKOSDAQUpdater at {DateTime.Now:HH:mm:ss}");
                            //await _timer_KosdaqUpdater.TryMeasureAndLogAsync(async () =>
                            //{
                            await runKOSDAQUpdater();
                            //});

                        }
                        catch (Exception ex)
                        {
                            //Console.WriteLine($"🔥 runKOSDAQUpdater crashed: {ex.Message}\n{ex.StackTrace}");
                        }

                        //Console.WriteLine("🔁 Restarting runKOSDAQUpdater in 10 seconds...");
                        await Task.Delay(10000);
                    }
                });


            }



        }



        private void Form1_Load(object sender, EventArgs e)
        {
            




            FormInitializeBasics();

            _indexHud = new IndexHudLabels(this);

            this.BeginInvoke(new Action(() =>
            {
                _indexHud.Relocate();
            }));

            //g.Sigma.LoadAtStart();

            g.chart1 = chart1;
            g.BookBidManager = new BookBidManager(); //g.BookBidManager.StartMonitorLoop();

            g.ChartManager = new ChartManager();
            g.ChartManager.SetChart1(chart1);

            g.StockRepo = StockRepository.Instance;





            //if (g.ImpulseGate == null)
            //    g.ImpulseGate = new PendingBestGate();

            if (g.ImpulseRunner == null)
                g.ImpulseRunner = new ImpulseRunner(g.ImpulseGate);

            //g.PendingBestGate = new PendingBestGate();






            g.cellHeight = this.DeviceDpi >= 192 ? 29 : 27; // Dell & HP 32인치


            // ✅ 여기서만 우주/레포 구성
            BootStrap.Initialize();   // (네가 만든 진입점 이름으로)



            KeyBindingRegistrar.RegisterAll();

            GeneratePanes();

            FormInitializeAsyncsIfConnected();

            g.ChartMain = new ChartMain(); // all new, Form_1 start
            PostProcessor.ManageChart1Invoke(); // Form1

            var FormSub = new FormSub();
            FormSub.SetMainForm(this);  // 🔹 Pass reference to main chart
            FormSub.Show(); // second chart

            g.KospiMinuteZ = new MinuteZEngine(
              sigma1: 11.89,
              sigma3: 20.77,
              sigma6: 29.25,
              sigma10: 37.09);

            g.KosdaqMinuteZ = new MinuteZEngine(
                sigma1: 14.32,
                sigma3: 24.41,
                sigma6: 34.21,
                sigma10: 43.41);




            // 1. 먼저 생성
            g.Sec10Kospi = new Sec10Engine();
            g.Sec10Kosdaq = new Sec10Engine();

            // 2. 그 다음 연결
            //_etfNq = new ETF_NQ();
            //_etfNq.SetEngines(g.Sec10Kospi, g.Sec10Kosdaq);

            //// 3. 초기화
            //_etfNq.Initialize(g.chart1);





            // Task taskJsb = Task.Run(async () => await Scraper.task_jsb());

            SoundUtils.Sound("일반", "to jsb");

            this.PerformLayout();
            this.Refresh(); //

            // StartPlanMonitoring();
            g.TradePlanManager = new New_Tradegy.Library.Deals.TradePlanManager(/* 필요 생성자 args */);

            g.TradePlanUiHandler = new New_Tradegy.Library.UI.TradePlanUiHandler(this);

            // ✅ Form1의 OnPlanTriggered가 아니라, UiHandler 메서드에 바로 연결
            g.TradePlanManager.PlanTriggered += g.TradePlanUiHandler.HandlePlanTriggered;

            InitPlanGrid();

            if (!g.test)
                StartRithmicPipeReceiver();

            //#region EOD 거래 로그 정리 (15:30 이후), Noftify
            //try
            //{
            //    var now = TimeUtils.GetKstNow();
            //    var t1530 = new DateTime(now.Year, now.Month, now.Day, 15, 30, 0);

            //    if (now >= t1530)
            //    {

            //        TradeEodProcessor.RunEodProcessAllPending(); // 남아있는 yyyyMMdd.txt 전부 처리
            //    }
            //}
            //catch
            //{
            //    // 정리 실패해도 프로그램 실행은 계속
            //}







            if (!_mouseHudStarted && !g.test)
            {
                _mouseHudStarted = true;
                // MouseHud.Start();   // ✅ 여기서 상주 HUD + 내부 타이머 시작
            }

            //#endregion

            //ShowBoardSafe();


            //_preOpenForm = new FormPreOpen();
            //_preOpenForm.Show();

            

        }

        public void StopPreOpenDownloaderAndHide()
        {
            if (_preOpenStopped)
                return;

            int hhmmss = Convert.ToInt32(DateTime.Now.ToString("HHmmss"));

            if (hhmmss < 90008)
                return;

            _preOpenStopped = true;

            _preOpenCts?.Cancel();

            if (_preOpenForm != null && !_preOpenForm.IsDisposed)
                _preOpenForm.Hide();

            // 저장은 여기서 호출
            //SavePreOpenDataAfterOpen();
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




        //public void RefreshEtfNq()
        //{
        //_etfNq?.Refresh(30, 18);
        //}



        public void UpdateHud()
        {
            var kospiData = g.StockRepo.TryGetDataOrNull("KODEX 레버리지");
            var kosdaqData = g.StockRepo.TryGetDataOrNull("KODEX 코스닥150레버리지");

            _indexHud.Update(kospiData, kosdaqData);
        }



        public void StartRithmicPipeReceiver()
        {
            // 이미 정상 동작 중이면 중복 시작 방지
            if (_rithmicTask != null &&
                !_rithmicTask.IsCompleted &&
                _rithmicCts != null &&
                !_rithmicCts.IsCancellationRequested)
            {
                return;
            }

            if (_rithmicCts != null)
            {
                try { _rithmicCts.Dispose(); } catch { }
                _rithmicCts = null;
            }

            _rithmicCts = new CancellationTokenSource();
            var ct = _rithmicCts.Token;

            _rithmicTask = Task.Run(async () =>
            {
                Debug.WriteLine("[RITHMIC PIPE] Receiver started.");

                while (!ct.IsCancellationRequested)
                {
                    NamedPipeClientStream pipe = null;
                    StreamReader reader = null;
                    CancellationTokenRegistration reg = default;

                    try
                    {
                        pipe = new NamedPipeClientStream(
                            ".",
                            "RithmicBridgePipe",
                            PipeDirection.In,
                            PipeOptions.Asynchronous);

                        // 서버가 늦게 떠도 계속 재시도
                        while (!ct.IsCancellationRequested && !pipe.IsConnected)
                        {
                            try
                            {
                                await pipe.ConnectAsync(800, ct);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (TimeoutException)
                            {
                                // retry
                            }
                            catch (IOException)
                            {
                                // retry
                            }

                            if (!pipe.IsConnected && !ct.IsCancellationRequested)
                                await Task.Delay(200, ct);
                        }

                        if (ct.IsCancellationRequested)
                            break;

                        if (!pipe.IsConnected)
                            continue;

                        Debug.WriteLine("[RITHMIC PIPE] Connected.");

                        // ReadLineAsync()를 취소 시 깨우기
                        reg = ct.Register(() =>
                        {
                            try { pipe.Dispose(); } catch { }
                        });

                        reader = new StreamReader(pipe);

                        while (!ct.IsCancellationRequested && pipe.IsConnected)
                        {
                            string line;

                            try
                            {
                                line = await reader.ReadLineAsync();
                            }
                            catch (ObjectDisposedException)
                            {
                                break;
                            }
                            catch (IOException)
                            {
                                break;
                            }

                            if (line == null)
                                break;

                            var parts = line.Split(',');

                            if (parts.Length < 4)
                                continue;

                            string type = parts[0];
                            string symbol = parts[1];

                            if (!double.TryParse(parts[2], out double price))
                                continue;

                            if (!int.TryParse(parts[3], out int size))
                                size = 0;

                            switch (type)
                            {
                                case "PRINT":
                                    UpdateNasdaqPrint(symbol, price, size);
                                    break;

                                case "BID":
                                    UpdateNasdaqBid(symbol, price, size);
                                    break;

                                case "ASK":
                                    UpdateNasdaqAsk(symbol, price, size);
                                    break;
                            }
                            // Beta Calculation
                            // Rolling Regression(가장 정석)
                            //    β = Cov(r_NQ, r_KOSPI) / Var(r_NQ)
                            // Ratio EMA(가볍고 빠름)
                            //    β_raw = r_KOSPI / r_NQ
                            //    β = EMA(β_raw)
                            // 가중 β(추천 ⭐)
                            //  if (| r_NQ | < threshold)
                            //      skip update;
                            //      β = EMA(r_KOSPI / r_NQ)

                            //beta: 현재 수준
                            //dBeta: 증가 / 감소 속도
                            //beta_vol : 흔들림 정도


                            //👉 print = 현재 힘
                            //👉 mid = 방향          (bid + ask) / 2.0;
                            //👉 spread = 환경,       ask - bid; 
                            //   0.25 정상, 0.5 수간 빔, 뉴스/급변 추격조심 기회
                            //       > 0.75 유동성붕괴, 얇은 시장, 큰 슬리피지 가능(시장에 사람이 없다)
                            //👉 imbalance = 의도   imbalance = (bidSize - askSize) / (bidSize + askSize);
                            // residual = beta * NQ - KOSPI
                            // NQ 영향인자 : 금리, 유동성/달러, 실적/성장 기대, 리스크(뉴스/지정학),
                            //              단기수급(프로그램, 옵션 포지션, 숏커버/롱청산

                            //원할 때 말해라 friend 😎
                            //SignalEngine / AlphaEngine / TradeManager
                            //한 번에 정리해준다


                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Debug.WriteLine("[RITHMIC PIPE] UnauthorizedAccessException: " + ex.Message);
                        // 관리자/일반 권한 mismatch 가능
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine("[RITHMIC PIPE] IOException: " + ex.Message);
                        // 서버 아직 없음 / 끊김 -> 재시도
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[RITHMIC PIPE] Unexpected exception: " + ex);
                    }
                    finally
                    {
                        try { reg.Dispose(); } catch { }
                        try { reader?.Dispose(); } catch { }
                        try { pipe?.Dispose(); } catch { }
                    }

                    if (!ct.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(200, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }

                Debug.WriteLine("[RITHMIC PIPE] Receiver stopped.");
            }, ct);
        }

        private static double _nqBid;
        private static double _nqAsk;
        private static double _nqPrint;
        private static int _nqBidSize;
        private static int _nqAskSize;
        private static int _nqPrintSize;

        private static void UpdateNasdaqPrint(string symbol, double price, int size)
        {
            _nqPrint = price;
            _nqPrintSize = size;

            UpdateNasdaqIndexFromPrice(price);
        }

        private static void UpdateNasdaqBid(string symbol, double price, int size)
        {
            _nqBid = price;
            _nqBidSize = size;
        }

        private static void UpdateNasdaqAsk(string symbol, double price, int size)
        {
            _nqAsk = price;
            _nqAskSize = size;
        }




        public void StopRithmicPipeReceiver()
        {
            var cts = _rithmicCts;
            if (cts == null)
                return;

            try
            {
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
            }
            catch { }
        }

        private static void UpdateNasdaqIndexFromPrice(double price)
        {
            double basis = g.RithmicBasis;
            if (basis <= 0 || price <= 0)
                return;

            MajorIndex.Instance.NasdaqIndex = (float)((price - basis) / basis * 100.0);
        }

        private bool TryGetFirstAvailableDouble(string json, out double value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (TryGetJsonDouble(json, key, out value))
                    return true;
            }

            value = 0;
            return false;
        }

        private bool TryGetJsonString(string json, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return false;

            string pattern = "\"" + key + "\"";
            int p = json.IndexOf(pattern, StringComparison.Ordinal);
            if (p < 0)
                return false;

            p = json.IndexOf(':', p + pattern.Length);
            if (p < 0)
                return false;

            p++;
            while (p < json.Length && char.IsWhiteSpace(json[p]))
                p++;

            if (p >= json.Length || json[p] != '"')
                return false;

            p++; // opening quote
            int start = p;

            while (p < json.Length)
            {
                if (json[p] == '"' && json[p - 1] != '\\')
                {
                    value = json.Substring(start, p - start);
                    return true;
                }
                p++;
            }

            return false;
        }

        // 기존에 이미 있으면 이건 생략해도 됨.
        // 소수점은 InvariantCulture 기준으로 파싱하는 쪽이 안전함.




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


        private static bool TryGetJsonDouble(string json, string key, out double value)
        {
            value = 0;

            // 패턴: "px":25797.25
            string token = "\"" + key + "\":";
            int i = json.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            i += token.Length;

            // 공백 스킵
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;

            // 숫자 구간 끝 찾기
            int j = i;
            while (j < json.Length)
            {
                char c = json[j];
                if ((c >= '0' && c <= '9') || c == '.' || c == '-' || c == '+'
                    || c == 'e' || c == 'E')
                {
                    j++;
                    continue;
                }
                break;
            }

            if (j <= i) return false;

            string num = json.Substring(i, j - i);
            return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }





        private void GeneratePanes()
        {
            // === Control Pane Initialization ===
            var controlTable = new DataTable();
            var controlDgv = new DataGridView();
            g.controlPane = new ControlPane(controlDgv, controlTable);  // Assume _view is public or internal
            controlDgv.Dock = DockStyle.None;
            this.Controls.Add(controlDgv);

            // === Trade Pane Initialization ===
            var tradeDgv = new DataGridView { Dock = DockStyle.None };
            var tradeDtb = new DataTable();
            g.tradePane = new TradePane(tradeDgv, tradeDtb);
            this.Controls.Add(tradeDgv);
            // Ensure control is ready
            tradeDgv.CreateControl();
            tradeDgv.PerformLayout();


            // Set widths only if column count matches expectation
            if (tradeDgv.Columns.Count >= 4)
            {

                int totalWidth = tradeDgv.Width;

                tradeDgv.Columns[0].Width = (int)(totalWidth * 0.20);
                tradeDgv.Columns[1].Width = (int)(totalWidth * 0.20);
                tradeDgv.Columns[2].Width = (int)(totalWidth * 0.25);
                tradeDgv.Columns[3].Width = (int)(totalWidth * 0.35);
            }


            // === Layout: 위치와 크기 수동 배치 ===



            int W = g.ChartManager.Chart1.Width / 10;
            int H = g.ChartManager.Chart1.Height / 3;
            int controlH = g.cellHeight * 3;
            controlDgv.Location = new Point(2 * W, H);
            controlDgv.Size = new Size(W, g.cellHeight * 3);

            tradeDgv.Location = new Point(2 * W, H + controlH);
            tradeDgv.Size = new Size(W, H - controlH);

            controlDgv.BringToFront();
            tradeDgv.BringToFront();


        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Debug.WriteLine($"KEY HIT d at {Environment.TickCount}  thread={Thread.CurrentThread.ManagedThreadId}");

            // 0. F1~F5, H → A/S/D/Z/X/C 변환
            //    Shift / Ctrl 조합은 그대로 보존
            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & (Keys.Shift | Keys.Control | Keys.Alt);

            switch (key)
            {
                case Keys.F5: key = Keys.A; break;
                case Keys.F9: key = Keys.S; break;
                case Keys.F10: key = Keys.D; break;
                case Keys.F11: key = Keys.Z; break;
                case Keys.F12: key = Keys.X; break;
                case Keys.H: key = Keys.C; break;
            }

            Keys mappedKeyData = key | mods;

            // 1. Global keybinding handler (test or real mode)
            if (KeyBindingManager.TryHandle(mappedKeyData))
                return true;

            // 2. If in real mode and NotifyBox is active, try NotifyBox.HandleKey(char)
            //    단, Shift/Ctrl/Alt 없는 순수 문자만 NotifyBox로 보냄
            //if (!g.test &&
            //    g.NotifyBox != null &&
            //    mods == Keys.None &&
            //    key >= Keys.A && key <= Keys.Z)
            //{
            //    char keyChar = (char)key;
            //    g.NotifyBox.HandleKey(char.ToLower(keyChar));
            //    return true;
            //}

            // 3. Let base class handle keys (e.g., navigation, focus)
            return base.ProcessCmdKey(ref msg, mappedKeyData);
        }
        static async Task runKOSPIUpdater()
        {
            KOSPIUpdater updater = new KOSPIUpdater();
            await updater.StartAsync();
            updater.Stop();
        }

        static async Task runKOSDAQUpdater()
        {
            KOSDAQUpdater updater = new KOSDAQUpdater();
            await updater.StartAsync();
            updater.Stop();
        }

        public class KOSPIUpdater
        {
            //private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
            private CpSvrNew7222 _cpsvrnew7222;
            private CancellationTokenSource _cancellationTokenSource;

            public KOSPIUpdater()
            {
                _cpsvrnew7222 = new CpSvrNew7222();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            public async Task StartAsync()
            {
                //Logger.Info("Starting KOSPI updater...");
                await RunPeriodicTask(_cancellationTokenSource.Token);
            }

            public void Stop()
            {
                //Logger.Info("Stopping KOSPI updater...");
                _cancellationTokenSource.Cancel();
                //LogManager.Shutdown();
            }

            private async Task RunPeriodicTask(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int HHmm = Convert.ToInt32(DateTime.Now.ToString("HHmm"));
                    if (HHmm < 903)
                    {
                        await Task.Delay(500);
                        continue;
                    }

                    if (wk.isWorkingHour())
                    {
                        _cpsvrnew7222.SetInputValue(0, 'B');
                        _cpsvrnew7222.SetInputValue(1, 0);
                        _cpsvrnew7222.SetInputValue(2, '1');
                        _cpsvrnew7222.SetInputValue(4, '2');

                        if (_cpsvrnew7222.GetDibStatus() == 1)
                        {
                            await Task.Delay(500, cancellationToken);
                            continue;
                        }

                        int retryCount = 0;
                        bool success = false;

                        while (retryCount < 3 && !success)
                        {
                            if (_cpsvrnew7222.BlockRequest() == 0)
                            {
                                int retail = (int)(_cpsvrnew7222.GetDataValue(1, 0) / 10.0); // 백만원 / 10 = 천만원
                                int inst = (int)(_cpsvrnew7222.GetDataValue(3, 0) / 10.0); // 백만원 / 10 = 천만원
                                int invest = (int)(_cpsvrnew7222.GetDataValue(4, 0) / 10.0); // 백만원 / 10 = 천만원
                                int pens = (int)(_cpsvrnew7222.GetDataValue(9, 0) / 10.0); // 백만원 / 10 = 천만원

                                // ✅ NEW: skip if all values are zero (only if before 09:10)
                                if (retail == 0 && inst == 0 && invest == 0 && pens == 0 && HHmm < 910)
                                {
                                    await Task.Delay(1000);
                                    continue;
                                }

                                MajorIndex.Instance.KospiRetailNetBuy = retail;
                                MajorIndex.Instance.KospiInstitutionNetBuy = inst;
                                MajorIndex.Instance.KospiInvestmentNetBuy = invest;
                                MajorIndex.Instance.KospiPensionNetBuy = pens;

                                success = true;
                            }
                            else
                            {
                                retryCount++;
                                await Task.Delay(500);
                            }
                        }
                        int remain = Form1.GetRemainRQ();
                        await RqPacing.SmartDelayAsync(
                            trCode: "7222-KOSPI",
                            HHmm: HHmm,
                            remainRq: remain,
                            lastSuccess: success,
                            message: success ? "ok" : "retry-or-fail",
                            allowSpeedUpTo3s: true,                   // ✅ 여유 있으면 3초까지 빨라짐
                            token: cancellationToken);
                    }




                    // Wait for 15 seconds before the next iteration
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        public class KOSDAQUpdater
        {
            //private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
            private CpSvrNew7222 _cpsvrnew7222;
            private CancellationTokenSource _cancellationTokenSource;

            public KOSDAQUpdater()
            {
                _cpsvrnew7222 = new CpSvrNew7222();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            public async Task StartAsync()
            {
                //Logger.Info("Starting KOSDAQ updater...");
                await RunPeriodicTask(_cancellationTokenSource.Token);
            }

            public void Stop()
            {
                //Logger.Info("Stopping KOSDAQ updater...");
                _cancellationTokenSource.Cancel();
                //LogManager.Shutdown();
            }
            private async Task RunPeriodicTask(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        int HHmm = Convert.ToInt32(DateTime.Now.ToString("HHmm"));

                        if (HHmm < 903)
                        {
                            await Task.Delay(500, cancellationToken);
                            continue;
                        }

                        if (wk.isWorkingHour())
                        {
                            _cpsvrnew7222.SetInputValue(0, 'C');
                            _cpsvrnew7222.SetInputValue(1, 0);
                            _cpsvrnew7222.SetInputValue(2, '1');
                            _cpsvrnew7222.SetInputValue(4, '2');

                            if (_cpsvrnew7222.GetDibStatus() == 1)
                            {
                                await Task.Delay(500, cancellationToken);
                                continue;
                            }

                            int retryCount = 0;
                            bool success = false;

                            while (retryCount < 3 && !success)
                            {
                                if (_cpsvrnew7222.BlockRequest() == 0)
                                {
                                    int retail = (int)(_cpsvrnew7222.GetDataValue(1, 0) / 10.0); // 백만원 / 10 = 천만원
                                    int inst = (int)(_cpsvrnew7222.GetDataValue(3, 0) / 10.0); // 백만원 / 10 = 천만원
                                    int invest = (int)(_cpsvrnew7222.GetDataValue(4, 0) / 10.0); // 백만원 / 10 = 천만원
                                    int pens = (int)(_cpsvrnew7222.GetDataValue(9, 0) / 10.0); // 백만원 / 10 = 천만원

                                    // ✅ Skip if all values are zero and it's still early
                                    if (retail == 0 && inst == 0 && invest == 0 && pens == 0 && HHmm < 910)
                                    {
                                        await Task.Delay(1000, cancellationToken);
                                        continue;
                                    }

                                    MajorIndex.Instance.KosdaqRetailNetBuy = retail;
                                    MajorIndex.Instance.KosdaqInstitutionNetBuy = inst;
                                    MajorIndex.Instance.KosdaqInvestmentNetBuy = invest;
                                    MajorIndex.Instance.KosdaqPensionNetBuy = pens;

                                    success = true;
                                }
                                else
                                {
                                    retryCount++;
                                    await Task.Delay(500, cancellationToken);
                                }
                            }

                            if (!success)
                            {
                                //Logger.Error("Failed to fetch KOSDAQ data after 3 retries.");
                            }
                            int remain = Form1.GetRemainRQ();
                            await RqPacing.SmartDelayAsync(
                                trCode: "7222-KOSDAQ",
                                HHmm: HHmm,
                                remainRq: remain,
                                lastSuccess: success,
                                message: success ? "ok" : "retry-or-fail",
                                allowSpeedUpTo3s: true,
                                token: cancellationToken);
                        }
                    }
                    catch (COMException comEx)
                    {
                        //Logger.Error(comEx, "COM error occurred while fetching KOSDAQ data.");
                    }
                    catch (Exception ex)
                    {
                        //Logger.Error(ex, "An error occurred while fetching KOSDAQ data.");
                    }
                }
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
        public static int GetRemainTR()
        {
            if (_cpcybos == null)
                return 20;
            return _cpcybos.GetLimitRemainCount(CPUTILLib.LIMIT_TYPE.LT_TRADE_REQUEST);     // 20건의 요청으로 제한
        }
        public static int GetRemainSB()
        {
            if (_cpcybos == null)
                return 400;
            return _cpcybos.GetLimitRemainCount(CPUTILLib.LIMIT_TYPE.LT_SUBSCRIBE);         // 400건의 요청으로 제한   
        }

        public static void task_RQTRSB()
        {
            while (true)
            {
                if (!wk.isWorkingHour())
                {
                    Thread.Sleep(1000 * 10);
                    continue;
                }

                if (GetRemainRQ() < 1)
                {
                    SoundUtils.Sound("일반", "no request");
                }
                if (GetRemainTR() == 0)
                {
                    SoundUtils.Sound("일반", "no trade");
                }
                if (GetRemainSB() == 0)
                {
                    SoundUtils.Sound("일반", "no subscribe");
                }
                Thread.Sleep(100);
            }
        }


        //        안녕하세요.Plus 담당자입니다.
        //1초 마다 다운로드하게 한다는 말이 1초마다 BlockRequest() 를 요청한다는 의미인지요?
        //조회시에는[시간대별 투자자매매추이] CpSysDib.CpSvrNew7222를 사용하고
        //그 이후로는 이에 매핑되는 실시간API가 있는지를  찾아서 실시간 업데이트 로직으로 구현함이 맞아보입니다.
        //장 시작되고 ( 9시부터)  요청 주기를 길게 해주거나(30초나 1분 등) 실시간 API를 이용한 로직을 이용해주시길 바랍니다.
        //코스피 매수액(개인, 외인, 기관)

        private void subscribe_8091S()
        {

            _cpsvr8091s = new DSCBO1Lib.CpSvr8091S();
            _cpsvr8091s.Received += new DSCBO1Lib._IDibEvents_ReceivedEventHandler(_cpsvr8091s_Received);

            _cpsvr8091s.SetInputValue(0, "888"); //회원사 코드(외국계 전체는 888,회원사 전체 "*", 044 메릴린치, 042 CS)
            _cpsvr8091s.SetInputValue(1, "*"); //종목 코드 [전체 종목에 대한 요청은 "*"]

            _cpsvr8091s.Subscribe();
        }

        /// <summary>
        /// not used also blocked on 20250504
        /// </summary>
        private void _cpsvr8091s_Received()
        {
            short 시간 = _cpsvr8091s.GetHeaderValue(0);
            string 회원사명 = _cpsvr8091s.GetHeaderValue(1);
            string 종목코드 = _cpsvr8091s.GetHeaderValue(2);

            char 매수매도구분 = (char)_cpsvr8091s.GetHeaderValue(4);
            long 매수매도량 = _cpsvr8091s.GetHeaderValue(5);
            long 순매수 = _cpsvr8091s.GetHeaderValue(6);
            char 순매수부호 = (char)_cpsvr8091s.GetHeaderValue(7);
            string 종목 = _cpsvr8091s.GetHeaderValue(3);
            long 외국계순매수량 = _cpsvr8091s.GetHeaderValue(8);


            var data = g.StockRepo.TryGetDataOrNull(종목);

            if (data != null && data.Api != null && data.Api.nrow > 0)
            {
                data.Api.당일외인순매수량 = (int)외국계순매수량; // 외국계 순매수량 업데이트
                data.Api.x[data.Api.nrow - 1, 5] = (int)외국계순매수량; // 외국계 순매수량 업데이트
            }

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



        private void ChangeMainTitleConnection()
        {
            _cpcybos = null;
            _cpcybos = new CPUTILLib.CpCybos();
            System.Threading.Thread.Sleep(200); // 약간의 대기
            if (_cpcybos.IsConnect == 1)
            {
                if (_timerConnection != null)
                {
                    _timerConnection.Stop();
                    _timerConnection.Dispose();
                    _timerConnection = null;
                }

                _timerCount = 0;

                // menuStrip1.BackColor = Color.FromArgb(228, 254, 226);

                Invoke(new MethodInvoker(ConnectionCompleted));

                MessageBox.Show("대신증권 플러스에 연결되었습니다.");

            }
            else
            {
                this.Text = "대신증권 플러스 Sample for C# (연결 안됨)";

                if (_timerCount == 0)
                {
                    //DialogConnection dialog = new DialogConnection();
                    //dialog.SetParent(this);
                    //dialog.ShowDialog(this);
                }
            }
        }
        private System.Windows.Forms.Timer _cybosRetryTimer;
        private void StartCybosRetry()
        {
            if (_cybosRetryTimer != null) return;

            _cybosRetryTimer = new System.Windows.Forms.Timer();
            _cybosRetryTimer.Interval = 1500;
            _cybosRetryTimer.Tick += (s, e) =>
            {
                int conn = _cpcybos.IsConnect;
                if (conn == 1)
                {
                    g.connected = true;
                    _cybosRetryTimer.Stop();
                    _cybosRetryTimer.Dispose();
                    _cybosRetryTimer = null;

                    // 여기서 구독/다운로더 재시작(필요 시)
                    // MarketEyeBatchDownloader.Restart();
                }
                else
                {
                    g.connected = false;
                    // 타이틀/로그만 업데이트
                }
            };
            _cybosRetryTimer.Start();
        }

        public void ConnectionCompleted()
        {
            this.Text = "대신증권 플러스 Sample for C# (연결 완료)";
        }

        private static void CpCybos_OnDisconnect()
        {
            _cpcybos = null;
            MessageBox.Show("대신증권 플러스 연결이 종료되었습니다.");
        }


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
