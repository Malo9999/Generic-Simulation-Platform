using UnityEngine;

public static class MachinePiecesLabRecipeLoader
{
    public static MachineRecipe Load(TextAsset recipeAsset)
    {
        return MachinePieceJsonLoader.LoadFromTextAssetOrThrow<MachineRecipe>(recipeAsset, "MachinePiecesLab recipe");
    }
}
