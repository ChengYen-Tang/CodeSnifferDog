namespace CodeSnifferDog.Server.Shared.Projects;

public static class ProjectSidebarRouteSelection
{
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
