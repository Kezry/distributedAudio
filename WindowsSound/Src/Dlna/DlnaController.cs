using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WindowsSound.Discovery;

namespace WindowsSound.Dlna
{
    /// <summary>
    /// DLNA 控制器
    /// 管理DLNA多机组的统一播放和同步
    /// </summary>
    public class DlnaController
    {
        private const int CONTROL_PORT = 5006;
        private const int REQUEST_TIMEOUT_MS = 5000;

        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, DlnaGroup> _groups;

        public event EventHandler<GroupStateChangedEventArgs> GroupStateChanged;
        public event EventHandler<SyncStatusEventArgs> SyncStatusUpdate;

        public IReadOnlyDictionary<string, DlnaGroup> Groups => _groups;

        public DlnaController()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(REQUEST_TIMEOUT_MS)
            };
            _groups = new Dictionary<string, DlnaGroup>();
        }

        /// <summary>
        /// 创建新的DLNA组
        /// </summary>
        public DlnaGroup CreateGroup(string name, bool syncTogether)
        {
            var group = new DlnaGroup(name, syncTogether);
            _groups[name] = group;

            GroupStateChanged?.Invoke(this, new GroupStateChangedEventArgs
            {
                GroupName = name,
                Action = GroupAction.Created
            });

            return group;
        }

        /// <summary>
        /// 删除DLNA组
        /// </summary>
        public void DeleteGroup(string name)
        {
            if (_groups.TryGetValue(name, out var group))
            {
                // 通知所有设备退出组
                foreach (var member in group.Members)
                {
                    SendLeaveGroupCommand(member.Device).Wait();
                }

                _groups.Remove(name);

                GroupStateChanged?.Invoke(this, new GroupStateChangedEventArgs
                {
                    GroupName = name,
                    Action = GroupAction.Deleted
                });
            }
        }

        /// <summary>
        /// 添加设备到组
        /// </summary>
        public async Task<bool> AddDeviceToGroupAsync(string groupName, AudioDevice device, int delayMs = 0)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                return false;
            }

            // 发送加入组命令
            var success = await SendJoinGroupCommand(device, groupName, delayMs, group.SyncTogether);

            if (success)
            {
                group.AddMember(device, delayMs);

                GroupStateChanged?.Invoke(this, new GroupStateChangedEventArgs
                {
                    GroupName = groupName,
                    DeviceId = device.DeviceId,
                    Action = GroupAction.DeviceAdded
                });
            }

            return success;
        }

        /// <summary>
        /// 从组中移除设备
        /// </summary>
        public async Task RemoveDeviceFromGroupAsync(string groupName, AudioDevice device)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                await SendLeaveGroupCommand(device);
                group.RemoveMember(device.DeviceId);

                GroupStateChanged?.Invoke(this, new GroupStateChangedEventArgs
                {
                    GroupName = groupName,
                    DeviceId = device.DeviceId,
                    Action = GroupAction.DeviceRemoved
                });
            }
        }

        /// <summary>
        /// 统一起播 (PLAY_AT)
        /// </summary>
        public async Task<bool> PlayGroupAtAsync(string groupName, long timestampMs, string mediaUrl = null)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                return false;
            }

            var tasks = new List<Task<bool>>();

            foreach (var member in group.Members)
            {
                long adjustedTimestamp = timestampMs + member.DelayMs;
                tasks.Add(SendPlayAtCommand(member.Device, adjustedTimestamp, mediaUrl));
            }

            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }

        /// <summary>
        /// 停止组播放
        /// </summary>
        public async Task StopGroupAsync(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                var tasks = group.Members.Select(m => SendStopCommand(m.Device));
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// 同步状态查询
        /// </summary>
        public async Task<Dictionary<string, int>> QueryGroupSyncStatusAsync(string groupName)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                return new Dictionary<string, int>();
            }

            var tasks = new List<Task<(string, int)>>();

            foreach (var member in group.Members)
            {
                tasks.Add(QueryDeviceSyncStatus(member.Device));
            }

            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(r => r.Item1, r => r.Item2);
        }

        /// <summary>
        /// 发送PLAY_AT命令
        /// </summary>
        private async Task<bool> SendPlayAtCommand(AudioDevice device, long timestampMs, string mediaUrl)
        {
            try
            {
                var command = new
                {
                    command = "PLAY_AT",
                    params = new
                    {
                        timestamp = timestampMs,
                        mediaUrl = mediaUrl ?? ""
                    }
                };

                return await SendCommandAsync(device, command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendPlayAtCommand error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 发送STOP命令
        /// </summary>
        private async Task<bool> SendStopCommand(AudioDevice device)
        {
            try
            {
                var command = new
                {
                    command = "STOP"
                };

                return await SendCommandAsync(device, command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendStopCommand error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 发送加入组命令
        /// </summary>
        private async Task<bool> SendJoinGroupCommand(AudioDevice device, string groupId, int delayMs, bool syncTogether)
        {
            try
            {
                var command = new
                {
                    command = "JOIN_GROUP",
                    params = new
                    {
                        groupId = groupId,
                        delayMs = delayMs,
                        syncTogether = syncTogether
                    }
                };

                return await SendCommandAsync(device, command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendJoinGroupCommand error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 发送退出组命令
        /// </summary>
        private async Task<bool> SendLeaveGroupCommand(AudioDevice device)
        {
            try
            {
                var command = new
                {
                    command = "LEAVE_GROUP"
                };

                return await SendCommandAsync(device, command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendLeaveGroupCommand error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 查询设备同步状态
        /// </summary>
        private async Task<(string deviceId, int offset)> QueryDeviceSyncStatus(AudioDevice device)
        {
            try
            {
                var command = new
                {
                    command = "GET_SYNC_STATUS"
                };

                var response = await SendCommandWithResponseAsync(device, command);

                if (response != null && response.TryGetProperty("offset", out var offsetProp))
                {
                    return (device.DeviceId, offsetProp.GetInt32());
                }

                return (device.DeviceId, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QueryDeviceSyncStatus error: {ex.Message}");
                return (device.DeviceId, 0);
            }
        }

        /// <summary>
        /// 发送命令到设备
        /// </summary>
        private async Task<bool> SendCommandAsync(AudioDevice device, object command)
        {
            try
            {
                string url = $"http://{device.IpAddress}:{CONTROL_PORT}/api/dlna/control";
                var json = JsonSerializer.Serialize(command);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 发送命令并获取响应
        /// </summary>
        private async Task<JsonElement?> SendCommandWithResponseAsync(AudioDevice device, object command)
        {
            try
            {
                string url = $"http://{device.IpAddress}:{CONTROL_PORT}/api/dlna/control";
                var json = JsonSerializer.Serialize(command);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<JsonElement>(responseJson);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// DLNA 组
    /// </summary>
    public class DlnaGroup
    {
        public string Name { get; set; }
        public bool SyncTogether { get; set; }
        public List<GroupMember> Members { get; set; }

        public DlnaGroup(string name, bool syncTogether)
        {
            Name = name;
            SyncTogether = syncTogether;
            Members = new List<GroupMember>();
        }

        public void AddMember(AudioDevice device, int delayMs)
        {
            Members.Add(new GroupMember
            {
                Device = device,
                DelayMs = delayMs,
                JoinedAt = DateTime.Now
            });
        }

        public void RemoveMember(string deviceId)
        {
            Members.RemoveAll(m => m.Device.DeviceId == deviceId);
        }
    }

    /// <summary>
    /// 组成员
    /// </summary>
    public class GroupMember
    {
        public AudioDevice Device { get; set; }
        public int DelayMs { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    /// <summary>
    /// 组状态变化事件参数
    /// </summary>
    public class GroupStateChangedEventArgs : EventArgs
    {
        public string GroupName { get; set; }
        public string DeviceId { get; set; }
        public GroupAction Action { get; set; }
    }

    /// <summary>
    /// 组操作类型
    /// </summary>
    public enum GroupAction
    {
        Created,
        Deleted,
        DeviceAdded,
        DeviceRemoved
    }

    /// <summary>
    /// 同步状态事件参数
    /// </summary>
    public class SyncStatusEventArgs : EventArgs
    {
        public string GroupName { get; set; }
        public Dictionary<string, int> DeviceOffsets { get; set; }
    }
}
