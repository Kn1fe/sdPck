using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace sdPck
{
    public partial class MainWindow : MetroWindow
    {
        public ArchiveEngine archive = new ArchiveEngine();
        public string[] startup_param = new string[0];

        public MainWindow()
        {
            InitializeComponent();
            startup_param = ((App)Application.Current).startup_param;
            DataContext = archive;
            if (startup_param?.Length > 0)
            {
                if (File.Exists(startup_param[0]))
                {
                    if (Path.GetExtension(startup_param[0]) == ".cup")
                    {
                        archive.UnpackCup(startup_param[0]);
                    }
                    else
                    {
                        archive.Unpack(startup_param[0]);
                    }
                }
                if (Directory.Exists(startup_param[0]))
                    archive.Compress(startup_param[0]);
            }
        }

        private void Unpack(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                archive.Unpack(ofd.FileName);
            }
        }

        private void UnpackCup(object sender, RoutedEventArgs e)
        {

        }

        private void Compress(object sender, RoutedEventArgs e)
        {
            WinForms.FolderBrowserDialog fbd = new WinForms.FolderBrowserDialog();
            if (fbd.ShowDialog() == WinForms.DialogResult.OK)
            {
                archive.Compress(fbd.SelectedPath);
            }
        }

        private void Regedit(object sender, RoutedEventArgs e)
        {
            try
            {
                // Directory
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\sd_pck", false);
                Registry.ClassesRoot.DeleteSubKeyTree(".pck", false);
                Registry.ClassesRoot.DeleteSubKeyTree(".cup", false);
                Registry.ClassesRoot.DeleteSubKeyTree("sdPck", false);
                using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell").CreateSubKey("sd_pck", RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    key.SetValue("Icon", $"{System.Reflection.Assembly.GetExecutingAssembly().Location},0");
                    key.SetValue("MUIVerb", "[sdPCK] Запаковать", RegistryValueKind.String);
                    using (var key1 = key.CreateSubKey("command"))
                    {
                        key1.SetValue(string.Empty, $"{System.Reflection.Assembly.GetExecutingAssembly().Location} \"%1\"", RegistryValueKind.ExpandString);
                    }
                }
                // PCK
                using (var key = Registry.ClassesRoot.CreateSubKey(".pck"))
                {
                    key.SetValue(string.Empty, $"sdPck", RegistryValueKind.String);
                }
                using (var key = Registry.ClassesRoot.CreateSubKey(".cup"))
                {
                    key.SetValue(string.Empty, $"sdPck", RegistryValueKind.String);
                }
                // CUP
                using (var key = Registry.ClassesRoot.CreateSubKey("sdPck"))
                {
                    key.SetValue(string.Empty, "Archive manager for Angelica Engine by Wanmei");
                    using (var key1 = key.CreateSubKey("DefaultIcon"))
                    {
                        key1.SetValue(string.Empty, $"{System.Reflection.Assembly.GetExecutingAssembly().Location},0");
                    }
                    key.CreateSubKey(@"Shell\Open\Command").SetValue(string.Empty, $"{System.Reflection.Assembly.GetExecutingAssembly().Location} \"%1\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Для изменения реестра нужно запустить программу от имени администратора");
                MessageBox.Show($"{ex.Message}\n\n{ex.Source}\n\n{ex.StackTrace}");
            }
        }
    }
}