# 虚拟声卡驱动架构设计

## 驱动概述

虚拟声卡驱动使Windows系统可以将分布式音频系统识别为真实的音频输出设备，支持所有应用程序的音频输出。

## 驱动类型选择

### 方案1: WaveRT 端口类驱动 (推荐)
- **优点**:
  - Microsoft官方支持架构
  - Windows 10/11原生兼容
  - 低延迟性能优秀
  - 支持硬件音频卸载
- **缺点**:
  - 开发复杂度高
  - 需要数字签名
  - 仅支持Windows 8+

### 方案2: WaveCyclic 端口类驱动
- **优点**:
  - 兼容Windows 7+
  - 开发相对简单
  - 文档和示例丰富
- **缺点**:
  - 延迟较高
  - 性能不如WaveRT

### 方案3: 用户模式音频引擎 (中间层)
- **优点**:
  - 无需驱动签名
  - 开发和调试简单
  - 快速迭代
- **缺点**:
  - 需要配合物理声卡
  - 系统级应用无法使用
  - 兼容性问题

## 选择: WaveCyclic + 用户模式中间层

采用混合方案:
1. **WaveCyclic端口类驱动** - 提供系统级音频设备
2. **用户模式音频服务** - 连接驱动和网络传输

## 驱动架构

```
┌─────────────────────────────────────────────────────┐
│                    Windows Applications              │
│              (DirectSound/WASAPI/MMSystem)           │
└───────────────────────┬─────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────┐
│               User Mode Audio Subsystem              │
│                  (audiosrv + components)             │
└───────────────────────┬─────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────┐
│                  Port Class Driver                   │
│                  (portcls.sys)                       │
└───────────────────────┬─────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────┐
│           DistributedAudio Virtual Driver            │
│                  (distributedaudio.sys)              │
│  ┌───────────────────────────────────────────────┐  │
│  │  WaveCyclic Miniport Driver                   │  │
│  │  - IRP处理                                    │  │
│  │  - 缓冲区管理                                 │  │
│  │  - 音频格式转换                               │  │
│  └───────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────┐  │
│  │  Shared Memory Interface                      │  │
│  │  - 环形缓冲区                                 │  │
│  │  - 事件同步                                   │  │
│  │  - 状态通知                                   │  │
│  └───────────────────────────────────────────────┘  │
└───────────────────────┬─────────────────────────────┘
                        │ Shared Memory + Events
┌───────────────────────▼─────────────────────────────┐
│           User Mode Audio Service                    │
│           (DistributedAudio.Service.exe)             │
│  ┌───────────────────────────────────────────────┐  │
│  │  Audio Capture Module                         │  │
│  │  - 读取共享内存数据                           │  │
│  │  - 音频编码                                   │  │
│  └───────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────┐  │
│  │  Network Streaming Module                     │  │
│  │  - UDP/RTP发送                                │  │
│  │  - 设备发现                                   │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

## 驱动组件

### 1. 内核模式驱动 (distributedaudio.sys)

**功能:**
- WaveCyclic迷你端口驱动实现
- 音频设备枚举
- IRP请求处理
- 共享内存管理
- 音频流控制

**主要接口:**
```c
// 设备初始化
NTSTATUS DriverEntry(PDRIVER_OBJECT DriverObject, PUNICODE_STRING RegistryPath);
NTSTATUS AddDevice(PDRIVER_OBJECT DriverObject, PDEVICE_OBJECT PhysicalDeviceObject);

// 流操作
NTSTATUS CreateStream(PDEVICE_OBJECT DeviceObject, PIRP Irp);
NTSTATUS CloseStream(PDEVICE_OBJECT DeviceObject, PIRP Irp);
NTSTATUS WriteStream(PDEVICE_OBJECT DeviceObject, PIRP Irp);
NTSTATUS GetStreamPosition(PDEVICE_OBJECT DeviceObject, PIRP Irp);

// 格式支持
NTSTATUS GetDataFormat(PDEVICE_OBJECT DeviceObject, PIRP Irp);
NTSTATUS SetDataFormat(PDEVICE_OBJECT DeviceObject, PIRP Irp);
```

### 2. 共享内存接口

**结构:**
```c
#define SHARED_MEMORY_SIZE (1024 * 1024) // 1MB环形缓冲区

