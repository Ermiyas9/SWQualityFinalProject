/* ======================================================================================================================== */
/* FILE             : SearchingPageApp.xaml.cs                                                                              */
/* PROJECT          : Software Quality Final Project Milestone 2                                                            */
/* NAMESPACE        : GroundTerminalApp                                                                                    */
/* PROGRAMMER       : Ermiyas (Endalkachew) Gulti, Mher Keshishian, Quang Minh Vu, Saje’- Antoine Rose                      */
/* FIRST VERSION    : 2025-11-22                                                                                            */
/* DESCRIPTION      : Defines the SearchingPageApp window for querying FDMS data and viewing live telemetry snapshots.     */
/* ======================================================================================================================== */


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



#pragma warning disable IDE0044 // Make field readonly


namespace GroundTerminalApp
{
    /// <summary>
    /// Interaction logic for SearchingPageApp.xaml
    /// </summary>
    public partial class SearchingPageApp : Window
    {
        /*
         Method: SearchingPageApp (Constructor)
         Description: Initializes search page and establishes database connection
         Receives dashboard instance reference and starts live telemetry update timer
         Parameters: FDMSDashboard dashboardInstance
         Returns: void
         */
        public SearchingPageApp(FDMSDashboard dashboardInstance)
        {
            InitializeComponent();

            bool connected = ConnectToDatabase();

            // Update UI connection indicators based on database state
            UpdateConnectionStatus(connectionStatusLbl, onlineIcon, offlineIcon, connected);
            DataContext = this;
            dashboard = dashboardInstance;

            // Initialize timer for live telemetry data refresh
            DispatcherTimer chartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            // Refresh telemetry display on each timer tick
            chartTimer.Tick += (s, e) => LiveUpdateTelemetryLabel();
            chartTimer.Start();
        
        }

        private FDMSDashboard dashboard;

        // Observable collection for altitude chart point updates
        public ObservableCollection<Point> AltitudePoints { get; set; } = new ObservableCollection<Point>();

        /*
         Method: ConnectToDatabase
         Description: Establishes connection to SQL Server and validates connection state
         Handles connection refresh and state validation for robustness
         Parameters: None
         Returns: bool - true if connection successful and open, false otherwise
         */
        public bool ConnectToDatabase()
        {
            // Retrieve connection from centralized ServerConnector
            serverConnectionForSearchingPage = ServerConnector.GetConnection();

            try
            {
                serverConnectionForSearchingPage.Open();

                // Validate connection state before proceeding
                if (serverConnectionForSearchingPage.State == System.Data.ConnectionState.Open)
                {
                    // Refresh connection by closing and reopening
                    serverConnectionForSearchingPage.Close();
                    serverConnectionForSearchingPage.Open();

                    return true;
                }
                else if (serverConnectionForSearchingPage.State == System.Data.ConnectionState.Closed)
                {
                    // Attempt to open closed connection
                    serverConnectionForSearchingPage.Open();

                    return serverConnectionForSearchingPage.State == System.Data.ConnectionState.Open;
                }
                else
                {
                    // Handle non-standard states (Connecting, Broken, etc.)
                    serverConnectionForSearchingPage.Open();
                    Console.WriteLine("Connection attempted from non-standard state.");

                    return serverConnectionForSearchingPage.State == System.Data.ConnectionState.Open;
                }
            }
            catch (SqlException ex)
            {
                // Log connection failure and return false
                Console.WriteLine($"Database connection failed: {ex.Message}");
                return false;
            }
        }

        /*
         Method: UpdateConnectionStatus
         Description: Updates UI labels and icons to reflect database connection state
         Parameters: Label connectionStatusLbl, Image onlineIcon, Image offlineIcon, bool isConnected
         Returns: void
         */
        public void UpdateConnectionStatus(Label connectionStatusLbl, Image onlineIcon, Image offlineIcon, bool isConnected)
        {
            if (isConnected)
            {
                // Display green indicator and online status
                connectionStatusLbl.Foreground = Brushes.Green;
                connectionStatusLbl.Content = "ONLINE";
                onlineIcon.Visibility = Visibility.Visible;
                offlineIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Display red indicator and offline status
                connectionStatusLbl.Foreground = Brushes.Red;
                connectionStatusLbl.Content = "OFFLINE";
                offlineIcon.Visibility = Visibility.Visible;
                onlineIcon.Visibility = Visibility.Collapsed;
            }
        }

