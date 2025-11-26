using ConsoleApp1;
using System;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;

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
	throw new InvalidOperationException("There are required settings missing in your App.config.");
}

if (!int.TryParse(serverPortString, out var serverPort))
{
	throw new InvalidOperationException("There is no valid integer for ServerPort in your App.config.");
}

if (!int.TryParse(sendIntervalMsString, out var sendIntervalMs))
{
	throw new InvalidOperationException("There is no valid integer for SendIntervalMs in your App.config.");
}

Console.WriteLine("AircraftTransmitter starting...");
Console.WriteLine($"Telemetry file: {telemetryFilePath}");
Console.WriteLine($"Tail number: {tailNumber}");
Console.WriteLine($"Server: {serverIp}:{serverPort}");
Console.WriteLine($"Send interval: {sendIntervalMs} ms");
Console.WriteLine();

var reader = new TelemetryFileReader(telemetryFilePath);
var builder = new PacketBuilder(tailNumber);

await RunAsync();

async Task RunAsync()
{
	var sender = new TcpSender(serverIp, serverPort);

	Console.WriteLine("Connecting to Ground Terminal...");

	await sender.ConnectAsync();

	Console.WriteLine("Connected. Starting transmission.");
	Console.WriteLine();

	var lineIndex = 0;

	foreach (var line in reader.ReadLines())
	{
		var packetBytes = builder.BuildPacket(line);

		await sender.SendPacketAsync(packetBytes);

		Console.WriteLine($"Sent line {lineIndex + 1}: {line.Trim()}");
		lineIndex++;

		await Task.Delay(sendIntervalMs);
	}

	Console.WriteLine();
	Console.WriteLine($"Transmission completed. Total packets sent: {lineIndex}");
}
