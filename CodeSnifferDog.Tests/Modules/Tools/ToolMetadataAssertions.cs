using Microsoft.Extensions.AI;
using System.Reflection;
using ComponentDescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace CodeSnifferDog.Tests.Modules.Tools;

internal static class ToolMetadataAssertions
{
    public static void AssertToolMetadata(
        IEnumerable<AITool> tools,
        IReadOnlyList<(string Name, string Description)> expected)
    {
        CollectionAssert.AreEqual(expected.Select(item => item.Name).ToArray(), tools.Select(tool => tool.Name).ToArray());
        CollectionAssert.AreEqual(
            expected.Select(item => item.Description).ToArray(),
            tools.Select(tool => tool.Description).ToArray());
    }

    public static void AssertAdapterDescription<TToolSet>(
        string methodName,
        string expectedDescription,
        IReadOnlyDictionary<string, string> expectedParameterDescriptions)
    {
        MethodInfo method = typeof(TToolSet).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        ComponentDescriptionAttribute? methodDescription = method.GetCustomAttribute<ComponentDescriptionAttribute>();
        Assert.IsNotNull(methodDescription);
        Assert.AreEqual(expectedDescription, methodDescription.Description);

        Dictionary<string, string> parameterDescriptions = method
            .GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ToDictionary(
                parameter => parameter.Name ?? string.Empty,
                parameter =>
                {
                    ComponentDescriptionAttribute? description = parameter.GetCustomAttribute<ComponentDescriptionAttribute>();
                    Assert.IsNotNull(description, $"Parameter '{parameter.Name}' is missing DescriptionAttribute.");
                    return description.Description;
                });

        CollectionAssert.AreEquivalent(
            expectedParameterDescriptions.OrderBy(pair => pair.Key).ToArray(),
            parameterDescriptions.OrderBy(pair => pair.Key).ToArray());
    }
}
