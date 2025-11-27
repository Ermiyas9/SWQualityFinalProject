using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static GroundTerminalApp.FDMSDashboard;

namespace GroundTerminalApp
{
    /// <summary>
    /// Interaction logic for GroundTerminalDashboard.xaml
    /// </summary>
    public partial class FDMSDashboard : Window
    {
        public FDMSDashboard()
        {
            InitializeComponent();

            // so I can call that I created in another window, since that method takes label check box and bool variable i will pass those 
            var searchPage = new SearchingPageApp();
            searchPage.UpdateConnectionStatus(connectionStatusLbl, connStatChkBx, true);


        }




        // we need a base class for the dash board components here and with one render method
        public class DashBoardComponents
        {


            // then we will have a render method and this method will be overriden by the child classes if its neccessarly 
            public virtual void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart or any thing ...");
            }
        }


        // the second class i am thinking is the child class of the component for example if we need to display chart  
        public class ChartDisplay : DashBoardComponents
        {
            public override void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart: title with data  points.");
            }
        }

        // this class is for status of the connection
        public class ConnectionStatus : DashBoardComponents
        {
            public bool IsConnected { get; set; }

            public override void RenderingDashbrdComponents()
            {
                string status = IsConnected ? "Connected" : "Disconnected";
                Console.WriteLine($"Title: {status}");
            }
        }


        // this class is for packet counter display to display what it sent or what it recieved 
        public class TheCounterComponent : DashBoardComponents
        {
            public int Received { get; set; }
            public int Sent { get; set; }

            public override void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Title - Received: {Received}, Sent: {Sent}");
            }
        }

        public class SideBarComponents : DashBoardComponents
        {
            // we will use this class to render between windows like to go to search query setting and so ion
        }


        // I need a class level field that store and connet the app to the database. we already have the connection string in config file 
        // so this class will be a storage for the connection string  just easier to to use and access  the connection string from other classes or methods
        public static class ServerConnector
        {
            // this method is to store the connection string from config file
            private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["SoftwareQualityFinalProject"]?.ConnectionString;

            // another  method to create and return a new SqlConnection when ever we need too 
            public static SqlConnection GetConnection()
            {
                return new SqlConnection(ConnectionString);
            }
        }




         


}
}
