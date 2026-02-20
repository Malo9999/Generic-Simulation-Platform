using UnityEngine;

public class SimDriver
{
    private readonly float tickDeltaTime;
    private ITickableSimulationRunner runner;
    private float accumulatedTime;
    private bool singleStepRequested;

    public SimDriver(float tickDeltaTime)
    {
        this.tickDeltaTime = Mathf.Max(0.0001f, tickDeltaTime);
    }

    public int CurrentTick { get; private set; }
    public bool IsPaused { get; private set; }
    public float TimeScale { get; private set; } = 1f;

    public void SetRunner(ISimulationRunner simulationRunner)
    {
        runner = simulationRunner as ITickableSimulationRunner;
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
        CurrentTick++;
    }
}
