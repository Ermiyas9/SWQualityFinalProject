using ConsoleApp1;
using System;
using System.Configuration;
using System.Text;

Console.WriteLine("AircraftTransmitter starting...");

var telemetryFilePath = ConfigurationManager.AppSettings["TelemetryFilePath"];
var tailNumber = ConfigurationManager.AppSettings["TailNumber"];
var serverIp = ConfigurationManager.AppSettings["ServerIp"];
var serverPortString = ConfigurationManager.AppSettings["ServerPort"];
var sendIntervalMsString = ConfigurationManager.AppSettings["SendIntervalMs"];

if (string.IsNullOrWhiteSpace(telemetryFilePath) ||
	string.IsNullOrWhiteSpace(tailNumber) ||
	string.IsNullOrWhiteSpace(serverIp) ||
	string.IsNullOrWhiteSpace(serverPortString) ||
	string.IsNullOrWhiteSpace(sendIntervalMsString))
{
	throw new InvalidOperationException("I need to check App.config, one or more required settings are missing.");
}

if (!int.TryParse(serverPortString, out var serverPort))
{
	throw new InvalidOperationException("I need a valid integer for ServerPort in App.config.");
}

if (!int.TryParse(sendIntervalMsString, out var sendIntervalMs))
{
	throw new InvalidOperationException("I need a valid integer for SendIntervalMs in App.config.");
}

Console.WriteLine($"Telemetry file: {telemetryFilePath}");
Console.WriteLine($"Tail number: {tailNumber}");
Console.WriteLine($"Server: {serverIp}:{serverPort}");
Console.WriteLine($"Send interval: {sendIntervalMs} ms");
Console.WriteLine();

var reader = new TelemetryFileReader(telemetryFilePath);
var builder = new PacketBuilder(tailNumber);

var index = 0;
foreach (var line in reader.ReadLines())
{
	if (index >= 3)
	{
		break;
	}

	var packetBytes = builder.BuildPacket(line);
	var packetText = Encoding.UTF8.GetString(packetBytes);

	Console.WriteLine("Raw line:");
	Console.WriteLine(line);
	Console.WriteLine("Packet:");
	Console.WriteLine(packetText);
	Console.WriteLine();

	index++;
}

Console.WriteLine("PacketBuilder test completed.");
