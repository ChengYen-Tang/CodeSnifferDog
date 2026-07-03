using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.RuleReview.State;

internal sealed class IssueStateStore
{
    private readonly Dictionary<RuleFlowKey, FlowState> _states = [];

    public StoredIssue Add(RuleFlowKey ruleFlowKey, NormalizedRuleIssue normalizedIssue, string issueId)
    {
        FlowState state = GetOrCreate(ruleFlowKey);
        state.NoIssueConclusion = null;

        StoredIssue? existingIssue = state.Issues
            .FirstOrDefault(candidate => RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(candidate, normalizedIssue));
        if (existingIssue is not null)
            return existingIssue;

        StoredIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(normalizedIssue, issueId);
        state.Issues.Add(storedIssue);
        return storedIssue;
    }

    public StoredIssue Get(RuleFlowKey ruleFlowKey, string ruleReviewIssueId) =>
        GetOrCreate(ruleFlowKey).Issues
            .FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim())
        ?? throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");

    public IReadOnlyList<StoredIssue> List(RuleFlowKey ruleFlowKey) =>
        [.. GetOrCreate(ruleFlowKey).Issues];

    public StoredIssue Update(RuleFlowKey ruleFlowKey, string ruleReviewIssueId, NormalizedRuleIssue normalizedIssue)
    {
        FlowState state = GetOrCreate(ruleFlowKey);
        int index = state.Issues.FindIndex(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

        if (index < 0)
            throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");

        StoredIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(
            normalizedIssue,
            state.Issues[index].RuleReviewIssueId);
        state.Issues[index] = storedIssue;
        return storedIssue;
    }

    public bool Delete(RuleFlowKey ruleFlowKey, string ruleReviewIssueId)
    {
        FlowState state = GetOrCreate(ruleFlowKey);
        StoredIssue? issue = state.Issues.FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

        if (issue is null)
            return false;

        state.Issues.Remove(issue);
        return true;
    }

    public NoIssueConclusion? GetNoIssueConclusion(RuleFlowKey ruleFlowKey) =>
        GetOrCreate(ruleFlowKey).NoIssueConclusion;

    public void SubmitNoIssueConclusion(RuleFlowKey ruleFlowKey, NoIssueConclusion conclusion)
    {
        FlowState state = GetOrCreate(ruleFlowKey);

        if (state.Issues.Count > 0)
            throw new InvalidOperationException("Cannot submit a no-issue conclusion while issues exist.");

        state.NoIssueConclusion = conclusion;
    }

    public void Clear(RuleFlowKey ruleFlowKey) =>
        _states.Remove(ruleFlowKey);

    public FlowState? Clone(RuleFlowKey ruleFlowKey) =>
        _states.TryGetValue(ruleFlowKey, out FlowState? state)
            ? state.Clone()
            : null;

    public void Restore(RuleFlowKey ruleFlowKey, FlowState? snapshot)
    {
        if (snapshot is null)
            _states.Remove(ruleFlowKey);
        else
            _states[ruleFlowKey] = snapshot.Clone();
    }

    private FlowState GetOrCreate(RuleFlowKey ruleFlowKey)
    {
        if (_states.TryGetValue(ruleFlowKey, out FlowState? state))
            return state;

        state = new FlowState();
        _states.Add(ruleFlowKey, state);
        return state;
    }
}
