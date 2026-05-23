using System.Windows;
using DistributedAudio.UI.ViewModels;

namespace DistributedAudio.UI.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }

    /// <summary>
    /// App.xaml.cs
    /// </summary>
    public partial class App : Application
    {
        public static new App Current => (App)Application.Current;

        public System.Windows.Threading.Dispatcher Dispatcher => Dispatcher;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
