/*
 * Filename:    PacketParsingTests.cs
 * By:          Saje Antoine Rose
 * Date:        December 4, 2025
 * Description: Unit tests for packet parsing and checksum validation in the ground terminal.
 *              T
 */

using GroundTerminalApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Text;

namespace GroundTerminalApp.Tests
{
    [TestClass]
    public class PacketParsingTests
    {
        private FDMSDashboard.TheCounterComponent packetCounter;

        [TestInitialize]
        public void TestInitialize()
        {
            // Reset packet counter before each test
            packetCounter = new FDMSDashboard.TheCounterComponent();
        }

        /*
         * Test:       ProcessPacket_ValidPacketProvided_UpdatesReceivedAndLastTelemetry
         * Category:   UNIT / STRUCTURAL / FUNCTIONAL
         * Purpose:    Validates that a valid packet is parsed, received counter incremented,
         *             and telemetry stored correctly.
         */
        [TestMethod]
        public void ProcessPacket_ValidPacketProvided_UpdatesReceivedAndLastTelemetry()
        {
            // Arrange
            string tailNumber = "N123456";
            uint sequenceNumber = 1;
            DateTime timestamp = new DateTime(2025, 12, 4, 14, 30, 0, DateTimeKind.Utc);
            double accelX = -0.799099;
            double accelY = 0.047375;
            double accelZ = 0.028341;
            double weight = 2154.000732;
            double altitude = 1124.106079;
            double pitch = 0.022695;
            double bank = 0.001006;

            // Calculate expected checksum using FDMS formula: (altitude + pitch + bank) / 3
            int expectedChecksum = (int)((altitude + pitch + bank) / 3.0);

            // Build packet string in FDMS format (pipe-delimited)
            string packetLine = $"{tailNumber}|{sequenceNumber}|{timestamp:O}|" +
                                $"{accelX.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{accelY.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{accelZ.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{weight.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{altitude.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{pitch.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{bank.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{expectedChecksum}";

            byte[] packetData = Encoding.ASCII.GetBytes(packetLine);

            // Act
            int receivedBefore = packetCounter.Received;
            bool result = packetCounter.ProcessPacket(packetData);
            int receivedAfter = packetCounter.Received;

            // Assert
            Assert.IsTrue(result, "ProcessPacket should return true for valid packet");
            Assert.AreEqual(receivedBefore + 1, receivedAfter, "Received counter should increment by 1");
            Assert.IsNotNull(packetCounter.LastTelemetry, "LastTelemetry should be populated");
            Assert.AreEqual(tailNumber, packetCounter.LastTelemetry.TailNumber, "Tail number should match");
            Assert.AreEqual(sequenceNumber, packetCounter.LastTelemetry.SequenceNumber, "Sequence number should match");
            Assert.AreEqual(expectedChecksum, packetCounter.LastTelemetry.Checksum, "Checksum should match");
            Assert.AreEqual(altitude, packetCounter.LastTelemetry.Altitude, 0.000001, "Altitude should match");
            Assert.AreEqual(pitch, packetCounter.LastTelemetry.Pitch, 0.000001, "Pitch should match");
            Assert.AreEqual(bank, packetCounter.LastTelemetry.Bank, 0.000001, "Bank should match");
            Assert.AreNotEqual(DateTime.MinValue, packetCounter.LastUpdateOfStream, "Stream timestamp should be updated");
        }

