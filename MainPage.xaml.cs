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
using Microsoft.PointOfService;
using MySql.Data.MySqlClient;
using System.Windows.Markup;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.IO;

namespace CafePOS
{
    /// <summary>
    /// Interaction logic for CategoryMainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        public int activated = 1;   // for license

        public CustomerWindow wndCustomer;
        public ScreenSaverWindow wndScreenSaver;

        public MySqlConnection sqlConn;

        public String curIdCategory = "0", curIdItem = "0", curIdTable = "0", curIdInstruction = "0", curIdPlus = "0", curIdMinus = "0";
        public List<CartItem> cart;
        public double totalPrice, paidMoney, inputtingMoney, due, prevPrice;

        public String cashierName = "", customerName = "";

        public int orderNoPerDay = 100, orderNoWhole = 100;
        public int justCheckedOut = 0;

        System.Windows.Threading.DispatcherTimer timer;
        public Process processVirtualKeyboard;

        public static String MYSQL_CONNECTION_STRING = "Server=localhost; Database=possystem; User ID=root; Password=;";

        public List<String> itemLabelsForPrint;
        public List<double> pricesForPrint;
        public List<int> itemLevelsForPrint;

        public int timeLastedFromCartCleared = 0;
        public Boolean isScreenSaverActive = false;

        public MainPage()
        {
            try
            { 
                sqlConn = new MySqlConnection(MYSQL_CONNECTION_STRING);
                sqlConn.Open();
            }
            catch
            {
                Console.WriteLine("DB Error !");
            }
            
            InitializeComponent();

            rightFrame.Content = new CategoryMainPage(this);

            getReadyForNewSale();

            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += onUpdateDateTime;
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Start();


            // ---------   load business info   ---------- 
            MySqlCommand cmd = new MySqlCommand("SELECT receipt_name, phone_number, abn FROM tbl_business_info", sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    GlobalVars.receiptName = (String)reader["receipt_name"];
                    GlobalVars.phoneNumber = (String)reader["phone_number"];
                    GlobalVars.ABN = (String)reader["abn"];
                }
            }
            catch { }
            finally
            {
                reader.Close();
            }

