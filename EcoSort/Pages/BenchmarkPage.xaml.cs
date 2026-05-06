using EcoSort.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace EcoSort.Pages;

public sealed partial class BenchmarkPage : Page
{
    private readonly GarbageClassificationService _classificationService;
    private readonly BenchmarkingService _benchmarkingService;
    public ObservableCollection<string> ProcessedImages { get; } = new();

    public BenchmarkPage()
    {
        this.InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Enabled;
        _classificationService = new GarbageClassificationService();
        _benchmarkingService = new BenchmarkingService(_classificationService);
    }

    private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        if (App.MainAppWindow is null)
        {
            return;
        }

        var windowHandle = WindowNative.GetWindowHandle(App.MainAppWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        await BenchmarkFolderAsync(folder);
    }

    private async Task BenchmarkFolderAsync(Windows.Storage.StorageFolder folder)
    {
        SelectFolderButton.IsEnabled = false;
        BenchmarkProgressRing.IsActive = true;
        BenchmarkProgressRing.Visibility = Visibility.Visible;
        ErrorInfoBar.IsOpen = false;
        ResultsInfoBar.IsOpen = false;
        ProcessedImages.Clear();

        try
        {
            StatusText.Text = $"Benchmarking folder: {folder.Name}...";

            var result = await _benchmarkingService.BenchmarkFolderAsync(folder);

            ProcessedImages.Clear();
            foreach (var imageName in result.ProcessedImages)
            {
                ProcessedImages.Add(imageName);
            }

            UpdateResultsUI(result);
            StatusText.Text = "Benchmark completed successfully!";
            ResultsInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            ErrorInfoBar.Message = ex.Message;
            ErrorInfoBar.IsOpen = true;
            StatusText.Text = "Benchmark failed.";
            ClearResultsUI();
        }
        finally
        {
            BenchmarkProgressRing.IsActive = false;
            BenchmarkProgressRing.Visibility = Visibility.Collapsed;
            SelectFolderButton.IsEnabled = true;
        }
    }

    private void UpdateResultsUI(BenchmarkingService.BenchmarkResult result)
    {
        ImageCountText.Text = $"{result.ImageCount} images";
        AverageLatencyText.Text = $"{result.AverageLatencyMs:F2} ms";
        MinLatencyText.Text = $"{result.MinLatencyMs:F2} ms";
        MaxLatencyText.Text = $"{result.MaxLatencyMs:F2} ms";
        TotalLatencyText.Text = $"{result.TotalLatencyMs:F2} ms";
    }

    private void ClearResultsUI()
    {
        ImageCountText.Text = "—";
        AverageLatencyText.Text = "—";
        MinLatencyText.Text = "—";
        MaxLatencyText.Text = "—";
        TotalLatencyText.Text = "—";
    }
}
