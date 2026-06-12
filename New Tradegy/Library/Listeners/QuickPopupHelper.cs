using New_Tradegy.Library.Deals;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static New_Tradegy.Library.Deals.QuickTradePopup;

namespace New_Tradegy.Library.Listeners
{
    public static class QuickPopupHelper
    {
        public static async Task<PopupResultKind> ConfirmByQuickPopupAsync(
            bool isBuy,
            string stock,
            int price,
            int qty,
            string reason,
            string commitSource,
            bool removeInterestedOnCancelRemove = true)
        {
            var sideColor = isBuy
                ? Color.FromArgb(180, 0, 170, 0)
                : Color.FromArgb(180, 200, 0, 0);

            int timeoutMs =
                commitSource == "impulse"
                ? 5000
                : 15000;

            var result = await QuickTradePopup.ShowAsync(
                symbol: stock,
                price: price,
                qty: qty,
                offsetX: isBuy ? +100 : -100,
                offsetY: -50,
                timeoutMs: timeoutMs,
                sideColor: sideColor,
                reason: reason
            );

            return result.Kind;
        }
    }
}
