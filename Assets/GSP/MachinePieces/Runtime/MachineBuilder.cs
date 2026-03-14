using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MachineBuilder
{
    private readonly PieceBuilder pieceBuilder = new();

    public BuiltMachineRuntime BuildMachineOrThrow(string name, MachineRecipe recipe, MachinePieceLibrary library, Transform parent, bool debug)
    {
        var validation = MachinePieceValidation.ValidateMachineRecipe(recipe, library);
        if (validation.Count > 0)
        {
            throw new InvalidOperationException($"MachineRecipe validation failed:\n - {string.Join("\n - ", validation)}");
        }

        var root = new GameObject(name);
        root.transform.SetParent(parent, false);

        var runtime = new BuiltMachineRuntime
        {
            MachineId = recipe.id,
            Recipe = recipe,
            Root = root
        };

        foreach (var instance in recipe.pieces ?? Array.Empty<PieceInstance>())
        {
            var spec = library.PieceSpecs[instance.pieceId];
            var built = pieceBuilder.BuildPiece(root.transform, spec, instance, library, debug);
            runtime.Pieces[instance.instanceId] = built;
        }

        return runtime;
    }

    public MachineRecipe ResolveToRuntimeRecipe(MachineRecipe recipe)
    {
        return recipe;
    }
}
