﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MyDesktopOrganizer
{
    public partial class MainWindow : Window
    {
        public static List<MainWindow> OpenBoxes = new List<MainWindow>();
        public static double GlobalIconSize = 70;
        public static double GlobalOpacity = 0.8;
        public static double GlobalCornerRadius = 15;
        public static Color GlobalColor = Color.FromRgb(16, 16, 16);
        public static bool GlobalShowFileName = true;
        public static bool GlobalShowShortcutSymbol = true;

        public string BoxId { get; private set; }
        private static bool _areBoxesHidden = false;
        private bool isCollapsed = false;
        private double expandedHeight;
        private DateTime lastClick = DateTime.MinValue;
        public string StorageFolderPath { get; private set; }
        private Grid? selectedPanel = null;
        private double currentIconSize = 70;
        private Point dragStartPoint;
        private bool isTitleVisible = true;
        private double _dpiX = 1.0, _dpiY = 1.0;
        public bool IsLocked { get; private set; } = false;
        public bool ShowFileName { get; private set; } = true;
        public bool ShowShortcutSymbol { get; private set; } = true;
        private double _originalMinHeight = 150;
        public MainWindow(string? id = null, BoxData? data = null)
        {
            InitializeComponent();
            OpenBoxes.Add(this);
            BoxId = id ?? Guid.NewGuid().ToString();
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            StorageFolderPath = Path.Combine(docPath, "MyDesktopOrganizer", "Box_" + BoxId);
            Directory.CreateDirectory(StorageFolderPath);
            TryLoadAppIcon(this);
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc);
            }

            this.Loaded += (s, e) => {
                expandedHeight = this.Height;
                _originalMinHeight = this.MinHeight;
                RefreshFiles();
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    _dpiX = source.CompositionTarget.TransformToDevice.M11;
                    _dpiY = source.CompositionTarget.TransformToDevice.M22;
                }
                if (data != null)
                {
                    this.Left = data.Left;
                    this.Top = data.Top;
                    this.Width = data.Width;
                    this.Height = data.Height;
                    this.BoxTitle.Text = data.Title;
                    this.isTitleVisible = data.IsTitleVisible;
                    this.BoxTitle.Visibility = isTitleVisible ? Visibility.Visible : Visibility.Collapsed;
                    SetBackgroundColor(data.Color);
                    
                    this.IsLocked = data.IsLocked;
                    if (this.IsLocked)
                    {
                        this.ResizeMode = ResizeMode.NoResize;
                        this.LockMenuItem.IsChecked = true;
                    }
                    this.ShowFileName = data.ShowFileName;
                    this.ShowShortcutSymbol = data.ShowShortcutSymbol;
                    SetIconSize(data.IconSize > 0 ? data.IconSize : GlobalIconSize);
                    SetCornerRadius(data.CornerRadius >= 0 ? data.CornerRadius : GlobalCornerRadius);
                }
                else
                {
                    SetIconSize(GlobalIconSize);
                    SetCornerRadius(GlobalCornerRadius);
                    SetShowFileName(GlobalShowFileName);
                    SetShowShortcutSymbol(GlobalShowShortcutSymbol);
                    Color c = GlobalColor;
                    c.A = (byte)(GlobalOpacity * 255);
                    SetBackgroundColor(c);
                }
            };
        }
        public static void TryLoadAppIcon(Window window)
        {
            try {
                var icon = LoadImageFromFile("MyDesktopOrganizer.ico");
                if (icon != null) window.Icon = icon;
                else window.Icon = BitmapFrame.Create(new Uri("pack://application:,,,/MyDesktopOrganizer.ico"));
            } 
            catch 
            {
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowInteropHelper wndHelper = new WindowInteropHelper(this);
            int exStyle = (int)GetWindowLong(wndHelper.Handle, GWL_EXSTYLE);
            SetWindowLong(wndHelper.Handle, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_TOOLWINDOW));
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        private void SnapToEdges()
        {
            double snapDist = 15;
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            double currentLeft = this.Left;
            double currentTop = this.Top;
            double currentWidth = this.Width;
            double currentHeight = this.Height;

            double bestLeft = currentLeft;
            double bestTop = currentTop;
            double minXDist = snapDist;
            double minYDist = snapDist;

            if (Math.Abs(currentLeft) < minXDist) { minXDist = Math.Abs(currentLeft); bestLeft = 0; }
            if (Math.Abs(currentLeft + currentWidth - screenW) < minXDist) { minXDist = Math.Abs(currentLeft + currentWidth - screenW); bestLeft = screenW - currentWidth; }

            if (Math.Abs(currentTop) < minYDist) { minYDist = Math.Abs(currentTop); bestTop = 0; }
            if (Math.Abs(currentTop + currentHeight - screenH) < minYDist) { minYDist = Math.Abs(currentTop + currentHeight - screenH); bestTop = screenH - currentHeight; }

            foreach (var box in OpenBoxes)
            {
                if (box == this) continue;

                if (Math.Abs(currentLeft + currentWidth - box.Left) < minXDist) { minXDist = Math.Abs(currentLeft + currentWidth - box.Left); bestLeft = box.Left - currentWidth; }
                if (Math.Abs(currentLeft - (box.Left + box.Width)) < minXDist) { minXDist = Math.Abs(currentLeft - (box.Left + box.Width)); bestLeft = box.Left + box.Width; }
                if (Math.Abs(currentLeft - box.Left) < minXDist) { minXDist = Math.Abs(currentLeft - box.Left); bestLeft = box.Left; }
                if (Math.Abs(currentLeft + currentWidth - (box.Left + box.Width)) < minXDist) { minXDist = Math.Abs(currentLeft + currentWidth - (box.Left + box.Width)); bestLeft = box.Left + box.Width - currentWidth; }

                if (Math.Abs(currentTop + currentHeight - box.Top) < minYDist) { minYDist = Math.Abs(currentTop + currentHeight - box.Top); bestTop = box.Top - currentHeight; }
                if (Math.Abs(currentTop - (box.Top + box.Height)) < minYDist) { minYDist = Math.Abs(currentTop - (box.Top + box.Height)); bestTop = box.Top + box.Height; }
                if (Math.Abs(currentTop - box.Top) < minYDist) { minYDist = Math.Abs(currentTop - box.Top); bestTop = box.Top; }
                if (Math.Abs(currentTop + currentHeight - (box.Top + box.Height)) < minYDist) { minYDist = Math.Abs(currentTop + currentHeight - (box.Top + box.Height)); bestTop = box.Top + box.Height - currentHeight; }
            }

            this.Left = bestLeft;
            this.Top = bestTop;
        }

        private void PreventOverlap()
        {
            for (int i = 0; i < 3; i++)
            {
                bool collisionFound = false;
                Rect current = new Rect(this.Left, this.Top, this.Width, this.Height);
                
                foreach (var box in OpenBoxes)
                {
                    if (box == this) continue;
                    Rect other = new Rect(box.Left, box.Top, box.Width, box.Height);

                    if (current.IntersectsWith(other))
                    {
                        collisionFound = true;
                        double overlapLeft = (current.Left + current.Width) - other.Left;
                        double overlapRight = (other.Left + other.Width) - current.Left;
                        double overlapTop = (current.Top + current.Height) - other.Top;
                        double overlapBottom = (other.Top + other.Height) - current.Top;
                        double minX = overlapLeft < overlapRight ? -overlapLeft : overlapRight;
                        double minY = overlapTop < overlapBottom ? -overlapTop : overlapBottom;

                        if (Math.Abs(minX) < Math.Abs(minY))
                            this.Left += minX;
                        else
                            this.Top += minY;
                        
                        current = new Rect(this.Left, this.Top, this.Width, this.Height);
                    }
                }
                if (!collisionFound) break;
            }
        }
        public void SetCornerRadius(double radius)
        {
            MainBorder.CornerRadius = new CornerRadius(radius);
        }

        public void SetBackgroundColor(string hexColor)
        {
            try 
            { 
                if (new BrushConverter().ConvertFromString(hexColor) is SolidColorBrush brush)
                    MainBorder.Background = brush; 
            } catch { }
        }

        public void SetBackgroundColor(Color color)
        {
            MainBorder.Background = new SolidColorBrush(color);
        }

        public void SetShowFileName(bool show)
        {
            ShowFileName = show;
            RefreshFiles();
        }

        public void SetShowShortcutSymbol(bool show)
        {
            ShowShortcutSymbol = show;
            RefreshFiles();
        }

        public void SetIconSize(double size)
        {
            currentIconSize = size;
            if (FilePanel != null)
            {
                FilePanel.ItemWidth = size + 10;
                double textHeight = ShowFileName ? 40 : 0;
                FilePanel.ItemHeight = size + textHeight + 10;
                RefreshFiles(); 
            }
        }

        public void SetWindowOpacity(double opacity)
        {
            if (MainBorder.Background is SolidColorBrush brush)
            {
                Color c = brush.Color;
                c.A = (byte)(opacity * 255);
                MainBorder.Background = new SolidColorBrush(c);
            }
        }
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            base.OnStateChanged(e);
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if ((DateTime.Now - lastClick).TotalMilliseconds < 300)
                {
                    ToggleCollapse();
                }
                lastClick = DateTime.Now;
                
                if (IsLocked) return;
                
                try { this.DragMove(); } catch { }
                SnapToEdges();
                PreventOverlap(); 
            }
        }
        private void ToggleCollapse()
        {
            if (isCollapsed)
            {
                AnimateHeight(expandedHeight, () => { this.MinHeight = _originalMinHeight; });
                ContentArea.Visibility = Visibility.Visible;
            }
            else
            {
                if (this.Height > 40) expandedHeight = this.Height;
                this.MinHeight = 0;
                AnimateHeight(30);
                ContentArea.Visibility = Visibility.Collapsed;
            }
            isCollapsed = !isCollapsed;
        }
        private void AnimateHeight(double targetHeight, Action? onCompleted = null)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase()
            };
            
            if (onCompleted != null)
            {
                animation.Completed += (s, e) => onCompleted();
            }

            this.BeginAnimation(Window.HeightProperty, animation);
        }

        private void Header_MouseUp(object sender, MouseButtonEventArgs e)
        {
        }
        private void RefreshFiles()
        {
            FilePanel.Children.Clear();
            FilePanel.ItemWidth = currentIconSize + 10;
            double textHeight = ShowFileName ? 40 : 0;
            FilePanel.ItemHeight = currentIconSize + textHeight + 10;

            if (Directory.Exists(StorageFolderPath))
            {
                foreach (var dir in Directory.GetDirectories(StorageFolderPath))
                {
                    AddFileIcon(dir);
                }
                foreach (var file in Directory.GetFiles(StorageFolderPath))
                {
                    AddFileIcon(file);
                }
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(file);
                            string destPath = Path.Combine(StorageFolderPath, fileName);

                            if (Directory.Exists(file))
                            {
                                int count = 1;
                                while (Directory.Exists(destPath) || File.Exists(destPath))
                                {
                                    destPath = Path.Combine(StorageFolderPath, $"{fileName} ({count++})");
                                }
                                Directory.Move(file, destPath);
                            }
                            else
                            {
                                int count = 1;
                                while (File.Exists(destPath) || Directory.Exists(destPath))
                                {
                                    string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                                    string ext = Path.GetExtension(fileName);
                                    destPath = Path.Combine(StorageFolderPath, $"{nameNoExt} ({count++}){ext}");
                                }
                                File.Move(file, destPath);
                            }
                        }
                        catch {  }
                    }
                    RefreshFiles();
                }
            }
        }

        public void RestoreFilesToDesktop()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (Directory.Exists(StorageFolderPath))
            {
                foreach (var dir in Directory.GetDirectories(StorageFolderPath))
                {
                    try
                    {
                        string dirName = Path.GetFileName(dir);
                        string destPath = Path.Combine(desktopPath, dirName);

                        int count = 1;
                        while (Directory.Exists(destPath) || File.Exists(destPath))
                        {
                            destPath = Path.Combine(desktopPath, $"{dirName} ({count++})");
                        }
                        Directory.Move(dir, destPath);
                    }
                    catch { }
                }
                foreach (var file in Directory.GetFiles(StorageFolderPath))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(desktopPath, fileName);

                        int count = 1;
                        while (File.Exists(destPath) || Directory.Exists(destPath))
                        {
                            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                            string ext = Path.GetExtension(fileName);
                            destPath = Path.Combine(desktopPath, $"{nameNoExt} ({count++}){ext}");
                        }
                        File.Move(file, destPath);
                    }
                    catch { }
                }
            }
        }

        private void AddFileIcon(string filePath)
        {
            Grid panel = new Grid
            { 
                Margin = new Thickness(5), 
                Width = currentIconSize, 
                Height = currentIconSize + 40,
                Background = Brushes.Transparent,
                Tag = filePath,
                Cursor = Cursors.Hand
            };
            
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(currentIconSize) });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        
            panel.MouseLeftButtonDown += (s, e) => 
            {

                if (selectedPanel != null) selectedPanel.Background = Brushes.Transparent;
                selectedPanel = (Grid)s; 
                selectedPanel.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)); // Blanco sutil (Dark Mode)
                dragStartPoint = e.GetPosition(null);
                if (e.ClickCount == 2)
                {
                    try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); } catch { }
                }
            };

            panel.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Point mousePos = e.GetPosition(null);
                    Vector diff = dragStartPoint - mousePos;

                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        string[] files = { filePath };
                        DataObject data = new DataObject(DataFormats.FileDrop, files);
                        DragDrop.DoDragDrop(panel, data, DragDropEffects.Move);
                        RefreshFiles();
                    }
                }
            };
            Image iconImage = new Image 
            { 
                Width = currentIconSize * 0.65, 
                Height = currentIconSize * 0.65, 
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.HighQuality);
            iconImage.Source = GetIcon(filePath, ShowShortcutSymbol);
            
            Grid.SetRow(iconImage, 0);
            panel.Children.Add(iconImage);
            
            if (ShowFileName)
            {
                TextBlock nameBlock = new TextBlock
                {
                    Text = Directory.Exists(filePath) ? Path.GetFileName(filePath) : Path.GetFileNameWithoutExtension(filePath),
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    FontSize = currentIconSize * 0.18,
                    Height = 38,
                    Padding = new Thickness(2,0,2,0),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(nameBlock, 1);
                panel.Children.Add(nameBlock);
            }
            
            FilePanel.Children.Add(panel);
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            BoxTitle.IsReadOnly = false;
            BoxTitle.IsHitTestVisible = true;
            BoxTitle.Focus();
            BoxTitle.SelectAll();
        }

        private void BoxTitle_LostFocus(object sender, RoutedEventArgs e)
        {
            BoxTitle.IsReadOnly = true;
            BoxTitle.IsHitTestVisible = false;
        }

        private void BoxTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BoxTitle.IsReadOnly = true;
                BoxTitle.IsHitTestVisible = false;
                Keyboard.ClearFocus();
            }
        }

        private void ToggleTitle_Click(object sender, RoutedEventArgs e)
        {
            isTitleVisible = !isTitleVisible;
            BoxTitle.Visibility = isTitleVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleLock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                IsLocked = item.IsChecked;
                ResizeMode = IsLocked ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
            }
        }

        private void ChangeColor_Click(object sender, RoutedEventArgs e)
        {
            string[] colors = { "#F2101010", "#F2003366", "#F2004400", "#F2440000", "#F2330033" };
            var rnd = new Random().Next(colors.Length);
            SetBackgroundColor(colors[rnd]);
        }
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsWindow.Instance != null)
            {
                SettingsWindow.Instance.Activate();
                if (SettingsWindow.Instance.WindowState == WindowState.Minimized)
                    SettingsWindow.Instance.WindowState = WindowState.Normal;
                return;
            }
            SettingsWindow settings = new SettingsWindow();
            settings.Show();
        }

        private void DeleteBox_Click(object sender, RoutedEventArgs e)
        {
            if (ModernMessageBox.Show("¿Eliminar esta caja?\nLos archivos volverán al escritorio.", "Confirmar Eliminación", true))
            {
                RestoreFilesToDesktop();
                OpenBoxes.Remove(this);
                SaveLayout();
                this.Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RestoreFilesToDesktop();
            this.Close();
        }

        private ImageSource? GetIcon(string path, bool showOverlay)
        {
            IntPtr hIcon = IntPtr.Zero;
            if (!showOverlay)
            {
                string targetPath = path;
                int iconIndex = 0;
                if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                        {
                            object? shellObj = Activator.CreateInstance(shellType);
                            if (shellObj != null)
                            {
                                dynamic shell = shellObj;
                                dynamic shortcut = shell.CreateShortcut(path);
                                string iconLoc = shortcut.IconLocation;
                                
                                if (!string.IsNullOrEmpty(iconLoc) && !iconLoc.StartsWith(","))
                                {
                                    var parts = iconLoc.Split(',');
                                    targetPath = parts[0];
                                    if (parts.Length > 1) int.TryParse(parts[1], out iconIndex);
                                }
                                else
                                {
                                    targetPath = shortcut.TargetPath;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (targetPath == path)
                {
                    SHFILEINFO shinfoLoc = new SHFILEINFO();
                    SHGetFileInfo(path, 0, ref shinfoLoc, (uint)Marshal.SizeOf(shinfoLoc), 0x1000); 
                    if (!string.IsNullOrEmpty(shinfoLoc.szDisplayName))
                    {
                        targetPath = shinfoLoc.szDisplayName;
                        iconIndex = shinfoLoc.iIcon;
                    }
                }

                if (SHDefExtractIcon(targetPath, iconIndex, 0, out hIcon, out _, 256) == 0)
                {
                    if (hIcon != IntPtr.Zero)
                    {
                        ImageSource icon = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        DestroyIcon(hIcon);
                    return icon;
                    }
                }
            }
            SHFILEINFO shinfo = new SHFILEINFO();
            SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), 0x100); 
            
            if (shinfo.hIcon != IntPtr.Zero)
            {
                ImageSource icon = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(shinfo.hIcon);
                return icon;
            }

            return null;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);
        [DllImport("shell32.dll")] private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
        [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static DateTime _lastDesktopClick = DateTime.MinValue;

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                if (IsClickOnDesktop(hookStruct.pt))
                {
                    if ((DateTime.Now - _lastDesktopClick).TotalMilliseconds < 250) 
                    {
                        ToggleDesktopIcons();
                        _lastDesktopClick = DateTime.MinValue;
                    }
                    else
                    {
                        _lastDesktopClick = DateTime.Now;
                    }
                }
            }
            else if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE && _areBoxesHidden)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                Point mousePt = new Point(hookStruct.pt.x, hookStruct.pt.y);

                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var box in OpenBoxes)
                    {

                        Rect boxRect = new Rect(box.Left * box._dpiX, box.Top * box._dpiY, box.Width * box._dpiX, box.Height * box._dpiY);
                        
                        if (boxRect.Contains(mousePt))
                        {
                            if (box.Visibility != Visibility.Visible || box.Opacity < 1.0)
                            {
                                box.BeginAnimation(UIElement.OpacityProperty, null);
                                box.Opacity = 1.0;
                                box.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            if (box.Visibility == Visibility.Visible)
                            {
                                box.BeginAnimation(UIElement.OpacityProperty, null);
                                box.Visibility = Visibility.Hidden;
                                box.Opacity = 0.0;
                            }
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Input);
            }
            
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static bool IsClickOnDesktop(POINT pt)
        {
            IntPtr hWnd = WindowFromPoint(pt);
            if (hWnd == IntPtr.Zero) return false;

            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string name = className.ToString();
            
            return name == "SysListView32" || name == "Progman" || name == "WorkerW" || name == "SHELLDLL_DefView";
        }

        private static void ToggleDesktopIcons()
        {
            IntPtr hWndListView = GetDesktopListView();
            if (hWndListView != IntPtr.Zero)
            {
                bool isVisible = IsWindowVisible(hWndListView);
                
                ShowWindow(hWndListView, isVisible ? 0 : 5);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _areBoxesHidden = isVisible;
                    bool show = !_areBoxesHidden;
                    foreach (var box in OpenBoxes)
                    {
                        if (show) box.FadeIn();
                        else box.FadeOut();
                    }
                });
            }
            else
            {
                ToggleAllBoxesVisibility();
            }
        }

        private static IntPtr GetDesktopListView()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr shellDll = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (shellDll == IntPtr.Zero)
            {
                EnumWindows((hwnd, lParam) =>
                {
                    IntPtr shell = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shell != IntPtr.Zero)
                    {
                        shellDll = shell;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }

            return FindWindowEx(shellDll, IntPtr.Zero, "SysListView32", null);
        }

        private static void ToggleAllBoxesVisibility()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _areBoxesHidden = !_areBoxesHidden;
                bool show = !_areBoxesHidden;
                foreach (var box in OpenBoxes)
                {
                    if (show) box.FadeIn();
                    else box.FadeOut();
                }
            });
        }

        public void FadeIn()
        {
            if (this.Visibility == Visibility.Visible && this.Opacity >= 1.0) return;
            
            this.Visibility = Visibility.Visible;
            DoubleAnimation anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(250));
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public void FadeOut()
        {
            if (this.Visibility != Visibility.Visible) return;

            DoubleAnimation anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250));
            anim.Completed += (s, e) => this.Visibility = Visibility.Hidden;
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public static void CleanupHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_MOUSEMOVE = 0x0200;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string? windowTitle);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private const int WS_EX_LAYERED = 0x80000;
        private const int LWA_ALPHA = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyDesktopOrganizer", "layout.json");

        public static void SaveLayout()
        {
            try
            {
                var list = new List<BoxData>();
                foreach (var box in OpenBoxes)
                {
                    list.Add(new BoxData
                    {
                        Id = box.BoxId,
                        Title = box.BoxTitle.Text,
                        Left = box.Left,
                        Top = box.Top,
                        Width = box.Width,
                        Height = box.Height,
                        Color = box.MainBorder.Background.ToString(),
                        IsTitleVisible = box.BoxTitle.Visibility == Visibility.Visible,
                        IsLocked = box.IsLocked,
                        ShowFileName = box.ShowFileName,
                        ShowShortcutSymbol = box.ShowShortcutSymbol,
                        IconSize = box.currentIconSize,
                        CornerRadius = box.MainBorder.CornerRadius.TopLeft
                    });
                }
                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        public static void LoadLayout()
        {
            if (!File.Exists(ConfigPath)) return;
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var list = JsonSerializer.Deserialize<List<BoxData>>(json);
                if (list != null)
                {
                    foreach (var data in list)
                    {
                        var box = new MainWindow(data.Id, data);
                        box.Show();
                    }
                    if (list.Count > 0)
                    {
                        var first = list[0];
                        GlobalIconSize = first.IconSize > 0 ? first.IconSize : GlobalIconSize;
                        GlobalCornerRadius = first.CornerRadius >= 0 ? first.CornerRadius : GlobalCornerRadius;
                        GlobalShowFileName = first.ShowFileName;
                        GlobalShowShortcutSymbol = first.ShowShortcutSymbol;
                        
                        try {
                            if (new BrushConverter().ConvertFromString(first.Color) is SolidColorBrush brush) {
                                GlobalColor = brush.Color;
                                GlobalColor.A = 255; // Reset alpha base
                                GlobalOpacity = brush.Color.A / 255.0;
                            }
                        } catch {}
                    }
                }
            }
            catch { }
        }
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
    }
    public class SettingsWindow : Window
    {
        public static SettingsWindow? Instance { get; private set; }

        public SettingsWindow()
        {
            Instance = this;
            this.Closed += (s, e) => Instance = null;

            Title = "Opciones";
            Width = 500; Height = 750;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize; 
            WindowStyle = WindowStyle.None; 
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            
            MainWindow.TryLoadAppIcon(this);

            Border mainBorder = new Border 
            { 
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 24)), 
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            mainBorder.Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 5, Opacity = 0.5, Color = Colors.Black };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // Barra Título
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Contenido

            Border titleBar = new Border { Background = Brushes.Transparent };
            titleBar.MouseLeftButtonDown += (s, e) => { try { this.DragMove(); } catch { } };
            
            Grid titleGrid = new Grid();
            
            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15,0,0,0) };
            Image appIcon = new Image { Width = 20, Height = 20, Margin = new Thickness(0,0,10,0) };
            var loadedIcon = MainWindow.LoadImageFromFile("MyDesktopOrganizer.ico");
            if (loadedIcon != null) appIcon.Source = loadedIcon;
            titlePanel.Children.Add(appIcon);
            titlePanel.Children.Add(new TextBlock { Text = "MyDesktopOrganizer", Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            titleGrid.Children.Add(titlePanel);
            
            StackPanel winBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,0,5,0) };
            
            Button minBtn = new Button { Content = "—", Width = 45, Height = 35, Background = Brushes.Transparent, Foreground = Brushes.Gray, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center };
            minBtn.Click += (s, e) => this.WindowState = WindowState.Minimized;
            minBtn.Template = GetBtnTemplate(Brushes.Transparent, new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 0);
            winBtns.Children.Add(minBtn);

            Button closeBtn = new Button { Content = "✕", Width = 45, Height = 35, Background = Brushes.Transparent, Foreground = Brushes.Gray, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 14 };
            closeBtn.Click += (s, e) => this.Close();
            closeBtn.Template = GetBtnTemplate(Brushes.Transparent, new SolidColorBrush(Color.FromRgb(196, 43, 28)), 0); 
            winBtns.Children.Add(closeBtn);
            
            titleGrid.Children.Add(winBtns);
            
            titleBar.Child = titleGrid;
            grid.Children.Add(titleBar);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0,0,0,15) };
            StackPanel mainStack = new StackPanel { Margin = new Thickness(25) };
            scroll.Content = mainStack;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            mainStack.Children.Add(CreateSection("General", panel => {
                CheckBox chkAutoStart = CreateCheckBox("Iniciar con Windows", IsAutoStartEnabled());
                chkAutoStart.Checked += (s, e) => SetAutoStart(true);
                chkAutoStart.Unchecked += (s, e) => SetAutoStart(false);
                panel.Children.Add(chkAutoStart);
            }));

            mainStack.Children.Add(CreateSection("Gestión de Cajas", panel => {
                Button btnNew = CreateButton("➕  Crear Nueva Caja", "#007ACC");
                btnNew.Click += (s, e) => { 
                    try {
                        new MainWindow().Show(); 
                    } catch (Exception ex) {
                        MessageBox.Show($"Error crítico al crear caja: {ex.Message}\nPosible causa: El archivo de icono no se encuentra.");
                    }
                };
                panel.Children.Add(btnNew);

                Button btnPanic = CreateButton("⚠️  Restaurar todo al Escritorio", "#3E3E42");
                btnPanic.Click += (s, e) => {
                    if (ModernMessageBox.Show("¿Estás seguro de que quieres restaurar TODOS los iconos al escritorio y cerrar las cajas?", "Confirmar Restauración", true))
                    {
                        var boxes = new List<MainWindow>(MainWindow.OpenBoxes);
                        foreach (var box in boxes) { box.RestoreFilesToDesktop(); box.Close(); }
                        ModernMessageBox.Show("Todos los iconos han sido devueltos al escritorio.", "Restauración Completa");
                    }
                };
                panel.Children.Add(btnPanic);

                Button btnExit = CreateButton("❌  Salir de la Aplicación", "#C42B1C");
                btnExit.Click += (s, e) => { 
                    Application.Current.Shutdown(); 
                };
                panel.Children.Add(btnExit);
            }));
            mainStack.Children.Add(CreateSection("Apariencia", panel => {
                panel.Children.Add(CreateLabel("Tamaño de Iconos"));
                Slider sizeSlider = CreateSlider(40, 120, MainWindow.GlobalIconSize);
                sizeSlider.ValueChanged += (s, e) => {
                    MainWindow.GlobalIconSize = e.NewValue;
                    UpdateAllBoxes(b => b.SetIconSize(e.NewValue));
                };
                panel.Children.Add(sizeSlider);

                panel.Children.Add(CreateLabel("Opacidad de Ventana"));
                Slider opacitySlider = CreateSlider(0.1, 1.0, MainWindow.GlobalOpacity);
                opacitySlider.ValueChanged += (s, e) => {
                    MainWindow.GlobalOpacity = e.NewValue;
                    UpdateAllBoxes(b => b.SetWindowOpacity(e.NewValue));
                };
                panel.Children.Add(opacitySlider);

                panel.Children.Add(CreateLabel("Redondez de Bordes"));
                Slider radiusSlider = CreateSlider(0, 30, MainWindow.GlobalCornerRadius);
                radiusSlider.ValueChanged += (s, e) => {
                    MainWindow.GlobalCornerRadius = e.NewValue;
                    UpdateAllBoxes(b => b.SetCornerRadius(e.NewValue));
                };
                panel.Children.Add(radiusSlider);
            }));


            mainStack.Children.Add(CreateSection("Visualización", panel => {
                CheckBox chkNames = CreateCheckBox("Mostrar Nombres de Archivo", MainWindow.GlobalShowFileName);
                chkNames.Checked += (s, e) => { MainWindow.GlobalShowFileName = true; UpdateAllBoxes(b => b.SetShowFileName(true)); };
                chkNames.Unchecked += (s, e) => { MainWindow.GlobalShowFileName = false; UpdateAllBoxes(b => b.SetShowFileName(false)); };
                panel.Children.Add(chkNames);

                CheckBox chkArrows = CreateCheckBox("Mostrar Flecha de Acceso Directo", MainWindow.GlobalShowShortcutSymbol);
                chkArrows.Checked += (s, e) => { MainWindow.GlobalShowShortcutSymbol = true; UpdateAllBoxes(b => b.SetShowShortcutSymbol(true)); };
                chkArrows.Unchecked += (s, e) => { MainWindow.GlobalShowShortcutSymbol = false; UpdateAllBoxes(b => b.SetShowShortcutSymbol(false)); };
                panel.Children.Add(chkArrows);
            }));


            mainStack.Children.Add(CreateSection("Personalización de Color", panel => {
      
                Color initColor = MainWindow.GlobalColor;
                Border preview = new Border { Height = 45, CornerRadius = new CornerRadius(6), Margin = new Thickness(0,0,0,15), BorderBrush = new SolidColorBrush(Color.FromArgb(50,255,255,255)), BorderThickness = new Thickness(1), Background = new SolidColorBrush(initColor) };
                panel.Children.Add(preview);

                var rSlider = CreateColorSlider("Rojo (R)", initColor.R, panel);
                var gSlider = CreateColorSlider("Verde (G)", initColor.G, panel);
                var bSlider = CreateColorSlider("Azul (B)", initColor.B, panel);

                void OnColorChange(object sender, RoutedPropertyChangedEventArgs<double> e) 
                { 
                    Color c = Color.FromRgb((byte)rSlider.Value, (byte)gSlider.Value, (byte)bSlider.Value); 
                    preview.Background = new SolidColorBrush(c);
                    
     
                    MainWindow.GlobalColor = c;
                    
                
                    Color finalC = c;
                    finalC.A = (byte)(MainWindow.GlobalOpacity * 255);
                    UpdateAllBoxes(b => b.SetBackgroundColor(finalC)); 
                }
                rSlider.ValueChanged += OnColorChange; gSlider.ValueChanged += OnColorChange; bSlider.ValueChanged += OnColorChange;
            }));

            mainStack.Children.Add(CreateSection("Autor", panel => {
                panel.Children.Add(new TextBlock { Text = "dvega-uy", Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,5) });
                Button btnGithub = CreateButton("", "#24292e");
                
                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                Image img = new Image { Width = 20, Height = 20, Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center };
                try {
                     var gitIcon = MainWindow.LoadImageFromFile("github-64.ico");
                     if (gitIcon != null) img.Source = gitIcon;
                     else 
                     {
                        var geometry = Geometry.Parse("M8,0C3.58,0,0,3.58,0,8c0,3.54,2.29,6.53,5.47,7.59c0.4,0.07,0.55-0.17,0.55-0.38c0-0.19-0.01-0.82-0.01-1.49c-2.01,0.37-2.53-0.49-2.69-0.94C3.22,12.51,2.96,12.05,2.71,11.91c-0.21-0.11-0.5-0.38-0.01-0.39c0.46-0.01,0.79,0.43,0.9,0.61c0.52,0.89,1.38,0.63,1.71,0.49c0.05-0.38,0.2-0.63,0.37-0.78c-1.79-0.2-3.67-0.9-3.67-3.99c0-0.88,0.31-1.59,0.82-2.15c-0.08-0.2-0.36-1.02,0.08-2.12c0,0,0.67-0.21,2.2,0.82c0.64-0.18,1.32-0.27,2-0.27c0.68,0,1.36,0.09,2,0.27c1.53-1.04,2.2-0.82,2.2-0.82c0.44,1.1,0.16,1.92,0.08,2.12c0.51,0.56,0.82,1.27,0.82,2.15c0,3.09-1.87,3.79-3.67,3.99c0.21,0.18,0.4,0.54,0.4,1.09c0,0.79-0.01,1.43-0.01,1.62c0,0.21,0.15,0.46,0.55,0.38C13.71,14.53,16,11.54,16,8C16,3.58,12.42,0,8,0z");
                        img.Source = new DrawingImage(new GeometryDrawing(Brushes.White, null, geometry));
                     }
                } catch {}
                sp.Children.Add(img);
                sp.Children.Add(new TextBlock { Text = "GitHub", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White });
                btnGithub.Content = sp;

                btnGithub.Click += (s, e) => { 
                    try { Process.Start(new ProcessStartInfo("https://github.com/dvega-uy") { UseShellExecute = true }); } catch { } 
                };
                panel.Children.Add(btnGithub);
            }));

            mainBorder.Child = grid;
            this.Content = mainBorder;
        }

        private void UpdateAllBoxes(Action<MainWindow> action)
        {
            foreach (var box in MainWindow.OpenBoxes) action(box);
        }

 
        private Border CreateSection(string title, Action<StackPanel> contentBuilder)
        {
            Border card = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 30, 32)), CornerRadius = new CornerRadius(8), Margin = new Thickness(0,0,0,20), Padding = new Thickness(15) };
            StackPanel panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,15) });
            contentBuilder(panel);
            card.Child = panel;
            return card;
        }

        private TextBlock CreateLabel(string text) => new TextBlock { Text = text, Foreground = Brushes.LightGray, FontSize = 12, Margin = new Thickness(0, 5, 0, 2) };
        
        private Slider CreateSlider(double min, double max, double val)
        {
            Slider s = new Slider { Minimum = min, Maximum = max, Value = val, Margin = new Thickness(0,0,0,10), IsSnapToTickEnabled = true, TickFrequency = (max-min)/20, IsMoveToPointEnabled = true };
            return s;
        }

        private CheckBox CreateCheckBox(string text, bool isChecked)
        {
            CheckBox cb = new CheckBox { Content = text, IsChecked = isChecked, Foreground = Brushes.LightGray, Margin = new Thickness(0, 5, 0, 10), FontSize = 13, Cursor = Cursors.Hand };
            return cb;
        }

        private Slider CreateColorSlider(string label, double val, StackPanel parent)
        {
            parent.Children.Add(CreateLabel(label));
            Slider s = new Slider { Minimum = 0, Maximum = 255, Value = val, Margin = new Thickness(0,0,0,10), IsSnapToTickEnabled = true, TickFrequency = 1, IsMoveToPointEnabled = true };
            parent.Children.Add(s);
            return s;
        }
        
        private Button CreateButton(string text, string hexColor)
        {
            var converter = new BrushConverter();
            var brush = (converter.ConvertFromString(hexColor) as SolidColorBrush) ?? new SolidColorBrush(Colors.Gray);

            var hoverColor = Color.FromRgb(
                (byte)Math.Min(255, brush.Color.R + 60), 
                (byte)Math.Min(255, brush.Color.G + 60), 
                (byte)Math.Min(255, brush.Color.B + 60));
            var hoverBrush = new SolidColorBrush(hoverColor);

            var btn = new Button { Content = text, Background = brush, Foreground = Brushes.White, Height = 40, Margin = new Thickness(0, 5, 0, 5), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 14 };
            btn.Template = GetBtnTemplate(brush, hoverBrush);
            return btn;
        }


        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                {
                    return key?.GetValue("MyDesktopOrganizer") != null;
                }
            }
            catch { return false; }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (enable)
                    {
                        string? path = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(path))
                            key?.SetValue("MyDesktopOrganizer", $"\"{path}\" --autostart");
                    }
                    else
                    {
                        key?.DeleteValue("MyDesktopOrganizer", false);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al configurar inicio automático: " + ex.Message); }
        }

        private ControlTemplate GetBtnTemplate(Brush normal, Brush hover, double radius = 5)
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.BackgroundProperty, normal);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(content);
            template.VisualTree = border;

            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, hover, "border"));
            template.Triggers.Add(mouseOver);
            
            return template;
        }
    }
    
    public partial class MainWindow
    {
        public static BitmapImage? LoadImageFromFile(string fileName)
        {
            try {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                
                if (!File.Exists(path))
                {
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        path = Path.Combine(Path.GetDirectoryName(exePath)!, fileName);
                }

                if (File.Exists(path)) {
                    byte[] buffer = File.ReadAllBytes(path);
                    var ms = new MemoryStream(buffer);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            } catch { }
            return null;
        }
    }

    public class BoxData
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Color { get; set; } = "";
        public bool IsTitleVisible { get; set; }
        public bool IsLocked { get; set; }
        public bool ShowFileName { get; set; } = true;
        public bool ShowShortcutSymbol { get; set; } = true;
        public double IconSize { get; set; }
        public double CornerRadius { get; set; }
    }

    public class ModernMessageBox : Window
    {
        public bool Result { get; private set; } = false;

        private ModernMessageBox(string message, string title, bool isConfirmation)
        {
            Title = title;
            Width = 350; Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;

            Border mainBorder = new Border 
            { 
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), 
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20)
            };
            mainBorder.Effect = new DropShadowEffect { BlurRadius = 20, Opacity = 0.5 };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });


            StackPanel msgPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            msgPanel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0,0,0,10) });
            msgPanel.Children.Add(new TextBlock { Text = message, Foreground = Brushes.LightGray, FontSize = 14, TextWrapping = TextWrapping.Wrap });
            grid.Children.Add(msgPanel);

  
            StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,20,0,0) };
            Grid.SetRow(btnPanel, 1);

            if (isConfirmation)
            {
                Button btnYes = CreateBtn("Sí, Eliminar", "#C42B1C"); // Rojo
                btnYes.Click += (s, e) => { Result = true; Close(); };
                btnPanel.Children.Add(btnYes);

                Button btnNo = CreateBtn("Cancelar", "#3E3E42");
                btnNo.Click += (s, e) => { Result = false; Close(); };
                btnPanel.Children.Add(btnNo);
            }
            else
            {
                Button btnOk = CreateBtn("Aceptar", "#007ACC");
                btnOk.Click += (s, e) => { Result = true; Close(); };
                btnPanel.Children.Add(btnOk);
            }

            grid.Children.Add(btnPanel);
            mainBorder.Child = grid;
            Content = mainBorder;
        }

        private Button CreateBtn(string text, string color)
        {
            var converter = new BrushConverter();
            var brush = (converter.ConvertFromString(color) as SolidColorBrush) ?? new SolidColorBrush(Colors.Gray);
            
            Button b = new Button 
            { 
                Content = text, 
                Width = 100, 
                Height = 35, 
                Margin = new Thickness(10,0,0,0), 
                Background = brush, 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0), 
                Cursor = Cursors.Hand
            };
            
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.BackgroundProperty, brush);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;

            Trigger mouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
   
            var hoverColor = Color.FromRgb((byte)Math.Min(255, brush.Color.R + 40), (byte)Math.Min(255, brush.Color.G + 40), (byte)Math.Min(255, brush.Color.B + 40));
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverColor), "border"));
            template.Triggers.Add(mouseOver);
            b.Template = template;

            return b;
        }

        public static bool Show(string message, string title = "Alerta", bool isConfirmation = false)
        {
            var msg = new ModernMessageBox(message, title, isConfirmation);
            msg.ShowDialog();
            return msg.Result;
        }
    }
}