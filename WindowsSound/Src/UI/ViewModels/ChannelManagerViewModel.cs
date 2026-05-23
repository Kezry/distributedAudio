using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WindowsSound.ChannelManager;
using WindowsSound.Discovery;

namespace WindowsSound.UI.ViewModels
{
    /// <summary>
    /// 声道管理器视图模型
    /// </summary>
    public class ChannelManagerViewModel : INotifyPropertyChanged
    {
        private readonly ChannelManager _channelManager;
        private readonly ChannelToneGenerator _toneGenerator;

        private SceneConfiguration _selectedScene;
        private ChannelConfiguration _selectedConfiguration;
        private ChannelAssignment _selectedAssignment;
        private AudioChannelLayout _selectedLayout;
        private string _newSceneName;
        private bool _isTestPlaying;

        public event PropertyChangedEventHandler PropertyChanged;

        public ChannelManagerViewModel()
        {
            _channelManager = new ChannelManager();
            _toneGenerator = new ChannelToneGenerator();

            Scenes = new ObservableCollection<SceneConfiguration>();
            Layouts = new ObservableCollection<AudioChannelLayout>();
            AvailableDevices = new ObservableCollection<AudioDevice>();
            ChannelAssignments = new ObservableCollection<ChannelAssignment>();

            LoadScenes();
            LoadLayouts();

            // 命令
            CreateSceneCommand = new RelayCommand(CreateScene, CanCreateScene);
            DeleteSceneCommand = new RelayCommand<SceneConfiguration>(DeleteScene, CanDeleteScene);
            SelectSceneCommand = new RelayCommand<SceneConfiguration>(SelectScene);
            AddConfigurationCommand = new RelayCommand(AddConfiguration, CanAddConfiguration);
            DeleteConfigurationCommand = new RelayCommand<ChannelConfiguration>(DeleteConfiguration);
            SelectLayoutCommand = new RelayCommand<AudioChannelLayout>(SelectLayout);
            AssignDeviceCommand = new RelayCommand<ChannelAssignment>(AssignDevice);
            UnassignChannelCommand = new RelayCommand<ChannelAssignment>(UnassignChannel);
            PlayTestToneCommand = new RelayCommand<ChannelType>(PlayTestTone);
            StopTestToneCommand = new RelayCommand(StopTestTone);
            PlaySweepCommand = new RelayCommand<ChannelType>(PlaySweep);
            SaveCommand = new RelayCommand(Save);
        }

        // 集合
        public ObservableCollection<SceneConfiguration> Scenes { get; }
        public ObservableCollection<AudioChannelLayout> Layouts { get; }
        public ObservableCollection<AudioDevice> AvailableDevices { get; }
        public ObservableCollection<ChannelAssignment> ChannelAssignments { get; }

        // 属性
        public SceneConfiguration SelectedScene
        {
            get => _selectedScene;
            set
            {
                _selectedScene = value;
                OnPropertyChanged();
                LoadConfigurations();
            }
        }

        public ChannelConfiguration SelectedConfiguration
        {
            get => _selectedConfiguration;
            set
            {
                _selectedConfiguration = value;
                OnPropertyChanged();
                LoadAssignments();
            }
        }

        public ChannelAssignment SelectedAssignment
        {
            get => _selectedAssignment;
            set
            {
                _selectedAssignment = value;
                OnPropertyChanged();
            }
        }

        public AudioChannelLayout SelectedLayout
        {
            get => _selectedLayout;
            set
            {
                _selectedLayout = value;
                OnPropertyChanged();
            }
        }

        public string NewSceneName
        {
            get => _newSceneName;
            set
            {
                _newSceneName = value;
                OnPropertyChanged();
                ((RelayCommand)CreateSceneCommand).NotifyCanExecuteChanged();
            }
        }

        public bool IsTestPlaying
        {
            get => _isTestPlaying;
            set
            {
                _isTestPlaying = value;
                OnPropertyChanged();
            }
        }

        // 命令
        public ICommand CreateSceneCommand { get; }
        public ICommand DeleteSceneCommand { get; }
        public ICommand SelectSceneCommand { get; }
        public ICommand AddConfigurationCommand { get; }
        public ICommand DeleteConfigurationCommand { get; }
        public ICommand SelectLayoutCommand { get; }
        public ICommand AssignDeviceCommand { get; }
        public ICommand UnassignChannelCommand { get; }
        public ICommand PlayTestToneCommand { get; }
        public ICommand StopTestToneCommand { get; }
        public ICommand PlaySweepCommand { get; }
        public ICommand SaveCommand { get; }

