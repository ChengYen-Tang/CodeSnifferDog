using CodeSnifferDog.Modules.Tools.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Agents.Common;

internal sealed class AgentToolComposer
{
    public IList<AITool> Compose(string repositoryRootPath, IEnumerable<AITool> domainTools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(domainTools);

        CommonToolSet commonToolSet = new(repositoryRootPath);
        return [.. commonToolSet.CreateTools(), .. domainTools];
    }
}
