
namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

/// <summary>
/// Selects which compaction strategy an agent runtime should use.
/// </summary>
public enum CompactionMode
{
    /// <summary>
    /// Uses the standard proactive and reactive compaction pipeline.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Disables proactive compaction and only compacts after qualifying failures.
    /// </summary>
    ReactiveOnly = 1,

    /// <summary>
    /// Enables context-collapse orchestration in addition to standard compaction behavior.
    /// </summary>
    ContextCollapse = 2,
}
