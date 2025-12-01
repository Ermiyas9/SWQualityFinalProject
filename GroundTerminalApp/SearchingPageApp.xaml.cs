using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Remoting.Channels;
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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using static GroundTerminalApp.FDMSDashboard;

namespace GroundTerminalApp
{
    /// <summary>
    /// Interaction logic for SearchingPageApp.xaml
    /// </summary>
    public partial class SearchingPageApp : Window
    {
        public SearchingPageApp(FDMSDashboard dashboardInstance) // I am letting this page to pass the object as param
        {
            InitializeComponent();

            bool connected = ConnectToDatabase();

            // pass the controls as parameters
            UpdateConnectionStatus(connectionStatusLbl, onlineIcon, offlineIcon, connected);
            DataContext = this;
            dashboard = dashboardInstance;

            // Start chart timer here so i can display the live data from dashboard 
            DispatcherTimer chartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // update every second
            };

            // each tick will call UpdateAltitudeTrend to refresh the snapshot points
            chartTimer.Tick += (s, e) => LiveUpdateTelemetryLabel();
            chartTimer.Start();
        
        }

        private FDMSDashboard dashboard;

        // Declare AltitudePoints as an observable collection of Points to do the live data display
        public ObservableCollection<Point> AltitudePoints { get; set; } = new ObservableCollection<Point>();

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

