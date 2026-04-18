using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ReviewStage;

namespace CodeSnifferDog.Models.ReviewAgentTeam;

public sealed class ReviewAgentTeamRunResult
{
    public required RepositoryPreparationWorkflowResult PreparationResult { get; init; }

    public required ReviewStageWorkflowResult ReviewStageResult { get; init; }
}
