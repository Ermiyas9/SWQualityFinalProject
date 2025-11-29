using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
// for chart 
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static GroundTerminalApp.FDMSDashboard;
using WFChart = System.Windows.Forms.DataVisualization.Charting.Chart;
using WFChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using WFColor = System.Drawing.Color;
using WFSeries = System.Windows.Forms.DataVisualization.Charting.Series;
using WFSeriesChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType;






namespace GroundTerminalApp
{
    /// <summary>
    /// Interaction logic for GroundTerminalDashboard.xaml
    /// </summary>
    public partial class FDMSDashboard : Window
    {
        // Tcp server components
        private TcpListener tcpListener;
        private CancellationTokenSource listenerCancellation;
        private TheCounterComponent packetCounter;
        private const int DefaultListenPort = 5000; // default port if it's not set in config
        private int listenPort;
        

        public FDMSDashboard()
        {
            InitializeComponent();

            // call the chart display once the app starts to display our line chart 
            LineChartSetupAndDisplay();

            // Pass FDMSDashboard itself into SearchingPageApp so i can access any data from that page 
            var searchPage = new SearchingPageApp(this);


            // so I can call that I created in another window, since that method takes label 
            // i made a little change here instead of using the checkbox i got icon from icons8.com website that we can user their icons 
            // so i am passing the images as a parameter
            //var searchPage = new SearchingPageApp();
            bool connected = searchPage.ConnectToDatabase();

            // pass the controls as parameters using the real connection state so it gets offline when its offline 
            searchPage.UpdateConnectionStatus(dbConnectionStatusLbl, dbOnlineIcon, dbOfflineIcon, connected);

            // Initialize packet counter and start TCP server
            packetCounter = new TheCounterComponent();
            StartTcpServer();



            // Timer to check stream status every second posiible
            streamStatusTimer = new DispatcherTimer();
            streamStatusTimer.Interval = TimeSpan.FromSeconds(1);
            streamStatusTimer.Tick += (s, e) => UpdatingTheStatusOfStream();
            streamStatusTimer.Start();
        }

        // a time class to track the stream online status with time so if its not send for a while we can apply that change to the UI
        private DispatcherTimer streamStatusTimer;

        private void BtnSearchAndQuery_Click(object sender, RoutedEventArgs e)
        {
            SearchingPageApp searchPage = new SearchingPageApp(this);
            searchPage.Owner = this;
            searchPage.Show();
        }

        private void BtnSystemLogs_Click(object sender, RoutedEventArgs e)
        {
            logsPage logsPage = new logsPage(this);
            logsPage.Owner = this;
            logsPage.Show();
        }

        private void BtnLoginPage_Click(object sender, RoutedEventArgs e)
        {
            UsersLoginPage loginPage = new UsersLoginPage();
            loginPage.Owner = this;
            loginPage.Show();
        }


        // updating the dashboard UI (packet counter section) from the packet counter
        private void UpdateDashboardFromCounter()
        {
            TelemetryData telemetry = packetCounter.LastTelemetry;
            int receivedCount = packetCounter.Received;
            int sentCount = packetCounter.Sent;

            // I am adding more two variables to store the tail number , Checksum
            tailNumberLbl.Content = $"Tail: {telemetry.TailNumber}";
            checksumLbl.Content = $"Checksum: {telemetry.Checksum}";


            // i added the dropped or corrupt packate field so
            int droppedCount = packetCounter.Dropped;

            // Update labels (we don't have sent count in here yet)----- I have added the labels thank u
            PcktRecievedLbl.Content = $"Received: {receivedCount}";
            LblSent.Content = $"Sent: {sentCount}";
            LblDropped.Content = $"Dropped: {droppedCount}";

            if (telemetry != null)
            {
                this.Title = $"FDMS Dashboard - Received: {receivedCount} - Tail: {telemetry.TailNumber}";

                UpdateLineChart(telemetry);

                // streamOnlineIcon update is handled separately in UpdatingTheStatusOfStream()
                LblAltitudeValue.Content = $"{telemetry.Altitude:F0} ft";
                LblPitchValue.Content = $" {telemetry.Pitch:F1}°";
                LblBankValue.Content = $" {telemetry.Bank:F1}°";

                // so i update the ui here for the checksum and the tailnumber 
                tailNumberLbl.Content = $" {telemetry.TailNumber}";
                checksumLbl.Content = $"{telemetry.Checksum}";

                LblAccelXValue.Content = $"Accel X: {telemetry.AccelX:F2}";
                LblAccelYValue.Content = $"Accel Y: {telemetry.AccelY:F2}";
                LblAccelZValue.Content = $"Accel Z: {telemetry.AccelZ:F2}";
            }
            else
            {
                this.Title = $"FDMS Dashboard - Received: {receivedCount}";
            }

            // update the stream status icon and label
            UpdatingTheStatusOfStream();
        }

