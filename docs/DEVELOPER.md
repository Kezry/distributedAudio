# Developer Documentation

## Architecture Overview

Distributed Audio System implements a real-time audio streaming architecture with the following key components:

```
┌─────────────────────────────────────────────────────────────────┐
│                        Windows PC                              │
│                                                                 │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐  │
│  │   Audio      │     │    Audio     │     │   Control    │  │
│  │  Capture     │────▶│   Encoder    │────▶│   Manager    │  │
│  │  (WASAPI)    │     │   (Opus)     │     │  (Channel)   │  │
│  └──────────────┘     └──────────────┘     └──────────────┘  │
│                                │                                │
│  ┌──────────────┐             │             ┌──────────────┐  │
│  │    PTP       │◀─────────────┘             │   DLNA       │  │
│  │   Synchrony  │                             │  Controller   │  │
│  └──────────────┘                             └──────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                                    │
                                    │ RTP/UDP (5004)
                                    │ PTP (5005)
                                    │ HTTP (5006)
                                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Android Device                               │
│                                                                 │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐  │
│  │    RTP       │     │    Audio     │     │   AAudio     │  │
│  │  Receiver    │────▶│   Decoder    │────▶│   Player     │─▶ Speakers
│  │             │     │   (Opus)     │     │              │  │
│  └──────────────┘     └──────────────┘     └──────────────┘  │
│         │                                                       │
│         ▼                                                       │
│  ┌──────────────┐                                               │
│  │    PTP       │                                               │
│  │  Response    │───────────────────────┐                      │
│  │             │                       │                      │
│  └──────────────┘                       ▼                      │
│                                    Clock Sync                  │
└─────────────────────────────────────────────────────────────────┘
```

## Module Details

### 1. Audio Capture (WASAPI)

**Location:** `WindowsSound/AudioCapture/`

**Key Classes:**
- `WasapiCapture` - Main capture engine
- `AudioDevice` - Device representation
- `AudioFormatConfig` - Format configuration

**Implementation:**
```csharp
// Initialize WASAPI in loopback mode
var deviceEnumerator = new MMDeviceEnumerator();
var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
var audioClient = device.AudioClient;
audioClient.Initialize(AudioClientShareMode.Shared,
                        AudioStreamFlags.Loopback,
                        1000000, // 1 second buffer
                        audioClient.MixFormat);

// Capture loop
var capture = new AudioCaptureClient(audioClient);
while (capturing) {
    var buffer = capture.GetBuffer(size);
    ProcessBuffer(buffer, size);
    capture.ReleaseBuffer(size);
}
```

**Performance Optimizations:**
- Use `AUDCLNT_STREAMFLAGS_LOOPBACK` for system audio
- Set `AUDCLNT_BUFFERFLAGS_SERVICE` for lower latency
- Prefer exclusive mode when possible
- Minimize buffer size (10-20ms)

### 2. Audio Encoder (Opus)

**Location:** `WindowsSound/AudioEncoder/`

**Configuration:**
```csharp
encoder = OpusEncoder.Create(sampleRate, channels, application);

// Low latency configuration
encoder.SetBitrate(128000);      // 128 kbps
encoder.SetComplexity(5);        // 0-10, lower = faster
encoder.SetInbandFEC(true);     // Forward Error Correction
encoder.SetPacketLossPerc(5);    // Expected packet loss
encoder.SetMaxBandwidth(OpusBandwidth.Fullband);
encoder.SetDTX(false);           // Discontinuity transmission
encoder.SetVBR(false);          // Constant bitrate for stability
```

**Frame Size:**
- 2.5ms @ 48kHz = 120 samples
- 5ms @ 48kHz = 240 samples
- 10ms @ 48kHz = 480 samples (recommended)
- 20ms @ 48kHz = 960 samples

### 3. Channel Router

**Location:** `WindowsSound/ChannelRouter/`

**Channel Mapping:**
```csharp
public enum ChannelType
{
    Left = 0,
    Right = 1,
    Center = 2,
    LFE = 3,
    LeftSurround = 4,
    RightSurround = 5,
    LeftRear = 6,
    RightRear = 7
}

// Route audio to specific devices
public byte[] RouteAudio(byte[] input, int deviceCount)
{
    // De-interleave channels
    var channels = Deinterleave(input);

    // Apply routing table
    var outputs = new Dictionary<Device, byte[]>();
    foreach (var device in devices)
    {
        outputs[device] = channels[device.AssignedChannel];
    }

    // Re-interleave for each device
    return Interleave(outputs);
}
```

### 4. Network Transport (RTP)

**Location:** `WindowsSound/SyncManager/`

