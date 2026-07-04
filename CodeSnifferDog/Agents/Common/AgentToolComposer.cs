using CodeSnifferDog.Modules.Tools.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.Common;

/// <summary>
/// Combines repository-scoped common tools with agent-specific domain tools.
/// </summary>
/// <param name="loggerFactory">Optional logger factory forwarded to the common tool set.</param>
internal sealed class AgentToolComposer(ILoggerFactory? loggerFactory = null)
{
    private readonly ILoggerFactory? _loggerFactory = loggerFactory;

    /// <summary>
    /// Composes the final tool list for one repository-scoped agent.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path used to configure common tools.</param>
    /// <param name="domainTools">Domain-specific tools for the target agent.</param>
    /// <returns>The combined tool list.</returns>
    /// <exception cref="ArgumentException"><paramref name="repositoryRootPath" /> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="domainTools" /> is <see langword="null" />.</exception>
    public IList<AITool> Compose(string repositoryRootPath, IEnumerable<AITool> domainTools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(domainTools);

        CommonToolSet commonToolSet = new(repositoryRootPath, _loggerFactory);
        return [.. commonToolSet.CreateTools(), .. domainTools];
    }
}
