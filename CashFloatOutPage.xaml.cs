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
    public partial class CashFloatOutPage : Page
    {
        private int amount = 0;
        
        public CashFloatOutPage()
        {
            InitializeComponent();

            TextBox_Note.Text = "Write in details for all the amount taken for.";
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
            TextBox_Amount.Text = "" + amount / 100.0;
        }
        public void onClickExit(object sender, EventArgs e)
        {
            GlobalVars.wndMainPage.onClickFunctions(null, null);
        }
        public void onClickSubmit(object sender, EventArgs e)
        {
            onClickOK(null, null);

            try
            {
                MySqlCommand cmd = new MySqlCommand("INSERT INTO tbl_float_out (date, amount, note) VALUES('" + DateTime.Now.ToString("yyyy-MM-dd") + "', " + amount / 100.0 + ", '" + TextBox_Note.Text + "') ", GlobalVars.wndMainPage.sqlConn);
                cmd.ExecuteNonQuery();
            }
            catch { }

            onClickExit(null, null);
        }
        public void OnGetFocus_Note(object sender, EventArgs e)
        {
            if (TextBox_Note.Text == "Write in details for all the amount taken for.")
            {
                TextBox_Note.Text = "";
            }

            GlobalVars.wndMainPage.openVirtualKeyboard();
        }
        public void OnLostFocus_Note(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextBox_Note.Text))
                TextBox_Note.Text = "Write in details for all the amount taken for.";
        }
    }
}
