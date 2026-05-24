using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedAudio.NetworkTransport
{
    /// <summary>
    /// RTP 数据包
    /// </summary>
    public class RtpPacket
    {
        public ushort SequenceNumber { get; set; }
        public uint Timestamp { get; set; }
        public uint Ssrc { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        public byte[] Serialize()
        {
            var packet = new List<byte>();

            // RTP Header (12 bytes)
            byte firstByte = 0x80; // Version 2, no padding, no extension, no CSRC
            packet.Add(firstByte);
            packet.Add(0x7F); // Payload type (dynamic)

            // Sequence number
            packet.Add((byte)(SequenceNumber >> 8));
            packet.Add((byte)SequenceNumber);

            // Timestamp
            packet.Add((byte)(Timestamp >> 24));
            packet.Add((byte)(Timestamp >> 16));
            packet.Add((byte)(Timestamp >> 8));
            packet.Add((byte)Timestamp);

            // SSRC
            packet.Add((byte)(Ssrc >> 24));
            packet.Add((byte)(Ssrc >> 16));
            packet.Add((byte)(Ssrc >> 8));
            packet.Add((byte)Ssrc);

            // Payload
            packet.AddRange(Payload);

            return packet.ToArray();
        }
    }

    /// <summary>
    /// UDP audio streaming client
    /// Sends audio data to sound player devices
    /// </summary>
    public class AudioStreamer : IDisposable
    {
        private UdpClient? _udpClient;
        private readonly CancellationTokenSource _cts = new();
        private bool _isStreaming;
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private ushort _sequenceNumber;
        private uint _timestamp;
        private readonly uint _ssrc;
        private Task? _sendTask;

        public event EventHandler<Exception>? StreamError;
        public event EventHandler? StreamStarted;
        public event EventHandler? StreamStopped;

        public string DeviceId { get; }
        public IPEndPoint? RemoteEndPoint { get; private set; }
        public bool IsStreaming => _isStreaming;
        public int QueueDepth => _sendQueue.Count;
        public long PacketsSent { get; private set; }
        public long BytesSent { get; private set; }

        public AudioStreamer(string deviceId)
        {
            DeviceId = deviceId;
            _ssrc = (uint)Random.Shared.Next();
        }

        /// <summary>
        /// Start streaming audio to a device
        /// </summary>
        public async Task StartAsync(IPEndPoint remoteEndPoint)
        {
            if (_isStreaming) return;

            RemoteEndPoint = remoteEndPoint;
            _udpClient = new UdpClient();
            _isStreaming = true;

            try
            {
                _udpClient.Connect(remoteEndPoint);

                // Start send loop
                _sendTask = Task.Run(SendLoop);

                StreamStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StreamError?.Invoke(this, ex);
                Stop();
            }
        }

        /// <summary>
        /// Queue audio data for sending
        /// </summary>
        public void Send(byte[] audioData)
        {
            if (!_isStreaming) return;

            // Limit queue depth to prevent memory issues
            if (_sendQueue.Count < 100)
            {
                _sendQueue.Enqueue(audioData);
            }
        }

        /// <summary>
        /// Send audio data immediately (synchronous)
        /// </summary>
        public async Task SendAsync(byte[] audioData)
        {
            if (!_isStreaming || _udpClient == null) return;

            try
            {
                var packet = new RtpPacket
                {
                    SequenceNumber = _sequenceNumber++,
                    Timestamp = _timestamp,
                    Ssrc = _ssrc,
                    Payload = audioData
                };

                _timestamp += (uint)(audioData.Length / 4); // Assuming stereo 16-bit

                var packetData = packet.Serialize();
                await _udpClient.SendAsync(packetData, packetData.Length);

                PacketsSent++;
                BytesSent += audioData.Length;
            }
            catch (Exception ex)
            {
                StreamError?.Invoke(this, ex);
            }
        }

        private async Task SendLoop()
        {
            while (!_cts.IsCancellationRequested && _isStreaming)
            {
                if (_sendQueue.TryDequeue(out var data))
                {
                    await SendAsync(data);
                }
                else
                {
                    await Task.Delay(1);
                }
            }
        }

        public void Stop()
        {
            if (!_isStreaming) return;

            _isStreaming = false;
            _cts.Cancel();

            _sendTask?.Wait(1000);
            _udpClient?.Close();
            _udpClient = null;

            StreamStopped?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Manages multiple audio streamers
    /// </summary>
    public class AudioStreamerManager : IDisposable
    {
        private readonly Dictionary<string, AudioStreamer> _streamers = new();
        private readonly object _lock = new();

        public event EventHandler<Exception>? StreamError;
        public event EventHandler<string>? DeviceConnected;
        public event EventHandler<string>? DeviceDisconnected;

        public IReadOnlyList<AudioStreamer> ActiveStreamers
        {
            get
            {
                lock (_lock)
                {
                    return _streamers.Values.ToList();
                }
            }
        }

        public int ActiveDeviceCount
        {
            get
            {
                lock (_lock)
                {
                    return _streamers.Count;
                }
            }
        }

        /// <summary>
        /// Add a device to the streaming session
        /// </summary>
        public async Task AddDeviceAsync(string deviceId, IPEndPoint endPoint)
        {
            lock (_lock)
            {
                if (_streamers.ContainsKey(deviceId)) return;

                var streamer = new AudioStreamer(deviceId);
                streamer.StreamError += (s, e) => StreamError?.Invoke(s, e);
                streamer.StreamStarted += (s, e) => DeviceConnected?.Invoke(s, deviceId);
                streamer.StreamStopped += (s, e) => DeviceDisconnected?.Invoke(s, deviceId);

                _streamers.Add(deviceId, streamer);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await streamer.StartAsync(endPoint);
                    }
                    catch (Exception ex)
                    {
                        StreamError?.Invoke(streamer, ex);
                    }
                });
            }
        }

        /// <summary>
        /// Remove a device from the streaming session
        /// </summary>
        public void RemoveDevice(string deviceId)
        {
            lock (_lock)
            {
                if (_streamers.TryGetValue(deviceId, out var streamer))
                {
                    streamer.Stop();
                    streamer.Dispose();
                    _streamers.Remove(deviceId);
                }
            }
        }

        /// <summary>
        /// Send audio to specific device
        /// </summary>
        public void SendTo(string deviceId, byte[] audioData)
        {
            AudioStreamer? streamer;
            lock (_lock)
            {
                _streamers.TryGetValue(deviceId, out streamer);
            }

            streamer?.Send(audioData);
        }

        /// <summary>
        /// Send audio to all connected devices
        /// </summary>
        public void SendToAll(byte[] audioData)
        {
            List<AudioStreamer> streamers;
            lock (_lock)
            {
                streamers = _streamers.Values.ToList();
            }

            foreach (var streamer in streamers)
            {
                streamer.Send(audioData);
            }
        }

        /// <summary>
        /// Get streamer for device
        /// </summary>
        public AudioStreamer? GetStreamer(string deviceId)
        {
            lock (_lock)
            {
                _streamers.TryGetValue(deviceId, out var streamer);
                return streamer;
            }
        }

        /// <summary>
        /// Get streaming statistics
        /// </summary>
        public (int activeDevices, long totalPackets, long totalBytes) GetStatistics()
        {
            lock (_lock)
            {
                return (
                    _streamers.Count,
                    _streamers.Values.Sum(s => s.PacketsSent),
                    _streamers.Values.Sum(s => s.BytesSent)
                );
            }
        }

        /// <summary>
        /// Stop all streamers
        /// </summary>
        public void StopAll()
        {
            lock (_lock)
            {
                foreach (var streamer in _streamers.Values)
                {
                    streamer.Stop();
                    streamer.Dispose();
                }
                _streamers.Clear();
            }
        }

        public void Dispose()
        {
            StopAll();
        }
    }
}
