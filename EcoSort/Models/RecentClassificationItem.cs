using System;
using System.Globalization;

namespace EcoSort.Models;

public sealed class RecentClassificationItem
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string ConfidenceLevel { get; init; }

    public required float Confidence { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string ImagePath { get; init; }

    public required string ImageUri { get; init; }

    public string ConfidenceText => Confidence.ToString("P1", CultureInfo.CurrentCulture);

    public string TimestampText => Timestamp.ToString("g", CultureInfo.CurrentCulture);
}
