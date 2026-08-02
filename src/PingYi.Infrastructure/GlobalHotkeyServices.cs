using System.ComponentModel;
using System.Runtime.InteropServices;
using PingYi.Core;

namespace PingYi.Infrastructure;

public readonly record struct GlobalHotkeyGesture(bool Control, bool Alt, bool Shift, char Key)
{
    public static GlobalHotkeyGesture Parse(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            throw new NotSupportedException("快捷键不能为空。");
        }

        var control = false;
        var alt = false;
        var shift = false;
        char? key = null;
        foreach (var token in shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                control = true;
            }
            else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
            }
            else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
            }
            else if (token.Length == 1 && char.IsAsciiLetterOrDigit(token[0]) && key is null)
            {
                key = char.ToUpperInvariant(token[0]);
            }
            else
            {
                throw new NotSupportedException($"不支持的快捷键：{shortcut}。");
            }
        }

        if (key is null || (!control && !alt && !shift))
        {
            throw new NotSupportedException("快捷键必须包含 Ctrl、Alt 或 Shift，以及一个字母或数字。");
        }

        return new GlobalHotkeyGesture(control, alt, shift, key.Value);
    }
}

public static class GlobalHotkeyServiceFactory
{
    public static IGlobalHotkeyService Create() =>
        OperatingSystem.IsWindows()
            ? new WindowsGlobalHotkeyService()
            : new X11GlobalHotkeyService();
}

internal sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0012;
    private const uint ModAlt = 0x0001;
    private const uint ModShift = 0x0004;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int HotkeyId = 0x5049;

    private Thread? _thread;
    private uint _threadId;

    public event EventHandler? Pressed;

    public async Task StartAsync(string shortcut, CancellationToken cancellationToken = default)
    {
        if (_thread is not null)
        {
            return;
        }

        var gesture = GlobalHotkeyGesture.Parse(shortcut);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() => MessageLoop(gesture, shortcut, started))
        {
            IsBackground = true,
            Name = "PingYi.GlobalHotkey"
        };
        _thread.Start();
        try
        {
            await started.Task.WaitAsync(cancellationToken);
        }
        catch
        {
            _thread = null;
            _threadId = 0;
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_thread is null)
        {
            return Task.CompletedTask;
        }

        PostThreadMessage(_threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _threadId = 0;
        return Task.CompletedTask;
    }

    private void MessageLoop(GlobalHotkeyGesture gesture, string shortcut, TaskCompletionSource started)
    {
        _threadId = GetCurrentThreadId();
        var modifiers = ModNoRepeat;
        if (gesture.Control) modifiers |= ModControl;
        if (gesture.Alt) modifiers |= ModAlt;
        if (gesture.Shift) modifiers |= ModShift;
        if (!RegisterHotKey(IntPtr.Zero, HotkeyId, modifiers, gesture.Key))
        {
            started.SetException(new Win32Exception(Marshal.GetLastWin32Error(), $"快捷键 {shortcut} 已被其他程序占用。"));
            return;
        }

        started.SetResult();
        try
        {
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.MessageId == WmHotkey && message.WParam.ToInt32() == HotkeyId)
                {
                    Pressed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        finally
        {
            UnregisterHotKey(IntPtr.Zero, HotkeyId);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr HWnd;
        public uint MessageId;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Message message, IntPtr window, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}

internal sealed class X11GlobalHotkeyService : IGlobalHotkeyService
{
    private const int KeyPress = 2;
    private const int GrabModeAsync = 1;
    private const uint LockMask = 2;
    private const uint ShiftMask = 1;
    private const uint ControlMask = 4;
    private const uint AltMask = 8;
    private const uint NumLockMask = 16;

    private Thread? _thread;
    private volatile bool _stopping;
    private IntPtr _display;
    private IntPtr _root;
    private int _keyCode;

    public event EventHandler? Pressed;

    public async Task StartAsync(string shortcut, CancellationToken cancellationToken = default)
    {
        var session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (string.Equals(session, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException("首版全局快捷键仅支持 X11。");
        }

        var gesture = GlobalHotkeyGesture.Parse(shortcut);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopping = false;
        _thread = new Thread(() => EventLoop(gesture, started))
        {
            IsBackground = true,
            Name = "PingYi.X11GlobalHotkey"
        };
        _thread.Start();
        await started.Task.WaitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _stopping = true;
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        return Task.CompletedTask;
    }

    private void EventLoop(GlobalHotkeyGesture gesture, TaskCompletionSource started)
    {
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
        {
            started.SetException(new InvalidOperationException("无法连接 X11 显示服务器。"));
            return;
        }

        _root = XDefaultRootWindow(_display);
        var keysym = XStringToKeysym(char.ToLowerInvariant(gesture.Key).ToString());
        _keyCode = XKeysymToKeycode(_display, keysym);
        var modifiers = 0u;
        if (gesture.Control) modifiers |= ControlMask;
        if (gesture.Alt) modifiers |= AltMask;
        if (gesture.Shift) modifiers |= ShiftMask;
        foreach (var lockMask in new[] { 0u, LockMask, NumLockMask, LockMask | NumLockMask })
        {
            XGrabKey(_display, _keyCode, modifiers | lockMask, _root, true, GrabModeAsync, GrabModeAsync);
        }
        XSync(_display, false);
        started.SetResult();
        try
        {
            while (!_stopping)
            {
                while (XPending(_display) > 0)
                {
                    XNextEvent(_display, out var xevent);
                    if (xevent.Type == KeyPress)
                    {
                        Pressed?.Invoke(this, EventArgs.Empty);
                    }
                }

                Thread.Sleep(20);
            }
        }
        finally
        {
            foreach (var lockMask in new[] { 0u, LockMask, NumLockMask, LockMask | NumLockMask })
            {
                XUngrabKey(_display, _keyCode, modifiers | lockMask, _root);
            }
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent
    {
        [FieldOffset(0)] public int Type;
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XStringToKeysym(string value);

    [DllImport("libX11.so.6")]
    private static extern int XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport("libX11.so.6")]
    private static extern int XGrabKey(
        IntPtr display,
        int keyCode,
        uint modifiers,
        IntPtr grabWindow,
        [MarshalAs(UnmanagedType.Bool)] bool ownerEvents,
        int pointerMode,
        int keyboardMode);

    [DllImport("libX11.so.6")]
    private static extern int XUngrabKey(IntPtr display, int keyCode, uint modifiers, IntPtr grabWindow);

    [DllImport("libX11.so.6")]
    private static extern int XPending(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XNextEvent(IntPtr display, out XEvent xevent);

    [DllImport("libX11.so.6")]
    private static extern int XSync(IntPtr display, [MarshalAs(UnmanagedType.Bool)] bool discard);
}
