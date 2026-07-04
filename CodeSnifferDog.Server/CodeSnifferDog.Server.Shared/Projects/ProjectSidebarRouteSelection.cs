namespace CodeSnifferDog.Server.Shared.Projects;

/// <summary>
/// Extracts the project selected by the current sidebar route.
/// </summary>
public static class ProjectSidebarRouteSelection
{
    /// <summary>
    /// Extracts the selected project identifier from a route and query string.
    /// </summary>
    /// <param name="uri">Current absolute URI.</param>
    /// <param name="relativePath">Current relative path.</param>
    /// <returns>The selected project identifier, or <see langword="null"/> when no project is selected.</returns>
    public static Guid? ExtractSelectedProjectId(Uri uri, string relativePath)
    {
        string[] segments = relativePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 2
            && string.Equals(segments[0], "reports", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[1], out Guid reportsProjectId))
            return reportsProjectId;

        if (segments.Length == 1
            && string.Equals(segments[0], "agent-status", StringComparison.OrdinalIgnoreCase))
        {
            string? projectId = TryGetQueryValue(uri.Query, "projectId");
            if (Guid.TryParse(projectId, out Guid agentStatusProjectId))
                return agentStatusProjectId;
        }

        return null;
    }

    /// <summary>
    /// Tries to read a single query-string value.
    /// </summary>
    /// <param name="query">Query-string portion of a URI.</param>
    /// <param name="key">Query-string key to read.</param>
    /// <returns>The decoded value, an empty string when the key has no value, or <see langword="null"/> when the key is missing.</returns>
    private static string? TryGetQueryValue(string query, string key)
    {
        ReadOnlySpan<char> trimmedQuery = query.AsSpan().TrimStart('?');
        if (trimmedQuery.IsEmpty)
            return null;

        foreach (string pair in trimmedQuery.ToString().Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            if (!string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
                continue;

            if (parts.Length == 1)
                return string.Empty;

            return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }
}
