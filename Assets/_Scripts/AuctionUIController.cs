using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class AuctionUIController : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private VisualElement root;

    private VisualElement auctionRoot;
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

    private int localBid = 0;
    private bool isLocked = false;

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

        if (bidMinusBtn != null) bidMinusBtn.clicked += () => ModifyBid(-1);
        if (bidPlusBtn != null) bidPlusBtn.clicked += () => ModifyBid(1);
        if (bidPlus5Btn != null) bidPlus5Btn.clicked += () => ModifyBid(5);
        if (lockBidBtn != null) lockBidBtn.clicked += LockBid;

        // Hide auction on initial load
        if (auctionRoot != null) auctionRoot.style.display = DisplayStyle.None;
    }

    private void HandleAuctionStarted(UnitData prize, float duration)
    {
        isLocked = false;
        localBid = 0;

        if (auctionRoot != null) auctionRoot.style.display = DisplayStyle.Flex;
        if (biddingControls != null) biddingControls.style.display = DisplayStyle.Flex;
        if (revealSection != null) revealSection.style.display = DisplayStyle.None;

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