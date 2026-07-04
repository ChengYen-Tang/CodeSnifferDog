using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.RuleReview.State;

/// <summary>
/// Stores rule-review issues and no-issue conclusions per rule flow.
/// </summary>
internal sealed class IssueStateStore
{
    private readonly Dictionary<RuleFlowKey, FlowState> _states = [];

    /// <summary>
    /// Adds one issue to the specified rule flow unless an equivalent issue already exists.
    /// </summary>
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

    /// <summary>
    /// Gets one stored issue by identifier.
    /// </summary>
    public StoredIssue Get(RuleFlowKey ruleFlowKey, string ruleReviewIssueId) =>
        GetOrCreate(ruleFlowKey).Issues
            .FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim())
        ?? throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");

    /// <summary>
    /// Lists the stored issues for one rule flow.
    /// </summary>
    public IReadOnlyList<StoredIssue> List(RuleFlowKey ruleFlowKey) =>
        [.. GetOrCreate(ruleFlowKey).Issues];

    /// <summary>
    /// Updates one stored issue by identifier.
    /// </summary>
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

    /// <summary>
    /// Deletes one stored issue by identifier.
    /// </summary>
    public bool Delete(RuleFlowKey ruleFlowKey, string ruleReviewIssueId)
    {
        FlowState state = GetOrCreate(ruleFlowKey);
        StoredIssue? issue = state.Issues.FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

        if (issue is null)
            return false;

        state.Issues.Remove(issue);
        return true;
    }

    /// <summary>
    /// Gets the submitted no-issue conclusion, if one exists.
    /// </summary>
    public NoIssueConclusion? GetNoIssueConclusion(RuleFlowKey ruleFlowKey) =>
        GetOrCreate(ruleFlowKey).NoIssueConclusion;

    /// <summary>
    /// Submits a no-issue conclusion for one rule flow.
    /// </summary>
    public void SubmitNoIssueConclusion(RuleFlowKey ruleFlowKey, NoIssueConclusion conclusion)
    {
        FlowState state = GetOrCreate(ruleFlowKey);

        if (state.Issues.Count > 0)
            throw new InvalidOperationException("Cannot submit a no-issue conclusion while issues exist.");

        state.NoIssueConclusion = conclusion;
    }

    /// <summary>
    /// Clears all state for one rule flow.
    /// </summary>
    public void Clear(RuleFlowKey ruleFlowKey) =>
        _states.Remove(ruleFlowKey);

    /// <summary>
    /// Clones the stored state for one rule flow.
    /// </summary>
    public FlowState? Clone(RuleFlowKey ruleFlowKey) =>
        _states.TryGetValue(ruleFlowKey, out FlowState? state)
            ? state.Clone()
            : null;

    /// <summary>
    /// Restores one rule flow from a snapshot.
    /// </summary>
    public void Restore(RuleFlowKey ruleFlowKey, FlowState? snapshot)
    {
        if (snapshot is null)
            _states.Remove(ruleFlowKey);
        else
            _states[ruleFlowKey] = snapshot.Clone();
    }

    /// <summary>
    /// Gets the existing state for one rule flow or creates a new one.
    /// </summary>
    private FlowState GetOrCreate(RuleFlowKey ruleFlowKey)
    {
        if (_states.TryGetValue(ruleFlowKey, out FlowState? state))
            return state;

        state = new FlowState();
        _states.Add(ruleFlowKey, state);
        return state;
    }
}