        /*
         * Test:       ProcessPacket_CorruptedChecksumProvided_IncrementsDroppedAndReturnsFalse
         * Category:   UNIT / STRUCTURAL / FUNCTIONAL
         * Purpose:    Validates that packets with bad checksums are rejected and dropped counter incremented.
         */
        [TestMethod]
        public void ProcessPacket_CorruptedChecksumProvided_IncrementsDroppedAndReturnsFalse()
        {
            // Arrange
            string tailNumber = "N654321";
            uint sequenceNumber = 2;
            DateTime timestamp = new DateTime(2025, 12, 4, 14, 31, 0, DateTimeKind.Utc);
            double accelX = -0.450000;
            double accelY = 0.120000;
            double accelZ = 0.030000;
            double weight = 2150.500000;
            double altitude = 1120.500000;
            double pitch = 0.025000;
            double bank = 0.002000;

            // Calculate correct checksum then corrupt it intentionally
            int correctChecksum = (int)((altitude + pitch + bank) / 3.0);
            int corruptedChecksum = correctChecksum + 999;

            string packetLine = $"{tailNumber}|{sequenceNumber}|{timestamp:O}|" +
                                $"{accelX.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{accelY.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{accelZ.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{weight.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{altitude.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{pitch.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{bank.ToString(CultureInfo.InvariantCulture)}|" +
                                $"{corruptedChecksum}";

            byte[] packetData = Encoding.ASCII.GetBytes(packetLine);

            // Act
            int droppedBefore = packetCounter.Dropped;
            bool result = packetCounter.ProcessPacket(packetData);
            int droppedAfter = packetCounter.Dropped;

            // Assert
            Assert.IsFalse(result, "ProcessPacket should return false for corrupted checksum");
            Assert.AreEqual(droppedBefore + 1, droppedAfter, "Dropped counter should increment by 1");
            Assert.IsNull(packetCounter.LastTelemetry, "LastTelemetry should remain null");
            Assert.AreEqual(0, packetCounter.Received, "Received counter should not increment");
        }

        /*
         * Test:       ProcessPacket_InvalidNumericValue_IncrementsDroppedAndReturnsFalse
         * Category:   UNIT / STRUCTURAL / FUNCTIONAL
         * Purpose:    Validates that packets with non-numeric values are rejected.
         */
        [TestMethod]
        public void ProcessPacket_InvalidNumericValue_IncrementsDroppedAndReturnsFalse()
        {
            // Arrange - non-numeric value in altitude field
            string packetWithInvalidValue = "N123456|1|2025-12-04T14:30:00Z|" +
                                           "-0.799099|0.047375|0.028341|" +
                                           "2154.000732|INVALID_ALTITUDE|0.022695|0.001006|374";

            byte[] packetData = Encoding.ASCII.GetBytes(packetWithInvalidValue);

            // Act
            int droppedBefore = packetCounter.Dropped;
            bool result = packetCounter.ProcessPacket(packetData);
            int droppedAfter = packetCounter.Dropped;

            // Assert
            Assert.IsFalse(result, "ProcessPacket should return false for invalid numeric value");
            Assert.AreEqual(droppedBefore + 1, droppedAfter, "Dropped counter should increment");
        }

        /*
         * Test:       ProcessPacket_MultipleValidPackets_CountersIncrementCorrectly
         * Category:   UNIT / STRUCTURAL / FUNCTIONAL
         * Purpose:    Validates that multiple valid packets process sequentially and maintain state.
         */
        [TestMethod]
        public void ProcessPacket_MultipleValidPackets_CountersIncrementCorrectly()
        {
            // Arrange - two valid packets with different sequence numbers
            string packet1 = "N123456|1|2025-12-04T14:30:00Z|" +
                            "-0.799099|0.047375|0.028341|" +
                            "2154.000732|1124.106079|0.022695|0.001006|374";

            string packet2 = "N123456|2|2025-12-04T14:31:00Z|" +
                            "-0.500000|0.100000|0.050000|" +
                            "2155.000000|1125.000000|0.023000|0.002000|375";

            byte[] packetData1 = Encoding.ASCII.GetBytes(packet1);
            byte[] packetData2 = Encoding.ASCII.GetBytes(packet2);

            // Act
            bool result1 = packetCounter.ProcessPacket(packetData1);
            uint seqAfterFirst = packetCounter.LastTelemetry.SequenceNumber;

            bool result2 = packetCounter.ProcessPacket(packetData2);
            uint seqAfterSecond = packetCounter.LastTelemetry.SequenceNumber;

            // Assert
            Assert.IsTrue(result1, "First packet should process successfully");
            Assert.IsTrue(result2, "Second packet should process successfully");
            Assert.AreEqual(2, packetCounter.Received, "Received counter should be 2");
            Assert.AreEqual<uint>(1u, seqAfterFirst, "First packet sequence should be 1");
            Assert.AreEqual<uint>(2u, seqAfterSecond, "Second packet sequence should be 2");
            Assert.AreEqual(0, packetCounter.Dropped, "Dropped counter should remain 0");
        }
    }
}
