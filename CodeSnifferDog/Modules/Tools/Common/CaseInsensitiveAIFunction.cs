using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Wraps an AI function so its named arguments are resolved without regard to case.
/// </summary>
internal sealed class CaseInsensitiveAIFunction(AIFunction innerFunction)
    : DelegatingAIFunction(innerFunction)
{
    /// <summary>
    /// Wraps an AI tool when it is invocable, preserving declaration-only tools unchanged.
    /// </summary>
    /// <param name="tool">The tool to wrap.</param>
    /// <returns>The case-insensitive tool.</returns>
    public static AITool Wrap(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return tool is AIFunction function ? Wrap(function) : tool;
    }

    /// <summary>
    /// Wraps an AI function unless it has already been normalized.
    /// </summary>
    /// <param name="function">The function to wrap.</param>
    /// <returns>The case-insensitive function.</returns>
    public static AIFunction Wrap(AIFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return function is CaseInsensitiveAIFunction
            ? function
            : new CaseInsensitiveAIFunction(function);
    }

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        AIFunctionArguments caseInsensitiveArguments = new(arguments, StringComparer.OrdinalIgnoreCase)
        {
            Services = arguments.Services,
            Context = arguments.Context,
        };

        return InnerFunction.InvokeAsync(caseInsensitiveArguments, cancellationToken);
    }
}