            // ---------   load printers info   ---------- 
            cmd = new MySqlCommand("SELECT printer_drink, printer_food, printer_main FROM tbl_printers_info", sqlConn);
            reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    GlobalVars.PRINTERNAME_FOOD = (String)reader["printer_food"];
                    GlobalVars.PRINTERNAME_DRINK = (String)reader["printer_drink"];
                    GlobalVars.PRINTERNAME_MAIN = (String)reader["printer_main"];
                }
            }
            catch { }
            finally
            {
                reader.Close();
            }

            // ---------  remove old sale-infos from database  ---------
            cmd = new MySqlCommand("DELETE FROM tbl_sale WHERE date_time <= '" + DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd") + "'", sqlConn);
            cmd.ExecuteNonQuery();
        }
        ~MainPage()
        {
            try
            {
                sqlConn.Close();
            }
            catch
            {

            }
        }

        public void onBtnMainMenu(object sender, RoutedEventArgs e)
        {
            replaceRightFrameContent(new CategoryMainPage(this));
        }
        private void onClickTableTakeaway(object sender, RoutedEventArgs e)
        {
            if (GlobalVars.isLoggedIn())
            {
                replaceRightFrameContent(new TableTakeawayPage(this));
            }
        }
        public void onClickFunctions(object sender, RoutedEventArgs e)
        {
            replaceRightFrameContent(new FunctionPage(this));
        }
        private void onClickPayments(object sender, RoutedEventArgs e)
        {
            if (GlobalVars.isLoggedIn())
            {
                replaceRightFrameContent(new PaymentPage(this));
            }
        }
        private void onClickDiscounts(object sender, RoutedEventArgs e)
        {
            if (GlobalVars.isLoggedIn())
            {
                replaceRightFrameContent(new DiscountPage(this));
            }
        }
        private void onClickLogInOut(object sender, RoutedEventArgs e)
        {
            if (!GlobalVars.isLoggedIn())
                new LoginWindow().Show();
            else
                if(MessageBox.Show("Are you sure to log out ?", "Log out", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    doLogOut();
        }

        public void doLogOut()
        {
            cashierName = "";  TextBlock_CashierName.Text = "";
            GlobalVars.loggedInUser = 0;
            GlobalVars.loggedInUserRole = "";

            onBtnMainMenu(null, null);
        }
        public void doLogIn(LoginWindow parent, int userCode, String password = null, LoginInputWindow wnd = null)
        {
            MySqlCommand cmd = new MySqlCommand("SELECT username, password, role FROM tbl_users WHERE user_code = " + userCode, sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    if (password == null)
                    {
                        new LoginInputWindow(parent, userCode, (String)reader["username"]).Show();
                    }
                    else
                    {
                        if (password == (String)reader["password"])
                        {
                            cashierName = (String)reader["username"];   TextBlock_CashierName.Text = (String)reader["username"];
                            GlobalVars.loggedInUser = userCode;
                            GlobalVars.loggedInUserRole = (String)reader["role"];

                            wnd.Close();
                            parent.Close();
                        }
                        else
                        {
                            MessageBox.Show("Input the password again please !", "Login error", MessageBoxButton.OK);
                        }
                    }
                }
            }
            catch { }
            finally
            {
                // Always call Close when done reading.
                reader.Close();
            }
        }

        public void onExitPOS()
        {
            if (processVirtualKeyboard != null && !processVirtualKeyboard.HasExited)
                processVirtualKeyboard.Kill();

            System.Windows.Application.Current.Shutdown();
        }

        public void openVirtualKeyboard()
        {
            processVirtualKeyboard = Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\osk.exe");
        }
        

        public void addItemToCart()
        {
            MySqlCommand cmd = new MySqlCommand("SELECT uid, name, price FROM tbl_items WHERE uid = " + curIdItem, sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    String itemLabel = ((String)reader["name"]).Replace("&", "&amp;");
                    double price = (Double)reader["price"];
                    String itemPrice = price.ToString();

                    CartItem item = new CartItem(CartItem.TYPE_ITEM, Convert.ToInt32(curIdItem), price);
                    AddToCart(item);

                    addToCartItemListBox(CartItem.TYPE_ITEM, itemLabel, itemPrice);
                }
            }
            catch { }
            finally
            {
                reader.Close();
            }
        }
        public void addInstructionToCart()
        {
            MySqlCommand cmd = new MySqlCommand("SELECT uid, instruction, price FROM tbl_instructions WHERE uid = " + curIdInstruction, sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    String itemLabel = (String)reader["instruction"];
                    double price = (Double)reader["price"];
                    String itemPrice = "";
                    if (price != 0.0) itemPrice = price.ToString();

                    CartItem item = new CartItem(CartItem.TYPE_INSTRUCTION, Convert.ToInt32(curIdInstruction), price);
                    AddToCart(item);

                    addToCartItemListBox(CartItem.TYPE_INSTRUCTION, itemLabel, itemPrice);
                }
            }
            catch { }
            finally
            {
                reader.Close();
            }
        }
        public void addPlusItemToCart()
        {
            MySqlCommand cmd = new MySqlCommand("SELECT uid, name, price FROM tbl_plus WHERE uid = " + curIdPlus, sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    String itemLabel = (String)reader["name"];
                    double price = (Double)reader["price"];
                    String itemPrice = "";
                    if (price != 0.0) itemPrice = price.ToString();

                    CartItem item = new CartItem(CartItem.TYPE_PLUS, Convert.ToInt32(curIdPlus), price);
                    AddToCart(item);

                    addToCartItemListBox(CartItem.TYPE_PLUS, itemLabel, itemPrice);
                }
            }
            catch { }
            finally
            {
                reader.Close();
            }
        }
        public void addMinusItemToCart()
        {
            MySqlCommand cmd = new MySqlCommand("SELECT uid, name FROM tbl_minus WHERE uid = " + curIdMinus, sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    String itemLabel = (String)reader["name"];

                    CartItem item = new CartItem(CartItem.TYPE_MINUS, Convert.ToInt32(curIdMinus));
                    AddToCart(item);

                    addToCartItemListBox(CartItem.TYPE_MINUS, itemLabel, "");
                }
            }
            catch { }
            finally
            {
                reader.Close();
            }
        }
        public void addMiscFoodToCart()
        {
            String itemLabel = "MISC FOOD";
            double realInputMoney = Math.Round(inputtingMoney / 100, 2);
            double price = realInputMoney;
            String itemPrice = price.ToString();

            CartItem item = new CartItem(CartItem.TYPE_MISCFOOD, 0, price);
            AddToCart(item);

            addToCartItemListBox(CartItem.TYPE_MISCFOOD, itemLabel, itemPrice);

            inputtingMoney = 0;
            textBlock_MoneyInputting.Text = "   ";
        }

        public void addMiscDrinkToCart()
        {
            String itemLabel = "MISC DRINK";
            double realInputMoney = Math.Round(inputtingMoney / 100, 2);
            double price = realInputMoney;
            String itemPrice = price.ToString();

            CartItem item = new CartItem(CartItem.TYPE_MISCDRINK, 0, price);
            AddToCart(item);

            addToCartItemListBox(CartItem.TYPE_MISCDRINK, itemLabel, itemPrice);

            inputtingMoney = 0;
            textBlock_MoneyInputting.Text = "   ";
        }

        public void addDiscountInfoToCart(String itemORsale, String dollarORpercent, double discountAmount)
        {
            String itemLabel = "";
            CartItem item = new CartItem();

            if (itemORsale == "Item")
            {
                if (dollarORpercent == "Dollar")
                {
                    itemLabel = "$" + discountAmount.ToString("0.0") + " Discount";
                    item = new CartItem(CartItem.TYPE_DISCOUNT_ITEM, 0, -discountAmount);

                }
                else if (dollarORpercent == "Percent")
                {
                    if (discountAmount == 100)
                    {
                        itemLabel = "Free Coffee";
                        item = new CartItem(CartItem.TYPE_DISCOUNT_FREECOFFEE, 0, -prevPrice * discountAmount / 100);
                    }
                    else
                    {
                        itemLabel = "" + discountAmount.ToString("0.0") + "% Discount";
                        item = new CartItem(CartItem.TYPE_DISCOUNT_ITEM, 0, -prevPrice * discountAmount / 100);
                    }
                }
            }
            else if (itemORsale == "Sale")
            {
                if (dollarORpercent == "Dollar")
                {
                    itemLabel = "$" + discountAmount.ToString("0.0") + " Sale Discount";
                    item = new CartItem(CartItem.TYPE_DISCOUNT_SALE, 0, -discountAmount);
                }
                else if (dollarORpercent == "Percent")
                {
                    itemLabel = "" + discountAmount.ToString("0.0") + "% Sale Discount";
                    item = new CartItem(CartItem.TYPE_DISCOUNT_SALE, 0, -totalPrice * discountAmount / 100);
                }
                else if (dollarORpercent == "Senior")
                {
                    itemLabel = "" + discountAmount.ToString("0.0") + "% Senior Discount";
                    item = new CartItem(CartItem.TYPE_DISCOUNT_SALE, 0, -totalPrice * discountAmount / 100);
                }
                else if (dollarORpercent == "Employee")
                {
                    itemLabel = "" + discountAmount.ToString("0.0") + "% Employee Discount";
                    item = new CartItem(CartItem.TYPE_DISCOUNT_SALE, 0, -totalPrice * discountAmount / 100);
                }
            }
                       
            AddToCart(item);
            addToCartItemListBox(item.type, itemLabel, item.price.ToString("0.00"));
        }


        private void onClickClear(object sender, RoutedEventArgs e)
        {
            if (!GlobalVars.isLoggedIn()) return;

            if (justCheckedOut == 1)
            {
                this.getReadyForNewSale();
            }
            else
            {
                int selectedIndex = cartItemList.SelectedIndex;
                if (selectedIndex != -1)
                {
                    CartItem selectedItem = cart.ElementAt(selectedIndex);
                    if (selectedItem.type == CartItem.TYPE_ITEM)
                    {
                        int cnt = 1;
                        for (int i = selectedIndex + 1; ; i++)
                        {
                            try
                            {
                                CartItem cartItem = cart.ElementAt(i);
                                if (cartItem == null || cartItem.type == CartItem.TYPE_ITEM || (cartItem.type >= CartItem.TYPE_PAID_CASH && cartItem.type <= CartItem.TYPE_PRICE_CHANGE)) break;
                                cnt++;
                            }
                            catch
                            {
                                break;
                            }
                        }

                        for (int i = 0; i < cnt; i++)
                        {
                            ClearFromCart(selectedIndex);
                        }
                    }
                    else if (   selectedItem.type >= CartItem.TYPE_INSTRUCTION && selectedItem.type <= CartItem.TYPE_MINUS ||
                                selectedItem.type >= CartItem.TYPE_DISCOUNT_ITEM && selectedItem.type <= CartItem.TYPE_DISCOUNT_SALE  )
                    {
                        ClearFromCart(selectedIndex);
                    }
                }
            }
        }

        public void onSelectTable(String idTable)
        {
            this.curIdTable = idTable;

            if (idTable == "0")
                TextBlock_TableNo.Text = "TakeAway";
            else TextBlock_TableNo.Text = "Table " + idTable;
        }
        public void onUpdateCustomerName(String customerName)
        {
            if (customerName == "CUSTOMER NAME ...")
                this.customerName = "";
            else this.customerName = customerName;

            TextBlock_CustomerName.Text = this.customerName;
        }

        public void onUpdateDateTime(Object source, EventArgs e)
        {
            DateTime dateTime = DateTime.Now;
            textBox_DateTime.Text = "Date: " + dateTime.Day + "/" + dateTime.Month + "/" + dateTime.Year + "   Time: " + dateTime.Hour + ":" + dateTime.Minute;

            if (this.activated == 0)
            {
                if (dateTime.Year == 2019 && dateTime.Month == 6 && dateTime.Day >= 25)
                    onExitPOS();
            }


            // screen-saver related
            timeLastedFromCartCleared++;
            if (wndScreenSaver != null && !isScreenSaverActive && timeLastedFromCartCleared >= 30)
            {
                wndCustomer.Hide();
                wndScreenSaver.Top = wndCustomer.Top;
                wndScreenSaver.Left = wndCustomer.Left;
                wndScreenSaver.WindowState = System.Windows.WindowState.Normal;
                wndScreenSaver.Show();
                isScreenSaverActive = true;
            }
        }

        private void AddToCart(CartItem item)
        {
            cart.Add(item);
            if (item.type >= CartItem.TYPE_ITEM && item.type <= CartItem.TYPE_MINUS ||
                item.type >= CartItem.TYPE_DISCOUNT_ITEM && item.type <= CartItem.TYPE_DISCOUNT_SALE)
                totalPrice += item.price;
            if (item.type == CartItem.TYPE_ITEM || item.type == CartItem.TYPE_MISCFOOD || item.type == CartItem.TYPE_MISCDRINK)
                prevPrice = item.price;
            else if (item.type >= CartItem.TYPE_INSTRUCTION && item.type <= CartItem.TYPE_MINUS)
                prevPrice += item.price;

            TextBlock_TotalPrice.Text = Math.Round(totalPrice, 2).ToString();
            wndCustomer.TextBlock_Total.Text = "$" + this.TextBlock_TotalPrice.Text;

            refreshDue();
        }
        private void ClearFromCart(int index)
        {
            totalPrice -= cart.ElementAt(index).price;
            TextBlock_TotalPrice.Text = Math.Round(totalPrice, 2).ToString();
            wndCustomer.TextBlock_Total.Text = "$" + this.TextBlock_TotalPrice.Text;

            cart.RemoveAt(index);
            cartItemList.Items.RemoveAt(index);
            wndCustomer.cartItemList.Items.RemoveAt(index);

            refreshDue();
        }
        private void refreshDue()
        {
            due = Math.Round(totalPrice - paidMoney, 2);
            if (due < 0.0) due = 0.0;
            TextBlock_Due.Text = due.ToString();
            wndCustomer.TextBlock_Due.Text = "$" + this.TextBlock_Due.Text;
        }

        public void onClickNumber(int n)
        {
            if (n == 100)
                inputtingMoney *= 100;
            else
                inputtingMoney = inputtingMoney * 10 + n;
            textBlock_MoneyInputting.Text = inputtingMoney.ToString() + "   ";
        }
        public void onBackspace()
        {
            inputtingMoney = (int)(inputtingMoney / 10);
            textBlock_MoneyInputting.Text = inputtingMoney.ToString() + "   ";
        }
        public void onEnterMoney(int type = -1)     // default : cash
        {
            if (justCheckedOut == 1)        // if already paid enough, don't get payment anymore
                return;

            if (type == -1 && inputtingMoney == 0)
                inputtingMoney = due * 100;
            else if(type == CartItem.TYPE_PAID_CREDIT || type == CartItem.TYPE_PAID_UBEREATS)
                inputtingMoney = due * 100;

            double realInputMoney = Math.Round(inputtingMoney / 100, 2);
            paidMoney += realInputMoney;
            
            if (type == -1) type = CartItem.TYPE_PAID_CASH;
            String itemLabel = "CASH";
            if (type == CartItem.TYPE_PAID_CREDIT)
                itemLabel = "CREDIT";
            else if (type == CartItem.TYPE_PAID_UBEREATS)
                itemLabel = "UBER EATS";

            CartItem item = new CartItem(type, 0, realInputMoney);
            AddToCart(item);

            String itemPrice = realInputMoney.ToString();
            addToCartItemListBox(type, itemLabel, itemPrice);

            if (paidMoney - totalPrice > -0.01)     // paid enough
            {
                double change = Math.Round(paidMoney - totalPrice, 2);
                item = new CartItem(CartItem.TYPE_PRICE_CHANGE, 0, change);
                AddToCart(item);

                addToCartItemListBox(CartItem.TYPE_PRICE_CHANGE, "CHANGE", change.ToString());
                justCheckedOut = 1;
                this.saveSale();
                this.printKitchenReceipts();
            }
            else                                    // should pay more
            {
                refreshDue();
            }
            
            inputtingMoney = 0;
            textBlock_MoneyInputting.Text = "   ";
        }

        private void addToCartItemListBox(int type, String itemLabel, String itemPrice)
        {
            String strXaml = "", strXamlCashierScreen = "", strXamlCustomerScreen = "";

            if (type == CartItem.TYPE_ITEM)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontSize = '15' FontWeight = 'Bold' Text = '" + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontSize = '15' FontWeight = 'Bold' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_INSTRUCTION)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontSize = '15' Text = '       " + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_PLUS)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontSize = '15' Text = '       + " + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_MINUS)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontSize = '15' Text = '       No " + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontSize = '15' Text = ''></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_MISCDRINK || type == CartItem.TYPE_MISCFOOD)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontWeight='Bold' FontSize = '15' Text = '" + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontWeight='Bold' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type >= CartItem.TYPE_PAID_CASH && type <= CartItem.TYPE_PAID_UBEREATS)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' HorizontalAlignment='Center' FontWeight = 'Bold' FontSize = '15' Text = '" + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontWeight = 'Bold' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_PRICE_CHANGE)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' HorizontalAlignment='Center' Foreground='Blue' FontWeight = 'Bold' FontSize = '15' Text = '" + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' Foreground='Blue' FontWeight = 'Bold' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_DISCOUNT_ITEM)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontSize = '15' Text = '       " + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_DISCOUNT_SALE)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontWeight='Bold' FontSize = '15' Text = '" + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontWeight='Bold' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }
            else if (type == CartItem.TYPE_DISCOUNT_FREECOFFEE)
            {
                strXaml = "<ListBoxItem xmlns ='http://schemas.microsoft.com/winfx/2006/xaml/presentation' {1}> " +
                                "<Grid {0}>" +
                                    "<Grid.ColumnDefinitions>" +
                                        "<ColumnDefinition Width = '7*'/>" +
                                        "<ColumnDefinition Width = '*'/>" +
                                    "</Grid.ColumnDefinitions>" +
                                    "<TextBlock Grid.Column = '0' FontSize = '15' Text = '       " + itemLabel + "'></TextBlock>" +
                                    "<TextBlock Grid.Column = '1' FontSize = '15' Text = '" + itemPrice + "'></TextBlock>" +
                                "</Grid>" +
                            "</ListBoxItem>";
            }

            strXamlCashierScreen = String.Format(strXaml, "Width='350'", "");
            strXamlCustomerScreen = String.Format(strXaml, "", "Margin='0 5 0 0'").Replace("FontSize = '15'", "FontSize = '20'");
            FrameworkElement ele = (FrameworkElement)XamlReader.Parse(strXamlCashierScreen);
            cartItemList.Items.Add(ele);
            ele = (FrameworkElement)XamlReader.Parse(strXamlCustomerScreen);
            wndCustomer.cartItemList.Items.Add(ele);

            timeLastedFromCartCleared = 0;
            if (isScreenSaverActive)
            {
                isScreenSaverActive = false;
                wndScreenSaver.Hide();
                wndCustomer.Show();
            }
        }

        public void getReadyForNewSale()
        {
            cart = new List<CartItem>();
            cartItemList.Items.Clear();
            if(wndCustomer != null)
                wndCustomer.cartItemList.Items.Clear();
            
            due = inputtingMoney = paidMoney = totalPrice = 0.0;
            
            TextBlock_TotalPrice.Text = "";
            TextBlock_Due.Text = "";
            if (wndCustomer != null)
            {
                wndCustomer.TextBlock_Total.Text = this.TextBlock_TotalPrice.Text;
                wndCustomer.TextBlock_Due.Text = this.TextBlock_Due.Text;
            }

            curIdCategory = "0"; curIdItem = "0"; curIdTable = "0"; curIdInstruction = "0"; curIdPlus = "0"; curIdMinus = "0";
            customerName = "";
            TextBlock_CustomerName.Text = ""; TextBlock_TableNo.Text = "";
            justCheckedOut = 0;

            // read order-numbers
            MySqlCommand cmd = new MySqlCommand("SELECT last_date, order_no_day, order_no_whole FROM tbl_last_sale", this.sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                if (reader.Read())
                {
                    String last_date = (String)reader["last_date"];
                    orderNoWhole = Convert.ToInt32(reader["order_no_whole"]) + 1;
                    if (last_date == DateTime.Now.ToString("yyyy-MM-dd"))
                        orderNoPerDay = Convert.ToInt32(reader["order_no_day"]) + 1;
                    else
                        orderNoPerDay = 100;

                    TextBlock_OrderNo.Text = "Order " + orderNoPerDay;
                }
            }
            catch { }
            finally
            {
                reader.Close();
            }
        }

        private void PrintHandler_KitchenReceipt(object sender, PrintPageEventArgs ppeArgs)
        {
            try
            {
                String strSplitter = "--------------------------------------";

                Font fontSmaller = new Font("Verdana", 13), fontNormal = new Font("Verdana", 14), fontBigger1 = new Font("Verdana", 15), fontBigger2 = new Font("Verdana", 16);
                Graphics g = ppeArgs.Graphics;
                StringFormat sfNormal = new StringFormat();
                StringFormat sfRight = new StringFormat();
                sfRight.Alignment = StringAlignment.Far;
                StringFormat sfCenter = new StringFormat();
                sfCenter.Alignment = StringAlignment.Center;


                int posX = 10, posY = 5;
                int width = 270;
                g.DrawString(strSplitter, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 15), sfCenter);
                posY += 15;
                if (curIdTable == "" || curIdTable == "0")
                {
                    g.DrawString("TAKEAWAY", fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                    posY += 25;
                }
                else
                {
                    g.DrawString("TABLE                     " + curIdTable, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                    posY += 25;
                    g.DrawString("DINE IN", fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                    posY += 25;
                }
                g.DrawString("ORDER #" + orderNoPerDay, fontBigger2, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfCenter);
                posY += 25;

                int i, n = itemLabelsForPrint.Count();
                for (i = 0; i < n; i++)
                {
                    if (itemLevelsForPrint[i] == 1) // if item, print splitter above
                    {
                        g.DrawString(strSplitter, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 15), sfCenter);
                        posY += 15;
                        g.DrawString(itemLabelsForPrint[i], fontBigger1, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfCenter);
                        posY += 30;
                    }
                    else
                    {
                        g.DrawString(itemLabelsForPrint[i], fontSmaller, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfCenter);
                        posY += 30;
                    }
                }

                posY += 10;
                g.DrawString(customerName, fontSmaller, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfCenter);
                posY += 30;
                g.DrawString("#" + orderNoWhole, fontSmaller, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfCenter);
                posY += 20;
                g.DrawString(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fontSmaller, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                posY += 25;
                g.DrawString("                             ", fontNormal, System.Drawing.Brushes.Black, new RectangleF(posX, posY, width, 30), sfNormal);
            }
            catch { }
        }
        public void printKitchenReceipts()
        {
            try
            {
                bool cashDrawerAlreadyOpened = false;

                String[] receiptTypes = { "WIRED", "WIRELESS" };
                foreach (String receiptType in receiptTypes)
                {
                    if (itemLabelsForPrint != null)
                    {
                        itemLabelsForPrint.Clear();
                        itemLabelsForPrint = null;
                    }
                    if (itemLevelsForPrint != null)
                    {
                        itemLevelsForPrint.Clear();
                        itemLevelsForPrint = null;
                    }
                    itemLabelsForPrint = new List<String>();
                    itemLevelsForPrint = new List<int>();

                    MySqlCommand cmd;
                    MySqlDataReader reader;
                    int printable = 1;
                    foreach (CartItem eachCartItem in cart)
                    {
                        if (eachCartItem.type == CartItem.TYPE_ITEM)
                        {
                            cmd = new MySqlCommand("SELECT * FROM tbl_items WHERE uid = " + eachCartItem.index, sqlConn);
                            reader = cmd.ExecuteReader();
                            try
                            {
                                if (reader.Read())
                                {
                                    String itemLabel = (String)reader["name"];
                                    if ((String)reader["printer_path"] == receiptType)
                                    {
                                        itemLabelsForPrint.Add(itemLabel);
                                        itemLevelsForPrint.Add(1);
                                        printable = 1;
                                    }
                                    else
                                        printable = 0;
                                }
                            }
                            catch { }
                            finally
                            {
                                reader.Close();
                            }
                        }
                        else if (printable == 1 && eachCartItem.type == CartItem.TYPE_INSTRUCTION)
                        {
                            cmd = new MySqlCommand("SELECT * FROM tbl_instructions WHERE uid = " + eachCartItem.index, sqlConn);
                            reader = cmd.ExecuteReader();
                            try
                            {
                                if (reader.Read())
                                {
                                    String itemLabel = (String)reader["instruction"];
                                    itemLabelsForPrint.Add(itemLabel);
                                    itemLevelsForPrint.Add(0);
                                }
                            }
                            catch { }
                            finally
                            {
                                reader.Close();
                            }
                        }
                        else if (printable == 1 && eachCartItem.type == CartItem.TYPE_PLUS)
                        {
                            cmd = new MySqlCommand("SELECT * FROM tbl_plus WHERE uid = " + eachCartItem.index, sqlConn);
                            reader = cmd.ExecuteReader();
                            try
                            {
                                if (reader.Read())
                                {
                                    String itemLabel = (String)reader["name"];
                                    itemLabelsForPrint.Add("+" + itemLabel);
                                    itemLevelsForPrint.Add(0);
                                }
                            }
                            catch { }
                            finally
                            {
                                reader.Close();
                            }
                        }
                        else if (printable == 1 && eachCartItem.type == CartItem.TYPE_MINUS)
                        {
                            cmd = new MySqlCommand("SELECT * FROM tbl_minus WHERE uid = " + eachCartItem.index, sqlConn);
                            reader = cmd.ExecuteReader();
                            try
                            {
                                if (reader.Read())
                                {
                                    String itemLabel = (String)reader["name"];
                                    itemLabelsForPrint.Add("No " + itemLabel);
                                    itemLevelsForPrint.Add(0);
                                }
                            }
                            catch { }
                            finally
                            {
                                reader.Close();
                            }
                        }
                    }

                    if (itemLabelsForPrint.Count() > 0)
                    {
                        PrintDocument doc = new PrintDocument();
                        doc = new PrintDocument();
                        if (receiptType == "WIRED")
                        {
                            doc.PrinterSettings.PrinterName = GlobalVars.PRINTERNAME_FOOD;
                            cashDrawerAlreadyOpened = true;
                        }
                        else if (receiptType == "WIRELESS")
                            doc.PrinterSettings.PrinterName = GlobalVars.PRINTERNAME_DRINK;
                        doc.PrintPage += new PrintPageEventHandler(PrintHandler_KitchenReceipt);
                        doc.Print();
                    }
                }

                if (!cashDrawerAlreadyOpened)
                {
                    // open the cash-drawer
                    PrintDocument doc2 = new PrintDocument();
                    doc2.PrinterSettings.PrinterName = GlobalVars.PRINTERNAME_MAIN;
                    doc2.PrintPage += new PrintPageEventHandler(PrintHandler_OpenCashDrawer);
                    doc2.Print();

                    //string output = "\x1B|\x70|\x30|\x37|\x79";
                    //RawPrinterHelper.SendStringToPrinter(GlobalVars.PRINTERNAME_MAIN, output);

                    /*
                     * PosExplorer explorer = new PosExplorer();
                    DeviceInfo ObjDevicesInfo = explorer.GetDevice("CashDrawer");
                    CashDrawer myCashDrawer = (CashDrawer)explorer.CreateInstance(ObjDevicesInfo);
                    myCashDrawer.Open();
                    myCashDrawer.Claim(1000);
                    myCashDrawer.DeviceEnabled = true;
                    myCashDrawer.OpenDrawer();
                    myCashDrawer.DeviceEnabled = false;
                    myCashDrawer.Release();
                    myCashDrawer.Close();
                    */
                }
            }
            catch { }
        }
        private void PrintHandler_CustomerReceipt(object sender, PrintPageEventArgs ppeArgs)
        {
            try
            {
                String strSplitter = "--------------------------------------";

                Font fontSmaller = new Font("Verdana", 11), fontNormal = new Font("Verdana", 12), fontBigger1 = new Font("Verdana", 14), fontBigger2 = new Font("Verdana", 16);
                Graphics g = ppeArgs.Graphics;
                StringFormat sfNormal = new StringFormat();
                StringFormat sfRight = new StringFormat();
                sfRight.Alignment = StringAlignment.Far;
                StringFormat sfCenter = new StringFormat();
                sfCenter.Alignment = StringAlignment.Center;


                int posX = 10, posY = 10;
                int width = 270;
                g.DrawString(GlobalVars.receiptName, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfCenter);
                posY += 20;
                g.DrawString("Phone: " + GlobalVars.phoneNumber, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfCenter);
                posY += 20;
                g.DrawString("ABN: " + GlobalVars.ABN, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfCenter);
                posY += 30;
                g.DrawString(strSplitter, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 15), sfCenter);
                posY += 15;
                if (curIdTable == "" || curIdTable == "0")
                {
                    g.DrawString("TAKEAWAY", fontBigger1, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                    posY += 25;
                }
                else
                {
                    g.DrawString("TABLE                   " + curIdTable, fontBigger1, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                    posY += 25;
                    g.DrawString("DINE IN", fontBigger1, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                    posY += 25;
                }
                g.DrawString("ORDER #" + orderNoPerDay, fontBigger2, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfCenter);
                posY += 30;
                g.DrawString(strSplitter, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 15), sfCenter);
                posY += 15;
                int i, n = itemLabelsForPrint.Count();
                for (i = 0; i < n; i++)
                {
                    if (itemLabelsForPrint[i] != "CASH" && itemLabelsForPrint[i] != "CREDIT DEBIT" && itemLabelsForPrint[i] != "UBER EATS" && itemLabelsForPrint[i] != "CHANGE")
                    {
                        g.DrawString(itemLabelsForPrint[i], fontNormal, System.Drawing.Brushes.Black, posX, posY, sfNormal);
                        if (pricesForPrint[i] != 0.0)
                            g.DrawString("$" + pricesForPrint[i].ToString("0.00"), fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfRight);
                        posY += 20;
                    }
                    else break;
                }
                if (i < n)
                {
                    g.DrawString("TOTAL", fontBigger2, System.Drawing.Brushes.Black, posX, posY, sfNormal);
                    g.DrawString("$" + totalPrice.ToString("0.00"), fontBigger2, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfRight);
                    posY += 30;

                    for (; i < n; i++)
                    {
                        g.DrawString(itemLabelsForPrint[i], fontBigger2, System.Drawing.Brushes.Black, posX, posY, sfNormal);
                        g.DrawString("$" + pricesForPrint[i].ToString("0.00"), fontBigger2, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfRight);
                        posY += 30;
                    }
                }
                g.DrawString("TAXABLE", fontNormal, System.Drawing.Brushes.Black, posX, posY, sfNormal);
                g.DrawString("$" + totalPrice.ToString("0.00"), fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfRight);
                posY += 20;
                g.DrawString("GST AMT", fontNormal, System.Drawing.Brushes.Black, posX, posY, sfNormal);
                g.DrawString("$" + (totalPrice / 11.0).ToString("0.00"), fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfRight);
                posY += 25;
                g.DrawString(cashierName, fontBigger1, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                posY += 25;
                g.DrawString("#" + orderNoWhole, fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 20), sfCenter);
                posY += 20;
                g.DrawString(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fontNormal, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 25), sfCenter);
                posY += 35;
                g.DrawString("TAX INVOICE", fontBigger1, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfCenter);
                posY += 30;
                g.DrawString("Thank you and have a nice day", fontSmaller, System.Drawing.Brushes.Black, new RectangleF(0, posY, width, 30), sfCenter);
                posY += 30;
                g.DrawString("                             ", fontNormal, System.Drawing.Brushes.Black, new RectangleF(posX, posY, width, 30), sfNormal);
            }
            catch { }
        }
        private void PrintHandler_OpenCashDrawer(object sender, PrintPageEventArgs ppeArgs)
        {
            // There's nothing to print, we just need to open cash-drawer  ( printer has option ; opening cash-drawer for its before-job )
                    // ppeArgs.Cancel = true;  ====> not opened and not printed
        }

        public void printCustomerReceipt()
        {
            try
            {
                PrintDocument doc = new PrintDocument();
                doc.PrinterSettings.PrinterName = GlobalVars.PRINTERNAME_MAIN;                
                doc.PrintPage += new PrintPageEventHandler(PrintHandler_CustomerReceipt);

                if (itemLabelsForPrint != null)
                {
                    itemLabelsForPrint.Clear();
                    itemLabelsForPrint = null;
                }
                if (pricesForPrint != null)
                {
                    pricesForPrint.Clear();
                    pricesForPrint = null;
                }
                itemLabelsForPrint = new List<String>();
                pricesForPrint = new List<double>();

                MySqlCommand cmd;
                MySqlDataReader reader;
                foreach (CartItem eachCartItem in cart)
                {
                    if (eachCartItem.type == CartItem.TYPE_ITEM)
                    {
                        cmd = new MySqlCommand("SELECT * FROM tbl_items WHERE uid = " + eachCartItem.index, sqlConn);
                        reader = cmd.ExecuteReader();
                        try
                        {
                            if (reader.Read())
                            {
                                String itemLabel = (String)reader["name"];
                                double price = (Double)reader["price"];
                                itemLabelsForPrint.Add(itemLabel);
                                pricesForPrint.Add(price);
                            }
                        }
                        catch { }
                        finally
                        {
                            reader.Close();
                        }
                    }
                    else if (eachCartItem.type == CartItem.TYPE_INSTRUCTION)
                    {
                        cmd = new MySqlCommand("SELECT * FROM tbl_instructions WHERE uid = " + eachCartItem.index, sqlConn);
                        reader = cmd.ExecuteReader();
                        try
                        {
                            if (reader.Read())
                            {
                                String itemLabel = (String)reader["instruction"];
                                double price = (Double)reader["price"];
                                itemLabelsForPrint.Add("       " + itemLabel);
                                pricesForPrint.Add(price);
                            }
                        }
                        catch { }
                        finally
                        {
                            reader.Close();
                        }
                    }
                    else if (eachCartItem.type == CartItem.TYPE_PLUS)
                    {
                        cmd = new MySqlCommand("SELECT * FROM tbl_plus WHERE uid = " + eachCartItem.index, sqlConn);
                        reader = cmd.ExecuteReader();
                        try
                        {
                            if (reader.Read())
                            {
                                String itemLabel = (String)reader["name"];
                                double price = (Double)reader["price"];
                                itemLabelsForPrint.Add("       + " + itemLabel);
                                pricesForPrint.Add(price);
                            }
                        }
                        catch { }
                        finally
                        {
                            reader.Close();
                        }
                    }
                    else if (eachCartItem.type == CartItem.TYPE_MINUS)
                    {
                        cmd = new MySqlCommand("SELECT * FROM tbl_minus WHERE uid = " + eachCartItem.index, sqlConn);
                        reader = cmd.ExecuteReader();
                        try
                        {
                            if (reader.Read())
                            {
                                String itemLabel = (String)reader["name"];
                                itemLabelsForPrint.Add("       No " + itemLabel);
                                pricesForPrint.Add(0.0);
                            }
                        }
                        catch { }
                        finally
                        {
                            reader.Close();
                        }
                    }
                    else if (eachCartItem.type == CartItem.TYPE_MISCFOOD)
                    {
                        itemLabelsForPrint.Add("MISC FOOD");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_MISCDRINK)
                    {
                        itemLabelsForPrint.Add("MISC DRINK");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_PAID_CASH)
                    {
                        itemLabelsForPrint.Add("CASH");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_PAID_CREDIT)
                    {
                        itemLabelsForPrint.Add("CREDIT");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_PAID_UBEREATS)
                    {
                        itemLabelsForPrint.Add("UBER");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_PRICE_CHANGE)
                    {
                        itemLabelsForPrint.Add("CHANGE");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_DISCOUNT_ITEM)
                    {
                        itemLabelsForPrint.Add("        ITEM DISCOUNT");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_DISCOUNT_SALE)
                    {
                        itemLabelsForPrint.Add("SALE DISCOUNT");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                    else if (eachCartItem.type == CartItem.TYPE_DISCOUNT_FREECOFFEE)
                    {
                        itemLabelsForPrint.Add("FREE COFFEE");
                        pricesForPrint.Add(eachCartItem.price);
                    }
                }

                doc.Print();
            }
            catch { }
        }


        public void saveSale()
        {
            new SaleInfo(cart, Convert.ToInt32(curIdTable), totalPrice, paidMoney, cashierName, customerName, orderNoPerDay, orderNoWhole).saveToDatabase(this.sqlConn);

            // save order-numbers
            MySqlCommand cmd = new MySqlCommand("UPDATE tbl_last_sale SET last_date='" + DateTime.Now.ToString("yyyy-MM-dd") + "', order_no_day=" + orderNoPerDay + ", order_no_whole=" + orderNoWhole,
                                                sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                reader.Read();
            }
            catch
            {
                Console.WriteLine("Database error when saving sales data !");
            }
            finally
            {
                reader.Close();
            }
        }


        /// manage functions
        public void onManage_MenuItem(int itemCode = 0, String itemType = "Item")
        {
            if (itemType == "Item")
            {
                this.replaceRightFrameContent(new ItemEditPage(itemCode));
            }
            else if (itemType == "Plus")
            {
                this.replaceRightFrameContent(new PlusItemEditPage(itemCode));
            }
            else if (itemType == "Minus")
            {
                this.replaceRightFrameContent(new MinusItemEditPage(itemCode));
            }
        }
        public void onManage_MenuCategory(int itemCode = 0)
        {
            this.replaceRightFrameContent(new CategoryEditPage(itemCode));
        }

        public void replaceRightFrameContent(Page newContent)
        {
            mainGrid.Children.Remove(this.rightFrame);
            this.RemoveLogicalChild(this.rightFrame);
            this.rightFrame = null;
            this.rightFrame = new Frame();
            mainGrid.Children.Add(this.rightFrame);
            Grid.SetColumn(this.rightFrame, 1);
            this.rightFrame.Content = newContent;
        }
    }


    public partial class CartItem : Object
    {
        public int type;    // 1 : item, 2 : instruction, 3 : plus, 4 : misc food, 5 : misc drink, 9 : minus     11 : Cash Paid, 12 : Credit Debit Card Paid, 13 : Uber Eats Paid, 21 : Change
        public static int TYPE_ITEM         = 1;
        public static int TYPE_INSTRUCTION  = 2;
        public static int TYPE_PLUS         = 3;
        public static int TYPE_MINUS        = 9;
        public static int TYPE_MISCFOOD     = 4;
        public static int TYPE_MISCDRINK    = 5;

        public static int TYPE_PAID_CASH    = 11;
        public static int TYPE_PAID_CREDIT  = 12;
        public static int TYPE_PAID_UBEREATS = 13;
        public static int TYPE_PRICE_CHANGE = 21;

        public static int TYPE_DISCOUNT_ITEM = 31;
        public static int TYPE_DISCOUNT_SALE = 35;
        public static int TYPE_DISCOUNT_FREECOFFEE = 32;

        public int index;   // uid of tbl_items or tbl_instructions, tbl_plus, tbl_minus
        public double price;

        public CartItem()
        {
            type = TYPE_ITEM;
            index = 0;
            price = 0.0;
        }
        public CartItem(int t, int i, double p = 0.0)
        {
            type = t;
            index = i;
            price = p;
        }
    }

    public partial class SaleInfo : Object
    {
        public String date_time;
        public String cashier, customer;
        public int table_no;
        public String str_cart;
        public double total, money_paid;
        public int orderNoPerDay, orderNoWhole;
        public String paymentMode;

        public SaleInfo(List<CartItem> cart, int table_no, double total, double money_paid, String cashier, String customer, int orderNoPerDay, int orderNoWhole)
        {
            this.date_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            this.paymentMode = "";
            this.str_cart = CartInfoToString(cart);
            this.table_no = table_no;
            this.total = total;
            this.money_paid = money_paid;
            this.cashier = cashier;
            this.customer = customer;
            this.orderNoPerDay = orderNoPerDay;
            this.orderNoWhole = orderNoWhole;
        }

        public String CartInfoToString(List<CartItem> cart)
        {
            String ret = "";
            foreach (CartItem cartItem in cart)
            {
                if (cartItem.type == CartItem.TYPE_ITEM)
                {
                    ret += "_i" + cartItem.index;     // item
                }
                else if (cartItem.type == CartItem.TYPE_INSTRUCTION)
                {
                    ret += "_s" + cartItem.index;     // instruction
                }
                else if (cartItem.type == CartItem.TYPE_PLUS)
                {
                    ret += "_p" + cartItem.index;     // plus item
                }
                else if (cartItem.type == CartItem.TYPE_MINUS)
                {
                    ret += "_m" + cartItem.index;     // minus item
                }
                else if (cartItem.type == CartItem.TYPE_MISCFOOD)
                {
                    ret += "_x" + cartItem.price;     // misc food
                }
                else if (cartItem.type == CartItem.TYPE_MISCDRINK)
                {
                    ret += "_y" + cartItem.price;     // misc drink
                }
                else if (cartItem.type == CartItem.TYPE_PAID_CASH)
                {
                    ret += "_c" + cartItem.price;     // cash payment
                    paymentMode = "Cash";
                }
                else if (cartItem.type == CartItem.TYPE_PAID_CREDIT)
                {
                    ret += "_d" + cartItem.price;     // credit/debit payment
                    paymentMode = "Credit Card";
                }
                else if (cartItem.type == CartItem.TYPE_PAID_UBEREATS)
                {
                    ret += "_u" + cartItem.price;     // uber-eats payment
                    paymentMode = "Uber Eats";
                }
                else if (cartItem.type == CartItem.TYPE_PRICE_CHANGE)
                {
                    ret += "_r" + cartItem.price;     // change
                }
                else if (cartItem.type == CartItem.TYPE_DISCOUNT_ITEM)
                {
                    ret += "_b" + cartItem.price;     // item discount
                }
                else if (cartItem.type == CartItem.TYPE_DISCOUNT_SALE)
                {
                    ret += "_e" + cartItem.price;     // sale discount
                }
                else if (cartItem.type == CartItem.TYPE_DISCOUNT_FREECOFFEE)
                {
                    ret += "_f" + cartItem.price;     // free coffee
                }
            }

            return ret;
        }

        public void saveToDatabase(MySqlConnection sqlConn)
        {
            MySqlCommand cmd = new MySqlCommand("INSERT INTO tbl_sale (date_time, cashier, customer, table_no, str_cart, total, money_paid, order_no_day, order_no_whole, payment_mode) " +
                                                " VALUES('" + date_time + "', '" + cashier + "', '" + customer + "', " + table_no + ", '" + str_cart + "', " + total + ", " + money_paid + ", " + orderNoPerDay + ", " + orderNoWhole + ", '" + paymentMode + "')", 
                                                sqlConn);
            MySqlDataReader reader = cmd.ExecuteReader();
            try
            {
                reader.Read();
            }
            catch
            {
                Console.WriteLine("Database error when saving sales data !");
            }
            finally
            {
                reader.Close();
            }
        }
    }

    public class RawPrinterHelper
    {
        // Structure and API declarions:
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDataType;
        }
        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        // SendBytesToPrinter()
        // When the function is given a printer name and an unmanaged array
        // of bytes, the function sends those bytes to the print queue.
        // Returns true on success, false on failure.
        public static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, Int32 dwCount)
        {
            Int32 dwError = 0, dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            DOCINFOA di = new DOCINFOA();
            bool bSuccess = false; // Assume failure unless you specifically succeed.

            di.pDocName = "RAW Document";
            // Win7
            di.pDataType = "RAW";

            // Win8+
            // di.pDataType = "XPS_PASS";

            // Open the printer.
            if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
            {
                // Start a document.
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    // Start a page.
                    if (StartPagePrinter(hPrinter))
                    {
                        // Write your bytes.
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            // If you did not succeed, GetLastError may give more information
            // about why not.
            if (bSuccess == false)
            {
                dwError = Marshal.GetLastWin32Error();
            }
            return bSuccess;
        }

        public static bool SendFileToPrinter(string szPrinterName, string szFileName)
        {
            // Open the file.
            FileStream fs = new FileStream(szFileName, FileMode.Open);
            // Create a BinaryReader on the file.
            BinaryReader br = new BinaryReader(fs);
            // Dim an array of bytes big enough to hold the file's contents.
            Byte[] bytes = new Byte[fs.Length];
            bool bSuccess = false;
            // Your unmanaged pointer.
            IntPtr pUnmanagedBytes = new IntPtr(0);
            int nLength;

            nLength = Convert.ToInt32(fs.Length);
            // Read the contents of the file into the array.
            bytes = br.ReadBytes(nLength);
            // Allocate some unmanaged memory for those bytes.
            pUnmanagedBytes = Marshal.AllocCoTaskMem(nLength);
            // Copy the managed byte array into the unmanaged array.
            Marshal.Copy(bytes, 0, pUnmanagedBytes, nLength);
            // Send the unmanaged bytes to the printer.
            bSuccess = SendBytesToPrinter(szPrinterName, pUnmanagedBytes, nLength);
            // Free the unmanaged memory that you allocated earlier.
            Marshal.FreeCoTaskMem(pUnmanagedBytes);
            fs.Close();
            fs.Dispose();
            fs = null;
            return bSuccess;
        }
        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            IntPtr pBytes;
            Int32 dwCount;
            // How many characters are in the string?
            dwCount = szString.Length;
            // Assume that the printer is expecting ANSI text, and then convert
            // the string to ANSI text.
            pBytes = Marshal.StringToCoTaskMemAnsi(szString);
            // Send the converted ANSI string to the printer.
            SendBytesToPrinter(szPrinterName, pBytes, dwCount);
            Marshal.FreeCoTaskMem(pBytes);
            return true;
        }
    }
}
