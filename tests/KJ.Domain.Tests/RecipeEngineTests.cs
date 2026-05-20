using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class RecipeEngineTests
{
    [Fact]
    public async Task SaveRecipe_ShouldBeRetrievable()
    {
        var engine = new RecipeEngine();
        var recipe = new RecipeData("TestRecipe", "1.0",
            new[] { new RecipeParameterData("speed", "100") },
            DateTimeOffset.Now, "admin");

        await engine.SaveRecipeAsync(recipe);

        var loaded = await engine.GetRecipeAsync("TestRecipe");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("TestRecipe");
        loaded.Parameters.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecipeAsync_ShouldReturnNull_WhenNotFound()
    {
        var engine = new RecipeEngine();
        var result = await engine.GetRecipeAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRecipe_ShouldRemoveRecipe()
    {
        var engine = new RecipeEngine();
        await engine.SaveRecipeAsync(new RecipeData("TestRecipe", "1.0",
            Array.Empty<RecipeParameterData>(), DateTimeOffset.Now, "admin"));

        await engine.DeleteRecipeAsync("TestRecipe");

        var result = await engine.GetRecipeAsync("TestRecipe");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_ShouldThrow_WhenRecipeNotFound()
    {
        var engine = new RecipeEngine();
        var act = () => engine.ApplyAsync("nonexistent");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetRecipesAsync_ShouldReturnAll()
    {
        var engine = new RecipeEngine();
        await engine.SaveRecipeAsync(new RecipeData("R1", "1.0", Array.Empty<RecipeParameterData>(), DateTimeOffset.Now, "a"));
        await engine.SaveRecipeAsync(new RecipeData("R2", "1.0", Array.Empty<RecipeParameterData>(), DateTimeOffset.Now, "b"));

        var recipes = await engine.GetRecipesAsync();
        recipes.Should().HaveCount(2);
    }
}
