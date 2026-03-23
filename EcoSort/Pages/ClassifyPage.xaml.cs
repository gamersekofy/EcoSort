using EcoSort.Models;
using EcoSort.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace EcoSort.Pages;

public sealed partial class ClassifyPage : Page
{
    private readonly GarbageClassificationService _classificationService;

    private readonly SolidColorBrush _defaultDropBrush;
    private readonly SolidColorBrush _hoverDropBrush;

    public ClassifyPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
        _classificationService = new GarbageClassificationService();

        var accentColor = ResolveAccentColor();
        _defaultDropBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B));
        _hoverDropBrush = new SolidColorBrush(accentColor);

        DropHoverBorder.Stroke = _hoverDropBrush;
        ResetDropVisuals();
    }

    private static Windows.UI.Color ResolveAccentColor()
    {
        if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accent) && accent is Windows.UI.Color color)
        {
            return color;
        }

        return Windows.UI.Color.FromArgb(255, 0, 120, 212);
    }

    private async void ClassifyButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickImageAsync();
        if (file is null)
        {
            return;
        }

        await ClassifyFileAsync(file);
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        ResultErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var capturedFile = await CapturePhotoAsync();
            if (capturedFile is null)
            {
                return;
            }

            await ClassifyFileAsync(capturedFile);
        }
        catch (Exception ex)
        {
            ResultErrorText.Text = $"Camera capture failed. {ex.Message}";
            ResultErrorText.Visibility = Visibility.Visible;
        }
    }

    private async Task<StorageFile?> CapturePhotoAsync()
    {
        if (App.MainAppWindow is null)
        {
            return null;
        }

        var captureUi = new CameraCaptureUI();
        captureUi.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
        captureUi.PhotoSettings.AllowCropping = false;

        var windowHandle = WindowNative.GetWindowHandle(App.MainAppWindow);
        InitializeWithWindow.Initialize(captureUi, windowHandle);

        return await captureUi.CaptureFileAsync(CameraCaptureUIMode.Photo);
    }

    private async Task ClassifyFileAsync(StorageFile file)
    {
        ResultErrorText.Visibility = Visibility.Collapsed;
        LowConfidenceInfoBar.IsOpen = false;

        try
        {
            await ShowPreviewAsync(file);

            ClassifyButton.IsEnabled = false;
            CaptureButton.IsEnabled = false;
            InferenceProgressRing.IsActive = true;
            InferenceProgressRing.Visibility = Visibility.Visible;

            var result = await _classificationService.ClassifyAsync(file);
            UpdateResultUi(result);
            await App.History.AddFromFileAsync(result, file);
        }
        catch (Exception ex)
        {
            ResultErrorText.Text = $"We couldn't classify this image. {ex.Message}";
            ResultErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            InferenceProgressRing.IsActive = false;
            InferenceProgressRing.Visibility = Visibility.Collapsed;
            ClassifyButton.IsEnabled = true;
            CaptureButton.IsEnabled = true;
            ResetDropVisuals();
        }
    }

    private void UpdateResultUi(ClassificationResult result)
    {
        ResultCategoryText.Text = $"Category: {result.DisplayName}";
        ResultConfidenceText.Text = $"Confidence: {result.ConfidenceLevel} ({result.Confidence:P1})";
        ResultExplanationText.Text = $"Explanation: {result.Explanation}";
        ResultGuidanceText.Text = $"Disposal guidance: {result.DisposalGuidance}";
        LowConfidenceInfoBar.IsOpen = result.IsLowConfidence;
    }

    private async Task<StorageFile?> PickImageAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.ViewMode = PickerViewMode.Thumbnail;

        if (App.MainAppWindow is null)
        {
            return null;
        }

        var windowHandle = WindowNative.GetWindowHandle(App.MainAppWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);

        return await picker.PickSingleFileAsync();
    }

    private async Task ShowPreviewAsync(StorageFile file)
    {
        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        PreviewImage.Source = bitmap;
    }

    private static bool IsSupportedImage(StorageFile file)
    {
        return file.FileType.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || file.FileType.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase);
    }

    private void DropTargetBorder_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop image to classify";
            e.DragUIOverride.IsContentVisible = true;
            ActivateDropVisuals();
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
            ResetDropVisuals();
        }
    }

    private void DropTargetBorder_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropVisuals();
    }

    private async void DropTargetBorder_Drop(object sender, DragEventArgs e)
    {
        ResultErrorText.Visibility = Visibility.Collapsed;

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            ResultErrorText.Text = "Drop an image file (.jpg, .jpeg, .png).";
            ResultErrorText.Visibility = Visibility.Visible;
            ResetDropVisuals();
            return;
        }

        var droppedItems = await e.DataView.GetStorageItemsAsync();
        var file = droppedItems.OfType<StorageFile>().FirstOrDefault(IsSupportedImage);

        if (file is null)
        {
            ResultErrorText.Text = "No supported image was found in the dropped items.";
            ResultErrorText.Visibility = Visibility.Visible;
            ResetDropVisuals();
            return;
        }

        await ClassifyFileAsync(file);
    }

    private void ActivateDropVisuals()
    {
        DropHoverBorder.Visibility = Visibility.Visible;
        DropTargetBorder.MinHeight = 380;
        DropHintText.Text = "Release to classify image";
        DropHintText.Foreground = _hoverDropBrush;
    }

    private void ResetDropVisuals()
    {
        DropHoverBorder.Visibility = Visibility.Collapsed;
        DropTargetBorder.MinHeight = 320;
        DropHintText.Text = "Drag and drop an image here (.jpg, .jpeg, .png), or use Classify Item.";
        DropHintText.Foreground = _defaultDropBrush;
    }
}
