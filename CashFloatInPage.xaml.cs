using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using MySql.Data.MySqlClient;

namespace CafePOS
{
    /// <summary>
    /// Interaction logic for CategoryMainPage.xaml
    /// </summary>
    public partial class CashFloatInPage : Page
    {
        private int amount = 0;
        
        public CashFloatInPage()
        {
            InitializeComponent();
        }

        public void onClickNumber(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                int n = Convert.ToInt32(btn.Content);
                amount = amount * 10 + n;
                TextBox_Amount.Text = "" + amount;
            }
        }
        public void onBackspace(object sender, EventArgs e)
        {
            amount = amount / 10;
            TextBox_Amount.Text = "" + amount;
        }
        public void onClickOK(object sender, EventArgs e)
        {
        }
        public void onClickExit(object sender, EventArgs e)
        {
            GlobalVars.wndMainPage.onClickFunctions(null, null);
        }
        public void onClickSubmit(object sender, EventArgs e)
        {
            try
            {
                MySqlCommand cmd = new MySqlCommand("INSERT INTO tbl_float_in (date, amount) VALUES('" + DateTime.Now.ToString("yyyy-MM-dd") + "', " + amount + ") " + 
                                                    "ON DUPLICATE KEY UPDATE amount = " + amount, 
                                                    GlobalVars.wndMainPage.sqlConn);
                cmd.ExecuteNonQuery();
            }
            catch { }
            
            onClickExit(null, null);
        }
    }
}
