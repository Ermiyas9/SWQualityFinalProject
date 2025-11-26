using System.Collections.Generic;
using System.IO;

namespace ConsoleApp1
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
