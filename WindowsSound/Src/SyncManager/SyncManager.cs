using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedAudio.SyncManager
{
    /// <summary>
    /// 音频同步管理器
    /// 实现 PTP (Precision Time Protocol) 进行多设备音频同步
    /// </summary>
    public class AudioSyncManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, DeviceLatencyInfo> _latencyInfo = new();
        private readonly ConcurrentDictionary<string, int> _delayCompensations = new();
        private UdpClient? _syncSocket;
        private readonly CancellationTokenSource _cts = new();
        private Task? _receiveTask;
        private readonly Stopwatch _systemClock = Stopwatch.StartNew();
        private bool _disposed;

        public event EventHandler<DeviceLatencyInfo>? LatencyMeasured;
        public event EventHandler<string>? DeviceSynchronized;
        public event EventHandler<Exception>? SyncError;

        public bool IsRunning { get; private set; }
        public int DefaultLatencyMs { get; set; } = 100;

        /// <summary>
        /// 启动同步服务
        /// </summary>
        public async Task StartAsync(int syncPort = 5005)
        {
            if (IsRunning) return;

            try
            {
                _syncSocket = new UdpClient(syncPort);
                IsRunning = true;

                // Start receive loop
                _receiveTask = Task.Run(ReceiveLoop);

                // Start periodic sync
                _ = Task.Run(PeriodicSyncLoop);
            }
            catch (Exception ex)
            {
                SyncError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// 停止同步服务
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            _cts.Cancel();
            _syncSocket?.Close();
            _receiveTask?.Wait(1000);
        }

        /// <summary>
        /// 测量设备延迟
        /// </summary>
        public async Task<DeviceLatencyInfo?> MeasureDeviceLatencyAsync(string deviceId, IPEndPoint endPoint)
        {
            try
            {
                var t1 = GetPreciseTime();

                // 发送同步请求
                var syncRequest = new SyncMessage
                {
                    Type = SyncMessageType.SyncRequest,
                    Timestamp = t1,
                    Data = System.Text.Encoding.UTF8.GetBytes(deviceId)
                };

                var requestData = syncRequest.Serialize();
                using var client = new UdpClient();
                await client.SendAsync(requestData, requestData.Length, endPoint);

                // 等待响应
                var receiveResult = await client.ReceiveAsync();
                var t4 = GetPreciseTime();

                var response = SyncMessage.Deserialize(receiveResult.Buffer);
                if (response?.Type == SyncMessageType.SyncResponse)
                {
                    var t2 = response.Timestamp;

                    // 计算网络延迟
                    int rtt = (int)((t4 - t1) / 10000); // Convert to ms

                    // 解析处理延迟
                    int processingDelay = 0;
                    if (response.Data.Length >= 4)
                    {
                        processingDelay = BitConverter.ToInt32(response.Data, 0);
                    }

                    var latencyInfo = new DeviceLatencyInfo
                    {
                        DeviceId = deviceId,
                        NetworkLatencyMs = rtt / 2,
                        ProcessingLatencyMs = processingDelay,
                        TotalLatencyMs = (rtt / 2) + processingDelay,
                        ClockOffsetMs = 0, // TODO: Implement clock offset calculation
                        LastMeasured = DateTime.Now
                    };

                    _latencyInfo[deviceId] = latencyInfo;

                    // 更新延迟补偿
                    _delayCompensations[deviceId] = latencyInfo.TotalLatencyMs;

                    LatencyMeasured?.Invoke(this, latencyInfo);
                    DeviceSynchronized?.Invoke(this, deviceId);

                    return latencyInfo;
                }
            }
            catch (Exception ex)
            {
                SyncError?.Invoke(this, ex);
            }

            return null;
        }

        /// <summary>
        /// 批量测量多个设备的延迟
        /// </summary>
        public async Task<Dictionary<string, DeviceLatencyInfo>> MeasureMultipleDevicesAsync(
            Dictionary<string, IPEndPoint> devices)
        {
            var results = new Dictionary<string, DeviceLatencyInfo>();
            var tasks = devices.Select(d => MeasureDeviceLatencyAsync(d.Key, d.Value));

            var latencyInfos = await Task.WhenAll(tasks);

            foreach (var info in latencyInfos.Where(i => i != null))
            {
                results[info!.DeviceId] = info;
            }

            return results;
        }

        /// <summary>
        /// 获取设备的延迟补偿
        /// </summary>
        public int GetDelayCompensation(string deviceId)
        {
            if (_delayCompensations.TryGetValue(deviceId, out var delay))
            {
                return delay;
            }
            return DefaultLatencyMs;
        }

        /// <summary>
        /// 设置设备的延迟补偿
        /// </summary>
        public void SetDelayCompensation(string deviceId, int delayMs)
        {
            _delayCompensations[deviceId] = Math.Max(0, delayMs);
        }

        /// <summary>
        /// 获取设备的延迟信息
        /// </summary>
        public DeviceLatencyInfo? GetLatencyInfo(string deviceId)
        {
            return _latencyInfo.TryGetValue(deviceId, out var info) && info.IsValid ? info : null;
        }

        /// <summary>
        /// 获取所有设备的延迟信息
        /// </summary>
        public IReadOnlyList<DeviceLatencyInfo> GetAllLatencyInfo()
        {
            return _latencyInfo.Values.Where(info => info.IsValid).ToList();
        }

        /// <summary>
        /// 计算最大延迟（用于同步所有设备）
        /// </summary>
        public int GetMaxLatency()
        {
            var validLatencies = _latencyInfo.Values
                .Where(info => info.IsValid)
                .Select(info => info.TotalLatencyMs)
                .ToList();

            if (validLatencies.Any())
            {
                return validLatencies.Max();
            }

            return DefaultLatencyMs;
        }

        /// <summary>
        /// 调整所有设备的延迟到最大值
        /// </summary>
        public void AlignAllDelays()
        {
            var maxLatency = GetMaxLatency();

            foreach (var kvp in _latencyInfo)
            {
                if (kvp.Value.IsValid)
                {
                    var additionalDelay = maxLatency - kvp.Value.TotalLatencyMs;
                    _delayCompensations[kvp.Key] = kvp.Value.TotalLatencyMs + additionalDelay;
                }
            }
        }

        /// <summary>
        /// 获取精确时间戳（微秒）
        /// </summary>
        private ulong GetPreciseTime()
        {
            return (ulong)(_systemClock.ElapsedTicks * 1000000 / Stopwatch.Frequency);
        }

        /// <summary>
        /// 接收循环
        /// </summary>
        private async Task ReceiveLoop()
        {
            while (!_cts.IsCancellationRequested && IsRunning)
            {
                try
                {
                    if (_syncSocket != null)
                    {
                        var result = await _syncSocket.ReceiveAsync();
                        ProcessSyncMessage(result.Buffer, result.RemoteEndPoint);
                    }
                }
                catch when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (IsRunning)
                    {
                        SyncError?.Invoke(this, ex);
                    }
                }
            }
        }

        /// <summary>
        /// 处理同步消息
        /// </summary>
        private void ProcessSyncMessage(byte[] data, IPEndPoint endPoint)
        {
            var message = SyncMessage.Deserialize(data);
            if (message == null) return;

            if (message.Type == SyncMessageType.SyncRequest)
            {
                // Respond to sync request
                var response = new SyncMessage
                {
                    Type = SyncMessageType.SyncResponse,
                    Timestamp = GetPreciseTime(),
                    Data = BitConverter.GetBytes(0) // Processing delay placeholder
                };

                var responseData = response.Serialize();
                _syncSocket?.SendAsync(responseData, responseData.Length, endPoint);
            }
        }

        /// <summary>
        /// 定期同步循环
        /// </summary>
        private async Task PeriodicSyncLoop()
        {
            while (!_cts.IsCancellationRequested && IsRunning)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    // Re-measure latency for all devices
                    // This is handled by the main application calling MeasureDeviceLatencyAsync
                }
                catch when (_cts.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 计算同步精度
        /// </summary>
        public (double maxDriftMs, double avgDriftMs) CalculateSyncPrecision()
        {
            var validLatencies = _latencyInfo.Values
                .Where(info => info.IsValid)
                .ToList();

            if (!validLatencies.Any())
                return (0, 0);

            var avgLatency = validLatencies.Average(info => info.TotalLatencyMs);
            var maxDrift = validLatencies.Max(info => Math.Abs(info.TotalLatencyMs - avgLatency));

            return (maxDrift, avgLatency);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _cts.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 动态缓冲区管理器
    /// 根据网络条件动态调整缓冲区大小
    /// </summary>
    public class DynamicBufferManager
    {
        private readonly ConcurrentDictionary<string, BufferState> _bufferStates = new();
        private const int MinBufferSize = 50;  // ms
        private const int MaxBufferSize = 500; // ms
        private const int DefaultBufferSize = 100; // ms

        public int GetBufferSize(string deviceId)
        {
            var state = _bufferStates.GetOrAdd(deviceId, _ => new BufferState
            {
                CurrentSize = DefaultBufferSize,
                PacketLossCount = 0,
                JitterSamples = new Queue<int>(100)
            });

            return state.CurrentSize;
        }

        public void UpdateBufferState(string deviceId, bool packetLost, int jitterMs)
        {
            var state = _bufferStates.GetOrAdd(deviceId, _ => new BufferState
            {
                CurrentSize = DefaultBufferSize,
                PacketLossCount = 0,
                JitterSamples = new Queue<int>(100)
            });

            // Track packet loss
            if (packetLost)
            {
                state.PacketLossCount++;
            }
            else
            {
                state.PacketLossCount = Math.Max(0, state.PacketLossCount - 1);
            }

            // Track jitter
            if (state.JitterSamples.Count >= 100)
            {
                state.JitterSamples.Dequeue();
            }
            state.JitterSamples.Enqueue(jitterMs);

            // Adjust buffer size
            AdjustBufferSize(state);
        }

        private void AdjustBufferSize(BufferState state)
        {
            // Calculate average jitter
            if (state.JitterSamples.Count == 0) return;

            var avgJitter = state.JitterSamples.Average();
            var targetSize = DefaultBufferSize + (int)avgJitter + (state.PacketLossCount * 20);

            // Apply limits
            state.CurrentSize = Math.Clamp(targetSize, MinBufferSize, MaxBufferSize);
        }

        private class BufferState
        {
            public int CurrentSize { get; set; }
            public int PacketLossCount { get; set; }
            public Queue<int> JitterSamples { get; set; } = new();
        }
    }
}
