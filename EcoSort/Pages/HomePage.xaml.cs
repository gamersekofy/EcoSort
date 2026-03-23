using EcoSort.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Windows.System;

namespace EcoSort.Pages;

public sealed partial class HomePage : Page
{
    private readonly string[] _tips =
    {
        "• Rinse recyclable containers to avoid contamination.",
        "• Flatten cardboard before placing it in recycling.",
        "• Donate usable clothes and shoes when possible.",
        "• Separate batteries from household trash for safer disposal."
    };

    private readonly DispatcherTimer _tipsTimer;
    private int _tipIndex;

    public HomePage()
    {
        InitializeComponent();

        QuickActionsFlipView.ItemsSource = QuickActions;
        RecentFlipView.ItemsSource = RecentItems;
        RecentItems.CollectionChanged += RecentItems_CollectionChanged;

        _tipsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(6)
        };
        _tipsTimer.Tick += TipsTimer_Tick;

        SetCurrentTip();
        UpdateRecentState();

        Loaded += HomePage_Loaded;
        Unloaded += HomePage_Unloaded;
    }

    private ObservableCollection<RecentClassificationItem> RecentItems => App.History.Items;

    private ObservableCollection<QuickActionCard> QuickActions { get; } =
    [
        new("Classify a new item", "Start image classification using upload, drag-and-drop, or camera capture.", "Open Classifier", "open-classify"),
        new("Learn disposal basics", "Review practical recycling and composting guidance in the educational hub.", "Open Education", "open-education"),
        new("Project repository", "View EcoSort source code and ongoing roadmap updates.", "Open GitHub", "open-github"),
        new("Windows AI + WinUI docs", "Explore official Microsoft docs for Windows AI and modern WinUI patterns.", "Open Docs", "open-docs")
    ];

    private void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        _tipsTimer.Start();
    }

    private void HomePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _tipsTimer.Stop();
    }

    private void RecentItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateRecentState();
    }

    private void TipsTimer_Tick(object? sender, object e)
    {
        _tipIndex = (_tipIndex + 1) % _tips.Length;
        SetCurrentTip();
    }

    private void SetCurrentTip()
    {
        RotatingTipText.Text = _tips[_tipIndex];
    }

    private void UpdateRecentState()
    {
        var hasItems = RecentItems.Count > 0;
        RecentFlipView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        NoRecentItemsText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ClearHistoryButton.IsEnabled = hasItems;
    }

    private async void QuickActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string actionKey)
        {
            return;
        }

        switch (actionKey)
        {
            case "open-classify":
                Frame?.Navigate(typeof(ClassifyPage));
                break;
            case "open-education":
                Frame?.Navigate(typeof(EducationPage));
                break;
            case "open-github":
                await LaunchUriAsync("https://github.com/gamersekofy/EcoSort");
                break;
            case "open-docs":
                await LaunchUriAsync("https://learn.microsoft.com/windows/ai/");
                break;
        }
    }

    private static async Task LaunchUriAsync(string uri)
    {
        await Launcher.LaunchUriAsync(new Uri(uri));
    }

    private void DeleteHotspot_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement hotspot && hotspot.Parent is FrameworkElement cardRoot && cardRoot.FindName("RemoveHistoryItemButton") is Button removeButton)
        {
            removeButton.Visibility = Visibility.Visible;
        }
    }

    private void RemoveHistoryItemButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Visibility = Visibility.Visible;
        }
    }

    private void HistoryCard_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement cardRoot && cardRoot.FindName("RemoveHistoryItemButton") is Button removeButton)
        {
            removeButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void RemoveHistoryItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not RecentClassificationItem item)
        {
            return;
        }

        await App.History.RemoveAsync(item);
    }

    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await App.History.ClearAsync();
    }

    private sealed record QuickActionCard(string Title, string Description, string ActionLabel, string ActionKey);
}
