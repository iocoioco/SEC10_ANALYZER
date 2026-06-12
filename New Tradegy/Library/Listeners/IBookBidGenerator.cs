using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy.Library.Listeners
{
    public interface IBookBidGenerator
    {
        DateTime LastReceivedTime { get; }
        DataGridView GenerateBookBidView(string stock);
        void Unsubscribe();
        void OpenOrUpdateConfirmationForm(
            bool isSell,
            string stock,
            int qty,
            int price,
            int urgency,
            string message);
    }
}
