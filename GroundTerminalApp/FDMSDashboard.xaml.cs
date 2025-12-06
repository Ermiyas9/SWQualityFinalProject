/* ======================================================================================================================== */
/* FILE             : FDMSDashboard.xaml.cs                                                                                 */
/* PROJECT          : Software Quality Final Project Milestone 2                                                            */
/* NAMESPACE        : GroundTerminalApp                                                                                     */
/* PROGRAMMER       : Ermiyas (Endalkachew) Gulti, Mher Keshishian, Quang Minh Vu, Saje'- Antoine Rose                      */
/* FIRST VERSION    : 2025-11-22                                                                                            */
/* DESCRIPTION      : Defines the FDMSDashboard WPF window, which acts as the main ground terminal UI. It hosts TCP        */
/*                    server logic, packet parsing and checksum validation, live telemetry visualization, and system logs.  */
/* ======================================================================================================================== */

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
        // TCP server and listener components
        private TcpListener tcpListener;
        private CancellationTokenSource listenerCancellation;
        private TheCounterComponent packetCounter;
        private const int DefaultListenPort = 5000;
        private int listenPort;

        /*
         Method: FDMSDashboard (Constructor)
         Description: Initializes dashboard UI, TCP server, chart display, and connection status
         Sets up timers for stream monitoring and loads initial database connection state
         Parameters: None
         Returns: void
         */
        public FDMSDashboard()
        {
            InitializeComponent();

            // Log dashboard startup
            WriteSystemLog("INFO", "FDMSDashboard", "Ground terminal dashboard started.");

            // Initialize altitude vs time chart display
            LineChartSetupAndDisplay();

            // Create search page with dashboard reference for data sharing
            var searchPage = new SearchingPageApp(this);

            // Test initial database connection
            bool connected = searchPage.ConnectToDatabase();

            // Log connection result
            if (connected)
            {
                WriteSystemLog("INFO", "FDMSDashboard", "Initial database connection succeeded.");
            }
            else
            {
                WriteSystemLog("ERROR", "FDMSDashboard", "Initial database connection failed.");
            }

            // Update UI connection indicators with actual connection state
            searchPage.UpdateConnectionStatus(dbConnectionStatusLbl, dbOnlineIcon, dbOfflineIcon, connected);

            // Initialize packet counter and start TCP listener
            packetCounter = new TheCounterComponent();
            StartTcpServer();

            // Timer for monitoring packet stream status
            streamStatusTimer = new DispatcherTimer();
            streamStatusTimer.Interval = TimeSpan.FromSeconds(1);
            streamStatusTimer.Tick += (s, e) => UpdatingTheStatusOfStream();
            streamStatusTimer.Start();
        }

        // Timer for periodic stream online/offline status checks
        private DispatcherTimer streamStatusTimer;

        /*
         Method: BtnSearchAndQuery_Click
         Description: Event handler for Search and Query button click
         Opens SearchingPageApp window with dashboard reference for data access
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private void BtnSearchAndQuery_Click(object sender, RoutedEventArgs e)
        {
            SearchingPageApp searchPage = new SearchingPageApp(this);
            searchPage.Owner = this;
            searchPage.Show();
        }

        /*
         Method: BtnSystemLogs_Click
         Description: Event handler for System Logs button click
         Opens logsPage window with dashboard reference for log filtering and display
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private void BtnSystemLogs_Click(object sender, RoutedEventArgs e)
        {
            LogsPage logsPage = new LogsPage(this);
            logsPage.Owner = this;
            logsPage.Show();
        }

        /*
         Method: BtnLoginPage_Click
         Description: Event handler for Login Page button click
         Opens UsersLoginPage window for user authentication and session management
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private void BtnLoginPage_Click(object sender, RoutedEventArgs e)
        {
            UsersLoginPage loginPage = new UsersLoginPage();
            loginPage.Owner = this;
            loginPage.Show();
        }

        /*
         Method: UpdateDashboardFromCounter
         Description: Refreshes all dashboard UI labels and charts with latest telemetry
         Updates packet counts, tail number, checksum, altitude, orientation, and acceleration values
         Parameters: None
         Returns: void
         */
        private void UpdateDashboardFromCounter()
        {
            TelemetryData telemetry = packetCounter.LastTelemetry;
            int receivedCount = packetCounter.Received;
            int sentCount = packetCounter.Sent;
            int droppedCount = packetCounter.Dropped;

            // Update packet counter labels
            PcktRecievedLbl.Content = $"Received: {receivedCount}";
            LblSent.Content = $"Sent: {sentCount}";
            LblDropped.Content = $"Dropped: {droppedCount}";

            if (telemetry != null)
            {
                // Update window title with latest packet count and aircraft
                this.Title = $"FDMS Dashboard - Received: {receivedCount} - Tail: {telemetry.TailNumber}";

                // Add latest telemetry point to altitude chart
                UpdateLineChart(telemetry);

                // Update all telemetry display labels with current values
                LblAltitudeValue.Content = $"{telemetry.Altitude:F0} ft";
                LblPitchValue.Content = $" {telemetry.Pitch:F1}°";
                LblBankValue.Content = $" {telemetry.Bank:F1}°";
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

            // Update stream status indicators separately
            UpdatingTheStatusOfStream();
        }

        /*
         Method: UpdatingTheStatusOfStream
         Description: Checks telemetry timestamp and updates stream online/offline UI indicators
         Clears telemetry labels when stream offline (no updates for 5+ seconds)
         Parameters: None
         Returns: void
         */
        private void UpdatingTheStatusOfStream()
        {
            bool streamOnline = false;

            // Determine stream status based on last update time
            if (packetCounter.LastUpdateOfStream != DateTime.MinValue)
            {
                streamOnline = (DateTime.UtcNow - packetCounter.LastUpdateOfStream).TotalSeconds <= 5;
            }

            if (streamOnline)
            {
                // Display green online indicator and status
                streamOnlineIcon.Visibility = Visibility.Visible;
                streamOfflineIcon.Visibility = Visibility.Collapsed;
                packetStreamStatusLbl.Foreground = Brushes.Green;
                packetStreamStatusLbl.Content = "ONLINE";
            }
            else
            {
                // Display red offline indicator and status
                streamOnlineIcon.Visibility = Visibility.Collapsed;
                streamOfflineIcon.Visibility = Visibility.Visible;
                packetStreamStatusLbl.Foreground = Brushes.Red;
                packetStreamStatusLbl.Content = "OFFLINE";

                // Clear telemetry labels when no data received
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

        /*
         Method: SaveGroundTerminalPacketsToDB
         Description: Inserts telemetry packet into AircraftTransmitterPackets table
         Persists all sensor data with timestamp for historical analysis
         Parameters: TelemetryData telemetry
         Returns: void
         */
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
                        // Parameterize all values to prevent SQL injection
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
                WriteSystemLog("ERROR", "FDMSDashboard", "DB insert failed (AircraftTransmitterPackets): " + ex.Message);
            }
        }

        /*
         Method: WriteSystemLog
         Description: Inserts log entry into SystemLogs table for audit trail
         Falls back to local text file if database logging fails
         Parameters: string level, string source, string message
         Returns: void
         */
        private void WriteSystemLog(string level, string source, string message)
        {
            try
            {
                using (SqlConnection conn = ServerConnector.GetConnection())
                {
                    conn.Open();

                    // Insert log entry with current timestamp
                    string sql = @"
                        INSERT INTO dbo.SystemLogs ([Timestamp], [Level], [Source], [Message])
                        VALUES (@Timestamp, @Level, @Source, @Message);";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Level", level);
                        cmd.Parameters.AddWithValue("@Source", source);
                        cmd.Parameters.AddWithValue("@Message", message);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to local file if database logging fails
                try
                {
                    File.AppendAllText(
                        "local_error_log.txt",
                        DateTime.Now.ToString("s") + " [LogError] " + ex.Message + Environment.NewLine
                    );
                }
                catch
                {
                    // Silent failure - prevent cascading errors
                }
            }
        }

        /*
         Method: StartTcpServer
         Description: Reads listen port from config, creates TCP listener, and begins accepting clients
         Starts background task for asynchronous client acceptance
         Parameters: None
         Returns: void
         */
        private void StartTcpServer()
        {
            listenerCancellation = new CancellationTokenSource();

            // Read port from configuration file
            string portText = ConfigurationManager.AppSettings["ServerPort"];
            int port;

            if (!int.TryParse(portText, out port))
            {
                port = DefaultListenPort;
            }

            listenPort = port;

            // Create and start TCP listener on all interfaces
            tcpListener = new TcpListener(IPAddress.Any, listenPort);
            tcpListener.Start();

            WriteSystemLog("INFO", "FDMSDashboard", "TCP server started on port " + listenPort + ".");

            // Start background task for accepting client connections
            Task acceptTask = AcceptClients(listenerCancellation.Token);
        }

        /*
         Method: StopTcpServer
         Description: Stops TCP listener and cancels client acceptance loop
         Cleans up resources and logs shutdown completion
         Parameters: None
         Returns: void
         */
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

                WriteSystemLog("INFO", "FDMSDashboard", "TCP server stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error stopping TCP server: " + ex.Message);
                WriteSystemLog("ERROR", "FDMSDashboard", "Error stopping TCP server: " + ex.Message);
            }
        }

        /*
         Method: AcceptClients
         Description: Asynchronously accepts incoming TCP connections in loop
         Dispatches each client to handler task on background thread
         Parameters: CancellationToken token
         Returns: Task - async acceptance loop operation
         */
        private async Task AcceptClients(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    // Wait for and accept incoming client connection
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();

                    WriteSystemLog("INFO", "FDMSDashboard", "Client connected: " + client.Client.RemoteEndPoint);

                    // Process client on background thread
                    Task clientTask = Task.Run(() => HandleClient(client, token));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accepting client: " + ex.Message);
                WriteSystemLog("ERROR", "FDMSDashboard", "Error accepting client: " + ex.Message);
            }
        }

        /*
         Method: HandleClient
         Description: Reads framed packets from client stream and processes telemetry data
         Validates packet checksums and updates dashboard in real-time
         Parameters: TcpClient client, CancellationToken token
         Returns: Task - async client handling operation
         */
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
                        // Read 4-byte frame length header
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

                        // Read packet data based on frame length
                        byte[] packetBuffer = new byte[packetLength];
                        int packetRead = await ReadFromStream(stream, packetBuffer, 0, packetLength, token);
                        if (packetRead != packetLength)
                        {
                            break;
                        }

                        // Process packet and update counters
                        bool ok = packetCounter.ProcessPacket(packetBuffer);
                        if (ok)
                        {
                            // Extract telemetry and persist to database
                            TelemetryData telemetry = packetCounter.LastTelemetry;
                            if (telemetry != null)
                            {
                                SaveGroundTerminalPacketsToDB(telemetry);
                                telemetryDataList.Add(telemetry);
                            }

                            // Update UI on dispatcher thread
                            Dispatcher.Invoke(UpdateDashboardFromCounter);
                        }
                    }
                }
                catch (Exception)
                {
                    // Client disconnected or read error - exit handler
                }
            }
        }

        /*
         Method: ReadFromStream
         Description: Reads exact number of bytes from network stream into buffer
         Handles partial reads by looping until count reached or stream ends
         Parameters: NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken token
         Returns: int - total bytes read (may be less than count if stream ends)
         */
        private async Task<int> ReadFromStream(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                // Read available data from stream
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, token);

                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        /*
         Method: LineChartSetupAndDisplay
         Description: Configures altitude vs time line chart with axes, colors, and formatting
         Initializes chart area and series for real-time data visualization
         Parameters: None
         Returns: void
         */
        private void LineChartSetupAndDisplay()
        {
            // Create chart area only if not already present
            if (lineChartAlltVsTime.ChartAreas.Count == 0)
            {
                var chartArea = new WFChartArea("MainArea");
                chartArea.BackColor = WFColor.FromArgb(34, 34, 34);

                // Configure axis titles and labels
                chartArea.AxisX.Title = "Time";
                chartArea.AxisY.Title = "Altitude";
                chartArea.AxisX.TitleForeColor = WFColor.Blue;
                chartArea.AxisY.TitleForeColor = WFColor.Green;
                chartArea.AxisX.LabelStyle.ForeColor = WFColor.Pink;
                chartArea.AxisY.LabelStyle.ForeColor = WFColor.Purple;

                // Disable grid lines for cleaner appearance
                chartArea.AxisX.MajorGrid.Enabled = false;
                chartArea.AxisY.MajorGrid.Enabled = false;

                // Format X-axis labels to show time only (not full date)
                chartArea.AxisX.LabelStyle.Format = "d MMM HH:mm:ss";

                lineChartAlltVsTime.ChartAreas.Add(chartArea);
            }

            // Add altitude series if not already present
            if (lineChartAlltVsTime.Series.FindByName("Altitude") == null)
            {
                var altitudeSeries = new WFSeries("Altitude");
                altitudeSeries.ChartType = WFSeriesChartType.Line;
                altitudeSeries.Color = WFColor.LightSkyBlue;

                // Configure X-axis as timestamp for temporal data
                altitudeSeries.XValueType = ChartValueType.DateTime;
                altitudeSeries.BorderWidth = 2;
                lineChartAlltVsTime.Series.Add(altitudeSeries);
            }

            // Style chart background and foreground
            lineChartAlltVsTime.BackColor = WFColor.FromArgb(34, 34, 34);
            lineChartAlltVsTime.ForeColor = WFColor.White;

            // Initialize chart with placeholder point if empty
            if (lineChartAlltVsTime.Series["Altitude"].Points.Count == 0)
            {
                lineChartAlltVsTime.Series["Altitude"].Points.AddXY(DateTime.Now, 1000);
            }
        }

        /*
         Method: UpdateLineChart
         Description: Appends new altitude data point to chart and trims old points if needed
         Maintains maximum 1000 points to prevent memory bloat
         Parameters: TelemetryData telemetry
         Returns: void
         */
        private void UpdateLineChart(TelemetryData telemetry)
        {
            if (telemetry == null) return;

            // Get altitude series from chart
            var seriesForAttitude = lineChartAlltVsTime.Series.FindByName("Altitude");
            if (seriesForAttitude == null) return;

            // Add new data point with timestamp and altitude
            seriesForAttitude.Points.AddXY(telemetry.Timestamp, telemetry.Altitude);

            // Remove oldest point if series exceeds maximum size
            if (seriesForAttitude.Points.Count > 1000)
                seriesForAttitude.Points.RemoveAt(0);
        }

        /*
         Class: DashBoardComponents
         Description: Base class for dashboard component rendering
         Provides virtual method for subclasses to override render behavior
         */
        public class DashBoardComponents
        {
            // Virtual render method for subclass implementation
            public virtual void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart or any thing ...");
            }
        }

        /*
         Class: ChartDisplay
         Description: Dashboard component for rendering altitude chart
         Overrides base render method to display chart-specific content
         */
        public class ChartDisplay : DashBoardComponents
        {
            public override void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart: title with data points.");
            }
        }

        /*
         Class: ConnectionStatus
         Description: Dashboard component for rendering database connection status
         Displays connected or disconnected status based on connection state
         */
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
         Description: Manages packet counting, telemetry storage, and checksum validation
         Thread-safe component with properties for received, sent, and dropped packet tracking
         */
        public class TheCounterComponent : DashBoardComponents
        {
            private int received;
            private int sent;
            private int dropped;
            private TelemetryData lastTelemetry;

            // Timestamp of last received telemetry for stream status monitoring
            public DateTime LastUpdateOfStream { get; private set; }

            private readonly object lockObject = new object();

            /*
             Property: Received
             Description: Gets/sets count of successfully received packets
             Thread-safe access with lock protection
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
             Description: Gets/sets count of packets sent by ground terminal
             */
            public int Sent
            {
                get { return sent; }
                set { sent = value; }
            }

            /*
             Property: Dropped
             Description: Gets/sets count of invalid or corrupted packets
             Thread-safe access with lock protection
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
             Description: Gets most recent telemetry data received
             Thread-safe read-only access
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
             Method: TheCounterComponent (Constructor)
             Description: Initializes packet counter with zero values and null telemetry
             */
            public TheCounterComponent()
            {
                received = 0;
                sent = 0;
                dropped = 0;
                lastTelemetry = null;
            }

            /*
             Method: ProcessPacket
             Description: Parses packet data, validates checksum, and updates counters
             Thread-safe updates to shared state with lock protection
             Parameters: byte[] packetData
             Returns: bool - true if valid packet processed, false if invalid or parse error
             */
            public bool ProcessPacket(byte[] packetData)
            {
                try
                {
                    // Convert packet bytes to ASCII string
                    string packetString = Encoding.ASCII.GetString(packetData).Trim();

                    // Attempt to parse and validate packet
                    if (DeconstructPacket(packetString, out TelemetryData telemetry))
                    {
                        // Update state under lock
                        lock (lockObject)
                        {
                            lastTelemetry = telemetry;
                            received++;
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
             Description: Parses delimited packet string into TelemetryData with checksum validation
             Validates all fields present and non-empty before parsing
             Parameters: string packetLine, out TelemetryData telemetry
             Returns: bool - true if valid packet, false if parsing or validation fails
             */
            private bool DeconstructPacket(string packetLine, out TelemetryData telemetry)
            {
                telemetry = null;

                // Split packet by pipe delimiter
                string[] parts = packetLine.Split('|');

                // Packet must have exactly 11 fields
                if (parts.Length != 11)
                    return false;

                try
                {
                    // Extract all fields from packet parts
                    string tailNumber = parts[0];
                    string sequenceText = parts[1];
                    string timestampText = parts[2];
                    string accelXText = parts[3];
                    string accelYText = parts[4];
                    string accelZText = parts[5];
                    string weightText = parts[6];
                    string altitudeText = parts[7];
                    string pitchText = parts[8];
                    string bankText = parts[9];
                    string checksumText = parts[10];

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

                    if (!uint.TryParse(sequenceText, out uint sequenceNumber))
                        return false;

                    // Parse and validate checksum field
                    if (!int.TryParse(checksumText, out int receivedChecksum))
                        return false;

                    // Use invariant culture for reliable numeric parsing
                    var culture = System.Globalization.CultureInfo.InvariantCulture;

                    double accelX = double.Parse(accelXText, culture);
                    double accelY = double.Parse(accelYText, culture);
                    double accelZ = double.Parse(accelZText, culture);
                    double weight = double.Parse(weightText, culture);
                    double altitude = double.Parse(altitudeText, culture);
                    double pitch = double.Parse(pitchText, culture);
                    double bank = double.Parse(bankText, culture);

                    // Calculate expected checksum and verify packet integrity
                    int calculatedChecksum = ComputeChecksum(altitude, pitch, bank);

                    if (receivedChecksum != calculatedChecksum)
                    {
                        // Packet failed checksum validation - increment dropped counter
                        lock (lockObject)
                        {
                            dropped++;
                        }
                        Console.WriteLine($"Checksum validation failed: received {receivedChecksum}, calculated {calculatedChecksum}");
                        return false;
                    }

                    // Checksum valid - create telemetry object with all parsed values
                    telemetry = new TelemetryData
                    {
                        TailNumber = tailNumber,
                        SequenceNumber = sequenceNumber,
                        Timestamp = DateTime.Parse(timestampText, culture, System.Globalization.DateTimeStyles.RoundtripKind),
                        AccelX = accelX,
                        AccelY = accelY,
                        AccelZ = accelZ,
                        Weight = weight,
                        Altitude = altitude,
                        Pitch = pitch,
                        Bank = bank,
                        Checksum = receivedChecksum
                    };

                    return true;
                }
                catch (Exception ex)
                {
                    // Parse error - increment dropped counter
                    lock (lockObject)
                    {
                        dropped++;
                    }
                    Console.WriteLine($"Error parsing packet: {ex.Message}");
                    return false;
                }
            }

            /*
             Method: ComputeChecksum
             Description: Calculates integer checksum from altitude, pitch, and bank values
             Uses average of three values divided by 3 for validation
             Parameters: double altitude, double pitch, double bank
             Returns: int - computed checksum value
             */
            private static int ComputeChecksum(double altitude, double pitch, double bank)
            {
                double checksumDouble = (altitude + pitch + bank) / 3.0;
                return (int)checksumDouble;
            }

            /*
             Method: RenderingDashbrdComponents
             Description: Writes packet counts and telemetry to console for debugging
             Displays current received/sent counts and latest telemetry summary
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

        // Collection for storing all received telemetry packets
        public List<TelemetryData> telemetryDataList { get; private set; } = new List<TelemetryData>();

        /*
         Class: TelemetryData
         Description: Model for aircraft telemetry packet with all sensor and orientation data
         */
        public class TelemetryData
        {
            public string TailNumber { get; set; }      // Aircraft identifier
            public uint SequenceNumber { get; set; }    // Packet sequence number
            public DateTime Timestamp { get; set; }     // UTC recording time
            public double AccelX { get; set; }          // X-axis acceleration (G-force)
            public double AccelY { get; set; }          // Y-axis acceleration (G-force)
            public double AccelZ { get; set; }          // Z-axis acceleration (G-force)
            public double Weight { get; set; }          // Aircraft weight (pounds)
            public double Altitude { get; set; }        // Altitude above sea level (feet)
            public double Pitch { get; set; }           // Pitch angle (degrees)
            public double Bank { get; set; }            // Bank angle (degrees)
            public int Checksum { get; set; }           // Packet checksum (validated)
        }

        /*
         Class: SideBarComponents
         Description: Base class for sidebar navigation components
         Used for routing between windows and pages
         */
        public class SideBarComponents : DashBoardComponents
        {
            // Navigation rendering implementation
        }

        /*
         Class: ServerConnector
         Description: Static utility for managing database connections
         Retrieves connection string from config file and creates SqlConnection instances
         */
        public static class ServerConnector
        {
            // Connection string read from configuration file
            private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["SoftwareQualityFinalProject"]?.ConnectionString;

            /*
             Method: GetConnection
             Description: Creates and returns new SQL connection instance
             Uses connection string from configuration for database access
             Parameters: None
             Returns: SqlConnection - new connection instance
             */
            public static SqlConnection GetConnection()
            {
                return new SqlConnection(ConnectionString);
            }
        }
    }
}
