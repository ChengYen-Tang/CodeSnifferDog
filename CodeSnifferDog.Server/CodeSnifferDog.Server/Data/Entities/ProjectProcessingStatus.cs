namespace CodeSnifferDog.Server.Data.Entities;

public enum ProjectProcessingStatus
{
    Queued = 0,
    Reviewing = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4,
}
