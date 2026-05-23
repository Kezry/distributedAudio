using System;
using System.Collections.Generic;
using System.Linq;

namespace DistributedAudio.ChannelRouter
{
    /// <summary>
    /// 声道定义
    /// </summary>
    public enum ChannelType
    {
        Left,           // 左前
        Right,          // 右前
        Center,         // 中置
        LowFrequency,   // 低频 (LFE)
        LeftSurround,   // 左环绕
        RightSurround,  // 右环绕
        LeftBack,       // 左后
        RightBack       // 右后
    }

    /// <summary>
    /// 声道配置
    /// </summary>
    public class ChannelConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public int ChannelCount { get; set; }
        public List<ChannelType> Channels { get; set; } = new();

        public static ChannelConfiguration Stereo = new()
        {
            Name = "立体声",
            ChannelCount = 2,
            Channels = new() { ChannelType.Left, ChannelType.Right }
        };

        public static ChannelConfiguration Surround21 = new()
        {
            Name = "2.1 声道",
            ChannelCount = 3,
            Channels = new() { ChannelType.Left, ChannelType.Right, ChannelType.LowFrequency }
        };

        public static ChannelConfiguration Surround51 = new()
        {
            Name = "5.1 声道",
            ChannelCount = 6,
            Channels = new()
            {
                ChannelType.Left,
                ChannelType.Right,
                ChannelType.Center,
                ChannelType.LowFrequency,
                ChannelType.LeftSurround,
                ChannelType.RightSurround
            }
        };

        public static ChannelConfiguration Surround71 = new()
        {
            Name = "7.1 声道",
            ChannelCount = 8,
            Channels = new()
            {
                ChannelType.Left,
                ChannelType.Right,
                ChannelType.Center,
                ChannelType.LowFrequency,
                ChannelType.LeftSurround,
                ChannelType.RightSurround,
                ChannelType.LeftBack,
                ChannelType.RightBack
            }
        };

        /// <summary>
        /// 获取声道的索引位置
        /// </summary>
        public int GetChannelIndex(ChannelType channel)
        {
            return Channels.IndexOf(channel);
        }

        /// <summary>
        /// 是否包含指定声道
        /// </summary>
        public bool HasChannel(ChannelType channel)
        {
            return Channels.Contains(channel);
        }
    }

    /// <summary>
    /// 设备声道分配
    /// </summary>
    public class DeviceChannelAssignment
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public List<ChannelType> AssignedChannels { get; set; } = new();
        public int Volume { get; set; } = 100;
        public int LatencyMs { get; set; } = 0;

        public string GetAssignmentDescription()
        {
            if (AssignedChannels.Count == 0)
                return "未分配";

            var channelNames = AssignedChannels.Select(c => c switch
            {
                ChannelType.Left => "左",
                ChannelType.Right => "右",
                ChannelType.Center => "中置",
                ChannelType.LowFrequency => "低频",
                ChannelType.LeftSurround => "左环绕",
                ChannelType.RightSurround => "右环绕",
                ChannelType.LeftBack => "左后",
                ChannelType.RightBack => "右后",
                _ => "未知"
            });

            return $"{string.Join(", ", channelNames)} ({Volume}%)";
        }
    }
}