typedef struct _SHARED_MEMORY_HEADER {
    ULONG WriteOffset;        // 写指针
    ULONG ReadOffset;         // 读指针
    ULONG BufferSize;         // 缓冲区大小
    ULONG SampleRate;         // 采样率
    ULONG Channels;           // 声道数
    ULONG BitsPerSample;      // 位深度
    volatile ULONG Active;    // 激活状态
    HANDLE DataReadyEvent;    // 数据就绪事件
} SHARED_MEMORY_HEADER, *PSHARED_MEMORY_HEADER;

typedef struct _SHARED_MEMORY_BUFFER {
    SHARED_MEMORY_HEADER Header;
    BYTE Data[SHARED_MEMORY_SIZE];
} SHARED_MEMORY_BUFFER, *PSHARED_MEMORY_BUFFER;
```

### 3. 用户模式服务

**功能:**
- 打开共享内存
- 监听数据就绪事件
- 读取音频数据
- 编码并发送

## 音频格式支持

| 格式 | 采样率 | 声道 | 位深度 | 状态 |
|------|--------|------|--------|------|
| PCM | 48kHz | 2 (立体声) | 16-bit | ✅ 必须 |
| PCM | 48kHz | 2 (立体声) | 24-bit | ✅ 推荐 |
| PCM | 48kHz | 6 (5.1) | 16-bit | ✅ 推荐 |
| PCM | 48kHz | 8 (7.1) | 16-bit | ✅ 推荐 |
| PCM | 44.1kHz | 2 | 16-bit | ⭐ 可选 |

## 开发工具链

1. **Windows Driver Kit (WDK)**
   - 版本: WDK for Windows 11, Version 22H2
   - 下载: https://learn.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk

2. **Visual Studio 2022**
   - 工作负载: 使用C++的桌面开发
   - 组件: Windows 10 SDK (10.0.22621.0)

3. **Windows Assessment and Deployment Kit (ADK)**
   - 用于驱动签名和测试

4. **Driver Verifier**
   - 驱动验证工具

## 驱动签名策略

### 开发阶段
- 使用测试签名模式
- 禁用驱动签名强制 (bcdedit /set testsigning on)

### 发布阶段
- EV代码签名证书 (约$300-500/年)
- Windows Hardware Dev Center提交
- WHQL认证 (可选，约$200/次)
- 或者使用自签名 + 用户手动安装

## 安装流程

1. **驱动安装包 (INF + CAT + SYS)**
```
distributedaudio.inf    # 安装信息文件
distributedaudio.cat    # 目录文件 (签名)
distributedaudio.sys    # 驱动二进制
```

2. **用户模式服务**
```
DistributedAudio.Service.exe  # 音频捕获服务
DistributedAudio.Config.exe   # 配置工具
```

3. **安装步骤**
   - 复制驱动文件到 System32\drivers
   - 使用pnputil安装驱动
   - 启动用户模式服务
   - 在声音设置中选择虚拟设备

## 兼容性矩阵

| Windows版本 | WaveCyclic | WaveRT | 状态 |
|-------------|-----------|--------|------|
| Windows 7   | ✅ | ❌ | 支持 |
| Windows 8   | ✅ | ⭐ | 推荐 WaveRT |
| Windows 8.1 | ✅ | ⭐ | 推荐 WaveRT |
| Windows 10  | ✅ | ⭐ | 推荐 WaveRT |
| Windows 11  | ✅ | ⭐ | 推荐 WaveRT |

## 性能目标

| 指标 | 目标值 | 测量方法 |
|------|--------|----------|
| 延迟 | <10ms | 延迟测量工具 |
| CPU占用 | <5% | 性能监视器 |
| 内存占用 | <20MB | 任务管理器 |
| 稳定性 | 连续运行7天无崩溃 | 稳定性测试 |

## 开发计划

### Phase 1: 基础驱动 (4-6周)
- [x] 驱动架构设计
- [ ] WaveCyclic迷你端口实现
- [ ] 共享内存接口
- [ ] 基础音频格式支持

### Phase 2: 用户模式服务 (2-3周)
- [ ] 共享内存读取
- [ ] 音频捕获模块
- [ ] 网络传输集成

### Phase 3: 测试和优化 (2-3周)
- [ ] 兼容性测试
- [ ] 性能优化
- [ ] 稳定性测试
- [ ] 驱动签名和打包

**总时间: 8-12周**
