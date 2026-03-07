using System.Collections.Generic;

public static class WorldRecipeRegistry
{
    private static readonly List<IWorldRecipe> Recipes = new List<IWorldRecipe>
    {
        new VoidNeonRecipe(),
        new SavannaRiverRecipe(),
        new CanyonPassRecipe()
    };

    public static IReadOnlyList<IWorldRecipe> GetRecipes()
    {
        return Recipes;
    }
}
