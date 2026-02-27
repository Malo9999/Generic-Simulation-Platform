using System;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Sim Settings/AntColonies", fileName = "AntColoniesSimSettings")]
public class AntColoniesSimSettings : SimSettingsBase
{
    [Serializable]
    public struct Params
    {
        public int nestCount;
        public int antsPerNest;
        public int maxAntsTotal;
        public int foodCount;
    }

    public Params parameters = new Params
    {
        nestCount = 2,
        antsPerNest = 12,
        maxAntsTotal = 50,
        foodCount = 10
    };

    private void OnEnable() => simulationId = "AntColonies";

    public override void ApplyTo(ScenarioConfig cfg)
    {
        if (cfg == null)
        {
            return;
        }

        cfg.antColonies ??= new AntColoniesConfig();
        cfg.antColonies.nestCount = parameters.nestCount;
        cfg.antColonies.antsPerNest = parameters.antsPerNest;
        cfg.antColonies.maxAntsTotal = parameters.maxAntsTotal;
        cfg.antColonies.worldRecipe ??= new AntWorldRecipe();
        cfg.antColonies.worldRecipe.foodCount = parameters.foodCount;
        cfg.antColonies.Normalize();
    }
}
