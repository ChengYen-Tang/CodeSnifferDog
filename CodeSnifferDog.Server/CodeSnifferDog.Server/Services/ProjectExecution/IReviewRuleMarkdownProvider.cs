namespace CodeSnifferDog.Server.Services.ProjectExecution;

public interface IReviewRuleMarkdownProvider
{
    bool HasRules { get; }

    Task<IReadOnlyList<string>> LoadRuleMarkdownsAsync(CancellationToken cancellationToken = default);
}
