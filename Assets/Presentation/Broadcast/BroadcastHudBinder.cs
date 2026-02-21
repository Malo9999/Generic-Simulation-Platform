using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BroadcastHudBinder : MonoBehaviour
{
    [SerializeField] private float refreshInterval = 0.25f;

    private Bootstrapper bootstrapper;
    private TMP_Text tmpText;
    private Text legacyText;
    private readonly StringBuilder builder = new StringBuilder(96);
    private float nextRefreshTime;

    private void Awake()
    {
        bootstrapper = Object.FindFirstObjectByType<Bootstrapper>();
        tmpText = GetComponent<TMP_Text>();
        legacyText = tmpText == null ? GetComponent<Text>() : null;
    }

    private void OnEnable()
    {
        nextRefreshTime = 0f;
        RefreshNow();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshNow();
    }

    private void RefreshNow()
    {
        if (bootstrapper == null)
        {
            bootstrapper = Object.FindFirstObjectByType<Bootstrapper>();
        }

        if (bootstrapper == null)
        {
            return;
        }

        builder.Clear();
        builder.Append("Simulation: ").Append(bootstrapper.CurrentSimulationId).Append('\n');
        builder.Append("Seed: ").Append(bootstrapper.CurrentSeed).Append('\n');
        builder.Append("Tick: ").Append(bootstrapper.CurrentTick).Append('\n');
        builder.Append("FPS: ").Append(bootstrapper.CurrentFps.ToString("0.0"));

        var textValue = builder.ToString();
        if (tmpText != null)
        {
            tmpText.SetText(textValue);
            return;
        }

        if (legacyText != null)
        {
            legacyText.text = textValue;
        }
    }
}
