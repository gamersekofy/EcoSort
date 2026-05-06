using EcoSort.Services;
using Microsoft.UI.Xaml;
using System;
using Windows.Storage;

namespace EcoSort
{
    public partial class App : Application
    {
        private const string ExperimentalLiveInferenceSettingKey = "ExperimentalLiveInferenceEnabled";
        private const string ExperimentalLiveInferenceCooldownSecondsSettingKey = "ExperimentalLiveInferenceCooldownSeconds";

        public static Window? MainAppWindow { get; private set; }

        public static ClassificationHistoryService History { get; } = new();

        public static bool ExperimentalLiveInferenceEnabled
        {
            get
            {
                var values = ApplicationData.Current.LocalSettings.Values;
                if (values.TryGetValue(ExperimentalLiveInferenceSettingKey, out var value) && value is bool enabled)
                {
                    return enabled;
                }

                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[ExperimentalLiveInferenceSettingKey] = value;
            }
        }

        // Deprecated: Kept for backward compatibility. Frame throttling is now handled internally by LiveInferenceService.
        [Obsolete("Use LiveInferenceService's built-in frame throttling instead")]
        public static int ExperimentalLiveInferenceCooldownSeconds
        {
            get
            {
                var values = ApplicationData.Current.LocalSettings.Values;
                if (values.TryGetValue(ExperimentalLiveInferenceCooldownSecondsSettingKey, out var value) && value is int seconds)
                {
                    return Math.Clamp(seconds, 2, 12);
                }

                return 4;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[ExperimentalLiveInferenceCooldownSecondsSettingKey] = Math.Clamp(value, 2, 12);
            }
        }

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
