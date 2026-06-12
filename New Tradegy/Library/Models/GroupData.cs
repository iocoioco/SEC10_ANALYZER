using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Models
{
    /// <summary>
    /// 섹터(그룹)별 "상태"를 저장하는 컨테이너.
    /// - Stocks/Title은 기존 그대로 사용
    /// - 차트/Anno는 여기 값만 보고 표시 (계산 로직과 표시 로직 분리)
    /// </summary>
    public sealed class GroupData
    {
        // ====== 기존 필수 ======
        public string Title { get; set; }                 // 섹터명
        public List<string> Stocks { get; set; } = new List<string>();  // 멤버 종목명

        // ====== 실시간 계산 공통 메타 ======
        public int MemberCount => Stocks?.Count ?? 0;     // 전체 멤버 수
        public int UsedCount { get; private set; }        // 이번 계산에 사용된 멤버 수
        public double CoverRatio { get; private set; }    // denomUsed / denomAll (0~1)
        public int LastMinuteKey { get; private set; }    // HHMM (예: 913)
        public DateTime LastUpdatedUtc { get; private set; }

        // ====== 섹터 값(차트에 직접 쓰는 값) ======
        // (SectorData.Api.x 에 저장하는 값과 1:1로 맞추면 디버깅 쉬움)
        public int Price100 { get; private set; }         // 섹터 가격(가중 평균)
        public int MoneyMult100 { get; private set; }     // 누적 거래액 배수*100
        public int ProMult100 { get; private set; }       // 누적 프로 배수*100
        public int ForMult100 { get; private set; }       // 누적 외인 배수*100
        public int InstMult100 { get; private set; }      // 누적 기관 배수*100

        // ====== 매수/매도배(분 단위, x[row,8]/x[row,9] 기반) ======
        public int BuyMult { get; private set; }          // 분 매수배(가중)
        public int SellMult { get; private set; }         // 분 매도배(가중)
        public int BuySellDiff => BuyMult - SellMult;     // (매수배 - 매도배)
        public int BuySellSum => BuyMult + SellMult;      // (매수배 + 매도배)

        // ====== (프로+외인)/거래액 비율 (분/누적 어느 쪽이든 선택 가능) ======
        // 네가 말한 방식: 누적값 기반으로 계산해도 OK (섹터는 누적이 핵심)
        public double PFOverMoneyPct { get; private set; } // 0~100 (%)





        // ====== 4개 랭킹(섹터 간) ======
        // 1 = 1등. 0이면 미계산.
        public int RankMoney { get; private set; }
        public int RankPro { get; private set; }
        public int RankFor { get; private set; }
        public int RankInst { get; private set; }

        public void ClearRanks()
        {
            RankMoney = 0;
            RankPro = 0;
            RankFor = 0;
            RankInst = 0;
        }

        public void SetRankMoney(int r) => RankMoney = r;
        public void SetRankPro(int r) => RankPro = r;
        public void SetRankFor(int r) => RankFor = r;
        public void SetRankInst(int r) => RankInst = r;


        // ====== 표시용 합쳐둔 문자열(선택) ======
        // 차트 anno에서 "3 | 7 | 12 | 1" 같은 문자열을 바로 쓰기 좋음
        // Usage of RankPackText
        //        var gd = grp; // GroupData

        //        string rankText = gd.RankPackText;
        //if (!string.IsNullOrEmpty(rankText))
        //{
        //    anno.DrawText(
        //        x,
        //        y,
        //        rankText,
        //        color: Color.LightGray
        //    );
        //}

        public string RankPackText
        {
            get
            {
                string F(int r) => r > 0 ? r.ToString() : "-";

                if (RankMoney <= 0 && RankPro <= 0 &&
                    RankFor <= 0 && RankInst <= 0)
                    return "";

                return $"{F(RankMoney)} | {F(RankPro)} | {F(RankFor)} | {F(RankInst)}";
            }
        }


        public GroupData(string title)
        {
            Title = title;
        }

        /// <summary>
        /// SectorBuilder가 "이번 분" 계산 끝나면 GroupData에 복사해두는 용도.
        /// </summary>
        public void UpdateSectorMetrics(
            int minuteKey,
            int usedCount,
            double coverRatio,
            int price100,
            int moneyMult100,
            int proMult100,
            int forMult100,
            int instMult100,
            int buyMult,
            int sellMult,
            double pfOverMoneyPct)
        {
            LastMinuteKey = minuteKey;
            UsedCount = usedCount;
            CoverRatio = coverRatio;

            Price100 = price100;
            MoneyMult100 = moneyMult100;
            ProMult100 = proMult100;
            ForMult100 = forMult100;
            InstMult100 = instMult100;

            BuyMult = buyMult;
            SellMult = sellMult;

            PFOverMoneyPct = pfOverMoneyPct;

            LastUpdatedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// SectorRanker가 섹터 간 정렬/랭킹 계산 후 여기에 저장.
        /// </summary>
        public void UpdateRanks(int rankMoney, int rankPro, int rankFor, int rankInst)
        {
            RankMoney = rankMoney;
            RankPro = rankPro;
            RankFor = rankFor;
            RankInst = rankInst;
        }

        /// <summary>
        /// 디버깅/로그용 한 줄 요약.
        /// </summary>
        public override string ToString()
        {
            return $"{Title} t={LastMinuteKey} used={UsedCount}/{MemberCount} cover={CoverRatio:P0} " +
                   $"M={MoneyMult100} P={ProMult100} F={ForMult100} I={InstMult100} " +
                   $"B/S={BuyMult}/{SellMult} PF%={PFOverMoneyPct:F1} " +
                   $"R={RankPackText}";
        }
    }
}

