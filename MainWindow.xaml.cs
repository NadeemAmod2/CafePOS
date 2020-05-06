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

namespace CafePOS
{
    public static class GlobalVars
    {
        public static MainPage wndMainPage;

        public static int loggedInUser = 0;
        public static String loggedInUserRole = "";

        public static String receiptName = "Seasoned";
        public static String phoneNumber = "04 02 00222333";
        public static String ABN = "12 123 345 435";

        public static Boolean isLoggedIn()
        {
            return loggedInUser != 0;
        }
        public static Boolean isAdminLoggedIn()
        {
            return loggedInUserRole == "Admin";
        }
        public static Boolean isManagerLoggedIn()
        {
            return loggedInUserRole == "Manager";
        }
        public static Boolean isCashierLoggedIn()
        {
            return loggedInUserRole == "Cashier";
        }


        public static String PRINTERNAME_FOOD = "SENOR GTP-250II";
        public static String PRINTERNAME_DRINK = "EPSON TM-m30 Receipt";
        public static String PRINTERNAME_MAIN = "EPSON TM-m30 Receipt";
        //public static String PRINTERNAME_FOOD = "Microsoft Print to PDF";
        //public static String PRINTERNAME_DRINK = "Microsoft Print to PDF";
        //public static String PRINTERNAME_MAIN = "Microsoft Print to PDF";
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Threading.DispatcherTimer timer;
        MainPage mainPage;
        
        public MainWindow()
        {
            InitializeComponent();
            mainFrame.Content = new LoadingPage();

            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += onAfterLoad;
            timer.Interval = new TimeSpan(0, 0, 3);
            timer.Start();
            GlobalVars.wndMainPage = mainPage = new MainPage();
        }

        public void onAfterLoad(Object source, EventArgs e)
        {
            WindowState = WindowState.Maximized;
            mainFrame.Content = mainPage;
            timer.Stop();

            // for screen which is faced to customers ; customer-screen
            mainPage.wndCustomer = new CustomerWindow();
            System.Windows.Forms.Screen s1;
            bool dualMonitor = true;
            try
            {
                s1 = System.Windows.Forms.Screen.AllScreens[1];
                dualMonitor = true;
            }
            catch
            {
                s1 = System.Windows.Forms.Screen.AllScreens[0];
                dualMonitor = false;
            }
            System.Drawing.Rectangle r1 = s1.WorkingArea;
            mainPage.wndCustomer.WindowState = System.Windows.WindowState.Normal;
            mainPage.wndCustomer.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            mainPage.wndCustomer.Top = r1.Top;
            mainPage.wndCustomer.Left = r1.Left;
            mainPage.wndCustomer.Show();
            mainPage.wndCustomer.WindowState = System.Windows.WindowState.Maximized;
            

            if (dualMonitor)
            {
                // screen-saver;  for now, it will be hidden
                mainPage.wndScreenSaver = new ScreenSaverWindow(r1.Width, r1.Height);
                mainPage.wndScreenSaver.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                mainPage.wndScreenSaver.Hide();
            }
            else
            {
                mainPage.wndScreenSaver = null;
            }
        }
    }
}
