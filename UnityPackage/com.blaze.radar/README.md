# Blaze Radar SDK

Unity 2021.3+ 的 FaseLase F10/F20 多指针输入包。包内 `Bridge~/win-x64` 已包含完整 self-contained `RadarBridge.exe` 发布目录；Editor 可自动启动，Windows Player 构建时会自动复制到游戏 EXE 旁。Unity 通过 Named Pipe 连接 Bridge，不会直接占用雷达 TCP 端口。

安装、场景配置与构建复制步骤见 `Documentation~/index.md` 或仓库根目录的 `docs/unity-integration.md`。Package Manager 可导入 **Basic Interaction** Sample。
