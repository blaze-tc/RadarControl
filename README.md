# RadarControl

FaseLase F10/F20 雷达桥接程序与 Unity 多指针交互 SDK。默认型号为 F10；WPF `RadarBridge.exe` 独占雷达 TCP 连接，Unity 只通过 Named Pipe 消费标准化指针帧。

## 快速开始

环境：Windows 10/11 x64、.NET 8 SDK；Unity 包最低版本为 Unity 2021.3 LTS。

```powershell
.\scripts\build.ps1
.\scripts\test.ps1 -NoBuild
.\scripts\publish-bridge.ps1
```

发布结果位于 `artifacts/publish/RadarBridge/win-x64/`。首次连接前，将电脑有线网卡设置为与雷达同网段的静态 IPv4（例如 `192.168.0.10/24`），在 Bridge 中选择该本机地址；雷达默认端点为 `192.168.0.100:8487`。

Unity 可通过 Package Manager 的本地包方式导入 `UnityPackage/com.yuexin.radar`，再导入 **Basic Interaction** Sample。完整步骤见 [Unity 集成](docs/unity-integration.md)。

## 仓库内容

- `src/`：Contracts、设备连接、配置、协议、处理、IPC 与 WPF Bridge。
- `tests/`：77 项 .NET 自动化测试，以及 Unity 共享解码源码兼容测试。
- `UnityPackage/com.yuexin.radar/`：可直接导入的 UPM 包、Editor 工具、PlayMode 测试和 Sample。
- `config/default-profile.json` / `f20-profile.json`：F10 默认配置与 F20 切换模板。
- `scripts/`：构建、测试和 win-x64 发布脚本。

## 文档

- [架构与线程模型](docs/architecture.md)
- [雷达及 IPC 协议](docs/protocol.md)
- [区域与四点标定](docs/calibration.md)
- [Unity 集成](docs/unity-integration.md)
- [故障排查与网络配置](docs/troubleshooting.md)
- [版本与已知限制](docs/version-and-limitations.md)
- [测试报告与现场门禁](docs/test-report.md)
- [原始执行说明书](RadarControl_Codex_Execution_Spec.md)
- `F10、F20说明书V9.1.1.pdf`

## 当前验证边界

Release 全解决方案已达到 0 警告、77/77 自动测试通过。当前工作区没有实体 F10/F20 和宿主 Unity 工程，因此 8 小时真机稳定性、真实场地标定与 Unity PlayMode/构建验收仍需现场执行；仓库已包含对应测试与检查清单。
