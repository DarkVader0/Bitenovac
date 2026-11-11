using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitenovac.RemoteTools;

/// <summary>
/// Provides methods for reading and writing the JSON files a tool keeps: its settings, which a
/// person writes and must be told about when wrong, and its state, which the tool writes itself.
/// </summary>
internal static class JsonFile
{
    /// <summary>
    /// Reads settings from <paramref name="text"/>, naming <paramref name="source"/> when they are wrong.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="text">The file's text.</param>
    /// <param name="source">Where the text came from, for messages.</param>
    /// <param name="typeInfo">The generated serialization metadata of <typeparamref name="T"/>.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="InvalidOperationException">The text is empty or malformed.</exception>
    public static T Deserialize<T>(string text, string source, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return JsonSerializer.Deserialize(text, typeInfo)
                ?? throw new InvalidOperationException($"{source} is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{source} is not valid: {exception.Message}");
        }
    }

    /// <summary>
    /// Reads state from <paramref name="path"/>, or starts afresh when there is none or it cannot be read.
    /// </summary>
    /// <typeparam name="T">The state type.</typeparam>
    /// <param name="path">The state file.</param>
    /// <param name="typeInfo">The generated serialization metadata of <typeparamref name="T"/>.</param>
    /// <returns>The state.</returns>
    public static T LoadOrNew<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : new()
    {
        if (!File.Exists(path))
            return new T();

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), typeInfo) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    /// <summary>
    /// Writes <paramref name="value"/> to <paramref name="path"/>, whole or not at all.
    /// </summary>
    /// <typeparam name="T">The state type.</typeparam>
    /// <param name="path">The state file.</param>
    /// <param name="value">The state.</param>
    /// <param name="typeInfo">The generated serialization metadata of <typeparamref name="T"/>.</param>
    public static void Save<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var partial = path + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(value, typeInfo));
        File.Move(partial, path, overwrite: true);
    }
}
