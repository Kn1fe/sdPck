using System;
using System.IO;
using System.Text;

namespace sdPck
{
	public class PCKFileEntry
	{
		public string Path { get; set; }
		public uint Offset { get; set; }
		public int Size { get; set; }
		public int CompressedSize { get; set; }

		public PCKFileEntry()
		{

		}

		public PCKFileEntry(byte[] bytes)
		{
			Read(bytes);
		}

		public void Read(byte[] bytes)
		{
			if (bytes.Length < 276)
			{
				//Decompress
				bytes = PCKZlib.Decompress(bytes, 276);
			}
			BinaryReader br = new BinaryReader(new MemoryStream(bytes));
			Path = Encoding.GetEncoding(936).GetString(br.ReadBytes(260)).Split(new string[] { "\0" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace("/", "\\");
			Offset = br.ReadUInt32();
			Size = br.ReadInt32();
			CompressedSize = br.ReadInt32();
			br.Close();
		}

		public byte[] Write(int CompressionLevel)
		{
			byte[] buffer = new byte[276];
			MemoryStream msb = new MemoryStream(buffer);
			BinaryWriter bw = new BinaryWriter(msb);
			bw.Write(Encoding.GetEncoding("GB2312").GetBytes(Path.Replace("/", "\\")));
            bw.BaseStream.Seek(260, SeekOrigin.Begin);
            bw.Write(Offset);
            bw.Write(Size);
            bw.Write(CompressedSize);
            bw.Write(0);
            msb.Seek(0, SeekOrigin.Begin);
			return PCKZlib.Compress(msb.ToArray(), CompressionLevel);
		}
	}
}
