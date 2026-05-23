using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DistributedAudio.AudioCapture
{
    /// <summary>
    /// 音频设备信息
    /// </summary>
    public class AudioDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }

        public string DisplayName => IsDefault ? $"{Name} (默认)" : Name;
    }

    /// <summary>
    /// 音频设备管理器
    /// </summary>
    public class AudioDeviceManager
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;

        public AudioDeviceManager()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
        }

        /// <summary>
        /// 获取所有音频输出设备
        /// </summary>
        public List<AudioDevice> GetOutputDevices()
        {
            var devices = new List<AudioDevice>();

            try
            {
                var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);

                foreach (var device in _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    devices.Add(new AudioDevice
                    {
                        Id = device.ID,
                        Name = device.FriendlyName,
                        FriendlyName = device.FriendlyName,
                        IsDefault = device.ID == defaultDevice.ID,
                        IsActive = device.State == DeviceState.Active
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// 获取默认音频输出设备
        /// </summary>
        public AudioDevice? GetDefaultDevice()
        {
            try
            {
                var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                return new AudioDevice
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    FriendlyName = device.FriendlyName,
                    IsDefault = true,
                    IsActive = device.State == DeviceState.Active
                };
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _deviceEnumerator?.Dispose();
        }
    }
}
