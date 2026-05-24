using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace WindowsSound.UI.ViewModels
{
    /// <summary>
    /// 工作模式
    /// </summary>
    public enum WorkMode
    {
        /// <summary>
        /// 声卡模式 - 低延迟、高同步精度
        /// </summary>
        SoundCard,

        /// <summary>
        /// DLNA 单机模式 - 标准DLNA播放
        /// </summary>
        DlnaSingle,

        /// <summary>
        /// DLNA 多机模式 - 需要同步校准
        /// </summary>
        DlnaMulti
    }

    /// <summary>
    /// 模式切换视图模型
    /// </summary>
    public class ModeSwitchViewModel : INotifyPropertyChanged
    {
        private WorkMode _currentMode;
        private bool _isModeChanging;

        public event PropertyChangedEventHandler PropertyChanged;

        public ModeSwitchViewModel()
        {
            CurrentMode = WorkMode.SoundCard;

            SwitchModeCommand = new RelayCommand<WorkMode>(SwitchMode, CanSwitchMode);
        }

        public WorkMode CurrentMode
        {
            get => _currentMode;
            set
            {
                _currentMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSoundCardMode));
                OnPropertyChanged(nameof(IsDlnaSingleMode));
                OnPropertyChanged(nameof(IsDlnaMultiMode));
                OnPropertyChanged(nameof(ModeDescription));
                OnPropertyChanged(nameof(ModeLatencyHint));
            }
        }

        public bool IsSoundCardMode => CurrentMode == WorkMode.SoundCard;
        public bool IsDlnaSingleMode => CurrentMode == WorkMode.DlnaSingle;
        public bool IsDlnaMultiMode => CurrentMode == WorkMode.DlnaMulti;

        /// <summary>
        /// 模式描述
        /// </summary>
        public string ModeDescription => CurrentMode switch
        {
            WorkMode.SoundCard => "声卡模式：使用自定义协议，低延迟、高同步精度，推荐用于多设备同步播放",
            WorkMode.DlnaSingle => "DLNA单机：标准DLNA协议，兼容性最好，适合单设备播放",
            WorkMode.DlnaMulti => "DLNA多机：增强DLNA协议，支持多设备同步，需要延迟校准",
            _ => "未知模式"
        };

        /// <summary>
        /// 延迟提示
        /// </summary>
        public string ModeLatencyHint => CurrentMode switch
        {
            WorkMode.SoundCard => "端到端延迟: 40-80ms | 设备间偏差: 5-20ms",
            WorkMode.DlnaSingle => "端到端延迟: 100-300ms | 单设备播放",
            WorkMode.DlnaMulti => "端到端延迟: 150-500ms | 设备间偏差: 20-100ms (需校准)",
            _ => ""
        };

        public bool IsModeChanging
        {
            get => _isModeChanging;
            set
            {
                _isModeChanging = value;
                OnPropertyChanged();
            }
        }

        public ICommand SwitchModeCommand { get; }

        private bool CanSwitchMode(WorkMode mode)
        {
            return !IsModeChanging && mode != CurrentMode;
        }

        private void SwitchMode(WorkMode mode)
        {
            if (!CanSwitchMode(mode)) return;

            IsModeChanging = true;
            CurrentMode = mode;

            // 触发模式切换事件
            ModeSwitched?.Invoke(this, new ModeSwitchedEventArgs(mode));

            IsModeChanging = false;
        }

        public event EventHandler<ModeSwitchedEventArgs> ModeSwitched;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 模式切换事件参数
    /// </summary>
    public class ModeSwitchedEventArgs : EventArgs
    {
        public WorkMode NewMode { get; }

        public ModeSwitchedEventArgs(WorkMode newMode)
        {
            NewMode = newMode;
        }
    }
}
