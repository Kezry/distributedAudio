# Distributed Audio System - 安装指南

## 系统要求

- **操作系统:** Windows 7 或更高版本 (推荐 Windows 10/11)
- **.NET:** .NET 8.0 Desktop Runtime
- **网络:** WiFi 局域网 (支持多播)
- **音频:** Windows 兼容的音频设备
- **Android 设备:** Android 4.4 或更高版本

## 安装步骤

### 方法 1: 使用安装程序 (推荐)

1. 下载 `DistributedAudio-Setup.exe`
2. 右键点击，选择"以管理员身份运行"
3. 按照安装向导完成安装
4. 如需虚拟声卡驱动，选择"Install virtual audio driver"组件
5. 安装完成后，从开始菜单启动"Distributed Audio"

### 方法 2: 手动安装

#### 1. 安装 Windows 发送端

```powershell
# 解压发布包到目标目录
# 例如: C:\Program Files\DistributedAudio

# 将应用目录添加到 PATH (可选)
[System.Environment]::SetEnvironmentVariable('Path', $env:Path + ';C:\Program Files\DistributedAudio', 'Machine')
```

#### 2. (可选) 安装虚拟声卡驱动

**警告:** 虚拟声卡驱动需要特殊签名。测试模式下可使用以下步骤：

```batch
# 以管理员身份运行
cd Installer
InstallDriver.bat

# 重启 Windows
# 重启后运行
InstallDriverAfterReboot.bat
```

**启用测试签名模式的注意事项:**
- 仅用于开发/测试环境
- 允许加载未签名的驱动
- 系统启动时会显示"测试模式"水印
- 生产环境需要有效签名的驱动

#### 3. 安装 Android 声音端

在 Android 设备上安装 `AndroidSoundPlayer.apk`:

```bash
# 通过 USB 安装
adb install AndroidSoundPlayer.apk

# 或者在设备上直接安装 APK 文件
```

#### 4. 安装 Android 配置端 (可选)

```bash
# 通过 USB 安装
adb install AndroidController.apk
```

## 配置

### Windows 端配置

1. 启动 Distributed Audio 应用
2. 应用会自动扫描网络中的 Android 设备
3. 为每个设备配置:
   - 工作模式 (声卡模式 / DLNA 单机 / DLNA 多机)
   - 声道分配 (立体声 / 左声道 / 右声道)
   - 延迟补偿

### 声道配置示例

#### 2.1 配置 (卧室音响)
```
左声道设备: Android-Device-1
右声道设备: Android-Device-2
低音炮: Android-Device-3
```

#### 5.1 配置 (家庭影院)
```
中置: Android-Device-1
左前: Android-Device-2
右前: Android-Device-3
左环绕: Android-Device-4
右环绕: Android-Device-5
低音炮: Android-Device-6
```

### Android 端配置

#### 声音端 (AndroidSoundPlayer)

1. 打开应用
2. 设置设备别名
3. 应用会自动发布 mDNS 服务

#### 配置端 (AndroidController)

1. 扫描网络中的声音设备
2. 点击设备进入详情页
3. 配置工作模式和声道
4. 测试播放和同步

## 防火墙配置

应用会自动配置防火墙规则。如需手动配置:

```batch
# 添加应用规则
netsh advfirewall firewall add rule name="Distributed Audio" ^
  dir=in action=allow program="C:\Program Files\DistributedAudio\DistributedAudio.exe"

# 添加 UDP 端口规则
for %p in (5004 5005 5006) do (
  netsh advfirewall firewall add rule name="Distributed Audio UDP %p" ^
    dir=in action=allow protocol=UDP localport=%p
)
```

## 卸载

### 使用安装程序卸载

1. 从"程序和功能"中卸载 "Distributed Audio System"
2. 或运行安装包选择卸载选项

### 手动卸载

```batch
# 停止并删除服务
sc stop DistributedAudioService
sc delete DistributedAudioService

# 卸载驱动 (如果已安装)
cd Installer
UninstallDriver.bat

# 删除应用文件
rd /s /q "C:\Program Files\DistributedAudio"

# 删除注册表项
reg delete "HKLM\SOFTWARE\Kezry\DistributedAudio" /f
```

## 故障排除

### 问题: 找不到 Android 设备

**解决方案:**
1. 确保 Android 设备与 PC 在同一 WiFi 网络
2. 检查防火墙设置
3. 尝试手动输入设备 IP 地址

### 问题: 音频断续或有延迟

**解决方案:**
1. 检查 WiFi 信号强度
2. 使用 5GHz WiFi 频段
3. 调整缓冲区大小设置
4. 关闭其他占用带宽的应用

### 问题: 设备间同步不准

**解决方案:**
1. 运行同步校准工具
2. 手动调整延迟补偿
3. 使用有线网络连接

### 问题: 驱动无法加载

**解决方案:**
1. 确保以管理员身份运行
2. 启用测试签名模式并重启
3. 检查 Windows 版本兼容性
4. 查看 Device Manager 中的错误代码

## 性能优化建议

### 网络优化

- 使用专用 WiFi 通道 (避免 2.4GHz 拥挤)
- 启用 QoS 服务
- 禁用 WiFi 省电模式
- 使用 5GHz WiFi 频段

### 音频质量

- 使用较高码率 (128-256 kbps)
- 启用 FEC (前向纠错)
- 调整缓冲区大小 (40-150ms)
- 使用低延迟模式

### 系统优化

- 关闭不必要的后台应用
- 设置高性能电源计划
- 禁用 Windows 自动更新 (使用时)
- 调整实时优先级

## 更新

### 自动更新

应用会检查更新并提示下载新版本。

### 手动更新

1. 从 GitHub 下载最新版本
2. 卸载旧版本 (配置会保留)
3. 安装新版本
4. 恢复配置 (如果需要)

## 许可证

本项目采用 CC BY-NC 4.0 许可证。

**非商业使用:** 个人和教育用途免费

**商业使用:** 需要书面授权

联系: c.jac@foxmail.com

## 支持

- 问题反馈: [GitHub Issues](https://github.com/Kezry/distributedAudio/issues)
- 文档: [项目 Wiki](https://github.com/Kezry/distributedAudio/wiki)
- 邮件: c.jac@foxmail.com
