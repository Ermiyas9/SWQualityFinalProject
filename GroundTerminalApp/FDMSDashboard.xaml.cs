/* ======================================================================================================================== */
/* FILE             : FDMSDashboard.xaml.cs                                                                                 */
/* PROJECT          : Software Quality Final Project Milestone 2                                                            */
/* NAMESPACE        : GroundTerminalApp                                                                                     */
/* PROGRAMMER       : Ermiyas (Endalkachew) Gulti, Mher Keshishian, Quang Minh Vu, Saje’- Antoine Rose                      */
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

		/// <summary>
		/// Initializes the FDMS dashboard UI, logging components, chart, counters, and TCP server.
		/// </summary>
		public FDMSDashboard()
        {
            InitializeComponent();

            // log that the dashboard UI has loaded
            WriteSystemLog("INFO", "FDMSDashboard", "Ground terminal dashboard started.");

			// call the chart display once the app starts to display our line chart 
			LineChartSetupAndDisplay();

            // Pass FDMSDashboard itself into SearchingPageApp so i can access any data from that page 
            var searchPage = new SearchingPageApp(this);


            // so I can call that I created in another window, since that method takes label 
            // i made a little change here instead of using the checkbox i got icon from icons8.com website that we can user their icons 
            // so i am passing the images as a parameter
            //var searchPage = new SearchingPageApp();
            bool connected = searchPage.ConnectToDatabase();

			// log the result of the first database connection test
			if (connected)
			{
				WriteSystemLog("INFO", "FDMSDashboard", "Initial database connection succeeded.");
			}
			else
			{
				WriteSystemLog("ERROR", "FDMSDashboard", "Initial database connection failed.");
			}

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

        /// <summary>
        /// Handles the click event for the "Search and Query" button.
        /// </summary>
        /// <param name="sender">The source of the event, typically the button that was clicked.</param>
        /// <param name="e">The event data associated with the click event.</param>
        private void BtnSearchAndQuery_Click(object sender, RoutedEventArgs e)
        {
            SearchingPageApp searchPage = new SearchingPageApp(this);
            searchPage.Owner = this;
            searchPage.Show();
        }

        /// <summary>
        /// Handles the click event for the "System Logs" button.
        /// </summary>
        /// <param name="sender">The source of the event, typically the button that was clicked.</param>
        /// <param name="e">The event data associated with the click event.</param>
        private void BtnSystemLogs_Click(object sender, RoutedEventArgs e)
        {
            logsPage logsPage = new logsPage(this);
            logsPage.Owner = this;
            logsPage.Show();
        }

        /// <summary>
        /// Handles the click event for the "Login Page" button.
        /// </summary>
        /// <param name="sender">The source of the event, typically the button that was clicked.</param>
        /// <param name="e">The event data associated with the click event.</param>
        private void BtnLoginPage_Click(object sender, RoutedEventArgs e)
        {
            UsersLoginPage loginPage = new UsersLoginPage();
            loginPage.Owner = this;
            loginPage.Show();
        }

		/// <summary>
		/// Updates dashboard labels and charts using the latest telemetry and packet counters.
		/// </summary>
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

		/// <summary>
		/// Checks the last telemetry update time and refreshes the stream online/offline status indicators.
		/// </summary>
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

		/// <summary>
		/// Saves the latest telemetry packet into the AircraftTransmitterPackets table.
		/// </summary>
		/// <param name="telemetry">The telemetry data object to be stored in the database.</param>
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
				WriteSystemLog("ERROR", "FDMSDashboard", "DB insert failed (AircraftTransmitterPackets): " + ex.Message);
			}
		}

		/// <summary>
		/// Inserts a single row into the SystemLogs table for application and TCP server events.
		/// </summary>
		/// <param name="level">The severity level of the log entry (for example, INFO or ERROR).</param>
		/// <param name="source">The logical source of the log entry, such as a class or method name.</param>
		/// <param name="message">The descriptive log message to record.</param>
		private void WriteSystemLog(string level, string source, string message)
		{
			try
			{
				using (SqlConnection conn = ServerConnector.GetConnection())
				{
					conn.Open();

					// insert one row into the SystemLogs table
					string sql = @"
                        INSERT INTO dbo.SystemLogs ([Timestamp], [Level], [Source], [Message])
                        VALUES (@Timestamp, @Level, @Source, @Message);";

					using (SqlCommand cmd = new SqlCommand(sql, conn))
					{
						// I am using current time for the Timestamp column
						cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
						cmd.Parameters.AddWithValue("@Level", level);
						cmd.Parameters.AddWithValue("@Source", source);
						cmd.Parameters.AddWithValue("@Message", message);

						cmd.ExecuteNonQuery(); // run the INSERT
					}
				}
			}
			catch (Exception ex)
			{
				// If logging to database fails, I just write to local file as backup
				try
				{
					File.AppendAllText(
						"local_error_log.txt",
						DateTime.Now.ToString("s") + " [LogError] " + ex.Message + Environment.NewLine
					);
				}
				catch
				{
					// ignore if even this fails
				}
			}
		}

		/// <summary>
		/// Reads the listen port from configuration, starts the TCP listener, and begins accepting clients.
		/// </summary>
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

			// log that the TCP server started on this port
			WriteSystemLog("INFO", "FDMSDashboard", "TCP server started on port " + listenPort + ".");

			Task acceptTask = AcceptClients(listenerCancellation.Token);
        }

		/// <summary>
		/// Stops the TCP listener and cancels the accept loop, logging any shutdown errors.
		/// </summary>
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

				// log that the TCP server stopped
				WriteSystemLog("INFO", "FDMSDashboard", "TCP server stopped.");
			}
            catch (Exception ex)
            {
                Console.WriteLine("Error stopping TCP server: " + ex.Message);
				WriteSystemLog("ERROR", "FDMSDashboard", "Error stopping TCP server: " + ex.Message);
			}
		}

		/// <summary>
		/// Asynchronously accepts incoming TCP clients and dispatches each to a handler task.
		/// </summary>
		/// <param name="token">A cancellation token used to stop accepting new clients.</param>
		/// <returns>A task that represents the asynchronous accept loop operation.</returns>
		private async Task AcceptClients(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();

					// log that a new client connected
					WriteSystemLog("INFO", "FDMSDashboard", "Client connected: " + client.Client.RemoteEndPoint);

					Task clientTask = Task.Run(() => HandleClient(client, token));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accepting client: " + ex.Message);
				WriteSystemLog("ERROR", "FDMSDashboard", "Error accepting client: " + ex.Message);
			}
		}


		/// <summary>
		/// Reads framed packets from the TCP client stream, updates counters, and processes telemetry.
		/// </summary>
		/// <param name="client">The connected TCP client providing telemetry data.</param>
		/// <param name="token">A cancellation token used to stop processing the client.</param>
		/// <returns>A task that represents the asynchronous client handling operation.</returns>
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

		/// <summary>
		/// Reads the requested number of bytes from a network stream into the buffer.
		/// </summary>
		/// <param name="stream">The network stream to read from.</param>
		/// <param name="buffer">The byte buffer used to store the incoming data.</param>
		/// <param name="offset">The starting index in the buffer where data should be written.</param>
		/// <param name="count">The total number of bytes expected to be read.</param>
		/// <param name="token">A cancellation token used to cancel the read operation.</param>
		/// <returns>The total number of bytes actually read from the stream.</returns>
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

		/// <summary>
		/// Configures the altitude-vs-time line chart area, axes, colors, and series definition.
		/// </summary>
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

		/// <summary>
		/// Appends a new altitude data point to the line chart and trims old points if needed.
		/// </summary>
		/// <param name="telemetry">The telemetry sample providing timestamp and altitude.</param>
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

		/// <summary>
		/// Provides a base virtual render method for dashboard components.
		/// </summary>
		public class DashBoardComponents
        {


            // then we will have a render method and this method will be overriden by the child classes if its neccessarly 
            public virtual void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart or any thing ...");
            }
        }

		/// <summary>
		/// Renders chart-related dashboard content, such as titles and data points.
		/// </summary>
		public class ChartDisplay : DashBoardComponents
        {
            public override void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart: title with data  points.");
            }
        }

		/// <summary>
		/// Renders the current connection status label as connected or disconnected.
		/// </summary>
		public class ConnectionStatus : DashBoardComponents
        {
            public bool IsConnected { get; set; }

            public override void RenderingDashbrdComponents()
            {
                string status = IsConnected ? "Connected" : "Disconnected";
                Console.WriteLine($"Title: {status}");
            }
        }

		/// <summary>
		/// Initializes packet counters, dropped packet tracking, and telemetry storage.
		/// </summary>
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

            /// <summary>
            /// Gets the most recent telemetry data received.
            /// </summary>
            /// <remarks>Access to this property is thread-safe. The returned value reflects the state
            /// of the telemetry data at the time of access.</remarks>
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

            /// <summary>
            /// Initializes a new instance of the <see cref="TheCounterComponent"/> class.
            /// </summary>
            /// <remarks>This constructor initializes the component with default values.  The counters
            /// for received, sent, and dropped packets are set to zero,  and the last telemetry data is initialized to
            /// null.</remarks>
            public TheCounterComponent()
            {
                received = 0;
                sent = 0;
                // i added this to check the status of the dropped packets 
                dropped = 0;
                lastTelemetry = null;
            }

            /// <summary>
            /// Processes a telemetry packet and updates the internal state with the extracted data.
            /// </summary>
            /// <remarks>This method attempts to parse the provided packet data, validate its
            /// checksum, and extract telemetry information. If successful, the extracted telemetry data is stored, and
            /// the internal counters and timestamp are updated. <para> Thread safety is ensured by locking during
            /// updates to shared state. </para></remarks>
            /// <param name="packetData">The raw packet data as a byte array. Must not be null.</param>
            /// <returns><see langword="true"/> if the packet was successfully processed and the telemetry data was updated;
            /// otherwise, <see langword="false"/> if the packet is invalid or processing fails.</returns>
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

			/// <summary>
			/// Parses a delimited packet string into a TelemetryData object and validates fields.
			/// </summary>
			/// <param name="packetLine">The raw packet string containing delimited telemetry values.</param>
			/// <param name="telemetry">When this method returns, holds the parsed telemetry data if successful.</param>
			/// <returns>True if the packet string is valid and parsed; otherwise, false.</returns>
			private bool DeconstructPacket(string packetLine, out TelemetryData telemetry)
			{
				telemetry = null;

				// Split packet by pipe delimiter
				string[] parts = packetLine.Split('|');

				// Packet must have exactly 11 fields (tail + seq + 8 data + 1 checksum)
				if (parts.Length != 11)
					return false;

				try
				{
					// Extract all fields from packet
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

					// Parse and validate checksum
					if (!int.TryParse(checksumText, out int receivedChecksum))
						return false;

					var culture = System.Globalization.CultureInfo.InvariantCulture;

					double accelX = double.Parse(accelXText, culture);
					double accelY = double.Parse(accelYText, culture);
					double accelZ = double.Parse(accelZText, culture);
					double weight = double.Parse(weightText, culture);
					double altitude = double.Parse(altitudeText, culture);
					double pitch = double.Parse(pitchText, culture);
					double bank = double.Parse(bankText, culture);

					int calculatedChecksum = ComputeChecksum(altitude, pitch, bank);

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
					lock (lockObject)
					{
						// increment dropped counter on parse error
						dropped++;
					}
					Console.WriteLine($"Error parsing packet: {ex.Message}");
					return false;
				}
			}

			/// <summary>
			/// Computes the integer checksum using the altitude, pitch, and bank values.
			/// </summary>
			/// <param name="altitude">The altitude component used in the checksum calculation.</param>
			/// <param name="pitch">The pitch component used in the checksum calculation.</param>
			/// <param name="bank">The bank component used in the checksum calculation.</param>
			/// <returns>The checksum value as an integer.</returns>
			private static int ComputeChecksum(double altitude, double pitch, double bank)
			{
				double checksumDouble = (altitude + pitch + bank) / 3.0;
				return (int)checksumDouble;
			}

			/// <summary>
			/// Writes packet counts and the latest telemetry summary to the console for debugging.
			/// </summary>
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

        /// <summary>
        /// Gets the collection of telemetry data entries.
        /// </summary>
        public List<TelemetryData> telemetryDataList { get; private set; } = new List<TelemetryData>(); 

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

			/// <summary>
			/// Creates and returns a new SQL connection using the configured connection string.
			/// </summary>
			/// <returns>A new SqlConnection instance for the FDMS database.</returns>
			public static SqlConnection GetConnection()
            {
                return new SqlConnection(ConnectionString);
            }
        }

        

    }
}
