using ConsoleApp1;
using System;
using System.Configuration;

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
	throw new InvalidOperationException("I need to check App.config, one of the settings is missing.");
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
var lineCount = 0;

foreach (var line in reader.ReadLines())
{
	if (lineCount < 5)
	{
		Console.WriteLine(line);
	}

	lineCount++;
}

Console.WriteLine();
Console.WriteLine($"Total lines read: {lineCount}");
