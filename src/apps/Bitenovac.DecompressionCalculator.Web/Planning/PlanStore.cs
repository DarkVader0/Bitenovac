using System.Text.Json;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Keeps every saved plan, each under its own key, with an index of their identifiers and the
/// plan opened last.
/// </summary>
public sealed class PlanStore(IKeyValueStorage storage)
{
    public const string IndexKey = "bitenovac.plan-ids";
    public const string LastOpenedKey = "bitenovac.last-plan";
    public const string LegacyKey = "bitenovac.current-plan";
    private const string PlanKeyPrefix = "bitenovac.plan.";
    private const char Separator = ',';

    private bool _migrated;

    public static string PlanKey(Guid id) => $"{PlanKeyPrefix}{id:D}";

    /// <summary>
    /// Returns every plan that can be read, the most recently changed first.
    /// </summary>
    public async Task<IReadOnlyList<DivePlanDocument>> ListAsync()
    {
        await MigrateAsync();

        var plans = new List<DivePlanDocument>();
        foreach (var id in await ReadIndexAsync())
        {
            if (await LoadAsync(id) is { } plan)
            {
                plans.Add(plan);
            }
        }

        return [.. plans.OrderByDescending(static plan => plan.ModifiedAt)];
    }

    /// <summary>
    /// Returns the plan, or <see langword="null"/> when it is not stored or cannot be read.
    /// </summary>
    public async Task<DivePlanDocument?> LoadAsync(Guid id)
    {
        await MigrateAsync();
        return Read(await storage.GetAsync(PlanKey(id)));
    }

    /// <summary>
    /// Stores the plan, adding it to the index when it is new, and remembers it as opened last.
    /// </summary>
    public async Task SaveAsync(DivePlanDocument plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await MigrateAsync();
        await WriteAsync(plan);
    }

    /// <summary>
    /// Removes the plan from the store.
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        await MigrateAsync();

        var index = await ReadIndexAsync();
        await WriteIndexAsync([.. index.Where(existing => existing != id)]);
        await storage.RemoveAsync(PlanKey(id));

        if (await LastOpenedAsync() == id)
        {
            await storage.RemoveAsync(LastOpenedKey);
        }
    }

    /// <summary>
    /// Remembers the plan as the one to open when the app starts.
    /// </summary>
    public ValueTask MarkOpenedAsync(Guid id) => storage.SetAsync(LastOpenedKey, id.ToString("D"));

    /// <summary>
    /// Returns the plan opened last, or <see langword="null"/> when there is none.
    /// </summary>
    public async Task<Guid?> LastOpenedAsync() =>
        Guid.TryParse(await storage.GetAsync(LastOpenedKey), out var id) ? id : null;

    private async Task WriteAsync(DivePlanDocument plan)
    {
        await storage.SetAsync(PlanKey(plan.Id), DivePlanJson.Serialize(plan));

        var index = await ReadIndexAsync();
        if (!index.Contains(plan.Id))
        {
            await WriteIndexAsync([.. index, plan.Id]);
        }

        await MarkOpenedAsync(plan.Id);
    }

    /// <remarks>
    /// Before saved plans, the app kept a single plan under <see cref="LegacyKey"/>.
    /// </remarks>
    private async Task MigrateAsync()
    {
        if (_migrated)
        {
            return;
        }

        _migrated = true;
        if (Read(await storage.GetAsync(LegacyKey)) is { } legacy)
        {
            await WriteAsync(legacy);
        }

        await storage.RemoveAsync(LegacyKey);
    }

    private async Task<Guid[]> ReadIndexAsync()
    {
        var value = await storage.GetAsync(IndexKey);
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        return
        [
            .. value.Split(Separator)
                .Select(static text => Guid.TryParse(text, out var id) ? id : Guid.Empty)
                .Where(static id => id != Guid.Empty)
                .Distinct()
        ];
    }

    private ValueTask WriteIndexAsync(IEnumerable<Guid> ids) =>
        storage.SetAsync(IndexKey, string.Join(Separator, ids.Select(static id => id.ToString("D"))));

    private static DivePlanDocument? Read(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return DivePlanJson.Deserialize(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
