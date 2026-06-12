using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Utils;
using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static New_Tradegy.Library.Deals.QuickTradePopup;

namespace New_Tradegy.Library.UI
{
    public sealed class TradePlanUiHandler
    {

        private readonly Form1 _form;
        private readonly SemaphoreSlim _popupLock = new SemaphoreSlim(1, 1);
        private bool _popupActive;

        public TradePlanUiHandler(Form1 form)
        {
            _form = form;
        }

        // ✅ TradePlanManager.PlanTriggered 이벤트에 바로 연결할 메서드
        public async void HandlePlanTriggered(TradePlanLine plan, DateTime now, int askPrice, int bidPrice, int askQty, int bidQty)
        {
            // async void는 이벤트 핸들러에서만 OK
            await ExecutePlanAsync(plan, askPrice, bidPrice);
        }

        private async Task ExecutePlanAsync(TradePlanLine plan, int askPrice, int bidPrice)
        {
            int cd = plan.CooldownSeconds <= 0 ? 10 : plan.CooldownSeconds;
            if ((DateTime.Now - plan.LastAlertTime).TotalSeconds < cd) return;
            plan.LastAlertTime = DateTime.Now;

            if (!_popupLock.Wait(0))
            {
                SoundUtils.Sound("Deal", "bjs");
                (_form as New_Tradegy.Form1)?.SetPlanColorQueuedPublic(plan, true);  // ★ 팝업 못 뜨는 대기 색
                return;
            }

            _popupActive = true;
            (_form as New_Tradegy.Form1)?.SetPlanColorActivePublic(plan, true);

            try
            {
                bool isBuy = plan.Side == "매수";
                string symbol = plan.Symbol;

               

                // ✅ 가격 결정 (BasePrice 우선, 없으면 ask/bid)
                int price = plan.BasePrice > 0
                    ? plan.BasePrice
                    : (isBuy ? askPrice : bidPrice);

                if (price <= 0)
                    return;

                // ✅ 수량 계산 (AmountK 기반)
                int qty = 0;

                if (plan.AmountK > 0)
                    qty = (int)((plan.AmountK * 10000L) / price);

                if (qty <= 0)
                    qty = 1;

                // ✅ 일회거래액 캡 (금액 기준 조정)
                long amount = (long)qty * price;
                if (amount > g.일회거래액)
                {
                    qty = (int)(g.일회거래액 / price);
                    if (qty <= 0)
                        return;
                }

                var old = g.PopupCurrent;
                // ✅ 매수: HUD+Sound만 하고 끝 (A 정책)
                if (isBuy && old != null)
                {
                    NotifyBuyPlanHudSound(symbol, price, qty, plan); // 네가 만들 메소드
                    return;
                }

                // ✅ 매도: 떠있는 팝업 있으면 먼저 닫고, 매도 팝업 진행
                
                if (old != null)
                {
                    try
                    {
                        // UI thread 아니면 Invoke 필요할 수 있음(너 구조에 따라)
                        old.Close();
                    }
                    catch { }
                }

                

                // result 처리(Confirm이면 CommitDeal 등) - 기존 코드 유지

                var result = await QuickTradePopup.ShowAsync(
                    symbol: symbol,
                    price: price,
                    qty: qty,
                    offsetX: isBuy ? +100 : -100,
                    offsetY: -50,
                    timeoutMs: 30000,
                    sideColor: isBuy
                        ? Color.FromArgb(230, 255, 230)
                        : Color.FromArgb(255, 230, 230),
                    reason: null
                );

                if (result.Kind == PopupResultKind.Confirm)
                {
                    DealManager.CommitDeal(
                        isBuy ? "매수" : "매도",
                        symbol,
                        (result.Price, result.Qty),
                        "plan"
                    );
                }
                else if (result.Kind == PopupResultKind.CancelKeep)
                {
                    // 관찰 유지 (아무 것도 안 함)
                    return;
                }
                else
                {
                    // CancelRemove: 관심 제거/플랜 제거 중 택1
                    // g.StockManager.RemoveInterestedWithBid(symbol);
                    return;
                }
            }
            finally
            {
                (_form as New_Tradegy.Form1)?.SetPlanColorActivePublic(plan, false);
                (_form as New_Tradegy.Form1)?.SetPlanColorQueuedPublic(plan, false);

                

                // ✅ 기존 락/플래그 쓰던 구조면 아래는 제거(또는 유지 시 중복 주의)
                _popupActive = false;
                _popupLock.Release();
            }
        }

        private void NotifyBuyPlanHudSound(
            string symbol,
            int price,
            int qty,
            TradePlanLine plan)
        {
            try
            {
                // 🔔 1️⃣ 소리
                Utils.SoundUtils.Sound("", "Buy Trade Popup");

                // 🟢 2️⃣ Center HUD 메시지 구성
                string text =
                    $"[BUY PLAN]\n" +
                    $"{symbol}  {price:N0}  x {qty:N0}";

                // ⏱ 0.8초 표시 (원하면 조정)
                CenterHudForm.Show(
                    text,
                    durationMs: 800,
                    fontSize: 42f);
            }
            catch
            {
                // HUD/사운드 실패해도 로직은 계속
            }
        }
    }
}
