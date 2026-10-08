# 検証記録

## 2026-10-09: 更新配布物の自動生成

- `codex/update-packages`で発行スクリプトだけを更新。Applet本体の版は0.1.1を維持し、`publish.bat`終了コード0。共通パッカーはAppDock 0.23.0のソースから発行。
- `publish/update.json`のID・版をmanifestと照合し、ZIPのサイズ17100bytesとSHA256 `a6370dad3b1af099e3e3bd3bf8f87d9390f6d9f74c66be854c01db72b12ecee8`を照合。ZIP内3ファイルすべてを通常発行フォルダーとバイト単位で比較し一致。収録: `Applet.WindowsTools.at365.deps.json`, `Applet.WindowsTools.at365.dll`, `extension.json`。
- 旧SDK/旧DLLの生成物が残るWatch・WindowMover・WindowsToolsでは、既存deployと一致する配布内容へ整理する処理を追加。任意のユーザーファイルの再帰削除は行わない。
- 共通検証結果はAppDockの`artifacts/applet-update-packages.json`、発行ログは`artifacts/Applet.WindowsTools.at365-update-publish.log`。実利用先deploy・外部公開・pushは未実施。実GitHub/HTTP(S)/UNC配布先の確認はユーザーが後で行う。Applet固有機能・実アカウント操作の再試験は今回の発行変更の対象外。

## 2026-10-08: 実利用先へのdeploy

- 配置後の実利用について、ユーザーが正常動作を確認したと報告（2026-10-08）。

- ユーザーの明示指示により、AppDockと全6Appletの`deploy.bat`を引数なしで実行し、7件すべて終了コード0。配置先は`A:\00.ESSENTIAL\00.MainTools\AppDock.at365`。5つの.NET Appletは現ソース/SDKで`publish.bat`を先に実行し、Gmailはdeploy内で再発行した。
- AppDock0.16.2、Gmail0.5.1、WallpaperSlideshow0.3.0、Watch0.1.1（native）、WebBrowserTools0.2.4、WindowMover0.2.1、WindowsTools0.1.1を配置。Watchの古いDLL版manifestを配置せず、現ソースのnative版へ更新。
- 配置対象21ファイルのSHA256はすべて発行元と一致。現ソースと配置manifestの版/runtime/entry、minimumHostVersionも照合。settings.json・avatar.png・Gmail accounts.jsonの3ファイルは配置前後のハッシュ不変。
- 配置前後とも関連プロセスなし。実利用アプリは起動していないため、次回起動で反映する。旧ファイル退避は行わず、設定・認証領域を配置スクリプトで変更していない。結果は`../AppDock.at365/artifacts/deploy-2026-10-08-result.json`（本体では`artifacts/deploy-2026-10-08-result.json`）。

## 2026-10-08: 依存パッケージ確認・現SDKへの回帰テスト追従

- 外部NuGet PackageReferenceなし。slnxの`dotnet list package --outdated`も更新なし。参照するAppDockのnpm更新詳細は[本体検証記録](../AppDock.at365/VERIFICATION.md)を参照。
- 現SDKのIUiService.GetImageDirectoryAsyncをテスト用FakeContextに追加（未使用のためNotSupportedException）。修正前のCS0535を解消し、Release build警告0/エラー0、新しいビルドから回帰11/11成功。製品コード・設定・版は変更なし。
- 実ディスプレイOFF・ロック・実アプリへのキー送信、publish/deployは今回実施していない。

2026-10-06 / Windows / .NET SDK 10.0.401

- WindowsTools Releaseビルド: 警告0件・エラー0件。発行成功。
- 回帰テスト: 11/11成功。キー構文、設定の不正値・重複ID、動的追加／変更／削除、消灯の期限・取消・停止、無操作時間・入力後の再監視・32bit時刻周回、自動ロックの有効化／無効化を確認。
- SendInput実動作: テスト専用TextBoxでCtrl+Aによる全選択、フォーカスとカーソル位置の維持を確認。テストのDPIをPerMonitorV2に固定して座標の仮想化を避けた。
- AppDock worktree: 型チェックとビルド成功、既存テストを含む33/33成功。
- Electron実UI + 発行DLL: 空のキー一覧で4コマンド登録、設定一覧からの追加・名前変更・削除、グローバルホットキー登録・解除、削除したコマンドの拒否を確認。
- 3600秒後の消灯を予約し、直後に取り消してパネル更新を確認。Applet再起動・無効化後のコマンド解除、ホスト終了を確認。テストプロファイルのみ使用。
- AppDock v0.4.0のポータブルEXE生成成功。展開済み配布EXE + 発行DLLでも同じUIテストに成功。
- ポータブルEXE自体は専用プロファイルの組み込みスモークテストでWindowsToolsの起動・パネル表示・終了コード0を確認。初回は設定ファイル置換時のEPERMで失敗し、同じプロファイルでの再実行が成功。ポータブルラッパーへのPlaywright直接接続はタイムアウトしたため、UIテストは展開済み配布EXEで実施した。

## 未検証の範囲

- 実PCをロックする操作、実ディスプレイの消灯は実行していない。期限計算と停止・取消は模擬APIで検証。
- 実エクスプローラーの起動、保護されたプロセスの実行ファイル取得、RDP、長時間常駐、スリープ復帰、管理者権限アプリへのキー送信は未検証。
- 実利用先への配置、設定変更、AppDock mainへのマージ、コミットは未実施。
