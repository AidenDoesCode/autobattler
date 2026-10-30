using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

[RequireComponent(typeof(PanelRenderer))]
public class UnitHealthBarUI : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private VisualElement root;
    private VisualElement barFill;
    private VisualElement popupContainer;
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
        
        // Destroy stuck labels when the unit turns off
        ClearPopups(); 
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        if (rootElement == null) return;

        root = rootElement;
        barFill = rootElement.Q<VisualElement>("bar-fill");
        popupContainer = rootElement.Q<VisualElement>("popup-container");

        if (barFill != null)
        {
            uiReady = true;
            ApplyBar();
        }
    }

    public void ClearPopups()
    {
        if (popupContainer != null)
        {
            popupContainer.Clear();
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
        if (uiReady) ApplyBar();
    }

    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        cachedPercentage = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) * 100f : 0f;
        if (uiReady) ApplyBar();
    }

    private void ApplyBar()
    {
        if (barFill == null) return;
        barFill.style.backgroundColor = cachedIsPlayerTeam ? allyColor : enemyColor;
        barFill.style.width = Length.Percent(cachedPercentage);
    }

    public void ShowCombatPopup(int amount, bool isHeal)
    {
        if (popupContainer == null && root != null)
        {
            popupContainer = root.Q<VisualElement>("popup-container");
        }

        if (popupContainer == null)
        {
            Debug.LogWarning("[UnitHealthBarUI] Cannot show popup: 'popup-container' not found in UXML!");
            return;
        }

        Label label = new Label(isHeal ? $"+{amount}" : amount.ToString());
        label.style.position = Position.Absolute;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 22;
        label.style.color = isHeal ? new Color(0.2f, 0.9f, 0.3f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);

        // Explicit width, height, and alignment so World Space doesn't collapse it
        label.style.width = 60;
        label.style.height = 30;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;

        // Position at the bottom-center of popup-container with slight random horizontal drift
        float randomX = Random.Range(-15f, 15f);
        label.style.left = (120f - 60f) * 0.5f + randomX; // Centered inside 120px container
        label.style.bottom = 0f;

        popupContainer.Add(label);
        StartCoroutine(AnimatePopupRoutine(label));
    }

    private IEnumerator AnimatePopupRoutine(Label label)
    {
        float duration = 0.8f;
        float elapsed = 0f;
        float startBottom = 0f;
        float endBottom = 40f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            label.style.bottom = Mathf.Lerp(startBottom, endBottom, t);

            if (t > 0.4f)
            {
                label.style.opacity = Mathf.Clamp01(1f - ((t - 0.4f) / 0.6f));
            }

            yield return null;
        }

        if (popupContainer != null && popupContainer.Contains(label))
        {
            popupContainer.Remove(label);
        }
    }
}