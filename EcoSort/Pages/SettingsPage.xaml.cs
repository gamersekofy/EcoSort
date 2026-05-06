using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EcoSort.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        LiveInferenceToggle.IsOn = App.ExperimentalLiveInferenceEnabled;
    }

    private void LiveInferenceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        App.ExperimentalLiveInferenceEnabled = LiveInferenceToggle.IsOn;
    }
}
