// File:            Program.cs
// Programmer:      Mher Keshishian
// First version:   2025-11-22
// Purpose:         Entry point for the aircraft transmitter console application.
//                  Reads settings from App.config, loads a telemetry file, builds
//                  packets, and sends them to the Ground Terminal over TCP.

using AircraftTransmitter;
using System;
using System.Configuration;
using System.Threading;

internal class Program
{
	private static void Main()
	{
		// PARSING INFO FROM CONFIG FILES
		
		string telemetryFilePath = ConfigurationManager.AppSettings["TelemetryFilePath"];
		string tailNumber = ConfigurationManager.AppSettings["TailNumber"];
		string serverIp = ConfigurationManager.AppSettings["ServerIp"];
		string serverPortString = ConfigurationManager.AppSettings["ServerPort"];
		string sendIntervalMsString = ConfigurationManager.AppSettings["SendIntervalMs"];

		if (string.IsNullOrWhiteSpace(telemetryFilePath) ||
			string.IsNullOrWhiteSpace(tailNumber) ||
			string.IsNullOrWhiteSpace(serverIp) ||
			string.IsNullOrWhiteSpace(serverPortString) ||
			string.IsNullOrWhiteSpace(sendIntervalMsString))
		{
			throw new InvalidOperationException("There are required settings missing in your App.config.");
		}

		if (!int.TryParse(serverPortString, out int serverPort))
		{
			throw new InvalidOperationException("There is no valid integer for ServerPort in your App.config.");
		}

		if (!int.TryParse(sendIntervalMsString, out int sendIntervalMs))
		{
			throw new InvalidOperationException("There is no valid integer for SendIntervalMs in your App.config.");
		}

		// starting service

		Console.WriteLine("AircraftTransmitter starting...");
		Console.WriteLine($"Telemetry file: {telemetryFilePath}");
		Console.WriteLine($"Tail number: {tailNumber}");
		Console.WriteLine($"Server: {serverIp}:{serverPort}");
		Console.WriteLine($"Send interval: {sendIntervalMs} ms");
		Console.WriteLine();

		TelemetryFileReader reader = new TelemetryFileReader(telemetryFilePath);
		PacketBuilder builder = new PacketBuilder(tailNumber);
		TcpSender sender = new TcpSender(serverIp, serverPort);

		try
		{
			Console.WriteLine("Connecting to Ground Terminal...");

			sender.Connect();

			Console.WriteLine("Connected. Starting transmission.");
			Console.WriteLine();

			int lineIndex = 0;

			foreach (string line in reader.ReadLines())
			{
				byte[] packetBytes = builder.BuildPacket(line);

				sender.SendPacket(packetBytes);

				Console.WriteLine($"Sent line {lineIndex + 1}: {line.Trim()}");
				lineIndex++;

				Thread.Sleep(sendIntervalMs);
			}

			Console.WriteLine();
			Console.WriteLine($"Transmission completed. Total packets sent: {lineIndex}");
		}
		finally
		{
			sender.Dispose();
		}
	}
}