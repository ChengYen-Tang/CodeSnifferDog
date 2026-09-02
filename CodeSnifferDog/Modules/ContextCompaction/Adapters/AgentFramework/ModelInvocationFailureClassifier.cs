using CodeSnifferDog.Models.ContextCompaction.Failures;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>
/// Normalizes provider-specific context-window failures without coupling the core compaction module to one provider SDK.
/// </summary>
internal static class ModelInvocationFailureClassifier
{
    private static readonly string[] ContextErrorCodes =
    [
        "contexttoolarge",
        "contextlengthexceeded",
        "contextwindowexceeded",
        "maximumcontextlengthexceeded",
        "maxcontextlengthexceeded",
        "prompttoolong",
        "inputtoolong",
    ];

    /// <summary>
    /// Determines whether an exception or any of its inner exceptions reports a context-window overflow.
    /// </summary>
    /// <param name="exception">Exception returned by a provider or an adapter.</param>
    /// <returns><see langword="true" /> when the failure is a context-window overflow.</returns>
    public static bool IsContextWindowExceeded(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        foreach (Exception candidate in Enumerate(exception))
        {
            if (candidate is ModelInvocationException
                {
                    FailureKind: ModelInvocationFailureKind.ContextWindowExceeded,
                })
            {
                return true;
            }

            object? rawResponse = TryGetRawResponse(candidate);
            int? status = TryGetStatus(candidate) ?? TryGetStatus(rawResponse);
            bool providerFailure = status is > 0 ||
                rawResponse is not null ||
                IsKnownProviderException(candidate);

            if (HasStructuredContextCode(candidate))
            {
                return true;
            }

            if (providerFailure &&
                (ContainsExplicitContextCode(candidate.Message) ||
                 ContainsExplicitContextCode(TryGetStringProperty(rawResponse, "ReasonPhrase")) ||
                 ContainsContextOverflowPhrase(candidate.Message) ||
                 ContainsContextOverflowPhrase(TryGetStringProperty(rawResponse, "ReasonPhrase"))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts an identified context-window failure to the application's normalized exception type.
    /// </summary>
    /// <param name="exception">Failure known to describe a context-window overflow.</param>
    /// <returns>The original normalized exception, or a new normalized wrapper.</returns>
    public static ModelInvocationException NormalizeContextWindowExceeded(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        foreach (Exception candidate in Enumerate(exception))
        {
            if (candidate is ModelInvocationException
                {
                    FailureKind: ModelInvocationFailureKind.ContextWindowExceeded,
                }
                normalized)
            {
                return normalized;
            }
        }

        return new ModelInvocationException(
            ModelInvocationFailureKind.ContextWindowExceeded,
            "The provider rejected the request because its context was too large.",
            exception);
    }

    private static bool HasStructuredContextCode(Exception exception)
    {
        foreach (string propertyName in new[] { "ErrorCode", "Code" })
        {
            if (ContainsExplicitContextCode(TryGetStringProperty(exception, propertyName)))
                return true;
        }

        if (exception.Data is not { Count: > 0 } data)
            return false;

        foreach (DictionaryEntry entry in data)
        {
            if (ContainsExplicitContextCode(ConvertToString(entry.Key)) ||
                ContainsExplicitContextCode(ConvertToString(entry.Value)))
            {
                return true;
            }
        }

        return false;
    }

    private static int? TryGetStatus(object? value)
    {
        if (value is null)
            return null;

        foreach (string propertyName in new[] { "Status", "StatusCode" })
        {
            object? statusValue = TryGetProperty(value, propertyName);
            if (statusValue is null)
                continue;

            try
            {
                int status = Convert.ToInt32(statusValue, CultureInfo.InvariantCulture);
                if (status > 0)
                    return status;
            }
            catch (FormatException)
            {
            }
            catch (InvalidCastException)
            {
            }
        }

        return null;
    }

    private static object? TryGetRawResponse(Exception exception)
    {
        MethodInfo? method = exception.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(static candidate =>
                candidate.Name == "GetRawResponse" &&
                candidate.GetParameters().Length == 0);

        if (method is null)
            return TryGetProperty(exception, "RawResponse");

        try
        {
            return method.Invoke(exception, null);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? TryGetStringProperty(object? instance, string propertyName) =>
        ConvertToString(TryGetProperty(instance, propertyName));

    private static object? TryGetProperty(object? instance, string propertyName)
    {
        if (instance is null)
            return null;

        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is null || !property.CanRead || property.GetIndexParameters().Length != 0)
            return null;

        try
        {
            return property.GetValue(instance);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? ConvertToString(object? value) =>
        value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    private static bool IsKnownProviderException(Exception exception)
    {
        string typeName = exception.GetType().FullName ?? exception.GetType().Name;
        return typeName.StartsWith("Azure.", StringComparison.Ordinal) ||
            typeName.StartsWith("System.ClientModel.", StringComparison.Ordinal) ||
            typeName.Contains("RequestFailedException", StringComparison.Ordinal) ||
            typeName.Contains("ClientResultException", StringComparison.Ordinal) ||
            typeName.Contains("HttpRequestException", StringComparison.Ordinal) ||
            typeName.Contains("HttpResponseException", StringComparison.Ordinal) ||
            typeName.Contains("ApiException", StringComparison.Ordinal) ||
            typeName.Contains("OpenAI", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsExplicitContextCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return ContextErrorCodes.Any(normalized.Contains);
    }

    private static bool ContainsContextOverflowPhrase(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("maximum context length", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("context window exceeded", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("prompt is too long", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("input is too long", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("context", StringComparison.OrdinalIgnoreCase) &&
             (message.Contains("too large", StringComparison.OrdinalIgnoreCase) ||
              message.Contains("exceeds the limit", StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<Exception> Enumerate(Exception root)
    {
        Stack<Exception> pending = new();
        HashSet<Exception> visited = new(ReferenceEqualityComparer.Instance);
        pending.Push(root);

        while (pending.TryPop(out Exception? current))
        {
            if (!visited.Add(current))
                continue;

            yield return current;

            if (current is AggregateException aggregateException)
            {
                foreach (Exception innerException in aggregateException.InnerExceptions)
                    pending.Push(innerException);
            }

            if (current.InnerException is { } nestedException)
                pending.Push(nestedException);
        }
    }
}
