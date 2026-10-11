namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Stores strings under keys, such as the browser's local storage.
/// </summary>
public interface IKeyValueStorage
{
    ValueTask<string?> GetAsync(string key);

    ValueTask SetAsync(string key, string value);

    ValueTask RemoveAsync(string key);
}
