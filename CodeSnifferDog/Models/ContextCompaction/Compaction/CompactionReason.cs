
namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

/// <summary>
/// Identifies why compaction was triggered.
/// </summary>
public enum CompactionReason
{
    /// <summary>
    /// Compaction ran because the automatic token threshold was reached.
    /// </summary>
    AutomaticThreshold = 0,

    /// <summary>
    /// Compaction ran reactively after a qualifying model invocation failure.
    /// </summary>
    Reactive = 1,

    /// <summary>
    /// Compaction ran because proactive context-collapse logic decided to collapse.
    /// </summary>
    ContextCollapseProactive = 2,
}
