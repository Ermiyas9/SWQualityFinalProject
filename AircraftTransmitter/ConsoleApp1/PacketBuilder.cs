// File:            PacketBuilder.cs
// Programmer:      Mher Keshishian
// First version:   2025-11-22
// Purpose:         Converts a raw telemetry line into a length-prefixed packet
//                  with a simple checksum, ready to be sent over TCP.

using System;
using System.Globalization;
using System.Text;

namespace AircraftTransmitter
{
	public class PacketBuilder
	{
		private readonly string tailNumber;
		private uint sequenceNumber;

		public PacketBuilder(string tailNumber)
		{
			this.tailNumber = tailNumber;
			this.sequenceNumber = 0;
		}

		/// <summary>
		/// Builds a telemetry packet from a raw CSV telemetry line,
		/// formatting the fields into the FDMS payload and appending
		/// a checksum based on altitude, pitch, and bank.
		/// </summary>
		/// <param name="rawLine">Raw telemetry line from the aircraft CSV file.</param>
		/// <returns>ASCII-encoded packet bytes ready to be sent over TCP.</returns>
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

			double accelX = double.Parse(parts[1], CultureInfo.InvariantCulture);
			double accelY = double.Parse(parts[2], CultureInfo.InvariantCulture);
			double accelZ = double.Parse(parts[3], CultureInfo.InvariantCulture);
			double weight = double.Parse(parts[4], CultureInfo.InvariantCulture);
			double altitude = double.Parse(parts[5], CultureInfo.InvariantCulture);
			double pitch = double.Parse(parts[6], CultureInfo.InvariantCulture);
			double bank = double.Parse(parts[7], CultureInfo.InvariantCulture);

			uint currentSequence = sequenceNumber++;

			string payload =
				$"{tailNumber}|" +
				$"{currentSequence}|" +
				$"{timestamp:O}|" +
				$"{accelX.ToString(CultureInfo.InvariantCulture)}|" +
				$"{accelY.ToString(CultureInfo.InvariantCulture)}|" +
				$"{accelZ.ToString(CultureInfo.InvariantCulture)}|" +
				$"{weight.ToString(CultureInfo.InvariantCulture)}|" +
				$"{altitude.ToString(CultureInfo.InvariantCulture)}|" +
				$"{pitch.ToString(CultureInfo.InvariantCulture)}|" +
				$"{bank.ToString(CultureInfo.InvariantCulture)}";

			int checksum = ComputeChecksum(altitude, pitch, bank);

			string packetLine = $"{payload}|{checksum}";
			string packetWithNewline = packetLine + "\n";

			return Encoding.ASCII.GetBytes(packetWithNewline);
		}

		/// <summary>
		/// Computes the packet checksum from altitude, pitch, and bank
		/// using the FDMS specification formula (Alt + Pitch + Bank) / 3,
		/// with truncation to a signed integer.
		/// </summary>
		/// <param name="altitude">Altitude value from the telemetry record.</param>
		/// <param name="pitch">Pitch value from the telemetry record.</param>
		/// <param name="bank">Bank value from the telemetry record.</param>
		/// <returns>Checksum as a signed integer.</returns>
		private static int ComputeChecksum(double altitude, double pitch, double bank)
		{
			double checksumDouble = (altitude + pitch + bank) / 3.0;
			return (int)checksumDouble;
		}
	}
}
