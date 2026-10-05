const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const host = path.resolve(process.argv[2] || path.join(root, '../AppDock.at365'));
const packaged = process.argv[3] ? path.resolve(process.argv[3]) : null;
const { _electron: electron } = require(path.join(host, 'node_modules/playwright'));
const { createDefaultSettings } = require(path.join(host, 'out/main/shared/settings-schema.js'));
const profile = path.join(root, 'artifacts', `host-${Date.now()}`);
const folder = path.join(profile, 'extensions/Applet.WindowsTools.at365');
fs.mkdirSync(folder, { recursive: true });
for (const name of ['extension.json', 'Applet.WindowsTools.at365.dll', 'Applet.WindowsTools.at365.deps.json'])
  fs.copyFileSync(path.join(root, 'publish/Applet.WindowsTools.at365', name), path.join(folder, name));
const id = 'at365.windows-tools';
const dynamicId = id + '.send.shortcut-1';
const settings = createDefaultSettings();
settings.host.notifications = false;
settings.extensions[id] = { enabled: true, settings: { displayOffSeconds: 3600, autoLockEnabled: false } };
settings.shortcuts[dynamicId] = ['Ctrl+Alt+F19'];
settings.globalShortcutCommands.push(dynamicId);
fs.writeFileSync(path.join(profile, 'settings.json'), JSON.stringify(settings));
let application;
async function snapshot(page) { return page.evaluate(() => window.dock.snapshot()); }
async function until(check, message) {
  const end = Date.now() + 15000;
  while (Date.now() < end) { if (await check()) return; await new Promise(r => setTimeout(r, 80)); }
  throw new Error(message);
}
async function state(page) { return (await snapshot(page)).extensions.find(e => e.id === id); }
(async () => {
  try {
    application = await electron.launch({
      executablePath: packaged || require(path.join(host, 'node_modules/electron')),
      args: packaged ? [`--test-profile=${profile}`] : [host, `--test-profile=${profile}`], timeout: 30000,
    });
    const page = await application.firstWindow();
    await page.getByRole('heading', { name: 'ホーム', exact: true }).waitFor();
    await until(async () => (await state(page))?.commands.length === 4, 'Initial 4 commands missing');
    assert.equal((await state(page)).state, 'running');
    await page.keyboard.press('Control+,');
    await page.locator('.settings-applet-list').getByRole('button', { name: 'Applet.WindowsTools.at365', exact: true }).click();
    await page.getByRole('button', { name: 'コマンドを追加', exact: true }).click();
    await page.getByLabel('送信するショートカット 1 名前', { exact: true }).fill('全選択テスト');
    await page.getByLabel('送信するショートカット 1 キー', { exact: true }).fill('Ctrl+A');
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await until(async () => (await state(page)).commands.some(c => c.id === dynamicId && c.title === '全選択テスト'), 'Dynamic add failed');
    await until(async () => (await snapshot(page)).globalHotKeys.some(h => h.commandId === dynamicId && h.registered), 'Dynamic global binding not registered');
    await page.screenshot({ path: path.join(profile, 'shortcut-settings.png'), fullPage: true });
    await page.getByLabel('送信するショートカット 1 名前', { exact: true }).fill('名前の変更テスト');
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await until(async () => (await state(page)).commands.some(c => c.id === dynamicId && c.title === '名前の変更テスト'), 'Dynamic rename failed');
    await page.getByRole('button', { name: '送信するショートカット 1 削除', exact: true }).click();
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await until(async () => (await state(page)).commands.length === 4, 'Dynamic delete failed');
    await until(async () => !(await snapshot(page)).globalHotKeys.some(h => h.commandId === dynamicId && h.registered), 'Removed hotkey remains registered');
    await assert.rejects(page.evaluate(command => window.dock.executeCommand(command), dynamicId), /利用できません/);
    // The hour-long reservation is canceled immediately; the display is never actually switched off.
    await page.evaluate(command => window.dock.executeCommand(command), id + '.display-off');
    assert.match((await state(page)).panel.facts[0].value, /予約中/);
    await page.evaluate(command => window.dock.executeCommand(command), id + '.cancel-display-off');
    assert.match((await state(page)).panel.facts[0].value, /待機中/);
    await page.evaluate(extension => window.dock.restartExtension(extension), id);
    await until(async () => (await state(page)).state === 'running' && (await state(page)).commands.length === 4, 'Restart failed');
    await page.evaluate(extension => window.dock.toggleExtension(extension, false), id);
    await until(async () => (await state(page)).state === 'stopped', 'Deactivate failed');
    assert.equal((await state(page)).commands.length, 0);
    const errors = (await snapshot(page)).logs.filter(entry => entry.level === 'error');
    assert.equal(errors.length, 0, JSON.stringify(errors));
    console.log('PASS live settings add/rename/delete, global registration/removal, rejected stale command, countdown/cancel, restart, deactivate');
    console.log(profile);
  } finally {
    if (application) await application.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
