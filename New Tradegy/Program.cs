using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using New_Tradegy.Library;
namespace New_Tradegy
{
  static class Program
  {
    // <summary>
    // The main entry point for the application.
    // </summary>
    [STAThread]
    static void Main()
     
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Application.Run(g.MainForm = new Form1());

            try
            {
                Application.Run(g.MainForm = new Form1());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup Error");
            }
        }
    }
}
