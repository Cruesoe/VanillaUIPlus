using System;

namespace VanillaUIPlus;

/// <summary>
/// Splits inspect text lines of the form "Label: value" so the list view can show them as rows.
/// </summary>
public static class InspectRows
{
    // Longest label a line may have to become a row; longer ones are sentences.
    private const int MaxLabelLength = 40;

    // A line with a plain label before ": " and a value after it; a label with markup or a sentence before the colon stays text.
    public static bool TrySplit(string line, out string label, out string value)
    {
        int split = line.IndexOf(": ", StringComparison.Ordinal);
        label = split > 0 ? line.Substring(0, split).Trim() : string.Empty;
        value = split > 0 ? line.Substring(split + 2).Trim() : string.Empty;
        return label.Length > 0 && value.Length > 0 && label.Length <= MaxLabelLength && label.IndexOf('<') < 0;
    }
}
