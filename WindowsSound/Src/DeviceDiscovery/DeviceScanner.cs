using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedAudio.DeviceDiscovery
{
    /// <summary>
    /// Sound device discovered in the network
    /// </summary>
    public class SoundDevice : IEquatable<SoundDevice>
    {
        public string Uuid { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 5004;
        public int SignalStrength { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int Latency { get; set; } = -1;
        public string Version { get; set; } = "1.0";

        public string DisplayName => string.IsNullOrEmpty(Alias) ?
            $"SoundPlayer-{Uuid.Substring(0, Math.Min(4, Uuid.Length))}" :
            $"{Alias} ({Uuid.Substring(0, Math.Min(4, Uuid.Length))})";

        public bool IsOnline => (DateTime.Now - LastSeen).TotalSeconds < 15;

        public bool Equals(SoundDevice? other)
        {
            if (other == null) return false;
            return Uuid == other.Uuid;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as SoundDevice);
        }

        public override int GetHashCode()
        {
            return Uuid.GetHashCode();
        }
    }

    /// <summary>
    /// Network device scanner using mDNS/Bonjour
    /// </summary>
    public class DeviceScanner : IDisposable
    {
        private readonly List<SoundDevice> _discoveredDevices = new();
        private readonly Dictionary<string, DateTime> _lastSeen = new();
        private bool _isScanning;
        private UdpClient? _mdnsClient;
        private readonly Timer _offlineCheckTimer;
        private const string MDNSServiceType = "_soundplayer._tcp.local.";

        public event EventHandler<SoundDevice>? DeviceFound;
        public event EventHandler<SoundDevice>? DeviceUpdated;
        public event EventHandler<string>? DeviceLost;
        public event EventHandler? ScanComplete;
        public event EventHandler<Exception>? ScanError;

        public IReadOnlyList<SoundDevice> DiscoveredDevices => _discoveredDevices.AsReadOnly();
        public bool IsScanning => _isScanning;

        public DeviceScanner()
        {
            // Check for offline devices every 5 seconds
            _offlineCheckTimer = new Timer(CheckOfflineDevices, null, 5000, 5000);
        }

        /// <summary>
        /// Start scanning for sound devices
        /// </summary>
        public async Task StartScanAsync(TimeSpan? timeout = null)
        {
            if (_isScanning) return;

            _isScanning = true;
            _discoveredDevices.Clear();
            _lastSeen.Clear();

            try
            {
                // Start mDNS listener
                _mdnsClient = new UdpClient();
                _mdnsClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _mdnsClient.Client.Bind(new IPEndPoint(IPAddress.Any, 5353));

                _ = Task.Run(ListenForMdnsResponses);

                // Send mDNS query
                await SendMdnsQueryAsync();

                // Wait for responses
                var waitTime = timeout ?? TimeSpan.FromSeconds(5);
                await Task.Delay(waitTime);

                StopScan();
                ScanComplete?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ScanError?.Invoke(this, ex);
                StopScan();
            }
        }

        /// <summary>
        /// Start continuous scanning mode
        /// </summary>
        public async Task StartContinuousScanAsync()
        {
            if (_isScanning) return;

            _isScanning = true;

            try
            {
                _mdnsClient = new UdpClient();
                _mdnsClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _mdnsClient.Client.Bind(new IPEndPoint(IPAddress.Any, 5353));

                _ = Task.Run(ListenForMdnsResponses);

                // Send periodic queries
                while (_isScanning)
                {
                    await SendMdnsQueryAsync();
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception ex)
            {
                ScanError?.Invoke(this, ex);
                StopScan();
            }
        }

        public void StopScan()
        {
            _isScanning = false;
            _mdnsClient?.Close();
            _mdnsClient = null;
        }

        /// <summary>
        /// Measure latency to a device
        /// </summary>
        public async Task<int> MeasureLatencyAsync(SoundDevice device)
        {
            try
            {
                using var client = new UdpClient();
                var endPoint = new IPEndPoint(IPAddress.Parse(device.Host), device.Port);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Send ping packet
                byte[] pingData = System.Text.Encoding.UTF8.GetBytes("PING");
                await client.SendAsync(pingData, pingData.Length, endPoint);

                // Wait for response (with timeout)
                var receiveTask = client.ReceiveAsync();
                var timeoutTask = Task.Delay(1000);

                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                stopwatch.Stop();

                if (completedTask == receiveTask)
                {
                    device.Latency = (int)stopwatch.ElapsedMilliseconds;
                    DeviceUpdated?.Invoke(this, device);
                    return device.Latency;
                }
            }
            catch
            {
                // Ignore errors
            }

            device.Latency = -1;
            return -1;
        }

        private async Task SendMdnsQueryAsync()
        {
            try
            {
                byte[] query = CreateMdnsQuery(MDNSServiceType);

                using UdpClient client = new();
                client.EnableBroadcast = true;
                client.MulticastLoopback = true;

                // Send to multicast address
                var multicastEndPoint = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
                await client.SendAsync(query, query.Length, multicastEndPoint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"mDNS query failed: {ex.Message}");
            }
        }

        private byte[] CreateMdnsQuery(string serviceType)
        {
            List<byte> packet = new();

            // Header
            packet.AddRange(new byte[] { 0x00, 0x00 }); // Transaction ID
            packet.AddRange(new byte[] { 0x00, 0x00 }); // Flags
            packet.AddRange(new byte[] { 0x00, 0x01 }); // Questions
            packet.AddRange(new byte[] { 0x00, 0x00 }); // Answer RRs
            packet.AddRange(new byte[] { 0x00, 0x00 }); // Authority RRs
            packet.AddRange(new byte[] { 0x00, 0x00 }); // Additional RRs

            // Question
            string[] parts = serviceType.Split('.');
            foreach (string part in parts)
            {
                packet.Add((byte)part.Length);
                packet.AddRange(System.Text.Encoding.ASCII.GetBytes(part));
            }
            packet.Add(0); // End of name
            packet.AddRange(new byte[] { 0x00, 0x0C }); // Type PTR
            packet.AddRange(new byte[] { 0x00, 0x01 }); // Class IN

            return packet.ToArray();
        }

        private async Task ListenForMdnsResponses()
        {
            while (_isScanning)
            {
                try
                {
                    UdpReceiveResult result = await _mdnsClient!.ReceiveAsync();
                    ParseMdnsResponse(result.Buffer, result.RemoteEndPoint);
                }
                catch when (_isScanning)
                {
                    // Expected when stopping
                    break;
                }
                catch
                {
                    // Ignore other errors and continue
                }
            }
        }

        private void ParseMdnsResponse(byte[] data, IPEndPoint endPoint)
        {
            try
            {
                // Parse mDNS response (simplified)
                // In real implementation, properly parse DNS-SD format

                // For now, create a placeholder device
                var device = new SoundDevice
                {
                    Uuid = Guid.NewGuid().ToString(),
                    Host = endPoint.Address.ToString(),
                    Port = 5004,
                    LastSeen = DateTime.Now
                };

                AddOrUpdateDevice(device);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing mDNS response: {ex.Message}");
            }
        }

        private void AddOrUpdateDevice(SoundDevice device)
        {
            _lastSeen[device.Uuid] = DateTime.Now;

            var existing = _discoveredDevices.FirstOrDefault(d => d.Uuid == device.Uuid);
            if (existing != null)
            {
                existing.LastSeen = DateTime.Now;
                DeviceUpdated?.Invoke(this, existing);
            }
            else
            {
                _discoveredDevices.Add(device);
                DeviceFound?.Invoke(this, device);
            }
        }

        private void CheckOfflineDevices(object? state)
        {
            var now = DateTime.Now;
            var offlineDevices = _lastSeen
                .Where(kvp => (now - kvp.Value).TotalSeconds > 15)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var uuid in offlineDevices)
            {
                var device = _discoveredDevices.FirstOrDefault(d => d.Uuid == uuid);
                if (device != null)
                {
                    _discoveredDevices.Remove(device);
                    _lastSeen.Remove(uuid);
                    DeviceLost?.Invoke(this, uuid);
                }
            }
        }

        public void Dispose()
        {
            StopScan();
            _offlineCheckTimer?.Dispose();
        }
    }
}
