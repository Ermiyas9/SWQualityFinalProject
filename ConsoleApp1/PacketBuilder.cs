using System;
using System.Globalization;
using System.Text;

namespace AircraftTransmitter
{
	public class PacketBuilder
	{
		private readonly string tailNumber;

		public PacketBuilder(string tailNumber)
		{
			this.tailNumber = tailNumber;
		}

		public byte[] BuildPacket(string rawLine)
		{
			string[] parts = rawLine.Split(',', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 8)
			{
				throw new FormatException($"Telemetry line has wrong format: {rawLine}");
			}

			string timestampText = parts[0].Trim();
			string normalizedTimestamp = timestampText.Replace('_', ' ');
			DateTime timestamp = DateTime.Parse(normalizedTimestamp, CultureInfo.InvariantCulture);

			double field1 = double.Parse(parts[1], CultureInfo.InvariantCulture);
			double field2 = double.Parse(parts[2], CultureInfo.InvariantCulture);
			double field3 = double.Parse(parts[3], CultureInfo.InvariantCulture);
			double field4 = double.Parse(parts[4], CultureInfo.InvariantCulture);
			double field5 = double.Parse(parts[5], CultureInfo.InvariantCulture);
			double field6 = double.Parse(parts[6], CultureInfo.InvariantCulture);
			double field7 = double.Parse(parts[7], CultureInfo.InvariantCulture);

			string payload =
				$"{tailNumber}|" +
				$"{timestamp:O}|" +
				$"{field1.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field2.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field3.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field4.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field5.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field6.ToString(CultureInfo.InvariantCulture)}|" +
				$"{field7.ToString(CultureInfo.InvariantCulture)}";

			int checksum = ComputeChecksum(payload);

			string packetLine = $"{payload}|{checksum}";
			string packetWithNewline = packetLine + "\n";

			return Encoding.ASCII.GetBytes(packetWithNewline);
		}

		private static int ComputeChecksum(string payload)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(payload);
			int sum = 0;

			foreach (byte b in bytes)
			{
				sum += b;
			}

			return sum & 0xFFFF;
		}
	}
}
