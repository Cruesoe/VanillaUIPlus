using System.Collections.Generic;

namespace VanillaUIPlus;

public sealed class SearchResultGroup<T>
    where T : class
{
    public readonly object Key;
    public readonly string Label;
    public readonly List<T> Items = new List<T>();

    public SearchResultGroup(object key, string label)
    {
        Key = key;
        Label = label;
    }
}
