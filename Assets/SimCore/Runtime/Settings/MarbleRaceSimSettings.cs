using System;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Sim Settings/MarbleRace", fileName = "MarbleRaceSimSettings")]
public class MarbleRaceSimSettings : SimSettingsBase
{
    [Serializable]
    public struct Params
    {
        public int marbleCount;
        public int laps;
        public string trackPreset;
    }

    public Params parameters = new Params
    {
        marbleCount = 12,
        laps = 3,
        trackPreset = "Auto"
    };

    private void OnEnable() => simulationId = "MarbleRace";

    public override void ApplyTo(ScenarioConfig cfg)
    {
        if (cfg == null)
        {
            return;
        }

        cfg.marbleRace ??= new MarbleRaceConfig();
        cfg.marbleRace.marbleCount = parameters.marbleCount;
        cfg.marbleRace.laps = parameters.laps;
        cfg.marbleRace.trackPreset = parameters.trackPreset;
        cfg.marbleRace.Normalize();
    }
}
