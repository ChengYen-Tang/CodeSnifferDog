using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

internal sealed class RuleReportWorkingStateStore
{
    private readonly Dictionary<RuleFlowKey, RuleReportFlowState> _flowStates = [];

    public void Initialize(RuleFlowKey ruleFlowKey, IEnumerable<StoredRuleReportIssue> snapshotIssues)
    {
        RuleReportFlowState flowState = GetOrCreate(ruleFlowKey);
        flowState.WorkingIssues.Clear();

        foreach (StoredRuleReportIssue issue in snapshotIssues)
            flowState.WorkingIssues.Add(RuleIssueStoreMapper.Clone(issue));

        flowState.LatestDiff = CreateEmptyDiff();
    }

    public StoredRuleReportIssue Add(RuleFlowKey ruleFlowKey, NormalizedRuleIssue normalizedIssue, string issueId)
    {
        RuleReportFlowState flowState = GetOrCreate(ruleFlowKey);
        StoredRuleReportIssue? existingIssue = flowState.WorkingIssues
            .FirstOrDefault(candidate => RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(candidate, normalizedIssue));
        if (existingIssue is not null)
            return existingIssue;

        StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(normalizedIssue, issueId);
        flowState.WorkingIssues.Add(storedIssue);
        return storedIssue;
    }

    public StoredRuleReportIssue Get(RuleFlowKey ruleFlowKey, string ruleReportIssueId) =>
        GetOrCreate(ruleFlowKey).WorkingIssues
            .FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim())
        ?? throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");

    public IReadOnlyList<StoredRuleReportIssue> List(RuleFlowKey ruleFlowKey) =>
        [.. GetOrCreate(ruleFlowKey).WorkingIssues];

    public StoredRuleReportIssue Update(RuleFlowKey ruleFlowKey, string ruleReportIssueId, NormalizedRuleIssue normalizedIssue)
    {
        RuleReportFlowState flowState = GetOrCreate(ruleFlowKey);
        int index = flowState.WorkingIssues.FindIndex(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

        if (index < 0)
            throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");

        StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(
            normalizedIssue,
            flowState.WorkingIssues[index].RuleReportIssueId);
        flowState.WorkingIssues[index] = storedIssue;
        return storedIssue;
    }

    public bool Delete(RuleFlowKey ruleFlowKey, string ruleReportIssueId)
    {
        RuleReportFlowState flowState = GetOrCreate(ruleFlowKey);
        StoredRuleReportIssue? issue = flowState.WorkingIssues.FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

        if (issue is null)
            return false;

        flowState.WorkingIssues.Remove(issue);
        return true;
    }

    public RuleReportDiff GetLatestDiff(RuleFlowKey ruleFlowKey) =>
        GetOrCreate(ruleFlowKey).LatestDiff;

    public void SetLatestDiff(RuleFlowKey ruleFlowKey, RuleReportDiff diff) =>
        GetOrCreate(ruleFlowKey).LatestDiff = diff;

    public void Clear(RuleFlowKey ruleFlowKey)
    {
        if (!_flowStates.TryGetValue(ruleFlowKey, out RuleReportFlowState? flowState))
            return;

        flowState.WorkingIssues.Clear();
        flowState.LatestDiff = CreateEmptyDiff();
    }

    public void Remove(RuleFlowKey ruleFlowKey) =>
        _flowStates.Remove(ruleFlowKey);

    public RuleReportFlowState? Clone(RuleFlowKey ruleFlowKey) =>
        _flowStates.TryGetValue(ruleFlowKey, out RuleReportFlowState? state)
            ? state.Clone()
            : null;

    public void Restore(RuleFlowKey ruleFlowKey, RuleReportFlowState? snapshot)
    {
        if (snapshot is null)
            _flowStates.Remove(ruleFlowKey);
        else
            _flowStates[ruleFlowKey] = snapshot.Clone();
    }

    public static RuleReportDiff CreateEmptyDiff()
        =>
        new()
        {
            CreatedIssues = [],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

    public static RuleReportDiff CloneDiff(RuleReportDiff diff)
        =>
        new()
        {
            CreatedIssues = [.. diff.CreatedIssues.Select(RuleIssueStoreMapper.Clone)],
            UpdatedIssues = [.. diff.UpdatedIssues.Select(RuleIssueStoreMapper.Clone)],
            DeletedIssues = [.. diff.DeletedIssues.Select(RuleIssueStoreMapper.Clone)],
        };

    private RuleReportFlowState GetOrCreate(RuleFlowKey ruleFlowKey)
    {
        if (_flowStates.TryGetValue(ruleFlowKey, out RuleReportFlowState? state))
            return state;

        state = new RuleReportFlowState();
        _flowStates.Add(ruleFlowKey, state);
        return state;
    }
}
