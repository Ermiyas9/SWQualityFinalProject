// File:            TcpSenderTests.cs
// Programmer:      Mher Keshishian
// First version:   2025-11-22
// Purpose:         Unit test for the TcpSender class. I verify that a packet
//                  is sent with a length prefix and that the server receives
//                  the expected message body.

using AircraftTransmitter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AircraftTransmitter.Tests
{
	[TestClass]
	public class TcpSenderTests
	{
		/// <summary>
		/// Verifies that SendPacket, when called with a valid payload and an active
		/// TCP listener on the loopback address, sends a length-prefixed message
		/// and the server receives the expected message body.
		/// </summary>
		[TestMethod]
		public void SendPacket_ValidMessage_ServerReceivesExpectedBody()
		{
			// Arrange
			TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();

			int port = ((IPEndPoint)listener.LocalEndpoint).Port;

			byte[] receivedBody = null;
			Exception serverException = null;

			// Start server thread
			Thread serverThread = new Thread(() =>
			{
				try
				{
					TcpClient serverClient = listener.AcceptTcpClient();
					NetworkStream serverStream = serverClient.GetStream();

					byte[] lengthBytes = new byte[4];
					ReadExact(serverStream, lengthBytes, 4);
					int length = BitConverter.ToInt32(lengthBytes, 0);

					byte[] body = new byte[length];
					ReadExact(serverStream, body, length);

					receivedBody = body;

					serverStream.Close();
					serverClient.Close();
					listener.Stop();
				}
				catch (Exception ex)
				{
					serverException = ex;
				}
			});

			serverThread.IsBackground = true;
			serverThread.Start();

			TcpSender sender = new TcpSender("127.0.0.1", port);

			try
			{
				sender.Connect();

				string message = "HELLO-FDMS";
				byte[] payload = Encoding.ASCII.GetBytes(message);

				sender.SendPacket(payload);
			}
			finally
			{
				sender.Dispose();
			}

			// Act & Assert
			bool joined = serverThread.Join(2000);
			Assert.IsTrue(joined, "Server thread did not finish in time.");

			if (serverException != null)
			{
				Assert.Fail("Server thread threw an exception: " + serverException.Message);
			}

			Assert.IsNotNull(receivedBody, "Server did not receive any data.");

			string receivedText = Encoding.ASCII.GetString(receivedBody);
			Assert.AreEqual("HELLO-FDMS", receivedText);
		}

		private static void ReadExact(NetworkStream stream, byte[] buffer, int size)
		{
			int offset = 0;

			while (offset < size)
			{
				int read = stream.Read(buffer, offset, size - offset);
				if (read == 0)
				{
					throw new InvalidOperationException("Connection closed while reading.");
				}

				offset += read;
			}
		}
	}
}
