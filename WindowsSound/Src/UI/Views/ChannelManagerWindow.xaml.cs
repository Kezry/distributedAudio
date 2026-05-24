using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DistributedAudio.AudioCapture;
using DistributedAudio.DeviceDiscovery;
using WindowsSound.ChannelManager;
using WindowsSound.UI.ViewModels;

namespace WindowsSound.UI.Views
{
    /// <summary>
    /// 声道管理器窗口
    /// </summary>
    public partial class ChannelManagerWindow : Window
    {
        private ChannelManagerViewModel ViewModel => (ChannelManagerViewModel)DataContext;

        public ChannelManagerWindow()
        {
            InitializeComponent();
            this.Loaded += ChannelManagerWindow_Loaded;
        }

        private void ChannelManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatus("声道管理器已加载");
        }

        /// <summary>
        /// 设置可用设备列表
        /// </summary>
        public void SetAvailableDevices(System.Collections.Generic.IEnumerable<AudioDevice> devices)
        {
            ViewModel?.SetAvailableDevices(devices);
            UpdateStatus($"已加载 {devices.Count()} 个可用设备");
        }

        /// <summary>
        /// 设备项拖拽开始
        /// </summary>
        private void DeviceItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioDevice device)
            {
                DragDrop.DoDragDrop(border, new DataObject(typeof(AudioDevice), device), DragDropEffects.Move);
            }
        }

        /// <summary>
        /// 声道项拖拽进入
        /// </summary>
        private void ChannelItem_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(AudioDevice)) != null)
            {
                if (sender is Border border)
                {
                    border.BorderBrush = new BrushConverter().ConvertFromString("#4CAF50") as Brush;
                    border.BorderThickness = new Thickness(2);
                }
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 声道项拖拽离开
        /// </summary>
        private void ChannelItem_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = new BrushConverter().ConvertFromString("#3a3a3a") as Brush;
                border.BorderThickness = new Thickness(1);
            }
        }

        /// <summary>
        /// 设备拖放到声道
        /// </summary>
        private void ChannelItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border && border.DataContext is ChannelAssignment assignment &&
                e.Data.GetData(typeof(AudioDevice)) is AudioDevice device)
            {
                // 恢复边框样式
                border.BorderBrush = new BrushConverter().ConvertFromString("#3a3a3a") as Brush;
                border.BorderThickness = new Thickness(1);

                // 分配设备到声道
                ViewModel.AssignDeviceToChannel(assignment.Channel, device);
                UpdateStatus($"已分配 {device.Alias} 到 {assignment.Channel}");
            }
        }

        /// <summary>
        /// 布局选择变化
        /// </summary>
        private void LayoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectedLayout != default)
            {
                UpdateStatus($"已选择布局: {ViewModel.SelectedLayout}");
            }
        }

        /// <summary>
        /// 添加配置按钮
        /// </summary>
        private void AddConfig_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedLayout == default)
            {
                MessageBox.Show("请先选择声道布局", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 播放所有声道测试音
        /// </summary>
        private void PlayAllChannelsTest_Click(object sender, RoutedEventArgs e)
        {
            var config = ViewModel.GetActiveConfiguration();
            if (config == null)
            {
                MessageBox.Show("请先选择一个配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 逐个播放各声道测试音
            var channels = ChannelConfiguration.GetChannelsForLayout(config.Layout);
            PlayChannelTestSequential(channels, 0);
        }

        /// <summary>
        /// 顺序播放各声道测试音
        /// </summary>
        private void PlayChannelTestSequential(System.Collections.Generic.List<ChannelType> channels, int index)
        {
            if (index >= channels.Count)
            {
                UpdateStatus("声道测试完成");
                ViewModel.StopTestTone();
                return;
            }

            var channel = channels[index];
            ViewModel.PlayTestTone(channel);
            UpdateStatus($"正在测试: {channel}");

            // 3秒后播放下一个
            var timer = new System.Timers.Timer(3000);
            timer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    PlayChannelTestSequential(channels, index + 1);
                });
                timer.Dispose();
            };
            timer.AutoReset = false;
            timer.Start();
        }

        /// <summary>
        /// 延迟校准
        /// </summary>
        private void Calibration_Click(object sender, RoutedEventArgs e)
        {
            var config = ViewModel.GetActiveConfiguration();
            if (config == null)
            {
                MessageBox.Show("请先选择一个配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 打开延迟校准对话框
            var calibrationDialog = new CalibrationDialog(config);
            if (calibrationDialog.ShowDialog() == true)
            {
                UpdateStatus("延迟校准已完成");
            }
        }

        /// <summary>
        /// 更新状态栏
        /// </summary>
        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ViewModel?.StopTestTone();
            base.OnClosing(e);
        }
    }

    /// <summary>
    /// 延迟校准对话框
    /// </summary>
    public partial class CalibrationDialog : Window
    {
        private readonly ChannelConfiguration _config;

        public CalibrationDialog(ChannelConfiguration config)
        {
            _config = config;
            InitializeComponent();
            Title = "延迟校准";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            BuildUI();
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.Background = new BrushConverter().ConvertFromString("#1e1e1e") as Brush;

            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            // 标题
            var title = new TextBlock
            {
                Text = "为每个设备设置延迟补偿（ms）",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new BrushConverter().ConvertFromString("#e0e0e0") as Brush,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(title);

            // 为每个分配的声道创建延迟调整控件
            foreach (var kvp in _config.Assignments.OrderBy(a => a.Value.Channel))
            {
                var channel = kvp.Value;
                if (string.IsNullOrEmpty(channel.DeviceId)) continue;

                var panel = new StackPanel { Margin = new Thickness(0, 5) };

                var label = new TextBlock
                {
                    Text = $"{channel.Channel}: {channel.DeviceId}",
                    Foreground = new BrushConverter().ConvertFromString("#e0e0e0") as Brush
                };
                panel.Children.Add(label);

                var slider = new Slider
                {
                    Minimum = -200,
                    Maximum = 200,
                    Value = channel.DelayMs,
                    TickFrequency = 10,
                    IsSnapToTickEnabled = true,
                    Margin = new Thickness(0, 5),
                    Foreground = new BrushConverter().ConvertFromString("#e0e0e0") as Brush
                };

                var valueText = new TextBlock
                {
                    Text = $"{channel.DelayMs} ms",
                    Foreground = new BrushConverter().ConvertFromString("#80c080") as Brush,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                slider.ValueChanged += (s, e) =>
                {
                    channel.DelayMs = (int)e.NewValue;
                    valueText.Text = $"{channel.DelayMs} ms";
                };

                panel.Children.Add(slider);
                stackPanel.Children.Add(panel);
                stackPanel.Children.Add(valueText);
            }

            // 按钮
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var okButton = new Button
            {
                Content = "确定",
                Padding = new Thickness(15, 5),
                Margin = new Thickness(0, 0, 10, 0)
            };
            okButton.Click += (s, e) => DialogResult = true;

            var cancelButton = new Button
            {
                Content = "取消",
                Padding = new Thickness(15, 5)
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            scrollViewer.Content = stackPanel;
            grid.Children.Add(scrollViewer);

            AddChild(grid);
        }
    }
}
