using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report.State;

internal sealed class WorkingStateStore
{
    private readonly Dictionary<RuleFlowKey, FlowState> _flowStates = [];

    public void Initialize(RuleFlowKey ruleFlowKey, IEnumerable<ReportStoredIssue> snapshotIssues)
    {
        FlowState flowState = GetOrCreate(ruleFlowKey);
        flowState.WorkingIssues.Clear();

        foreach (ReportStoredIssue issue in snapshotIssues)
            flowState.WorkingIssues.Add(RuleIssueStoreMapper.Clone(issue));

        flowState.LatestDiff = CreateEmptyDiff();
    }

    public ReportStoredIssue Add(RuleFlowKey ruleFlowKey, NormalizedRuleIssue normalizedIssue, string issueId)
    {
        FlowState flowState = GetOrCreate(ruleFlowKey);
        ReportStoredIssue? existingIssue = flowState.WorkingIssues
            .FirstOrDefault(candidate => RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(candidate, normalizedIssue));
        if (existingIssue is not null)
            return existingIssue;

        ReportStoredIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(normalizedIssue, issueId);
        flowState.WorkingIssues.Add(storedIssue);
        return storedIssue;
    }

    public ReportStoredIssue Get(RuleFlowKey ruleFlowKey, string ruleReportIssueId) =>
        GetOrCreate(ruleFlowKey).WorkingIssues
            .FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim())
        ?? throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");

    public IReadOnlyList<ReportStoredIssue> List(RuleFlowKey ruleFlowKey) =>
        [.. GetOrCreate(ruleFlowKey).WorkingIssues];

    public ReportStoredIssue Update(RuleFlowKey ruleFlowKey, string ruleReportIssueId, NormalizedRuleIssue normalizedIssue)
    {
        FlowState flowState = GetOrCreate(ruleFlowKey);
        int index = flowState.WorkingIssues.FindIndex(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

        if (index < 0)
            throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");

        ReportStoredIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(
            normalizedIssue,
            flowState.WorkingIssues[index].RuleReportIssueId);
        flowState.WorkingIssues[index] = storedIssue;
        return storedIssue;
    }

    public bool Delete(RuleFlowKey ruleFlowKey, string ruleReportIssueId)
    {
        FlowState flowState = GetOrCreate(ruleFlowKey);
        ReportStoredIssue? issue = flowState.WorkingIssues.FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

        if (issue is null)
            return false;

        flowState.WorkingIssues.Remove(issue);
        return true;
    }

    public Diff GetLatestDiff(RuleFlowKey ruleFlowKey) =>
        GetOrCreate(ruleFlowKey).LatestDiff;

    public void SetLatestDiff(RuleFlowKey ruleFlowKey, Diff diff) =>
        GetOrCreate(ruleFlowKey).LatestDiff = diff;

    public void Clear(RuleFlowKey ruleFlowKey)
    {
        if (!_flowStates.TryGetValue(ruleFlowKey, out FlowState? flowState))
            return;

        flowState.WorkingIssues.Clear();
        flowState.LatestDiff = CreateEmptyDiff();
    }

    public void Remove(RuleFlowKey ruleFlowKey) =>
        _flowStates.Remove(ruleFlowKey);

    public FlowState? Clone(RuleFlowKey ruleFlowKey) =>
        _flowStates.TryGetValue(ruleFlowKey, out FlowState? state)
            ? state.Clone()
            : null;

    public void Restore(RuleFlowKey ruleFlowKey, FlowState? snapshot)
    {
        if (snapshot is null)
            _flowStates.Remove(ruleFlowKey);
        else
            _flowStates[ruleFlowKey] = snapshot.Clone();
    }

    public static Diff CreateEmptyDiff()
        =>
        new()
        {
            CreatedIssues = [],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

    public static Diff CloneDiff(Diff diff)
        =>
        new()
        {
            CreatedIssues = [.. diff.CreatedIssues.Select(RuleIssueStoreMapper.Clone)],
            UpdatedIssues = [.. diff.UpdatedIssues.Select(RuleIssueStoreMapper.Clone)],
            DeletedIssues = [.. diff.DeletedIssues.Select(RuleIssueStoreMapper.Clone)],
        };

    private FlowState GetOrCreate(RuleFlowKey ruleFlowKey)
    {
        if (_flowStates.TryGetValue(ruleFlowKey, out FlowState? state))
            return state;

        state = new FlowState();
        _flowStates.Add(ruleFlowKey, state);
        return state;
    }
}
