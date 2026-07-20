# 故障排查与网络配置

## Bridge 无法连接雷达

1. 给有线网卡设置静态 IPv4，例如 `192.168.0.10`、掩码 `255.255.255.0`；不要把本机地址设为雷达的 `192.168.0.100`。
2. 在 Bridge 的“本机网卡”选择该 IPv4，设备地址保持 `192.168.0.100:8487`。
3. 暂时禁用会抢占同网段路由的其他网卡，确认网线与供电。
4. 查看 `%LOCALAPPDATA%/RadarControl/logs/RadarBridge-YYYYMMDD.log` 中的绑定地址、重连和超时记录。

## 有连接但没有有效点

- 确认型号为 F10/F20 中的实际型号，量程没有设得过小。
- 检查有效角度、有效多边形、四边死区和屏蔽区；可逐层开启 Raw/Valid/Cluster/Target 视图定位在哪一步被过滤。
- CRC 错误持续增长通常说明收到的不是本说明书对应协议、链路数据损坏或存在额外前导数据。

## Unity 无输入

- 先确认 Bridge 的 Unity 状态已握手，并且 PipeName 两侧一致。
- 确认场景只有一个有效输入模块，Canvas/Camera 上有对应 Raycaster。
- 确认 Homography 有效且目标归一化坐标在 `[0,1]`。
- Player 包必须把完整 `RadarBridge` 目录放在 exe 同级；只复制 exe 不够。

## 配置与日志位置

- 默认配置：`%LOCALAPPDATA%/Yuexin/RadarBridge/config.json`
- 日志：`%LOCALAPPDATA%/RadarControl/logs/`
- 可用 `RadarBridge.exe --profile <path>` 指定独立配置。

删除或移走损坏配置后会回到 F10 默认值。保留问题现场时请同时提供配置、当日日志和 `.radarrec`，不要只提供截图。
