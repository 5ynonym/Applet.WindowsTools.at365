using System.Text.Json;
using AppDock.SDK;
using Applets.WindowsTools;
using Shortcut = Applets.WindowsTools.Shortcut;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--native-send"]) return NativeSend.Run();
        return Run().GetAwaiter().GetResult();
    }
    private static async Task<int> Run()
    {
        var tests = new List<(string, Func<Task>)>();
        void Test(string name, Action test) => tests.Add((name, () => { test(); return Task.CompletedTask; }));
        Test("shortcut grammar and virtual keys", () => {
            var chord = KeyChord.Parse("Ctrl+Shift+T");
            Check(chord.Key == 'T' && chord.Modifiers.SequenceEqual(new ushort[] { 0x11, 0x10 }));
            Check(KeyChord.Parse("Win+Left").Extended);
            Check(KeyChord.Parse("F24").Key == 0x87);
            Check(KeyChord.Parse("Alt+F4").Key == 0x73);
        });
        Test("reject unknown, repeated and empty keys", () => {
            foreach (var keys in new[] { "", "Ctrl+", "Ctrl+Ctrl+A", "Ctrl+Control+A", "F25", "%{F4}", "Ctrl+A+B", "Text: hello" })
                Throws(() => KeyChord.Parse(keys));
        });
        Test("shortcut list validates duplicate IDs and format", () => {
            foreach (var json in new[] { "null", "{}", "[{}]", "[{\"id\":\"bad.id\",\"title\":\"X\",\"keys\":\"A\"}]",
                "[{\"id\":\"a\",\"title\":\"X\",\"keys\":\"A\"},{\"id\":\"a\",\"title\":\"Y\",\"keys\":\"B\"}]" })
                Throws(() => Shortcut.Parse(JsonSerializer.Deserialize<JsonElement>(json)));
        });
        Test("display waits until deadline, runs once", () => {
            var timer = new DisplayCountdown(); timer.Schedule(100, 5);
            Check(!timer.TakeDue(5099)); Check(timer.TakeDue(5100)); Check(!timer.TakeDue(99999));
        });
        Test("display reschedule and cancel", () => {
            var timer = new DisplayCountdown(); timer.Schedule(0, 5); timer.Schedule(4000, 5);
            Check(!timer.TakeDue(5000)); timer.Cancel(); Check(!timer.TakeDue(99999));
        });
        Test("idle begins when enabled and rearms only after input", () => {
            var policy = new IdleLockPolicy(); policy.Reset(100_000);
            Check(!policy.TakeDue(100_000, 0, 1)); Check(!policy.TakeDue(159_999, 0, 1));
            Check(policy.TakeDue(160_000, 0, 1)); Check(!policy.TakeDue(250_000, 0, 1));
            Check(!policy.TakeDue(250_000, 249_000, 1)); Check(policy.TakeDue(309_000, 249_000, 1));
        });
        Test("idle counter wrap-around", () => {
            var policy = new IdleLockPolicy(); var start = (long)uint.MaxValue - 30_000;
            policy.Reset(start); Check(!policy.TakeDue(start + 59_999, (uint)start, 1));
            Check(policy.TakeDue(start + 60_000, (uint)start, 1));
        });
        tests.Add(("live add, rename, replace, delete; invalid edit retains commands", async () => {
            var context = new FakeContext(); var platform = new FakePlatform(); var applet = new WindowsToolsApplet(platform);
            await applet.ActivateAsync(context, default); Check(context.Handlers.Count == 4);
            await context.Change("shortcuts", new[] { new { id = "new-tab", title = "New tab", keys = "Ctrl+T" } });
            Check(context.Handlers.Count == 5); await context.Execute("send.new-tab"); Check(platform.Sends == 1);
            await context.Change("shortcuts", new[] { new { id = "new-tab", title = "Renamed", keys = "Ctrl+N" } });
            Check(context.Titles[context.ExtensionId + ".send.new-tab"] == "Renamed");
            await context.Execute("send.new-tab"); Check(platform.LastChord!.Key == 'N');
            await ThrowsAsync(() => context.Change("shortcuts", new[] { new { id = "new-tab", title = "Broken", keys = "bad" } }));
            Check(context.Handlers.Count == 5);
            await context.Change("shortcuts", Array.Empty<object>()); Check(context.Handlers.Count == 4);
            await applet.DeactivateAsync(default); Check(context.Timer is null && context.Changed is null);
        }));
        tests.Add(("display countdown executes, cancels and stops on deactivate", async () => {
            var context = new FakeContext(); var platform = new FakePlatform(); var applet = new WindowsToolsApplet(platform);
            await applet.ActivateAsync(context, default); await context.Execute("display-off");
            platform.Uptime = 4999; await context.Tick(); Check(platform.Offs == 0);
            platform.Uptime = 5000; await context.Tick(); Check(platform.Offs == 1);
            await context.Tick(); Check(platform.Offs == 1);
            await context.Execute("display-off"); await context.Execute("cancel-display-off");
            platform.Uptime = 20000; await context.Tick(); Check(platform.Offs == 1);
            await context.Execute("display-off"); var queuedTick = context.Timer!;
            await applet.DeactivateAsync(default); platform.Uptime = 30000;
            await queuedTick(default); Check(platform.Offs == 1);
        }));
        tests.Add(("idle lock opt-in, threshold, input reset, disable", async () => {
            var context = new FakeContext(); var platform = new FakePlatform(); var applet = new WindowsToolsApplet(platform);
            await applet.ActivateAsync(context, default);
            platform.Uptime = 100_000_000; await context.Tick(); Check(platform.Locks == 0);
            await context.Change("autoLockMinutes", 1); await context.Execute("toggle-auto-lock");
            await context.Tick(); Check(platform.Locks == 0);
            platform.Uptime += 60_000; await context.Tick(); Check(platform.Locks == 1);
            platform.Uptime += 60_000; await context.Tick(); Check(platform.Locks == 1);
            await context.Execute("toggle-auto-lock"); platform.Input = (uint)platform.Uptime;
            platform.Uptime += 60_000; await context.Tick(); Check(platform.Locks == 1);
            await applet.DeactivateAsync(default);
        }));
        tests.Add(("folder delegates once; canceled command has no effects", async () => {
            var context = new FakeContext(); var platform = new FakePlatform(); var applet = new WindowsToolsApplet(platform);
            await applet.ActivateAsync(context, default); await context.Execute("open-folder"); Check(platform.Folders == 1);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await ThrowsAsync(() => context.Handlers[context.ExtensionId + ".open-folder"](cancellation.Token)); Check(platform.Folders == 1);
            await applet.DeactivateAsync(default);
        }));
        var failed = 0;
        foreach (var (name, test) in tests) {
            try { await test(); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
        }
        Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed"); return failed == 0 ? 0 : 1;
    }
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Assertion failed"); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected rejection"); }
    private static async Task ThrowsAsync(Func<Task> action) { try { await action(); } catch (Exception) { return; } throw new InvalidOperationException("Expected rejection"); }
    private sealed class FakePlatform : IWindowsPlatform
    {
        public long Uptime { get; set; }
        public uint Input;
        public int Offs, Locks, Folders, Sends;
        public KeyChord? LastChord;
        public uint LastInputTimestamp() => Input;
        public void OpenForegroundFolder() => Folders++;
        public void TurnOffDisplay() => Offs++;
        public void Lock() => Locks++;
        public Task SendAsync(KeyChord chord, CancellationToken token) { Sends++; LastChord = chord; return Task.CompletedTask; }
    }
    private sealed class FakeContext : IExtensionContext, ICommandService, ISettingsService, IUiService, ISchedulerService
    {
        public string ExtensionId => "at365.windows-tools";
        public Dictionary<string, Func<CancellationToken, Task>> Handlers = [];
        public Dictionary<string, string> Titles = [];
        public Dictionary<string, object> Values = [];
        public Func<CancellationToken, Task>? Changed, Timer;
        public ICommandService Commands => this;
        public ISettingsService Settings => this;
        public IUiService Ui => this;
        public ISchedulerService Scheduler => this;
        public ITrayService Tray => throw new NotSupportedException();
        public INotificationService Notifications => throw new NotSupportedException();
        public IBrowserService Browser => throw new NotSupportedException();
        public ILogService Log => throw new NotSupportedException();
        public IStorageService Storage => throw new NotSupportedException();
        public ISecretService Secrets => throw new NotSupportedException();
        public void Register(string id, string title, Func<CancellationToken, Task> handler) { Handlers.Add(id, handler); Titles.Add(id, title); }
        public Task ReplaceAsync(IReadOnlyList<CommandRegistration> commands, CancellationToken token = default) {
            Handlers = commands.ToDictionary(c => c.Id, c => c.Handler); Titles = commands.ToDictionary(c => c.Id, c => c.Title); return Task.CompletedTask;
        }
        public T Get<T>(string key, T fallback) => Values.TryGetValue(key, out var value) ? JsonSerializer.SerializeToElement(value).Deserialize<T>()! : fallback;
        public Task SetAsync<T>(string key, T value, CancellationToken token = default) { Values[key] = value!; return Task.CompletedTask; }
        public IDisposable OnChanged(Func<CancellationToken, Task> handler) { Changed = handler; return new Release(() => Changed = null); }
        public Task SetOptionsAsync(string key, IReadOnlyList<SettingOption> options, CancellationToken token = default) => throw new NotSupportedException();
        public Task ShowPanelAsync(AppDock.SDK.Panel panel, CancellationToken token = default) => Task.CompletedTask;
        public Task<string> GetImageDirectoryAsync(CancellationToken token = default) => throw new NotSupportedException();
        public IDisposable Every(TimeSpan interval, Func<CancellationToken, Task> callback) { Timer = callback; return new Release(() => Timer = null); }
        public Task Execute(string suffix) => Handlers[ExtensionId + "." + suffix](default);
        public Task Tick() => Timer!(default);
        public async Task Change(string key, object value) { Values[key] = value; if (Changed is not null) await Changed(default); }
        private sealed class Release(Action action) : IDisposable { public void Dispose() => action(); }
    }
}
