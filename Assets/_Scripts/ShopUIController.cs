using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(PanelRenderer))]
public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }

    private PanelRenderer panelRenderer;
    private VisualElement root;

    private Label goldLabel;
    private Label xpLabel;
    private Label levelLabel;
    private Label boardCapLabel;
    private Button rerollButton;
    private Button buyXpButton;
    private Button lockShopButton;

    // Synergy UI Elements
    private VisualElement synergyBody;
    private Button toggleSynergyBtn;
    private VisualElement meleeBox;
    private Label meleeCountLabel;
    private VisualElement rangedBox;
    private Label rangedCountLabel;
    private VisualElement supportBox;
    private Label supportCountLabel;
    private bool isSynergyCollapsed = false;

    // Item Tray UI Elements
    private VisualElement[] itemSlotElements;
    private VisualElement dragGhost;
    private Label ghostLabel;

    // Inspection Card Elements
    private VisualElement inspectionModal;
    private Label inspectTitle;
    private Label inspectSubtitle;
    private Label inspectStatHp;
    private Label inspectStatAd;
    private Label inspectStatExtra;
    private Label inspectDescLabel;
    private VisualElement inspectItemsBox;
    private Label inspectItemsHeader;
    private VisualElement inspectItemsList;
    private Button inspectCloseBtn;

    // Shop Card Elements
    private VisualElement[] cardSlots;
    private Label[] nameLabels;
    private Label[] costLabels;
    private Label[] archetypeLabels;

    private BoardGridManager gridManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panelRenderer = GetComponent<PanelRenderer>();
    }

    void Start()
    {
        gridManager = Object.FindAnyObjectByType<BoardGridManager>();
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

        UpdateSynergyUI();
        UpdateItemTrayUI();
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

        if (SynergyManager.Instance != null)
        {
            SynergyManager.Instance.OnSynergiesUpdated -= UpdateSynergyUI;
            SynergyManager.Instance.OnSynergiesUpdated += UpdateSynergyUI;
        }

        if (ItemInventoryManager.Instance != null)
        {
            ItemInventoryManager.Instance.OnInventoryChanged -= UpdateItemTrayUI;
            ItemInventoryManager.Instance.OnInventoryChanged += UpdateItemTrayUI;
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

        if (SynergyManager.Instance != null)
        {
            SynergyManager.Instance.OnSynergiesUpdated -= UpdateSynergyUI;
        }

        if (ItemInventoryManager.Instance != null)
        {
            ItemInventoryManager.Instance.OnInventoryChanged -= UpdateItemTrayUI;
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

        // Direct per-frame cursor tracking mapped precisely to panel space
        if (dragGhost != null && dragGhost.style.display == DisplayStyle.Flex && Mouse.current != null && root != null && root.panel != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector2 screenCoords = new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y);
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenCoords);

            dragGhost.style.left = panelPos.x;
            dragGhost.style.top = panelPos.y;
        }

        // Right-click empty backdrop / ground closes inspection card
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !ItemInventoryManager.Instance.IsDraggingItem)
        {
            if (inspectionModal != null && inspectionModal.style.display == DisplayStyle.Flex)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector2 screenCoords = new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y);
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenCoords);

                if (!inspectionModal.worldBound.Contains(panelPos))
                {
                    CloseInspection();
                }
            }
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

        // Drag Ghost Visual
        dragGhost = root.Q<VisualElement>("dragged-item-ghost");
        ghostLabel = root.Q<Label>("ghost-label");

        // Inspection Modal Query
        inspectionModal = root.Q<VisualElement>("inspection-modal");
        inspectTitle = root.Q<Label>("inspect-title");
        inspectSubtitle = root.Q<Label>("inspect-subtitle");
        inspectStatHp = root.Q<Label>("inspect-stat-hp");
        inspectStatAd = root.Q<Label>("inspect-stat-ad");
        inspectStatExtra = root.Q<Label>("inspect-stat-extra");
        inspectDescLabel = root.Q<Label>("inspect-desc-label");
        inspectItemsBox = root.Q<VisualElement>("inspect-items-box");
        inspectItemsHeader = root.Q<Label>("inspect-items-header");
        inspectItemsList = root.Q<VisualElement>("inspect-items-list");
        inspectCloseBtn = root.Q<Button>("inspect-close-btn");

        if (inspectCloseBtn != null)
        {
            inspectCloseBtn.clicked -= CloseInspection;
            inspectCloseBtn.clicked += CloseInspection;
        }

        // Synergy Elements
        synergyBody = root.Q<VisualElement>("synergy-body");
        toggleSynergyBtn = root.Q<Button>("toggle-synergy-btn");
        meleeBox = root.Q<VisualElement>("melee-trait-box");
        meleeCountLabel = root.Q<Label>("melee-count-label");
        rangedBox = root.Q<VisualElement>("ranged-trait-box");
        rangedCountLabel = root.Q<Label>("ranged-count-label");
        supportBox = root.Q<VisualElement>("support-trait-box");
        supportCountLabel = root.Q<Label>("support-count-label");

        if (toggleSynergyBtn != null)
        {
            toggleSynergyBtn.clicked -= OnToggleSynergyClicked;
            toggleSynergyBtn.clicked += OnToggleSynergyClicked;
        }

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

        // Item Tray Elements (8 slots with right-click inspection and left-click drag)
        itemSlotElements = new VisualElement[8];
        for (int i = 0; i < 8; i++)
        {
            int slotIndex = i;
            VisualElement slot = root.Q<VisualElement>($"item-slot-{i}");
            itemSlotElements[i] = slot;

            if (slot != null)
            {
                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    // Right-Click: Inspect Item
                    if (evt.button == 1)
                    {
                        if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.inventorySlots[slotIndex] != null)
                        {
                            InspectItem(ItemInventoryManager.Instance.inventorySlots[slotIndex]);
                            evt.StopPropagation();
                            return;
                        }
                    }

                    // Left-Click: Start Drag
                    if (evt.button == 0 && ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.inventorySlots[slotIndex] != null)
                    {
                        ItemInventoryManager.Instance.StartDraggingItem(slotIndex);
                        slot.CapturePointer(evt.pointerId);

                        ItemData it = ItemInventoryManager.Instance.inventorySlots[slotIndex];
                        if (dragGhost != null && ghostLabel != null && it != null && root != null && root.panel != null)
                        {
                            ghostLabel.text = it.itemCode ?? it.itemName.Substring(0, Mathf.Min(2, it.itemName.Length));

                            Color ghostBorderColor = it.isCompletedItem ? new Color(1f, 0.84f, 0f) : new Color(0.3f, 0.6f, 0.9f);
                            SetBorderColor(dragGhost, ghostBorderColor);

                            if (Mouse.current != null)
                            {
                                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                                Vector2 screenCoords = new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y);
                                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenCoords);
                                dragGhost.style.left = panelPos.x;
                                dragGhost.style.top = panelPos.y;
                            }

                            dragGhost.style.display = DisplayStyle.Flex;
                        }

                        slot.style.opacity = 0.35f;
                        evt.StopPropagation();
                    }
                });

                slot.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (slot.HasPointerCapture(evt.pointerId))
                    {
                        slot.ReleasePointer(evt.pointerId);
                    }

                    if (dragGhost != null) dragGhost.style.display = DisplayStyle.None;
                    slot.style.opacity = 1f;

                    if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.IsDraggingItem)
                    {
                        Vector2 screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)evt.position;
                        ItemInventoryManager.Instance.FinishItemDrag(screenPos);
                        evt.StopPropagation();
                    }
                });
            }
        }

        // Shop Card Elements
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

                card.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1) // Right Click
                    {
                        if (ShopManager.Instance != null && ShopManager.Instance.currentShopUnits != null &&
                            slotIndex < ShopManager.Instance.currentShopUnits.Length &&
                            ShopManager.Instance.currentShopUnits[slotIndex] != null)
                        {
                            InspectUnitData(ShopManager.Instance.currentShopUnits[slotIndex]);
                            evt.StopPropagation();
                        }
                    }
                });
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

        UpdateSynergyUI();
        UpdateItemTrayUI();
    }

    // --- INSPECTION CARD LOGIC ---

    public void InspectUnit(Unit unit)
    {
        if (inspectionModal == null || unit == null || unit.data == null) return;

        SetBorderColor(inspectionModal, new Color(0.95f, 0.77f, 0.06f));
        inspectTitle.text = unit.data.unitName;

        string stars = unit.starLevel == 3 ? "★★★" : (unit.starLevel == 2 ? "★★" : "★");
        inspectSubtitle.text = $"{stars} • {unit.data.archetype} ({unit.data.goldCost}g)";
        inspectSubtitle.style.color = unit.starLevel >= 3 ? new Color(1f, 0.84f, 0f) : new Color(0.85f, 0.90f, 0.95f);

        inspectStatHp.text = $"HP: {unit.currentHealth} / {unit.GetEffectiveMaxHealth()}";
        inspectStatAd.text = $"Attack Damage: {unit.GetEffectiveAttackDamage()}";

        float bonusAS = unit.GetItemBonusAttackSpeed() * 100f;
        int flatDR = unit.GetItemFlatDamageReduction();
        inspectStatExtra.text = $"Bonus AS: +{bonusAS:F0}%   |   DR: -{flatDR}";

        inspectDescLabel.style.display = DisplayStyle.None;

        // Render equipped items
        inspectItemsBox.style.display = DisplayStyle.Flex;
        inspectItemsHeader.text = $"EQUIPPED ITEMS ({unit.equippedItems.Count}/{Unit.MaxItems})";
        inspectItemsList.Clear();

        if (unit.equippedItems.Count == 0)
        {
            Label noneLabel = new Label("No items equipped");
            noneLabel.style.fontSize = 13;
            noneLabel.style.color = new Color(0.6f, 0.65f, 0.7f);
            noneLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            noneLabel.style.marginTop = 3;
            noneLabel.style.whiteSpace = WhiteSpace.Normal;
            inspectItemsList.Add(noneLabel);
        }
        else
        {
            foreach (var item in unit.equippedItems)
            {
                if (item == null) continue;

                VisualElement itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.justifyContent = Justify.SpaceBetween;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.flexWrap = Wrap.Wrap;
                itemRow.style.marginBottom = 5;
                itemRow.style.paddingLeft = 8;
                itemRow.style.paddingRight = 8;
                itemRow.style.paddingTop = 5;
                itemRow.style.paddingBottom = 5;
                itemRow.style.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                SetBorderRadius(itemRow, 4);

                Label nameLbl = new Label(item.itemName);
                nameLbl.style.fontSize = 13;
                nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLbl.style.color = item.isCompletedItem ? new Color(1f, 0.84f, 0f) : Color.white;
                nameLbl.style.whiteSpace = WhiteSpace.Normal;
                nameLbl.style.marginRight = 6;
                itemRow.Add(nameLbl);

                string buffText = "";
                if (item.bonusAttackDamage > 0) buffText += $"+{item.bonusAttackDamage} AD ";
                if (item.bonusHealth > 0) buffText += $"+{item.bonusHealth} HP ";
                if (item.bonusAttackSpeed > 0f) buffText += $"+{item.bonusAttackSpeed * 100f:F0}% AS ";
                if (item.flatDamageReduction > 0) buffText += $"-{item.flatDamageReduction} DR";

                Label buffLbl = new Label(buffText.Trim());
                buffLbl.style.fontSize = 12;
                buffLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                buffLbl.style.color = new Color(0.3f, 0.85f, 0.95f);
                buffLbl.style.whiteSpace = WhiteSpace.Normal;
                itemRow.Add(buffLbl);

                inspectItemsList.Add(itemRow);
            }
        }

        inspectionModal.style.display = DisplayStyle.Flex;
    }

    public void InspectUnitData(UnitData data, ItemData attachedItem = null)
    {
        if (inspectionModal == null || data == null) return;

        SetBorderColor(inspectionModal, new Color(0.95f, 0.77f, 0.06f));
        inspectTitle.text = data.unitName;
        inspectSubtitle.text = $"★ • {data.archetype} ({data.goldCost}g Cost)";
        inspectSubtitle.style.color = new Color(0.85f, 0.90f, 0.95f);

        inspectStatHp.text = $"Base HP: {data.maxHealth}";
        inspectStatAd.text = $"Base Attack: {data.attackDamage}";
        inspectStatExtra.text = $"Range: {data.attackRange:F1}   |   Speed: {data.moveSpeed:F1}";

        inspectDescLabel.style.display = DisplayStyle.None;

        inspectItemsBox.style.display = DisplayStyle.Flex;
        inspectItemsList.Clear();

        if (attachedItem != null)
        {
            inspectItemsHeader.text = "ATTACHED ITEM";
            VisualElement itemRow = new VisualElement();
            itemRow.style.flexDirection = FlexDirection.Row;
            itemRow.style.justifyContent = Justify.SpaceBetween;
            itemRow.style.alignItems = Align.Center;
            itemRow.style.flexWrap = Wrap.Wrap;
            itemRow.style.paddingLeft = 8;
            itemRow.style.paddingRight = 8;
            itemRow.style.paddingTop = 5;
            itemRow.style.paddingBottom = 5;
            itemRow.style.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            SetBorderRadius(itemRow, 4);

            Label nameLbl = new Label(attachedItem.itemName);
            nameLbl.style.fontSize = 13;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.color = attachedItem.isCompletedItem ? new Color(1f, 0.84f, 0f) : Color.white;
            nameLbl.style.whiteSpace = WhiteSpace.Normal;
            nameLbl.style.marginRight = 6;
            itemRow.Add(nameLbl);

            string buffText = "";
            if (attachedItem.bonusAttackDamage > 0) buffText += $"+{attachedItem.bonusAttackDamage} AD ";
            if (attachedItem.bonusHealth > 0) buffText += $"+{attachedItem.bonusHealth} HP ";
            if (attachedItem.bonusAttackSpeed > 0f) buffText += $"+{attachedItem.bonusAttackSpeed * 100f:F0}% AS ";
            if (attachedItem.flatDamageReduction > 0) buffText += $"-{attachedItem.flatDamageReduction} DR";

            Label buffLbl = new Label(buffText.Trim());
            buffLbl.style.fontSize = 12;
            buffLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            buffLbl.style.color = new Color(0.3f, 0.85f, 0.95f);
            buffLbl.style.whiteSpace = WhiteSpace.Normal;
            itemRow.Add(buffLbl);

            inspectItemsList.Add(itemRow);
        }
        else
        {
            inspectItemsHeader.text = "SHOP OFFER";
            Label noneLabel = new Label("Equip items once purchased");
            noneLabel.style.fontSize = 13;
            noneLabel.style.color = new Color(0.6f, 0.65f, 0.7f);
            noneLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            noneLabel.style.marginTop = 3;
            noneLabel.style.whiteSpace = WhiteSpace.Normal;
            inspectItemsList.Add(noneLabel);
        }

        inspectionModal.style.display = DisplayStyle.Flex;
    }

    public void InspectItem(ItemData item)
    {
        if (inspectionModal == null || item == null) return;

        Color itemBorderColor = item.isCompletedItem ? new Color(1f, 0.84f, 0f) : new Color(0.3f, 0.6f, 0.9f);
        SetBorderColor(inspectionModal, itemBorderColor);

        inspectTitle.text = item.itemName;
        inspectSubtitle.text = item.isCompletedItem ? "Completed Combined Item" : $"{item.componentType} Component";
        inspectSubtitle.style.color = item.isCompletedItem ? new Color(1f, 0.84f, 0f) : new Color(0.3f, 0.6f, 0.9f);

        inspectStatHp.text = item.bonusHealth > 0 ? $"+{item.bonusHealth} Max Health" : "";
        inspectStatHp.style.display = item.bonusHealth > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        inspectStatHp.style.whiteSpace = WhiteSpace.Normal;

        inspectStatAd.text = item.bonusAttackDamage > 0 ? $"+{item.bonusAttackDamage} Attack Damage" : "";
        inspectStatAd.style.display = item.bonusAttackDamage > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        inspectStatAd.style.whiteSpace = WhiteSpace.Normal;

        string extra = "";
        if (item.bonusAttackSpeed > 0f) extra += $"+{item.bonusAttackSpeed * 100f:F0}% Attack Speed   ";
        if (item.flatDamageReduction > 0) extra += $"-{item.flatDamageReduction} Damage Taken";
        inspectStatExtra.text = extra.Trim();
        inspectStatExtra.style.display = string.IsNullOrEmpty(extra) ? DisplayStyle.None : DisplayStyle.Flex;
        inspectStatExtra.style.whiteSpace = WhiteSpace.Normal;

        if (!string.IsNullOrEmpty(item.description))
        {
            inspectDescLabel.text = item.description;
            inspectDescLabel.style.display = DisplayStyle.Flex;
            inspectDescLabel.style.whiteSpace = WhiteSpace.Normal;
        }
        else
        {
            inspectDescLabel.style.display = DisplayStyle.None;
        }

        inspectItemsBox.style.display = DisplayStyle.None;
        inspectionModal.style.display = DisplayStyle.Flex;
    }

    public void CloseInspection()
    {
        if (inspectionModal != null)
        {
            inspectionModal.style.display = DisplayStyle.None;
        }
    }

    // --- UI Toolkit Style Helpers ---

    private void SetBorderColor(VisualElement element, Color color)
    {
        if (element == null) return;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
    }

    private void SetBorderRadius(VisualElement element, float radius)
    {
        if (element == null) return;
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }

    private void OnToggleSynergyClicked()
    {
        if (synergyBody == null) return;
        isSynergyCollapsed = !isSynergyCollapsed;
        synergyBody.style.display = isSynergyCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
        if (toggleSynergyBtn != null) toggleSynergyBtn.text = isSynergyCollapsed ? "▲" : "▼";
    }

    private void UpdateSynergyUI()
    {
        if (SynergyManager.Instance == null) return;

        int melee = SynergyManager.Instance.meleeCount;
        int meleeTier = SynergyManager.Instance.meleeTier;
        if (meleeCountLabel != null) meleeCountLabel.text = $"{melee} / {(meleeTier >= 1 ? 4 : 2)}";
        SetTraitVisual(meleeBox, meleeTier);

        int ranged = SynergyManager.Instance.rangedCount;
        int rangedTier = SynergyManager.Instance.rangedTier;
        if (rangedCountLabel != null) rangedCountLabel.text = $"{ranged} / {(rangedTier >= 1 ? 4 : 2)}";
        SetTraitVisual(rangedBox, rangedTier);

        int support = SynergyManager.Instance.supportCount;
        int supportTier = SynergyManager.Instance.supportTier;
        if (supportCountLabel != null) supportCountLabel.text = $"{support} / 2";
        SetTraitVisual(supportBox, supportTier);
    }

    private void SetTraitVisual(VisualElement box, int tier)
    {
        if (box == null) return;

        if (tier == 0)
        {
            box.style.borderLeftColor = new StyleColor(new Color(0.45f, 0.45f, 0.45f));
            box.style.backgroundColor = new StyleColor(new Color(0.15f, 0.17f, 0.20f, 0.6f));
        }
        else if (tier == 1)
        {
            box.style.borderLeftColor = new StyleColor(new Color(0.85f, 0.55f, 0.15f));
            box.style.backgroundColor = new StyleColor(new Color(0.25f, 0.20f, 0.10f, 0.8f));
        }
        else
        {
            box.style.borderLeftColor = new StyleColor(new Color(0.95f, 0.80f, 0.20f));
            box.style.backgroundColor = new StyleColor(new Color(0.35f, 0.30f, 0.05f, 0.85f));
        }
    }

    private void UpdateItemTrayUI()
    {
        if (ItemInventoryManager.Instance == null || itemSlotElements == null) return;

        for (int i = 0; i < itemSlotElements.Length; i++)
        {
            VisualElement slot = itemSlotElements[i];
            if (slot == null) continue;

            slot.Clear();
            slot.style.opacity = 1f;
            ItemData it = ItemInventoryManager.Instance.inventorySlots[i];

            if (it != null)
            {
                Label label = new Label(it.itemCode ?? it.itemName.Substring(0, Mathf.Min(2, it.itemName.Length)));
                label.pickingMode = PickingMode.Ignore;
                label.style.fontSize = 11;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = it.isCompletedItem ? new Color(1f, 0.84f, 0f) : Color.white;
                slot.Add(label);

                Color borderCol = it.isCompletedItem ? new Color(1f, 0.84f, 0f) : new Color(0.3f, 0.6f, 0.9f);
                SetBorderColor(slot, borderCol);
            }
            else
            {
                Color defaultBorder = new Color(0.3f, 0.35f, 0.4f);
                SetBorderColor(slot, defaultBorder);
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
            lockShopButton.style.backgroundColor = new StyleColor(new Color(0.85f, 0.65f, 0.13f));
            SetBorderColor(lockShopButton, new Color(1f, 0.84f, 0f));
            lockShopButton.style.color = new StyleColor(Color.black);
        }
        else
        {
            lockShopButton.text = "Lock";
            lockShopButton.style.backgroundColor = new StyleColor(new Color(0.18f, 0.20f, 0.24f));
            SetBorderColor(lockShopButton, new Color(0.27f, 0.31f, 0.37f));
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