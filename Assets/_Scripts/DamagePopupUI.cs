using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class DamagePopupUI : MonoBehaviour
{
    private PanelRenderer panelRenderer;
    private Label popupLabel;
    private Camera mainCamera;

    [Header("Settings")]
    [SerializeField] private float lifetime = 1.0f;
    [SerializeField] private float floatSpeed = 1.2f;

    private float timer = 0f;
    private Vector3 moveDirection;

    private string pendingText = "";
    private Color pendingColor = Color.white;
    private bool isDataSet = false;

    void Awake()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        mainCamera = Camera.main;
        moveDirection = new Vector3(Random.Range(-0.3f, 0.3f), floatSpeed, 0f);
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
        if (panelRenderer != null)
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        TryBindUI(rootElement);
    }

    public void Setup(int amount, bool isHeal)
    {
        if (isHeal)
        {
            pendingText = $"+{amount}";
            pendingColor = new Color(0.2f, 0.9f, 0.3f, 1f); // Green
        }
        else
        {
            pendingText = amount.ToString();
            pendingColor = new Color(1f, 0.25f, 0.25f, 1f); // Red
        }

        isDataSet = true;

        // Attempt immediate binding if root already exists
        if (panelRenderer != null && panelRenderer.visualTreeAsset != null)
        {
            // Try resolving immediately
            TryBindUI(null);
        }
    }

    private void TryBindUI(VisualElement root)
    {
        if (!isDataSet) return;

        if (popupLabel == null)
        {
            if (root != null)
            {
                popupLabel = root.Q<Label>("popup-text");
            }
        }

        if (popupLabel != null)
        {
            popupLabel.text = pendingText;
            popupLabel.style.color = pendingColor;
            popupLabel.style.opacity = 1f;
        }
    }

    void LateUpdate()
    {
        // Billboard toward camera
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }

        // Float upwards
        transform.position += moveDirection * Time.deltaTime;

        timer += Time.deltaTime;

        // If label hasn't been found yet, keep querying until PanelRenderer initializes
        if (popupLabel == null && isDataSet)
        {
            // PanelRenderer may have finished building
            var root = GetComponent<PanelRenderer>()?.visualTreeAsset?.CloneTree();
            // Just wait for OnUIReload
        }

        // Fade out
        if (timer > lifetime * 0.5f)
        {
            float fadeProgress = (timer - lifetime * 0.5f) / (lifetime * 0.5f);
            if (popupLabel != null)
            {
                popupLabel.style.opacity = Mathf.Clamp01(1f - fadeProgress);
            }
        }

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}