using System.Text.Json;
using AppDock.SDK;

namespace Applets.WindowsTools;

public sealed class WindowsToolsApplet : IAppDockExtension
{
    private readonly IWindowsPlatform platform;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly DisplayCountdown display = new();
    private readonly IdleLockPolicy idle = new();
    private IExtensionContext context = null!;
    private IDisposable? schedule, settingsSubscription;
    private bool active, autoLockEnabled;
    private int displaySeconds = 5, lockMinutes = 360;
    private int shortcutCount;
    public WindowsToolsApplet() : this(new WindowsPlatform()) { }
    internal WindowsToolsApplet(IWindowsPlatform platform) => this.platform = platform;

    public async Task ActivateAsync(IExtensionContext context, CancellationToken token)
    {
        this.context = context;
        active = true;
        idle.Reset(platform.Uptime);
        await RefreshSettings(token);
        settingsSubscription = context.Settings.OnChanged(RefreshSettings);
        schedule = context.Scheduler.Every(TimeSpan.FromSeconds(1), Tick);
    }

    private async Task RefreshSettings(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (!active) return;
            var shortcuts = Shortcut.Parse(context.Settings.Get("shortcuts", JsonSerializer.SerializeToElement(Array.Empty<object>())));
            var commands = new List<CommandRegistration> {
                Command("open-folder", "最前面アプリの実行ファイルのフォルダーを開く", ct => { platform.OpenForegroundFolder(); return Task.CompletedTask; }),
                Command("display-off", "ディスプレイの消灯を予約", async ct => {
                    display.Schedule(platform.Uptime, displaySeconds); await ShowPanel(ct);
                }),
                Command("cancel-display-off", "ディスプレイの消灯予約を取り消す", async ct => {
                    display.Cancel(); await ShowPanel(ct);
                }),
                Command("toggle-auto-lock", "無操作時の自動ロックを切り替える", async ct => {
                    var enabled = !autoLockEnabled;
                    await context.Settings.SetAsync("autoLockEnabled", enabled, ct);
                    autoLockEnabled = enabled; idle.Reset(platform.Uptime); await ShowPanel(ct);
                })
            };
            foreach (var shortcut in shortcuts)
                commands.Add(Command("send." + shortcut.Id, shortcut.Title, ct => platform.SendAsync(shortcut.Chord, ct)));
            await context.Commands.ReplaceAsync(commands, token);
            displaySeconds = Math.Clamp(context.Settings.Get("displayOffSeconds", 5), 1, 3600);
            var enabled = context.Settings.Get("autoLockEnabled", false);
            var minutes = Math.Clamp(context.Settings.Get("autoLockMinutes", 360), 1, 1440);
            if (enabled != autoLockEnabled || minutes != lockMinutes) idle.Reset(platform.Uptime);
            autoLockEnabled = enabled;
            lockMinutes = minutes;
            shortcutCount = shortcuts.Count;
            await ShowPanel(token);
        }
        finally { gate.Release(); }
    }

    private CommandRegistration Command(string suffix, string title, Func<CancellationToken, Task> action) =>
        new(context.ExtensionId + "." + suffix, title, async token => {
            await gate.WaitAsync(token);
            try { token.ThrowIfCancellationRequested(); if (active) await action(token); }
            finally { gate.Release(); }
        });

    private async Task Tick(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!active) return;
            if (display.TakeDue(platform.Uptime))
            {
                platform.TurnOffDisplay();
                await ShowPanel(token);
            }
            token.ThrowIfCancellationRequested();
            if (autoLockEnabled && idle.TakeDue(platform.Uptime, platform.LastInputTimestamp(), lockMinutes))
                platform.Lock();
        }
        finally { gate.Release(); }
    }

    private Task ShowPanel(CancellationToken token) => context.Ui.ShowPanelAsync(new Panel("WindowsTools",
        "フォルダー表示とキー送信は実行時の最前面アプリが対象です。ほかのアプリにはグローバルショートカットから実行してください。",
        [new("消灯", display.Due is null ? $"待機中（実行から{displaySeconds}秒後）" : "消灯を予約中（再実行で予約し直し）"),
         new("自動ロック", autoLockEnabled ? $"{lockMinutes}分間、マウス・キーボードの操作がないとロック" : "無効"),
         new("キー送信", $"{shortcutCount}コマンド。設定の一覧を保存すると追加・削除が反映されます。")],
        [new("フォルダーを開く", context.ExtensionId + ".open-folder"),
         new("消灯を予約", context.ExtensionId + ".display-off"),
         new("消灯予約を取り消す", context.ExtensionId + ".cancel-display-off"),
         new("自動ロックを切り替える", context.ExtensionId + ".toggle-auto-lock")]), token);

    public async Task DeactivateAsync(CancellationToken token)
    {
        settingsSubscription?.Dispose(); settingsSubscription = null;
        schedule?.Dispose(); schedule = null;
        await gate.WaitAsync(token);
        try { active = false; display.Cancel(); }
        finally { gate.Release(); }
    }
}
