using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(120)]
public class MarbleRacePanelBinder : MonoBehaviour
{

    private const string MarbleRaceSimId = "MarbleRace";
    private const string PanelKey = "MarbleRace";

    [SerializeField, Range(4f, 10f)] private float refreshHz = 6f;

    private readonly StringBuilder leaderboardBuilder = new(512);

    private SimPanelHost simPanelHost;
    private Bootstrapper bootstrapper;
    private MarbleRaceRunner runner;

    private Transform panelRoot;
    private Text statusText;
    private Text leaderboardText;
    private Text winnerText;
    private Button startButton;
    private Button restartButton;

    private float nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBinderInstance()
    {
        if (UnityEngine.Object.FindFirstObjectByType<MarbleRacePanelBinder>() != null)
        {
            return;
        }

        var binderObject = new GameObject(nameof(MarbleRacePanelBinder));
        binderObject.AddComponent<MarbleRacePanelBinder>();
        UnityEngine.Object.DontDestroyOnLoad(binderObject);
    }

    private void OnEnable()
    {
        nextRefreshTime = 0f;
        TickUi(true);
    }

    private void Update()
    {
        TickUi(false);
    }

    private void TickUi(bool immediate)
    {
        if (!immediate && Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + 1f / Mathf.Clamp(refreshHz, 4f, 10f);

        EnsureReferences();
        if (simPanelHost == null)
        {
            return;
        }

        var activeSimId = ResolveActiveSimId();
        if (!string.Equals(activeSimId, MarbleRaceSimId, StringComparison.OrdinalIgnoreCase))
        {
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(false);
            }

            return;
        }

        EnsurePanel();
        simPanelHost.ShowOnly(PanelKey);

        if (runner == null)
        {
            runner = UnityEngine.Object.FindFirstObjectByType<MarbleRaceRunner>()
                ?? UnityEngine.Object.FindAnyObjectByType<MarbleRaceRunner>();
        }

        if (runner == null)
        {
            statusText.text = "Status: Waiting for MarbleRaceRunner";
            leaderboardText.text = string.Empty;
            winnerText.text = string.Empty;
            startButton.gameObject.SetActive(false);
            restartButton.gameObject.SetActive(true);
            return;
        }

        statusText.text = $"Status: {runner.CurrentPhase}";

        var final = runner.CurrentPhase == MarbleRaceRunner.RacePhase.Finished || runner.CurrentPhase == MarbleRaceRunner.RacePhase.Cooldown;
        runner.FillLeaderboard(leaderboardBuilder, 12, final);
        leaderboardText.text = leaderboardBuilder.ToString();
        winnerText.text = final ? runner.GetWinnerLine() : string.Empty;

        startButton.gameObject.SetActive(runner.CurrentPhase == MarbleRaceRunner.RacePhase.Ready);
        restartButton.gameObject.SetActive(final);
    }

    private void EnsureReferences()
    {
        if (simPanelHost == null)
        {
            simPanelHost = UnityEngine.Object.FindFirstObjectByType<SimPanelHost>()
                ?? UnityEngine.Object.FindAnyObjectByType<SimPanelHost>();
        }

        if (bootstrapper == null)
        {
            bootstrapper = UnityEngine.Object.FindFirstObjectByType<Bootstrapper>()
                ?? UnityEngine.Object.FindAnyObjectByType<Bootstrapper>();
        }
    }

    private string ResolveActiveSimId()
    {
        if (bootstrapper != null)
        {
            return bootstrapper.CurrentSimulationId;
        }

        return simPanelHost != null ? simPanelHost.ActiveSimId : string.Empty;
    }

    private void EnsurePanel()
    {
        panelRoot = simPanelHost.EnsurePanel(PanelKey, CreatePanel);
    }

    private Transform CreatePanel(Transform parent)
    {
        var panel = new GameObject("MarbleRacePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(0f, 280f);

        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = false;

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleText(panel.transform, "Leaderboard", 20, FontStyle.Bold);
        statusText = titleText(panel.transform, "Status: Ready", 15, FontStyle.Normal);
        leaderboardText = titleText(panel.transform, string.Empty, 14, FontStyle.Normal);
        leaderboardText.alignment = TextAnchor.UpperLeft;

        winnerText = titleText(panel.transform, string.Empty, 14, FontStyle.Bold);

        startButton = CreateButton(panel.transform, "Start", () =>
        {
            if (runner != null)
            {
                runner.StartRace();
            }
        });

        restartButton = CreateButton(panel.transform, "Restart", () =>
        {
            if (bootstrapper != null)
            {
                bootstrapper.ResetSimulation();
            }
        });

        return panel.transform;
    }

    private static Text titleText(Transform parent, string text, int size, FontStyle style)
    {
        var go = new GameObject($"Text_{text}", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.raycastTarget = false;

        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = size + 8f;
        layout.preferredHeight = size + 8f;

        return t;
    }

    private static Button CreateButton(Transform parent, string label, Action onClick)
    {
        var root = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);

        var image = root.GetComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

        var button = root.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick?.Invoke());

        var buttonLayout = root.GetComponent<LayoutElement>();
        buttonLayout.minHeight = 34f;
        buttonLayout.preferredHeight = 34f;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(root.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelGo.GetComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.raycastTarget = false;

        return button;
    }
}
