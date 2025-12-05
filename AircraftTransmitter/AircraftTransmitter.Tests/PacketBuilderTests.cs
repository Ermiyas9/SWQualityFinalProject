// File:            PacketBuilderTests.cs
// Programmer:      Mher Keshishian
// First version:   2025-11-22
// Purpose:         Unit test for the PacketBuilder class.

using AircraftTransmitter;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AircraftTransmitter.Tests
{
	[TestClass]
	public class PacketBuilderTests
	{
		/// <summary>
		/// Tests that the BuildPacket method correctly constructs a telemetry packet from
		/// a valid input line, ensuring all fields and the checksum are accurate.
		/// </summary>
		[TestMethod]
		public void BuildPacket_ValidTelemetryLine_ReturnsPacketWithCorrectFieldsAndChecksum()
		{
			// Arrange
			string tailNumber = "C-FGAX";
			PacketBuilder builder = new PacketBuilder(tailNumber);

			string line = " 7_8_2018 19:34:3,-0.319754, -0.716176, 1.797150, 2154.670410, 1643.844116, 0.022278, 0.033622,";

			// Act
			byte[] packetBytes = builder.BuildPacket(line);
			string packetText = Encoding.ASCII.GetString(packetBytes).TrimEnd('\n');

			// Assert
			string[] parts = packetText.Split('|');

			// Tail + Sequence + Timestamp + 7 numeric fields + Checksum = 11 parts total
			Assert.AreEqual(11, parts.Length, "Packet should have 11 parts (tail + sequence + timestamp + 7 fields + checksum).");

			// Header: tail + sequence
			Assert.AreEqual(tailNumber, parts[0], "Tail number does not match.");
			Assert.AreEqual("0", parts[1], "First packet should have sequence number 0.");

			// Timestamp (round-trip format)
			bool parsedTimestamp = DateTime.TryParse(
				parts[2],
				null,
				DateTimeStyles.RoundtripKind,
				out _);
			Assert.IsTrue(parsedTimestamp, "Timestamp is not in a valid round-trip format.");

			// Expected numeric fields from the original CSV line
			double fieldAccelX = double.Parse("-0.319754", CultureInfo.InvariantCulture);
			double fieldAccelY = double.Parse("-0.716176", CultureInfo.InvariantCulture);
			double fieldAccelZ = double.Parse("1.797150", CultureInfo.InvariantCulture);
			double fieldWeight = double.Parse("2154.670410", CultureInfo.InvariantCulture);
			double fieldAltitude = double.Parse("1643.844116", CultureInfo.InvariantCulture);
			double fieldPitch = double.Parse("0.022278", CultureInfo.InvariantCulture);
			double fieldBank = double.Parse("0.033622", CultureInfo.InvariantCulture);

			// Body: AccelX | AccelY | AccelZ | Weight | Altitude | Pitch | Bank
			Assert.AreEqual(fieldAccelX.ToString(CultureInfo.InvariantCulture), parts[3]);
			Assert.AreEqual(fieldAccelY.ToString(CultureInfo.InvariantCulture), parts[4]);
			Assert.AreEqual(fieldAccelZ.ToString(CultureInfo.InvariantCulture), parts[5]);
			Assert.AreEqual(fieldWeight.ToString(CultureInfo.InvariantCulture), parts[6]);
			Assert.AreEqual(fieldAltitude.ToString(CultureInfo.InvariantCulture), parts[7]);
			Assert.AreEqual(fieldPitch.ToString(CultureInfo.InvariantCulture), parts[8]);
			Assert.AreEqual(fieldBank.ToString(CultureInfo.InvariantCulture), parts[9]);

			// Checksum = (Altitude + Pitch + Bank) / 3, truncated to int
			double expectedChecksumDouble = (fieldAltitude + fieldPitch + fieldBank) / 3.0;
			int expectedChecksum = (int)expectedChecksumDouble;

			int actualChecksum = int.Parse(parts[10], CultureInfo.InvariantCulture);
			Assert.AreEqual(expectedChecksum, actualChecksum, "Checksum does not match expected value.");
		}

		/// <summary>
		/// Verifies that the BuildPacket method throws an exception when
		/// provided with an invalid telemetry line.
		/// </summary>
		[TestMethod]
		public void BuildPacket_InvalidTelemetryLine_ThrowsFormatException()
		{
			// Arrange
			string tailNumber = "C-FGAX";
			PacketBuilder builder = new PacketBuilder(tailNumber);

			// Act
			string badLine = "bad,data,only,three";

			// Assert
			Assert.ThrowsException<FormatException>(() => builder.BuildPacket(badLine));
		}

		/// <summary>
		/// Tests that the ComputeChecksum method produces a packet with the expected checksum
		/// when provided with a specific input line.
		/// </summary>
		[TestMethod]
		public void BuildPacket_SpecExampleLine_ProducesChecksum374()
		{
			// Arrange
			string tailNumber = "C-FGAX";
			PacketBuilder builder = new PacketBuilder(tailNumber);

			string line = "7_8_2018 19:35:21,-0.799099, 0.047375, 0.028341, 2154.000732, 1124.106079, 0.022695, 0.001006,";

			// Act
			byte[] packetBytes = builder.BuildPacket(line);
			string packetText = Encoding.ASCII.GetString(packetBytes).TrimEnd('\n');

			// Assert
			string[] parts = packetText.Split('|');
			Assert.AreEqual(11, parts.Length, "Packet should have 11 parts including checksum.");

			int actualChecksum = int.Parse(parts[10], CultureInfo.InvariantCulture);
			Assert.AreEqual(374, actualChecksum, "Checksum for spec example line should be 374.");
		}
	}
}