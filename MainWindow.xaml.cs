using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DynamicNotch
{
    public class FileItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string IconSymbol { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        const byte VK_LWIN = 0x5B;
        const byte VK_V = 0x56;
        const byte VK_OEM_PERIOD = 0xBE;
        const int KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        public ObservableCollection<FileItem> StoredFiles { get; set; } = new ObservableCollection<FileItem>();
        private Point _dragStartPoint;
        private bool _isDraggingOver = false;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            StoredFiles.CollectionChanged += StoredFiles_CollectionChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
        }

        private void StoredFiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            EmptyText.Visibility = StoredFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CollapseNotch();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Topmost = true;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandNotch();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isDraggingOver)
            {
                CollapseNotch();
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            _isDraggingOver = true;
            ExpandNotch();
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            Point pt = e.GetPosition(this);
            if (pt.X <= 0 || pt.X >= this.ActualWidth - 1 || pt.Y <= 0 || pt.Y >= this.ActualHeight - 1)
            {
                _isDraggingOver = false;
                CollapseNotch();
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            _isDraggingOver = false;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (!StoredFiles.Any(f => f.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                    {
                        bool isDir = Directory.Exists(file);
                        string name = Path.GetFileName(file);
                        if (string.IsNullOrEmpty(name)) name = file;

                        string icon = "\xE7C3"; // Default Document
                        if (isDir)
                        {
                            icon = "\xE8B7"; // Folder
                        }
                        else
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            switch (ext)
                            {
                                case ".png": case ".jpg": case ".jpeg": case ".gif": case ".bmp": case ".webp": icon = "\xE8B9"; break; // Photo
                                case ".mp4": case ".mkv": case ".avi": case ".mov": icon = "\xE714"; break; // Video
                                case ".mp3": case ".wav": case ".flac": icon = "\xE8D6"; break; // Audio
                                case ".xlsx": case ".xls": case ".csv": icon = "\xE8F2"; break; // Table/Excel (List/Table icon)
                                case ".doc": case ".docx": case ".txt": icon = "\xE8A5"; break; // Document/Word
                                case ".pdf": icon = "\xEA90"; break; // PDF
                                case ".zip": case ".rar": case ".7z": icon = "\xE188"; break; // ZipFolder
                                case ".cs": case ".js": case ".py": case ".cpp": case ".html": case ".css": icon = "\xE943"; break; // Code
                            }
                        }

                        StoredFiles.Add(new FileItem
                        {
                            FilePath = file,
                            FileName = name,
                            IconSymbol = icon
                        });
                    }
                }
            }

            if (!this.IsMouseOver)
            {
                CollapseNotch();
            }
        }

        private void ExpandNotch()
        {
            this.Height = 350;
            this.Width = 600;
            NotchBorder.Height = double.NaN; // Auto-size to content
            NotchBorder.Width = 580;
            NotchBorder.CornerRadius = new CornerRadius(15);
            NotchBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 0)); // Show background (50% transparent)
            NotchContent.Visibility = Visibility.Visible;
            
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Width - this.Width) / 2;
            this.Top = 0; // Extremely top
        }

        private void CollapseNotch()
        {
            this.Height = 8; // Increased detection height slightly for ease
            this.Width = 600; // Increased detection width
            NotchBorder.Height = 8;
            NotchBorder.Width = 600;
            NotchBorder.CornerRadius = new CornerRadius(0);
            NotchBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)); // #01000000 (100% invisible but allows MouseEnter triggers)
            NotchContent.Visibility = Visibility.Collapsed;

            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Width - this.Width) / 2;
            this.Top = 0; // Extremely top
        }

        private void FileItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FileItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is FrameworkElement fe && fe.DataContext is FileItem item)
                    {
                        string[] files = new string[] { item.FilePath };
                        DataObject dragData = new DataObject(DataFormats.FileDrop, files);
                        DragDrop.DoDragDrop(fe, dragData, DragDropEffects.Copy);
                    }
                }
            }
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FileItem item)
            {
                StoredFiles.Remove(item);
            }
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            StoredFiles.Clear();
        }

        private void Snip_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); } catch { }
        }

        private void Clip_Click(object sender, RoutedEventArgs e)
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_V, 0, 0, 0);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_OEM_PERIOD, 0, 0, 0);
            keybd_event(VK_OEM_PERIOD, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}