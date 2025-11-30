/* ======================================================================================================================== */
/* FILE             : FDMSDashboard.xaml.cs                                                                                 */
/* PROJECT          : Software Quality Final Project Milestone 2                                                            */
/* NAMESPACE        : GroundTerminalApp                                                                                     */
/* PROGRAMMER       : Ermiyas (Endalkachew) Gulti, Mher Keshishian, Quang Minh Vu, Saje’- Antoine Rose                      */
/* FIRST VERSION    : 2025-11-30                                                                                            */
/* DESCRIPTION      : This file defines the FDMSDashboard class, which we use it  as the main WPF dashboard window.             */
/*                    It integrates SQL Server, TCP networking, telemetry visualization, and system logging.                 */
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



/*  
 *  NAMESPACE       : GroundTerminalApp
 *  DESCRIPTION     : This namespace Contains classes and methods for simulating a ground terminal dashboard.
 *                    it give us the connection with SQL Server to log system events and telemetry packets.
 *                    Hosts a TCP server to receive aircraft telemetry data streams.
 *                    Includes WPF UI logic for displaying charts, connection status, and live telemetry.
 */
namespace GroundTerminalApp
{

    /*  
     *  CLASS           : FDMSDashboard
     *  DESCRIPTION     : This class is our main dashboard window of the Ground Terminal application.
     *                  : These are the few resposibilities of this class 
     *                      1. It initializes and manages UI components like charts, labels, icons and text boxs.
     *                      2. it connects to the remote SQL Server for inserting updating and logging and telemetry data to database
     *                      3. it hosts a TCP server to receive aircraft telemetry packets.
     *                      4. it also updates live charts and status indicators based on incoming data.
     *                      5. it provides navigation to search, logs, and login pages.
     */
    public partial class FDMSDashboard : Window
    {

        /*  
         *  FIELD          : tcpListener
         *  DESCRIPTION    : This class will be used for TCP listener instance and it accept incoming client connections.
         */
        private TcpListener tcpListener;


        /*  
        *  FIELD          : listenerCancellation
        *  DESCRIPTION    : We will use this class for cancellation token source and it will stop the TCP server safely.
        */
        private CancellationTokenSource listenerCancellation;


        /*  
         *  FIELD          : packetCounter
         *  DESCRIPTION    : Counter component that processes telemetry packets and tracks statistics.
         */
        private TheCounterComponent packetCounter;

        /*  
         *  FIELD          : DefaultListenPort
         *  DESCRIPTION    : Default TCP port will be used as default port if it's not set in config
         */
        private const int DefaultListenPort = 5000;


        /*  
         *  FIELD          : listenPort
         *  DESCRIPTION    : Actual TCP port used by the server, read from configuration or default.
         */
        private int listenPort;

        /*  
         *  FIELD          : streamStatusTimer
         *  DESCRIPTION    : A time class to track the stream online status with time so 
         *                 : if its not send for a while we can apply that change to the UI
         */
        private DispatcherTimer streamStatusTimer;


        /*  
         *  CONSTRUCTOR     : FDMSDashboard()
         *  DESCRIPTION     : Initializes the FDMSDashboard window.
         *                    - Loads UI components.
         *                    - Sets up charts.
         *                    - Tests database connection and updates status icons.
         *                    - Starts TCP server for telemetry packet reception.
         *                    - Initializes stream status timer.
         */
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


        /*  
         *  METHOD          : BtnSearchAndQuery_Click
         *  DESCRIPTION     : Opens the search and query page window.
         *  PARAMETERS      : sender - event source 
         *                  : e - event arguments.
         */
        private void BtnSearchAndQuery_Click(object sender, RoutedEventArgs e)
        {
            SearchingPageApp searchPage = new SearchingPageApp(this);
            searchPage.Owner = this;
            searchPage.Show();
        }


        /*  
        *  METHOD          : BtnSystemLogs_Click
        *  DESCRIPTION     : Opens the system logs page window.
        */
        private void BtnSystemLogs_Click(object sender, RoutedEventArgs e)
        {
            logsPage logsPage = new logsPage(this);
            logsPage.Owner = this;
            logsPage.Show();
        }


        /*  
         *  METHOD          : BtnLoginPage_Click
         *  DESCRIPTION     : Opens the user login page window.
         */
        private void BtnLoginPage_Click(object sender, RoutedEventArgs e)
        {
            UsersLoginPage loginPage = new UsersLoginPage();
            loginPage.Owner = this;
            loginPage.Show();
        }


        /*  
         *  METHOD          : UpdateDashboardFromCounter()
         *  DESCRIPTION     : updating the dashboard UI (packet counter section) from the packet counter
         *                    Displays received, sent, dropped counts and updates labels for telemetry values.
         */
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


        /*  
         *  METHOD          : UpdatingTheStatusOfStream()
         *  DESCRIPTION     : Updates the stream status icons and labels based on last packet timestamp.
         *                    Clears telemetry labels if stream goes offline.
         */
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


