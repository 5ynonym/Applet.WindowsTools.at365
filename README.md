# Applet.WindowsTools.at365

最前面アプリのフォルダー表示、消灯予約、設定で追加できるショートカットキー送信、無操作時のPCロックを提供する AppDock 用 DLL Applet です。

## 必要なホスト

**動的コマンド更新と `shortcut-list` 設定に対応した AppDock v0.4.0 が必要です。** ホスト側変更は [AppDock](../AppDock.at365/) の main にマージ済みです。従来の v0.3.1 ではこのAppletは読み込めません。

## コマンドと設定

| コマンドID（`at365.windows-tools.` に続く部分） | 動作 |
| --- | --- |
| `open-folder` | 最前面アプリの実行ファイルがあるフォルダーをエクスプローラーで開く |
| `display-off` | 設定した秒数後にディスプレイを消灯。再実行すると予約し直す |
| `cancel-display-off` | 保留中の消灯予約を取り消す |
| `toggle-auto-lock` | 自動ロックの有効／無効を切り替えて保存 |
| `send.<ID>` | 設定一覧に追加したショートカットキーを送信 |

| 設定 | 既定値 | 範囲・意味 |
| --- | --- | --- |
| 消灯までの秒数 | 5秒 | 1〜3600秒 |
| 無操作時の自動ロック | 無効 | マウス・キーボードの両方を監視 |
| ロックまでの無操作時間 | 360分 | 1〜1440分 |
| 送信するショートカット | 空 | 最大32件。ID、表示名、送信キーを追加・削除可能 |

予約と無操作判定は1秒間隔で確認するため、実行は指定時刻から約1秒遅れることがあります。消灯時間の設定変更は次の予約から反映されます。自動ロックは有効化／監視時間の変更から数え直し、起動前の無操作だけを理由に直ちにロックしません。同じ無操作期間には1回だけロックを要求し、新しい入力があると再び監視します。

Appletの無効化・再起動・ホスト終了で消灯予約と監視を停止します。消灯予約は次の起動へ持ち越しません。PCロックはサインアウトやスリープではありません。

### Watch との対応

- Watch の `OpenCurrentProcessFolder` はプロセスの実行ファイルの場所を開く機能です。WindowsToolsも同じ動作です。エクスプローラーで閲覧しているフォルダーや、エディターで編集中の文書のフォルダー、プロセス内部の作業ディレクトリを取得する機能ではありません。
- Watch の消灯用ショートカットは1秒待機でした。WindowsToolsでは秒数を設定できます。
- Watch の自動ロックはマウスのみ・6時間固定でした。今回の「PCの操作がなかった場合」に合わせて、WindowsToolsは `GetLastInputInfo` によるマウスとキーボードの無操作時間を使い、分単位で変更できます。

### キー送信を追加する

1. AppDock の「設定」から Applet.WindowsTools.at365 を選択します。
2. 「送信するショートカット」で「コマンドを追加」を押します。
3. ID、名前、送信キーを編集して「変更をすべて保存」を押します。
4. 再起動せずコマンドパレットとショートカット一覧に反映されます。

例: ID `new-tab`、名前「新しいタブ」、キー `Ctrl+T`。生成されるコマンドIDは `at365.windows-tools.send.new-tab` です。IDを保てば名前・送信キーを変更しても呼び出し用ショートカットとピン留めを維持できます。IDを変えると別コマンドになり、削除したコマンドは実行できなくなります。再追加に備えてAppDock側のキー割り当てやピンの保存値は残します。

送信キーは `Ctrl` / `Alt` / `Shift` / `Win` と、英数字、F1〜F24、Enter、Tab、Escape、Space、Backspace、Delete、Insert、Home、End、PageUp、PageDown、Left、Right、Up、Down の組み合わせです。例: `Ctrl+Shift+T`、`Alt+F4`、`Win+E`。1コマンドは1組のキー操作です。文字列の入力や従来のSendKeys独自構文（`%{F4}`など）は扱いません。

呼び出し用のキーと送信キーは別の組み合わせにしてください。同じ組み合わせを送ることで再度呼び出されるループを抑止するため、500ms以内のキー送信を拒否します。修飾キーが離れるまで最大2秒待機し、最前面ウィンドウが途中で変わった場合は送信を中止します。送信にはWin32の `SendInput` を使います。管理者権限のアプリや保護された画面への送信にはWindowsの制限があります。

フォルダー表示とキー送信は、**実行時の最前面アプリ**が対象です。AppDockの画面から実行するとAppDock自身が対象になります。別のアプリを操作するときは、そのコマンドへグローバルショートカットを割り当ててください。送信側は `Win` に対応しますが、既存AppDockの呼び出し用キー設定はWindowsキー未対応です。

## ビルド・配置

.NET 10 SDKを使用します。隣接する AppDock.at365 のSDKを既定の参照先としています。

```powershell
dotnet build .\Applet.WindowsTools.at365.slnx -c Release
.\publish.bat
```

別の配置場所にあるAppDockのSDKを使う場合:

```powershell
.\publish.bat -AppDockRoot "A:\30.PROJECT\AppDock.at365"
# ソリューション全体のビルド時は -p:AppDockRoot=... を指定
```

発行先は [publish/Applet.WindowsTools.at365](publish/Applet.WindowsTools.at365/) です。`-OutputDirectory` で変更できます。

動的コマンド対応版AppDockを終了し、そのEXEがあるフォルダーを指定して配置します。

```powershell
.\deploy.bat "A:\Apps\AppDock.at365"
```

引数なしの場合は無視対象の `deploy.local.txt` の1行目を使います。[記入例](deploy.local.txt.example)も参照してください。どちらも未指定なら配置しません。`deploy.bat` は既定の発行先からDLL・deps.json・manifestを `extensions/Applet.WindowsTools.at365` へコピーします。AppDock.SDKはホストが供給します。

AppDockを起動し直してAppletを有効にしてください。既定では自動ロックは無効、キー送信一覧は空です。実利用環境への配置・自動ロックの有効化はこの実装作業では行っていません。

## 検証

```powershell
dotnet run --project .\Applet.WindowsTools.RegressionTests -c Release
```

消灯・ロック・待ち時間のテストは模擬APIと仮想時刻を使用します。

`--native-send` を付けると、テスト専用のテキストボックスへCtrl+Aを送って確認します。一時的にテスト画面へフォーカスを移すので、他のUIテストや手操作と同時に実行しないでください。

[scripts/test-host.cjs](scripts/test-host.cjs) は専用プロファイルでAppDockを起動し、設定画面からの追加・変更・削除、動的ホットキーの登録解除、取消、再起動と停止を検証します。NodeとPlaywrightはAppDock側の依存を使用します。第1引数にホストソースのパス、第2引数に配布版EXEのパスを指定すると配布版でも検証できます。

実測結果は [VERIFICATION.md](VERIFICATION.md) に記載します。
