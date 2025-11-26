// File:            TcpSender.cs
// Programmer:      Mher Keshishian
// First version:   2025-11-22
// Purpose:         Handles the TCP connection to the Ground Terminal.
//                  I connect as a client and send length-prefixed packets.

using System;
using System.Net.Sockets;

namespace AircraftTransmitter
{
	public class TcpSender : IDisposable
	{
		private readonly string serverIp;
		private readonly int serverPort;

		private TcpClient client;
		private NetworkStream stream;

		public TcpSender(string serverIp, int serverPort)
		{
			this.serverIp = serverIp;
			this.serverPort = serverPort;
		}
		
		public void Connect()
		{
			client = new TcpClient(serverIp, serverPort);
			stream = client.GetStream();
		}

		public void SendPacket(byte[] packet)
		{
			if (stream == null)
			{
				throw new InvalidOperationException("Are you connected?");
			}

			byte[] lengthBytes = BitConverter.GetBytes(packet.Length);

			stream.Write(lengthBytes, 0, lengthBytes.Length);
			stream.Write(packet, 0, packet.Length);
			stream.Flush();
		}

		public void Dispose()
		{
			if (stream != null)
			{
				stream.Dispose();
			}

			if (client != null)
			{
				client.Close();
			}
		}
	}
}
