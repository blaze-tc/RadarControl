# Unity 集成

## 安装

1. 先运行 `.\scripts\publish-bridge.ps1`。
2. Unity Package Manager 选择 **Add package from disk**，打开 `UnityPackage/com.yuexin.radar/package.json`。
3. 导入 **Basic Interaction** Sample。
4. 通过 **Tools > Yuexin Radar > Create or Select Settings** 设置 Editor 中的 `RadarBridge.exe` 路径。
5. 在场景中执行 **GameObject > Yuexin Radar > Create Runtime**。

## EventSystem 约束

- 每个 Screen Space Canvas 需要 `GraphicRaycaster`。
- 3D/2D 交互相机分别添加 `PhysicsRaycaster`/`Physics2DRaycaster`。
- 禁用 `StandaloneInputModule`，避免同一 EventSystem 重复派发。
- `RadarInputModule` 支持多 TrackId 独立 Pointer 状态。调试时可选 `RadarAndMouseDebug`，发布时建议 `RadarOnly`。

## Bridge 启动与构建

Launcher 先复用已有 Bridge；否则从 Editor 设置路径或玩家旁的 `RadarBridge/RadarBridge.exe` 启动，并传入 Unity PID。构建后处理器复制整个 publish 目录到：

```text
Game.exe
RadarBridge/
  RadarBridge.exe
  profiles/default-profile.json
  profiles/f20-profile.json
Game_Data/
```

若源目录缺少 `RadarBridge.exe`，Unity Build 会明确失败而不是生成缺件包。可在 **Tools > Yuexin Radar > Bridge Publish Directory** 指定其他发布目录。

## 验收

包内有 `Tests/Runtime` PlayMode 测试，覆盖 Button 点击和双指针独立状态；Sample 包含 Button、Toggle、Slider、ScrollRect、3D、2D 和双目标展示。当前仓库没有宿主 Unity 工程，必须在目标项目内实际运行这些测试与 Windows Player 构建。
