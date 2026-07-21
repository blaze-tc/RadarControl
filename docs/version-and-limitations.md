# 版本与已知限制

## 1.1.4

- Bridge：`BridgeVersion.Value = 1.1.4`
- Unity SDK：`UnitySdkVersion.Value = 1.1.4`，与 `package.json` 一致
- Unity 包名：`com.blaze.radar`；公共 C# 命名空间：`Blaze.Radar`
- IPC：`IpcProtocolVersion.Current = 1`
- Basic Interaction：包含有界真机日志、帧序号/时间/丢帧统计、逐指针数据及 UGUI/2D/3D EventSystem 回调。
- Bridge 可视化：区域 1 固定显示原始雷达点；区域 2 显示变换/过滤后的有效点、目标和可编辑输出区域，点云短时余辉只影响显示。
- Windows 显示：显式使用 Per-Monitor V2 DPI、ClearType 与像素对齐，适配投影电脑和不同缩放比例的多显示器。

IPC 主版本不一致时握手会返回明确 Error，客户端不得继续消费业务帧。Bridge 与 Unity SDK 可独立修订，但修改帧结构或语义时必须同时升级 IPC 版本和兼容测试。

## 已知限制

- 第一版只读厂家点数据；没有厂家文档支持的设备写协议，因此不设置雷达 IP、网关、掩码、扫描频率、输出角度或马达状态。
- 仅支持 Windows x64，进程间通信使用 Windows Named Pipe。
- UPM Git 下载包含约 161 MB 的 self-contained Windows Bridge；这是免安装 .NET Runtime 的代价。
- 当前验证使用模拟数据、loopback TCP 和共享源码兼容测试；没有实体 F10/F20 的 8 小时稳定性证据。
- UPM 包以 Unity 2021.3 为最低声明版本；Runtime、Editor 与 Basic Interaction Sample 已在 Unity 2021.3.45f1 中真实导入并编译。包内 PlayMode 测试和 Windows Player 构建仍需在集成项目执行。
- 标定与屏蔽区依赖实际安装几何；仓库无法预置某个场地的角点。
- `.radarrec` 保存原始 TCP 块和连接状态，不等同于设备厂商原始文件格式。
