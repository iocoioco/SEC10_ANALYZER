

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Models
{

    public class OrderItem // check order processing & holding
    {
        public string stock; // 종목
        public string m_sCode; // 종목코드
        public string buyorSell; // 매수, 매도, 보유

        public int m_ordKey; // 주문번호
        public int m_ordOrgKey; // 원주문번호

        // public string m_sText;
        public int m_nAmt; // 주문수량
        public int m_nContAmt; // 체결수량
        public int m_nPrice; //주문단가
                             // public string m_sCredit;
        public int m_nModAmt; // 접수시 주문수량 = 정정취소 가능수량

        public string m_sHogaFlag; // 주문호가구분코드내용
    }

}