using CodeSnifferDog.Modules.Tools.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.Common;

internal sealed class AgentToolComposer(ILoggerFactory? loggerFactory = null)
{
    private readonly ILoggerFactory? _loggerFactory = loggerFactory;

    public IList<AITool> Compose(string repositoryRootPath, IEnumerable<AITool> domainTools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(domainTools);

        CommonToolSet commonToolSet = new(repositoryRootPath, _loggerFactory);
        return [.. commonToolSet.CreateTools(), .. domainTools];
    }
}
