using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using NAudio.Lame;
using System.Net.Sockets;

namespace DistributedAudio.DeviceDiscovery
{
    /// <summary>
    /// Sound device discovered in the network
    /// </summary>
    public class SoundDevice
    {
        public string Uuid { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 5004;
        public int SignalStrength { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int Latency { get; set; } = -1;

        public string DisplayName => $"{Alias} ({Uuid.Substring(0, 4)})";
        public bool IsOnline => (DateTime.Now - LastSeen).TotalSeconds < 10;
    }

    /// <summary>
    /// Network device scanner using mDNS/Bonjour
    /// </summary>
    public class DeviceScanner
    {
        private readonly List<SoundDevice> _discoveredDevices = new();
        private bool _isScanning;
        private UdpClient? _mdnsClient;

        public event EventHandler<SoundDevice>? DeviceFound;
        public event EventHandler<string>? DeviceLost;
        public event EventHandler? ScanComplete;

        public IReadOnlyList<SoundDevice> DiscoveredDevices => _discoveredDevices.AsReadOnly();
        public bool IsScanning => _isScanning;

        /// <summary>
        /// Start scanning for sound devices
        /// </summary>
        public async Task StartScanAsync()
        {
            if (_isScanning) return;

            _isScanning = true;
            _discoveredDevices.Clear();

            // Start mDNS listener
            _mdnsClient = new UdpClient(5353);
            _mdnsClient.MulticastLoopback = true;

            _ = Task.Run(ListenForMdnsResponses);

            // Send mDNS query
            await SendMdnsQueryAsync();

            // Wait for responses
            await Task.Delay(5000);

            StopScan();
            ScanComplete?.Invoke(this, EventArgs.Empty);
        }

        public void StopScan()
        {
            _isScanning = false;
            _mdnsClient?.Close();
            _mdnsClient = null;
        }

        private async Task SendMdnsQueryAsync()
        {
            try
            {
                // mDNS query for _soundplayer._tcp.local
                byte[] query = CreateMdnsQuery("_soundplayer._tcp.local");

                using UdpClient client = new();
                client.EnableBroadcast = true;
                await client.SendAsync(query, query.Length, new IPEndPoint(IPAddress.Broadcast, 5353));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"mDNS query failed: {ex.Message}");
            }
        }

        private byte[] CreateMdnsQuery(string serviceType)
        {
            // Simplified mDNS query packet
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
                catch
                {
                    break;
                }
            }
        }

        private void ParseMdnsResponse(byte[] data, IPEndPoint endPoint)
        {
            // Parse mDNS response and extract device info
            // This is simplified - real implementation would properly parse the DNS packet

            var device = new SoundDevice
            {
                Host = endPoint.Address.ToString(),
                LastSeen = DateTime.Now
            };

            _discoveredDevices.Add(device);
            DeviceFound?.Invoke(this, device);
        }
    }
}
