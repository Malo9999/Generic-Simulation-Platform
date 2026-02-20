using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public interface IReplayableSimulationRunner : ISimulationRunner
{
    void ApplyReplaySnapshot(int tick, object state);
    void ApplyReplayEvent(int tick, string eventType, object payload);
}

public sealed class ReplayDriver
{
    private readonly float tickDeltaTime;
    private readonly Dictionary<int, object> snapshotsByTick = new();
    private readonly Dictionary<int, List<ReplayEvent>> eventsByTick = new();

    private ITickableSimulationRunner tickableRunner;
    private IReplayableSimulationRunner replayableRunner;
    private float accumulatedTime;
    private bool singleStepRequested;
    private bool rendererOnly;

    public ReplayDriver(float tickDeltaTime)
    {
        this.tickDeltaTime = Mathf.Max(0.0001f, tickDeltaTime);
    }

    public int CurrentTick { get; private set; }
    public bool IsPaused { get; private set; }
    public float TimeScale { get; private set; } = 1f;
    public bool IsLoaded { get; private set; }

    public bool Load(ScenarioConfig config)
    {
        ResetPlaybackState();

        var replayFolder = config?.replay?.runFolder;
        if (string.IsNullOrWhiteSpace(replayFolder))
        {
            Debug.LogWarning("ReplayDriver: replay.runFolder is empty. Replay disabled.");
            return false;
        }

        rendererOnly = config.replay.rendererOnly;

        var snapshotsPath = Path.Combine(replayFolder, "snapshots.jsonl");
        var eventsPath = Path.Combine(replayFolder, "events.jsonl");

        ReadSnapshots(snapshotsPath, snapshotsByTick);
        ReadEvents(eventsPath, eventsByTick);

        if (snapshotsByTick.Count == 0 && eventsByTick.Count == 0)
        {
            Debug.LogWarning($"ReplayDriver: no replay data found in '{replayFolder}'.");
            return false;
        }

        IsLoaded = true;
        Debug.Log($"ReplayDriver: loaded {snapshotsByTick.Count} snapshots and {eventsByTick.Count} event ticks from '{replayFolder}'.");
        return true;
    }

    public void SetRunner(ISimulationRunner simulationRunner)
    {
        tickableRunner = simulationRunner as ITickableSimulationRunner;
        replayableRunner = simulationRunner as IReplayableSimulationRunner;
        accumulatedTime = 0f;
        CurrentTick = 0;
        singleStepRequested = false;
    }

    public bool ValidateRunnerForLoadedConfig(string simulationId)
    {
        if (!IsLoaded)
        {
            return false;
        }

        var hasReplayable = replayableRunner != null;
        var hasTickable = tickableRunner != null;
        var valid = rendererOnly ? hasReplayable : hasReplayable || hasTickable;
        if (valid)
        {
            return true;
        }

        var requiredContract = rendererOnly
            ? "IReplayableSimulationRunner"
            : "IReplayableSimulationRunner or ITickableSimulationRunner";

        Debug.LogError(
            $"ReplayDriver: Invalid runner contract for simulation '{simulationId}'. " +
            $"replay.rendererOnly={rendererOnly} requires {requiredContract}, " +
            $"but found replayable={hasReplayable}, tickable={hasTickable}. Replay disabled.");

        IsLoaded = false;
        return false;
    }

    public void Pause() => IsPaused = true;

    public void Resume()
    {
        IsPaused = false;
        singleStepRequested = false;
    }

    public void RequestSingleStep()
    {
        IsPaused = true;
        singleStepRequested = true;
    }

    public void SetTimeScale(float newTimeScale)
    {
        TimeScale = Mathf.Max(0f, newTimeScale);
    }

    public void Advance(float frameDeltaTime)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (singleStepRequested)
        {
            RunTick();
            singleStepRequested = false;
            return;
        }

        if (IsPaused || TimeScale <= 0f)
        {
            return;
        }

        accumulatedTime += Mathf.Max(0f, frameDeltaTime) * TimeScale;
        while (accumulatedTime >= tickDeltaTime)
        {
            accumulatedTime -= tickDeltaTime;
            RunTick();
        }
    }

    private void RunTick()
    {
        if (snapshotsByTick.TryGetValue(CurrentTick, out var state))
        {
            replayableRunner?.ApplyReplaySnapshot(CurrentTick, state);
        }

        if (eventsByTick.TryGetValue(CurrentTick, out var replayEvents) && replayableRunner != null)
        {
            for (var i = 0; i < replayEvents.Count; i++)
            {
                replayableRunner.ApplyReplayEvent(CurrentTick, replayEvents[i].eventType, replayEvents[i].payload);
            }
        }

        if (!rendererOnly && replayableRunner == null)
        {
            tickableRunner?.Tick(CurrentTick, tickDeltaTime);
        }

        CurrentTick++;
    }

    private void ResetPlaybackState()
    {
        snapshotsByTick.Clear();
        eventsByTick.Clear();
        CurrentTick = 0;
        IsPaused = false;
        TimeScale = 1f;
        accumulatedTime = 0f;
        singleStepRequested = false;
        IsLoaded = false;
        rendererOnly = false;
    }

    private static void ReadSnapshots(string path, Dictionary<int, object> destination)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parsed = JsonConvert.DeserializeObject<SnapshotLine>(line);
            if (parsed == null)
            {
                continue;
            }

            destination[parsed.tick] = parsed.state;
        }
    }

    private static void ReadEvents(string path, Dictionary<int, List<ReplayEvent>> destination)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parsed = JsonConvert.DeserializeObject<ReplayEventLine>(line);
            if (parsed == null)
            {
                continue;
            }

            if (!destination.TryGetValue(parsed.tick, out var eventsForTick))
            {
                eventsForTick = new List<ReplayEvent>();
                destination[parsed.tick] = eventsForTick;
            }

            eventsForTick.Add(new ReplayEvent
            {
                eventType = parsed.eventType,
                payload = parsed.payload
            });
        }
    }

    private sealed class SnapshotLine
    {
        public int tick;
        public JToken state;
    }

    private sealed class ReplayEventLine
    {
        public int tick;
        public string eventType;
        public JToken payload;
    }

    private sealed class ReplayEvent
    {
        public string eventType;
        public JToken payload;
    }
}
