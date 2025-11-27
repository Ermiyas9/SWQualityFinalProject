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
            IsConnected(); 

            // so I am just calling this method to check that i am actually connected to the database 
            if (ConnectToDatabase())
            {
                
                connectionStatusLbl.Foreground = Brushes.Green;
                connStatusChkBox.Background = Brushes.Green;
                connStatusChkBox.Content = "ONLINE";
                connStatusChkBox.Foreground = Brushes.Green; 
            }
            else
            {
              
                connectionStatusLbl.Foreground = Brushes.Red;
                connStatusChkBox.Content = "OFFLINE";
                connStatusChkBox.Background = Brushes.Red;
                connStatusChkBox.Foreground = Brushes.Red;
            }
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
        public void IsConnected ()
        {
            // so I am just calling this method to check that i am actually connected to the database 
            if (ConnectToDatabase())
            {
                // if the connection is connected the i will change the check box and its lebal in to green
                connectionStatusLbl.Foreground = Brushes.Green;
                connStatusChkBox.Background = Brushes.Green;
                connStatusChkBox.Content = "ONLINE";
                connStatusChkBox.Foreground = Brushes.Green;
            }
            else
            {
                // other wise I will keep it red 
                connectionStatusLbl.Foreground = Brushes.Red;
                connStatusChkBox.Content = "OFFLINE";
                connStatusChkBox.Background = Brushes.Red;
                connStatusChkBox.Foreground = Brushes.Red;
            }
        }







    }
}