            LoadData(userInput); 
        }

        // I am creating a method that update ui 
        private void LoadData(string userInput)
        {
            List<string> displayItems = new List<string>();


            if (GetTargetTableFromInput(userInput) == 1)
            {
                // take of the first char
                char firstCharacter = userInput[0];

                // The rest of the string ID
                string theIDPart = userInput.Substring(1);

                // then convert it to int
                int idPart = ParseInt(theIDPart);

                getFlightTableData(idPart);

                // then update the ui 
                updateUI(userInput);


            }
            else if (GetTargetTableFromInput(userInput) == 2)
            {

                // take of the first char
                char firstCharacter = userInput[0];

                // The rest of the string ID
                string theIDPart = userInput.Substring(1);

                // then convert it to int
                int idPart = ParseInt(theIDPart);

                // call the Channel query method 
                getChannelTableData(idPart);

                // DEBUGGING if u ever see this line delete it before we submit 
                //MessageBox.Show($"ID {idPart}");


                 // then update the ui 
                 updateUI(userInput);
            }
            else if (GetTargetTableFromInput(userInput) == 3)
            {
                // take of the first char
                char firstCharacter = userInput[0];

                // The rest of the string ID
                string theIDPart = userInput.Substring(1);

                // then convert it to int
                int idPart = ParseInt(theIDPart);

                // send it to telemetry query to get the table 
                getDataFrmAircraftTransmitterPackets(idPart);

                // then update the ui 
                updateUI(userInput);
            }
            else
            {
                // other wise their is some issue let user to check their input again 
                MessageBox.Show("Please enter F, T, or C followed by the ID: F = Flight, T = Telemetry Package, C = Channel (e.g., F101, T25, C7)");

                return;

            }
        }





        private void updateUI(string userInput)
        {
            int returnedValue = GetTargetTableFromInput(userInput);
            var displayItems = new List<string>();

            // Flight data
            if (returnedValue == 1 && flightsInfoList.Any())
            {
                var f = flightsInfoList.First();
                displayItems.Add($"Flight ID: {f.FlightId}");
                displayItems.Add($"Aircraft ID: {f.AircraftId}");
                displayItems.Add($"Flight Code: {f.FlightCode}");
                displayItems.Add($"Departure Time: {f.DepartureTime:g}");
                displayItems.Add($"Arrival Time: {(f.ArrivalTime.HasValue ? f.ArrivalTime.Value.ToString("g") : "N/A")}");
            }
            // Channel data
            else if (returnedValue == 2 && channelInfoList.Any())
            {
                var c = channelInfoList.First();
                displayItems.Add($"Channel ID: {c.ChannelId}");
                displayItems.Add($"Channel Name: {c.ChannelName}");
                displayItems.Add($"Channel Code: {c.ChannelCode}");
                displayItems.Add($"Description: \n{c.Description}");
            }
            // Telemetry data
            else if (returnedValue == 3 && telemetryList.Any())
            {
                var t = telemetryList.First();
                displayItems.Add($"Telemetry ID: {t.TelemetryId}");
                displayItems.Add($"Timestamp: {t.Timestamp:g}");
                displayItems.Add($"Tail Number: {t.TailNumber}");
                displayItems.Add($"Checksum: {t.Checksum}");
                displayItems.Add($"Altitude: {t.Altitude} ft");
                displayItems.Add($"Pitch: {t.Pitch}°");
                displayItems.Add($"Bank: {t.Bank}°");
                displayItems.Add($"Accel X: {t.AccelX}");
                displayItems.Add($"Accel Y: {t.AccelY}");
                displayItems.Add($"Accel Z: {t.AccelZ}");
            }
            else
            {
                displayItems.Add("Invalid input. Please start with F, T, or C.");
            }

            // Bind results to UI
            SearchIndxListBox.ItemsSource = displayItems;
        }




        private void LiveUpdateTelemetryLabel()
        {
            var latestPacket = dashboard.telemetryDataList.LastOrDefault();
            if (latestPacket == null) return;

            QueryStatusChip.Text = "Live Data Ready";

            ResultsHeaderLbl.Content = " Reciving a value of 6 Values";

            // create a new display object with formatted strings
            var displayValues = new TelemetryDataNameAndValues
            {

                TailNumber = $"TailNumber = {latestPacket.TailNumber}",
                Pitch = $"Pitch = {latestPacket.Pitch:F2}",
                AccelX = $"AccelX = {latestPacket.AccelX:F3}",
                AccelY = $"AccelY = {latestPacket.AccelY:F3}",
                AccelZ = $"AccelZ = {latestPacket.AccelZ:F3}",
                Bank = $"Bank = {latestPacket.Bank:F2}",
            };

            // update the DataContext so the XAML labels refresh
            this.DataContext = displayValues;
        }




        private double NormalizeAltitude(double altitude)
        {
            double maxAltitude = 40000; 
            return (altitude / maxAltitude) * 140;
        }


        /*  
         *  METHOD          : ParseInt
         *  RETURN TYPE     : int
         *  PARAMETERS      : string valueOfCell         -> String value to parse into integer.
         *  DESCRIPTION     : Safely parses string values into integers.
         *                    Returns 0 if parsing fails or value is invalid.
         */
        int ParseInt(string valueOfCell)
        {
            // so first i need to check if the incoming string value is null, empty or just a dash
            // if that is the case then i will simply return 0 since it is not a valid number
            if (string.IsNullOrWhiteSpace(valueOfCell) || valueOfCell.Trim() == "-")
                return 0;

            try
            {
                // here i will try to parse the string value into an integer
                // if the parsing works fine then i will return the integer value
                return int.Parse(valueOfCell.Trim());
            }
            catch
            {
                // but if parsing fails for any reason then i will catch the error
                // and return 0 as a safe default value instead of crashing the app
                return 0;
            }
        }






        // another method that check the first character and decide which table to query
        private int GetTargetTableFromInput(string userInput)
        {
            char firstCharacter = userInput[0];

            // so if this method returns true, then check the firstcharacter and tell me if it is Flight query, Channer or packate
            if (ParseUserInput(userInput,  firstCharacter))
            {
                if (firstCharacter == 'F')
                {
                    return 1;
                }
                else if (firstCharacter == 'C')
                {
                    return 2;
                }
                else if (firstCharacter == 'T')
                {
                    return 3;
                }      
            }

            return 0; 
        }


        // A method to parse user input
        private bool ParseUserInput(string userInput, char firstCharacter)
        {
            // Check if input is empty or null first
            if (string.IsNullOrWhiteSpace(userInput))
            {
                MessageBox.Show("Input cannot be empty. Please enter F, T, or C followed by the ID.");
                return false;
            }

            // Only check the first character
            firstCharacter = userInput[0];

            if (firstCharacter == 'F' || firstCharacter == 'T' || firstCharacter == 'C')
            {
                // its valid input return true other than that return false 
                // also send the first character in return 
                return true;
            }
            else
            {
                MessageBox.Show("Please enter F, T, or C followed by the ID: F = Flight, T = Telemetry Package, C = Channel (e.g., F101, T25, C7)");
                return false; 
            }
        }

        public void getFlightTableData(int userInputIDPart)
        {
            string sqlQuery = $@"
                                SELECT FlightId, AircraftId, FlightCode, DepartureTime, ArrivalTime
                                FROM dbo.Flight
                                WHERE FlightId = @FlightId";


            // so we got the query string which store the sql query, the source table name string and the list or dictionary that stores the output data 
            // then we run the reader 
            using (var cmd = new SqlCommand(sqlQuery, serverConnectionForSearchingPage))
            {
                // i need a parameter before i excute the reader 
                cmd.Parameters.AddWithValue("@FlightId", userInputIDPart);

                // the i will excute a reader and import and save the values in the list of worker info
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    

                    while (reader.Read())
                    {
                        // Convert each value to its own type
                        int flightId = reader.GetInt32(reader.GetOrdinal("FlightId"));
                        int aircraftId = reader.GetInt32(reader.GetOrdinal("AircraftId"));
                        string flightCode = reader["FlightCode"].ToString();
                        DateTime departureTime = reader.GetDateTime(reader.GetOrdinal("DepartureTime"));
                        DateTime? arrivalTime = reader.IsDBNull(reader.GetOrdinal("ArrivalTime"))
                            ? (DateTime?)null
                            : reader.GetDateTime(reader.GetOrdinal("ArrivalTime"));

                        // Store them into local object
                        FlightInfo flight = new FlightInfo
                        {
                            FlightId = flightId,
                            AircraftId = aircraftId,
                            FlightCode = flightCode,
                            DepartureTime = departureTime,
                            ArrivalTime = arrivalTime
                        };

                        // Add to list
                        flightsInfoList.Add(flight);
                    }

                }
            }

        }


        // method to get the datas from AircraftTransmitterPackets table 
        private void getDataFrmAircraftTransmitterPackets(int userInputIDPart)
        {
            string sqlQuery = @"
                                SELECT * 
                                FROM dbo.AircraftTransmitterPackets 
                                WHERE TelemetryId = @TelemetryId 
                                ORDER BY TelemetryId;";



            using (var cmd = new SqlCommand(sqlQuery, serverConnectionForSearchingPage))
            {
                // i need a parameter before i excute the reader 
                cmd.Parameters.AddWithValue("@TelemetryId", userInputIDPart);

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

                        telemetryList.Add(telemetry);
                    }

                }
            }
        }
      

        public void getChannelTableData(int userInputIDPart)
        {
            string sqlQuery = @"
                                SELECT ChannelId, ChannelName, ChannelCode, Description 
                                FROM Channel 
                                WHERE ChannelId = @ChannelId";



            // so we got the query string which store the sql query, the source table name string and the list or dictionary that stores the output data 
            // then we run the reader 
            using (var cmd = new SqlCommand(sqlQuery, serverConnectionForSearchingPage))
            {
                // i need a parameter before i excute the reader 
                cmd.Parameters.AddWithValue("@ChannelId", userInputIDPart);

                // the i will excute a reader and import and save the values in the list of worker info
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    // I will read the data from the reader and save it in the list for each rows
                    while (reader.Read())
                    {
                        // convert each value to its own type and add it to the list
                        // then store them into local
                        ChannelInfo channel = new ChannelInfo
                        {
                            ChannelId = reader.GetInt32(reader.GetOrdinal("ChannelID")),
                            ChannelName = reader["ChannelName"].ToString(),
                            ChannelCode = reader["ChannelCode"].ToString(),
                            Description = reader["Description"].ToString()
                        };

                        channelInfoList.Add(channel);

                    }
                }
            }

        }

        // i added this to store the database connection to a class level field 
        private SqlConnection serverConnectionForSearchingPage;

        private List<ChannelInfo> channelInfoList = new List<ChannelInfo>();

        private List<FlightInfo> flightsInfoList = new List<FlightInfo>();

        // this class I am using it to store the data that comes from the table and I will make it list object
        private List<TelemetryData> telemetryList = new List<TelemetryData>();
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


        // this class will store the information that comes from Channel table 
        public class ChannelInfo
        {
            public int ChannelId { get; set; }
            public string ChannelName { get; set; }
            public string ChannelCode { get; set; }
            public string Description { get; set; }
        }

        public class FlightInfo
        {
            public int FlightId { get; set; }
            public int AircraftId { get; set; }
            public string FlightCode { get; set; }
            public DateTime DepartureTime { get; set; }
            public DateTime? ArrivalTime { get; set; }
        }

        // this class is a control for the line point in the graph so when ever it updates it will
        // display the temetry data in x their name and y their values 
        public class TelemetryDataNameAndValues
        {
            // each property formats the telemetry field as "Name = Value"
            public string TailNumber {  get; set; }
            public string Attitude {  get; set; }
            public string Pitch {  get; set; }
            public string AccelX { get; set; }
            public string AccelY { get; set; }
            public string AccelZ {  get; set; }
            public string Bank { get;  set; }
            public string CheckSum { get; set; } 
            public string RecievedPacketNumber { get; set; }

        }






    }

}
