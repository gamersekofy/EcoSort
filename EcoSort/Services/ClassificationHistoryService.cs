using EcoSort.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace EcoSort.Services;

public sealed class ClassificationHistoryService
{
    private const int MaxItems = 20;

    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly string _baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EcoSort");
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private bool _initialized;

    public ObservableCollection<RecentClassificationItem> Items { get; } = new();

    private string HistoryFilePath => Path.Combine(_baseFolder, "history.json");

    private string ImagesFolderPath => Path.Combine(_baseFolder, "history-images");

    public async Task InitializeAsync()
    {
        await _sync.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(_baseFolder);
            Directory.CreateDirectory(ImagesFolderPath);

            if (File.Exists(HistoryFilePath))
            {
                var json = await File.ReadAllTextAsync(HistoryFilePath);
                var metadata = JsonSerializer.Deserialize<List<RecentClassificationMetadata>>(json) ?? [];

                foreach (var entry in metadata)
                {
                    if (!File.Exists(entry.ImagePath))
                    {
                        continue;
                    }

                    Items.Add(new RecentClassificationItem
                    {
                        Id = entry.Id,
                        DisplayName = entry.DisplayName,
                        ConfidenceLevel = entry.ConfidenceLevel,
                        Confidence = entry.Confidence,
                        Timestamp = entry.Timestamp,
                        ImagePath = entry.ImagePath,
                        ImageUri = ToFileUri(entry.ImagePath)
                    });
                }
            }

            _initialized = true;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task AddFromFileAsync(ClassificationResult result, StorageFile sourceFile)
    {
        await EnsureInitializedAsync();

        var id = Guid.NewGuid().ToString("N");
        var extension = Path.GetExtension(sourceFile.Name);
        var imageName = $"{id}{extension}";

        var imagesFolder = await GetImagesFolderAsync();
        var copiedFile = await sourceFile.CopyAsync(imagesFolder, imageName, NameCollisionOption.ReplaceExisting);

        var newItem = new RecentClassificationItem
        {
            Id = id,
            DisplayName = result.DisplayName,
            ConfidenceLevel = result.ConfidenceLevel,
            Confidence = result.Confidence,
            Timestamp = DateTimeOffset.Now,
            ImagePath = copiedFile.Path,
            ImageUri = ToFileUri(copiedFile.Path)
        };

        await _sync.WaitAsync();
        try
        {
            Items.Insert(0, newItem);

            while (Items.Count > MaxItems)
            {
                var removed = Items[^1];
                Items.RemoveAt(Items.Count - 1);
                DeleteImageIfExists(removed.ImagePath);
            }

            await SaveMetadataAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task RemoveAsync(RecentClassificationItem item)
    {
        await EnsureInitializedAsync();

        await _sync.WaitAsync();
        try
        {
            var toRemove = Items.FirstOrDefault(x => x.Id == item.Id);
            if (toRemove is null)
            {
                return;
            }

            Items.Remove(toRemove);
            DeleteImageIfExists(toRemove.ImagePath);
            await SaveMetadataAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task ClearAsync()
    {
        await EnsureInitializedAsync();

        await _sync.WaitAsync();
        try
        {
            foreach (var item in Items)
            {
                DeleteImageIfExists(item.ImagePath);
            }

            Items.Clear();
            await SaveMetadataAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    private static string ToFileUri(string path)
    {
        return new Uri(path, UriKind.Absolute).AbsoluteUri;
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private async Task<StorageFolder> GetImagesFolderAsync()
    {
        var localFolder = ApplicationData.Current.LocalFolder;
        var appFolder = await localFolder.CreateFolderAsync("EcoSort", CreationCollisionOption.OpenIfExists);
        return await appFolder.CreateFolderAsync("history-images", CreationCollisionOption.OpenIfExists);
    }

    private void DeleteImageIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task SaveMetadataAsync()
    {
        Directory.CreateDirectory(_baseFolder);

        var metadata = Items.Select(x => new RecentClassificationMetadata
        {
            Id = x.Id,
            DisplayName = x.DisplayName,
            ConfidenceLevel = x.ConfidenceLevel,
            Confidence = x.Confidence,
            Timestamp = x.Timestamp,
            ImagePath = x.ImagePath
        }).ToList();

        var json = JsonSerializer.Serialize(metadata, _jsonOptions);
        await File.WriteAllTextAsync(HistoryFilePath, json);
    }

    private sealed class RecentClassificationMetadata
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public required string ConfidenceLevel { get; init; }

        public required float Confidence { get; init; }

        public required DateTimeOffset Timestamp { get; init; }

        public required string ImagePath { get; init; }
    }
}
