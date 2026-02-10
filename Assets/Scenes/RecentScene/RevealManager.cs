using UnityEngine;
using UnityEngine.UI;

public class RevealManager : MonoBehaviour
{
    [Header("Door before reveal room (optional)")]
    public DoorManager revealDoor;

    [Header("Door Handle Interaction (optional)")]
    [Tooltip("If assigned, the door opens via handle click instead of proximity. Supports both sides of the door.")]
    public DoorHandleInteractable[] doorHandleInteractables;

    [Header("XR Rig / XR Origin")]
    public Transform xrOrigin;

    [Header("State Machine")]
    public ExperimentStateManager stateManager;

    [Header("Reveal UI (World Space Canvases)")]
    public GameObject failUI;      // disabled by default
    public GameObject successUI;   // disabled by default

    [Header("Behavior")]
    public bool triggerOncePerCondition = true;

    private bool _isActiveRevealTrigger = false;
    private bool _showSuccessThisCondition = false;
    private bool _triggeredThisCondition = false;

    private bool HasHandles => doorHandleInteractables != null && doorHandleInteractables.Length > 0;

    private void Start()
    {
        // Wire up handle callbacks
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.onHandleActivated.AddListener(OnDoorHandleActivated);
    }

    private void OnTriggerEnter(Collider other) => TryTrigger(other);
    private void OnTriggerStay(Collider other)  => TryTrigger(other);

    private void TryTrigger(Collider other)
    {
        if (!_isActiveRevealTrigger) return;
        if (triggerOncePerCondition && _triggeredThisCondition) return;
        if (!xrOrigin || !stateManager) return;

        if (other.transform != xrOrigin && !other.transform.IsChildOf(xrOrigin))
            return;

        // If door handles are assigned, don't auto-open the door on proximity.
        // The player must click the handle instead.
        if (HasHandles)
            return;

        PerformReveal();
    }

    /// <summary>
    /// Called by DoorHandleInteractable when the player clicks the handle.
    /// </summary>
    private void OnDoorHandleActivated()
    {
        if (!_isActiveRevealTrigger) return;
        if (triggerOncePerCondition && _triggeredThisCondition) return;
        if (!stateManager) return;

        PerformReveal();
    }

    private void PerformReveal()
    {
        _triggeredThisCondition = true;

        // Open reveal door
        if (revealDoor != null)
            revealDoor.Open();

        // Disarm all handles after use
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.SetArmed(false);

        // Show fail/success UI
        if (failUI) failUI.SetActive(!_showSuccessThisCondition);
        if (successUI) successUI.SetActive(_showSuccessThisCondition);

        // Middle isolation may have disabled Graphic.enabled; force it back on
        ForceEnableVisuals(_showSuccessThisCondition ? successUI : failUI);

        // Notify state machine
        stateManager.OnRevealReached();

        QuestEventOutlet.Send(_showSuccessThisCondition ? "reveal_success" : "reveal_fail");
        Debug.Log($"[RevealManager] Triggered at {name}. Success={_showSuccessThisCondition}");
    }

    /// <summary>
    /// Arms/disarms reveal logic for the current phase.
    /// IMPORTANT: In some phases (e.g., experiment end) you may want to disarm WITHOUT hiding UI.
    /// </summary>
    public void ConfigureForCondition(bool activeRevealTrigger, bool showSuccess, bool resetAndHideUI = true)
    {
        _isActiveRevealTrigger = activeRevealTrigger;
        _showSuccessThisCondition = showSuccess;

        if (resetAndHideUI)
        {
            ResetRevealRoom(closeDoor: false);
        }

        // Arm/disarm handles AFTER reset so reset doesn't undo arming
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.SetArmed(activeRevealTrigger);
    }

    public void ResetRevealRoom(bool closeDoor)
    {
        _triggeredThisCondition = false;

        if (failUI) failUI.SetActive(false);
        if (successUI) successUI.SetActive(false);

        if (closeDoor && revealDoor != null)
            revealDoor.SetOpenImmediate(false);

        // Disarm all handles when resetting
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.SetArmed(false);
    }

    private static void ForceEnableVisuals(GameObject root)
    {
        if (root == null) return;

        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].enabled = true;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = true;
    }
}
