using System;
using System.Collections.Generic;
using System.Linq;
using DistributedAudio.DeviceDiscovery;

namespace DistributedAudio.ChannelRouter
{
    /// <summary>
    /// 声道路由器
    /// 将多声道音频分离并路由到不同设备
    /// </summary>
    public class ChannelRouter : IDisposable
    {
        private ChannelConfiguration _configuration = ChannelConfiguration.Stereo;
        private readonly List<DeviceChannelAssignment> _assignments = new();
        private bool _disposed;

        public ChannelConfiguration Configuration
        {
            get => _configuration;
            set => _configuration = value;
        }

        public IReadOnlyList<DeviceChannelAssignment> Assignments => _assignments.AsReadOnly();

        /// <summary>
        /// 设置声道配置
        /// </summary>
        public void SetConfiguration(ChannelConfiguration config)
        {
            _configuration = config;
            ValidateAssignments();
        }

        /// <summary>
        /// 分配设备到声道
        /// </summary>
        public void AssignDeviceToChannels(SoundDevice device, List<ChannelType> channels)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ChannelRouter));

            // 验证声道是否在当前配置中
            foreach (var channel in channels)
            {
                if (!_configuration.HasChannel(channel))
                {
                    throw new ArgumentException($"声道 {channel} 不在当前配置 {_configuration.Name} 中");
                }
            }

            // 移除设备的旧分配
            _assignments.RemoveAll(a => a.DeviceId == device.Uuid);

            // 添加新分配
            var assignment = new DeviceChannelAssignment
            {
                DeviceId = device.Uuid,
                DeviceName = device.DisplayName,
                AssignedChannels = channels,
                LatencyMs = device.Latency > 0 ? device.Latency : 0
            };

            _assignments.Add(assignment);
        }

        /// <summary>
        /// 移除设备分配
        /// </summary>
        public void RemoveDeviceAssignment(string deviceId)
        {
            _assignments.RemoveAll(a => a.DeviceId == deviceId);
        }

        /// <summary>
        /// 设置设备音量
        /// </summary>
        public void SetDeviceVolume(string deviceId, int volume)
        {
            var assignment = _assignments.FirstOrDefault(a => a.DeviceId == deviceId);
            if (assignment != null)
            {
                assignment.Volume = Math.Clamp(volume, 0, 100);
            }
        }

        /// <summary>
        /// 设置设备延迟补偿
        /// </summary>
        public void SetDeviceLatency(string deviceId, int latencyMs)
        {
            var assignment = _assignments.FirstOrDefault(a => a.DeviceId == deviceId);
            if (assignment != null)
            {
                assignment.LatencyMs = Math.Max(0, latencyMs);
            }
        }

        /// <summary>
        /// 路由音频数据到设备
        /// </summary>
        public Dictionary<string, byte[]> RouteAudio(byte[] interleavedPcmData)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ChannelRouter));

            var result = new Dictionary<string, byte[]>();

            foreach (var assignment in _assignments)
            {
                var channelData = ExtractChannels(interleavedPcmData, assignment.AssignedChannels);
                result[assignment.DeviceId] = channelData;
            }

            return result;
        }

        /// <summary>
        /// 从交织的 PCM 数据中提取指定声道
        /// </summary>
        private byte[] ExtractChannels(byte[] interleavedData, List<ChannelType> channels)
        {
            if (channels.Count == 0)
                return Array.Empty<byte>();

            // 假设 16-bit PCM，每个采样 2 字节
            const int bytesPerSample = 2;
            int totalChannels = _configuration.ChannelCount;
            int samplesPerChannel = interleavedData.Length / (totalChannels * bytesPerSample);

            var output = new byte[channels.Count * samplesPerChannel * bytesPerSample];
            int outputOffset = 0;

            for (int i = 0; i < samplesPerChannel; i++)
            {
                foreach (var channel in channels)
                {
                    int channelIndex = _configuration.GetChannelIndex(channel);
                    if (channelIndex >= 0)
                    {
                        int sourceOffset = (i * totalChannels + channelIndex) * bytesPerSample;

                        // 复制一个采样（2 字节）
                        output[outputOffset++] = interleavedData[sourceOffset];
                        output[outputOffset++] = interleavedData[sourceOffset + 1];
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// 验证分配的有效性
        /// </summary>
        private void ValidateAssignments()
        {
            var toRemove = new List<DeviceChannelAssignment>();

            foreach (var assignment in _assignments)
            {
                // 检查所有分配的声道是否在当前配置中
                if (assignment.AssignedChannels.Any(c => !_configuration.HasChannel(c)))
                {
                    toRemove.Add(assignment);
                }
            }

            foreach (var assignment in toRemove)
            {
                _assignments.Remove(assignment);
            }
        }

        /// <summary>
        /// 获取未分配的声道
        /// </summary>
        public List<ChannelType> GetUnassignedChannels()
        {
            var assignedChannels = _assignments.SelectMany(a => a.AssignedChannels).ToHashSet();
            return _configuration.Channels.Where(c => !assignedChannels.Contains(c)).ToList();
        }

        /// <summary>
        /// 检查配置是否有效
        /// </summary>
        public bool IsValidConfiguration()
        {
            // 检查所有声道是否都被分配
            var assignedChannels = _assignments.SelectMany(a => a.AssignedChannels).ToHashSet();

            foreach (var channel in _configuration.Channels)
            {
                if (!assignedChannels.Contains(channel))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigurationSummary()
        {
            if (_assignments.Count == 0)
                return $"{_configuration.Name} - 未分配设备";

            var assignments = _assignments
                .Select(a => $"{a.DeviceName}: {a.GetAssignmentDescription()}")
                .ToList();

            return $"{_configuration.Name}\n{string.Join("\n", assignments)}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _assignments.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 声道平衡控制器
    /// </summary>
    public class ChannelBalanceController
    {
        public float LeftGain { get; set; } = 1.0f;
        public float RightGain { get; set; } = 1.0f;
        public float CenterGain { get; set; } = 1.0f;
        public float SubGain { get; set; } = 1.0f;

        /// <summary>
        /// 应用平衡到 PCM 数据
        /// </summary>
        public byte[] ApplyBalance(byte[] pcmData, ChannelConfiguration config)
        {
            // 简化实现：仅支持立体声平衡
            if (config.ChannelCount != 2)
                return pcmData;

            var result = new byte[pcmData.Length];

            for (int i = 0; i < pcmData.Length; i += 4)
            {
                // 左声道
                short leftSample = (short)((pcmData[i + 1] << 8) | pcmData[i]);
                short adjustedLeft = (short)(leftSample * LeftGain);
                result[i] = (byte)(adjustedLeft & 0xFF);
                result[i + 1] = (byte)((adjustedLeft >> 8) & 0xFF);

                // 右声道
                short rightSample = (short)((pcmData[i + 3] << 8) | pcmData[i + 2]);
                short adjustedRight = (short)(rightSample * RightGain);
                result[i + 2] = (byte)(adjustedRight & 0xFF);
                result[i + 3] = (byte)((adjustedRight >> 8) & 0xFF);
            }

            return result;
        }

        /// <summary>
        /// 重置为默认平衡
        /// </summary>
        public void Reset()
        {
            LeftGain = 1.0f;
            RightGain = 1.0f;
            CenterGain = 1.0f;
            SubGain = 1.0f;
        }
    }
}
