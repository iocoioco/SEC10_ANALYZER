using New_Tradegy.Library.Models;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace New_Tradegy.Library.Deals
{
    // ===========================================================
    // 1. TradePlanLine
    // ===========================================================
    public class TradePlanLine
    {
        // ===== 기본 정보 =====
        public bool On { get; set; }
        public string Symbol { get; set; }
        public string Side { get; set; }          // "매수" / "매도"
        public int BasePrice { get; set; }
        public int AmountK { get; set; }
        public int Quantity { get; set; }

        // ===== 🔥 % 트리거 (x100 정수) =====
        // 50 => 0.5%
        // 레버리지: 5 => 0.05%
        public int TriggerPctX100 { get; set; } = 50;

        // ===== UI 표시용 =====
        public double Foeton { get; set; }
        public double Multiplier { get; set; }

        public DateTime LastModified { get; set; }
        public int DisplayNo { get; set; }

        // ===== 감시 =====
        public bool MonitorEnabled { get; set; } = false;
        public DateTime LastAlertTime { get; set; } = DateTime.MinValue;
        public int CooldownSeconds { get; set; } = 5;
    }

    internal sealed class HoldingRiskState
    {
        public string Symbol;
        public int PeakBidPrice;
        public DateTime EntryTime;
        public bool AutoExitTriggered;
        public DateTime LastWarnTime;
        public bool WarningActive;

        // ---- 지수 음성 알림용 추가 ----
        public DateTime LastIndexVoiceTime;
        public int LastIndexVoiceBidPrice;

        public void Reset()
        {
            PeakBidPrice = 0;
            EntryTime = DateTime.MinValue;
            AutoExitTriggered = false;
            LastWarnTime = DateTime.MinValue;
            WarningActive = false;

            // 지수 음성 상태 초기화
            LastIndexVoiceTime = DateTime.MinValue;
            LastIndexVoiceBidPrice = 0;
        }
    }

    // ===========================================================
    // 3. TradePlanManager
    // ===========================================================
    public class TradePlanManager //: ITradePlanManager
    {
        private readonly Dictionary<string, HoldingRiskState> _holdingRiskMap
    = new Dictionary<string, HoldingRiskState>(StringComparer.OrdinalIgnoreCase);

        public event Action<TradePlanLine, DateTime, int, int, int, int> PlanTriggered;
        public BindingList<TradePlanLine> Plans { get; } = new BindingList<TradePlanLine>();

        private readonly Dictionary<string, SymbolState> _states =
            new Dictionary<string, SymbolState>();

        private HoldingRiskState GetOrCreateHoldingRiskState(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return null;

            if (!_holdingRiskMap.TryGetValue(symbol, out var state))
            {
                state = new HoldingRiskState
                {
                    Symbol = symbol
                };
                _holdingRiskMap[symbol] = state;
            }

            return state;
        }

        private static bool IsAutoStopIndexEtf(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return false;

            return symbol.Equals("KODEX 레버리지", StringComparison.OrdinalIgnoreCase)
                || symbol.Equals("KODEX 코스닥150레버리지", StringComparison.OrdinalIgnoreCase);
        }

        private void CheckHoldingRisk(
            DateTime now,
            string symbol,
            int askPrice,
            int bidPrice,
            int askQty,
            int bidQty)
        {
            if (string.IsNullOrEmpty(symbol))
                return;

            var data = g.StockRepo.TryGetDataOrNull(symbol);
            if (data == null)
                return;

            var risk = GetOrCreateHoldingRiskState(symbol);
            if (risk == null)
                return;

            int qty = 0;
            int avgPrice = 0;

            try
            {
                var deal = data.Deal;
                if (deal == null)
                    return;

                qty = deal.보유량;
                avgPrice = (int)deal.장부가;
            }
            catch
            {
                return;
            }

            // 보유 없으면 상태 초기화
            if (qty <= 0)
            {
                risk.Reset();
                return;
            }

            // 최초 보유 진입 시점 초기화
            if (risk.EntryTime == DateTime.MinValue)
            {
                risk.EntryTime = now;
                risk.PeakBidPrice = bidPrice > 0 ? bidPrice : avgPrice;
                risk.AutoExitTriggered = false;
                risk.LastWarnTime = DateTime.MinValue;

                // 지수 음성 초기값
                risk.LastIndexVoiceTime = now;
                risk.LastIndexVoiceBidPrice = bidPrice > 0 ? bidPrice : avgPrice;
            }

            // peak 갱신
            if (bidPrice > risk.PeakBidPrice)
                risk.PeakBidPrice = bidPrice;

            // 진입 직후 보호시간
            if ((now - risk.EntryTime).TotalSeconds < 2)
                return;





            // -------- KODEX 2종목: 자동손절(시장가 매도) --------
            if (IsAutoStopIndexEtf(symbol))
            {
                // 1초 방향음
                if ((now - risk.LastIndexVoiceTime).TotalSeconds >= 1.0)
                {
                    int prev = risk.LastIndexVoiceBidPrice;
                    int curr = bidPrice > 0 ? bidPrice : prev;

                    if (curr > prev)
                        SoundUtils.Sound("Deal", "u");
                    else if (curr < prev)
                        SoundUtils.Sound("Deal", "d");

                    risk.LastIndexVoiceBidPrice = curr;
                    risk.LastIndexVoiceTime = now;
                }

                if (!risk.AutoExitTriggered && !g.confirm_sell)
                {
                    bool sold = CheckHoldingRisk_SellIndexAtMarketPrice(
                        now,
                        symbol,
                        askPrice,
                        bidPrice,
                        askQty,
                        bidQty);

                    if (sold)
                        risk.AutoExitTriggered = true;
                }
                return;
            }




            // -------- 일반 종목: 기존 경고 유지 --------
            CheckNormalStockWarning(
                now,
                symbol,
                askPrice,
                bidPrice,
                askQty,
                bidQty,
                qty,
                avgPrice,
                risk);
        }

        private bool CheckHoldingRisk_SellIndexAtMarketPrice(
            DateTime now,
            string symbol,
            int askPrice,
            int bidPrice,
            int askQty,
            int bidQty)
        {
            if (string.IsNullOrEmpty(symbol))
                return false;

            var data = g.StockRepo.TryGetDataOrNull(symbol);
            if (data == null)
                return false;

            var risk = GetOrCreateHoldingRiskState(symbol);
            if (risk == null)
                return false;

            int qty = 0;
            int avgPrice = 0;

            try
            {
                var deal = data.Deal;
                if (deal == null)
                    return false;

                qty = deal.보유량;
                avgPrice = (int)deal.장부가;
            }
            catch
            {
                return false;
            }

            if (qty <= 0)
                return false;

            if (risk.AutoExitTriggered)
                return false;

            // 예시 손절가 계산
            int stopPrice = (int)(avgPrice * 0.9975);   // <- 친구 기준식으로 교체

            if (bidPrice > 0 && bidPrice <= stopPrice)
            {
                risk.AutoExitTriggered = true;

                // 실제 주문 함수로 연결
                SendFastSellOrder(symbol, qty, bidPrice);

                return true;   // 🔥 실제 매도 발생 시만 true
            }

            return false;      // 🔥 나머지는 전부 false
        }

        private void CheckNormalStockWarning( 
            DateTime now,
            string symbol,
            int askPrice,
            int bidPrice,
            int askQty,
            int bidQty,
            int qty,
            int avgPrice,
            HoldingRiskState risk)
        {
            if (avgPrice <= 0 || bidPrice <= 0 || risk == null)
                return;

            // 손실률(%)
            double lossPercent = ((double)(bidPrice - avgPrice) / avgPrice) * 100.0;

            // 경고 기준
            const double warnThreshold = -0.30;

            // 1) 손실 구간 벗어나면 조용 + 상태 해제
            if (lossPercent > warnThreshold)
            {
                risk.WarningActive = false;
                return;
            }

            // 2) 처음 손실 구간 진입이면 즉시 1회 경고
            if (!risk.WarningActive)
            {
                risk.WarningActive = true;
                risk.LastWarnTime = now;

                RaiseNormalStockWarning(
                    symbol,
                    now,
                    bidPrice,
                    qty,
                    lossPercent);

                return;
            }

            // 3) 이미 손실 구간이면 10초마다 반복 경고
            if ((now - risk.LastWarnTime).TotalSeconds < 10)
                return;

            risk.LastWarnTime = now;

            RaiseNormalStockWarning(
                symbol,
                now,
                bidPrice,
                qty,
                lossPercent);
        }

        private void SendFastSellOrder(string symbol, int qty, int bidPrice)
        {
            if (string.IsNullOrEmpty(symbol) || qty <= 0)
                return;

            try
            {
                Utils.SoundUtils.Sound("Deal", "Kodex Sold");

                // 지수 자동손절은 체결 우선
                // 03 = 시장가
                DealManager.DealExec("매도", symbol, 0, qty, "03");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AUTO-SELL-ERROR] {symbol} qty={qty} bid={bidPrice} ex={ex.Message}");
            }
        }

        private void RaiseNormalStockWarning(
            string symbol,
            DateTime now,
            int bidPrice,
            int qty,
            double lossPercent)
        {
            try
            {
                Utils.SoundUtils.Sound("Deal", "Sell");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WARN-ERROR] {symbol} {ex.Message}");
            }
        }


        // ---------------------------
        // 플랜 추가
        // ---------------------------
        public void AddPlanFromOrderBook(string symbol, string side, int basePrice, int defaultAmountK)
        {
            //System.Diagnostics.Debugger.Break();
            int defaultTrigger = IsEtfOrLeveraged(symbol) ? 5 : 50;

            var line = new TradePlanLine
            {
                On = true,
                Symbol = symbol,
                Side = side,
                BasePrice = basePrice,
                AmountK = defaultAmountK,
                Quantity = 0,
                LastModified = DateTime.Now,
                TriggerPctX100 = defaultTrigger
            };

            Plans.Add(line);
        }

        public static bool IsEtfOrLeveraged(string symbol)
        {
            return symbol.StartsWith("KODEX")
                || symbol.StartsWith("TIGER")
                || symbol.Contains("레버")
                || symbol.Contains("인버");
        }

        // ---------------------------
        // 호가 틱 입력
        // ---------------------------
        public void OnBookTick(DateTime now,
                       string symbol,
                       int askPrice,
                       int bidPrice,
                       int askQty,
                       int bidQty)
        {
            if (string.IsNullOrEmpty(symbol))
                return;

            int midPrice = (askPrice + bidPrice) / 2;
            var state = GetOrCreateSymbolState(symbol, midPrice);

            state.Update(now, midPrice);

            // 1) 보유종목 리스크 검사
            CheckHoldingRisk(now, symbol, askPrice, bidPrice, askQty, bidQty);

            // 2) 기존 Plan 감시
            //foreach (var plan in Plans.Where(p => p.On && p.MonitorEnabled && p.Symbol == symbol))
            //{
            //    // 쿨다운
            //    if ((now - plan.LastAlertTime).TotalSeconds < plan.CooldownSeconds)
            //        continue;

            //    var data = g.StockRepo.TryGetDataOrNull(symbol);
            //    if (CheckConditions(plan, state, askPrice, bidPrice, askQty, bidQty, data))
            //    {
            //        plan.LastAlertTime = now;

            //        PlanTriggered?.Invoke(
            //            plan,
            //            now,
            //            askPrice,
            //            bidPrice,
            //            askQty,
            //            bidQty
            //        );
            //    }
            //}
        }

        // ---------------------------
        // 상태 관리
        // ---------------------------
        private SymbolState GetOrCreateSymbolState(string symbol, int refPrice)
        {
            if (!_states.TryGetValue(symbol, out var state))
            {
                int tickSize = DealUtils.GetTick(symbol, refPrice); // 참고용
                state = new SymbolState(tickSize);
                _states[symbol] = state;
            }
            return state;
        }

        // ---------------------------
        // 🔥 % 기반 트리거 조건 // WARN
        // ---------------------------
        private bool CheckConditions(
            TradePlanLine plan,
            SymbolState state,
            int askPrice,
            int bidPrice,
            int askQty,
            int bidQty,
            StockData data
              )
        {
            // 1) 가격 일치
            if (plan.Side == "매수")
            {
                if (plan.BasePrice != askPrice) return false;
                // 2) 잔량
                if (plan.Quantity > 0 && askQty < plan.Quantity) return false;
            }
            else if (plan.Side == "매도")
            {
                if (plan.BasePrice != bidPrice) return false;
                if (plan.Quantity > 0 && bidQty < plan.Quantity) return false;
            }
            else
            {
                return false;
            }

            // 3) 변동률 트리거 (기존 유지)
            double thrPct = plan.TriggerPctX100 / 100.0;
            if (plan.Side == "매수") // “3분 저점 대비 상승률”
            {
                if (state.VarFromLowPct > thrPct) return true;
            }
            else // “3분 고점 대비 하락률 %”
            {
                if (state.VarFromHighPct > thrPct) return true;
            }

            // 4) Foeton
            if (plan.Foeton > 0)
            {
                double foetonNow = data.Api.분프로천[0] + data.Api.분외인천[0];
                if (foetonNow < plan.Foeton) return false;
            }

            // 5) Multiplier
            if (plan.Multiplier > 0)
            {
                if (data.Api.분배수차[0] < plan.Multiplier) return false;
            }

            return true;
        }
    }

    // ===========================================================
    // 4. SymbolState
    // ===========================================================
    public class SymbolState
    {
        private readonly Queue<(DateTime time, int price)> _prices =
            new Queue<(DateTime time, int price)>();
        public int TickSize { get; }
        public int LowPrice { get; private set; }
        public int HighPrice { get; private set; }

        // 🔥 % 기반
        public double VarFromLowPct { get; private set; }
        public double VarFromHighPct { get; private set; }

        public SymbolState(int tickSize)
        {
            TickSize = tickSize;
            LowPrice = int.MaxValue;
            HighPrice = 0;
        }

        //        최근 3분 가격 중 최저/최고를 갱신하고,
        //현재가가 저점 대비 얼마나 올랐는지 / 고점 대비 얼마나 내려왔는지를 %로 계산
        public void Update(DateTime now, int lastPrice) // midPrice passed to lastPrice
        {
            _prices.Enqueue((now, lastPrice));

            var cutoff = now.AddMinutes(-3);
            while (_prices.Count > 0 && _prices.Peek().time < cutoff)
                _prices.Dequeue();

            if (_prices.Count > 0)
            {
                LowPrice = _prices.Min(p => p.price);
                HighPrice = _prices.Max(p => p.price);
            }
            else
            {
                LowPrice = lastPrice;
                HighPrice = lastPrice;
            }

            if (LowPrice > 0)
                VarFromLowPct = 100.0 * (lastPrice - LowPrice) / LowPrice;
            else
                VarFromLowPct = 0;

            if (HighPrice > 0)
                VarFromHighPct = 100.0 * (HighPrice - lastPrice) / HighPrice;
            else
                VarFromHighPct = 0;
        }
    }
}
