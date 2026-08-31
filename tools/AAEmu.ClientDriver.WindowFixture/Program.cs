using System.Runtime.InteropServices;
using System.Text;

namespace AAEmu.ClientDriver.WindowFixture;

internal static class Program
{
    private const string WindowClassName = "AAEmuClientDriverWindowFixture";
    private const string TitlePrefix = "AAEmu ClientDriver Window Fixture";
    private const uint WsOverlappedWindow = 0x00CF0000;
    private const int CwUseDefault = unchecked((int)0x80000000);
    private const int SwShow = 5;
    private const uint WmClose = 0x0010;
    private const uint WmDestroy = 0x0002;
    private const uint WmKeyDown = 0x0100;
    private const uint WmChar = 0x0102;
    private const uint WmLeftButtonDown = 0x0201;
    private const int IdcArrow = 32512;
    private const int ColorWindow = 5;

    private static readonly WindowProcedure WindowProcedureRoot = HandleWindowMessage;
    private static readonly StringBuilder TypedText = new();
    private static IntPtr _windowHandle;
    private static int? _lastKey;
    private static (int X, int Y)? _lastClick;

    [STAThread]
    private static int Main()
    {
        if (!OperatingSystem.IsWindows())
            return 2;

        var instance = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = WindowProcedureRoot,
            Instance = instance,
            Cursor = LoadCursor(IntPtr.Zero, new IntPtr(IdcArrow)),
            Background = new IntPtr(ColorWindow + 1),
            ClassName = WindowClassName
        };
        if (RegisterClassEx(ref windowClass) == 0)
            return Marshal.GetLastWin32Error();

        _windowHandle = CreateWindowEx(
            0,
            WindowClassName,
            TitlePrefix,
            WsOverlappedWindow,
            CwUseDefault,
            CwUseDefault,
            640,
            420,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
        if (_windowHandle == IntPtr.Zero)
            return Marshal.GetLastWin32Error();

        ShowWindow(_windowHandle, SwShow);
        UpdateWindow(_windowHandle);
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        return 0;
    }

    private static IntPtr HandleWindowMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter)
    {
        switch (message)
        {
            case WmKeyDown:
                _lastKey = unchecked((int)wordParameter.ToInt64());
                UpdateTitle();
                return IntPtr.Zero;
            case WmChar:
                var character = unchecked((char)wordParameter.ToInt64());
                if (character is >= ' ' and <= '~' && TypedText.Length < 256)
                    TypedText.Append(character);
                UpdateTitle();
                return IntPtr.Zero;
            case WmLeftButtonDown:
                var packed = unchecked((uint)longParameter.ToInt64());
                _lastClick = (unchecked((short)(packed & 0xffff)), unchecked((short)(packed >> 16)));
                UpdateTitle();
                return IntPtr.Zero;
            case WmClose:
                DestroyWindow(windowHandle);
                return IntPtr.Zero;
            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return DefWindowProc(windowHandle, message, wordParameter, longParameter);
        }
    }

    private static void UpdateTitle()
    {
        var key = _lastKey?.ToString() ?? "none";
        var click = _lastClick is { } point ? $"{point.X},{point.Y}" : "none";
        SetWindowText(_windowHandle, $"{TitlePrefix} | key={key} | click={click} | text={TypedText}");
    }

    private delegate IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMessage
    {
        public IntPtr WindowHandle;
        public uint Message;
        public IntPtr WordParameter;
        public IntPtr LongParameter;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowText(IntPtr windowHandle, string value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMessage(out WindowMessage message, IntPtr windowHandle, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref WindowMessage message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessage(ref WindowMessage message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);
}
