using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
	internal class PacketBuilder
	{
		private readonly string tailNumber;

		// Constructors
		public PacketBuilder(string tailNumber)
		{
			this.tailNumber = tailNumber;
		}

		public byte[] BuildPacket(string rawLine)
		{
			var parts = rawLine.Split(',', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 8)
			{
				throw new FormatException($"Telemetry line with wrong format: {rawLine}");
			}

			var timestampText = parts[0].Trim();                         // "7_8_2018 19:34:3"
			var normalizedTimestamp = timestampText.Replace('_', ' ');   // "7 8 2018 19:34:3"
			var timestamp = DateTime.Parse(normalizedTimestamp, CultureInfo.InvariantCulture);

			// Parse string to double. ensure "." as decimal separator
			var field1 = double.Parse(parts[1], CultureInfo.InvariantCulture);
			var field2 = double.Parse(parts[2], CultureInfo.InvariantCulture);
			var field3 = double.Parse(parts[3], CultureInfo.InvariantCulture);
			var field4 = double.Parse(parts[4], CultureInfo.InvariantCulture);
			var field5 = double.Parse(parts[5], CultureInfo.InvariantCulture);
			var field6 = double.Parse(parts[6], CultureInfo.InvariantCulture);
			var field7 = double.Parse(parts[7], CultureInfo.InvariantCulture);

			// Build payload string with "|" separator
			var payload =
				$"{tailNumber}|" +
				$"{timestamp:O}|" +
				$"{field1.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field2.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field3.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field4.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field5.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field6.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field7.ToString(CultureInfo.InvariantCulture)}";

			var checksum = ComputeChecksum(payload);

			var packetLine = $"{payload}|{checksum}";
			var packetWithNewline = packetLine + "\n";

			return Encoding.UTF8.GetBytes(packetWithNewline);
		}

		// calculate checksum based on sum of all bytes modulo 65536
		private static int ComputeChecksum(string payload)
		{
			// convert payload string into array of bytes
			var bytes = Encoding.UTF8.GetBytes(payload);
			var sum = 0;

			foreach (var b in bytes)
			{
				sum += b;
			}

			return sum & 0xFFFF;
		}
	}
}
