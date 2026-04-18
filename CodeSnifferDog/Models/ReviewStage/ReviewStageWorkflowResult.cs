using CodeSnifferDog.Models.Preparation;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageWorkflowResult
{
    public required RepositoryPreparationWorkflowResult PreparationResult { get; init; }

    public required IReadOnlyList<ReviewStageProjectResult> ProjectResults { get; init; }

    public required IReadOnlyList<string> RuleMarkdowns { get; init; }

    public required bool HasAnyReviewGroups { get; init; }

    public required bool AllReviewGroupsFinished { get; init; }
}
