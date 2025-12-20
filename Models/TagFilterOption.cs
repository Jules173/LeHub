namespace LeHub.Models;

/// <summary>
/// Represents an option in the tag filter dropdown.
/// Wraps either the "All" option (Tag = null) or a specific tag.
/// </summary>
public class TagFilterOption
{
    public string Label { get; }
    public Tag? Tag { get; }

    public TagFilterOption(string label, Tag? tag = null)
    {
        Label = label;
        Tag = tag;
    }

    /// <summary>
    /// Creates the "All" option.
    /// </summary>
    public static TagFilterOption All => new("Tous", null);

    /// <summary>
    /// Creates a tag option.
    /// </summary>
    public static TagFilterOption FromTag(Tag tag) => new(tag.Name, tag);

    public override string ToString() => Label;
}
