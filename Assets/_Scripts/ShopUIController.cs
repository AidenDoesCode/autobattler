using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class ShopUIController : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private VisualElement root;

    private Label goldLabel;
    private Label xpLabel;
    private Label levelLabel;
    private Label boardCapLabel;
    private Button rerollButton;
    private Button buyXpButton;
    private Button lockShopButton;

    private VisualElement[] cardSlots;
    private Label[] nameLabels;
    private Label[] costLabels;
    private Label[] archetypeLabels;

    private BoardGridManager gridManager;

    void Awake()
    {
        panelRenderer = GetComponent<PanelRenderer>();
    }

    void Start()
    {
        gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        SubscribeEvents();

        if (EconomyManager.Instance != null)
        {
            UpdateGoldUI(EconomyManager.Instance.currentGold);
            UpdateXPUI(EconomyManager.Instance.currentXP, EconomyManager.Instance.GetRequiredXPForCurrentLevel(), EconomyManager.Instance.currentLevel);
        }

        if (ShopManager.Instance != null)
        {
            UpdateLockUI(ShopManager.Instance.isLocked);
        }
    }

    void OnEnable()
    {
        if (panelRenderer != null)
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        SubscribeEvents();
    }

    void OnDisable()
    {
        if (panelRenderer != null)
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnGoldChanged -= UpdateGoldUI;
            EconomyManager.Instance.OnGoldChanged += UpdateGoldUI;

            EconomyManager.Instance.OnXPChanged -= UpdateXPUI;
            EconomyManager.Instance.OnXPChanged += UpdateXPUI;
        }

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnShopGenerated -= PopulateShopCards;
            ShopManager.Instance.OnShopGenerated += PopulateShopCards;

            ShopManager.Instance.OnUnitPurchased -= ClearCardSlot;
            ShopManager.Instance.OnUnitPurchased += ClearCardSlot;

            ShopManager.Instance.OnLockStateChanged -= UpdateLockUI;
            ShopManager.Instance.OnLockStateChanged += UpdateLockUI;
        }
    }

    private void UnsubscribeEvents()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnGoldChanged -= UpdateGoldUI;
            EconomyManager.Instance.OnXPChanged -= UpdateXPUI;
        }

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnShopGenerated -= PopulateShopCards;
            ShopManager.Instance.OnUnitPurchased -= ClearCardSlot;
            ShopManager.Instance.OnLockStateChanged -= UpdateLockUI;
        }
    }

    void Update()
    {
        if (gridManager != null && boardCapLabel != null)
        {
            int current = gridManager.GetCurrentBoardUnitCount();
            int max = gridManager.maxBoardUnits;
            boardCapLabel.text = $"Units: {current} / {max}";
            boardCapLabel.style.color = current > max ? new Color(1f, 0.3f, 0.3f) : Color.white;
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        if (rootElement == null) return;
        root = rootElement;

        goldLabel = root.Q<Label>("gold-label");
        xpLabel = root.Q<Label>("xp-label");
        levelLabel = root.Q<Label>("level-label");
        boardCapLabel = root.Q<Label>("board-cap-label");

        rerollButton = root.Q<Button>("reroll-btn");
        buyXpButton = root.Q<Button>("buy-xp-btn");
        lockShopButton = root.Q<Button>("lock-shop-btn");

        if (rerollButton != null)
        {
            rerollButton.clicked -= OnRerollClicked;
            rerollButton.clicked += OnRerollClicked;
        }

        if (buyXpButton != null)
        {
            buyXpButton.clicked -= OnBuyXPClicked;
            buyXpButton.clicked += OnBuyXPClicked;
        }

        if (lockShopButton != null)
        {
            lockShopButton.clicked -= OnLockShopClicked;
            lockShopButton.clicked += OnLockShopClicked;
        }

        // Query shop card elements (5 slots)
        int size = ShopManager.Instance != null ? ShopManager.Instance.shopSize : 5;
        cardSlots = new VisualElement[size];
        nameLabels = new Label[size];
        costLabels = new Label[size];
        archetypeLabels = new Label[size];

        for (int i = 0; i < size; i++)
        {
            int slotIndex = i;
            VisualElement card = root.Q<VisualElement>($"card-slot-{i}");
            cardSlots[i] = card;

            if (card != null)
            {
                nameLabels[i] = card.Q<Label>("unit-name");
                costLabels[i] = card.Q<Label>("unit-cost");
                archetypeLabels[i] = card.Q<Label>("unit-archetype");

                Button buyBtn = card.Q<Button>("buy-card-btn");
                if (buyBtn != null)
                {
                    buyBtn.clicked += () => OnBuyCardClicked(slotIndex);
                }
            }
        }

        SubscribeEvents();

        if (EconomyManager.Instance != null)
        {
            UpdateGoldUI(EconomyManager.Instance.currentGold);
            UpdateXPUI(EconomyManager.Instance.currentXP, EconomyManager.Instance.GetRequiredXPForCurrentLevel(), EconomyManager.Instance.currentLevel);
        }

        if (ShopManager.Instance != null)
        {
            UpdateLockUI(ShopManager.Instance.isLocked);

            if (ShopManager.Instance.currentShopUnits != null)
            {
                PopulateShopCards(ShopManager.Instance.currentShopUnits);
            }
        }
    }

    private void OnRerollClicked()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.RerollShop(isFree: false);
        }
    }

    private void OnBuyXPClicked()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.BuyXP();
        }
    }

    private void OnLockShopClicked()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ToggleLock();
        }
    }

    private void UpdateLockUI(bool isLocked)
    {
        if (lockShopButton == null) return;

        if (isLocked)
        {
            lockShopButton.text = "Locked";
            lockShopButton.style.backgroundColor = new StyleColor(new Color(0.85f, 0.65f, 0.13f)); // Gold/Amber
            lockShopButton.style.borderTopColor = new StyleColor(new Color(1f, 0.84f, 0f));
            lockShopButton.style.borderBottomColor = new StyleColor(new Color(1f, 0.84f, 0f));
            lockShopButton.style.borderLeftColor = new StyleColor(new Color(1f, 0.84f, 0f));
            lockShopButton.style.borderRightColor = new StyleColor(new Color(1f, 0.84f, 0f));
            lockShopButton.style.color = new StyleColor(Color.black);
        }
        else
        {
            lockShopButton.text = "Lock";
            lockShopButton.style.backgroundColor = new StyleColor(new Color(0.18f, 0.20f, 0.24f)); // Neutral dark slate
            lockShopButton.style.borderTopColor = new StyleColor(new Color(0.27f, 0.31f, 0.37f));
            lockShopButton.style.borderBottomColor = new StyleColor(new Color(0.27f, 0.31f, 0.37f));
            lockShopButton.style.borderLeftColor = new StyleColor(new Color(0.27f, 0.31f, 0.37f));
            lockShopButton.style.borderRightColor = new StyleColor(new Color(0.27f, 0.31f, 0.37f));
            lockShopButton.style.color = new StyleColor(new Color(0.93f, 0.94f, 0.95f));
        }
    }

    private void OnBuyCardClicked(int slotIndex)
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.BuyUnit(slotIndex);
        }
    }

    private void UpdateGoldUI(int gold)
    {
        if (goldLabel != null)
        {
            goldLabel.text = $"{gold}g";
        }
    }

    private void UpdateXPUI(int currentXP, int maxXP, int level)
    {
        if (xpLabel != null)
        {
            xpLabel.text = (EconomyManager.Instance != null && level >= EconomyManager.Instance.maxLevel)
                ? "MAX"
                : $"{currentXP} / {maxXP} XP";
        }

        if (levelLabel != null)
        {
            levelLabel.text = $"Lvl {level}";
        }

        if (buyXpButton != null)
        {
            if (EconomyManager.Instance != null && level >= EconomyManager.Instance.maxLevel)
            {
                buyXpButton.text = "Max Level";
                buyXpButton.SetEnabled(false);
            }
            else
            {
                buyXpButton.text = "Buy XP (4g)";
                buyXpButton.SetEnabled(true);
            }
        }
    }

    private void PopulateShopCards(UnitData[] cards)
    {
        if (cards == null || cardSlots == null) return;

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (i >= cards.Length || cards[i] == null)
            {
                ClearCardSlot(i);
                continue;
            }

            UnitData u = cards[i];
            if (cardSlots[i] != null) cardSlots[i].style.visibility = Visibility.Visible;
            if (nameLabels[i] != null) nameLabels[i].text = u.unitName;
            if (costLabels[i] != null) costLabels[i].text = $"{u.goldCost}g";
            if (archetypeLabels[i] != null) archetypeLabels[i].text = u.archetype.ToString();
        }
    }

    private void ClearCardSlot(int slotIndex)
    {
        if (cardSlots != null && slotIndex >= 0 && slotIndex < cardSlots.Length && cardSlots[slotIndex] != null)
        {
            cardSlots[slotIndex].style.visibility = Visibility.Hidden;
        }
    }
}