using EcoSort.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace EcoSort.Services;

public sealed class GarbageClassificationService : IDisposable
{
    private const int ImageSize = 224;
    private const string InputName = "images";
    private const string OutputName = "logits";

    private static readonly string[] ClassNames =
    {
        "battery",
        "biological",
        "brown-glass",
        "cardboard",
        "clothes",
        "green-glass",
        "metal",
        "paper",
        "plastic",
        "shoes",
        "trash",
        "white-glass"
    };

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["battery"] = "Battery",
        ["biological"] = "Biological",
        ["brown-glass"] = "Brown Glass",
        ["cardboard"] = "Cardboard",
        ["clothes"] = "Clothes",
        ["green-glass"] = "Green Glass",
        ["metal"] = "Metal",
        ["paper"] = "Paper",
        ["plastic"] = "Plastic",
        ["shoes"] = "Shoes",
        ["trash"] = "Trash",
        ["white-glass"] = "White Glass"
    };

    private static readonly Dictionary<string, string> Explanations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["battery"] = "The item shows features commonly seen in batteries or battery-powered waste.",
        ["biological"] = "The texture and organic appearance resemble food or compostable waste.",
        ["brown-glass"] = "The object appears to have the color and surface traits of brown glass containers.",
        ["cardboard"] = "The image looks like paperboard packaging with typical cardboard edges and fibers.",
        ["clothes"] = "Fabric-like textures and shape suggest this is clothing or textile material.",
        ["green-glass"] = "The model detected glass-like reflections and a green-tinted appearance.",
        ["metal"] = "The item looks rigid and reflective, which is common for metal waste.",
        ["paper"] = "The material appears thin and fibrous, similar to paper products.",
        ["plastic"] = "The shape and surface characteristics look like plastic packaging or containers.",
        ["shoes"] = "The object appears consistent with footwear shape and material patterns.",
        ["trash"] = "The item appears mixed or non-distinct, matching general trash characteristics.",
        ["white-glass"] = "The model identified clear or white glass visual patterns in the image."
    };

    private static readonly Dictionary<string, string> DisposalGuidanceByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["battery"] = "Take batteries to a hazardous waste or battery drop-off location. Do not place in regular recycling.",
        ["biological"] = "If your area supports composting, place it in compost. Otherwise follow local organic waste rules.",
        ["brown-glass"] = "Rinse and place in glass recycling if accepted locally.",
        ["cardboard"] = "Flatten and keep dry, then place in paper/cardboard recycling.",
        ["clothes"] = "Consider donation or textile recycling before disposal.",
        ["green-glass"] = "Rinse and place in glass recycling if accepted locally.",
        ["metal"] = "Clean and place in metal recycling where available.",
        ["paper"] = "Keep paper clean and dry, then place in paper recycling.",
        ["plastic"] = "Check local plastic acceptance rules and rinse containers before recycling.",
        ["shoes"] = "Prioritize donation or textile/footwear collection programs when available.",
        ["trash"] = "Use general waste disposal and check local guidance for any recoverable parts.",
        ["white-glass"] = "Rinse and place in glass recycling if accepted locally."
    };

    private readonly InferenceSession _session;

    public GarbageClassificationService()
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "memberB_resnet50_aug_on.onnx");

        try
        {
            var directMlOptions = new SessionOptions();
            directMlOptions.AppendExecutionProvider_DML();
            _session = new InferenceSession(modelPath, directMlOptions);
        }
        catch
        {
            _session = new InferenceSession(modelPath);
        }
    }

    public async Task<ClassificationResult> ClassifyAsync(StorageFile file)
    {
        var inputData = await BuildInputTensorAsync(file);

        var tensor = new DenseTensor<float>(inputData, new[] { 1, 3, ImageSize, ImageSize });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(InputName, tensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

        var logits = results.First(x => x.Name == OutputName).AsEnumerable<float>().ToArray();
        var probabilities = Softmax(logits);
        var topIndex = ArgMax(probabilities);

        var className = ClassNames[topIndex];
        var confidence = probabilities[topIndex];

        return new ClassificationResult
        {
            Category = className,
            DisplayName = DisplayNames[className],
            Confidence = confidence,
            ConfidenceLevel = ResolveConfidenceLevel(confidence),
            Explanation = Explanations[className],
            DisposalGuidance = DisposalGuidanceByCategory[className]
        };
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    private static async Task<float[]> BuildInputTensorAsync(StorageFile file)
    {
        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);

        var transform = new BitmapTransform
        {
            ScaledWidth = ImageSize,
            ScaledHeight = ImageSize,
            InterpolationMode = BitmapInterpolationMode.Linear
        };

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        var pixels = pixelData.DetachPixelData();
        var tensorData = new float[3 * ImageSize * ImageSize];

        var mean = new[] { 0.485f, 0.456f, 0.406f };
        var std = new[] { 0.229f, 0.224f, 0.225f };

        for (var y = 0; y < ImageSize; y++)
        {
            for (var x = 0; x < ImageSize; x++)
            {
                var pixelIndex = (y * ImageSize + x) * 4;
                var r = pixels[pixelIndex + 2] / 255f;
                var g = pixels[pixelIndex + 1] / 255f;
                var b = pixels[pixelIndex] / 255f;

                var hwIndex = y * ImageSize + x;
                tensorData[hwIndex] = (r - mean[0]) / std[0];
                tensorData[(ImageSize * ImageSize) + hwIndex] = (g - mean[1]) / std[1];
                tensorData[(2 * ImageSize * ImageSize) + hwIndex] = (b - mean[2]) / std[2];
            }
        }

        return tensorData;
    }

    private static float[] Softmax(IReadOnlyList<float> logits)
    {
        var max = logits.Max();
        var exps = new float[logits.Count];
        var sum = 0f;

        for (var i = 0; i < logits.Count; i++)
        {
            exps[i] = MathF.Exp(logits[i] - max);
            sum += exps[i];
        }

        for (var i = 0; i < exps.Length; i++)
        {
            exps[i] /= sum;
        }

        return exps;
    }

    private static int ArgMax(IReadOnlyList<float> values)
    {
        var bestIndex = 0;
        var bestValue = values[0];

        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static string ResolveConfidenceLevel(float confidence)
    {
        if (confidence >= 0.90f)
        {
            return "High";
        }

        if (confidence >= 0.70f)
        {
            return "Medium";
        }

        return "Low";
    }
}
