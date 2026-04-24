namespace KJ.Domain.Services;

public sealed class RecipeEngine : IRecipeEngine
{
    public Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        // 占位：后续在 Infrastructure 里从 DB/文件加载配方并下发到设备
        return Task.CompletedTask;
    }
}

