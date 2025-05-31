using System.Text.Json;

namespace FishChamp.Helpers;

/// <summary>
/// Utility class for handling property values from Dictionary&lt;string, object&gt; 
/// that may contain JsonElement objects when deserialized from JSON.
/// </summary>
public static class PropertyHelper
{
    /// <summary>
    /// Safely gets a typed value from a dictionary property, handling JsonElement conversion.
    /// </summary>
    /// <typeparam name="T">The target type to convert to</typeparam>
    /// <param name="properties">The properties dictionary</param>
    /// <param name="key">The property key</param>
    /// <param name="defaultValue">Default value if property is not found or conversion fails</param>
    /// <returns>The converted value or default value</returns>
    public static T GetProperty<T>(this Dictionary<string, object> properties, string key, T defaultValue = default!)
    {
        if (!properties.TryGetValue(key, out var value))
            return defaultValue;

        return ConvertValue<T>(value, defaultValue);
    }

    /// <summary>
    /// Safely gets a typed value from a dictionary property with out parameter pattern.
    /// </summary>
    /// <typeparam name="T">The target type to convert to</typeparam>
    /// <param name="properties">The properties dictionary</param>
    /// <param name="key">The property key</param>
    /// <param name="result">The converted value</param>
    /// <returns>True if the property exists and was successfully converted</returns>
    public static bool TryGetProperty<T>(this Dictionary<string, object> properties, string key, out T result)
    {
        result = default!;
        
        if (!properties.TryGetValue(key, out var value))
            return false;

        try
        {
            result = ConvertValue<T>(value, default!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts a value that may be a JsonElement to the target type.
    /// </summary>
    /// <typeparam name="T">The target type</typeparam>
    /// <param name="value">The value to convert</param>
    /// <param name="defaultValue">Default value if conversion fails</param>
    /// <returns>The converted value</returns>
    public static T ConvertValue<T>(object value, T defaultValue = default!)
    {
        if (value == null)
            return defaultValue;

        // If it's already the target type, return it
        if (value is T directValue)
            return directValue;

        // Handle JsonElement case
        if (value is JsonElement element)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element);
            }
            catch
            {
                return defaultValue;
            }
        }

        // Try direct conversion for common types
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets an integer property value with default.
    /// </summary>
    public static int GetInt(this Dictionary<string, object> properties, string key, int defaultValue = 0)
        => GetProperty(properties, key, defaultValue);

    /// <summary>
    /// Gets a double property value with default.
    /// </summary>
    public static double GetDouble(this Dictionary<string, object> properties, string key, double defaultValue = 0.0)
        => GetProperty(properties, key, defaultValue);

    /// <summary>
    /// Gets a string property value with default.
    /// </summary>
    public static string GetString(this Dictionary<string, object> properties, string key, string defaultValue = "")
        => GetProperty(properties, key, defaultValue);

    /// <summary>
    /// Gets a boolean property value with default.
    /// </summary>
    public static bool GetBool(this Dictionary<string, object> properties, string key, bool defaultValue = false)
        => GetProperty(properties, key, defaultValue);
}