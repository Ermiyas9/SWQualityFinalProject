using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
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
            UpdateConnectionStatus(connectionStatusLbl, onlineIcon, offlineIcon, connected);
        }

        // i added this to store the database connection to a class level field 
        private SqlConnection serverConnectionForSearchingPage;

        // this class I am using it to store the data that comes from the table and I will make it list object
        private List<TelemetryData> telemetryData = new List<TelemetryData>();
        public class TelemetryData
        {
            public int TelemetryId { get; set; }
            public DateTime Timestamp { get; set; }
            public string TailNumber { get; set; }
            public int Checksum { get; set; }
            public double Altitude { get; set; }
            public double Pitch { get; set; }
            public double Bank { get; set; }
            public double AccelX { get; set; }
            public double AccelY { get; set; }
            public double AccelZ { get; set; }
        }



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
        public void UpdateConnectionStatus(Label connectionStatusLbl, Image onlineIcon, Image offlineIcon, bool isConnected)
        {

            if (isConnected)
            {
                // if the connection is connected then change the check box and its label into green
                // so when its online i will show the online icon that i got
                // from https://icons8.com/ and when its offline will do the same but offline icon
                connectionStatusLbl.Foreground = Brushes.Green;
                connectionStatusLbl.Content = "ONLINE";
                onlineIcon.Visibility = Visibility.Visible;
                offlineIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                // otherwise keep it red
                connectionStatusLbl.Foreground = Brushes.Red;
                connectionStatusLbl.Content = "OFFLINE"; // fixed to show OFFLINE correctly
                offlineIcon.Visibility = Visibility.Visible;
                onlineIcon.Visibility = Visibility.Collapsed;
            }
        }


        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string userInput = SearchBoxTxtBx.Text.Trim();

            if (string.IsNullOrEmpty(userInput))
            {
                MessageBox.Show("Please enter a FlightID, TelemetryID, or ChannelID to search.");

                return;
            }

            string theSourceTableName = "Channel";

            string theSQLQuery = $@"SELECT ChannelName, ChannelCode, Description 
                                   FROM {theSourceTableName} 
                                   WHERE ChannelId = @ChannelId";


            // Create the output list
            List<string> theOutputData = new List<string>();

            // then call the load data method to get the datas from the database 
            LoadDataFromDatabase(theSQLQuery, theOutputData);

            // bind the entire list to the ListBox
            SearchIndxListBox.ItemsSource = theOutputData;

            //for (int i = 0; i < theOutputData.Count; i++)
            //{
            //    // the we will bind the results to the ListBox to display 
            //    SearchIndxListBox.ItemsSource = theOutputData[i];
            //}
        }


        // a method to parse user input 
        private void ParseUserInput(string userInput)
        {

        }


        // method to get the datas from AircraftTransmitterPackets table 
        private void getDataFrmAircraftTransmitterPackets(string userInputValue)
        {
            string sqlQuery = $"SELECT * FROM dbo.AircraftTransmitterPackets WHERE @TelemetryId = {userInputValue} ORDER BY TelemetryId;";



            using (var cmd = new SqlCommand(sqlQuery, serverConnectionForSearchingPage))
            {
                // i need a parameter before i excute the reader 
                cmd.Parameters.AddWithValue($"@{userInputValue}", SearchBoxTxtBx.Text.Trim());

                // the i will excute a reader and import and save the values in the list of worker info
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    // I will read the data from the reader and save it in the list for each rows
                    while (reader.Read())
                    {

                        var telemetry = new TelemetryData
                        {
                            TelemetryId = reader.GetInt32(reader.GetOrdinal("TelemetryId")),
                            Timestamp = reader.GetDateTime(reader.GetOrdinal("SampleTimeStamp")),
                            TailNumber = reader.GetString(reader.GetOrdinal("TailNumber")),
                            Checksum = reader.GetInt32(reader.GetOrdinal("Checksum")),
                            Altitude = reader.GetDouble(reader.GetOrdinal("Altitude")),
                            Pitch = reader.GetDouble(reader.GetOrdinal("Pitch")),
                            Bank = reader.GetDouble(reader.GetOrdinal("Bank")),
                            AccelX = reader.GetDouble(reader.GetOrdinal("AccelX")),
                            AccelY = reader.GetDouble(reader.GetOrdinal("AccelY")),
                            AccelZ = reader.GetDouble(reader.GetOrdinal("AccelZ"))
                        };

                        telemetryData.Add(telemetry);
                    }

                }
            }
        }
        // here I am thinking a generic method that we can call it from any class 
        // this method will accept query string , table name , and a list which is the results that comes from the database 
        public void LoadDataFromDatabase(string theSQLQuery, List<string> theOutputData)
        {
            // so we got the query string which store the sql query, the source table name string and the list or dictionary that stores the output data 
            // then we run the reader 
            using (var cmd = new SqlCommand(theSQLQuery, serverConnectionForSearchingPage))
            {
                // i need a parameter before i excute the reader 
                cmd.Parameters.AddWithValue("@ChannelId", SearchBoxTxtBx.Text.Trim());

                // the i will excute a reader and import and save the values in the list of worker info
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    // I will read the data from the reader and save it in the list for each rows
                    while (reader.Read())
                    {
                        // convert each value to its own type and add it to the list
                        // I dont thing i need to convert them now since the text box only accept string value
                        // oh maybe i need to make convert them to string? 
                        string channelName = reader["ChannelName"].ToString();
                        string channelCode = reader["ChannelCode"].ToString();
                        string description = reader["Description"].ToString();

                        // then i ombine into one string for display in ListBox
                        string displayRow = $"{channelName} - {channelCode} - {description}";
                        theOutputData.Add(displayRow);



                    }
                }
            }

        }

    }

}
