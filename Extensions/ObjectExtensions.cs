using System.Text.Json;

namespace Processors.Extensions;

public static class ObjectExtensions
{
    public static string ToDisplayString(this object? value)
    {
        if (value == null)
            return "null";

        // Handle arrays
        if (value is Array array)
        {
            var items = new List<string>();
            foreach (var item in array)
            {
                items.Add(item?.ToString() ?? "null");
            }
            return $"[{string.Join(", ", items)}]";
        }

        // Handle IEnumerable (but not strings)
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var items = new List<string>();
            foreach (var item in enumerable)
            {
                items.Add(item?.ToString() ?? "null");
            }
            return $"[{string.Join(", ", items)}]";
        }

        // Handle dictionaries
        if (value is System.Collections.IDictionary dictionary)
        {
            var items = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                items.Add($"{entry.Key}: {entry.Value?.ToDisplayString()}");
            }
            return $"{{{string.Join(", ", items)}}}";
        }

        // Handle complex objects
        if (value.GetType().IsClass && value is not string)
        {
            try
            {
                return JsonSerializer.Serialize(value, new JsonSerializerOptions 
                { 
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch
            {
                return value.ToString() ?? "null";
            }
        }

        return value.ToString() ?? "null";
    }
}