        /*
         Method: SearchButton_Click
         Description: Event handler for search button click
         Retrieves user input and triggers data load operation
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string userInput = SearchBoxTxtBx.Text.Trim();

            LoadData(userInput); 
        }

        /*
         Method: LoadData
         Description: Parses user input and routes query to appropriate database table
         Validates input format and loads Flight, Channel, or Telemetry data
         Parameters: string userInput
         Returns: void
         */
        private void LoadData(string userInput)
        {
            //List<string> displayItems = new List<string>();

            if (GetTargetTableFromInput(userInput) == 1)
            {
                // Extract numeric portion from Flight query format (Fxxx)
                //char firstCharacter = userInput[0];
                string theIDPart = userInput.Substring(1);
                int idPart = ParseInt(theIDPart);

                // Query Flight table with parsed ID
                GetFlightTableData(idPart);
                UpdateUI(userInput);
            }
            else if (GetTargetTableFromInput(userInput) == 2)
            {
                // Extract numeric portion from Channel query format (Cxxx)
                //char firstCharacter = userInput[0];
                string theIDPart = userInput.Substring(1);
                int idPart = ParseInt(theIDPart);

                // Query Channel table with parsed ID
                GetChannelTableData(idPart);
                UpdateUI(userInput);
            }
            else if (GetTargetTableFromInput(userInput) == 3)
            {
                // Extract numeric portion from Telemetry query format (Txxx)
                //char firstCharacter = userInput[0];
                string theIDPart = userInput.Substring(1);
                int idPart = ParseInt(theIDPart);

                // Query AircraftTransmitterPackets table with parsed ID
                GetDataFrmAircraftTransmitterPackets(idPart);
                UpdateUI(userInput);
            }
            else
            {
                // Invalid input format - notify user of expected format
                MessageBox.Show("Please enter F, T, or C followed by the ID: F = Flight, T = Telemetry Package, C = Channel (e.g., F101, T25, C7)");
                return;
            }
        }

        /*
         Method: updateUI
         Description: Formats and displays query results in search results listbox
         Populates display with Flight, Channel, or Telemetry data from loaded lists
         Parameters: string userInput
         Returns: void
         */
        private void UpdateUI(string userInput)
        {
            int returnedValue = GetTargetTableFromInput(userInput);
            var displayItems = new List<string>();

            // Flight data display format
            if (returnedValue == 1 && flightsInfoList.Any())
            {
                var f = flightsInfoList.First();
                displayItems.Add($"Flight ID: {f.FlightId}");
                displayItems.Add($"Aircraft ID: {f.AircraftId}");
                displayItems.Add($"Flight Code: {f.FlightCode}");
                displayItems.Add($"Departure Time: {f.DepartureTime:g}");
                displayItems.Add($"Arrival Time: {(f.ArrivalTime.HasValue ? f.ArrivalTime.Value.ToString("g") : "N/A")}");
            }
            // Channel data display format
            else if (returnedValue == 2 && channelInfoList.Any())
            {
                var c = channelInfoList.First();
                displayItems.Add($"Channel ID: {c.ChannelId}");
                displayItems.Add($"Channel Name: {c.ChannelName}");
                displayItems.Add($"Channel Code: {c.ChannelCode}");
                displayItems.Add($"Description: \n{c.Description}");
            }
            // Telemetry data display format
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

            // Bind formatted results to listbox
            SearchIndxListBox.ItemsSource = displayItems;
        }

        /*
         Method: LiveUpdateTelemetryLabel
         Description: Updates telemetry display with latest data from dashboard feed
         Formats values and refreshes UI labels with current packet information
         Parameters: None
         Returns: void
         */
        private void LiveUpdateTelemetryLabel()
        {
            // Retrieve latest telemetry packet from dashboard stream
            var latestPacket = dashboard.telemetryDataList.LastOrDefault();
            if (latestPacket == null) return;

            QueryStatusChip.Text = "Live Data Ready";
            ResultsHeaderLbl.Content = " Reciving a value of 6 Values";

            // Format telemetry values for display
            var displayValues = new TelemetryDataNameAndValues
            {
                TailNumber = $"TailNumber = {latestPacket.TailNumber}",
                Pitch = $"Pitch = {latestPacket.Pitch:F2}",
                AccelX = $"AccelX = {latestPacket.AccelX:F3}",
                AccelY = $"AccelY = {latestPacket.AccelY:F3}",
                AccelZ = $"AccelZ = {latestPacket.AccelZ:F3}",
                Bank = $"Bank = {latestPacket.Bank:F2}",
            };

            // Bind to DataContext to trigger UI label refresh
            this.DataContext = displayValues;
        }


