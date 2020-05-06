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
using System.Windows.Markup;
using MySql.Data.MySqlClient;

namespace CafePOS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            MySqlCommand cmd = new MySqlCommand("SELECT user_code, username, role FROM tbl_users WHERE is_deleted = 0", GlobalVars.wndMainPage.sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                int col = 0, row = 1;

                while (reader.Read())
                {
                    String color = "White";
                    switch ((String)reader["role"])
                    {
                        case "Admin":
                            color = "Red";
                            break;
                        case "Manager":
                            color = "Green";
                            break;
                    }

                    String strXaml = "<Button Grid.Column='" + col + "' Grid.Row='" + row + "'" +
                                            " Style='{StaticResource MyStyle_PushButton}'" +
                                            " xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " + 
                                            " Name='Account_" + (int)reader["user_code"] + "' " +
                                            " Content='" + (String)reader["username"] + "' Background='#2A2A2A' Foreground='" + color + "' Margin='2.5'/>";
                    try
                    {
                        FrameworkElement ele = (FrameworkElement)XamlReader.Parse(strXaml);
                        gridAccounts.Children.Add(ele);
                    }
                    catch
                    {
                        Console.WriteLine("EXCEPTION");
                    }

                    col++;
                    if (col > 4)
                    {
                        col = 0;
                        row++;
                    }
                }
            }
            catch { }
            finally
            {
                // Always call Close when done reading.
                reader.Close();
            }

            connectEventHandlers();
        }

        private void connectEventHandlers()
        {
            foreach (Button btn in gridAccounts.Children)
            {
                btn.Click += onSelectAccount;
            }
        }

        private void onSelectAccount(object sender, RoutedEventArgs e)
        {
            String userCode = ((Button)sender).Name.Split('_')[1];
            GlobalVars.wndMainPage.doLogIn(this, Convert.ToInt32(userCode));
        }
    }
}
