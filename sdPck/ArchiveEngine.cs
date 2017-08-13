using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace sdPck
{
    public class ArchiveEngine : INotifyPropertyChanged
    {
        private string _ProgressText = "";
        public string ProgressText
        {
            get => _ProgressText;
            set
            {
                if (_ProgressText != value)
                {
                    _ProgressText = value;
                    OnPropertyChanged("ProgressText");
                }
            }
        }
        public int ProgressMax = 0;
        public int progressvalue = 0;
        public int ProgressValue
        {
            get => progressvalue;
            set
            {
                progressvalue = value;
                OnPropertyChanged("ProgressPercent");
            }
        }
        public int ProgressPercent => Math.Round((float)ProgressValue / (float)ProgressMax * 100, 0).ToInt();
        private int _CompressionLevel = 9;
        public int CompressionLevel
        {
            get => _CompressionLevel - 1;
            set
            {
                if (_CompressionLevel + 1 != value)
                {
                    _CompressionLevel = value + 1;
                    OnPropertyChanged("CompressionLevel");
                }
            }
        }

        public PCKFileEntry[] ReadFileTable(PCKStream stream)
        {
            ProgressText = "Чтение файловой таблицы";
            stream.Seek(-8, SeekOrigin.End);
            int FilesCount = stream.ReadInt32();
            stream.Seek(-272, SeekOrigin.End);
            long FileTableOffset = (long)((ulong)((uint)(stream.ReadUInt32() ^ (ulong)stream.key.KEY_1)));
            PCKFileEntry[] entrys = new PCKFileEntry[FilesCount];
            stream.Seek(FileTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < entrys.Length; ++i)
            {
                int EntrySize = stream.ReadInt32() ^ stream.key.KEY_1;
                stream.ReadInt32();
                entrys[i] = new PCKFileEntry(stream.ReadBytes(EntrySize));
            }
            Console.WriteLine();
            return entrys;
        }

        public void Unpack(string path)
        {
            new Thread(() => _Unpack(path)).Start();
        }

        public void Compress(string path)
        {
            new Thread(() => _Compress(path)).Start();
        }

        public void UnpackCup(string path)
        {
            new Thread(() => _UnpackCup(path)).Start();
        }

        private void _Unpack(string path)
        {
            string dir = $"{path}.files";
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            PCKStream stream = new PCKStream(path);
            PCKFileEntry[] files = ReadFileTable(stream);
            ProgressMax = files.Length;
            foreach (PCKFileEntry entry in files)
            {
                ProgressText = $"Распаковка {ProgressValue}/{files.Length}: {entry.Path}";
                string p = Path.Combine(dir, Path.GetDirectoryName(entry.Path));
                if (!Directory.Exists(p))
                {
                    Directory.CreateDirectory(p);
                }
                File.WriteAllBytes(Path.Combine(dir, entry.Path), ReadFile(stream, entry));
                ++ProgressValue;
            }
            stream.Dispose();
            ProgressValue = 0;
            ProgressText = "Архив успешно распакован";
        }

        public void _Compress(string dir)
        {
            string pck = dir.Replace(".files", "");
            if (File.Exists(pck))
                File.Delete(pck);
            if (File.Exists(pck.Replace(".pck", ".pkx")))
                File.Delete(pck.Replace(".pck", ".pkx"));
            ProgressText = "Получаем список файлов";
            string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            for (int a = 0; a < files.Length; ++a)
            {
                files[a] = files[a].Replace(dir, "").Replace("/", "\\").Remove(0, 1);
            }
            List<PCKFileEntry> Table = new List<PCKFileEntry>();
            PCKStream stream = new PCKStream(pck);
            stream.WriteInt32(stream.key.FSIG_1);
            stream.WriteInt32(0);
            stream.WriteInt32(stream.key.FSIG_2);
            ProgressMax = files.Length;
            foreach (string file in files)
            {
                ProgressText = $"Запаковка {ProgressValue}/{files.Length}: {file.Replace(dir, "")}";
                byte[] buffer = File.ReadAllBytes(Path.Combine(dir, files[ProgressValue]));
                byte[] compressed = PCKZlib.Compress(buffer, CompressionLevel);
                Table.Add(new PCKFileEntry()
                {
                    Path = files[ProgressValue],
                    Offset = (uint)stream.Position,
                    Size = buffer.Length,
                    CompressedSize = compressed.Length
                });
                stream.WriteBytes(compressed);
                ++ProgressValue;
            }
            ProgressValue = 0;
            long FileTableOffset = stream.Position;
            foreach (PCKFileEntry entry in Table)
            {
                ProgressText = $"Запись файловой таблицы {ProgressValue}/{files.Length}";
                byte[] buffer = Table[ProgressValue].Write(CompressionLevel);
                stream.WriteInt32(buffer.Length ^ stream.key.KEY_1);
                stream.WriteInt32(buffer.Length ^ stream.key.KEY_2);
                stream.WriteBytes(buffer);
                ++ProgressValue;
            }
            stream.WriteInt32(stream.key.ASIG_1);
            stream.WriteInt16(2);
            stream.WriteInt16(2);
            stream.WriteUInt32((uint)(FileTableOffset ^ stream.key.KEY_1));
            stream.WriteInt32(0);
            stream.WriteBytes(Encoding.Default.GetBytes("Angelica File Package, Perfect World."));
            byte[] nuller = new byte[215];
            stream.WriteBytes(nuller);
            stream.WriteInt32(stream.key.ASIG_2);
            stream.WriteInt32(Table.Count);
            stream.WriteInt16(2);
            stream.WriteInt16(2);
            stream.Seek(4, SeekOrigin.Begin);
            stream.WriteUInt32((uint)stream.GetLenght());
            stream.Dispose();
            ProgressValue = 0;
            ProgressText = "Архив успешно запакован";
        }

        public void _UnpackCup(string path)
        {
            string dir = $"{path}.files";
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            PCKStream stream = new PCKStream(path);
            PCKFileEntry[] files = ReadFileTable(stream);
            PCKFileEntry[] x_files = files.Where(x => x.Path.EndsWith(".inc")).ToArray();
            ProgressText =  $"Парсим X-V.inc файлы: {x_files.Length}";
            List<string> files_path = new List<string>();
            foreach (PCKFileEntry entry in x_files)
            {
                StreamReader sr = new StreamReader(new MemoryStream(ReadFile(stream, entry)));
                string root_path = "";
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (line.StartsWith("!") || line.StartsWith("+"))
                    {
                        string file_path = line.Split(' ')[1].Replace("/", "\\");
                        if (file_path.StartsWith("\\"))
                        {
                            files_path.Add(file_path);
                            root_path = file_path.Replace($"\\{file_path.Split('\\').Last()}", "");
                        }
                        else
                        {
                            files_path.Add(Path.Combine(root_path, file_path));
                        }
                    }
                }
            }
            ProgressMax = files.Length;
            foreach (PCKFileEntry entry in files)
            {
                var p = files_path.Where(x => entry.Path.ToLower().Contains(x.ToLower())).ToList();
                if (p.Count > 0)
                {
                    BinaryReader br = new BinaryReader(new MemoryStream(ReadFile(stream, entry)));
                    int size = br.ReadInt32();
                    byte[] bytes = PCKZlib.Decompress(br.ReadBytes(entry.Size - 4), size);
                    string file_path = ParseBase64Path(dir, p.First());
                    ProgressText = $"Распаковка {ProgressValue}/{files.Length}: {file_path}";
                    if (!Directory.Exists(Path.GetDirectoryName(file_path)))
                        Directory.CreateDirectory(Path.GetDirectoryName(file_path));
                    File.WriteAllBytes(file_path, bytes);
                }
                ++ProgressValue;
            }
            stream.Dispose();
            ProgressValue = 0;
            ProgressText = "Архив успешно распакован";
        }

        public string ParseBase64Path(string root_path, string path)
        {
            string output = root_path;
            string[] split = path.Split(char.Parse("\\"));
            foreach (string str in split)
            {
                try
                {
                    output += $"\\{Encoding.GetEncoding(936).GetString(Convert.FromBase64String(str))}";
                }
                catch
                {
                    output += str;
                }
            }
            return output;
        }

        public byte[] ReadFile(PCKStream stream, PCKFileEntry file)
        {
            stream.Seek(file.Offset, SeekOrigin.Begin);
            byte[] bytes = stream.ReadBytes(file.CompressedSize);
            return file.CompressedSize < file.Size ? PCKZlib.Decompress(bytes, file.Size) : bytes;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
