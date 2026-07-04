namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Persists a generated rule report for a project.
/// </summary>
public sealed class ProjectRuleReportRecord
{
    /// <summary>
    /// Gets or sets the rule report identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning project identifier.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the stable rule key.
    /// </summary>
    public required string RuleKey { get; set; }

    /// <summary>
    /// Gets or sets the hashed rule key used for uniqueness constraints.
    /// </summary>
    public required string RuleKeyHash { get; set; }

    /// <summary>
    /// Gets or sets the human-readable rule name.
    /// </summary>
    public required string RuleName { get; set; }

    /// <summary>
    /// Gets or sets the markdown report content.
    /// </summary>
    public required string MarkdownContent { get; set; }

    /// <summary>
    /// Gets or sets when the rule report record was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning project navigation property.
    /// </summary>
    public ProjectRecord? Project { get; set; }
}
