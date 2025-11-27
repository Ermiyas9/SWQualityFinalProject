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
            private TelemetryData lastTelemetry;
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
                        Bank = double.Parse(bankText)
                    };

                    return true;
                }
                catch (Exception ex)
                {
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