        // 方法
        private void LoadScenes()
        {
            Scenes.Clear();
            foreach (var scene in _channelManager.Scenes)
            {
                Scenes.Add(scene);
            }

            if (Scenes.Any())
            {
                SelectedScene = Scenes[0];
            }
        }

        private void LoadConfigurations()
        {
            ChannelAssignments.Clear();
            if (SelectedScene?.Configurations != null)
            {
                foreach (var config in SelectedScene.Configurations)
                {
                    foreach (var kvp in config.Assignments)
                    {
                        ChannelAssignments.Add(kvp.Value);
                    }
                }
            }
        }

        private void LoadAssignments()
        {
            ChannelAssignments.Clear();
            if (SelectedConfiguration?.Assignments != null)
            {
                foreach (var kvp in SelectedConfiguration.Assignments)
                {
                    ChannelAssignments.Add(kvp.Value);
                }
            }
        }

        private void LoadLayouts()
        {
            Layouts.Clear();
            Layouts.Add(AudioChannelLayout.Stereo);
            Layouts.Add(AudioChannelLayout.TwoPointOne);
            Layouts.Add(AudioChannelLayout.FivePointOne);
            Layouts.Add(AudioChannelLayout.SevenPointOne);
        }

        private bool CanCreateScene()
        {
            return !string.IsNullOrWhiteSpace(NewSceneName);
        }

        private void CreateScene()
        {
            var scene = _channelManager.CreateScene(NewSceneName);
            Scenes.Add(scene);
            SelectedScene = scene;
            NewSceneName = null;
        }

        private bool CanDeleteScene(SceneConfiguration scene)
        {
            return scene != null && Scenes.Count > 1;
        }

        private void DeleteScene(SceneConfiguration scene)
        {
            _channelManager.DeleteScene(scene.SceneName);
            Scenes.Remove(scene);
            if (SelectedScene == scene && Scenes.Any())
            {
                SelectedScene = Scenes[0];
            }
        }

        private void SelectScene(SceneConfiguration scene)
        {
            _channelManager.SetCurrentScene(scene.SceneName);
        }

        private bool CanAddConfiguration()
        {
            return SelectedScene != null && SelectedLayout != default;
        }

        private void AddConfiguration()
        {
            if (SelectedScene == null || SelectedLayout == default) return;

            var configName = $"{SelectedLayout} 配置";
            var config = _channelManager.AddConfiguration(SelectedScene, configName, SelectedLayout);
            SelectedConfiguration = config;
        }

        private void DeleteConfiguration(ChannelConfiguration config)
        {
            if (SelectedScene == null || config == null) return;

            SelectedScene.Configurations.Remove(config);
            if (SelectedConfiguration == config)
            {
                SelectedConfiguration = SelectedScene.Configurations.FirstOrDefault();
            }
        }

        private void SelectLayout(AudioChannelLayout layout)
        {
            SelectedLayout = layout;
        }

        private void AssignDevice(ChannelAssignment assignment)
        {
            // 弹出设备选择对话框（由View处理）
            // 这里只存储选中的设备
        }

        public void AssignDeviceToChannel(ChannelType channel, AudioDevice device)
        {
            if (SelectedConfiguration == null || device == null) return;

            _channelManager.AssignDevice(SelectedConfiguration, channel, device);
            LoadAssignments();
        }

        private void UnassignChannel(ChannelAssignment assignment)
        {
            if (SelectedConfiguration == null || assignment == null) return;

            _channelManager.UnassignChannel(SelectedConfiguration, assignment.Channel);
            LoadAssignments();
        }

        private void PlayTestTone(ChannelType channel)
        {
            IsTestPlaying = true;
            _toneGenerator.Frequency = 440;
            _toneGenerator.Volume = 0.5;
            _toneGenerator.PlayTone(channel, 3000);
        }

        private void PlaySweep(ChannelType channel)
        {
            IsTestPlaying = true;
            _toneGenerator.PlaySweepTone(channel, 200, 2000, 3000);
        }

        private void StopTestTone()
        {
            _toneGenerator.Stop();
            IsTestPlaying = false;
        }

        private void Save()
        {
            // 配置已自动保存
        }

        public void SetAvailableDevices(System.Collections.Generic.IEnumerable<AudioDevice> devices)
        {
            AvailableDevices.Clear();
            foreach (var device in devices)
            {
                AvailableDevices.Add(device);
            }
        }

        public ChannelConfiguration GetActiveConfiguration()
        {
            return SelectedConfiguration;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
