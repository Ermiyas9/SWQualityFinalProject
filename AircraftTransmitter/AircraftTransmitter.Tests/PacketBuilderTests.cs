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

			// Act
			string line = " 7_8_2018 19:34:3,-0.319754, -0.716176, 1.797150, 2154.670410, 1643.844116, 0.022278, 0.033622,";

			byte[] packetBytes = builder.BuildPacket(line);
			string packetText = Encoding.ASCII.GetString(packetBytes).TrimEnd('\n');

			// Assert
			string[] parts = packetText.Split('|');
			Assert.AreEqual(10, parts.Length, "Packet should have 10 parts (tail + timestamp + 7 fields + checksum).");

			Assert.AreEqual(tailNumber, parts[0], "Tail number does not match.");

			bool parsedTimestamp = DateTime.TryParse(parts[1], null, DateTimeStyles.RoundtripKind, out _);
			Assert.IsTrue(parsedTimestamp, "Timestamp is not in a valid round-trip format.");

			double field1 = double.Parse("-0.319754", CultureInfo.InvariantCulture);
			double field2 = double.Parse("-0.716176", CultureInfo.InvariantCulture);
			double field3 = double.Parse("1.797150", CultureInfo.InvariantCulture);
			double field4 = double.Parse("2154.670410", CultureInfo.InvariantCulture);
			double field5 = double.Parse("1643.844116", CultureInfo.InvariantCulture);
			double field6 = double.Parse("0.022278", CultureInfo.InvariantCulture);
			double field7 = double.Parse("0.033622", CultureInfo.InvariantCulture);

			Assert.AreEqual(field1.ToString(CultureInfo.InvariantCulture), parts[2]);
			Assert.AreEqual(field2.ToString(CultureInfo.InvariantCulture), parts[3]);
			Assert.AreEqual(field3.ToString(CultureInfo.InvariantCulture), parts[4]);
			Assert.AreEqual(field4.ToString(CultureInfo.InvariantCulture), parts[5]);
			Assert.AreEqual(field5.ToString(CultureInfo.InvariantCulture), parts[6]);
			Assert.AreEqual(field6.ToString(CultureInfo.InvariantCulture), parts[7]);
			Assert.AreEqual(field7.ToString(CultureInfo.InvariantCulture), parts[8]);

			double expectedChecksumDouble = (field5 + field6 + field7) / 3.0;
			int expectedChecksum = (int)expectedChecksumDouble;

			int actualChecksum = int.Parse(parts[9], CultureInfo.InvariantCulture);
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
			string tailNumber = "C-FGAX";
			PacketBuilder builder = new PacketBuilder(tailNumber);

			string line = "7_8_2018 19:35:21,-0.799099, 0.047375, 0.028341, 2154.000732, 1124.106079, 0.022695, 0.001006,";

			byte[] packetBytes = builder.BuildPacket(line);
			string packetText = Encoding.ASCII.GetString(packetBytes).TrimEnd('\n');

			string[] parts = packetText.Split('|');
			Assert.AreEqual(10, parts.Length);

			int actualChecksum = int.Parse(parts[9], CultureInfo.InvariantCulture);
			Assert.AreEqual(374, actualChecksum);
		}
	}
}