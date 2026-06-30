namespace CodeSnifferDog.Server.Client.Layout.Navigation;

internal sealed record ProjectItem(string ProjectId, string Name, string Meta, IReadOnlyList<ProjectAction> Actions);
