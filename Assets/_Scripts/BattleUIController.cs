using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

[RequireComponent(typeof(PanelRenderer))]
public class BattleUIController : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private Button startBattleButton;
    private GameManager gameManager;
    private bool uiReady = false;

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer != null)
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
    }

    private void Start()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();
        StartCoroutine(InitializeButtonRoutine());
    }

    private void OnDisable()
    {
        uiReady = false;
        if (panelRenderer != null)
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        if (startBattleButton != null)
        {
            startBattleButton.clicked -= OnStartBattleClicked;
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        if (rootElement == null) return;

        startBattleButton = rootElement.Q<Button>("StartBattleButton");
        if (startBattleButton != null)
        {
            ConnectButton();
        }
    }

    private IEnumerator InitializeButtonRoutine()
    {
        yield return null;

        if (uiReady) yield break;

        UIDocument uiDocument = Object.FindAnyObjectByType<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null) yield break;

        startBattleButton = uiDocument.rootVisualElement.Q<Button>("StartBattleButton");
        if (startBattleButton != null)
        {
            ConnectButton();
        }
    }

    private void ConnectButton()
    {
        if (startBattleButton == null) return;

        startBattleButton.clicked -= OnStartBattleClicked;
        startBattleButton.clicked += OnStartBattleClicked;
        uiReady = true;
    }

    private void OnStartBattleClicked()
    {
        Debug.Log("Button was clicked!");

        if (gameManager == null)
        {
            Debug.LogError("GameManager is missing from the scene!");
            return;
        }

        if (gameManager.currentState == GameManager.GameState.Planning)
        {
            gameManager.StartBattle();
            if (startBattleButton != null)
            {
                startBattleButton.text = "End Battle";
            }
        }
        else
        {
            gameManager.ReturnToPlanning();
            if (startBattleButton != null)
            {
                startBattleButton.text = "Start Battle";
            }
        }
    }

    public void ResetButtonText()
    {
        if (startBattleButton != null)
        {
            startBattleButton.text = "Start Battle";
        }
    }
}