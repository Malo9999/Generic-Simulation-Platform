using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public class SimDriver
{
    private readonly float tickDeltaTime;
    private ITickableSimulationRunner runner;
    private float accumulatedTime;
    private bool singleStepRequested;

    private RecordingSession recordingSession;

    public SimDriver(float tickDeltaTime)
    {
        this.tickDeltaTime = Mathf.Max(0.0001f, tickDeltaTime);
    }

    public int CurrentTick { get; private set; }
    public bool IsPaused { get; private set; }
    public float TimeScale { get; private set; } = 1f;

    public void ConfigureRecording(ScenarioConfig config, string runFolderPath, EventBus eventBus)
    {
        recordingSession = RecordingSession.Create(config, runFolderPath, eventBus);
    }

    public void SetRunner(ITickableSimulationRunner tickableRunner)
    {
        runner = tickableRunner;
        accumulatedTime = 0f;
        CurrentTick = 0;
        singleStepRequested = false;
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
        if (runner == null)
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
        runner?.Tick(CurrentTick, tickDeltaTime);
        recordingSession?.RecordTick(CurrentTick, runner);
        CurrentTick++;
    }

    private sealed class RecordingSession
    {
        private readonly int snapshotEveryNTicks;
        private readonly bool eventsEnabled;
        private readonly string snapshotPath;
        private readonly string eventsPath;
        private readonly EventBus eventBus;

        private readonly List<SimulationEvent> pendingEvents = new();

        private RecordingSession(int snapshotEveryNTicks, bool eventsEnabled, string snapshotPath, string eventsPath, EventBus eventBus)
        {
            this.snapshotEveryNTicks = snapshotEveryNTicks;
            this.eventsEnabled = eventsEnabled;
            this.snapshotPath = snapshotPath;
            this.eventsPath = eventsPath;
            this.eventBus = eventBus;
        }

        public static RecordingSession Create(ScenarioConfig config, string runFolderPath, EventBus eventBus)
        {
            if (config?.recording == null || !config.recording.enabled || string.IsNullOrWhiteSpace(runFolderPath))
            {
                return null;
            }

            Directory.CreateDirectory(runFolderPath);

            var snapshotPath = Path.Combine(runFolderPath, "snapshots.jsonl");
            var eventsPath = Path.Combine(runFolderPath, "events.jsonl");

            File.WriteAllText(snapshotPath, string.Empty);
            if (config.recording.eventsEnabled)
            {
                File.WriteAllText(eventsPath, string.Empty);
            }

            return new RecordingSession(
                Mathf.Max(1, config.recording.snapshotEveryNTicks),
                config.recording.eventsEnabled,
                snapshotPath,
                eventsPath,
                eventBus);
        }

        public void RecordTick(int tick, ITickableSimulationRunner tickableRunner)
        {
            if (tickableRunner is IRecordable recordable && tick % snapshotEveryNTicks == 0)
            {
                var snapshotLine = JsonConvert.SerializeObject(new TickSnapshot
                {
                    tick = tick,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    state = recordable.CaptureState()
                });
                AppendLine(snapshotPath, snapshotLine);
            }

            if (!eventsEnabled || eventBus == null)
            {
                return;
            }

            pendingEvents.Clear();
            eventBus.DrainTo(pendingEvents);
            for (var i = 0; i < pendingEvents.Count; i++)
            {
                var eventLine = JsonConvert.SerializeObject(new EventLine
                {
                    tick = tick,
                    timestamp = pendingEvents[i].timestamp,
                    eventType = pendingEvents[i].eventType,
                    payload = pendingEvents[i].payload
                });
                AppendLine(eventsPath, eventLine);
            }
        }

        private static void AppendLine(string path, string line)
        {
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }

        private sealed class TickSnapshot
        {
            public int tick;
            public string timestamp;
            public object state;
        }

        private sealed class EventLine
        {
            public int tick;
            public string timestamp;
            public string eventType;
            public object payload;
        }
    }
}

public sealed class EventBus
{
    private readonly Queue<SimulationEvent> queue = new();

    public void Publish(string eventType, object payload = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return;
        }

        queue.Enqueue(new SimulationEvent
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            eventType = eventType,
            payload = payload
        });
    }

    public void DrainTo(List<SimulationEvent> destination)
    {
        if (destination == null)
        {
            return;
        }

        while (queue.Count > 0)
        {
            destination.Add(queue.Dequeue());
        }
    }
}

public static class EventBusService
{
    public static EventBus Global { get; private set; } = new();

    public static void ResetGlobal()
    {
        Global = new EventBus();
    }
}

public sealed class SimulationEvent
{
    public string timestamp;
    public string eventType;
    public object payload;
}
