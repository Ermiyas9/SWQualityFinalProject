// File:            TelemetryFileReader.cs
// Programmer:      Mher Keshishian
// First version:   2025-11-22
// Purpose:         Reads telemetry lines from a text file so I can feed them
//                  into the packet builder and transmitter.

using System.Collections.Generic;
using System.IO;

namespace AircraftTransmitter
{
	internal class TelemetryFileReader
	{
		private readonly string filePath;

		public TelemetryFileReader(string filePath)
		{
			this.filePath = filePath;
		}

		public IEnumerable<string> ReadLines()
		{
			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException($"Telemetry file not found: {filePath}");
			}

			return File.ReadLines(filePath);
		}
	}
}