        /*
         Method: ParseInt
         Description: Safely parses string values to integers with validation
         Returns 0 if input is null, empty, or parsing fails
         Parameters: string valueOfCell
         Returns: int - parsed integer or 0 if invalid
         */
        public int ParseInt(string valueOfCell)
        {
            // Reject null, empty, whitespace, or dash-only strings
            if (string.IsNullOrWhiteSpace(valueOfCell) || valueOfCell.Trim() == "-")
                return 0;

            try
            {
                // Attempt to parse string to integer
                return int.Parse(valueOfCell.Trim());
            }
            catch
            {
                // Return safe default on parsing failure
                return 0;
            }
        }

        /*
         Method: GetTargetTableFromInput
         Description: Determines target database table based on user input prefix
         Validates input format and returns table identifier (1=Flight, 2=Channel, 3=Telemetry)
         Parameters: string userInput
         Returns: int - 1 for Flight, 2 for Channel, 3 for Telemetry, 0 for invalid
         */
        private int GetTargetTableFromInput(string userInput)
        {
            //char firstCharacter = userInput[0];

            // Route to appropriate table based on validated input prefix
            if (ParseUserInput(userInput))
            {
                if (userInput[0] == 'F')
                {
                    return 1;
                }
                else if (userInput[0] == 'C')
                {
                    return 2;
                }
                else if (userInput[0] == 'T')
                {
                    return 3;
                }      
            }

            return 0; 
        }

        /*
         Method: ParseUserInput
         Description: Validates user input format and prefix character
         Checks for empty input and valid prefixes (F, T, C)
         Parameters: string userInput, char firstCharacter
         Returns: bool - true if input is valid, false otherwise
         */
        private bool ParseUserInput(string userInput)
        {
            // Reject null or empty input
            if (string.IsNullOrWhiteSpace(userInput))
            {
                MessageBox.Show("Input cannot be empty. Please enter F, T, or C followed by the ID.");
                return false;
            }

            // Extract and validate first character
            //firstCharacter = userInput[0];

            if (userInput[0] == 'F' || userInput[0] == 'T' || userInput[0] == 'C')
            {
                // Valid prefix found
                return true;
            }
            else
            {
                // Invalid prefix - notify user of expected format
                MessageBox.Show("Please enter F, T, or C followed by the ID: F = Flight, T = Telemetry Package, C = Channel (e.g., F101, T25, C7)");
                return false; 
            }
        }

