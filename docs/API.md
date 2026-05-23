# Distributed Audio System - HTTP Control API

## 概述

Distributed Audio System 提供 HTTP REST API 用于远程配置和控制 Android 声音设备。

## 基础信息

- **协议**: HTTP/1.1
- **端口**: 5006
- **内容类型**: application/json
- **字符编码**: UTF-8

## API 端点

### 1. 设备信息

获取设备的基本信息和能力。

**请求:**
```
GET /api/device
```

**响应:**
```json
{
  "success": true,
  "data": {
    "deviceId": "550e8400-e29b-41d4-a716-446655440000",
    "alias": "Bedroom Speaker",
    "ipAddress": "192.168.1.100",
    "macAddress": "AA:BB:CC:DD:EE:FF",
    "version": "1.0.0",
    "mode": "soundcard",
    "channel": "stereo",
    "capabilities": {
      "supportsDlna": true,
      "supportsMultiChannel": true,
      "maxSampleRate": 48000,
      "minLatency": 40,
      "maxLatency": 300
    }
  }
}
```

### 2. 设备状态

获取设备当前运行状态。

**请求:**
```
GET /api/status
```

**响应:**
```json
{
  "success": true,
  "data": {
    "isPlaying": true,
    "latencyMs": 45,
    "rssi": -52,
    "packetLossRate": 0.003,
    "bufferLevel": 75,
    "syncOffset": 8,
    "volume": 80
  }
}
```

### 3. 配置设备

设置设备的工作模式、声道等配置。

**请求:**
```
POST /api/config
Content-Type: application/json

{
  "mode": "soundcard",
  "channel": "left",
  "alias": "Bedroom Speaker",
  "dlnaGroupEnabled": false,
  "dlnaDelayMs": 0
}
```

**参数说明:**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| mode | string | 否 | 工作模式: `soundcard`, `dlna_single`, `dlna_multi` |
| channel | string | 否 | 声道: `stereo`, `left`, `right`, `center`, `lfe` 等 |
| alias | string | 否 | 设备别名 |
| dlnaGroupEnabled | boolean | 否 | 是否启用 DLNA 多机组 |
| dlnaDelayMs | int | 否 | DLNA 延迟补偿 (毫秒)，范围 -200 到 200 |

**响应:**
```json
{
  "success": true,
  "message": "Configuration updated",
  "data": {
    "mode": "soundcard",
    "channel": "left",
    "alias": "Bedroom Speaker",
    "dlnaGroupId": "",
    "dlnaDelayMs": 0
  }
}
```

### 4. 同步校准

启动设备同步校准测试。

**请求:**
```
POST /api/calibration/start
```

**响应:**
```json
{
  "success": true,
  "message": "Calibration started",
  "data": {
    "calibrationId": "cal_1234567890",
    "estimatedDuration": 5000,
    "steps": [
      "Playing left channel test tone",
      "Playing right channel test tone",
      "Measuring clock offset",
      "Measuring network delay"
    ]
  }
}
```

**校准完成通知（WebSocket）:**
```json
{
  "event": "calibrationComplete",
  "data": {
    "calibrationId": "cal_1234567890",
    "clockOffset": 8,
    "networkDelay": 25,
    "recommendedDelayMs": 10
  }
}
```

### 5. DLNA 多机组

管理 DLNA 多机组配置。

#### 5.1 创建组

**请求:**
```
POST /api/dlna/group
Content-Type: application/json

{
  "action": "create",
  "groupName": "Living Room",
  "syncTogether": true
}
```

**响应:**
```json
{
  "success": true,
  "data": {
    "groupId": "group_1234567890",
    "groupName": "Living Room",
    "syncTogether": true
  }
}
```

#### 5.2 加入组

**请求:**
```
POST /api/dlna/group
Content-Type: application/json

{
  "action": "join",
  "groupId": "group_1234567890",
  "delayMs": 15
}
```

**响应:**
```json
{
  "success": true,
  "message": "Joined group",
  "data": {
    "groupId": "group_1234567890",
    "position": 1
  }
}
```

#### 5.3 离开组

**请求:**
```
POST /api/dlna/group
Content-Type: application/json

{
  "action": "leave"
}
```

**响应:**
```json
{
  "success": true,
  "message": "Left group"
}
```

#### 5.4 统一起播

**请求:**
```
POST /api/dlna/control
Content-Type: application/json

{
  "action": "play_at",
  "timestamp": 1700000000000,
  "mediaUrl": "http://192.168.1.10:8080/audio.opus"
}
```

**参数说明:**

| 参数 | 类型 | 说明 |
|------|------|------|
| action | string | 操作: `play_at`, `stop`, `pause`, `seek` |
| timestamp | long | 播放时间戳（毫秒），用于同步 |
| mediaUrl | string | 媒体 URL（可选） |

**响应:**
```json
{
  "success": true,
  "data": {
    "scheduledTime": 1700000000100,
    "devices": 6
  }
}
```

### 6. 测试音

播放测试音用于声道验证。

**请求:**
```
POST /api/test/tone
Content-Type: application/json

{
  "channel": "left",
  "frequency": 440,
  "duration": 2000
}
```

**参数说明:**

| 参数 | 类型 | 说明 |
|------|------|------|
| channel | string | 声道: `left`, `right`, `all` |
| frequency | int | 频率 (Hz)，范围 100-10000 |
| duration | int | 持续时间 (ms)，范围 100-10000 |

