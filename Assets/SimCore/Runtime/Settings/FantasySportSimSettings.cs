using System;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Sim Settings/FantasySport", fileName = "FantasySportSimSettings")]
public class FantasySportSimSettings : SimSettingsBase
{
    [Serializable]
    public struct Params
    {
        public int teamCount;
        public int playersPerTeam;
        public float periodLength;
    }

    public Params parameters = new Params
    {
        teamCount = 2,
        playersPerTeam = 8,
        periodLength = 180f
    };

    private void OnEnable() => simulationId = "FantasySport";

    public override void ApplyTo(ScenarioConfig cfg)
    {
        if (cfg == null)
        {
            return;
        }

        cfg.fantasySport ??= new FantasySportConfig();
        cfg.fantasySport.teamCount = parameters.teamCount;
        cfg.fantasySport.playersPerTeam = parameters.playersPerTeam;
        cfg.fantasySport.periodLength = parameters.periodLength;
        cfg.fantasySport.Normalize();
    }
}
