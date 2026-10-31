using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(PanelRenderer))]
public class UnitHealthBarUI : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private VisualElement root;
    private VisualElement barFill;
    private VisualElement popupContainer;
    private Label starBadgeLabel;
    
    // Overhead Item Elements
    private VisualElement itemsContainer;
    private VisualElement[] overheadItemSlots;

    private Camera mainCamera;
    private bool uiReady = false;

    private float cachedPercentage = 100f;
    private bool cachedIsPlayerTeam = true;
    private int cachedStarLevel = 1;
    private List<ItemData> cachedItems = new List<ItemData>();

    [Header("Colors")]
    [SerializeField] private Color allyColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color enemyColor = new Color(0.9f, 0.2f, 0.2f);

    [Header("Star Display Settings")]
    [Tooltip("If false, 1-star units hide their star badge to avoid clutter.")]
    public bool showOneStarBadge = false;

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

        ClearPopups();
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        if (rootElement == null) return;

        root = rootElement;
        barFill = rootElement.Q<VisualElement>("bar-fill");
        popupContainer = rootElement.Q<VisualElement>("popup-container");
        starBadgeLabel = rootElement.Q<Label>("star-badge-label");
        itemsContainer = rootElement.Q<VisualElement>("items-container");

        overheadItemSlots = new VisualElement[3];
        for (int i = 0; i < 3; i++)
        {
            overheadItemSlots[i] = rootElement.Q<VisualElement>($"overhead-item-{i}");
        }

        if (barFill != null)
        {
            uiReady = true;
            ApplyBar();
            ApplyStarBadge();
            ApplyItems();
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

    public void UpdateStarLevel(int starLevel)
    {
        cachedStarLevel = starLevel;
        if (uiReady) ApplyStarBadge();
    }

    public void UpdateEquippedItems(List<ItemData> items)
    {
        cachedItems.Clear();
        if (items != null)
        {
            cachedItems.AddRange(items);
        }

        if (uiReady) ApplyItems();
    }

    private void ApplyBar()
    {
        if (barFill == null) return;
        barFill.style.backgroundColor = cachedIsPlayerTeam ? allyColor : enemyColor;
        barFill.style.width = Length.Percent(cachedPercentage);
    }

    private void ApplyStarBadge()
    {
        if (starBadgeLabel == null && root != null)
        {
            starBadgeLabel = root.Q<Label>("star-badge-label");
        }

        if (starBadgeLabel == null) return;

        if (cachedStarLevel <= 1 && !showOneStarBadge)
        {
            starBadgeLabel.style.display = DisplayStyle.None;
            return;
        }

        starBadgeLabel.style.display = DisplayStyle.Flex;

        switch (cachedStarLevel)
        {
            case 2:
                starBadgeLabel.text = "★★";
                starBadgeLabel.style.fontSize = 14;
                starBadgeLabel.style.color = new StyleColor(new Color(0.85f, 0.90f, 0.95f)); // Silver
                break;

            case 3:
                starBadgeLabel.text = "★★★";
                starBadgeLabel.style.fontSize = 16;
                starBadgeLabel.style.color = new StyleColor(new Color(1f, 0.84f, 0.0f)); // Gold
                break;

            default:
                starBadgeLabel.text = "★";
                starBadgeLabel.style.fontSize = 12;
                starBadgeLabel.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.75f));
                break;
        }
    }

    private void ApplyItems()
    {
        if (itemsContainer == null && root != null)
        {
            itemsContainer = root.Q<VisualElement>("items-container");
        }

        if (overheadItemSlots == null && root != null)
        {
            overheadItemSlots = new VisualElement[3];
            for (int i = 0; i < 3; i++)
            {
                overheadItemSlots[i] = root.Q<VisualElement>($"overhead-item-{i}");
            }
        }

        if (itemsContainer == null || overheadItemSlots == null) return;

        if (cachedItems.Count == 0)
        {
            itemsContainer.style.display = DisplayStyle.None;
            return;
        }

        itemsContainer.style.display = DisplayStyle.Flex;

        for (int i = 0; i < 3; i++)
        {
            VisualElement slot = overheadItemSlots[i];
            if (slot == null) continue;

            slot.Clear();

            if (i < cachedItems.Count && cachedItems[i] != null)
            {
                ItemData it = cachedItems[i];
                slot.style.display = DisplayStyle.Flex;

                Label codeLabel = new Label(it.itemCode ?? it.itemName.Substring(0, Mathf.Min(2, it.itemName.Length)));
                codeLabel.pickingMode = PickingMode.Ignore;
                codeLabel.style.fontSize = 8;
                codeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                codeLabel.style.color = it.isCompletedItem ? new Color(1f, 0.84f, 0f) : Color.white;
                codeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                slot.Add(codeLabel);

                Color borderColor = it.isCompletedItem ? new Color(1f, 0.84f, 0f) : new Color(0.3f, 0.6f, 0.9f);
                slot.style.borderTopColor = borderColor;
                slot.style.borderBottomColor = borderColor;
                slot.style.borderLeftColor = borderColor;
                slot.style.borderRightColor = borderColor;
            }
            else
            {
                slot.style.display = DisplayStyle.None;
            }
        }
    }

    public void ShowCombatPopup(int amount, bool isHeal)
    {
        if (popupContainer == null && root != null)
        {
            popupContainer = root.Q<VisualElement>("popup-container");
        }

        if (popupContainer == null) return;

        Label label = new Label(isHeal ? $"+{amount}" : amount.ToString());
        label.style.position = Position.Absolute;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 20;
        label.style.color = isHeal ? new Color(0.2f, 0.9f, 0.3f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);
        label.style.width = 60;
        label.style.height = 30;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;

        float randomX = Random.Range(-15f, 15f);
        label.style.left = (120f - 60f) * 0.5f + randomX;
        label.style.bottom = 0f;

        popupContainer.Add(label);
        StartCoroutine(AnimatePopupRoutine(label));
    }

    public void ClearPopups()
    {
        if (popupContainer != null)
        {
            popupContainer.Clear();
        }
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