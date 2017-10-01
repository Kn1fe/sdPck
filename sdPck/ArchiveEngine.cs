using Amib.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using WPFLocalizeExtension.Extensions;

namespace sdPck
{
    public delegate void CloseAfterFinish();

    public class ArchiveEngine : INotifyPropertyChanged
    {
        public event CloseAfterFinish CloseOnFinish;

        const int ProcessorsFactor = 8;
        private SmartThreadPool threadPool = new SmartThreadPool(30000, Environment.ProcessorCount * ProcessorsFactor);
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
            get => _CompressionLevel;
            set
            {
                if (_CompressionLevel != value)
                {
                    _CompressionLevel = value;
                    OnPropertyChanged("CompressionLevel");
                }
            }
        }
        private System.Timers.Timer timers = new System.Timers.Timer(7000);
        private CountdownEvent events = new CountdownEvent(0);

        public ArchiveEngine()
        {
            timers.Elapsed += (a, b) =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            };
            timers.Start();
        }

        public int GetFilesCount(PCKStream stream)
        {
            stream.Seek(-8, SeekOrigin.End);
            return stream.ReadInt32();
        }

        public PCKFileEntry[] ReadFileTable(PCKStream stream)
        {
            ProgressText = LocExtension.GetLocalizedValue<string>("ReadingFileTable");
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
            return entrys;
        }

        public IEnumerable<PCKFileEntry> YieldReadFileTable(PCKStream stream)
        {
            stream.Seek(-8, SeekOrigin.End);
            int FilesCount = stream.ReadInt32();
            ProgressMax = FilesCount;
            stream.Seek(-272, SeekOrigin.End);
            long FileTableOffset = (long)((ulong)((uint)(stream.ReadUInt32() ^ (ulong)stream.key.KEY_1)));
            stream.Seek(FileTableOffset, SeekOrigin.Begin);
            BinaryReader TableStream = new BinaryReader(new MemoryStream(stream.ReadBytes((int)(stream.GetLenght() - FileTableOffset - 280))));
            for (int i = 0; i < FilesCount; ++i)
            {
                int EntrySize = TableStream.ReadInt32() ^ stream.key.KEY_1;
                TableStream.ReadInt32();
                yield return new PCKFileEntry(TableStream.ReadBytes(EntrySize));
            }
        }

        public void Unpack(string path)
        {
            new Thread(() =>
            {
                try
                {
                    _Unpack(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\n\n{ex.Source}\n\n{ex.StackTrace}", "ОШИБКА", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }).Start();
        }

        public void Compress(string path)
        {
            new Thread(() =>
            {
                try
                {
                    _Compress(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\n\n{ex.Source}\n\n{ex.StackTrace}", "ОШИБКА", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }).Start();
        }

        public void UnpackCup(string path)
        {
            new Thread(() =>
            {
                try
                {
                    _UnpackCup(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\n\n{ex.Source}\n\n{ex.StackTrace}", "ОШИБКА", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }).Start();
        }

        private void _Unpack(string path)
        {
            string dir = $"{path}.files";
            if (Directory.Exists(dir))
            {
                ProgressText = LocExtension.GetLocalizedValue<string>("RemoveExitstsDirectory");
                Directory.Delete(dir, true);
            }
            Directory.CreateDirectory(dir);
            PCKStream stream = new PCKStream(path);
            events = new CountdownEvent(GetFilesCount(stream));
            IEnumerator<PCKFileEntry> enumerator = YieldReadFileTable(stream).GetEnumerator();
            while (enumerator.MoveNext())
            {
                string p = Path.Combine(dir, Path.GetDirectoryName(enumerator.Current.Path));
                if (!Directory.Exists(p))
                {
                    Directory.CreateDirectory(p);
                }
                ProgressText = $"{LocExtension.GetLocalizedValue<string>("Unpacking")} {ProgressValue}/{ProgressMax}: {enumerator.Current.Path}";
                stream.Seek(enumerator.Current.Offset, SeekOrigin.Begin);
                byte[] file = stream.ReadBytes(enumerator.Current.CompressedSize);
                threadPool.QueueWorkItem(x => {
                    byte[] buffer = (x as object[])[0] as byte[];
                    PCKFileEntry entry = (x as object[])[1] as PCKFileEntry;
                    File.WriteAllBytes(Path.Combine(dir, entry.Path), buffer.Length < entry.Size ? PCKZlib.Decompress(buffer, entry.Size) : buffer);
                    events.Signal();
                }, new object[] { file, enumerator.Current });
                while (threadPool.CurrentWorkItemsCount > Environment.ProcessorCount * ProcessorsFactor)
                    Thread.Sleep(100);
                ++ProgressValue;
            }
            ProgressText = LocExtension.GetLocalizedValue<string>("WaitThreads");
            events.Wait();
            stream.Dispose();
            ProgressValue = 0;
            ProgressText = LocExtension.GetLocalizedValue<string>("Ready");
            CloseOnFinish?.Invoke();
        }

        public void _Compress(string dir)
        {
            string pck = dir.Replace(".files", "");
            if (File.Exists(pck))
                File.Delete(pck);
            if (File.Exists(pck.Replace(".pck", ".pkx")))
                File.Delete(pck.Replace(".pck", ".pkx"));
            ProgressText = LocExtension.GetLocalizedValue<string>("FileList");
            string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            PCKStream stream = new PCKStream(pck);
            stream.WriteInt32(stream.key.FSIG_1);
            stream.WriteInt32(0);
            stream.WriteInt32(stream.key.FSIG_2);
            ProgressMax = files.Length;
            MemoryStream FileTable = new MemoryStream();
            events = new CountdownEvent(files.Length);
            for (ProgressValue = 0; ProgressValue < ProgressMax; ++ProgressValue)
            {
                string file = files[ProgressValue].Replace(dir, "").Replace("/", "\\").Remove(0, 1);
                ProgressText = $"{LocExtension.GetLocalizedValue<string>("Compressing")} {ProgressValue}/{ProgressMax}: {file}";
                byte[] decompressed = File.ReadAllBytes(Path.Combine(dir, files[ProgressValue]));
                byte[] compressed = PCKZlib.Compress(decompressed, CompressionLevel);
                var entry = new PCKFileEntry()
                {
                    Path = file,
                    Offset = (uint)stream.Position,
                    Size = decompressed.Length,
                    CompressedSize = compressed.Length
                };
                stream.WriteBytes(compressed);
                threadPool.QueueWorkItem(x => {
                    PCKFileEntry e = x as PCKFileEntry;
                    byte[] buffer = e.Write(CompressionLevel);
                    lock (FileTable)
                    {
                        FileTable.Write(BitConverter.GetBytes(buffer.Length ^ stream.key.KEY_1), 0, 4);
                        FileTable.Write(BitConverter.GetBytes(buffer.Length ^ stream.key.KEY_2), 0, 4);
                        FileTable.Write(buffer, 0, buffer.Length);
                    }
                    events.Signal();
                }, entry);
            }
            events.Wait();
            long FileTableOffset = stream.Position;
            stream.WriteBytes(FileTable.ToArray());
            stream.WriteInt32(stream.key.ASIG_1);//4
            stream.WriteInt16(2);//2
            stream.WriteInt16(2);//2
            stream.WriteUInt32((uint)(FileTableOffset ^ stream.key.KEY_1));//4
            stream.WriteInt32(0);//4
            stream.WriteBytes(Encoding.Default.GetBytes("Angelica File Package, Perfect World."));//37
            byte[] nuller = new byte[215];
            stream.WriteBytes(nuller);//215 - 268
            stream.WriteInt32(stream.key.ASIG_2);//4
            stream.WriteInt32(files.Length);//4
            stream.WriteInt16(2);//2
            stream.WriteInt16(2);//2
            stream.Seek(4, SeekOrigin.Begin);
            stream.WriteUInt32((uint)stream.GetLenght());
            stream.Dispose();
            ProgressValue = 0;
            ProgressText = LocExtension.GetLocalizedValue<string>("Ready");
            CloseOnFinish?.Invoke();
        }

        public void _UnpackCup(string path)
        {
            string dir = $"{path}.files";
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            PCKStream stream = new PCKStream(path);
            var files = ReadFileTable(stream);
            PCKFileEntry[] x_files = files.Where(x => x.Path.EndsWith(".inc")).ToArray();
            ProgressText = $"{LocExtension.GetLocalizedValue<string>("ParseX")}: {x_files.Length}";
            List<string> files_path = new List<string>();
            foreach (PCKFileEntry entry in x_files)
            {
                StreamReader sr = new StreamReader(new MemoryStream(ReadFile(stream, entry)));
                string root_path = "";
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if ((line.StartsWith("!") || line.StartsWith("+")) && line.Contains(" "))
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
            events = new CountdownEvent(files.Length);
            foreach (PCKFileEntry entry in files)
            {
                var p = files_path.Where(x => entry.Path.ToLower().Contains(x.ToLower())).ToList();
                if (p.Count > 0)
                {
                    ProgressText = $"{LocExtension.GetLocalizedValue<string>("Unpacking")} {ProgressValue}/{ProgressMax}: {ParseBase64Path(dir, p.First())}";
                    byte[] file = ReadFile(stream, entry);
                    threadPool.QueueWorkItem(x => {
                        byte[] buffer = (x as object[])[0] as byte[];
                        BinaryReader br = new BinaryReader(new MemoryStream(buffer));
                        int size = br.ReadInt32();
                        string file_path = ParseBase64Path(dir, (x as object[])[1].ToString());
                        if (!Directory.Exists(Path.GetDirectoryName(file_path)))
                            Directory.CreateDirectory(Path.GetDirectoryName(file_path));
                        File.WriteAllBytes(file_path, PCKZlib.Decompress(br.ReadBytes(buffer.Length - 4), size));
                        events.Signal();
                    }, new object[] { file, p.First() });
                    while (threadPool.CurrentWorkItemsCount > Environment.ProcessorCount * ProcessorsFactor)
                        Thread.Sleep(100);
                }
                else
                    events.Signal();
                ++ProgressValue;
            }
            events.Wait();
            stream.Dispose();
            ProgressValue = 0;
            ProgressText = LocExtension.GetLocalizedValue<string>("Ready");
            CloseOnFinish?.Invoke();
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
