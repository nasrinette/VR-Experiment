using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class DoorHandleInteractable : MonoBehaviour
{
    [Header("Hover-to-Open")]
    [Tooltip("Seconds the user must hover over the handle to activate it.")]
    public float hoverDuration = 2f;

    [Header("Visual Feedback")]
    [Tooltip("Color applied to the handle when armed (interactable).")]
    public Color armedColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Tooltip("Color the handle lerps towards as hover progress fills.")]
    public Color activatingColor = new Color(0.6f, 1f, 0.6f, 1f);

    [Tooltip("Pulse speed when armed but not hovered (0 = no pulse).")]
    public float pulseSpeed = 2f;

    [Header("References")]
    [Tooltip("Renderer to apply visual feedback to. Auto-detected if not set.")]
    public Renderer handleRenderer;

    [Header("Events")]
    public UnityEvent onHandleActivated;

    private XRSimpleInteractable _interactable;
    private Material _instanceMaterial;
    private Color _originalColor;
    private bool _isArmed;
    private bool _isHovered;
    private float _hoverTimer;
    private bool _activated;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();

        if (handleRenderer == null)
            handleRenderer = GetComponent<Renderer>();

        if (handleRenderer != null)
        {
            _instanceMaterial = handleRenderer.material;
            _originalColor = _instanceMaterial.GetColor(BaseColor);
        }

        _interactable.hoverEntered.AddListener(OnHoverEnter);
        _interactable.hoverExited.AddListener(OnHoverExit);

        SetArmed(false);
    }

    void Update()
    {
        if (!_isArmed || _instanceMaterial == null) return;

        if (_isHovered && !_activated)
        {
            // Count up while hovering
            _hoverTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_hoverTimer / hoverDuration);

            // Lerp color from armed → activating as progress fills
            _instanceMaterial.SetColor(BaseColor, Color.Lerp(armedColor, activatingColor, progress));

            if (progress >= 1f)
            {
                _activated = true;
                QuestEventOutlet.Send($"door_handle_activated:{name}");
                Debug.Log($"[DoorHandle] Activated by hover: {name}");
                onHandleActivated?.Invoke();
            }
        }
        else if (!_isHovered && !_activated)
        {
            // Pulse when armed but not hovered
            if (pulseSpeed > 0f)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                _instanceMaterial.SetColor(BaseColor, Color.Lerp(_originalColor, armedColor, t));
            }
        }
    }

    public void SetArmed(bool armed)
    {
        _isArmed = armed;
        _isHovered = false;
        _hoverTimer = 0f;
        _activated = false;

        if (_interactable != null)
            _interactable.enabled = armed;

        if (_instanceMaterial == null) return;

        if (armed)
        {
            _instanceMaterial.SetColor(BaseColor, armedColor);
            Debug.Log($"[DoorHandle] Armed: {name}");
        }
        else
        {
            _instanceMaterial.SetColor(BaseColor, _originalColor);
        }
    }

    public bool IsArmed => _isArmed;

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        if (!_isArmed || _activated) return;
        _isHovered = true;
        _hoverTimer = 0f;
        Debug.Log($"[DoorHandle] Hover start: {name}");
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        if (!_isArmed) return;
        _isHovered = false;
        _hoverTimer = 0f;

        // Reset color back to armed pulse
        if (_instanceMaterial != null && !_activated)
            _instanceMaterial.SetColor(BaseColor, armedColor);
    }

    void OnDestroy()
    {
        if (_instanceMaterial != null)
        {
            _instanceMaterial.SetColor(BaseColor, _originalColor);
            Destroy(_instanceMaterial);
        }

        if (_interactable != null)
        {
            _interactable.hoverEntered.RemoveListener(OnHoverEnter);
            _interactable.hoverExited.RemoveListener(OnHoverExit);
        }
    }
}
