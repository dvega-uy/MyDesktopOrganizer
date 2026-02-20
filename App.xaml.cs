using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MyDesktopOrganizer;

public partial class App : Application
{
    private NativeTrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MyDesktopOrganizer.MainWindow.LoadLayout();
        
        string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyDesktopOrganizer.ico");
        if (!System.IO.File.Exists(iconPath))
        {
            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath)) iconPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(exePath)!, "MyDesktopOrganizer.ico");
        }
        _trayIcon = new NativeTrayIcon(iconPath, () => 
        {
            if (MyDesktopOrganizer.SettingsWindow.Instance != null)
            {
                MyDesktopOrganizer.SettingsWindow.Instance.Activate();
                if (MyDesktopOrganizer.SettingsWindow.Instance.WindowState == WindowState.Minimized)
                    MyDesktopOrganizer.SettingsWindow.Instance.WindowState = WindowState.Normal;
            }
            else
            {
                new MyDesktopOrganizer.SettingsWindow().Show();
            }
        });
        bool isAutoStart = false;
        foreach (string arg in e.Args)
        {
            if (arg == "--autostart") isAutoStart = true;
        }
        if (!isAutoStart) new MyDesktopOrganizer.SettingsWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            foreach (var box in MyDesktopOrganizer.MainWindow.OpenBoxes.ToList())
            {
                box.RestoreFilesToDesktop();
            }
        }
        catch { }

        _trayIcon?.Dispose();
        MyDesktopOrganizer.MainWindow.CleanupHook();
        MyDesktopOrganizer.MainWindow.SaveLayout();
        base.OnExit(e);
    }
}

public class NativeTrayIcon : IDisposable
{
    private NotifyIconData _data;
    private readonly HwndSource _messageWindow;
    private readonly Action _onClick;

    public NativeTrayIcon(string iconPath, Action onClick)
    {
        _onClick = onClick;

        _messageWindow = new HwndSource(new HwndSourceParameters());
        _messageWindow.AddHook(WndProc);

        IntPtr hIcon = LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x00000010);
        if (hIcon == IntPtr.Zero) hIcon = LoadImage(IntPtr.Zero, 32512, 1, 0, 0, 0x00008000);

        _data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NotifyIconData)),
            hWnd = _messageWindow.Handle,
            uID = 1,
            uFlags = 0x00000001 | 0x00000002 | 0x00000004,
            uCallbackMessage = 0x0400 + 1,
            hIcon = hIcon,
            szTip = "My Desktop Organizer"
        };

        Shell_NotifyIcon(0, ref _data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0400 + 1)
        {
            if ((int)lParam == 0x0201 || (int)lParam == 0x0204)
            {
                Application.Current.Dispatcher.Invoke(_onClick);
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Shell_NotifyIcon(2, ref _data);
        _messageWindow.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);
}
