namespace KJ.Infrastructure.Data.Entities;

public sealed class Recipe
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0";

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<RecipeParameter> Parameters { get; set; } = new List<RecipeParameter>();
}

public sealed class RecipeParameter
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
