# 延时启动器（DelayLauncher）

一个单文件 Windows 小工具：**等待指定时间 → 打开指定程序 → 自己退出**。

> 直接下载：[Releases](../../releases/latest)  
> 许可证：[MIT](LICENSE)

## 直接使用

1. 双击 `DelayLauncher.exe`。
2. 设置延迟时间，单位可选“秒 / 分钟 / 小时”（最长 24 小时）。
3. 点“浏览…”选择要启动的程序，也可以把程序直接拖进输入框。
4. 需要的话填写启动参数。
5. 保持勾选“启动目标程序后，本程序自动退出”，点“开始倒计时”。

倒计时结束后，目标程序会启动，本程序约 0.8 秒后自动关闭。倒计时过程中可以点“取消”。

## 自动开始（v1.1 新增，默认开启）

界面上新增了选项：

> **延迟超过 10 秒时，启动即自动倒计时**

默认勾选。开启后：

- 打开程序时会读取上次保存的目标程序和延迟时间；
- 如果目标程序有效，且延迟 **大于 10 秒**，程序会直接开始倒计时，不需要点击“开始倒计时”，也不会弹窗询问；
- 倒计时结束后启动目标程序，并自动退出；
- 取消勾选后，始终需要手动点击“开始倒计时”；
- 首次使用还没有保存的目标程序，或延迟不超过 10 秒时，仍然需要手动点击一次。

设置保存在 `%APPDATA%\DelayLauncher\settings.ini`，因此第一次设置好目标并运行一次后，下次打开即可自动执行。

## 命令行 / 快捷方式

带参数启动时会自动填入并立即开始倒计时：

```bat
DelayLauncher.exe --delay 30 --target "C:\Windows\System32\notepad.exe"
DelayLauncher.exe -d 15 -t "C:\Games\game.exe" -a "-windowed"
DelayLauncher.exe --delay 2m --target "D:\Tools\app.exe" --args "/silent" --silent
```

参数说明：

| 参数 | 说明 |
| --- | --- |
| `--delay`, `-d` | 延迟时间，默认 10 秒；支持 `30`、`30s`、`5m`、`1h`、`00:00:30` |
| `--target`, `-t` | 要启动的程序、文件或网址 |
| `--args`, `-a` | 传给目标程序的参数 |
| `--silent`, `-s` | 不显示界面，延迟结束后启动目标并立即退出 |
| `--noexit` | 启动目标后不退出（仅图形界面有效） |
| `--help`, `-h` | 显示帮助 |

也可以直接把目标程序路径作为第一个位置参数：

```bat
DelayLauncher.exe "C:\App\app.exe" --delay 10
```

### 静默模式示例

创建一个快捷方式，在“目标”里填写：

```
"C:\Tools\DelayLauncher.exe" --delay 1m --target "C:\App\app.exe" --silent
```

双击快捷方式后不会出现界面，1 分钟后启动 `app.exe`，启动器随即退出。

## 注意事项

- 程序是 .NET Framework 4.x 的单文件程序，Windows 10/11 自带运行环境，无需安装。
- 如果目标程序需要管理员权限，启动器会触发 UAC；取消 UAC 会显示启动失败。
- 启动目标时，工作目录会自动设为目标程序所在目录，方便游戏/程序读取同目录文件。
- 静默模式没有界面，运行记录和错误信息写入：

```
%TEMP%\DelayLauncher\launcher.log
```

## 数字签名

`DelayLauncher.exe` 已使用 **Rain Sextet** 的代码签名证书签名：

- 签名者名称：`CN=Rain Sextet, O=Rain Sextet`
- 签名者证书指纹（SHA-1）：`B52A90BB3933D0A0D3CB6B59DF6892EA92912250`
- 签名算法：SHA-256 / Authenticode
- 根证书：`RainSextet-CodeSigning-Root.cer`
- 签名者证书：`RainSextet-CodeSigning.cer`

该证书是**自签名证书**，不是公共 CA 颁发的证书。本机当前用户已信任该根证书，`Get-AuthenticodeSignature` 显示为 `Valid`。换到其他电脑后，如果 Windows 显示“不受信任”，需要导入随附的证书：

```powershell
Import-Certificate -FilePath .\RainSextet-CodeSigning-Root.cer -CertStoreLocation Cert:\CurrentUser\Root
Import-Certificate -FilePath .\RainSextet-CodeSigning.cer -CertStoreLocation Cert:\CurrentUser\TrustedPublisher
```

说明：自签名证书只能证明“签名者名称是 Rain Sextet”，不等同于公共 CA 的身份认证。若需要所有 Windows 电脑默认信任的签名，需要向公共代码签名 CA 申请证书。

验证签名：

```powershell
Get-AuthenticodeSignature .\DelayLauncher.exe | Format-List Status, StatusMessage, SignerCertificate
```

## 从源码构建

需要 Windows 自带的 .NET Framework 4.x 编译器：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

源码文件：

- `DelayLauncher.cs`：全部程序逻辑和界面
- `app.manifest`：DPI 感知和权限声明
- `make-icon.ps1`：生成多尺寸 `app.ico`
- `build.ps1`：编译为 `DelayLauncher.exe`
- `RainSextet-CodeSigning-Root.cer`：签名根证书（用于信任导入）
- `RainSextet-CodeSigning.cer`：签名者公钥证书

注意：重新运行 `build.ps1` 会生成未签名的新 EXE，数字签名会丢失。

## 许可证

本项目使用 [MIT License](LICENSE)，版权归 Rain Sextet 所有。
