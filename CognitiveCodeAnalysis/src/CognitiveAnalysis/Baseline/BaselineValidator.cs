/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using System.Text.Json;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

/// <summary>
/// Checks the fields of a baseline that are read, and collects every problem instead of stopping at the first. Fields
/// that are not read, and unknown ones, are not checked.
/// </summary>
internal static class BaselineValidator
{
    private static readonly NullabilityInfoContext Nullability = new();

    internal static List<string> Validate(JsonElement root)
    {
        var errors = new List<string>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add("the root must be a JSON object");
            return errors;
        }

        ValidateObject(root, typeof(CognitiveBaselineSnapshot), "", errors);
        return errors;
    }

    private static void ValidateObject(JsonElement element, Type type, string path, List<string> errors)
    {
        foreach (JsonProperty member in element.EnumerateObject())
        {
            PropertyInfo? property = type.GetProperties().FirstOrDefault(
                p => string.Equals(p.Name, member.Name, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                ValidateValue(member.Value, property, path + member.Name, errors);
            }
        }
    }

    private static void ValidateValue(JsonElement value, PropertyInfo property, string path, List<string> errors)
    {
        Type type = property.PropertyType;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            ValidateList(value, type.GetGenericArguments()[0], path, errors);
            return;
        }

        bool nullable = Nullable.GetUnderlyingType(type) != null
            || Nullability.Create(property).WriteState == NullabilityState.Nullable;
        if (value.ValueKind == JsonValueKind.Null && nullable)
        {
            return;
        }

        string? problem = Problem(value, Nullable.GetUnderlyingType(type) ?? type);
        if (problem != null)
        {
            errors.Add($"{path} {problem}");
        }
    }

    private static void ValidateList(JsonElement value, Type itemType, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{path} must be an array");
            return;
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            if (item.ValueKind == JsonValueKind.Object)
            {
                ValidateObject(item, itemType, itemPath + ".", errors);
            }
            else
            {
                errors.Add($"{itemPath} must be an object");
            }
        }
    }

    private static string? Problem(JsonElement value, Type type)
    {
        if (type == typeof(string))
        {
            return value.ValueKind == JsonValueKind.String ? null : "must be a string";
        }

        if (type == typeof(DateTimeOffset))
        {
            return value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out _)
                ? null
                : "must be a date and time";
        }

        if (type == typeof(int))
        {
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && number >= 0
                ? null
                : "must be a non-negative integer";
        }

        return value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double real)
            && double.IsFinite(real)
            && real >= 0
                ? null
                : "must be a finite, non-negative number";
    }
}
