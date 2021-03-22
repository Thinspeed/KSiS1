using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Linq;

namespace TestingWeb
{
	class ICMP
	{
		public byte Type;
		public byte Code;
		public UInt16 Checksum;
		public int MessageSize;
		public UInt16 Identifire;
		public UInt16 SequenceNumber;
		public byte[] Message = new byte[1024];

		public ICMP()
		{
		}

		public ICMP(byte[] data, int size)
		{
			Type = data[20];
			Code = data[21];
			Checksum = BitConverter.ToUInt16(data, 22);
			Identifire = BitConverter.ToUInt16(data, 24);
			SequenceNumber = BitConverter.ToUInt16(data, 26); 
			MessageSize = size - 24;
			Buffer.BlockCopy(data, 28, Message, 0, MessageSize);
		}

		public byte[] getBytes()
		{
			byte[] data;
			if (MessageSize % 2 == 0)
			{
				 data = new byte[MessageSize + 8];
			}
			else
            {
				data = new byte[(MessageSize + 1) + 8];
			}

			Buffer.BlockCopy(BitConverter.GetBytes(Type), 0, data, 0, 1);
			Buffer.BlockCopy(BitConverter.GetBytes(Code), 0, data, 1, 1);
			Buffer.BlockCopy(BitConverter.GetBytes(Checksum), 0, data, 2, 2);
			Buffer.BlockCopy(BitConverter.GetBytes(Identifire).Reverse().ToArray(), 0, data, 4, 2);
			Buffer.BlockCopy(BitConverter.GetBytes(SequenceNumber).Reverse().ToArray(), 0, data, 6, 2);
			Buffer.BlockCopy(Message, 0, data, 8, MessageSize);
			return data;
		}

		public UInt16 getChecksum()
		{
			Checksum = 0;
			UInt32 chcksm = 0;
			byte[] data = getBytes();
			int packetsize = MessageSize + 8;
			for (int i = 0; i < packetsize; i+= 2)
			{
				chcksm += Convert.ToUInt32(BitConverter.ToUInt16(data, i));
			}

			chcksm = (chcksm >> 16) + (chcksm & 0xffff);
			chcksm += (chcksm >> 16);
			return (UInt16)(~chcksm);
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			bool endPointReached = false;
			var data = new byte[1024];
			data[0] = 8; data[1] = 0;
			string trecerIP = Console.ReadLine();
			IPHostEntry ipHost = Dns.GetHostEntry(trecerIP);
			IPAddress ipAddr = ipHost.AddressList[0];
			IPEndPoint iPEndPoint = new IPEndPoint(ipAddr, 8753);
			var endPoint = (EndPoint)iPEndPoint;

			Socket socket = new Socket(ipAddr.AddressFamily, SocketType.Raw, ProtocolType.Icmp);

			ICMP packet = new ICMP();
			packet.Type = 8;
			packet.Code = 0;
			packet.Checksum = 0;
			packet.Identifire = 1;
			packet.SequenceNumber = 1;
			data = Encoding.ASCII.GetBytes("test packet");
			Buffer.BlockCopy(data, 0, packet.Message, 0, data.Length);
			packet.MessageSize = data.Length;
			int packetsize = packet.MessageSize + 8;

			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);

			for (int i = 1; i < 33; i++)
            {
				if (endPointReached)
				{
					break;
				}

				int badCount = 0;
				Console.Write("{0, 3}.", i);
				socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, i);
				for (int j = 0; j < 3; j++)
				{
					packet.Checksum = packet.getChecksum();
					DateTime timeStart = DateTime.Now;
					socket.SendTo(packet.getBytes(), packetsize, SocketFlags.None, iPEndPoint);
					try
					{
						data = new byte[1024];
						int recv = socket.ReceiveFrom(data, ref endPoint);
						TimeSpan timestop = DateTime.Now - timeStart;
						ICMP response = new ICMP(data, recv);

						Console.Write("{0, 4}ms", timestop.Milliseconds);
						if (response.Type == 0)
						{
							endPointReached = true;
						}
					}
					catch (SocketException)
					{
						Console.Write("{0, 6}", "*  ");
						badCount++;
					}

					packet.SequenceNumber++;
				}


				if (badCount == 3)
				{
					Console.WriteLine("  Превышен интервал ожидания для запроса.");
				}
				else
				{
                    try
                    {
						Console.WriteLine("  {0} [{1}]", Dns.GetHostEntry(endPoint.ToString().Split(':')[0]).HostName, 
														 endPoint.ToString().Split(':')[0]);
                    }
                    catch
                    {
						Console.WriteLine("  {0}", endPoint.ToString().Split(':')[0]);
					}
				}
			}
		}
	}
}
