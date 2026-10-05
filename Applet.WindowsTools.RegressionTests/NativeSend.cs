using System.Diagnostics;
using System.Runtime.InteropServices;
using Applets.WindowsTools;

internal static class NativeSend
{
    public static int Run()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var previous = GetForegroundWindow();
        try
        {
            using var form = new Form { Text = "WindowsTools isolated SendInput test", Width = 450, Height = 150 };
            var input = new TextBox { Text = "WindowsTools test text", Dock = DockStyle.Fill };
            form.Controls.Add(input); form.Show(); form.Activate(); input.Focus();
            var thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var attached = thread != 0 && AttachThreadInput(GetCurrentThreadId(), thread, true);
            try { SetForegroundWindow(form.Handle); }
            finally { if (attached) AttachThreadInput(GetCurrentThreadId(), thread, false); }
            if (GetForegroundWindow() != form.Handle) throw new InvalidOperationException("Cannot focus test window");
            input.Focus(); Application.DoEvents();
            input.SelectionStart = input.TextLength; input.SelectionLength = 0;
            var cursor = Cursor.Position;
            var task = new WindowsPlatform().SendAsync(KeyChord.Parse("Ctrl+A"), CancellationToken.None);
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted) {
                if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException();
                Application.DoEvents(); Thread.Sleep(10);
            }
            task.GetAwaiter().GetResult(); Application.DoEvents();
            Console.WriteLine($"Selection={input.SelectionLength}/{input.TextLength}; inputFocused={input.Focused}; cursor={cursor}->{Cursor.Position}; foreground={GetForegroundWindow() == form.Handle}");
            if (input.SelectionLength != input.TextLength || Cursor.Position != cursor || GetForegroundWindow() != form.Handle)
                throw new InvalidOperationException("Selection, cursor, or focus mismatch");
            Console.WriteLine("PASS SendInput Ctrl+A to owned TextBox; selection, cursor and focus verified");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if (previous != 0) SetForegroundWindow(previous); }
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint from, uint to, bool attach);
}
