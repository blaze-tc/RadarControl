# RadarControl 安装与首次使用

本文用于把 Blaze Radar SDK 安装到现有 Unity 项目，并验证包内自带的 `RadarBridge.exe` 可以正常启动。推荐使用固定版本 `v1.1.1`，便于团队成员和构建机获得完全相同的内容。

## 1. 环境要求

- Windows 10/11 x64。
- Unity 2021.3 LTS 或更高版本。
- 电脑已经安装 Git，并且在命令行执行 `git --version` 能正常返回版本号。
- 至少预留 500 MB 磁盘空间。UPM 包约 161 MB，Unity 还会在项目缓存中保存一份副本。
- 使用真实 FaseLase F10/F20 时，需要一块可设置静态 IPv4 的有线网卡。

包内已经包含完整的 .NET 8 self-contained `RadarBridge.exe` 运行目录，测试电脑不需要另外安装 .NET Runtime。当前 Bridge 只支持 Windows x64。

### 从 1.0.1 旧包迁移

`v1.1.0` 已把包名从 `com.yuexin.radar` 改为 `com.blaze.radar`，C# 公共命名空间改为 `Blaze.Radar`。如果项目导入过旧版 Sample，Unity 不会在移除旧包时自动删除复制到 `Assets` 的 Sample；它会继续编译并产生旧命名空间错误。升级前请：

1. 在 Package Manager 中移除 `com.yuexin.radar`，或从 `Packages/manifest.json` 删除旧条目。
2. 删除旧的 `Assets/Samples/Yuexin Radar SDK/1.0.1` 目录。
3. 安装下文的 `com.blaze.radar#v1.1.1`。
4. 从 **Blaze Radar SDK** 重新导入 **Basic Interaction** Sample。

Named Pipe 名称 `Yuexin.RadarBridge` 是 Bridge 与 Unity 的通信协议标识，为兼容现有 Bridge 保持不变；不要把它改为包名。

## 2. 推荐安装：Unity Package Manager Git URL

1. 打开目标 Unity 项目。
2. 选择 **Window > Package Manager**。
3. 点击左上角 **+**，选择 **Add package from git URL** 或 **Install package from git URL**。
4. 粘贴以下固定版本地址：

   ```text
   https://github.com/blaze-tc/RadarControl.git?path=/UnityPackage/com.blaze.radar#v1.1.1
   ```

5. 点击 **Add**，等待约 161 MB 的 SDK 和 Bridge 下载、解析与导入完成。
6. 在 Package Manager 中确认出现 **Blaze Radar SDK 1.1.1**。

如需始终跟随最新开发版本，可以使用下面的地址，但正式项目不建议锁定到会变化的 `main`：

```text
https://github.com/blaze-tc/RadarControl.git?path=/UnityPackage/com.blaze.radar#main
```

## 3. 通过 manifest.json 安装

也可以关闭 Unity，在项目的 `Packages/manifest.json` 中将以下条目加入 `dependencies`：

```json
{
  "dependencies": {
    "com.blaze.radar": "https://github.com/blaze-tc/RadarControl.git?path=/UnityPackage/com.blaze.radar#v1.1.1"
  }
}
```

保留项目原有的其他依赖项，不要用上面的示例覆盖整个文件。保存后重新打开 Unity，Package Manager 会自动下载安装。

## 4. 从本地仓库安装

本地开发或无法通过 Unity 直接下载时，可以先克隆仓库：

```powershell
git clone https://github.com/blaze-tc/RadarControl.git
cd RadarControl
git checkout v1.1.1
```

然后在 Unity Package Manager 中选择 **+ > Add package from disk**，打开：

```text
RadarControl/UnityPackage/com.blaze.radar/package.json
```

本地安装会直接引用该目录。移动或删除克隆目录后，Unity 项目将无法继续解析这个包。

## 5. 安装后的场景配置

1. 在 Package Manager 中选择 **Blaze Radar SDK**，展开 **Samples**，导入 **Basic Interaction**。首次接入建议先用 Sample 验证，再集成业务场景。
2. 执行 **Tools > Blaze Radar > Create or Select Settings**。Unity 会创建 `Assets/Resources/RadarRuntimeSettings.asset`。
3. 建议首次测试使用以下设置：

   - `Auto Start`：开启。
   - `Exit Bridge With Unity`：开启。
   - `Pipe Name`：保持 `Yuexin.RadarBridge`。
   - `Editor Bridge Executable`：留空，自动使用包内 Bridge。
   - `Profile Path`：留空，使用 Bridge 的默认用户配置。

