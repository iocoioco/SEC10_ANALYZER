using New_Tradegy.Library.UI;

public class IndexRangeTracker
{
    private int lastIndexKospi = 0;
    private int lastIndexKosdaq = 0;

    public void CheckIndexAndSound(int indexPrice, string market)
    {
        int lastIndex = (market == "Kospi") ? lastIndexKospi : lastIndexKosdaq;

        // 최초 값이면 기준만 세팅하고 종료
        if (lastIndex == 0)
        {     
            if (market == "Kospi")
                lastIndexKospi = indexPrice;
            else
                lastIndexKosdaq = indexPrice;
            return;
        }

        int delta = indexPrice - lastIndex;
        int steps = delta / 10;   // 0.1% 단위 (부호 유지)

        if (steps != 0)
        {
            string code = (market == "Kospi") ? "KP" : "KQ";
            string arrow = steps > 0 ? "⬆" : "⬇";

            // 실제 퍼센트 (indexPrice가 *100 기준이라면)
            double pct = indexPrice / 100.0;

            string msg = $"{code} {pct:F2}% {arrow}";

            // ding + HUD 표시
            // MouseHud.ShowExtra(msg, 3000);

            // 기준 업데이트 (한 번에 점프해도 맞추기)
            if (market == "Kospi")
                lastIndexKospi += steps * 10;
            else
                lastIndexKosdaq += steps * 10;
        }
    }
}