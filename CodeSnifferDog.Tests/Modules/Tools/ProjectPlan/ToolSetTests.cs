using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.Tools.ProjectPlan;

[TestClass]
public sealed class ToolSetTests
{
    [TestMethod]
    public void CreateProjectPlanAgentTools_ReturnsExpectedToolNames()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        IList<AITool> tools = toolSet.CreateProjectPlanAgentTools();

        CollectionAssert.AreEqual(
            new[] { "AddProjectPlanTaskItem", "AddProjectPlanTaskItems", "DeleteProjectPlanTaskItem", "ListProjectPlanTaskItems" },
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void CreateVerifierTools_ReturnsExpectedToolNames()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        IList<AITool> tools = toolSet.CreateVerifierTools();

        CollectionAssert.AreEqual(
            new[] { "ListProjectPlanTaskItems", "SubmitReviewVerdict" },
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task PublicMethods_DelegateToServices()
    {
        InMemoryTaskItemStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ToolSet toolSet = new(store, verdictBuffer);

        AddProjectPlanTaskItemResult result = await toolSet.AddProjectPlanTaskItemAsync(CreateArgs(" Program.cs "), CancellationToken.None);
        IReadOnlyList<StoredTaskItem> taskItems = await toolSet.ListProjectPlanTaskItemsAsync(CancellationToken.None);
        bool deleted = await toolSet.DeleteProjectPlanTaskItemAsync(
            new DeleteProjectPlanTaskItemArgs
            {
                ProjectPlanTaskItemId = result.ProjectPlanTaskItemId,
            },
            CancellationToken.None);
        bool verdictSubmitted = await toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = true,
                Message = " approved ",
            },
            CancellationToken.None);

        Assert.HasCount(1, taskItems);
        Assert.AreEqual("Program.cs", taskItems[0].Files[0].FilePath);
        Assert.IsTrue(deleted);
        Assert.IsTrue(verdictSubmitted);
        Assert.AreEqual("approved", verdictBuffer.Latest!.Message);
    }

    [TestMethod]
    public async Task AddProjectPlanTaskItemsAsync_ReturnsStoredIds()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        AddProjectPlanTaskItemsResult result = await toolSet.AddProjectPlanTaskItemsAsync(
            new AddProjectPlanTaskItemsArgs
            {
                TaskItems = [CreateArgs("Program.cs"), CreateArgs("Cache.cs")],
            },
            CancellationToken.None);

        Assert.HasCount(2, result.ProjectPlanTaskItemIds);
    }

    private static AddProjectPlanTaskItemArgs CreateArgs(string filePath) =>
        new()
        {
            Files =
            [
                new PlanFile
                {
                    FilePath = filePath,
                    TotalLines = 10,
                },
            ],
        };
}
