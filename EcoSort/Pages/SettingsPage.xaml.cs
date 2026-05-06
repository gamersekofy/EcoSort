using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace EcoSort.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitializing;

    public SettingsPage()
    {
        InitializeComponent();

        _isInitializing = true;
        LiveInferenceToggle.IsOn = App.ExperimentalLiveInferenceEnabled;
        var cooldownTag = App.ExperimentalLiveInferenceCooldownSeconds.ToString();
        LiveInferenceCooldownCombo.SelectedItem = LiveInferenceCooldownCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(x => (x.Tag as string) == cooldownTag)
            ?? LiveInferenceCooldownCombo.Items.OfType<ComboBoxItem>().First();
        _isInitializing = false;
    }

    private void LiveInferenceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        App.ExperimentalLiveInferenceEnabled = LiveInferenceToggle.IsOn;
    }

    private void LiveInferenceCooldownCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        if (LiveInferenceCooldownCombo.SelectedItem is ComboBoxItem item
            && int.TryParse(item.Tag as string, out var seconds))
        {
            App.ExperimentalLiveInferenceCooldownSeconds = seconds;
        }
    }
}
