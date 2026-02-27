using System;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Sim Settings/RaceCar", fileName = "RaceCarSimSettings")]
public class RaceCarSimSettings : SimSettingsBase
{
    [Serializable]
    public struct Params
    {
        public int carCount;
        public int laps;
        public string trackPreset;
    }

    public Params parameters = new Params
    {
        carCount = 10,
        laps = 3,
        trackPreset = "Auto"
    };

    private void OnEnable() => simulationId = "RaceCar";

    public override void ApplyTo(ScenarioConfig cfg)
    {
        if (cfg == null)
        {
            return;
        }

        cfg.raceCar ??= new RaceCarConfig();
        cfg.raceCar.carCount = parameters.carCount;
        cfg.raceCar.laps = parameters.laps;
        cfg.raceCar.trackPreset = parameters.trackPreset;
        cfg.raceCar.Normalize();
    }
}