**响应:**
```json
{
  "success": true,
  "message": "Playing test tone on left channel"
}
```

## WebSocket 事件

服务器通过 WebSocket 推送实时状态更新。

### 连接

```
ws://device-ip:5006/api/events
```

### 事件类型

#### 1. 状态更新

```json
{
  "event": "statusUpdate",
  "timestamp": 1700000000000,
  "data": {
    "isPlaying": true,
    "latencyMs": 45,
    "bufferLevel": 75
  }
}
```

#### 2. 配置变更

```json
{
  "event": "configChanged",
  "timestamp": 1700000000000,
  "data": {
    "field": "mode",
    "oldValue": "stereo",
    "newValue": "left"
  }
}
```

#### 3. 错误通知

```json
{
  "event": "error",
  "timestamp": 1700000000000,
  "data": {
    "code": "BUFFER_UNDERFLOW",
    "message": "Buffer underrun detected",
    "severity": "warning"
  }
}
```

## 错误码

| 错误码 | 说明 |
|--------|------|
| 400 | 请求参数错误 |
| 404 | 端点不存在 |
| 500 | 服务器内部错误 |
| 503 | 服务不可用 |

### 错误响应格式

```json
{
  "success": false,
  "error": {
    "code": 400,
    "message": "Invalid parameter: channel must be 'left' or 'right'",
    "details": {
      "field": "channel",
      "value": "center",
      "validValues": ["left", "right", "stereo"]
    }
  }
}
```

## 速率限制

为了防止滥用，API 实施以下速率限制：

- 普通请求: 100 次/分钟
- 配置更改: 10 次/分钟
- 测试音: 6 次/分钟

超过限制将返回 `429 Too Many Requests`。

## 示例代码

### Python 示例

```python
import requests
import json

class AudioDevice:
    def __init__(self, ip):
        self.base_url = f"http://{ip}:5006/api"
        self.session = requests.Session()

    def get_info(self):
        response = self.session.get(f"{self.base_url}/device")
        return response.json()['data']

    def set_config(self, mode, channel):
        payload = {
            "mode": mode,
            "channel": channel
        }
        response = self.session.post(f"{self.base_url}/config", json=payload)
        return response.json()

    def start_calibration(self):
        response = self.session.post(f"{self.base_url}/calibration/start")
        return response.json()

# 使用示例
device = AudioDevice("192.168.1.100")
info = device.get_info()
print(f"Device: {info['alias']}")
print(f"Mode: {info['mode']}")

device.set_config("soundcard", "left")
result = device.start_calibration()
print(f"Calibration: {result['message']}")
```

### JavaScript 示例

```javascript
class AudioDevice {
    constructor(ip) {
        this.baseUrl = `http://${ip}:5006/api`;
    }

    async getInfo() {
        const response = await fetch(`${this.baseUrl}/device`);
        const data = await response.json();
        return data.data;
    }

    async setConfig(config) {
        const response = await fetch(`${this.baseUrl}/config`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(config)
        });
        return await response.json();
    }

    async playTestTone(channel) {
        const response = await fetch(`${this.baseUrl}/test/tone`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                channel: channel,
                frequency: 440,
                duration: 2000
            })
        });
        return await response.json();
    }
}

// 使用示例
(async () => {
    const device = new AudioDevice('192.168.1.100');
    const info = await device.getInfo();
    console.log(`Device: ${info.alias}`);

    await device.setConfig({
        mode: 'soundcard',
        channel: 'left'
    });

    await device.playTestTone('left');
    console.log('Playing test tone...');
})();
```

### C# 示例

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class AudioDeviceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public AudioDeviceClient(string ipAddress)
    {
        _httpClient = new HttpClient();
        _baseUrl = $"http://{ipAddress}:5006/api";
    }

    public async Task<DeviceInfo> GetInfoAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/device");
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<DeviceInfo>>(content);
        return result.Data;
    }

    public async Task<bool> SetConfigAsync(DeviceConfig config)
    {
        var json = JsonConvert.SerializeObject(config);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/config", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<object>>(responseContent);

        return result.Success;
    }

    public async Task<CalibrationResult> StartCalibrationAsync()
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/calibration/start", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<CalibrationResult>>(content);
        return result.Data;
    }
}

// 使用示例
public async Task ConfigureDevice()
{
    var client = new AudioDeviceClient("192.168.1.100");
    var info = await client.GetInfoAsync();
    Console.WriteLine($"Device: {info.Alias}");

    var config = new DeviceConfig
    {
        Mode = "soundcard",
        Channel = "left"
    };

    var success = await client.SetConfigAsync(config);
    if (success)
    {
        Console.WriteLine("Configuration updated!");
    }
}
```

## 最佳实践

1. **使用连接池** - 重用 HTTP 连接以提高性能
2. **实现重试逻辑** - 网络请求可能失败
3. **缓存设备信息** - 减少不必要的 API 调用
4. **使用 WebSocket** - 实时状态更新
5. **处理速率限制** - 尊重服务器的速率限制

## 更新日志

### v1.0.0 (2026-05-24)
- 初始 API 发布
- 支持设备信息、状态、配置端点
- DLNA 多机组管理
- 同步校准 API
- 测试音控制
