using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
    /// Interaction logic for SearchingPageApp.xaml
    /// </summary>
    public partial class SearchingPageApp : Window
    {
        public SearchingPageApp()
        {
            InitializeComponent();

            bool connected = ConnectToDatabase();

            // pass the controls as parameters
            UpdateConnectionStatus(connectionStatusLbl, connStatusChkBox, connected);
        }

        // i added this to store the database connection to a class level field 
        private SqlConnection serverConnectionForSearchingPage;

        public bool ConnectToDatabase()
        {
            // then call the GetConnection method from another window to get the connection 
            serverConnectionForSearchingPage = ServerConnector.GetConnection();

            try
            {
                serverConnectionForSearchingPage.Open();

                // so I added this if condition is to be safe incase the database is not connected or desposed 
                // check if connection is open
                if (serverConnectionForSearchingPage.State == System.Data.ConnectionState.Open)
                {
                    // I can just refresh the connection here by closing and reopening it  
                    serverConnectionForSearchingPage.Close();
                    serverConnectionForSearchingPage.Open();

                    return true;
                }
                else if (serverConnectionForSearchingPage.State == System.Data.ConnectionState.Closed)
                {
                    // so if its closed then open it 
                    serverConnectionForSearchingPage.Open();

                    return serverConnectionForSearchingPage.State == System.Data.ConnectionState.Open;
                }
                else
                {
                    // here if their is any other states like Connecting, Broken, etc.
                    serverConnectionForSearchingPage.Open();
                    Console.WriteLine("Connection attempted from non-standard state.");

                    return serverConnectionForSearchingPage.State == System.Data.ConnectionState.Open;
                }
            }
            catch (SqlException ex)
            {
                // handle failed connection gracefully
                Console.WriteLine($"Database connection failed: {ex.Message}");
                return false;
            }


            
        }
        public void UpdateConnectionStatus(Label statusLabel, CheckBox statusCheckBox, bool isConnected)
        {
            if (isConnected)
            {
                // if the connection is connected then change the check box and its label into green
                statusLabel.Foreground = Brushes.Green;
                statusCheckBox.Background = Brushes.Green;
                statusCheckBox.Content = "ONLINE";
                statusCheckBox.Foreground = Brushes.Green;
            }
            else
            {
                // otherwise keep it red
                statusLabel.Foreground = Brushes.Red;
                statusCheckBox.Content = "OFFLINE";
                statusCheckBox.Background = Brushes.Red;
                statusCheckBox.Foreground = Brushes.Red;
            }
        }


    }
}
