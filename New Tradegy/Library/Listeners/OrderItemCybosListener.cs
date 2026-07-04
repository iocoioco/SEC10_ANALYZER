
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



                OrderItem data = null;

                switch (sConFlag)
                {
                    case "1": // 체결

                        if (!OrderItemTracker.OrderMap.ContainsKey(nOrdKey))
                            return;

                        data = OrderItemTracker.OrderMap[nOrdKey];

                        if (data.m_nAmt - nContAmt > 0)
                        {
                            data.m_nAmt -= nContAmt;
                            data.m_nModAmt = data.m_nAmt;
                            data.m_nContAmt += nContAmt;
                        }
                        else
                        {
                            OrderItemTracker.OrderMap.Remove(nOrdKey);
                        }

                        break;

                    case "2": // 확인
                        {
                            if (!OrderItemTracker.OrderMap.ContainsKey(nOrdOrgKey))
                            {
                                if ((string)mapConclution["정정취소"] == "3")
                                    OrderItemTracker.OrderMap.Remove(nOrdKey);
                                break;
                            }

                            data = OrderItemTracker.OrderMap[nOrdOrgKey];

                            if ((string)mapConclution["정정취소"] == "2")
                            {
                                if (data.m_nAmt - nContAmt > 0)
                                {
                                    data.m_nAmt -= nContAmt;
                                    data.m_nModAmt = data.m_nAmt;

                                    var item1 = new OrderItem
                                    {
                                        stock = data.stock,
                                        m_ordKey = nOrdKey,
                                        m_ordOrgKey = nOrdOrgKey,
                                        m_sCode = (string)mapConclution["종목코드"],
                                        m_nAmt = nContAmt,
                                        m_nPrice = nPrice,
                                        m_nContAmt = 0,
                                        m_nModAmt = nContAmt,
                                        buyorSell = data.buyorSell,
                                        m_sHogaFlag = (string)mapConclution["주문호가구분"]
                                    };

                                    OrderItemTracker.OrderMap[item1.m_ordKey] = item1;
                                }
                                else
                                {
                                    OrderItemTracker.OrderMap.Remove(nOrdOrgKey);

                                    var item1 = new OrderItem
                                    {
                                        stock = data.stock,
                                        m_ordKey = nOrdKey,
                                        m_ordOrgKey = nOrdOrgKey,
                                        m_sCode = (string)mapConclution["종목코드"],
                                        m_nAmt = nContAmt,
                                        m_nPrice = nPrice,
                                        m_nContAmt = 0,
                                        m_nModAmt = nContAmt,
                                        buyorSell = data.buyorSell,
                                        m_sHogaFlag = (string)mapConclution["주문호가구분"]
                                    };

                                    OrderItemTracker.OrderMap[item1.m_ordKey] = item1;
                                }
                            }
                            else if ((string)mapConclution["정정취소"] == "3")
                            {
                                OrderItemTracker.OrderMap.Remove(nOrdOrgKey);
                            }

                            break;
                        }

                    case "3": // 거부
                        Utils.SoundUtils.Sound("Keys", "거부됨");
                        break;

                    case "4": // 접수
                        {
                            if ((string)mapConclution["정정취소"] != "1")
                                break;

                            var item = new OrderItem
                            {
                                stock = stock,
                                m_ordKey = nOrdKey,
                                m_ordOrgKey = nOrdOrgKey,
                                m_sCode = (string)mapConclution["종목코드"],

                                m_nAmt = nContAmt,
                                m_nPrice = nPrice,
                                m_nContAmt = 0,
                                m_nModAmt = nContAmt,

                                buyorSell =
                                    (string)mapConclution["매수매도"] == "1"
                                        ? "매도"
                                        : "매수",

                                m_sHogaFlag = (string)mapConclution["주문호가구분"]
                            };

                            OrderItemTracker.OrderMap[item.m_ordKey] = item;

                            break;
                        }
                }


                if (sConFlag == "1" || sConFlag == "2" || sConFlag == "4")
                {
                    DealManager.DealHold();

                    g.tradePane?.SafeBeginInvoke(() =>
                    {
                        g.tradePane.RefreshTradePane();
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