using System.Collections.Concurrent;
using NJsonSchema;
using NJsonSchema.Validation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>One schema-conformance violation found while validating ITR JSON against the ITD schema.</summary>
public sealed record SchemaIssue(string Path, string Kind, string Property);

/// <summary>
/// Result of schema validation: whether an official schema was available for the AY+form, and any
/// violations found. <see cref="SchemaAvailable"/> is false (with no errors) when no schema is bundled.
/// </summary>
public sealed record SchemaValidationResult(bool SchemaAvailable, IReadOnlyList<SchemaIssue> Errors)
{
    public static readonly SchemaValidationResult NotAvailable = new(false, Array.Empty<SchemaIssue>());
    public bool IsConformant => SchemaAvailable && Errors.Count == 0;
}

/// <summary>
/// Validates a generated ITR JSON document against the OFFICIAL ITD JSON schema (JSON Schema draft-04),
/// embedded as a resource in this assembly. Only the AY2026-27-notified forms (ITR-1, ITR-4) are bundled
/// today; for any other AY/form there is no schema yet and validation is skipped
/// (<see cref="SchemaValidationResult.SchemaAvailable"/> = false). The parsed schema is cached per
/// resource — parsing the ~150–250KB document is the only expensive step; <c>Validate</c> is fast.
/// </summary>
public static class ItrSchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema?> Cache = new();

    /// <summary>True when an official schema is bundled for this AY + form.</summary>
    public static bool HasSchema(string assessmentYearCode, ItrType form)
        => TryResourceName(assessmentYearCode, form, out _);

    public static SchemaValidationResult Validate(string assessmentYearCode, ItrType form, string json)
    {
        if (!TryResourceName(assessmentYearCode, form, out var resource))
        {
            return SchemaValidationResult.NotAvailable;
        }

        var schema = Cache.GetOrAdd(resource, LoadSchema);
        if (schema is null)
        {
            return SchemaValidationResult.NotAvailable;
        }

        var errors = new List<SchemaIssue>();
        Collect(schema.Validate(json), errors);
        return new SchemaValidationResult(true, errors);
    }

    /// <summary>
    /// Flatten NJsonSchema's validation errors, including the leaf causes nested under a container error
    /// (e.g. ArrayItemNotValid / PropertyRequired on a sub-object) so a violation names the exact field —
    /// not just the array/object that contains it.
    /// </summary>
    private static void Collect(IEnumerable<ValidationError> errors, List<SchemaIssue> sink)
    {
        foreach (var e in errors)
        {
            sink.Add(new SchemaIssue(
                string.IsNullOrEmpty(e.Path) ? "$" : e.Path,
                e.Kind.ToString(),
                e.Property ?? string.Empty));

            if (e is ChildSchemaValidationError child)
            {
                foreach (var group in child.Errors.Values)
                {
                    Collect(group, sink);
                }
            }
        }
    }

    private static bool TryResourceName(string assessmentYearCode, ItrType form, out string resource)
    {
        // Canonicalise the AY ("AY2026-27" / "2026.0.0-provisional" → "2026"; "AY2025-26" → "2025").
        var ay = new string((assessmentYearCode ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

        // AY2026-27: only ITR-1 & ITR-4 are notified.
        if (ay.Contains("2026"))
        {
            switch (form)
            {
                case ItrType.ITR1:
                    resource = "ITR-1_2026.json";
                    return true;
                case ItrType.ITR4:
                    resource = "ITR-4_2026.json";
                    return true;
            }
        }

        // AY2025-26: ITR-2 & ITR-3 are conformant + runtime-validated.
        if (ay.Contains("2025"))
        {
            switch (form)
            {
                case ItrType.ITR2:
                    resource = "ITR-2_2025.json";
                    return true;
                case ItrType.ITR3:
                    resource = "ITR-3_2025.json";
                    return true;
            }
        }

        resource = string.Empty;
        return false;
    }

    private static JsonSchema? LoadSchema(string resource)
    {
        var asm = typeof(ItrSchemaValidator).Assembly;
        using var stream = asm.GetManifestResourceStream(resource);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return JsonSchema.FromJsonAsync(text).GetAwaiter().GetResult();
    }
}
