# Distributed Audio System - Protocol Documentation

## 1. mDNS 设备发现协议

### 服务定义
- **服务类型:** `_soundplayer._tcp.local.`
- **端口:** 5004 (音频数据)
- **同步端口:** 5005 (PTP 同步)
- **配置端口:** 5006 (HTTP 配置 API)

### TXT 记录格式
```
uuid=<设备UUID>
alias=<设备别名>
mac=<MAC地址>
version=<版本号>
```

### 示例
```
SoundPlayer-ABCD._soundplayer._tcp.local.
port=5004
uuid=123e4567-e89b-12d3-a456-426614174000
alias=Living Room Speaker
mac=00:11:22:33:44:55
version=1.0
```

---

## 2. 音频流传输协议 (RTP 封装)

### RTP Header 格式
```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|V=2|P|X|  CC   |M|     PT      |       sequence number         |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                           timestamp                           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                             SSRC                              |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

### 字段说明
- **Version (V):** 2
- **Padding (P):** 0
- **Extension (X):** 0
- **CSRC Count (CC):** 0
- **Marker (M):** 0 或 1 (帧边界标记)
- **Payload Type (PT):** 动态 (96-127)
- **Sequence Number:** 递增序列号
- **Timestamp:** 微秒时间戳
- **SSRC:** 同步源标识符

### Payload 格式

#### Opus 编码格式
```
+----------------+------------------+
| Payload Header | Opus Data        |
+----------------+------------------+
| Size (2 bytes) | Encoded Audio    |
+----------------+------------------+
```

#### PCM 格式 (未压缩)
```
+----------------------------------+
| PCM Audio Data                   |
| - 16-bit samples                 |
| - Stereo/5.1/7.1 channels        |
| - 48000 Hz sample rate           |
+----------------------------------+
```

---

## 3. 同步协议 (PTP 实现)

### 时间戳格式
- **单位:** 微秒 (μs)
- **基准:** 系统启动时间
- **精度:** 纳秒级

### 同步消息类型

#### SyncRequest (0x01)
```
+--------+--------+--------+--------+
| Type   | T1 (8 bytes)            |
+--------+--------+--------+--------+
```
- **Type:** 0x01
- **T1:** 发送时间戳

#### SyncResponse (0x02)
```
+--------+--------+--------+--------+
| Type   | T2 (8 bytes)            |
+--------+--------+--------+--------+
```
- **Type:** 0x02
- **T2:** 响应时间戳

### 延迟计算
```
RTT = (T4 - T1) - (T2 - T3)
Network Latency = RTT / 2
Clock Offset = (T2 + T3 - T1 - T4) / 2
```

---

## 4. HTTP 控制 API 规范

### 基础 URL
```
http://<设备IP>:5006/api
```

### 端点

#### GET /api/config
获取设备配置

**Response:**
```json
{
  "workMode": "SOUND_CARD",
  "channelMode": "STEREO",
  "alias": "Living Room Speaker",
  "bufferSize": 2048,
  "latency": 100
}
```

#### POST /api/config
设置设备配置

**Request:**
```json
{
  "workMode": "SOUND_CARD",
  "channelMode": "LEFT",
  "alias": "New Name",
  "bufferSize": 4096,
  "latency": 150
}
```

**Response:** 200 OK

#### GET /api/ping
测试设备连接

**Response:** 200 OK

---

## 5. 配置数据格式定义

### WorkMode 枚举
```json
{
  "SOUND_CARD": "声卡模式",
  "DLNA": "DLNA 模式"
}
```

### ChannelMode 枚举
```json
{
  "STEREO": "立体声",
  "LEFT": "左声道",
  "RIGHT": "右声道"
}
```

### 设备信息
```json
{
  "uuid": "string (设备唯一标识)",
  "alias": "string (用户自定义名称)",
  "macAddress": "string (MAC 地址)",
  "host": "string (IP 地址)",
  "port": "int (音频端口)",
  "signalStrength": "int (信号强度 0-100)",
  "latency": "int (网络延迟 ms)",
  "version": "string (软件版本)"
}
```

### 配置信息
```json
{
  "workMode": "SOUND_CARD | DLNA",
  "channelMode": "STEREO | LEFT | RIGHT",
  "alias": "string",
  "bufferSize": "int (字节)",
  "latency": "int (毫秒)"
}
```

---

## 6. 网络要求

### WiFi 要求
- **频段:** 推荐 5GHz (减少干扰)
- **协议:** 802.11n 或更高
- **带宽:** 最少 20 Mbps，推荐 50 Mbps
- **延迟:** < 20ms
- **抖动:** < 10ms

### 端口使用
- **5004:** 音频数据流 (UDP)
- **5005:** 同步控制 (UDP)
- **5006:** 配置 API (HTTP)
- **5353:** mDNS (UDP 多播)

---

## 7. 性能指标

| 指标 | 目标值 | 说明 |
|------|--------|------|
| 端到端延迟 | < 50ms | 从采集到播放 |
| 同步精度 | < 5ms | 多设备间偏差 |
| 丢包率 | < 1% | 网络丢包率 |
| 抖动 | < 10ms | 网络延迟波动 |
| 音频质量 | > 90% | MOS 分数 |

---

## 8. 错误处理

### 错误码
| 错误码 | 描述 |
|--------|------|
| 1000 | 设备未找到 |
| 1001 | 连接超时 |
| 1002 | 配置失败 |
| 1003 | 音频解码错误 |
| 1004 | 同步失败 |

### 错误响应格式
```json
{
  "error": {
    "code": 1001,
    "message": "Connection timeout",
    "details": "Failed to connect to device within 10s"
  }
}
```