4. 打开要接入的场景，执行 **GameObject > Blaze Radar > Create Runtime**。该命令会创建运行对象和 `RadarInputModule`，并禁用已有的 `StandaloneInputModule`。
5. 检查交互对象：

   - Screen Space Canvas 必须有 `GraphicRaycaster`。
   - 3D 交互相机添加 `PhysicsRaycaster`。
   - 2D 交互相机添加 `Physics2DRaycaster`。
   - 一个场景只保留一个启用的 EventSystem 输入模块，避免重复点击。

6. 需要先用鼠标验证 UI 时，在 EventSystem 的 `RadarInputModule` 上把 `Input Mode` 设为 `RadarAndMouseDebug`；真实部署时改回 `RadarOnly`。

## 6. 首次运行与验证

1. 进入 Unity Play Mode。
2. Unity 会从包缓存的 `Bridge~/win-x64/RadarBridge.exe` 自动启动 Bridge。不要只复制或单独运行包内的 EXE；它需要同目录的 DLL 和运行时文件。
3. Unity 左上角调试面板应从 `IPC: DISCONNECTED` 变为 `IPC: CONNECTED`。
4. 没有实体雷达时，在 Bridge 中点击 **启动模拟**，然后观察 Sample 中的指针和交互控件。
5. 使用实体雷达时：

   - 将电脑有线网卡设置为静态 IPv4，例如 `192.168.0.10`、掩码 `255.255.255.0`。
   - 不要把电脑地址设置为雷达地址 `192.168.0.100`。
   - 在 Bridge 中选择该本机 IPv4。
   - 雷达默认地址保持 `192.168.0.100:8487`，选择实际 F10/F20 型号后点击连接。

退出 Play Mode 或关闭 Unity 时，由 Unity 启动的 Bridge 会自动退出。

## 7. Windows Player 构建

正常执行 Windows x64 Build 即可。包内的构建处理器会把完整 Bridge 发布目录复制到游戏 EXE 同级：

```text
Game.exe
Game_Data/
RadarBridge/
  RadarBridge.exe
  RadarBridge.dll
  *.dll
  profiles/
    default-profile.json
    f20-profile.json
```

发布或复制游戏时必须保留整个 `RadarBridge` 目录。只复制 `RadarBridge.exe` 会导致程序无法启动。如果构建时找不到内嵌 Bridge，Unity Build 会直接报错并给出缺失路径。

## 8. 更新与卸载

更新时，在 Package Manager 中移除旧版本后重新添加新的版本标签，或直接修改 `Packages/manifest.json` URL 末尾的标签。使用固定标签时，不要期待 Unity 自动获得 `main` 上的新提交。

卸载时：

1. 退出 Play Mode，确认 `RadarBridge.exe` 已关闭。
2. 在 Package Manager 中选择 **Blaze Radar SDK > Remove**。
3. 如不再需要，可手动删除 `Assets/Resources/RadarRuntimeSettings.asset` 和导入到 `Assets/Samples/` 下的 Basic Interaction Sample。

## 9. 常见安装问题

### Unity 提示无法克隆 Git 仓库

- 在 PowerShell 中执行 `git --version`，确认 Git 可用。
- 确认能访问 `https://github.com/blaze-tc/RadarControl`。
- URL 中必须同时保留 `?path=/UnityPackage/com.blaze.radar` 和 `#v1.1.1`。
- 安装失败后可重启 Unity，再从 Package Manager 重新添加。

### Bridge 没有自动启动

- 确认 Settings 中 `Auto Start` 已开启。
- 确认 `Editor Bridge Executable` 留空；错误的覆盖路径会优先于包内 Bridge。
- 查看 Unity Console 中的 `RadarBridge.exe not found` 路径提示。
- Bridge 日志位于 `%LOCALAPPDATA%/RadarControl/logs/`。

### Bridge 启动但 Unity 一直显示 IPC: DISCONNECTED

- 确认 Unity Settings 和 Bridge 使用相同的 `Pipe Name`，默认值都是 `Yuexin.RadarBridge`。
- 关闭其他正在运行的旧版 Bridge，再重新进入 Play Mode。
- 检查场景中是否存在 `RadarFrameDispatcher` 和 `RadarBridgeLauncher`。

### Bridge 能运行但无法连接雷达

这通常是网卡配置问题，与 UPM 安装无关。按本文第 6 节设置静态 IPv4，并参考 [故障排查与网络配置](docs/troubleshooting.md)。

## 10. 相关链接

- [GitHub 标签 v1.1.1](https://github.com/blaze-tc/RadarControl/tree/v1.1.1)
- [Unity 集成说明](docs/unity-integration.md)
- [故障排查与网络配置](docs/troubleshooting.md)
- [测试报告](docs/test-report.md)
