using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistributedAudio.AudioCapture;
using DistributedAudio.AudioEncoder;
using DistributedAudio.ChannelRouter;
using DistributedAudio.DeviceDiscovery;
using DistributedAudio.NetworkTransport;
using DistributedAudio.SyncManager;

namespace DistributedAudio.UI.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly WasapiCapture _audioCapture;
        private readonly OpusEncoder _audioEncoder;
        private readonly DeviceScanner _deviceScanner;
        private readonly ChannelRouter _channelRouter;
        private readonly AudioStreamerManager _streamerManager;
        private readonly AudioSyncManager _syncManager;
        private readonly DynamicBufferManager _bufferManager;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isStreaming;

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private int _activeDeviceCount;

        [ObservableProperty]
        private long _totalPacketsSent;

        [ObservableProperty]
        private long _totalBytesSent;

        [ObservableProperty]
        private double _currentAudioLevel;

        [ObservableProperty]
        private string _selectedChannelConfiguration = "立体声";

        public MainViewModel()
        {
            // Initialize services
            _audioCapture = new WasapiCapture();
            _audioEncoder = new OpusEncoder();
            _deviceScanner = new DeviceScanner();
            _channelRouter = new ChannelRouter();
            _streamerManager = new AudioStreamerManager();
            _syncManager = new AudioSyncManager();
            _bufferManager = new DynamicBufferManager();

            // Subscribe to events
            _deviceScanner.DeviceFound += OnDeviceFound;
            _deviceScanner.DeviceLost += OnDeviceLost;
            _streamerManager.DeviceConnected += OnDeviceConnected;
            _streamerManager.DeviceDisconnected += OnDeviceDisconnected;
            _audioCapture.AudioDataCaptured += OnAudioDataCaptured;

            // Initialize collections
            DiscoveredDevices = new ObservableCollection<SoundDevice>();
            ActiveDevices = new ObservableCollection<ActiveDeviceViewModel>();

            // Start sync manager
            Task.Run(async () => await _syncManager.StartAsync());
        }

        public ObservableCollection<SoundDevice> DiscoveredDevices { get; }
        public ObservableCollection<ActiveDeviceViewModel> ActiveDevices { get; }

        public string[] ChannelConfigurations => new[]
        {
            "立体声",
            "2.1 声道",
            "5.1 声道",
            "7.1 声道"
        };

        /// <summary>
        /// 开始扫描设备
        /// </summary>
        [RelayCommand]
        private async Task StartScanAsync()
        {
            if (IsScanning) return;

            IsScanning = true;
            StatusMessage = "正在扫描设备...";

            try
            {
                await _deviceScanner.StartScanAsync();
                StatusMessage = $"发现 {DiscoveredDevices.Count} 个设备";
            }
            catch (Exception ex)
            {
                StatusMessage = $"扫描失败: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        /// <summary>
        /// 停止扫描
        /// </summary>
        [RelayCommand]
        private void StopScan()
        {
            _deviceScanner.StopScan();
            IsScanning = false;
            StatusMessage = "扫描已停止";
        }

        /// <summary>
        /// 选择设备并分配声道
        /// </summary>
        [RelayCommand]
        private async Task SelectDeviceAsync(SoundDevice? device)
        {
            if (device == null) return;

            // Show device configuration dialog
            // For now, default to stereo configuration
            var channels = new List<ChannelType> { ChannelType.Left, ChannelType.Right };

            _channelRouter.AssignDeviceToChannels(device, channels);

            // Add to active devices
            var activeDevice = new ActiveDeviceViewModel(device)
            {
                AssignedChannels = "立体声",
                Volume = 100,
                IsEnabled = true
            };

            ActiveDevices.Add(activeDevice);

            // Measure latency
            Task.Run(async () =>
            {
                var endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(device.Host), device.Port);
                await _syncManager.MeasureDeviceLatencyAsync(device.Uuid, endPoint);
            });

            StatusMessage = $"已添加 {device.DisplayName}";
        }

        /// <summary>
        /// 开始音频流传输
        /// </summary>
        [RelayCommand]
        private async Task StartStreamingAsync()
        {
            if (IsStreaming) return;

            if (ActiveDevices.Count == 0)
            {
                StatusMessage = "请先选择设备";
                return;
            }

            IsStreaming = true;
            StatusMessage = "正在启动音频流...";

            try
            {
                // Configure channel router
                UpdateChannelConfiguration();

                // Start audio capture
                _audioCapture.Start();

                // Connect to all devices
                foreach (var device in ActiveDevices.Where(d => d.IsEnabled))
                {
                    var endPoint = new System.Net.IPEndPoint(
                        System.Net.IPAddress.Parse(device.Device.Host),
                        device.Device.Port
                    );

                    await _streamerManager.AddDeviceAsync(device.Device.Uuid, endPoint);
                }

                StatusMessage = $"正在流式传输到 {ActiveDevices.Count(d => d.IsEnabled)} 个设备";
            }
            catch (Exception ex)
            {
                StatusMessage = $"启动失败: {ex.Message}";
                IsStreaming = false;
            }
        }

        /// <summary>
        /// 停止音频流传输
        /// </summary>
        [RelayCommand]
        private void StopStreaming()
        {
            if (!IsStreaming) return;

            _audioCapture.Stop();
            _streamerManager.StopAll();

            IsStreaming = false;
            StatusMessage = "音频流已停止";
        }

        /// <summary>
        /// 移除设备
        /// </summary>
        [RelayCommand]
        private void RemoveDevice(ActiveDeviceViewModel? device)
        {
            if (device == null) return;

            ActiveDevices.Remove(device);
            _channelRouter.RemoveDeviceAssignment(device.Device.Uuid);
            _streamerManager.RemoveDevice(device.Device.Uuid);

            UpdateStatistics();
            StatusMessage = $"已移除 {device.Device.DisplayName}";
        }

        /// <summary>
        /// 更新声道配置
        /// </summary>
        partial void OnSelectedChannelConfigurationChanged(string value)
        {
            UpdateChannelConfiguration();
        }

        private void UpdateChannelConfiguration()
        {
            var config = SelectedChannelConfiguration switch
            {
                "立体声" => ChannelConfiguration.Stereo,
                "2.1 声道" => ChannelConfiguration.Surround21,
                "5.1 声道" => ChannelConfiguration.Surround51,
                "7.1 声道" => ChannelConfiguration.Surround71,
                _ => ChannelConfiguration.Stereo
            };

            _channelRouter.SetConfiguration(config);
        }

        private void OnAudioDataCaptured(object? sender, byte[] audioData)
        {
            if (!IsStreaming) return;

            // Route audio to devices
            var routedAudio = _channelRouter.RouteAudio(audioData);

            // Encode and send to each device
            foreach (var (deviceId, channelData) in routedAudio)
            {
                try
                {
                    var encodedData = _audioEncoder.Encode(channelData);
                    _streamerManager.SendTo(deviceId, encodedData);
                }
                catch
                {
                    // Encoding error - skip this frame
                }
            }

            // Update audio level (simplified)
            CurrentAudioLevel = CalculateAudioLevel(audioData);

            // Update statistics
            UpdateStatistics();
        }

        private double CalculateAudioLevel(byte[] pcmData)
        {
            if (pcmData.Length < 2) return 0;

            // Calculate RMS for first channel
            long sum = 0;
            int samples = pcmData.Length / 4; // Stereo 16-bit

            for (int i = 0; i < samples; i++)
            {
                short sample = (short)((pcmData[i * 4 + 1] << 8) | pcmData[i * 4]);
                sum += sample * sample;
            }

            double rms = Math.Sqrt(sum / (double)samples);
            return Math.Min(100, rms / 327.68); // Convert to percentage
        }

        private void UpdateStatistics()
        {
            var (activeDevices, totalPackets, totalBytes) = _streamerManager.GetStatistics();
            ActiveDeviceCount = activeDevices;
            TotalPacketsSent = totalPackets;
            TotalBytesSent = totalBytes;
        }

        private void OnDeviceFound(object? sender, SoundDevice device)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (!DiscoveredDevices.Any(d => d.Uuid == device.Uuid))
                {
                    DiscoveredDevices.Add(device);
                }
            });
        }

        private void OnDeviceLost(object? sender, string deviceId)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var device = DiscoveredDevices.FirstOrDefault(d => d.Uuid == deviceId);
                if (device != null)
                {
                    DiscoveredDevices.Remove(device);
                }
            });
        }

        private void OnDeviceConnected(object? sender, string deviceId)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var device = ActiveDevices.FirstOrDefault(d => d.Device.Uuid == deviceId);
                if (device != null)
                {
                    device.IsConnected = true;
                }
            });
        }

        private void OnDeviceDisconnected(object? sender, string deviceId)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var device = ActiveDevices.FirstOrDefault(d => d.Device.Uuid == deviceId);
                if (device != null)
                {
                    device.IsConnected = false;
                }
            });
        }
    }

    /// <summary>
    /// 活动设备视图模型
    /// </summary>
    public class ActiveDeviceViewModel : ObservableObject
    {
        public SoundDevice Device { get; }

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _assignedChannels = string.Empty;

        [ObservableProperty]
        private int _volume;

        [ObservableProperty]
        private int _latency;

        public ActiveDeviceViewModel(SoundDevice device)
        {
            Device = device;
        }
    }
}
