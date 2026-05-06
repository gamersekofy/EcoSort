using EcoSort.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace EcoSort.Services;

public sealed class BenchmarkingService
{
    private readonly GarbageClassificationService _classificationService;
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };

    public BenchmarkingService(GarbageClassificationService classificationService)
    {
        _classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
    }

    public class BenchmarkResult
    {
        public int ImageCount { get; set; }
        public double AverageLatencyMs { get; set; }
        public double MinLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double TotalLatencyMs { get; set; }
        public List<double> IndividualLatencies { get; set; } = new();
        public List<string> ProcessedImages { get; set; } = new();
    }

    public async Task<BenchmarkResult> BenchmarkFolderAsync(StorageFolder folder)
    {
        if (folder == null)
        {
            throw new ArgumentNullException(nameof(folder));
        }

        var result = new BenchmarkResult();
        var imageFiles = await GetImageFilesAsync(folder);

        if (imageFiles.Count == 0)
        {
            throw new InvalidOperationException("No supported image files found in the selected folder.");
        }

        var stopwatch = Stopwatch.StartNew();

        foreach (var file in imageFiles)
        {
            try
            {
                var fileStopwatch = Stopwatch.StartNew();
                _ = await _classificationService.ClassifyAsync(file);
                fileStopwatch.Stop();

                var latencyMs = fileStopwatch.Elapsed.TotalMilliseconds;
                result.IndividualLatencies.Add(latencyMs);
                result.ProcessedImages.Add(file.Name);
            }
            catch (Exception ex)
            {
                // Log the error but continue processing
                System.Diagnostics.Debug.WriteLine($"Error processing {file.Name}: {ex.Message}");
            }
        }

        stopwatch.Stop();

        if (result.IndividualLatencies.Count > 0)
        {
            result.ImageCount = result.IndividualLatencies.Count;
            result.TotalLatencyMs = stopwatch.Elapsed.TotalMilliseconds;
            result.AverageLatencyMs = result.IndividualLatencies.Average();
            result.MinLatencyMs = result.IndividualLatencies.Min();
            result.MaxLatencyMs = result.IndividualLatencies.Max();
        }

        return result;
    }

    public async Task<BenchmarkResult> BenchmarkSingleImageAsync(StorageFile file)
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        var result = new BenchmarkResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _ = await _classificationService.ClassifyAsync(file);
            stopwatch.Stop();

            var latencyMs = stopwatch.Elapsed.TotalMilliseconds;
            result.IndividualLatencies.Add(latencyMs);
            result.ProcessedImages.Add(file.Name);
            result.ImageCount = 1;
            result.TotalLatencyMs = latencyMs;
            result.AverageLatencyMs = latencyMs;
            result.MinLatencyMs = latencyMs;
            result.MaxLatencyMs = latencyMs;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing {file.Name}: {ex.Message}");
            throw;
        }

        return result;
    }

    private static async Task<List<StorageFile>> GetImageFilesAsync(StorageFolder folder)
    {
        var imageFiles = new List<StorageFile>();

        try
        {
            var files = await folder.GetFilesAsync();

            foreach (var file in files)
            {
                if (SupportedExtensions.Contains(file.FileType.ToLowerInvariant()))
                {
                    imageFiles.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading folder: {ex.Message}");
        }

        return imageFiles;
    }
}
