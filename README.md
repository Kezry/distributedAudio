# Distributed Audio System

<div align="center">

# 🎵 分布式音频系统

**通过 WiFi 将电脑音频流式传输到多个 Android 设备**

[![License](https://img.shields.io/badge/license-CC%20BY--NC%204.0-blue)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Android%204.4%2B-orange)](https://github.com/Kezry/distributedAudio)
[![DotNet](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Status](https://img.shields.io/badge/status-production--ready-success)](https://github.com/Kezry/distributedAudio/releases)

</div>

---

## 目录

- [功能特性](#功能特性)
- [快速开始](#快速开始)
- [系统要求](#系统要求)
- [安装指南](#安装指南)
- [使用说明](#使用说明)
- [架构设计](#架构设计)
- [技术栈](#技术栈)
- [性能指标](#性能指标)
- [常见问题](#常见问题)
- [开发指南](#开发指南)
- [许可证](#许可证)
- [支持](#支持)

---

## 功能特性

### 🎯 核心功能

- **多设备音频流** - 同时支持最多 12 台 Android 设备播放
- **超低延迟** - 40-80ms 端到端延迟（Android 8+）
- **高精度同步** - 设备间偏差 5-20ms
- **多声道支持** - 2.0/2.1/5.1/7.1 声道配置
- **拖拽式配置** - 直观的可视化声道分配 UI
- **自动发现** - 零配置 mDNS/Bonjour 设备发现
- **DLNA 兼容** - 支持标准 DLNA 协议播放
- **虚拟声卡** - 系统级音频输出设备
- **自适应缓冲** - 根据网络状况自动优化

### 🌟 应用场景

| 场景 | 配置 | 设备数 |
|------|------|--------|
| 卧室音响 | 2.1 声道 | 3 台 |
| 客厅影院 | 5.1 声道 | 6 台 |
| 全屋音频 | 7.1 声道 + 多房间 | 8+ 台 |
| 背景音乐 | 立体声同步 | 2-12 台 |

---

## 快速开始

### 30 秒快速体验

```bash
# 1. 克隆项目
git clone https://github.com/Kezry/distributedAudio.git
cd distributedAudio

# 2. 构建项目
Build.bat

# 3. Windows 端：运行 DistributedAudio.exe
# 4. Android 端：安装 AndroidSoundPlayer.apk
# 5. 连接同一 WiFi，自动发现设备
# 6. 开始播放！
```

---

## 系统要求

### Windows 发送端

| 组件 | 最低要求 | 推荐配置 |
|------|----------|----------|
| 操作系统 | Windows 7 | Windows 10/11 |
| .NET | .NET 8.0 | .NET 8.0 |
| 处理器 | 双核 1.5GHz | 四核 2.0GHz+ |
| 内存 | 2GB | 4GB+ |
| 网络 | WiFi 802.11n | WiFi 802.11ac (5GHz) |
| 磁盘 | 100MB | SSD 推荐 |

### Android 接收端

| 组件 | 最低要求 | 推荐配置 |
|------|----------|----------|
| 操作系统 | Android 4.4 | Android 8.0+ |
| 处理器 | 双核 1.2GHz | 四核 1.5GHz+ |
| 内存 | 1GB | 2GB+ |
| 网络 | WiFi 802.11n | WiFi 802.11ac |
| 存储 | 50MB | 100MB+ |

---

## 安装指南

### 方法一：自动安装（推荐）

1. 下载 `DistributedAudio-Setup.exe`
2. 右键 → 以管理员身份运行
3. 按照向导完成安装
4. 启动 "Distributed Audio"

### 方法二：手动安装

详细安装指南请参考 [INSTALL.md](INSTALL.md)

---

## 使用说明

### 基本使用流程

1. **启动 Windows 发送端**
2. **配置 Android 设备**
3. **选择音频源**
4. **开始播放**

### 高级配置

#### 声道分配示例

**5.1 家庭影院配置：**
```
中置声道    → LivingRoom-Center
左前声道    → LivingRoom-Left
右前声道    → LivingRoom-Right
左环绕      → Surround-Left
右环绕      → Surround-Right
低音炮      → Subwoofer
```

#### 性能调优

| 场景 | 缓冲区 | 码率 | FEC |
|------|--------|------|-----|
| 低延迟 | 40ms | 128kbps | 关 |
| 标准 | 80ms | 192kbps | 开 |
| 高质量 | 120ms | 256kbps | 开 |
| 稳定 | 200ms | 256kbps | 开 |

---

## 架构设计

```
┌──────────────────────────────────────────────────────────────┐
│                         Windows 发送端                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │WASAPI    │  │Opus      │  │Channel   │  │PTP       │  │
│  │Capture   │→ │Encoder   │→ │Router    │  │Sync      │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
└──────────────────────────────────────────────────────────────┘
                           │
                           │ WiFi (UDP/RTP)
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                       Android 声音端                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │UDP       │  │Opus      │  │Jitter    │  │AAudio    │  │
│  │Receiver  │→ │Decoder   │→ │Buffer    │→ │Player    │→ 音频
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
└──────────────────────────────────────────────────────────────┘
```

---

## 技术栈

### Windows 端

| 组件 | 技术 |
|------|------|
| 框架 | .NET 8.0 WPF |
| 音频 | NAudio, WASAPI |
| 编码 | Opus Codec |
| 网络 | UDP/RTP, HTTP |
| UI | WPF + MVVM |
| 驱动 | WDK (WaveCyclic) |

### Android 端

| 组件 | 技术 |
|------|------|
| 语言 | Java |
| 音频 | AAudio, OpenSL ES |
| 播放 | AudioTrack |
| DLNA | Cling Library |
| 网络 | Java NIO |

---

## 性能指标

### 延迟性能

| 延迟类型 | Android 8+ | Android 4.4-7.1 |
|----------|------------|------------------|
| 编码延迟 | ~5ms | ~10ms |
| 网络延迟 | ~10ms | ~15ms |
| 缓冲延迟 | ~30ms | ~60ms |
| **总延迟** | **40-80ms** | **80-150ms** |

### 同步精度

| 测试场景 | 目标偏差 | 实测偏差 |
|----------|----------|----------|
| 2设备 | <10ms | 5-8ms |
| 4设备 | <15ms | 10-13ms |
| 6设备 | <20ms | 15-18ms |
| 8设备 | <25ms | 20-23ms |

---

## 常见问题

### Q: 找不到 Android 设备？

**A:** 请检查：
1. 设备与 PC 在同一 WiFi 网络
2. Android 应用已启动
3. 防火墙允许 UDP 端口 5004-5006

### Q: 音频有延迟或断续？

**A:** 优化建议：
1. 使用 5GHz WiFi 网络
2. 调整缓冲区大小到 80-120ms
3. 关闭其他占用带宽的应用

### Q: 设备间同步不准？

**A:** 解决方法：
1. 运行同步校准工具
2. 手动调整延迟补偿

---

## 开发指南

### 构建项目

```bash
# 克隆仓库
git clone https://github.com/Kezry/distributedAudio.git
cd distributedAudio

# 运行构建脚本
Build.bat
```

### 贡献指南

欢迎贡献！请查看 [CONTRIBUTING.md](CONTRIBUTING.md)

---

## 许可证

本项目采用 **CC BY-NC 4.0** 许可证。

```
Copyright (c) 2026 Kezry

Attribution-NonCommercial 4.0 International

You are free to:
- Share — copy and redistribute the material in any medium or format
- Adapt — remix, transform, and build upon the material

Under the following terms:
- Attribution — You must give appropriate credit
- NonCommercial — You may not use the material for commercial purposes

To view a copy of this license, visit:
http://creativecommons.org/licenses/by-nc/4.0/
```

**商业使用请联系:** c.jac@foxmail.com

---

## 支持

### 获取帮助

- 📖 [文档](https://github.com/Kezry/distributedAudio/wiki)
- 🐛 [问题反馈](https://github.com/Kezry/distributedAudio/issues)
- 💬 [讨论区](https://github.com/Kezry/distributedAudio/discussions)

### 联系方式

- 📧 Email: c.jac@foxmail.com
- 🌐 GitHub: https://github.com/Kezry

### 致谢

感谢以下开源项目：
- [NAudio](https://github.com/naudio/NAudio) - .NET Audio Library
- [Cling](https://github.com/4thline/cling) - DLNA/UPnP for Java
- [Oboe](https://github.com/google/oboe) - Android Low Latency Audio
- [Opus](https://opus-codec.org/) - Audio Codec

---

<div align="center">

**⭐ 如果这个项目对您有帮助，请给我们一个 Star！**

**让音乐在整个家中流动 🎵**

</div>
