
namespace CodeSnifferDog.Models.ContextCompaction.Failures;

public enum ModelInvocationFailureKind
{
    Unknown = 0,
    ContextWindowExceeded = 1,
    MediaPayloadTooLarge = 2,
}