**RTP Packet Format:**
```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|V=2|P|X| CC   |M|     PT      |       Sequence Number         |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                           Timestamp                           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|           Synchronization Source (SSRC) identifier            |
+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
|             Contributing Source (CSRC) identifiers             |
|                              ...                              |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                            Payload                            |
|                            ...                                |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

**Implementation:**
```csharp
public class RtpPacket
{
    private const byte RTP_VERSION = 2;
    private const byte PAYLOAD_TYPE = 96; // Dynamic

    public byte[] CreatePacket(byte[] audioPayload, uint timestamp, ushort seqNo)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RTP Header (12 bytes)
        byte firstByte = (RTP_VERSION << 6) | PAYLOAD_TYPE;
        writer.Write(firstByte);
        writer.Write(seqNo);
        writer.Write(timestamp);
        writer.Write(ssrc);

        // Payload
        writer.Write(audioPayload);

        return ms.ToArray();
    }
}
```

### 5. PTP Synchronization

**Protocol Flow:**
```
Client (Windows)                    Server (Android)
     |                                      |
     |-------- Sync Request (T1) --------->|
     |                                      |
     |<------- Sync Response (T2) ---------|
     |                                      |
     |<------- Follow Up (T3) --------------|
     |                                      |
     |-------- Delay Request (T4) -------->|
     |                                      |
     |<------- Delay Response (T4) ---------|
     |                                      |
```

**Calculations:**
```java
// Offset calculation (from client perspective)
long offset = ((t2 - t1) + (t3 - t4)) / 2;
long delay = (t4 - t1) - (t3 - t2);

// Apply offset to local clock
long correctedTime = System.nanoTime() + offset;
```

### 6. Jitter Buffer

**Location:** `AndroidSoundPlayer/buffer/`

**Adaptive Algorithm:**
```java
public class AdaptiveJitterBuffer {
    private int minBufferSize = 40;   // ms
    private int maxBufferSize = 300;  // ms
    private int targetBufferSize = 100; // ms

    public void adjustBuffer(NetworkCondition condition) {
        if (condition.jitter > 30 || condition.lossRate > 0.05) {
            // High jitter or loss - increase buffer
            targetBufferSize = Math.min(targetBufferSize + 10, maxBufferSize);
        } else if (condition.jitter < 10 && condition.lossRate < 0.01) {
            // Good conditions - decrease buffer for lower latency
            targetBufferSize = Math.max(targetBufferSize - 5, minBufferSize);
        }
    }
}
```

**Packet Loss Concealment (PLC):**
- Opus built-in PLC for packet loss up to 20ms
- For longer losses, use waveform interpolation
- Priority: Left > Right > Center > Surrounds

### 7. Virtual Audio Driver

**Location:** `VirtualAudioDriver/src/`

**Architecture:**
```
User Mode                    Kernel Mode
─────────────────────────────────────────────────
Applications
    │
    ▼
AudioSrv (audiosrv.dll)
    │
    ▼
PortCls (portcls.sys)
    │
    ▼
┌───────────────────────────────┐
│  distributedaudio.sys         │
│  ┌─────────────────────────┐  │
│  │  WaveCyclic Miniport   │  │
│  │  - IRP handling        │  │
│  │  - Buffer management   │  │
│  └─────────────────────────┘  │
│  ┌─────────────────────────┐  │
│  │  Shared Memory         │  │
│  │  - Ring buffer (1MB)   │  │
│  │  - Event sync          │  │
│  └─────────────────────────┘  │
└───────────────────────────────┘
    │ (User mapping)
    ▼
┌───────────────────────────────┐
│  DistributedAudio.Service.exe │
│  - Memory mapped reader       │
│  - Opus encoder               │
│  - Network sender             │
└───────────────────────────────┘
```

## Protocol Specifications

### mDNS Discovery

**Service Type:** `_dlna._tcp.local` or custom `_dist-audio._udp.local`

**TXT Records:**
```
vn= DistributedAudio
mn= DeviceName
cap= streams,timestamp
ver= 1.0
mode= soundcard|dlna_single|dlna_multi
```

### RTP Audio Format

- **Payload Type:** 96 (Dynamic)
- **Clock Rate:** 48000 Hz
- **Encoding:** Opus
- **Frame Size:** 960 samples (10ms @ 48kHz)
- **Channels:** 2 (stereo) or 6/8 (multichannel)

### PTP Protocol

**Port:** 5005 (UDP)

**Messages:**
- `SYNC_REQUEST (0x01)` - T1: Client timestamp
- `SYNC_RESPONSE (0x02)` - T2: Server receive time, T1: Echo back
- `FOLLOW_UP (0x03)` - T3: Server send time
- `DELAY_REQUEST (0x04)` - T4: Client receive time
- `DELAY_RESPONSE (0x05)` - T4: Echo back

### HTTP Control API

**Base URL:** `http://device-ip:5006/api`