        private void UpdatingTheStatusOfStream()
        {
            bool streamOnline = false;

            // by using the LastUpdateOfStream i will know the status of the stream if is online or not 
            if (packetCounter.LastUpdateOfStream != DateTime.MinValue)
            {
                streamOnline = (DateTime.UtcNow - packetCounter.LastUpdateOfStream).TotalSeconds <= 5;
            }

            // the in this condition i can display or hide the online status 
            if (streamOnline)
            {
                streamOnlineIcon.Visibility = Visibility.Visible;
                streamOfflineIcon.Visibility = Visibility.Collapsed;
                packetStreamStatusLbl.Foreground = Brushes.Green;
                packetStreamStatusLbl.Content = "ONLINE";
            }
            else
            {
                streamOnlineIcon.Visibility = Visibility.Collapsed;
                streamOfflineIcon.Visibility = Visibility.Visible;
                packetStreamStatusLbl.Foreground = Brushes.Red;
                packetStreamStatusLbl.Content = "OFFLINE";

                // Clear telemetry labels with with we do this when the server goes offline
                LblAltitudeValue.Content = "NA";
                LblPitchValue.Content = "NA";
                LblBankValue.Content = "NA";
                tailNumberLbl.Content = "NA";
                checksumLbl.Content = "NA";
                LblAccelXValue.Content = "NA";
                LblAccelYValue.Content = "NA";
                LblAccelZValue.Content = "NA";
            }

        }


