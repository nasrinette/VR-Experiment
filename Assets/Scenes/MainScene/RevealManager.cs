using UnityEngine;
using UnityEngine.UI;

public class RevealManager : MonoBehaviour
{
    [Header("Door before reveal room (optional)")]
    public DoorManager revealDoor;

    [Header("Door Handle Interaction (optional)")]
    [Tooltip("If assigned, the door opens via handle click instead of proximity. Supports both sides of the door.")]
    public DoorHandleInteractable[] doorHandleInteractables;

    [Header("State Machine")]
    public ExperimentStateManager stateManager;

    [Header("Rating UI (shown each condition, re-enabled on reset)")]
    public GameObject ratingUI;    // disabled by default

    [Header("Reveal UI (World Space Canvases)")]
    public GameObject failUI;      // disabled by default
    public GameObject successUI;   // disabled by default

    [Header("Barrier Completion Gating")]
    [Tooltip("Assign all BarrierManagers (one per condition). Rating slider is hidden until the active one completes.")]
    public BarrierManager[] barrierManagers;

    [Header("Behavior")]
    public bool triggerOncePerCondition = true;

    private bool _isActiveRevealTrigger = false;
    private bool _showSuccessThisCondition = false;
    private bool _triggeredThisCondition = false;
    private bool _ratingDone = false;
    private RatingSliderUI _ratingSliderUI;

    private bool HasHandles => doorHandleInteractables != null && doorHandleInteractables.Length > 0;

    private bool HasBarrierManagers => barrierManagers != null && barrierManagers.Length > 0;

    private bool AnyBarrierManagerLastOpen()
    {
        if (!HasBarrierManagers) return true;
        foreach (var bm in barrierManagers)
            if (bm != null && bm.isActiveAndEnabled && bm.LastBarrierOpen)
                return true;
        return false;
    }

    private void Start()
    {
        // Wire up handle callbacks
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.onHandleActivated.AddListener(OnDoorHandleActivated);

        if (HasBarrierManagers)
            foreach (var bm in barrierManagers)
                if (bm != null)
                    bm.OnAllBarriersOpen += OnAllBarriersOpen;

        if (ratingUI != null)
        {
            _ratingSliderUI = ratingUI.GetComponentInChildren<RatingSliderUI>(true);
            if (_ratingSliderUI != null)
                _ratingSliderUI.onRatingDone += OnRatingDone;
        }
    }

    private void OnDestroy()
    {
        if (HasBarrierManagers)
            foreach (var bm in barrierManagers)
                if (bm != null)
                    bm.OnAllBarriersOpen -= OnAllBarriersOpen;

        if (_ratingSliderUI != null)
            _ratingSliderUI.onRatingDone -= OnRatingDone;
    }

    /// <summary>
    /// Called by DoorHandleInteractable when the player clicks the handle.
    /// </summary>
    private void OnDoorHandleActivated()
    {
        if (!_isActiveRevealTrigger) return;
        if (!_ratingDone && HasBarrierManagers) return;
        if (triggerOncePerCondition && _triggeredThisCondition) return;
        if (!stateManager) return;

        PerformReveal();
    }

    private void OnAllBarriersOpen()
    {
        if (ratingUI != null)
            ratingUI.SetActive(true);

        QuestEventOutlet.Send("rating_slider_enabled");
        Debug.Log("[RevealManager] All barriers open – rating slider shown.");
    }

    private void OnRatingDone()
    {
        _ratingDone = true;

        // Arm handles now so the player can open the reveal door
        if (_isActiveRevealTrigger && HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.SetArmed(true);

        QuestEventOutlet.Send("rating_done");
        Debug.Log("[RevealManager] Rating submitted – door handle now armed.");
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

        // Handles stay disarmed until rating is submitted (OnRatingDone arms them).
        // If there is no rating UI at all, arm immediately so the door still works.
        if (HasHandles && activeRevealTrigger && ratingUI == null)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.SetArmed(true);
    }

    public void ResetRevealRoom(bool closeDoor)
    {
        _triggeredThisCondition = false;
        _ratingDone = false;

        // Hide rating UI until the active condition's barriers complete
        if (ratingUI)
        {
            bool show = !HasBarrierManagers || AnyBarrierManagerLastOpen();
            ratingUI.SetActive(show);
        }

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
