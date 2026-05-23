using System;

namespace DistributedAudio.AudioCapture
{
    /// <summary>
    /// 音频格式配置
    /// </summary>
    public class AudioFormatConfig
    {
        public int SampleRate { get; set; } = 48000;
        public int Channels { get; set; } = 2;
        public int BitsPerSample { get; set; } = 16;

        // 预设的采样率
        public static readonly int[] CommonSampleRates = { 44100, 48000, 96000 };
        public static readonly int[] CommonChannelCounts = { 2, 6, 8 }; // 立体声, 5.1, 7.1

        /// <summary>
        /// 获取格式描述
        /// </summary>
        public string GetFormatDescription()
        {
            string channelDesc = Channels switch
            {
                2 => "立体声",
                6 => "5.1 声道",
                8 => "7.1 声道",
                _ => $"{Channels} 声道"
            };

            return $"{SampleRate / 1000}kHz, {BitsPerSample}bit, {channelDesc}";
        }

        /// <summary>
        /// 计算比特率
        /// </summary>
        public int CalculateBitrate()
        {
            return SampleRate * Channels * BitsPerSample;
        }

        /// <summary>
        /// 计算每秒字节数
        /// </summary>
        public int CalculateBytesPerSecond()
        {
            return CalculateBitrate() / 8;
        }

        /// <summary>
        /// 计算缓冲区大小
        /// </summary>
        public int CalculateBufferSize(int latencyMs = 20)
        {
            return (CalculateBytesPerSecond() * latencyMs) / 1000;
        }

        /// <summary>
        /// 创建标准立体声配置
        /// </summary>
        public static AudioFormatConfig CreateStereo() => new()
        {
            SampleRate = 48000,
            Channels = 2,
            BitsPerSample = 16
        };

        /// <summary>
        /// 创建 5.1 声道配置
        /// </summary>
        public static AudioFormatConfig CreateSurround51() => new()
        {
            SampleRate = 48000,
            Channels = 6,
            BitsPerSample = 16
        };

        /// <summary>
        /// 创建 7.1 声道配置
        /// </summary>
        public static AudioFormatConfig CreateSurround71() => new()
        {
            SampleRate = 48000,
            Channels = 8,
            BitsPerSample = 16
        };
    }
}