      /*  
       *  METHOD          : SaveGroundTerminalPacketsToDB
       *  DESCRIPTION     : Persists telemetry packet data into the SQL Server database.
       *  PARAMETERS      : telemetry - Telemetry data object containing parsed packet values.
       */
        private void SaveGroundTerminalPacketsToDB(TelemetryData telemetry)
        {
            // I am creating this Method to store the packets like pitch tail number and so on so this table will store those values into database table
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

        /*  
         *  METHOD          : WriteSystemLog
         *  DESCRIPTION     : Writes system log entries into SQL Server SystemLogs table.
         *                    Falls back to local file logging if DB insert fails.
         *  PARAMETERS      : String level    - A string variable to log severity 
         *                  : String source   - A string variable for the log source
         *                  : String message  - A string variable that hold the log message.
         */
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
					// will ignore if even this fails
				}
			}
		}


        /*  
        *  METHOD          : StartTcpServer()
        *  DESCRIPTION     : Starts the TCP server to listen for incoming telemetry packets.
        *                    Reads port configuration and logs startup.
        */
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

        /*  
        *  METHOD          : StopTcpServer()
        *  DESCRIPTION     : Stops the TCP server and cancels listener tasks.
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

				// log that the TCP server stopped
				WriteSystemLog("INFO", "FDMSDashboard", "TCP server stopped.");
			}
            catch (Exception ex)
            {
                Console.WriteLine("Error stopping TCP server: " + ex.Message);
				WriteSystemLog("ERROR", "FDMSDashboard", "Error stopping TCP server: " + ex.Message);
			}
		}

        /*  
         *  METHOD          : AcceptClients
         *  DESCRIPTION     : Accepts incoming TCP clients asynchronously and spawns handler tasks.
         *  PARAMETERS      : token - cancellation token for graceful shutdown.
         */
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


        /*  
         *  METHOD          : HandleClient
         *  DESCRIPTION     : Handles a connected TCP client by reading packets and processing telemetry.
         *  PARAMETERS      : client - TCP client instance, token - cancellation token.
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
        /*  
         *  METHOD          : ReadFromStream
         *  DESCRIPTION     : Reads a specified number of bytes from a network stream.
         *  PARAMETERS      : stream - network stream, buffer - byte array, offset - start index,
         *                    count - number of bytes, token - cancellation token.
         *  RETURNS         : int - number of bytes read.
         */
        private async Task<int> ReadFromStream(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            // reading exact number of bytes from the network stream
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


        /*  
        *  METHOD          : LineChartSetupAndDisplay()
        *  DESCRIPTION     : Initializes and styles the altitude vs time line chart.
        */
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


        /*  
         *  METHOD          : UpdateLineChart
         *  DESCRIPTION     : Updates the line chart with new telemetry altitude data.
         *  PARAMETERS      : telemetry - TelemetryData object containing altitude and timestamp.
         */
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


        /*  
        *  CLASS           : DashBoardComponents
        *  DESCRIPTION     : Base class for dashboard components.
        *                    Provides a virtual rendering method to be overridden by child classes.
        */
        public class DashBoardComponents
        {
            // we need a base class for the dash board components here and with one render method
            // then we will have a render method and this method will be overriden by the child classes if its neccessarly 
            public virtual void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart or any thing ...");
            }
        }


        /*  
         *  CLASS           : ChartDisplay
         *  DESCRIPTION     : Child class of DashBoardComponents.
         *                    Represents chart rendering logic.
         */
        public class ChartDisplay : DashBoardComponents
        {
            // the second class i am thinking is the child class of the component for example if we need to display chart

             /*  
              *  CLASS           : ConnectionStatus
              *  DESCRIPTION     : Child class of DashBoardComponents.
              *                    Represents connection status display logic.
              *  PROPERTY        : IsConnected - indicates whether the system is connected.
             */
            public override void RenderingDashbrdComponents()
            {
                Console.WriteLine($"Rendering chart: title with data  points.");
            }
        }

        /*  
         *  CLASS           : ConnectionStatus
         *  DESCRIPTION     : Child class of DashBoardComponents.
         *                    Represents connection status display logic.
         *  PROPERTY        : IsConnected - indicates whether the system is connected.
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
         *  CLASS           : TheCounterComponent
         *  DESCRIPTION     : Receives TCP packets, validates checksums, and parses telemetry data.
         *                    Inherits from DashBoardComponents.
         *                    Tracks received, sent, and dropped packet counts and stores the latest telemetry.
         *                    Provides methods for packet processing, checksum validation, and rendering output.
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
              *  PROPERTY        : Received
              *  DESCRIPTION     : Gets or sets the number of successfully received packets.
              *                    Increments only when checksum validation succeeds.
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
             *  PROPERTY        : Sent
             *  DESCRIPTION     : Gets or sets the number of packets sent by the ground terminal.
             */
            public int Sent
            {
                get { return sent; }
                set { sent = value; }
            }

            /*  
             *  PROPERTY        : Dropped
             *  DESCRIPTION     : Gets or sets the number of invalid or corrupted packets.
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
             *  PROPERTY        : LastTelemetry
             *  DESCRIPTION     : Gets the most recently received and validated telemetry packet.
             *                    Returns a TelemetryData object or null if none received yet.
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
             *  METHOD          : TheCounterComponent() [Constructor]
             *  DESCRIPTION     : Initializes packet counters and telemetry storage.
             *                    Sets received, sent, and dropped counts to zero.
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
             *  METHOD          : ProcessPacket
             *  DESCRIPTION     : Receives raw TCP packet bytes, validates checksum, and parses telemetry.
             *  PARAMETERS      : byte[] packetData - Raw bytes from TCP network stream.
             *  RETURNS         : bool - True if packet is valid and processed, false otherwise.
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
             *  METHOD          : DeconstructPacket
             *  DESCRIPTION     : Parses packet structure, validates checksum, and extracts telemetry fields.
             *  PARAMETERS      : string packetLine - Complete packet as ASCII string.
             *                    out TelemetryData telemetry - Parsed telemetry if valid.
             *  RETURNS         : bool - True if packet is valid and parsed, false otherwise.
            */
            private bool DeconstructPacket(string packetLine, out TelemetryData telemetry)
            {
                telemetry = null;
			/*
			Method: DeconstructPacket
			Description: Parses a single FDMS packet, validates the checksum using
						 the (Altitude + Pitch + Bank) / 3 specification, and
						 constructs a TelemetryData object when the packet is valid.
			Parameters: string packetLine - Complete packet line including checksum
						out TelemetryData telemetry - Parsed telemetry output on success
			Returns: bool - True if the packet is valid and telemetry was created,
							false if checksum or parsing fails
			*/
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

			/*
			Method: ComputeChecksum
			Description: Computes the FDMS checksum from altitude, pitch, and bank
						 using the specification formula (Altitude + Pitch + Bank) / 3
						 and truncates the result to a signed integer.
			Parameters: double altitude - Altitude value from the packet
						double pitch - Pitch value from the packet
						double bank - Bank value from the packet
			Returns: int - Truncated checksum value as a signed integer
			*/
			private static int ComputeChecksum(double altitude, double pitch, double bank)
			{
				double checksumDouble = (altitude + pitch + bank) / 3.0;
				return (int)checksumDouble;
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


        /*  
         *  CLASS           : TelemetryData
         *  DESCRIPTION     : Data transfer object for parsed aircraft telemetry from a single packet.
         *                    Properties correspond to FDMS packet format fields.
         *                    Used to communicate between network layer and UI display components.
         */ 
        public List<TelemetryData> telemetryDataList { get; private set; } = new List<TelemetryData>();


        /*  
         *  CLASS           : TelemetryData
         *  DESCRIPTION     : Data transfer object for parsed aircraft telemetry from a single packet.
         *                    Properties correspond to FDMS packet format fields.
         *                    Used to communicate between network layer and UI display components.
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
            public int Checksum { get; set; }           // Packet checksum (validated) i am adding this cus i want to display the checksum on dashboard

            /*  
             *  PROPERTY        : Checksum
             *  DESCRIPTION     : Validated packet checksum value.
             *                    Used to confirm packet integrity and displayed on the dashboard.
             */
            public int Checksum { get; set; }
        }


        /*  
         *  CLASS           : SideBarComponents
         *  DESCRIPTION     : Child class of DashBoardComponents.
         *                    Used to render navigation between windows such as search, query, and settings.
         */
        public class SideBarComponents : DashBoardComponents
        {
            // we will use this class to render between windows like to go to search query setting and so ion
        }


        /*  
         *  CLASS           : ServerConnector
         *  DESCRIPTION     : Provides SQL Server connection management.
         *                    Stores connection string from configuration file and returns SqlConnection objects.
         *                    Used by dashboard and packet storage methods for database access.
         */
        public static class ServerConnector
        {
            /*  
             *  FIELD           : ConnectionString
             *  DESCRIPTION     : Stores the connection string retrieved from configuration file.
             */
            // I need a class level field that store and connet the app to the database. we already have the connection string in config file 
            // so this class will be a storage for the connection string  just easier to to use and access  the connection string from other classes or methods
            // this method is to store the connection string from config file
            private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["SoftwareQualityFinalProject"]?.ConnectionString;

            /*  
             *  METHOD          : GetConnection
             *  DESCRIPTION     : Creates and returns a new SqlConnection using the stored connection string.
             *  RETURNS         : SqlConnection - Active connection object for database operations.
             */
            public static SqlConnection GetConnection()
            {
                // another  method to create and return a new SqlConnection when ever we need too 
                return new SqlConnection(ConnectionString);
            }
        }
    }
}
