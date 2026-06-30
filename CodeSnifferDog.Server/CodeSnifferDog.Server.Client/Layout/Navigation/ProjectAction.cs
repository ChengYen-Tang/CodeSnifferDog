namespace CodeSnifferDog.Server.Client.Layout.Navigation;

internal sealed record ProjectAction(string IconText, string Label, ProjectActionKind Kind, string? Href)
{
    public static ProjectAction Link(string iconText, string label, string href) =>
        new(iconText, label, ProjectActionKind.Link, href);

    public static ProjectAction Delete() =>
        new("D", "Delete", ProjectActionKind.Delete, null);

    public static ProjectAction Cancel() =>
        new("X", "Cancel", ProjectActionKind.Cancel, null);
}
