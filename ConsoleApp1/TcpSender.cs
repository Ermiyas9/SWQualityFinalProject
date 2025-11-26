using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
	internal class TcpSender : IAsyncDisposable
	{
		private readonly string serverIp;
		private readonly int serverPort;

		private TcpClient? client; // it can be null
		private NetworkStream? stream; // it can be null

		public TcpSender(string serverIp, int serverPort)
		{
			this.serverIp = serverIp;
			this.serverPort = serverPort;
		}

		// connect to Ground Terminal
		public async Task ConnectAsync()
		{
			client = new TcpClient();
			await client.ConnectAsync(serverIp, serverPort);
			stream = client.GetStream();
		}

		// send a packet (with length prefix)
		public async Task SendPacketAsync(byte[] packet)
		{
			if (stream == null)
			{
				throw new InvalidOperationException("Are you connected?");
			}

			var lengthBytes = BitConverter.GetBytes(packet.Length);
			await stream.WriteAsync(lengthBytes.AsMemory());
			await stream.WriteAsync(packet.AsMemory());
			await stream.FlushAsync();
		}

		public async ValueTask DisposeAsync()
		{
			if (stream != null)
			{
				await stream.DisposeAsync();
			}

			// close if not null
			client?.Close();
		}
	}
}
