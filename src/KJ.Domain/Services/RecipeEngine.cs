using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class RecipeEngine : IRecipeEngine
{
    private readonly ConcurrentDictionary<string, RecipeData> _recipes = new();

    public Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        if (!_recipes.ContainsKey(recipeName))
            throw new InvalidOperationException($"Recipe '{recipeName}' not found.");
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
}
