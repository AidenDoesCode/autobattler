using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class UnitHealthBarUI : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private VisualElement barFill;
    private Camera mainCamera;
    private bool uiReady = false;

    private float cachedPercentage = 100f;
    private bool cachedIsPlayerTeam = true;

    [Header("Colors")]
    [SerializeField] private Color allyColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color enemyColor = new Color(0.9f, 0.2f, 0.2f);

    void Awake()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        mainCamera = Camera.main;
    }

    void OnEnable()
    {
        if (panelRenderer != null)
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
    }

    void OnDisable()
    {
        uiReady = false;
        if (panelRenderer != null)
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        if (rootElement == null) return;

        barFill = rootElement.Q<VisualElement>("bar-fill");
        if (barFill != null)
        {
            uiReady = true;
            ApplyStyle();
        }
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }

    public void SetTeam(bool isPlayerTeam)
    {
        cachedIsPlayerTeam = isPlayerTeam;
        if (uiReady) ApplyStyle();
    }

    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        if (maxHealth > 0)
        {
            cachedPercentage = Mathf.Clamp01((float)currentHealth / maxHealth) * 100f;
        }
        else
        {
            cachedPercentage = 0f;
        }

        if (uiReady)
        {
            ApplyStyle();
        }
    }

    private void ApplyStyle()
    {
        if (barFill == null) return;

        barFill.style.backgroundColor = cachedIsPlayerTeam ? allyColor : enemyColor;
        barFill.style.width = Length.Percent(cachedPercentage);
    }
}