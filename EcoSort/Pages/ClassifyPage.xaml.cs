using EcoSort.Models;
using EcoSort.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
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

    private MediaCapture? _mediaCapture;
    private MediaFrameSourceGroup? _mediaFrameSourceGroup;
    private DispatcherTimer? _liveInferenceTimer;
    private bool _isCameraRunning;
    private bool _isLiveInferenceTickRunning;

    public ClassifyPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Enabled;
        _classificationService = new GarbageClassificationService();

        var accentColor = ResolveAccentColor();
        _defaultDropBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B));
        _hoverDropBrush = new SolidColorBrush(accentColor);

        DropHoverBorder.Stroke = _hoverDropBrush;
        ResetDropVisuals();
        UpdateExperimentalLiveInferenceBanner();

        Unloaded += ClassifyPage_Unloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        UpdateExperimentalLiveInferenceBanner();
        RefreshLiveInferenceCooldown();
        RefreshCameraButtonStates();
    }

    private static Windows.UI.Color ResolveAccentColor()
    {
        if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accent) && accent is Windows.UI.Color color)
        {
            return color;
        }

        return Windows.UI.Color.FromArgb(255, 0, 120, 212);
    }

    private void UpdateExperimentalLiveInferenceBanner()
    {
        ExperimentalLiveInferenceInfoBar.IsOpen = App.ExperimentalLiveInferenceEnabled;
    }

    private async void ClassifyButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickImageAsync();
        if (file is null)
        {
            return;
        }

        await ClassifyFileAsync(file, addToHistory: true);
    }

    private async void StartCameraButton_Click(object sender, RoutedEventArgs e)
    {
        ResultErrorText.Visibility = Visibility.Collapsed;

        try
        {
            await StartCameraPreviewAsync();
        }
        catch (Exception ex)
        {
            ResultErrorText.Text = $"Unable to start camera preview. {ex.Message}";
            ResultErrorText.Visibility = Visibility.Visible;
            await StopCameraPreviewAsync();
        }
    }

    private async void SnapPhotoButton_Click(object sender, RoutedEventArgs e)
    {
        ResultErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var file = await CapturePhotoFromPreviewAsync();
            if (file is null)
            {
                return;
            }

            await ClassifyFileAsync(file, addToHistory: true);
        }
        catch (Exception ex)
        {
            ResultErrorText.Text = $"Camera capture failed. {ex.Message}";
            ResultErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void StopCameraButton_Click(object sender, RoutedEventArgs e)
    {
        await StopCameraPreviewAsync();
    }

    private void RefreshLiveInferenceCooldown()
    {
        EnsureLiveInferenceTimer();
        if (_liveInferenceTimer is not null)
        {
            _liveInferenceTimer.Interval = TimeSpan.FromSeconds(App.ExperimentalLiveInferenceCooldownSeconds);
        }
    }

    private void RefreshCameraButtonStates()
    {
        StartCameraButton.IsEnabled = !_isCameraRunning;
        SnapPhotoButton.IsEnabled = _isCameraRunning;
        StopCameraButton.IsEnabled = _isCameraRunning;
    }

    private async Task StartCameraPreviewAsync()
    {
        if (_isCameraRunning)
        {
            return;
        }

        var groups = await MediaFrameSourceGroup.FindAllAsync();
        if (groups.Count == 0)
        {
            throw new InvalidOperationException("No camera devices found.");
        }

        _mediaFrameSourceGroup = groups.First();
        _mediaCapture = new MediaCapture();

        var initializationSettings = new MediaCaptureInitializationSettings
        {
            SourceGroup = _mediaFrameSourceGroup,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            PhotoCaptureSource = PhotoCaptureSource.Auto
        };

        await _mediaCapture.InitializeAsync(initializationSettings);

        var preferredSourceInfo = _mediaFrameSourceGroup.SourceInfos
            .FirstOrDefault(x => x.SourceKind == MediaFrameSourceKind.Color && x.MediaStreamType == MediaStreamType.VideoPreview)
            ?? _mediaFrameSourceGroup.SourceInfos.FirstOrDefault(x => x.SourceKind == MediaFrameSourceKind.Color)
            ?? _mediaFrameSourceGroup.SourceInfos.First();

        var frameSource = _mediaCapture.FrameSources[preferredSourceInfo.Id];
        CameraPreviewElement.Source = MediaSource.CreateFromMediaFrameSource(frameSource);

        CameraPreviewElement.Visibility = Visibility.Visible;
        SnapPhotoButton.Visibility = Visibility.Visible;
        StopCameraButton.Visibility = Visibility.Visible;

        _isCameraRunning = true;
        RefreshCameraButtonStates();
        RefreshLiveInferenceCooldown();

        if (App.ExperimentalLiveInferenceEnabled)
        {
            _liveInferenceTimer?.Start();
        }
    }

    private async Task<StorageFile?> CapturePhotoFromPreviewAsync()
    {
        if (_mediaCapture is null)
        {
            return null;
        }

        var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync($"capture_{Guid.NewGuid():N}.jpg", CreationCollisionOption.GenerateUniqueName);
        await _mediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);
        return file;
    }

    private async Task StopCameraPreviewAsync()
    {
        _liveInferenceTimer?.Stop();

        if (_mediaCapture is not null)
        {
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }

        _mediaFrameSourceGroup = null;
        CameraPreviewElement.Source = null;
        CameraPreviewElement.Visibility = Visibility.Collapsed;

        SnapPhotoButton.Visibility = Visibility.Collapsed;
        StopCameraButton.Visibility = Visibility.Collapsed;

        _isCameraRunning = false;
        RefreshCameraButtonStates();

        await Task.CompletedTask;
    }

    private void EnsureLiveInferenceTimer()
    {
        if (_liveInferenceTimer is not null)
        {
            return;
        }

        _liveInferenceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(App.ExperimentalLiveInferenceCooldownSeconds)
        };
        _liveInferenceTimer.Tick += LiveInferenceTimer_Tick;
    }

    private async void LiveInferenceTimer_Tick(object? sender, object e)
    {
        if (!_isCameraRunning || !App.ExperimentalLiveInferenceEnabled || _isLiveInferenceTickRunning)
        {
            return;
        }

        _isLiveInferenceTickRunning = true;
        try
        {
            var file = await CapturePhotoFromPreviewAsync();
            if (file is null)
            {
                return;
            }

            await ClassifyFileAsync(file, addToHistory: true);
        }
        catch
        {
            _liveInferenceTimer?.Stop();
        }
        finally
        {
            _isLiveInferenceTickRunning = false;
        }
    }

    private async Task ClassifyFileAsync(StorageFile file, bool addToHistory)
    {
        ResultErrorText.Visibility = Visibility.Collapsed;
        LowConfidenceInfoBar.IsOpen = false;

        try
        {
            await ShowPreviewAsync(file);

            ClassifyButton.IsEnabled = false;
            StartCameraButton.IsEnabled = false;
            SnapPhotoButton.IsEnabled = false;
            StopCameraButton.IsEnabled = false;
            InferenceProgressRing.IsActive = true;
            InferenceProgressRing.Visibility = Visibility.Visible;

            var result = await _classificationService.ClassifyAsync(file);
            UpdateResultUi(result);

            if (addToHistory)
            {
                await App.History.AddFromFileAsync(result, file);
            }
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
            RefreshCameraButtonStates();
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

        await ClassifyFileAsync(file, addToHistory: true);
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

    private async void ClassifyPage_Unloaded(object sender, RoutedEventArgs e)
    {
        await StopCameraPreviewAsync();
    }
}
