using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using DistributedAudio.AudioCapture;
using DistributedAudio.DeviceDiscovery;

namespace WindowsSound.ChannelManager
{
    /// <summary>
    /// 声道管理器 - 管理多设备声道分配
    /// </summary>
    public class ChannelManager
    {
        private const string CONFIG_DIR = "Config";
        private const string SCENES_FILE = "scenes.json";

        private List<SceneConfiguration> _scenes;
        private SceneConfiguration _currentScene;

        public event EventHandler<SceneConfiguration> SceneChanged;
        public event EventHandler<ChannelAssignment> AssignmentChanged;

        public IReadOnlyList<SceneConfiguration> Scenes => _scenes;
        public SceneConfiguration CurrentScene => _currentScene;

        public ChannelManager()
        {
            _scenes = new List<SceneConfiguration>();
            LoadScenes();
        }

        /// <summary>
        /// 创建新场景
        /// </summary>
        public SceneConfiguration CreateScene(string name)
        {
            var scene = new SceneConfiguration(name);
            _scenes.Add(scene);
            SaveScenes();
            return scene;
        }

        /// <summary>
        /// 删除场景
        /// </summary>
        public bool DeleteScene(string sceneName)
        {
            var scene = _scenes.FirstOrDefault(s => s.SceneName == sceneName);
            if (scene != null)
            {
                _scenes.Remove(scene);
                SaveScenes();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置当前场景
        /// </summary>
        public void SetCurrentScene(string sceneName)
        {
            var scene = _scenes.FirstOrDefault(s => s.SceneName == sceneName);
            if (scene != null)
            {
                _currentScene = scene;
                SceneChanged?.Invoke(this, scene);
            }
        }

        /// <summary>
        /// 添加声道配置到场景
        /// </summary>
        public ChannelConfiguration AddConfiguration(SceneConfiguration scene, string name, AudioChannelLayout layout)
        {
            var config = ChannelConfiguration.CreatePreset(layout);
            config.Name = name;
            scene.Configurations.Add(config);
            SaveScenes();
            return config;
        }

        /// <summary>
        /// 分配设备到声道
        /// </summary>
        public void AssignDevice(ChannelConfiguration config, ChannelType channel, AudioDevice device, int delayMs = 0)
        {
            if (!config.Assignments.ContainsKey(channel))
            {
                config.Assignments[channel] = new ChannelAssignment { Channel = channel };
            }

            config.Assignments[channel].DeviceId = device.DeviceId;
            config.Assignments[channel].DelayMs = delayMs;

            AssignmentChanged?.Invoke(this, config.Assignments[channel]);
            SaveScenes();
        }

        /// <summary>
        /// 取消声道分配
        /// </summary>
        public void UnassignChannel(ChannelConfiguration config, ChannelType channel)
        {
            if (config.Assignments.ContainsKey(channel))
            {
                config.Assignments[channel].DeviceId = null;
                AssignmentChanged?.Invoke(this, config.Assignments[channel]);
                SaveScenes();
            }
        }

        /// <summary>
        /// 设置声道延迟补偿
        /// </summary>
        public void SetChannelDelay(ChannelConfiguration config, ChannelType channel, int delayMs)
        {
            if (config.Assignments.ContainsKey(channel))
            {
                config.Assignments[channel].DelayMs = delayMs;
                AssignmentChanged?.Invoke(this, config.Assignments[channel]);
                SaveScenes();
            }
        }

        /// <summary>
        /// 设置声道增益
        /// </summary>
        public void SetChannelGain(ChannelConfiguration config, ChannelType channel, float gain)
        {
            if (config.Assignments.ContainsKey(channel))
            {
                config.Assignments[channel].Gain = Math.Clamp(gain, 0.0f, 2.0f);
                AssignmentChanged?.Invoke(this, config.Assignments[channel]);
                SaveScenes();
            }
        }

        /// <summary>
        /// 获取指定声道的设备
        /// </summary>
        public AudioDevice GetDeviceForChannel(ChannelConfiguration config, ChannelType channel, IEnumerable<AudioDevice> availableDevices)
        {
            if (config.Assignments.TryGetValue(channel, out var assignment))
            {
                return availableDevices.FirstOrDefault(d => d.DeviceId == assignment.DeviceId);
            }
            return null;
        }

        /// <summary>
        /// 获取设备分配的所有声道
        /// </summary>
        public List<ChannelType> GetChannelsForDevice(ChannelConfiguration config, string deviceId)
        {
            return config.Assignments
                .Where(kvp => kvp.Value.DeviceId == deviceId)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// 验证配置完整性
        /// </summary>
        public bool ValidateConfiguration(ChannelConfiguration config, IEnumerable<AudioDevice> availableDevices, out string error)
        {
            error = null;

            var requiredChannels = ChannelConfiguration.GetChannelsForLayout(config.Layout);
            var availableDeviceIds = availableDevices.Select(d => d.DeviceId).ToHashSet();

            foreach (var channel in requiredChannels)
            {
                if (!config.Assignments.TryGetValue(channel, out var assignment))
                {
                    error = $"声道 {channel} 未配置";
                    return false;
                }

                if (string.IsNullOrEmpty(assignment.DeviceId))
                {
                    error = $"声道 {channel} 未分配设备";
                    return false;
                }

                if (!availableDeviceIds.Contains(assignment.DeviceId))
                {
                    error = $"声道 {channel} 分配的设备不可用";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 保存场景配置
        /// </summary>
        private void SaveScenes()
        {
            try
            {
                Directory.CreateDirectory(CONFIG_DIR);
                var path = Path.Combine(CONFIG_DIR, SCENES_FILE);

                var serializer = new DataContractJsonSerializer(typeof(List<SceneConfiguration>));
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    serializer.WriteObject(stream, _scenes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存场景失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载场景配置
        /// </summary>
        private void LoadScenes()
        {
            try
            {
                var path = Path.Combine(CONFIG_DIR, SCENES_FILE);
                if (!File.Exists(path))
                {
                    // 创建默认场景
                    CreateDefaultScenes();
                    return;
                }

                var serializer = new DataContractJsonSerializer(typeof(List<SceneConfiguration>));
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    _scenes = (List<SceneConfiguration>)serializer.ReadObject(stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载场景失败: {ex.Message}");
                CreateDefaultScenes();
            }
        }

        /// <summary>
        /// 创建默认场景
        /// </summary>
        private void CreateDefaultScenes()
        {
            // 卧室场景 - 2.1 立体声 + 低音炮
            var bedroom = new SceneConfiguration("卧室");
            AddConfiguration(bedroom, "床头音响", AudioChannelLayout.TwoPointOne);

            // 客厅场景 - 5.1 环绕声
            var livingRoom = new SceneConfiguration("客厅");
            AddConfiguration(livingRoom, "家庭影院", AudioChannelLayout.FivePointOne);

            // 全屋场景 - 7.1 全景声
            var wholeHouse = new SceneConfiguration("全屋");
            AddConfiguration(wholeHouse, "全景声系统", AudioChannelLayout.SevenPointOne);

            SaveScenes();
        }

        /// <summary>
        /// 导出配置为 JSON
        /// </summary>
        public string ExportConfiguration(ChannelConfiguration config)
        {
            var serializer = new DataContractJsonSerializer(typeof(ChannelConfiguration));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, config);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// 从 JSON 导入配置
        /// </summary>
        public ChannelConfiguration ImportConfiguration(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(ChannelConfiguration));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (ChannelConfiguration)serializer.ReadObject(stream);
            }
        }
    }
}
