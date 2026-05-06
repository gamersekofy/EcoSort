using EcoSort.Models;
using EcoSort.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
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
using Windows.UI;
using WinRT.Interop;

namespace EcoSort.Pages;

public sealed partial class ClassifyPage : Page
{
    private readonly GarbageClassificationService _classificationService;
    private LiveInferenceService? _liveInferenceService;

    private readonly SolidColorBrush _defaultDropBrush;
    private readonly SolidColorBrush _hoverDropBrush;

    private MediaCapture? _mediaCapture;
    private MediaFrameSourceGroup? _mediaFrameSourceGroup;
    private bool _isCameraRunning;

    public ClassifyPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Enabled;
        _classificationService = new GarbageClassificationService();

        // Initialize live inference service (only used if experimental feature is enabled)
        _liveInferenceService = new LiveInferenceService(targetFps: 5);
        _liveInferenceService.OnDetectionsUpdated += LiveInferenceService_OnDetectionsUpdated;
        _liveInferenceService.OnInferenceError += LiveInferenceService_OnInferenceError;

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
        try
        {
            await StartCameraPreviewAsync();
            
            // Start live inference ONLY if experimental feature is enabled
            if (App.ExperimentalLiveInferenceEnabled && _liveInferenceService != null && _mediaCapture != null)
            {
                try
                {
                    // Initialize the service with the media capture instance
                    await _liveInferenceService.InitializeAsync(_mediaCapture);
                    
                    var preferredSourceInfo = _mediaFrameSourceGroup?.SourceInfos
                        .FirstOrDefault(x => x.SourceKind == MediaFrameSourceKind.Color && x.MediaStreamType == MediaStreamType.VideoPreview)
                        ?? _mediaFrameSourceGroup?.SourceInfos.FirstOrDefault(x => x.SourceKind == MediaFrameSourceKind.Color);

                    if (preferredSourceInfo != null)
                    {
                        var frameSource = _mediaCapture.FrameSources[preferredSourceInfo.Id];
                        await _liveInferenceService.StartAsync(frameSource);
                        DetectionOverlayCanvas.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClassifyPage] Failed to start live inference: {ex.Message}");
                    // Continue without live inference if it fails
                    DetectionOverlayCanvas.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                DetectionOverlayCanvas.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            ResultErrorText.Text = $"Failed to start camera: {ex.Message}";
            ResultErrorText.Visibility = Visibility.Visible;
            StartCameraButton.IsEnabled = true;
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
        try
        {
            // Stop live inference first if it's running
            if (_liveInferenceService != null)
            {
                await _liveInferenceService.StopAsync();
            }

            ClearDetectionOverlay();
            DetectionOverlayCanvas.Visibility = Visibility.Collapsed;
            
            await StopCameraPreviewAsync();
        }
        catch (Exception ex)
        {
            ResultErrorText.Text = $"Error stopping camera: {ex.Message}";
            ResultErrorText.Visibility = Visibility.Visible;
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

    private void ResetDropVisuals()
    {
        DropHoverBorder.Visibility = Visibility.Collapsed;
        DropHintText.Opacity = 1.0;
    }

    private void DropTargetBorder_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        DropHoverBorder.Visibility = Visibility.Visible;
    }

    private void DropTargetBorder_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropVisuals();
    }

    private async void DropTargetBorder_Drop(object sender, DragEventArgs e)
    {
        ResetDropVisuals();
        await HandleDropAsync(e.DataView);
    }

    private async Task HandleDropAsync(DataPackageView dataPackageView)
    {
        try
        {
            if (dataPackageView.Contains(StandardDataFormats.StorageItems))
            {
                var storageItems = await dataPackageView.GetStorageItemsAsync();
                foreach (var item in storageItems.OfType<StorageFile>())
                {
                    if (item.ContentType.StartsWith("image/"))
                    {
                        await ClassifyFileAsync(item, addToHistory: true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ResultErrorText.Text = $"Error handling drop: {ex.Message}";
            ResultErrorText.Visibility = Visibility.Visible;
        }
    }

    private void LiveInferenceService_OnDetectionsUpdated(List<DetectionResult> detections)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RenderDetections(detections);
        });
    }

    private void LiveInferenceService_OnInferenceError(string errorMessage)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            System.Diagnostics.Debug.WriteLine($"[ClassifyPage] Live inference error: {errorMessage}");
        });
    }

    private void RenderDetections(List<DetectionResult> detections)
    {
        DetectionOverlayCanvas.Children.Clear();

        if (DetectionOverlayCanvas.Visibility != Visibility.Visible || detections == null || detections.Count == 0)
            return;

        // Get canvas dimensions (normalized coordinates are 0.0 to 1.0)
        var canvasWidth = DetectionOverlayCanvas.ActualWidth;
        var canvasHeight = DetectionOverlayCanvas.ActualHeight;

        foreach (var detection in detections)
        {
            // Convert normalized coordinates to canvas pixels
            double x1 = detection.X1 * canvasWidth;
            double y1 = detection.Y1 * canvasHeight;
            double x2 = detection.X2 * canvasWidth;
            double y2 = detection.Y2 * canvasHeight;
            double width = x2 - x1;
            double height = y2 - y1;

            // Draw bounding box
            var rect = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)), // Green
                StrokeThickness = 2
            };
            Canvas.SetLeft(rect, x1);
            Canvas.SetTop(rect, y1);
            DetectionOverlayCanvas.Children.Add(rect);

            // Draw label if classification exists
            if (detection.Classification != null)
            {
                var label = new TextBlock
                {
                    Text = $"{detection.Classification.DisplayName} ({detection.DetectorConfidence:F1}%)",
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),
                    FontSize = 12
                };
                Canvas.SetLeft(label, x1);
                Canvas.SetTop(label, Math.Max(0, y1 - 20));
                DetectionOverlayCanvas.Children.Add(label);
            }
        }
    }

    private void ClearDetectionOverlay()
    {
        DetectionOverlayCanvas.Children.Clear();
    }

    private async void ClassifyPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_isCameraRunning)
        {
            await StopCameraPreviewAsync();
        }

        if (_liveInferenceService != null)
        {
            await _liveInferenceService.StopAsync();
        }
    }
}
