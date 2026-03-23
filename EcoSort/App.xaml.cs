using EcoSort.Services;
using Microsoft.UI.Xaml;

namespace EcoSort
{
    public partial class App : Application
    {
        public static Window? MainAppWindow { get; private set; }

        public static ClassificationHistoryService History { get; } = new();

        public App()
        {
            InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await History.InitializeAsync();

            MainAppWindow = new MainWindow();
            MainAppWindow.Activate();
        }
    }
}