        /*
         Method: getFlightTableData
         Description: Queries Flight table and retrieves flight record by ID
         Populates flightsInfoList with flight details including times and aircraft reference
         Parameters: int userInputIDPart
         Returns: void
         */
        public void GetFlightTableData(int userInputIDPart)
        {
            string sqlQuery = $@"
                                SELECT FlightId, AircraftId, FlightCode, DepartureTime, ArrivalTime
                                FROM dbo.Flight
                                WHERE FlightId = @FlightId";

            using (var cmd = new SqlCommand(sqlQuery, serverConnectionForSearchingPage))
            {
                // Parameterize query to prevent SQL injection
                cmd.Parameters.AddWithValue("@FlightId", userInputIDPart);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Extract and type-cast database values
                        int flightId = reader.GetInt32(reader.GetOrdinal("FlightId"));
                        int aircraftId = reader.GetInt32(reader.GetOrdinal("AircraftId"));
                        string flightCode = reader["FlightCode"].ToString();
                        DateTime departureTime = reader.GetDateTime(reader.GetOrdinal("DepartureTime"));
                        DateTime? arrivalTime = reader.IsDBNull(reader.GetOrdinal("ArrivalTime"))
                            ? (DateTime?)null
                            : reader.GetDateTime(reader.GetOrdinal("ArrivalTime"));

                        // Create and add Flight object to list
                        FlightInfo flight = new FlightInfo
                        {
                            FlightId = flightId,
                            AircraftId = aircraftId,
                            FlightCode = flightCode,
                            DepartureTime = departureTime,
                            ArrivalTime = arrivalTime
                        };

                        flightsInfoList.Add(flight);
                    }
                }
            }
        }

        /*
         Method: getDataFrmAircraftTransmitterPackets
         Description: Queries AircraftTransmitterPackets table for telemetry data by ID
         Retrieves all telemetry fields including acceleration, orientation, and altitude
         Parameters: int userInputIDPart
         Returns: void
         */
        private void GetDataFrmAircraftTransmitterPackets(int userInputIDPart)
        {
            string sqlQuery = @"
                                SELECT * 
                                FROM dbo.AircraftTransmitterPackets 
                                WHERE TelemetryId = @TelemetryId 
                                ORDER BY TelemetryId;";

            using (var cmd = new SqlCommand(sqlQuery, serverConnectionForSearchingPage))
            {
                // Parameterize query to prevent SQL injection
                cmd.Parameters.AddWithValue("@TelemetryId", userInputIDPart);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Extract telemetry packet fields and type-cast
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

        /*
         Method: getChannelTableData
         Description: Queries Channel table and retrieves channel record by ID
         Populates channelInfoList with channel details including name, code, and description
         Parameters: int userInputIDPart
         Returns: void
         */
        public void GetChannelTableData(int userInputIDPart)
        {
            string sqlQuery = @"
                                SELECT ChannelId, ChannelName, ChannelCode, Description
                                FROM Channel
                                WHERE ChannelId = @ChannelId";

            try
            {
                // Ensure connection exists and is open
                if (serverConnectionForSearchingPage == null)
                    throw new InvalidOperationException("Database connection is null.");

                if (serverConnectionForSearchingPage.State != ConnectionState.Open)
                    serverConnectionForSearchingPage.Open();

                using (var cmd = new SqlCommand(sqlQuery, serverConnectionForSearchingPage))
                {
                    cmd.Parameters.AddWithValue("@ChannelId", userInputIDPart);

                    channelInfoList.Clear();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var channel = new ChannelInfo
                            {
                                // Use the exact column name from the SELECT
                                ChannelId = reader.GetInt32(reader.GetOrdinal("ChannelId")),
                                ChannelName = reader.IsDBNull(reader.GetOrdinal("ChannelName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ChannelName")),
                                ChannelCode = reader.IsDBNull(reader.GetOrdinal("ChannelCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ChannelCode")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? string.Empty : reader.GetString(reader.GetOrdinal("Description"))
                            };

                            channelInfoList.Add(channel);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Database query failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}");
            }
        }

        // Class-level field for database connection
        private SqlConnection serverConnectionForSearchingPage;

        // Lists to store query results
        private List<ChannelInfo> channelInfoList = new List<ChannelInfo>();
        private List<FlightInfo> flightsInfoList = new List<FlightInfo>();
        private List<TelemetryData> telemetryList = new List<TelemetryData>();

        /*
         Class: TelemetryData
         Description: Model for aircraft telemetry packet data
         Stores sensor readings and aircraft orientation values from transmitter packets
         */
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

        /*
         Class: ChannelInfo
         Description: Model for communication channel data
         Stores channel identification and configuration information
         */
        public class ChannelInfo
        {
            public int ChannelId { get; set; }
            public string ChannelName { get; set; }
            public string ChannelCode { get; set; }
            public string Description { get; set; }
        }

        /*
         Class: FlightInfo
         Description: Model for flight operation data
         Stores flight identification, aircraft reference, and timing information
         */
        public class FlightInfo
        {
            public int FlightId { get; set; }
            public int AircraftId { get; set; }
            public string FlightCode { get; set; }
            public DateTime DepartureTime { get; set; }
            public DateTime? ArrivalTime { get; set; }
        }

        /*
         Class: TelemetryDataNameAndValues
         Description: View model for telemetry display on UI labels
         Formats telemetry fields as "Name = Value" strings for UI binding
         */
        public class TelemetryDataNameAndValues
        {
            // Each property stores formatted telemetry field as displayable string
            public string TailNumber { get; set; }
            public string Attitude { get; set; }
            public string Pitch { get; set; }
            public string AccelX { get; set; }
            public string AccelY { get; set; }
            public string AccelZ { get; set; }
            public string Bank { get; set; }
            public string CheckSum { get; set; } 
            public string RecievedPacketNumber { get; set; }
        }
    }
}
