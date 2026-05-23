using System.Windows;
using WindowsSound.UI.ViewModels;

namespace WindowsSound.UI.Views
{
    /// <summary>
    /// 模式切换窗口
    /// </summary>
    public partial class ModeSwitchWindow : Window
    {
        private readonly ModeSwitchViewModel _viewModel;

        public ModeSwitchWindow()
        {
            InitializeComponent();
            _viewModel = new ModeSwitchViewModel();
            DataContext = _viewModel;

            _viewModel.ModeSwitched += OnModeSwitched;
        }

        private void OnModeSwitched(object sender, ModeSwitchedEventArgs e)
        {
            // 模式切换成功，关闭窗口
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 获取选中的模式
        /// </summary>
        public WorkMode GetSelectedMode()
        {
            return _viewModel.CurrentMode;
        }

        /// <summary>
        /// 设置当前模式
        /// </summary>
        public void SetCurrentMode(WorkMode mode)
        {
            _viewModel.CurrentMode = mode;
        }
    }
}
