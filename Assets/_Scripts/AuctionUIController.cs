using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class AuctionUIController : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private VisualElement root;

    private VisualElement auctionRoot;
    private VisualElement prizeCard;
    private VisualElement biddingControls;
    private VisualElement revealSection;

    private Label timerLabel;
    private Label prizeNameLabel;
    private Label prizeArchetypeLabel;
    private Label playerBidLabel;
    private Label revealWinnerLabel;
    private Label revealDetailsLabel;

    private Button bidMinusBtn;
    private Button bidPlusBtn;
    private Button bidPlus5Btn;
    private Button lockBidBtn;

    // Auction Inspection Modal
    private VisualElement inspectModal;
    private Label inspectTitle;
    private Label inspectSubtitle;
    private Label inspectHp;
    private Label inspectAd;
    private Label inspectExtra;
    private Button inspectCloseBtn;

    private int localBid = 0;
    private bool isLocked = false;
    private UnitData currentPrizeUnit;

    void Awake()
    {
        panelRenderer = GetComponent<PanelRenderer>();
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
        if (AuctionManager.Instance != null)
        {
            AuctionManager.Instance.OnAuctionStarted += HandleAuctionStarted;
            AuctionManager.Instance.OnTimerTick += HandleTimerTick;
            AuctionManager.Instance.OnPlayerBidChanged += HandlePlayerBidChanged;
            AuctionManager.Instance.OnAuctionEnded += HandleAuctionEnded;
        }
    }

    private void UnsubscribeEvents()
    {
        if (AuctionManager.Instance != null)
        {
            AuctionManager.Instance.OnAuctionStarted -= HandleAuctionStarted;
            AuctionManager.Instance.OnTimerTick -= HandleTimerTick;
            AuctionManager.Instance.OnPlayerBidChanged -= HandlePlayerBidChanged;
            AuctionManager.Instance.OnAuctionEnded -= HandleAuctionEnded;
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        if (rootElement == null) return;
        root = rootElement;

        auctionRoot = root.Q<VisualElement>("auction-root");
        prizeCard = root.Q<VisualElement>("prize-card");
        biddingControls = root.Q<VisualElement>("bidding-controls");
        revealSection = root.Q<VisualElement>("reveal-section");

        timerLabel = root.Q<Label>("auction-timer-label");
        prizeNameLabel = root.Q<Label>("prize-name");
        prizeArchetypeLabel = root.Q<Label>("prize-archetype");
        playerBidLabel = root.Q<Label>("player-bid-label");
        revealWinnerLabel = root.Q<Label>("reveal-winner-label");
        revealDetailsLabel = root.Q<Label>("reveal-details-label");

        bidMinusBtn = root.Q<Button>("bid-minus-btn");
        bidPlusBtn = root.Q<Button>("bid-plus-btn");
        bidPlus5Btn = root.Q<Button>("bid-plus5-btn");
        lockBidBtn = root.Q<Button>("lock-bid-btn");

        // Inspection Modal Query
        inspectModal = root.Q<VisualElement>("auction-inspect-modal");
        inspectTitle = root.Q<Label>("auction-inspect-title");
        inspectSubtitle = root.Q<Label>("auction-inspect-subtitle");
        inspectHp = root.Q<Label>("auction-inspect-hp");
        inspectAd = root.Q<Label>("auction-inspect-ad");
        inspectExtra = root.Q<Label>("auction-inspect-extra");
        inspectCloseBtn = root.Q<Button>("auction-inspect-close-btn");

        if (bidMinusBtn != null) bidMinusBtn.clicked += () => ModifyBid(-1);
        if (bidPlusBtn != null) bidPlusBtn.clicked += () => ModifyBid(1);
        if (bidPlus5Btn != null) bidPlus5Btn.clicked += () => ModifyBid(5);
        if (lockBidBtn != null) lockBidBtn.clicked += LockBid;

        if (inspectCloseBtn != null)
        {
            inspectCloseBtn.clicked += CloseInspection;
        }

        // Left-Click or Right-Click on the prize card opens inspection modal
        if (prizeCard != null)
        {
            prizeCard.RegisterCallback<PointerDownEvent>(evt =>
            {
                ShowInspection();
                evt.StopPropagation();
            });
        }

        // Clicking outside the modal on the background closes it
        if (auctionRoot != null)
        {
            auctionRoot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (inspectModal != null && inspectModal.style.display == DisplayStyle.Flex)
                {
                    CloseInspection();
                }
            });
        }

        if (auctionRoot != null) auctionRoot.style.display = DisplayStyle.None;
        if (inspectModal != null) inspectModal.style.display = DisplayStyle.None;
    }

    private void ShowInspection()
    {
        if (inspectModal == null || currentPrizeUnit == null) return;

        if (inspectTitle != null) inspectTitle.text = currentPrizeUnit.unitName;
        if (inspectSubtitle != null) inspectSubtitle.text = $"★ • {currentPrizeUnit.archetype} ({currentPrizeUnit.goldCost}g Tier)";
        if (inspectHp != null) inspectHp.text = $"Base HP: {currentPrizeUnit.maxHealth}";
        if (inspectAd != null) inspectAd.text = $"Base Attack: {currentPrizeUnit.attackDamage}";
        if (inspectExtra != null) inspectExtra.text = $"Range: {currentPrizeUnit.attackRange:F1}  |  Speed: {currentPrizeUnit.moveSpeed:F1}";

        inspectModal.style.display = DisplayStyle.Flex;
    }

    private void CloseInspection()
    {
        if (inspectModal != null)
        {
            inspectModal.style.display = DisplayStyle.None;
        }
    }

    private void HandleAuctionStarted(UnitData prize, float duration)
    {
        isLocked = false;
        localBid = 0;
        currentPrizeUnit = prize;

        if (auctionRoot != null) auctionRoot.style.display = DisplayStyle.Flex;
        if (biddingControls != null) biddingControls.style.display = DisplayStyle.Flex;
        if (revealSection != null) revealSection.style.display = DisplayStyle.None;
        if (inspectModal != null) inspectModal.style.display = DisplayStyle.None;

        if (prizeNameLabel != null) prizeNameLabel.text = prize.unitName;
        if (prizeArchetypeLabel != null) prizeArchetypeLabel.text = $"{prize.archetype} | Tier {prize.goldCost}";
        if (playerBidLabel != null) playerBidLabel.text = "0g";
        if (lockBidBtn != null)
        {
            lockBidBtn.text = "Lock In Bid";
            lockBidBtn.SetEnabled(true);
        }
    }

    private void HandleTimerTick(float timeRemaining)
    {
        if (timerLabel != null)
        {
            timerLabel.text = $"Time Remaining: {Mathf.CeilToInt(timeRemaining)}s";
        }
    }

    private void HandlePlayerBidChanged(int newBid)
    {
        localBid = newBid;
        if (playerBidLabel != null)
        {
            playerBidLabel.text = $"{localBid}g";
        }
    }

    private void ModifyBid(int delta)
    {
        if (isLocked || AuctionManager.Instance == null) return;
        AuctionManager.Instance.SetPlayerBid(localBid + delta);
    }

    private void LockBid()
    {
        if (isLocked || AuctionManager.Instance == null) return;
        isLocked = true;
        AuctionManager.Instance.LockInPlayerBid();

        if (lockBidBtn != null)
        {
            lockBidBtn.text = "LOCKED IN";
            lockBidBtn.SetEnabled(false);
        }
    }

    private void HandleAuctionEnded(bool playerWon, string winnerName, int pBid, int aiBid)
    {
        CloseInspection();

        if (biddingControls != null) biddingControls.style.display = DisplayStyle.None;
        if (revealSection != null) revealSection.style.display = DisplayStyle.Flex;

        if (revealWinnerLabel != null)
        {
            revealWinnerLabel.text = playerWon ? "YOU WON THE AUCTION!" : $"{winnerName} Won!";
            revealWinnerLabel.style.color = playerWon ? new Color(0.2f, 0.9f, 0.3f) : new Color(1f, 0.3f, 0.3f);
        }

        if (revealDetailsLabel != null)
        {
            revealDetailsLabel.text = $"Your Secret Bid: {pBid}g  |  Opponent: {aiBid}g";
        }

        StartCoroutine(HideAuctionDelayed());
    }

    private System.Collections.IEnumerator HideAuctionDelayed()
    {
        yield return new WaitForSeconds(3f);
        if (auctionRoot != null)
        {
            auctionRoot.style.display = DisplayStyle.None;
        }
    }
}