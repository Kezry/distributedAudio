using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedAudio.NetworkTransport
{
    /// <summary>
    /// UDP audio streaming client
    /// Sends audio data to sound player devices
    /// </summary>
    public class AudioStreamer : IDisposable
    {
        private UdpClient? _udpClient;
        private readonly CancellationTokenSource _cts = new();
        private bool _isStreaming;

        public event EventHandler<Exception>? StreamError;

        public bool IsStreaming => _isStreaming;

        /// <summary>
        /// Start streaming audio to a device
        /// </summary>
        public async Task StartAsync(IPEndPoint remoteEndPoint)
        {
            if (_isStreaming) return;

            _udpClient = new UdpClient();
            _isStreaming = true;

            try
            {
                await _udpClient.ConnectAsync(remoteEndPoint);
                await Task.Run(() => StreamLoop(remoteEndPoint), _cts.Token);
            }
            catch (Exception ex)
            {
                StreamError?.Invoke(this, ex);
                Stop();
            }
        }

        /// <summary>
        /// Send audio data
        /// </summary>
        public async Task SendAsync(byte[] audioData)
        {
            if (!_isStreaming || _udpClient == null) return;

            try
            {
                await _udpClient.SendAsync(audioData, audioData.Length);
            }
            catch (Exception ex)
            {
                StreamError?.Invoke(this, ex);
            }
        }

        private async Task StreamLoop(IPEndPoint endPoint)
        {
            while (!_cts.IsCancellationRequested && _isStreaming)
            {
                await Task.Delay(10);
            }
        }

        public void Stop()
        {
            _isStreaming = false;
            _cts.Cancel();
            _udpClient?.Close();
            _udpClient = null;
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
    public class AudioStreamerManager
    {
        private readonly Dictionary<string, AudioStreamer> _streamers = new();

        public event EventHandler<Exception>? StreamError;

        /// <summary>
        /// Add a device to the streaming session
        /// </summary>
        public void AddDevice(string deviceId, IPEndPoint endPoint)
        {
            if (_streamers.ContainsKey(deviceId)) return;

            var streamer = new AudioStreamer();
            streamer.StreamError += (s, e) => StreamError?.Invoke(s, e);

            _streamers.Add(deviceId, streamer);
            _ = streamer.StartAsync(endPoint);
        }

        /// <summary>
        /// Remove a device from the streaming session
        /// </summary>
        public void RemoveDevice(string deviceId)
        {
            if (_streamers.TryGetValue(deviceId, out var streamer))
            {
                streamer.Stop();
                streamer.Dispose();
                _streamers.Remove(deviceId);
            }
        }

        /// <summary>
        /// Send audio to all connected devices
        /// </summary>
        public async Task SendToAllAsync(byte[] audioData)
        {
            var tasks = _streamers.Values.Select(s => s.SendAsync(audioData));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Stop all streamers
        /// </summary>
        public void StopAll()
        {
            foreach (var streamer in _streamers.Values)
            {
                streamer.Stop();
                streamer.Dispose();
            }
            _streamers.Clear();
        }
    }
}
