using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using New_Tradegy.Library.UI;  // Make sure AlertForm.cs is added to your project

namespace New_Tradegy.Library.Utils
{
    public class PingAndSpeedMonitor
    {
        private readonly string _host;
        private readonly string _logFilePath;
        private readonly string _wavFilePath;
        private readonly int _logIntervalSeconds;
        private readonly int _pingThresholdMs;
        private readonly double _speedThresholdMbps;
        private readonly string _testDownloadUrl = "https://speed.hetzner.de/1MB.bin";

        private CancellationTokenSource _cts;
        private DateTime _lastLogTime = DateTime.MinValue;
        private DateTime _lastAlertTime = DateTime.MinValue;
        private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(3);
        private int _consecutiveBadCount = 0;
        private readonly int _maxBadBeforeAlert = 2;

        //private AlertForm alertForm;

        public PingAndSpeedMonitor(
            string host = "daishin.co.kr",
            string logFilePath = "ping_log.txt",
            string wavFilePath = @"Resources\alert.wav",
            int logIntervalSeconds = 600,
            int pingThresholdMs = 300,
            double speedThresholdMbps = 10.0)
        {
            _host = host;
            _logFilePath = logFilePath;
            _wavFilePath = wavFilePath;
            _logIntervalSeconds = logIntervalSeconds;
            _pingThresholdMs = pingThresholdMs;
            _speedThresholdMbps = speedThresholdMbps;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string logLine;
                bool isBad = false;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Ping
                long pingMs = -1;
                try
                {
                    PingReply reply = await new Ping().SendPingAsync(_host); 
                    if (reply.Status == IPStatus.Success)
                    {
                        pingMs = reply.RoundtripTime;
                        if (pingMs > _pingThresholdMs)
                            isBad = true;
                    }
                    else
                    {
                        isBad = true;
                    }
                }
                catch
                {
                    isBad = true;
                }

                // Speed
                double mbps = -1;
                try
                {
                    using (var client = new HttpClient())
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        byte[] data = await client.GetByteArrayAsync(_testDownloadUrl);
                        sw.Stop();

                        double sec = sw.Elapsed.TotalSeconds;
                        double bits = data.Length * 8;
                        mbps = bits / 1_000_000.0 / sec;

                        if (mbps < _speedThresholdMbps)
                            isBad = true;
                    }
                }
                catch
                {
                    isBad = true;
                }

                // Handle alert logic
                if (isBad)
                    _consecutiveBadCount++;
                else
                    _consecutiveBadCount = 0;

                bool shouldAlert = _consecutiveBadCount >= _maxBadBeforeAlert &&
                                   (DateTime.Now - _lastAlertTime) > _alertCooldown;

                string message = $"⚠️ Ping: {(pingMs >= 0 ? pingMs + " ms" : "FAIL")}\nSpeed: {(mbps >= 0 ? mbps.ToString("F2") + " Mbps" : "FAIL")}";

                if (shouldAlert)
                {
                    //ShowAlertForm(message);
                    //PlayAlertSound();
                    SoundUtils.Sound("일반", "WiFi Trouble");
                    _lastAlertTime = DateTime.Now;
                    _consecutiveBadCount = 0;
                }
                else if (alertForm != null && !alertForm.IsDisposed)
                {
                    alertForm.UpdateText(message);
                }

                logLine = $"{timestamp} | Ping: {(pingMs >= 0 ? pingMs + " ms" : "FAIL")} | Download: {(mbps >= 0 ? mbps.ToString("F2") + " Mbps" : "FAIL")}";

                if ((DateTime.Now - _lastLogTime).TotalSeconds >= _logIntervalSeconds)
                {
                    AppendLog(logLine);
                    _lastLogTime = DateTime.Now;
                }

                //Console.WriteLine(logLine);
                await Task.Delay(5000, token);  // Check every 5 seconds
            }
        }

        private void AppendLog(string line)
        {
            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                //Console.WriteLine("Log error: " + ex.Message);
            }
        }

    }
}
