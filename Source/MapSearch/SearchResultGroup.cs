using System.Collections.Generic;

namespace VanillaUIPlus;

public sealed class SearchResultGroup<T>
    where T : class
{
    public readonly object Key;
    public readonly string Label;
    public readonly List<T> Items = new List<T>();
    private int headerCount = -1;
    private string header = string.Empty;

    public SearchResultGroup(object key, string label)
    {
        Key = key;
        Label = label;
    }

    // The label with its item count, rebuilt only when the count changes.
    public string Header
    {
        get
        {
            if (headerCount != Items.Count)
            {
                headerCount = Items.Count;
                header = Label + " (" + headerCount + ")";
            }

            return header;
        }
    }
}
