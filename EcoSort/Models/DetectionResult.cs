namespace EcoSort.Models;

/// <summary>
/// Represents a detected object with its classification result.
/// Bounding box coordinates are in normalized 0.0 to 1.0 range.
/// </summary>
public class DetectionResult
{
    /// <summary>Bounding box - top-left X (normalized 0.0-1.0)</summary>
    public float X1 { get; set; }

    /// <summary>Bounding box - top-left Y (normalized 0.0-1.0)</summary>
    public float Y1 { get; set; }

    /// <summary>Bounding box - bottom-right X (normalized 0.0-1.0)</summary>
    public float X2 { get; set; }

    /// <summary>Bounding box - bottom-right Y (normalized 0.0-1.0)</summary>
    public float Y2 { get; set; }

    /// <summary>Detector confidence score (0.0-1.0)</summary>
    public float DetectorConfidence { get; set; }

    /// <summary>Garbage classification result for this detection</summary>
    public ClassificationResult? Classification { get; set; }

    /// <summary>Width of bounding box (normalized)</summary>
    public float Width => X2 - X1;

    /// <summary>Height of bounding box (normalized)</summary>
    public float Height => Y2 - Y1;

    /// <summary>Center point of bounding box (normalized)</summary>
    public (float CenterX, float CenterY) Center => ((X1 + X2) / 2, (Y1 + Y2) / 2);
}