        // I am creating this Method to store the packets like pitch tail number and so on so this table will store those values into database table
        private void SaveGroundTerminalPacketsToDB(TelemetryData telemetry)
        {
            try
            {
                using (SqlConnection conn = ServerConnector.GetConnection())
                {
                    conn.Open();

                    string sqlForStoringPcktsToTable = @"
                                                        INSERT INTO dbo.AircraftTransmitterPackets
                                                        (SampleTimeStamp, TailNumber, [Checksum], Altitude, Pitch, Bank, AccelX, AccelY, AccelZ)
                                                        VALUES
                                                        (@SampleTimeStamp, @TailNumber, @Checksum, @Altitude, @Pitch, @Bank, @AccelX, @AccelY, @AccelZ)";

                    using (SqlCommand cmd = new SqlCommand(sqlForStoringPcktsToTable, conn))
                    {
                        cmd.Parameters.AddWithValue("@SampleTimeStamp", telemetry.Timestamp);
                        cmd.Parameters.AddWithValue("@TailNumber", telemetry.TailNumber);
                        cmd.Parameters.AddWithValue("@Checksum", telemetry.Checksum);
                        cmd.Parameters.AddWithValue("@Altitude", telemetry.Altitude);
                        cmd.Parameters.AddWithValue("@Pitch", telemetry.Pitch);
                        cmd.Parameters.AddWithValue("@Bank", telemetry.Bank);
                        cmd.Parameters.AddWithValue("@AccelX", telemetry.AccelX);
                        cmd.Parameters.AddWithValue("@AccelY", telemetry.AccelY);
                        cmd.Parameters.AddWithValue("@AccelZ", telemetry.AccelZ);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB insert failed (AircraftTransmitterPackets): " + ex.Message);
            }
        }


        // starting the tcp server
        private void StartTcpServer()
        {
            listenerCancellation = new CancellationTokenSource();

            string portText = ConfigurationManager.AppSettings["ServerPort"];
            int port;

            if (!int.TryParse(portText, out port))
            {
                port = DefaultListenPort;
            }

            listenPort = port;

            tcpListener = new TcpListener(IPAddress.Any, listenPort);
            tcpListener.Start();

            Task acceptTask = AcceptClients(listenerCancellation.Token);
        }

        // stopping the tcp server
        private void StopTcpServer()
        {
            try
            {
                if (listenerCancellation != null)
                {
                    listenerCancellation.Cancel();
                }

                if (tcpListener != null)
                {
                    tcpListener.Stop();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error stopping TCP server: " + ex.Message);
            }
        }

        // accepting clients
        private async Task AcceptClients(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    Task clientTask = Task.Run(() => HandleClient(client, token));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accepting client: " + ex.Message);
            }
        }


        // handling clients by reading packets and processing them
        private async Task HandleClient(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                byte[] lengthBuffer = new byte[4];

                try
                {
                    while (true)
                    {
                        int lengthRead = await ReadFromStream(stream, lengthBuffer, 0, 4, token);
                        if (lengthRead != 4)
                        {
                            break;
                        }

                        int packetLength = BitConverter.ToInt32(lengthBuffer, 0);
                        if (packetLength <= 0)
                        {
                            break;
                        }

                        byte[] packetBuffer = new byte[packetLength];
                        int packetRead = await ReadFromStream(stream, packetBuffer, 0, packetLength, token);
                        if (packetRead != packetLength)
                        {
                            break;
                        }

                        bool ok = packetCounter.ProcessPacket(packetBuffer);
                        if (ok)
                        {
                            // I am adding this block to insert into the table
                            // what ever we recieve we will update to the dashboard the same time we will store to the db table
                            TelemetryData telemetry = packetCounter.LastTelemetry;
                            if (telemetry != null)
                            {
                                SaveGroundTerminalPacketsToDB(telemetry);

                                telemetryDataList.Add(telemetry);
                            }
                            Dispatcher.Invoke(UpdateDashboardFromCounter);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        // reading exact number of bytes from the network stream
        private async Task<int> ReadFromStream(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, token);

                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        private void LineChartSetupAndDisplay()
        {
            // add the chart area once 
            if (lineChartAlltVsTime.ChartAreas.Count == 0)
            {
                var chartArea = new WFChartArea("MainArea");
                chartArea.BackColor = WFColor.FromArgb(34, 34, 34); // BG color

                // The title for AXISS
                chartArea.AxisX.Title = "Time";
                chartArea.AxisY.Title = "Altitude";

                // color for the chart area 
                chartArea.AxisX.TitleForeColor = WFColor.Blue;  
                chartArea.AxisY.TitleForeColor = WFColor.Green;
                chartArea.AxisX.LabelStyle.ForeColor = WFColor.Pink;
                chartArea.AxisY.LabelStyle.ForeColor = WFColor.Purple;
                chartArea.AxisX.MajorGrid.Enabled = false;
                chartArea.AxisY.MajorGrid.Enabled = false;

                // bfor just show the time not the date 
                chartArea.AxisX.LabelStyle.Format = "d MMM HH:mm:ss";

                // add the chart 
                lineChartAlltVsTime.ChartAreas.Add(chartArea);
            }

            //  so we need to add the att series only if it didnt come from terminal
            if (lineChartAlltVsTime.Series.FindByName("Altitude") == null)
            {
                var altitudeSeries = new WFSeries("Altitude");
                altitudeSeries.ChartType = WFSeriesChartType.Line;
                altitudeSeries.Color = WFColor.LightSkyBlue;

                // So we can make the titme stamp to be the x value and the attitude Y
                altitudeSeries.XValueType = ChartValueType.DateTime;
                altitudeSeries.BorderWidth = 2;
                lineChartAlltVsTime.Series.Add(altitudeSeries);
            }


            // styling for the chart bg color and foreclr
            lineChartAlltVsTime.BackColor = WFColor.FromArgb(34, 34, 34);
            lineChartAlltVsTime.ForeColor = WFColor.White;

            if (lineChartAlltVsTime.Series["Altitude"].Points.Count == 0)
            {
                lineChartAlltVsTime.Series["Altitude"].Points.AddXY(DateTime.Now, 1000);
            }
        }

 
        private void UpdateLineChart(TelemetryData telemetry)
        {
            // the chart works only when we recieve data 
            if (telemetry == null) return;

            // create a variable that hold the chart series  
            var seriesForAttitude = lineChartAlltVsTime.Series.FindByName("Altitude");

            // then if the series is null rturn if not then add the series from the attitude that comes Ground Terminal
            if (seriesForAttitude == null) return;

            seriesForAttitude.Points.AddXY(telemetry.Timestamp, telemetry.Altitude);

            // THIS IS OPTIONAL TO KEEP THE CHART TO BE SMALLER
            if (seriesForAttitude.Points.Count > 1000)
                seriesForAttitude.Points.RemoveAt(0);
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

        /*
        Class: TheCounterComponent
        Description: Receives TCP packets, validates checksums, parses telemetry data
        Inherits: DashBoardComponents
        Purpose: Counter display and packet deconstruction for ground terminal
        */
        public class TheCounterComponent : DashBoardComponents
        {
            private int received;
            private int sent;

            // this field i am going to use it to track the status of dropped packets
            private int dropped;
            private TelemetryData lastTelemetry;

            // I am adding this to keep track of the time stamp for online and offline status 
            public DateTime LastUpdateOfStream { get; private set; }

            private readonly object lockObject = new object();

            /*
            Property: Received
            Description: Gets/sets number of successfully received packets
            Increments only on valid checksum validation
            */
            public int Received
            {
                get
                {
                    lock (lockObject)
                    {
                        return received;
                    }
                }
                set
                {
                    lock (lockObject)
                    {
                        received = value;
                    }
                }
            }

            /*
            Property: Sent
            Description: Gets/sets number of packets sent by ground terminal
            */
            public int Sent
            {
                get { return sent; }
                set { sent = value; }
            }

            /*
                Property: Dropped
                Description: Gets/sets number of invalid or corrupted packets
            */
            public int Dropped
            {
                get
                {
                    lock (lockObject)
                    {
                        return dropped;
                    }
                }
                set
                {
                    lock (lockObject)
                    {
                        dropped = value;
                    }
                }
            }

            /*
            Property: LastTelemetry
            Description: Gets the most recently received and validated telemetry packet
            Returns: TelemetryData object or null if no packets received yet
            */
            public TelemetryData LastTelemetry
            {
                get
                {
                    lock (lockObject)
                    {
                        return lastTelemetry;
                    }
                }
            }




            /*
            Method: TheCounterComponent() [Constructor]
            Description: Initializes packet counters and telemetry storage
            */
            public TheCounterComponent()
            {
                received = 0;
                sent = 0;
                // i added this to check the status of the dropped packets 
                dropped = 0;
                lastTelemetry = null;
            }

            /*
            Method: ProcessPacket
            Description: Receives raw TCP packet bytes, validates and parses telemetry
            Parameters: byte[] packetData - Raw bytes from TCP network stream
            Returns: bool - True if packet valid and processed, false if validation failed
            */
            public bool ProcessPacket(byte[] packetData)
            {
                try
                {
                    // Convert byte array to ASCII string and remove whitespace
                    string packetString = Encoding.ASCII.GetString(packetData).Trim();

                    // Attempt to deconstruct packet and validate checksum
                    if (DeconstructPacket(packetString, out TelemetryData telemetry))
                    {
                        // Store telemetry and increment counter under lock
                        lock (lockObject)
                        {
                            lastTelemetry = telemetry;
                            received++;

                            // here I update the time stamp here 
                            LastUpdateOfStream = DateTime.UtcNow;



                        }
                        return true;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing packet: {ex.Message}");
                    return false;
                }
            }

            /*
            Method: DeconstructPacket
            Description: Parses packet structure, validates checksum, extracts telemetry fields
            Parameters: string packetLine - Complete packet as ASCII string
                        out TelemetryData telemetry - Parsed telemetry if valid
            Returns: bool - True if packet valid and parsed, false if validation/parsing failed
            */
            private bool DeconstructPacket(string packetLine, out TelemetryData telemetry)
            {
                telemetry = null;

                // Split packet by pipe delimiter
                string[] parts = packetLine.Split('|');

                // Packet must have exactly 10 fields (9 data + 1 checksum)
                if (parts.Length != 10)
                    return false;

                try
                {
                    // Extract all fields from packet
                    string tailNumber = parts[0];
                    string timestampText = parts[1];
                    string accelXText = parts[2];
                    string accelYText = parts[3];
                    string accelZText = parts[4];
                    string weightText = parts[5];
                    string altitudeText = parts[6];
                    string pitchText = parts[7];
                    string bankText = parts[8];
                    string checksumText = parts[9];

                    // Validate no empty fields to prevent parsing errors
                    if (string.IsNullOrWhiteSpace(accelXText) ||
                        string.IsNullOrWhiteSpace(accelYText) ||
                        string.IsNullOrWhiteSpace(accelZText) ||
                        string.IsNullOrWhiteSpace(weightText) ||
                        string.IsNullOrWhiteSpace(altitudeText) ||
                        string.IsNullOrWhiteSpace(pitchText) ||
                        string.IsNullOrWhiteSpace(bankText) ||
                        string.IsNullOrWhiteSpace(checksumText))
                    {
                        return false;
                    }

                    // Parse and validate checksum
                    if (!int.TryParse(checksumText, out int receivedChecksum))
                        return false;

                    // Reconstruct payload for checksum verification (without checksum field)
                    string payload = $"{tailNumber}|{timestampText}|{accelXText}|{accelYText}|{accelZText}|{weightText}|{altitudeText}|{pitchText}|{bankText}";
                    int calculatedChecksum = ComputeChecksum(payload);

                    // Verify checksum - packet is corrupted if mismatch
                    if (receivedChecksum != calculatedChecksum)
                    {
                        lock (lockObject)
                        {
                            // so increment dropped counter here
                            dropped++;
                        }
                        Console.WriteLine($"Checksum validation failed: received {receivedChecksum}, calculated {calculatedChecksum}");
                        return false;
                    }

                    // Checksum valid - parse all telemetry values
                    telemetry = new TelemetryData
                    {
                        TailNumber = tailNumber,
                        Timestamp = DateTime.Parse(timestampText),
                        AccelX = double.Parse(accelXText),
                        AccelY = double.Parse(accelYText),
                        AccelZ = double.Parse(accelZText),
                        Weight = double.Parse(weightText),
                        Altitude = double.Parse(altitudeText),
                        Pitch = double.Parse(pitchText),
                        Bank = double.Parse(bankText),
                        Checksum = receivedChecksum
                    };

                    return true;
                }
                catch (Exception ex)
                {
                    lock (lockObject)
                    {
                        // increment dropped counter on parse error
                        dropped++;
                    }
                    Console.WriteLine($"Error parsing packet: {ex.Message}");
                    return false;
                }
            }

            /*
            Method: ComputeChecksum
            Description: Computes 16-bit checksum matching aircraft transmitter algorithm
            Parameters: string payload - Packet data without checksum field
            Returns: int - 16-bit checksum value
            */
            private static int ComputeChecksum(string payload)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(payload);
                int sum = 0;

                // Sum all byte values
                foreach (byte b in bytes)
                {
                    sum += b;
                }

                // Mask to 16 bits
                return sum & 0xFFFF;
            }

            /*
            Method: RenderingDashbrdComponents()
            Description: Displays packet counter and latest telemetry to console output
            Called by dashboard UI to update display with current packet information
            Parameters: None
            Returns: void
            */
            public override void RenderingDashbrdComponents()
            {
                int currentReceived = 0;
                TelemetryData currentTelemetry = null;

                lock (lockObject)
                {
                    currentReceived = received;
                    currentTelemetry = lastTelemetry;
                }

                // Display packet counts
                Console.WriteLine($"Title - Received: {currentReceived}, Sent: {Sent}");

                // Display latest telemetry if available
                if (currentTelemetry != null)
                {
                    Console.WriteLine($"  Latest: {currentTelemetry.TailNumber} at {currentTelemetry.Timestamp}");
                    Console.WriteLine($"  Altitude: {currentTelemetry.Altitude}, Pitch: {currentTelemetry.Pitch}, Bank: {currentTelemetry.Bank}");
                }
            }
        }

        // this list is to store the live values so i can pass it to other windows 
        public List<TelemetryData> telemetryDataList { get; private set; } = new List<TelemetryData>(); 

        /*
        Class: TelemetryData
        Description: Data transfer object for parsed aircraft telemetry from single packet
        Properties correspond to FDMS packet format fields
        Used to communicate between network layer and UI display components
        */
        public class TelemetryData
        {
            public string TailNumber { get; set; }      // Aircraft identifier
            public DateTime Timestamp { get; set; }     // UTC recording time
            public double AccelX { get; set; }          // X-axis acceleration (G-force)
            public double AccelY { get; set; }          // Y-axis acceleration (G-force)
            public double AccelZ { get; set; }          // Z-axis acceleration (G-force)
            public double Weight { get; set; }          // Aircraft weight (pounds)
            public double Altitude { get; set; }        // Altitude above sea level (feet)
            public double Pitch { get; set; }           // Pitch angle (degrees)
            public double Bank { get; set; }            // Bank angle (degrees)
            public int Checksum { get; set; }           // Packet checksum (validated) i am adding this cus i want to display the checksum on dashboard

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
