using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
				throw new FileNotFoundException($"Telemetry file not exists: {filePath}");
			}

			return File.ReadLines(filePath);
		}
	}
}
