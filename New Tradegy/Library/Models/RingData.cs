using System;

namespace New_Tradegy.Library.Models
{
    public struct RingData
    {
        public long TimeMs;

        // 섹터 표시용 (있으면 좋고 없어도 계산엔 무관)
        public int Price100;

        // 누적(원래 api.x에서 가져오는 누적들)
        public int ProCum;
        public int ForCum;
        public int InstCum;
        public int MoneyCum;

        // ✅ 핵심: 누적 체결액 (10M 단위)
        public long BuyAmtCum10M;   // 누적 매수체결액(천만원 단위)
        public long SellAmtCum10M;  // 누적 매도체결액(천만원 단위)
    }

    public sealed class RingBuffer<T> where T : struct
    {
        private readonly T[] _buf;
        private int _head = -1;
        private int _count = 0;

        public int Count => _count;

        public RingBuffer(int capacity)
        {
            _buf = new T[capacity];
        }

        public void Append(in T v)
        {
            _head = (_head + 1) % _buf.Length;
            _buf[_head] = v;
            if (_count < _buf.Length) _count++;
        }

        public bool TryGetByBackIndex(int k, out T v)
        {
            if (k < 0 || k >= _count) { v = default; return false; }
            int idx = _head - k;
            if (idx < 0) idx += _buf.Length;
            v = _buf[idx];
            return true;
        }

        public bool TryGetOldest(out T v)
        {
            if (_count == 0) { v = default; return false; }
            int idx = _head - (_count - 1);
            while (idx < 0) idx += _buf.Length;
            v = _buf[idx];
            return true;
        }

        public T GetLatest() => _count == 0 ? default : _buf[_head];
    }

    public static class RingCalc
    {
        // ✅ 30초 동안의 Δ(천만원 단위)만 반환
        public static bool TryGetDelayed30x2_Delta10M(
            RingBuffer<RingData> ring,
            long nowMs,
            out int dPro30_10M,
            out int dFor30_10M,
            out int dInst30_10M,
            out int dMoney30_10M,
            out long buyDelta10M_30s,
            out long sellDelta10M_30s,
            out double pf30)   // ✅ 30초 구간 PF%
        {
            dPro30_10M = dFor30_10M = dInst30_10M = dMoney30_10M = 0;
            buyDelta10M_30s = sellDelta10M_30s = 0;
            pf30 = 0;

            if (!TryGetAtOrBefore(ring, nowMs, out var a)) return false;
            if (!TryGetAtOrBefore(ring, nowMs - 30_000, out var b)) return false;

            int p30 = a.ProCum - b.ProCum;
            int f30 = a.ForCum - b.ForCum;
            int i30 = a.InstCum - b.InstCum;
            int m30 = a.MoneyCum - b.MoneyCum;

            // 리셋/역전 방어
            if (p30 < 0) p30 = 0;
            if (f30 < 0) f30 = 0;
            if (i30 < 0) i30 = 0;
            if (m30 < 0) m30 = 0;

            dPro30_10M = p30;
            dFor30_10M = f30;
            dInst30_10M = i30;
            dMoney30_10M = m30;

            long b30 = a.BuyAmtCum10M - b.BuyAmtCum10M;
            long s30 = a.SellAmtCum10M - b.SellAmtCum10M;

            if (b30 < 0) b30 = 0;
            if (s30 < 0) s30 = 0;

            buyDelta10M_30s = b30;
            sellDelta10M_30s = s30;

            if (m30 > 0) pf30 = 100.0 * (p30 + f30) / m30;
            return true;
        }

        private static bool TryGetAtOrBefore(
            RingBuffer<RingData> ring,
            long targetMs,
            out RingData snap)
        {
            snap = default;

            for (int k = 0; k < ring.Count; k++)
            {
                if (!ring.TryGetByBackIndex(k, out var s)) break;
                if (s.TimeMs <= targetMs) { snap = s; return true; }
            }
            return false;
        }
    }

}
