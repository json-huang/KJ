using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class RecipeEngine : IRecipeEngine
{
    private readonly ConcurrentDictionary<string, RecipeData> _recipes = new();
    private readonly ITagStore _tagStore;

    /// <summary>配方应用后触发。</summary>
    public event EventHandler<RecipeData>? RecipeApplied;

    public RecipeEngine(ITagStore tagStore)
    {
        _tagStore = tagStore;
    }

    public Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        if (!_recipes.TryGetValue(recipeName, out var recipe))
            throw new InvalidOperationException($"Recipe '{recipeName}' not found.");

        // 将配方参数写入 TagStore，实际驱动采集会读取这些值
        foreach (var param in recipe.Parameters)
        {
            var tagId = new TagId(param.Key);
            var value = ParseValue(param.Value);
            _tagStore.Upsert(new TagValue(tagId, value, TagQuality.Good, DateTimeOffset.Now));
        }

        RecipeApplied?.Invoke(this, recipe);
        return Task.CompletedTask;
    }

    public Task<RecipeData?> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        _recipes.TryGetValue(recipeName, out var recipe);
        return Task.FromResult(recipe);
    }

    public Task<IReadOnlyList<RecipeData>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecipeData>>(_recipes.Values.ToList().AsReadOnly());

    public Task SaveRecipeAsync(RecipeData recipe, CancellationToken cancellationToken = default)
    {
        _recipes[recipe.Name] = recipe;
        return Task.CompletedTask;
    }

    public Task DeleteRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        _recipes.TryRemove(recipeName, out _);
        return Task.CompletedTask;
    }

    private static object? ParseValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out var intVal)) return intVal;
        if (double.TryParse(value, out var dblVal)) return dblVal;
        if (bool.TryParse(value, out var boolVal)) return boolVal;
        return value;
    }
}
