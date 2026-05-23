using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace WindowsSound.ChannelManager
{
    /// <summary>
    /// 音频声道配置
    /// 支持 2.0, 2.1, 5.1, 7.1 配置
    /// </summary>
    [DataContract]
    public class ChannelConfiguration
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public AudioChannelLayout Layout { get; set; }

        [DataMember]
        public Dictionary<ChannelType, ChannelAssignment> Assignments { get; set; }

        public ChannelConfiguration()
        {
            Assignments = new Dictionary<ChannelType, ChannelAssignment>();
        }

        public ChannelConfiguration(string name, AudioChannelLayout layout) : this()
        {
            Name = name;
            Layout = layout;
        }

        /// <summary>
        /// 获取指定布局的所有声道
        /// </summary>
        public static List<ChannelType> GetChannelsForLayout(AudioChannelLayout layout)
        {
            return layout switch
            {
                AudioChannelLayout.Stereo => new List<ChannelType>
                    { ChannelType.Left, ChannelType.Right },
                AudioChannelLayout.TwoPointOne => new List<ChannelType>
                    { ChannelType.Left, ChannelType.Right, ChannelType.LFE },
                AudioChannelLayout.FivePointOne => new List<ChannelType>
                    { ChannelType.Left, ChannelType.Right, ChannelType.Center, ChannelType.LFE,
                      ChannelType.LeftSurround, ChannelType.RightSurround },
                AudioChannelLayout.SevenPointOne => new List<ChannelType>
                    { ChannelType.Left, ChannelType.Right, ChannelType.Center, ChannelType.LFE,
                      ChannelType.LeftSurround, ChannelType.RightSurround,
                      ChannelType.LeftRear, ChannelType.RightRear },
                _ => new List<ChannelType>()
            };
        }

        /// <summary>
        /// 创建预设配置模板
        /// </summary>
        public static ChannelConfiguration CreatePreset(AudioChannelLayout layout)
        {
            var config = new ChannelConfiguration
            {
                Name = layout.ToString(),
                Layout = layout
            };

            foreach (var channel in GetChannelsForLayout(layout))
            {
                config.Assignments[channel] = new ChannelAssignment
                {
                    Channel = channel,
                    DeviceId = null,
                    DelayMs = 0,
                    Gain = 1.0f
                };
            }

            return config;
        }
    }

    /// <summary>
    /// 音频声道布局
    /// </summary>
    [DataContract]
    public enum AudioChannelLayout
    {
        [EnumMember(Value = "2.0")]
        Stereo = 2,

        [EnumMember(Value = "2.1")]
        TwoPointOne = 3,

        [EnumMember(Value = "5.1")]
        FivePointOne = 6,

        [EnumMember(Value = "7.1")]
        SevenPointOne = 8
    }

    /// <summary>
    /// 声道类型
    /// </summary>
    [DataContract]
    public enum ChannelType
    {
        [EnumMember(Value = "L")]
        Left,

        [EnumMember(Value = "R")]
        Right,

        [EnumMember(Value = "C")]
        Center,

        [EnumMember(Value = "LFE")]
        LFE,

        [EnumMember(Value = "LS")]
        LeftSurround,

        [EnumMember(Value = "RS")]
        RightSurround,

        [EnumMember(Value = "LR")]
        LeftRear,

        [EnumMember(Value = "RR")]
        RightRear
    }

    /// <summary>
    /// 声道分配配置
    /// </summary>
    [DataContract]
    public class ChannelAssignment
    {
        [DataMember]
        public ChannelType Channel { get; set; }

        [DataMember]
        public string DeviceId { get; set; }

        [DataMember]
        public int DelayMs { get; set; }

        [DataMember]
        public float Gain { get; set; }

        [DataMember]
        public bool IsMuted { get; set; }

        public ChannelAssignment()
        {
            Gain = 1.0f;
            DelayMs = 0;
            IsMuted = false;
        }
    }

    /// <summary>
    /// 场景配置（保存多个声道布局配置）
    /// </summary>
    [DataContract]
    public class SceneConfiguration
    {
        [DataMember]
        public string SceneName { get; set; }

        [DataMember]
        public DateTime CreatedAt { get; set; }

        [DataMember]
        public List<ChannelConfiguration> Configurations { get; set; }

        public SceneConfiguration()
        {
            Configurations = new List<ChannelConfiguration>();
            CreatedAt = DateTime.Now;
        }

        public SceneConfiguration(string name) : this()
        {
            SceneName = name;
        }
    }
}