**Endpoints:**
```
GET    /device              # Device information
GET    /status             # Current status
POST   /config             # Set configuration
POST   /calibration/start  # Start sync test
POST   /dlna/group         # DLNA group management
```

## Performance Tuning

### Reducing Latency

1. **Use AAudio on Android 8+:**
```java
AudioStreamBuilder builder = new AudioStreamBuilder();
builder.setPerformanceMode(AudioStreamBuilder.PERFORMANCE_MODE_LOW_LATENCY);
builder.setBufferCapacityInFrames(480); // 10ms
```

2. **Optimize Opus encoder:**
```csharp
encoder.SetComplexity(5);        // Lower complexity = faster
encoder.SetPacketLossPerc(5);    // Enable FEC for reliability
encoder.SetDTX(false);          // Disable DTX for consistent timing
```

3. **Thread priorities:**
```java
Process.setThreadPriority(Process.THREAD_PRIORITY_URGENT_AUDIO);
```

### Improving Sync Accuracy

1. **Use PTP instead of NTP:**
   - PTP accuracy: microseconds
   - NTP accuracy: milliseconds

2. **Enable kernel PTP on Linux (if applicable):**
```bash
ethtool -T eth0
```

3. **Minimize interrupt latency:**
```c
// Use real-time priority
pthread_setschedparam(pthread_self(), SCHED_FIFO, &param);
```

### Network Optimization

1. **Use QoS tagging:**
```csharp
socket.SetSocketOption(SocketOptionLevel.IP, 
                        SocketOptionName.TypeOfService,
                        0x88); // IPTOS_PRECENCE_AF41
```

2. **Enable multicast QoS on router:**
- WMM (Wi-Fi Multimedia)
- DSCP marking
- Traffic shaping

## Debugging

### Enable Verbose Logging

**Windows:**
```csharp
// In appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning"
    }
  }
}
```

**Android:**
```java
// In AndroidManifest.xml
android:debuggable="true"

// In code
Log.d("AudioPlayer", "Debug message");
```

### Network Analysis

**Use Wireshark filters:**
```
# RTP packets
udp.port == 5004

# PTP packets
udp.port == 5005

# mDNS
udp.port == 5353
```

### Performance Profiling

**Windows - Performance Monitor:**
- Process > % Processor Time
- Process > Private Bytes
- .NET CLR Memory > # Bytes in all Heaps
- Network Interface > Bytes Total/sec

**Android - Profiler:**
- CPU Profiler
- Memory Profiler
- Network Profiler

## Testing

### Unit Tests

```csharp
[Fact]
public void TestOpusEncoding()
{
    var encoder = OpusEncoder.Create(48000, 2, OpusApplication.Voip);
    var input = GenerateSineWave(48000, 2, 1000, 4800);
    var encoded = encoder.Encode(input, input.Length);
    
    Assert.NotNull(encoded);
    Assert.InRange(encoded.Length, 0, 4000); // Max Opus frame size
}
```

### Integration Tests

```csharp
[Fact]
public async Task TestFullPipeline()
{
    // Arrange
    var capture = new WasapiCapture();
    var encoder = new OpusEncoder();
    var decoder = new OpusDecoder();

    // Act
    capture.Start();
    await Task.Delay(1000);
    var pcm = capture.GetBuffer();
    var opus = encoder.Encode(pcm);
    var decoded = decoder.Decode(opus);

    // Assert
    Assert.NotNull(decoded);
    Assert.Equal(pcm.Length, decoded.Length);
}
```

## Troubleshooting

### Common Issues

**Issue: High latency on Android**
```bash
# Check if low latency mode is enabled
adb shell getprop | grep audio

# Force low latency mode
adb shell setprop audio.low_latency.mode 1
```

**Issue: Packet loss**
```bash
# Check network quality
ping -t -s 1400 <device-ip>

# Check buffer overflow
adb shell "dumpsys media.audio_flinger | grep buffers"
```

**Issue: Sync drift**
```java
// Monitor PTP offset
Log.d("PTP", "Offset: " + clockOffset + "ms");
Log.d("PTP", "Delay: " + networkDelay + "ms");

// Recalibrate if offset > 20ms
if (Math.abs(clockOffset) > 20000) {
    requestCalibration();
}
```

## Additional Resources

- [Opus Codec Documentation](https://opus-codec.org/docs/)
- [Oboe Audio Documentation](https://google.github.io/oboe/)
- [RTP RFC 3550](https://tools.ietf.org/html/rfc3550)
- [PTP RFC 1588](https://tools.ietf.org/html/rfc1588)
- [DLNA Guidelines](https://www.dlna.org/guidelines/)

## Contact

For technical questions:
- Email: c.jac@foxmail.com
- GitHub: https://github.com/Kezry/distributedAudio/issues
