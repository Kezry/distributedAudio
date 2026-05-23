using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace DistributedAudio.UI.Services
{
    /// <summary>
    /// 全局快捷键服务
    /// </summary>
    public class HotKeyService : IDisposable
    {
        private readonly Dictionary<int, Action> _hotKeys = new();
        private IntPtr _windowHandle;
        private const int WM_HOTKEY = 0x0312;
        private bool _disposed;
        private int _currentId = 1;

        public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

        public class HotKeyEventArgs : EventArgs
        {
            public int Id { get; set; }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
        }

        /// <summary>
        /// 注册全局快捷键
        /// </summary>
        public int RegisterHotKey(Key key, ModifierKeys modifiers, Action action)
        {
            var id = _currentId++;

            _hotKeys[id] = action;

            // In a real implementation, this would use Windows API
            // to register the hotkey globally
            // RegisterHotKey(windowHandle, id, modifiers, key);

            return id;
        }

        /// <summary>
        /// 注销快捷键
        /// </summary>
        public void UnregisterHotKey(int id)
        {
            _hotKeys.Remove(id);

            // UnregisterHotKey(windowHandle, id);
        }

        /// <summary>
        /// 处理快捷键按下
        /// </summary>
        public void OnHotKeyPressed(int id)
        {
            if (_hotKeys.TryGetValue(id, out var action))
            {
                action?.Invoke();
            }

            HotKeyPressed?.Invoke(this, new HotKeyEventArgs { Id = id });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Unregister all hotkeys
                foreach (var id in _hotKeys.Keys)
                {
                    // UnregisterHotKey(windowHandle, id);
                }

                _hotKeys.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 系统集成服务
    /// </summary>
    public class SystemIntegrationService
    {
        private readonly TrayIconService _trayIconService;
        private readonly HotKeyService _hotKeyService;
        private readonly Window _mainWindow;

        public SystemIntegrationService(Window mainWindow)
        {
            _mainWindow = mainWindow;
            _trayIconService = new TrayIconService();
            _hotKeyService = new HotKeyService();

            InitializeServices();
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        private void InitializeServices()
        {
            // Initialize tray icon
            _trayIconService.Initialize();
            _trayIconService.ShowMainWindow += (s, e) => ShowMainWindow();
            _trayIconService.ExitApplication += (s, e) => ExitApplication();

            // Register global hotkeys
            _hotKeyService.RegisterHotKey(
                Key.O,
                ModifierKeys.Control | ModifierKeys.Shift,
                () => ShowMainWindow()
            );

            _hotKeyService.RegisterHotKey(
                Key.S,
                ModifierKeys.Control | ModifierKeys.Shift,
                () => ToggleStreaming()
            );
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow()
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.WindowState = WindowState.Normal;
        }

        /// <summary>
        /// 最小化到托盘
        /// </summary>
        public void MinimizeToTray()
        {
            _mainWindow.Hide();
            _trayIconService.ShowNotification("已最小化", "程序已最小化到系统托盘");
        }

        /// <summary>
        /// 切换流式传输状态
        /// </summary>
        private void ToggleStreaming()
        {
            // In a real implementation, this would toggle the streaming state
            _trayIconService.ShowNotification("快捷键", "流式传输切换");
        }

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        public void SetAutoStart(bool enable)
        {
            try
            {
                var appName = "DistributedAudio";
                var executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enable)
                    {
                        key?.SetValue(appName, executablePath);
                    }
                    else
                    {
                        key?.DeleteValue(appName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set autostart: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否已设置开机自启动
        /// </summary>
        public bool IsAutoStartEnabled()
        {
            try
            {
                var appName = "DistributedAudio";

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    var value = key?.GetValue(appName);
                    return value != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 退出应用
        /// </summary>
        private void ExitApplication()
        {
            _mainWindow.Close();
        }

        public void Dispose()
        {
            _trayIconService?.Dispose();
            _hotKeyService?.Dispose();
        }
    }
}
