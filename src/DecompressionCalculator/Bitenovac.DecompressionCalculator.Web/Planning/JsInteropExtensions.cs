using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Guards JavaScript interop calls, so that a runtime which has disconnected yields the default
/// result instead of an unhandled exception.
/// </summary>
internal static class JsInteropExtensions
{
    /// <summary>
    /// Invokes a JavaScript function that returns a value, or returns the default value when the
    /// runtime has disconnected.
    /// </summary>
    /// <typeparam name="T">The type the JavaScript function returns.</typeparam>
    /// <param name="js">The JavaScript runtime.</param>
    /// <param name="identifier">The identifier of the JavaScript function.</param>
    /// <param name="args">The arguments passed to the function.</param>
    /// <returns>The value the function returned, or the default value of <typeparamref name="T"/>.</returns>
    public static async ValueTask<T?> InvokeGuardedAsync<T>(this IJSRuntime js, string identifier,
        params object?[]? args)
    {
        try
        {
            return await js.InvokeAsync<T>(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            return default;
        }
    }

    /// <summary>
    /// Invokes a JavaScript function that returns nothing, ignoring a disconnected runtime.
    /// </summary>
    /// <param name="js">The JavaScript runtime.</param>
    /// <param name="identifier">The identifier of the JavaScript function.</param>
    /// <param name="args">The arguments passed to the function.</param>
    public static async ValueTask InvokeVoidGuardedAsync(this IJSRuntime js, string identifier,
        params object?[]? args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Invokes a function on a JavaScript module that returns a value, or returns the default value when
    /// the runtime has disconnected.
    /// </summary>
    /// <typeparam name="T">The type the JavaScript function returns.</typeparam>
    /// <param name="module">The JavaScript module reference, or <see langword="null"/> when the module was never loaded.</param>
    /// <param name="identifier">The identifier of the function on the module.</param>
    /// <param name="args">The arguments passed to the function.</param>
    /// <returns>The value the function returned, or the default value of <typeparamref name="T"/>.</returns>
    public static async ValueTask<T?> InvokeGuardedAsync<T>(this IJSObjectReference? module, string identifier,
        params object?[]? args)
    {
        if (module is null)
        {
            return default;
        }

        try
        {
            return await module.InvokeAsync<T>(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            return default;
        }
    }

    /// <summary>
    /// Invokes a function on a JavaScript module that returns nothing, ignoring a disconnected runtime.
    /// </summary>
    /// <param name="module">The JavaScript module reference, or <see langword="null"/> when the module was never loaded.</param>
    /// <param name="identifier">The identifier of the function on the module.</param>
    /// <param name="args">The arguments passed to the function.</param>
    public static async ValueTask InvokeVoidGuardedAsync(this IJSObjectReference? module, string identifier,
        params object?[]? args)
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
