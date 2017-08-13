using System;
using System.IO;

namespace sdPck
{
	public class PCKStream : IDisposable
	{
		protected BufferedStream pck = null;
		protected BufferedStream pkx = null;
		string path = "";
		public long Position = 0;
		public PCKKey key = new PCKKey();
		const int PCK_MAX_SIZE = 2147483392;

		public PCKStream(string path, PCKKey key = null)
		{
			this.path = path;
			if (key != null)
			{
				this.key = key;
			}
			pck = new BufferedStream(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite), 33554432);
			if (File.Exists(path.Replace(".pck", ".pkx")) && Path.GetExtension(path) != ".cup")
				pkx = new BufferedStream(new FileStream(path.Replace(".pck", ".pkx"), FileMode.Open, FileAccess.ReadWrite), 33554432);
		}

		public void Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					Position = offset;
					break;
				case SeekOrigin.Current:
					Position += offset;
					break;
				case SeekOrigin.End:
					Position = GetLenght() + offset;
					break;
			}
		}

		public long GetLenght()
		{
			return pkx != null ? pck.Length + pkx.Length : pck.Length;
		}

		public byte[] ReadBytes(int count)
		{
			byte[] array = new byte[count];
			int BytesRead = 0;
			if (Position < pck.Length)
			{
				pck.Seek(Position, SeekOrigin.Begin);
				BytesRead = pck.Read(array, 0, count);
				if (BytesRead < count && pkx != null)
				{
					pkx.Seek(0, SeekOrigin.Begin);
					BytesRead += pkx.Read(array, BytesRead, count - BytesRead);
				}
			}
			else if (Position > pck.Length && pkx != null)
			{
				pkx.Seek(Position - pck.Length, SeekOrigin.Begin);
				BytesRead = pkx.Read(array, 0, count);
			}
			Position += count;
			return array;
		}

		public void WriteBytes(byte[] array)
		{
			if (Position >= PCK_MAX_SIZE)
			{
				if (pkx == null)
				{
					pkx = new BufferedStream(new FileStream(path.Replace(".pck", ".pkx"), FileMode.Create, FileAccess.ReadWrite), 33554432);
				}
				pkx.Seek(Position - pck.Length, SeekOrigin.Begin);
				pkx.Write(array, 0, array.Length);
			}
			else if (Position + array.Length < PCK_MAX_SIZE)
			{
				pck.Seek(Position, SeekOrigin.Begin);
				pck.Write(array, 0, array.Length);
			}
			else
			{
				if (pkx == null)
				{
					pkx = new BufferedStream(new FileStream(path.Replace(".pck", ".pkx"), FileMode.Create, FileAccess.ReadWrite), 33554432);
				}
				pck.Seek(Position, SeekOrigin.Begin);
				pck.Write(array, 0, (int)(PCK_MAX_SIZE - Position));
				pkx.Write(array, (int)(PCK_MAX_SIZE - Position), array.Length - (int)(PCK_MAX_SIZE - Position));
			}
			Position += array.Length;
		}

		public uint ReadUInt32()
		{
			return BitConverter.ToUInt32(ReadBytes(4), 0);
		}

		public int ReadInt32()
		{
			return BitConverter.ToInt32(ReadBytes(4), 0);
		}

		public void WriteUInt32(uint value)
		{
			WriteBytes(BitConverter.GetBytes(value));
		}

		public void WriteInt32(int value)
		{
			WriteBytes(BitConverter.GetBytes(value));
		}

		public void WriteInt16(short value)
		{
            WriteBytes(BitConverter.GetBytes(value));
		}

		public void Dispose()
		{
			pck?.Close();
			pkx?.Close();
		}
	}
}
