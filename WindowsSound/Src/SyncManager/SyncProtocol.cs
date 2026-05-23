using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DistributedAudio.SyncManager
{
    /// <summary>
    /// PTP (Precision Time Protocol) 同步消息
    /// </summary>
    public enum SyncMessageType : byte
    {
        SyncRequest = 0x01,
        SyncResponse = 0x02,
        FollowUp = 0x03,
        DelayRequest = 0x04,
        DelayResponse = 0x05
    }

    /// <summary>
    /// 同步消息
    /// </summary>
    public class SyncMessage
    {
        public SyncMessageType Type { get; set; }
        public ulong Timestamp { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public byte[] Serialize()
        {
            var result = new List<byte>();
            result.Add((byte)Type);

            var timestampBytes = BitConverter.GetBytes(Timestamp);
            if (!BitConverter.IsLittleEndian)
                timestampBytes = timestampBytes.Reverse().ToArray();
            result.AddRange(timestampBytes);

            result.AddRange(Data);

            return result.ToArray();
        }

        public static SyncMessage? Deserialize(byte[] data)
        {
            if (data.Length < 9) return null;

            var message = new SyncMessage
            {
                Type = (SyncMessageType)data[0],
                Timestamp = BitConverter.ToUInt64(data, 1)
            };

            if (data.Length > 9)
            {
                message.Data = new byte[data.Length - 9];
                Array.Copy(data, 9, message.Data, 0, message.Data.Length);
            }

            return message;
        }
    }

    /// <summary>
    /// 设备延迟信息
    /// </summary>
    public class DeviceLatencyInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public int NetworkLatencyMs { get; set; }
        public int ProcessingLatencyMs { get; set; }
        public int TotalLatencyMs { get; set; }
        public int ClockOffsetMs { get; set; }
        public DateTime LastMeasured { get; set; } = DateTime.Now;

        public bool IsValid => (DateTime.Now - LastMeasured).TotalSeconds < 30;
    }
}
