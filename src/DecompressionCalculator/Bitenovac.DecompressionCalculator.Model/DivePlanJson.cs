using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides methods for storing a plan as JSON and reading it back.
/// </summary>
public static class DivePlanJson
{
    /// <summary>
    /// Returns a plan as JSON.
    /// </summary>
    /// <param name="plan">The plan to store.</param>
    /// <returns>The plan as a JSON object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    public static string Serialize(DivePlanDocument plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return JsonSerializer.Serialize(plan, DivePlanJsonContext.Default.DivePlanDocument);
    }

    /// <summary>
    /// Reads a plan written by <see cref="Serialize"/>.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException"><paramref name="json"/> is not a stored plan, lacks a value every plan has, or holds a time not in UTC.</exception>
    public static DivePlanDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            return JsonSerializer.Deserialize(json, DivePlanJsonContext.Default.DivePlanDocument)
                   ?? throw new JsonException("The JSON holds no plan.");
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("The stored plan holds a value no plan can have.", exception);
        }
    }
}

/// <summary>
/// Provides the source-generated serializer for <see cref="DivePlanDocument"/>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(DivePlanDocument))]
internal sealed partial class DivePlanJsonContext : JsonSerializerContext;
