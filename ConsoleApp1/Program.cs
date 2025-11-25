using System;
using System.Configuration;

Console.WriteLine("AircraftTransmitter starting...");

// Read connection string from App.config
string connectionString = GetConnectionString();

// Print a preview (do NOT show the full password in logs)
Console.WriteLine("Connection string loaded from App.config.");

// TODO later: call my DbTester and telemetry sender loop here.

// Local helper function that reads the connection string
static string GetConnectionString()
{
	var cs = ConfigurationManager.ConnectionStrings["FdmsDb"]?.ConnectionString;

	if (string.IsNullOrWhiteSpace(cs))
	{
		throw new InvalidOperationException(
			"Connection string 'FdmsDb' not found. Check App.config <connectionStrings>.");
	}

	return cs;
}
