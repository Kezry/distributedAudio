using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace DistributedAudio.AudioCapture
{
    /// <summary>
    /// 音频设备热插拔监控
    /// </summary>
    public class AudioDeviceWatcher : IDisposable
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;
        private bool _disposed;

        public event EventHandler<string>? DeviceAdded;
        public event EventHandler<string>? DeviceRemoved;
        public event EventHandler? DefaultDeviceChanged;

        public AudioDeviceWatcher()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            RegisterDeviceNotifications();
        }

        /// <summary>
        /// 注册设备通知
        /// </summary>
        private void RegisterDeviceNotifications()
        {
            // NAudio doesn't provide direct device change notifications
            // In a real implementation, you would use Windows API
            // to register for device notifications

            // This is a placeholder for the implementation
            // In production, you would:
            // 1. Register a window message listener
            // 2. Listen for WM_DEVICECHANGE messages
            // 3. Parse DEV_BROADCAST_DEVICEINTERFACE structures
            // 4. Fire appropriate events
        }

        /// <summary>
        /// 处理设备添加
        /// </summary>
        private void OnDeviceAdded(string deviceId)
        {
            DeviceAdded?.Invoke(this, deviceId);
        }

        /// <summary>
        /// 处理设备移除
        /// </summary>
        private void OnDeviceRemoved(string deviceId)
        {
            DeviceRemoved?.Invoke(this, deviceId);
        }

        /// <summary>
        /// 处理默认设备更改
        /// </summary>
        private void OnDefaultDeviceChanged()
        {
            DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Unregister device notifications
                _deviceEnumerator?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 网络状态监控
    /// </summary>
    public class NetworkStateMonitor
    {
        public event EventHandler? NetworkConnected;
        public event EventHandler? NetworkDisconnected;
        public event EventHandler<string>? NetworkChanged;

        /// <summary>
        /// 开始监控网络状态
        /// </summary>
        public void StartMonitoring()
        {
            // Register for network change notifications
            // This would use NetworkChange.NetworkAddressChanged event
            // or Windows API for more detailed network status

            System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += (s, e) =>
            {
                OnNetworkStateChanged();
            };
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            // Unregister from network change notifications
        }

        private void OnNetworkStateChanged()
        {
            // Check current network status
            var isConnected = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

            if (isConnected)
            {
                NetworkConnected?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                NetworkDisconnected?.Invoke(this, EventArgs.Empty);
            }

            NetworkChanged?.Invoke(this, isConnected ? "Connected" : "Disconnected");
        }

        /// <summary>
        /// 获取当前网络状态
        /// </summary>
        public bool IsNetworkAvailable()
        {
            return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        }

        /// <summary>
        /// 获取所有网络接口
        /// </summary>
        public List<NetworkInterfaceInfo> GetNetworkInterfaces()
        {
            var interfaces = new List<NetworkInterfaceInfo>();

            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    interfaces.Add(new NetworkInterfaceInfo
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        IsWireless = ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211,
                        Speed = ni.Speed
                    });
                }
            }

            return interfaces;
        }
    }

    /// <summary>
    /// 网络接口信息
    /// </summary>
    public class NetworkInterfaceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsWireless { get; set; }
        public long Speed { get; set; }

        public string GetSpeedDescription()
        {
            if (Speed >= 1000000000)
                return $"{Speed / 1000000000} Gbps";
            else if (Speed >= 1000000)
                return $"{Speed / 1000000} Mbps";
            else
                return $"{Speed / 1000} Kbps";
        }
    }
}
