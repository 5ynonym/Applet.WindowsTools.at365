# Applet.WindowsTools.at365 開発ガイド

利用方法は[README.md](README.md)、実測結果と未確認事項は[VERIFICATION.md](VERIFICATION.md)を参照してください。

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

発行先は `publish/Applet.WindowsTools.at365/` です。`-OutputDirectory` で変更できます。

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

## 実装上の補足

動的コマンド更新と `shortcut-list` を使います。対応APIはAppDock 0.4.0で導入されています。無操作判定には `GetLastInputInfo`、キー送信には `SendInput` を使用します。送信前の物理キー解放は25ms間隔で確認し、最大2秒待機します。送信後は150msだけ実行中を維持し、ホストのグローバルキー再入防止と組み合わせます。

## 文書の更新

READMEには動作環境・導入・操作・設定・利用上の制約を記載します。開発環境・ビルド・テスト・発行・開発者用配置・実装の説明はこのファイル、実測結果と未検証事項はVERIFICATION.mdへ記載します。共通方針は[AppDockのドキュメント方針](../AppDock.at365/docs/documentation.md)を参照してください。
