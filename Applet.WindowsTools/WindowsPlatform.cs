using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Applets.WindowsTools;

internal interface IWindowsPlatform
{
    long Uptime { get; }
    uint LastInputTimestamp();
    void OpenForegroundFolder();
    void TurnOffDisplay();
    void Lock();
    Task SendAsync(KeyChord chord, CancellationToken token);
}

internal sealed class WindowsPlatform : IWindowsPlatform
{
    private static readonly int[] HeldModifiers = [0x10, 0x11, 0x12, 0x5B, 0x5C];
    public long Uptime => Environment.TickCount64;
    public uint LastInputTimestamp()
    {
        var info = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        if (!GetLastInputInfo(ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return info.Time;
    }
    public void OpenForegroundFolder()
    {
        var window = Target();
        GetWindowThreadProcessId(window, out var pid);
        using var process = Process.GetProcessById((int)pid);
        var file = process.MainModule?.FileName;
        var directory = Path.GetDirectoryName(file);
        if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("対象アプリの実行ファイルのフォルダーを取得できませんでした。");
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add(directory);
        using var explorer = Process.Start(start);
    }
    public void TurnOffDisplay()
    {
        if (!PostMessage(new nint(0xFFFF), 0x0112, 0xF170, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Lock()
    {
        if (!LockWorkStation()) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public async Task SendAsync(KeyChord chord, CancellationToken token)
    {
        var target = Target();
        var started = Uptime;
        // Let the invoking hotkey go up. Never release keys physically held by the user.
        do
        {
            await Task.Delay(25, token);
            if (GetForegroundWindow() != target) throw new InvalidOperationException("最前面のウィンドウが変わったためキー送信を中止しました。");
            if (Uptime - started >= 2000) throw new InvalidOperationException("修飾キーを離してから再実行してください。");
        } while (HeldModifiers.Any(Pressed) || Pressed(chord.Key));
        token.ThrowIfCancellationRequested();
        var events = new List<Input>();
        foreach (var modifier in chord.Modifiers) events.Add(KeyEvent(modifier, false, modifier == 0x5B));
        events.Add(KeyEvent(chord.Key, false, chord.Extended));
        events.Add(KeyEvent(chord.Key, true, chord.Extended));
        foreach (var modifier in chord.Modifiers.Reverse()) events.Add(KeyEvent(modifier, true, modifier == 0x5B));
        var sent = SendInput((uint)events.Count, events.ToArray(), Marshal.SizeOf<Input>());
        if (sent != events.Count)
        {
            var error = Marshal.GetLastWin32Error();
            // Only release keys for which we may have injected key-down successfully.
            var releases = events.Take((int)sent).Where(e => (e.Data.Keyboard.Flags & 2) == 0)
                .Reverse().Select(e => KeyEvent(e.Data.Keyboard.Key, true, (e.Data.Keyboard.Flags & 1) != 0)).ToArray();
            if (releases.Length > 0) SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>());
            throw new InvalidOperationException($"キーを送信できませんでした（{sent}/{events.Count}, Win32={error}）。管理者権限のアプリには送信が制限されます。");
        }
        // Keep the command in flight while Windows dispatches any matching global hotkey.
        await Task.Delay(150, token);
    }
    private static bool Pressed(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private static nint Target()
    {
        var window = GetForegroundWindow();
        if (window == 0 || window == GetDesktopWindow() || window == GetShellWindow() || !IsWindowVisible(window))
            throw new InvalidOperationException("操作対象のアプリを最前面にしてください。");
        return window;
    }
    private static Input KeyEvent(ushort key, bool up, bool extended) => new() {
        Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = key, Flags = (up ? 2u : 0) | (extended ? 1u : 0) } }
    };
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Time; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetLastInputInfo(ref LastInput info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool LockWorkStation();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
