using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core.Estimation;

[TestClass]
public sealed class TokenEstimatorTests
{
    [TestMethod]
    public void StructuredToolResult_ToString_ContainsOnlyTheCollectionTypeName()
    {
        List<StoredTaskItem> taskItems = CreateTaskItems();

        string rendered = taskItems.ToString()!;
        int typeNameTokens = EstimateTextTokens(rendered);
        int serializedPayloadTokens = EstimateJsonTokens(taskItems);

        StringAssert.Contains(rendered, "List`1");
        Assert.IsFalse(rendered.Contains("src/project-plan/task-0", StringComparison.Ordinal));
        Assert.IsTrue(
            serializedPayloadTokens > typeNameTokens * 100,
            $"The structured payload ({serializedPayloadTokens} tokens) should be materially larger than its type name ({typeNameTokens} tokens).");
    }

    [TestMethod]
    public void EstimateContent_FunctionResultWithStructuredList_IncludesTheSerializedPayload()
    {
        List<StoredTaskItem> taskItems = CreateTaskItems();
        FunctionResultContent result = new("call-1", taskItems);

        int estimatedTokens = TokenEstimator.EstimateContent(result);
        int serializedPayloadTokens = EstimateJsonTokens(taskItems);

        Assert.IsTrue(
            estimatedTokens >= serializedPayloadTokens,
            $"The estimate ({estimatedTokens}) must include the serialized tool result ({serializedPayloadTokens}).");
    }

    [TestMethod]
    public void EstimateContent_FunctionCallWithStructuredArguments_IncludesTheSerializedPayload()
    {
        List<StoredTaskItem> taskItems = CreateTaskItems();
        FunctionCallContent call = new(
            "call-1",
            "AddProjectPlanTaskItems",
            new Dictionary<string, object?>
            {
                ["taskItems"] = taskItems,
            });

        int estimatedTokens = TokenEstimator.EstimateContent(call);
        int serializedPayloadTokens = EstimateJsonTokens(taskItems);

        Assert.IsTrue(
            estimatedTokens >= serializedPayloadTokens,
            $"The estimate ({estimatedTokens}) must include the serialized function arguments ({serializedPayloadTokens}).");
    }

    private static List<StoredTaskItem> CreateTaskItems() =>
    [
        .. Enumerable.Range(0, 8).Select(taskIndex => new StoredTaskItem
        {
            ProjectPlanTaskItemId = $"task-{taskIndex}",
            Files =
            [
                .. Enumerable.Range(0, 4).Select(fileIndex => new PlanFile
                {
                    FilePath = $"src/project-plan/task-{taskIndex}/file-{fileIndex}-{new string('x', 300)}.cs",
                    TotalLines = 100 + fileIndex,
                }),
            ],
        }),
    ];

    private static int EstimateJsonTokens(object value) =>
        Math.Max(1, Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))) / 4);

    private static int EstimateTextTokens(string value) =>
        Math.Max(1, Encoding.UTF8.GetByteCount(value) / 4);
}
