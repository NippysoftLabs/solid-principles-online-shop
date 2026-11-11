using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.RegularExpressions;

namespace Examples.Contracts.After;

/// <summary>
/// Compares two JSON schemas produced by JsonSchemaExporter for the same message, from the point
/// of view of a reader built for the old version that receives messages written with the new one.
/// It compares types, properties (names ignoring case), the required list, array items and enum values.
/// Enums only show up as values when ContractJson writes them as strings. It does not compare
/// formats, patterns, ranges or what a value means: an amount that changes from units to cents passes.
/// </summary>
public static partial class SchemaCompatibility
{
    public static JsonNode SchemaOf(Type type) =>
        JsonSchemaExporter.GetJsonSchemaAsNode(ContractJson.Options, type,
            new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });

    public static IReadOnlyList<string> BreakingChanges(JsonNode oldSchema, JsonNode newSchema)
    {
        var changes = new List<string>();
        Compare("$", oldSchema, newSchema, changes);
        return changes;
    }

    private static void Compare(string path, JsonNode? oldNode, JsonNode? newNode, List<string> changes)
    {
        if (oldNode is not JsonObject oldObj || newNode is not JsonObject newObj)
        {
            return;
        }

        var oldTypes = Types(oldObj);
        var newTypes = Types(newObj);
        // Every type the writer may now produce must be one the old reader accepts.
        // An integer is accepted where a number was.
        var unreadable = newTypes.Where(t => !oldTypes.Contains(t) && !(t == "integer" && oldTypes.Contains("number"))).ToList();
        if (oldTypes.Count > 0 && unreadable.Count > 0)
        {
            changes.Add($"{path}: type [{string.Join(",", oldTypes)}] became [{string.Join(",", newTypes)}]");
        }

        if (oldObj["enum"] is JsonArray oldEnum && newObj["enum"] is JsonArray newEnum)
        {
            var known = oldEnum.Select(v => v?.ToJsonString()).ToHashSet();
            foreach (var value in newEnum.Select(v => v?.ToJsonString()).Where(v => !known.Contains(v)))
            {
                changes.Add($"{path}: new enum value {value}");
            }
        }

        var oldRequired = Required(oldObj);
        var newRequired = Required(newObj);
        var newProps = newObj["properties"] as JsonObject;
        if (oldObj["properties"] is JsonObject oldProps)
        {
            foreach (var (name, oldProp) in oldProps)
            {
                var child = $"{path}.{name}";
                if (!TryFindProperty(newProps, name, out var newProp))
                {
                    if (oldRequired.Contains(name))
                    {
                        changes.Add($"{child}: required property removed");
                    }
                    continue;
                }
                if (oldRequired.Contains(name) && !newRequired.Contains(name))
                {
                    changes.Add($"{child}: was required, may now be missing");
                }
                Compare(child, oldProp, newProp, changes);
            }
        }

        Compare($"{path}[]", oldObj["items"], newObj["items"], changes);
    }

    private static List<string> Types(JsonObject schema) => schema["type"] switch
    {
        JsonValue v => [v.GetValue<string>()],
        JsonArray a => a.Select(t => t!.GetValue<string>()).ToList(),
        _ => [],
    };

    // Names compare ignoring case, the way ContractJson reads them: "amount" renamed to "Amount" still
    // reaches an old reader. An exact match wins when both spellings exist.
    private static bool TryFindProperty(JsonObject? properties, string name, out JsonNode? node)
    {
        node = null;
        if (properties is null)
        {
            return false;
        }
        if (properties.TryGetPropertyValue(name, out node))
        {
            return true;
        }
        foreach (var (key, value) in properties)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                node = value;
                return true;
            }
        }
        return false;
    }

    private static HashSet<string> Required(JsonObject schema) =>
        schema["required"] is JsonArray a ? a.Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];

    /// <summary>
    /// Versions are MAJOR.MINOR with ASCII digits and no leading zeros. A breaking change needs a
    /// higher major; any other change needs a higher version. Returns null when the bump is valid.
    /// </summary>
    public static string? CheckVersionBump(string oldVersion, string newVersion, IReadOnlyList<string> breakingChanges)
    {
        if (!TryParse(oldVersion, out var oldV) || !TryParse(newVersion, out var newV))
        {
            return $"versions must look like 1.2 (got '{oldVersion}' and '{newVersion}')";
        }
        if (newV.CompareTo(oldV) <= 0)
        {
            return $"{newVersion} is not higher than {oldVersion}";
        }
        if (breakingChanges.Count > 0 && newV.Major == oldV.Major)
        {
            return $"{breakingChanges.Count} breaking change(s) need version {oldV.Major + 1}.0, not {newVersion}";
        }
        return null;
    }

    private static bool TryParse(string text, out (int Major, int Minor) version)
    {
        version = default;
        var m = VersionPattern().Match(text);
        if (!m.Success)
        {
            return false;
        }
        version = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
        return true;
    }

    // [0-9], not \d: in .NET \d also matches other Unicode digits such as U+0662.
    // \z, not $: in .NET $ also matches before a final "\n".
    [GeneratedRegex(@"^(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
