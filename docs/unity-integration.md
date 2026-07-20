# Unity 集成

面向首次使用者的完整安装、场景配置和验收步骤见 [安装说明与首次使用](../INSTALL.md)。

## 安装

如果项目曾安装 `com.yuexin.radar`，请先移除旧包并删除旧的 `Assets/Samples/Yuexin Radar SDK/1.0.1`；该目录是 Unity 复制到 `Assets` 的 Sample，不会随旧包自动移除。

1. Unity Package Manager 选择 **Install package from git URL**，推荐使用固定版本地址 `https://github.com/blaze-tc/RadarControl.git?path=/UnityPackage/com.blaze.radar#v1.1.0`；也可以选择 **Add package from disk** 并打开 `UnityPackage/com.blaze.radar/package.json`。
2. 等待约 161 MB 的 self-contained Bridge 与 SDK 下载、导入完成。
3. 导入 **Basic Interaction** Sample。
4. 通过 **Tools > Blaze Radar > Create or Select Settings** 创建 Settings；`Editor Bridge Executable` 留空时自动使用包内 Bridge，需要调试其他版本时才设置覆盖路径。
5. 在场景中执行 **GameObject > Blaze Radar > Create Runtime**。

## EventSystem 约束

- 每个 Screen Space Canvas 需要 `GraphicRaycaster`。
- 3D/2D 交互相机分别添加 `PhysicsRaycaster`/`Physics2DRaycaster`。
- 禁用 `StandaloneInputModule`，避免同一 EventSystem 重复派发。
- `RadarInputModule` 支持多 TrackId 独立 Pointer 状态。调试时可选 `RadarAndMouseDebug`，发布时建议 `RadarOnly`。

## Bridge 启动与构建

Launcher 先复用已有 Bridge；否则在 Editor 中从包缓存的 `Bridge~/win-x64/RadarBridge.exe` 启动，在 Player 中从玩家旁的 `RadarBridge/RadarBridge.exe` 启动，并传入 Unity PID。构建后处理器自动把包内完整发布目录复制到：

```text
Game.exe
RadarBridge/
  RadarBridge.exe
  profiles/default-profile.json
  profiles/f20-profile.json
Game_Data/
```

若包内缺少 `RadarBridge.exe`，Unity Build 会明确失败而不是生成缺件包。开发者可运行 `.\scripts\publish-bridge.ps1` 重新生成并同步内嵌目录，或在 **Tools > Blaze Radar > Bridge Publish Directory** 指定其他发布目录。

## 验收

包内有 `Tests/Runtime` PlayMode 测试，覆盖 Button 点击和双指针独立状态；Sample 包含 Button、Toggle、Slider、ScrollRect、3D、2D 和双目标展示。包的 Runtime、Editor 与 Basic Interaction Sample 已在 Unity 2021.3.45f1 中完成真实导入编译；PlayMode 交互与 Windows Player 构建仍应在目标项目内验收。
