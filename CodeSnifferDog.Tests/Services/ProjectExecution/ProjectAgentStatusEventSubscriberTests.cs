using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectExecution.Status;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectAgentStatusEventSubscriberTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ToolCallEvents_MergeIntoSingleToolTimelineEntry()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishToolCallStartedAsync("call-1", "CreateRuleReviewIssue", "{ \"Severity\": \"High\" }", TestContext.CancellationToken);
        await agentScope.PublishToolCallCompletedAsync("call-1", "Created issue RRI-1", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, entries);
        Assert.AreEqual("System prompt", agent.SystemPrompt);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Tool, entries[0].EntryType);
        Assert.AreEqual("call-1", entries[0].ToolCallId);
        Assert.AreEqual("CreateRuleReviewIssue", entries[0].ToolName);
        Assert.AreEqual("{ \"Severity\": \"High\" }", entries[0].ToolArguments);
        Assert.AreEqual("Created issue RRI-1", entries[0].ToolResult);
        Assert.IsNull(entries[0].Message);
        Assert.AreEqual(1L, entries[0].Sequence);
    }

    [TestMethod]
    public async Task ToolCallCompletedEvent_WithoutStart_CreatesSingleToolTimelineEntry()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishToolCallCompletedAsync("call-1", "Created issue RRI-1", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, entries);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Tool, entries[0].EntryType);
        Assert.AreEqual("call-1", entries[0].ToolCallId);
        Assert.AreEqual("Created issue RRI-1", entries[0].ToolResult);
        Assert.IsNull(entries[0].ToolName);
        Assert.IsNull(entries[0].ToolArguments);
        Assert.IsNull(entries[0].Message);
        Assert.AreEqual(1L, entries[0].Sequence);
    }

    [TestMethod]
    public async Task CreatedEvent_PersistsFinalRenderedSystemPrompt()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        string agentKey = "agent-1";
        string repositoryRootPath = @"Z:\GitHub\CodeSnifferDog";
        string promptTemplate = new PromptAssetReader().ReadRequiredPrompt(ProjectPlanPromptAssetPaths.ProjectPlanAgentPrompt);
        string expectedSystemPrompt = new PromptTemplateRenderer().Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
            });
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, agentKey);
        AgentCreationResult createdAgent = new ProjectPlanAgentFactory(CreateCompactionOptions())
            .Create(
                NoOpChatClient.Instance,
                repositoryRootPath,
                new InMemoryProjectPlanTaskItemStore(),
                new CodeSnifferDog.Modules.Tools.Review.ReviewVerdictBuffer());

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", createdAgent.SystemPrompt, "Waiting", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == agentKey, TestContext.CancellationToken);

        Assert.AreEqual(expectedSystemPrompt, createdAgent.SystemPrompt);
        Assert.AreEqual(createdAgent.SystemPrompt, agent.SystemPrompt);
        Assert.AreNotEqual(promptTemplate, agent.SystemPrompt);
        Assert.IsFalse(agent.SystemPrompt.Contains("{{RepositoryRootPath}}", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ToolCallStartedEvent_Replayed_UpdatesExistingToolTimelineEntry()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishToolCallStartedAsync("call-1", "CreateRuleReviewIssue", "{ \"Severity\": \"High\" }", TestContext.CancellationToken);
        await agentScope.PublishToolCallStartedAsync("call-1", "CreateRuleReviewIssue", "{ \"Severity\": \"Critical\" }", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, entries);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Tool, entries[0].EntryType);
        Assert.AreEqual("call-1", entries[0].ToolCallId);
        Assert.AreEqual("CreateRuleReviewIssue", entries[0].ToolName);
        Assert.AreEqual("{ \"Severity\": \"Critical\" }", entries[0].ToolArguments);
        Assert.IsNull(entries[0].ToolResult);
        Assert.AreEqual(1L, entries[0].Sequence);
    }

    [TestMethod]
    public async Task ToolCallCompletedThenStarted_MergesIntoSingleToolTimelineEntry()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishToolCallCompletedAsync("call-1", "Created issue RRI-1", TestContext.CancellationToken);
        await agentScope.PublishToolCallStartedAsync("call-1", "CreateRuleReviewIssue", "{ \"Severity\": \"High\" }", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, entries);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Tool, entries[0].EntryType);
        Assert.AreEqual("call-1", entries[0].ToolCallId);
        Assert.AreEqual("CreateRuleReviewIssue", entries[0].ToolName);
        Assert.AreEqual("{ \"Severity\": \"High\" }", entries[0].ToolArguments);
        Assert.AreEqual("Created issue RRI-1", entries[0].ToolResult);
        Assert.IsNull(entries[0].Message);
        Assert.AreEqual(1L, entries[0].Sequence);
    }

    [TestMethod]
    public async Task MessageEvents_PersistInputAndOutputTimelineEntries()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishUserMessageAsync("Inspect Program.cs", TestContext.CancellationToken);
        await agentScope.PublishAssistantMessageAsync("I will inspect Program.cs.", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, entries);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Input, entries[0].EntryType);
        Assert.AreEqual("Inspect Program.cs", entries[0].Message);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Output, entries[1].EntryType);
        Assert.AreEqual("I will inspect Program.cs.", entries[1].Message);
        Assert.AreEqual(1L, entries[0].Sequence);
        Assert.AreEqual(2L, entries[1].Sequence);
        Assert.IsTrue(entries.All(entry => entry.ToolName is null));
        Assert.IsTrue(entries.All(entry => entry.ToolArguments is null));
        Assert.IsTrue(entries.All(entry => entry.ToolResult is null));
    }

    [TestMethod]
    public async Task TranscriptClearedEvent_RemovesAttemptTranscriptEntries_ButPreservesInputs()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishUserMessageAsync("Inspect Program.cs", TestContext.CancellationToken);
        DateTimeOffset clearAfterUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await agentScope.PublishAssistantMessageAsync("I will inspect Program.cs.", TestContext.CancellationToken);
        await agentScope.PublishToolCallStartedAsync("call-1", "RunShellCommand", "{}", TestContext.CancellationToken);
        await agentScope.PublishToolCallCompletedAsync("call-1", "Program.cs", TestContext.CancellationToken);
        await agentScope.PublishCompactionAsync(TestContext.CancellationToken);
        await agentScope.PublishTranscriptClearedAsync(clearAfterUtc, TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, entries);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Input, entries[0].EntryType);
        Assert.AreEqual("Inspect Program.cs", entries[0].Message);

        ProjectAgentLiveUpdateDto removeUpdate = liveUpdateNotifier.Updates
            .Single(update => update.Kind == ProjectAgentLiveUpdateKind.TimelineEntriesRemoved);
        Assert.IsNotNull(removeUpdate.RemovedTimelineEntries);
        Assert.AreEqual(agent.Id, removeUpdate.RemovedTimelineEntries.AgentId);
        Assert.HasCount(3, removeUpdate.RemovedTimelineEntries.TimelineEntryIds);
    }

    [TestMethod]
    public async Task CompactionEvent_PersistsCompactionTimelineEntry()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishCompactionAsync(TestContext.CancellationToken);
        await agentScope.PublishCompactionAsync(TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual(CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Waiting, agent.Status);
        Assert.HasCount(2, entries);
        Assert.IsTrue(entries.All(entry => entry.EntryType == ProjectAgentTimelineEntryType.Compaction));
        Assert.AreEqual(1L, entries[0].Sequence);
        Assert.AreEqual(2L, entries[1].Sequence);
        Assert.IsTrue(entries.All(entry => entry.Message is null));
        Assert.IsTrue(entries.All(entry => entry.ToolName is null));
        Assert.IsTrue(entries.All(entry => entry.ToolArguments is null));
        Assert.IsTrue(entries.All(entry => entry.ToolResult is null));
    }

    [TestMethod]
    public async Task LiveUpdates_PublishGroupAgentStatusAndTimelineProjectionEvents()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishStatusChangedAsync("Running", TestContext.CancellationToken);
        await agentScope.PublishUserMessageAsync("Inspect Program.cs", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        Assert.HasCount(4, liveUpdateNotifier.Updates);
        Assert.IsTrue(liveUpdateNotifier.Updates.All(update => update.ProjectId == projectId));

        ProjectAgentLiveUpdateDto groupUpdate = liveUpdateNotifier.Updates[0];
        Assert.AreEqual(ProjectAgentLiveUpdateKind.AgentGroupUpserted, groupUpdate.Kind);
        Assert.IsNotNull(groupUpdate.Group);
        ProjectAgentGroupLiveDto group = groupUpdate.Group;
        Assert.AreEqual("Review Task 1", group.DisplayName);

        ProjectAgentLiveUpdateDto agentUpdate = liveUpdateNotifier.Updates[1];
        Assert.AreEqual(ProjectAgentLiveUpdateKind.AgentUpserted, agentUpdate.Kind);
        Assert.IsNotNull(agentUpdate.Agent);
        ProjectAgentLiveDto agent = agentUpdate.Agent;
        Assert.AreEqual("Rule Review", agent.DisplayName);
        Assert.AreEqual(ProjectAgentRunStatus.Waiting, agent.Status);

        ProjectAgentLiveUpdateDto statusUpdate = liveUpdateNotifier.Updates[2];
        Assert.AreEqual(ProjectAgentLiveUpdateKind.AgentStatusChanged, statusUpdate.Kind);
        Assert.IsNotNull(statusUpdate.AgentStatus);
        ProjectAgentStatusChangedDto agentStatus = statusUpdate.AgentStatus;
        Assert.AreEqual(ProjectAgentRunStatus.Running, agentStatus.Status);
        Assert.AreEqual(agent.AgentId, agentStatus.AgentId);

        ProjectAgentLiveUpdateDto timelineUpdate = liveUpdateNotifier.Updates[3];
        Assert.AreEqual(ProjectAgentLiveUpdateKind.TimelineEntryUpserted, timelineUpdate.Kind);
        Assert.IsNotNull(timelineUpdate.TimelineEntry);
        ProjectAgentTimelineEntryDto timelineEntry = timelineUpdate.TimelineEntry;
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Input, timelineEntry.EntryKind);
        Assert.AreEqual("Inspect Program.cs", timelineEntry.Message);
        Assert.AreEqual(agent.AgentId, timelineEntry.AgentId);
    }

    [TestMethod]
    public async Task ToolProjectionLiveUpdates_UseSameTimelineEntryIdAcrossUpserts()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, liveUpdateNotifier, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishToolCallCompletedAsync("call-1", "Created issue RRI-1", TestContext.CancellationToken);
        await agentScope.PublishToolCallStartedAsync("call-1", "CreateRuleReviewIssue", "{ \"Severity\": \"High\" }", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        List<ProjectAgentLiveUpdateDto> timelineUpdates = liveUpdateNotifier.Updates
            .Where(update => update.Kind == ProjectAgentLiveUpdateKind.TimelineEntryUpserted)
            .ToList();

        Assert.HasCount(2, timelineUpdates);
        Assert.IsNotNull(timelineUpdates[0].TimelineEntry);
        Assert.IsNotNull(timelineUpdates[1].TimelineEntry);
        ProjectAgentTimelineEntryDto firstTimelineEntry = timelineUpdates[0].TimelineEntry!;
        ProjectAgentTimelineEntryDto secondTimelineEntry = timelineUpdates[1].TimelineEntry!;
        Assert.AreEqual(firstTimelineEntry.TimelineEntryId, secondTimelineEntry.TimelineEntryId);
        Assert.AreEqual("call-1", firstTimelineEntry.ToolCallId);
        Assert.AreEqual("Created issue RRI-1", firstTimelineEntry.ToolResult);
        Assert.IsNull(firstTimelineEntry.ToolName);
        Assert.AreEqual("CreateRuleReviewIssue", secondTimelineEntry.ToolName);
        Assert.AreEqual("{ \"Severity\": \"High\" }", secondTimelineEntry.ToolArguments);
        Assert.AreEqual("Created issue RRI-1", secondTimelineEntry.ToolResult);
    }

    private static ServiceProvider CreateServices()
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        return services.BuildServiceProvider();
    }

    private static OperationalContextAgentCompactionOptions CreateCompactionOptions() =>
        new OperationalContextAgentCompactionOptionsFactory(
            new PromptAssetReader(),
            new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>"))
            .CreateFromPromptAsset(
                ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt,
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                });

    private sealed class CollectingProjectAgentStatusLiveUpdateNotifier : IProjectAgentStatusLiveUpdateNotifier
    {
        public List<ProjectAgentLiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpChatClient : IChatClient
    {
        public static NoOpChatClient Instance { get; } = new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingSummarizer(string response) : IOperationalContextCompactionSummarizer
    {
        private readonly string _response = response;

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_response);
    }
}
