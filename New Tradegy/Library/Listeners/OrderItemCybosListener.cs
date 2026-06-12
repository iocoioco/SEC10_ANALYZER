
using CPUTILLib;
using DSCBO1Lib;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.UI.KeyBindings;
using New_Tradegy.Library.Utils;
using NLog.Layouts;
using System.IO;
using System.Collections;
using System.Drawing;
using System.Linq;
using System;
using System.Threading.Tasks;
namespace New_Tradegy.Library.Listeners
{
    internal class OrderItemCybosListener
    {
        private static DSCBO1Lib.CpConclusion _CpConclusion;
        private static CPUTILLib.CpStockCode _cpstockcode = new CPUTILLib.CpStockCode();
        // 주문 항목 정보 

        private static CPTRADELib.CpTd0311 _cptd0311; //주문(현금 주문) 데이터를 요청

        private static bool _isSubscribed = false;

        public static void Init_CpConclusion()
        {
            if (_isSubscribed) return;

            _CpConclusion = new DSCBO1Lib.CpConclusion();
            _CpConclusion.Received += new DSCBO1Lib._IDibEvents_ReceivedEventHandler(CpConclusion_Received);
            _CpConclusion.Subscribe();

            _isSubscribed = true;
        }

        private static void CpConclusion_Received()
        {
            try
            {
                Hashtable mapConclution = new Hashtable();
                int nContAmt = _CpConclusion.GetHeaderValue(3);
                int nPrice = _CpConclusion.GetHeaderValue(4);
                int nOrdKey = _CpConclusion.GetHeaderValue(5);
                int nOrdOrgKey = _CpConclusion.GetHeaderValue(6);
                string sConFlag = _CpConclusion.GetHeaderValue(14);

                mapConclution["종목코드"] = _CpConclusion.GetHeaderValue(9);
                string stock = _cpstockcode.CodeToName(mapConclution["종목코드"].ToString());
                mapConclution["매수매도"] = _CpConclusion.GetHeaderValue(12);
                mapConclution["정정취소"] = _CpConclusion.GetHeaderValue(16);
                mapConclution["주문호가구분"] = _CpConclusion.GetHeaderValue(18);
                mapConclution["주문조건구분"] = _CpConclusion.GetHeaderValue(19);
                mapConclution["장부가"] = _CpConclusion.GetHeaderValue(21);
                mapConclution["매도가능"] = _CpConclusion.GetHeaderValue(22);
                mapConclution["체결기준잔고"] = _CpConclusion.GetHeaderValue(23);


                //lock (OrderItemTracker.orderLock)
                {
                    switch (sConFlag)
                    {
                        case "1": // 체결
                            OrderItemTracker.Update(nOrdKey, x =>
                            {
                                if (x.m_nAmt - nContAmt > 0)
                                {
                                    x.m_nAmt -= nContAmt;
                                    x.m_nModAmt = x.m_nAmt;
                                    x.m_nContAmt += nContAmt;
                                }
                                else
                                {
                                    OrderItemTracker.Remove(nOrdKey);
                                }

                                var side = x.buyorSell;   // <-- 여기만 "자동완성"으로 정확히 맞춰!
                                if (side != "매수" && side != "매도")
                                    return;

                                var code = x.stock;
                                var price = x.m_nPrice;
                                var qty = nContAmt;

                                Directory.CreateDirectory(@"C:\BJS\Z Log\매수매도체결내역");

                                var now = TimeUtils.GetKstNow();

                                string line =
                                    $"{now:yyyy-MM-dd HH:mm:ss.fff}," +
                                    $"EXEC," +
                                    $"orderId={nOrdKey}," +
                                    $"code={code}," +
                                    $"qty={qty}," +
                                    $"price={price}," +
                                    $"side={side}";

                                string dailyPath =
                                    Path.Combine(@"C:\BJS\Z Log\매수매도체결내역",
                                        now.ToString("yyyyMMdd") + ".txt");
                                try
                                {
                                    File.AppendAllText(dailyPath, line + Environment.NewLine);
                                }
                                catch
                                {
                                    // 로그 실패는 무시 (체결 흐름 방해 금지)
                                }
                            });

                            break;

                        case "2": // 확인
                            if (!OrderItemTracker.Exists(nOrdOrgKey))
                            {
                                if ((string)mapConclution["정정취소"] == "3")
                                    OrderItemTracker.Remove(nOrdKey);
                                break;
                            }

                            var original = OrderItemTracker.Get(nOrdOrgKey);
                            if (original == null) break;

                            if ((string)mapConclution["정정취소"] == "2")
                            {
                                if (original.m_nAmt - nContAmt > 0)
                                {
                                    original.m_nAmt -= nContAmt;
                                    original.m_nModAmt = original.m_nAmt;

                                    var updated = new OrderItem
                                    {
                                        m_ordKey = nOrdKey,
                                        m_ordOrgKey = nOrdOrgKey,
                                        m_sCode = (string)mapConclution["종목코드"],
                                        m_nAmt = nContAmt,
                                        m_nPrice = nPrice,
                                        m_nContAmt = 0,
                                        m_nModAmt = nContAmt,
                                        buyorSell = original.buyorSell,
                                        m_sHogaFlag = (string)mapConclution["주문호가구분"]
                                    };
                                    OrderItemTracker.Add(updated);
                                }
                                else
                                {
                                    OrderItemTracker.Remove(nOrdOrgKey);

                                    var updated = new OrderItem
                                    {
                                        m_ordKey = nOrdKey,
                                        m_ordOrgKey = nOrdOrgKey,
                                        m_sCode = (string)mapConclution["종목코드"],
                                        m_nAmt = nContAmt,
                                        m_nPrice = nPrice,
                                        m_nContAmt = 0,
                                        m_nModAmt = nContAmt,
                                        buyorSell = original.buyorSell,
                                        m_sHogaFlag = (string)mapConclution["주문호가구분"]
                                    };
                                    OrderItemTracker.Add(updated);
                                }
                            }
                            else if ((string)mapConclution["정정취소"] == "3")
                            {
                                OrderItemTracker.Remove(nOrdOrgKey);
                            }
                            break;

                        case "3": // 거부
                            Utils.SoundUtils.Sound("Keys", "거부됨");
                            break;

                        case "4": // 접수
                            if ((string)mapConclution["정정취소"] != "1") break;

                            var newItem = new OrderItem
                            {
                                stock = stock,
                                m_ordKey = nOrdKey,
                                m_ordOrgKey = nOrdOrgKey,
                                m_sCode = (string)mapConclution["종목코드"],
                                m_nAmt = nContAmt,
                                m_nPrice = nPrice,
                                m_nContAmt = 0,
                                m_nModAmt = nContAmt,
                                buyorSell = (string)mapConclution["매수매도"] == "1" ? "매도" : "매수",
                                m_sHogaFlag = (string)mapConclution["주문호가구분"]
                            };
                            OrderItemTracker.Add(newItem);
                            // 주문 요청 수량 : newItem.m_nAmt
                            //TimeLogger.Log("ACK", nOrdKey, stock, newItem.m_nAmt, nPrice, (string)mapConclution["접수시간"]);


                            //needTradePaneUpdate = true;
                            break;
                    }
                }

                // CpConclusion_Received() 안 체결/접수 후 처리 부분
                if (sConFlag == "1" || sConFlag == "2" || sConFlag == "4")
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);

                        DealManager.DealHold();

                        g.tradePane?.SafeBeginInvoke(() =>
                        {
                            g.tradePane?.RefreshTradePane();
                        });
                    });
                }
            }

            catch (Exception ex)
            {
                Utils.SoundUtils.Sound("Keys", "error");
            }
        }
    }
}