namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Provides copies of a read-only list with one item replaced, added, inserted, or removed.
/// </summary>
public static class ListEdit
{
    public static T[] Replace<T>(IReadOnlyList<T> list, int index, T item)
    {
        var copy = list.ToArray();
        copy[index] = item;
        return copy;
    }

    public static T[] Add<T>(IReadOnlyList<T> list, T item) => [.. list, item];

    public static T[] Insert<T>(IReadOnlyList<T> list, int index, T item) => [.. list.Take(index), item, .. list.Skip(index)];

    public static T[] RemoveAt<T>(IReadOnlyList<T> list, int index) => [.. list.Take(index), .. list.Skip(index + 1)];
}
