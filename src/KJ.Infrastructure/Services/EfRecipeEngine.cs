using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

public sealed class EfRecipeEngine : IRecipeEngine
{
    private readonly IRecipeEngine _inner;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private bool _loaded;

    public EfRecipeEngine(IRecipeEngine inner, IDbContextFactory<KjDbContext> dbFactory)
    {
        _inner = inner;
        _dbFactory = dbFactory;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var recipes = db.Recipes
                .Include(r => r.Parameters)
                .AsNoTracking()
                .ToList();

            foreach (var recipe in recipes)
            {
                var data = new RecipeData(
                    recipe.Name,
                    recipe.Version,
                    recipe.Parameters.Select(p => new RecipeParameterData(p.Key, p.Value)).ToList(),
                    new DateTimeOffset(recipe.CreatedAt, TimeSpan.Zero),
                    recipe.CreatedBy);
                try { _inner.SaveRecipeAsync(data).GetAwaiter().GetResult(); }
                catch { }
            }
        }
        catch { }
    }

    public Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return _inner.ApplyAsync(recipeName, cancellationToken);
    }

    public Task<RecipeData?> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return _inner.GetRecipeAsync(recipeName, cancellationToken);
    }

    public Task<IReadOnlyList<RecipeData>> GetRecipesAsync(CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return _inner.GetRecipesAsync(cancellationToken);
    }

    public async Task SaveRecipeAsync(RecipeData recipe, CancellationToken cancellationToken = default)
    {
        await _inner.SaveRecipeAsync(recipe, cancellationToken).ConfigureAwait(false);

        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var existing = await db.Recipes
                    .Include(r => r.Parameters)
                    .FirstOrDefaultAsync(r => r.Name == recipe.Name, cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null)
                {
                    existing.Version = recipe.Version;
                    existing.CreatedBy = recipe.CreatedBy;
                    existing.CreatedAt = recipe.CreatedAt.UtcDateTime;
                    existing.Parameters.Clear();
                    foreach (var p in recipe.Parameters)
                        existing.Parameters.Add(new RecipeParameter { Id = Guid.NewGuid(), Key = p.Key, Value = p.Value });
                }
                else
                {
                    var entity = new Recipe
                    {
                        Id = Guid.NewGuid(),
                        Name = recipe.Name,
                        Version = recipe.Version,
                        CreatedAt = recipe.CreatedAt.UtcDateTime,
                        CreatedBy = recipe.CreatedBy,
                    };
                    foreach (var p in recipe.Parameters)
                        entity.Parameters.Add(new RecipeParameter { Id = Guid.NewGuid(), Key = p.Key, Value = p.Value });
                    db.Recipes.Add(entity);
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch { }
        }, cancellationToken);
    }

    public async Task DeleteRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        await _inner.DeleteRecipeAsync(recipeName, cancellationToken).ConfigureAwait(false);

        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var entity = await db.Recipes
                    .FirstOrDefaultAsync(r => r.Name == recipeName, cancellationToken)
                    .ConfigureAwait(false);
                if (entity is not null)
                {
                    db.Recipes.Remove(entity);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch { }
        }, cancellationToken);
    }
}
