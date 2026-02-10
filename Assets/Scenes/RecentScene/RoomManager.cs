using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI; // for Graphic (UI + TMP UGUI visuals)

public class RoomManager : MonoBehaviour
{
    [Header("Door to lock")]
    public DoorManager doorManager;

    [Header("Player collider (recommended)")]
    [Tooltip("Assign the XR Origin's CharacterController object collider (or a dedicated PlayerBody collider).")]
    public Collider playerBodyCollider; // best: a single collider, not hands

    [Header("Timing")]
    public float closeDelaySeconds = 0.2f;
    public float lockSeconds = 5f;

    [Header("Timer UI (always visible)")]
    public TMP_Text timerText;

    [Header("Behaviour")]
    public bool openDoorWhenDone = true;
    public bool resetDoorOnDisable = true;

    // -----------------------------
    // Glass room additions (optional)
    // -----------------------------
    [Header("Glass Room (Optional)")]
    [Tooltip("If enabled, this room behaves as a 'glass room': shows the glass room object and hides everything else visually.")]
    public bool isGlassRoom = false;

    [Tooltip("Disabled initially. Will be enabled when player enters this room trigger, then disabled again when timer ends.")]
    public GameObject glassRoomObject;

    [Tooltip("Optional. Assign XR Rig / XR Origin root to guarantee it stays visible. If not assigned, we use playerBodyCollider.transform.root.")]
    public Transform xrRigRoot;

    [Tooltip("If true, hides lights too (except lights under the preserved transforms).")]
    public bool hideOtherLights = false;

    [Header("Glass Room Skybox (Optional)")]
    [Tooltip("Skybox material to apply while the glass room is active.")]
    public Material roomSkybox;

    [Tooltip("Skybox material to restore when the glass room ends. If null, will restore whatever skybox was present at Start().")]
    public Material defaultSkybox;

    private Collider _triggerCollider;
    private bool _running;

    // Visual hide state caches (so we can restore exactly)
    private readonly Dictionary<Renderer, bool> _rendererStates = new Dictionary<Renderer, bool>(1024);
    private readonly Dictionary<Graphic, bool> _graphicStates = new Dictionary<Graphic, bool>(512);
    private readonly Dictionary<Light, bool> _lightStates = new Dictionary<Light, bool>(128);

    // Skybox state cache
    private Material _startupSkybox;
    private bool _skyboxOverridden;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider == null || !_triggerCollider.isTrigger)
            Debug.LogWarning("[RoomManager] This object should have a Collider set as Is Trigger.", this);
    }

    private void Start()
    {
        SetTimerText(0f); // show 00:00 initially

        // Cache the skybox that exists when the scene starts
        _startupSkybox = RenderSettings.skybox;

        // Ensure glass room starts disabled
        if (isGlassRoom && glassRoomObject != null)
            glassRoomObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_running) return;
        if (!doorManager || !timerText) return;
        if (!IsPlayerBody(other)) return;

        StartCoroutine(LockCycle());
    }

    private void OnTriggerExit(Collider other)
    {
        // Rearm only after the player body leaves the trigger volume
        if (!IsPlayerBody(other)) return;

        if (_triggerCollider != null)
            _triggerCollider.enabled = true;
    }

    private bool IsPlayerBody(Collider other)
    {
        // Strict match: only the collider you assigned can trigger the room lock
        return playerBodyCollider != null && other == playerBodyCollider;
    }

    private IEnumerator LockCycle()
    {
        _running = true;

        QuestEventOutlet.Send($"room_enter:{name}");

        // Prevent re-trigger while the player is still overlapping the slab
        if (_triggerCollider != null)
            _triggerCollider.enabled = false;

        // Start countdown immediately
        float remaining = lockSeconds;
        SetTimerText(remaining);

        QuestEventOutlet.Send($"timer_start:{name}:{lockSeconds}s");

        // GLASS ROOM: enable glass + hide everything else visually + set skybox
        if (isGlassRoom)
        {
            QuestEventOutlet.Send($"glass_room_on:{name}");
            if (glassRoomObject != null)
                glassRoomObject.SetActive(true);

            HideSceneVisualsExceptThisRoom();
            ApplyGlassRoomSkybox();
        }

        // Close behind user after a short delay (so it closes behind them, not onto them)
        if (closeDelaySeconds > 0f)
            yield return new WaitForSeconds(closeDelaySeconds);

        doorManager.Close();

        // Update once per frame
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (remaining < 0f) remaining = 0f;
            SetTimerText(remaining);
            yield return null;
        }

        // Freeze at 00:00
        SetTimerText(0f);
        QuestEventOutlet.Send($"timer_end:{name}");

        // GLASS ROOM: restore everything + disable glass room again + restore skybox
        if (isGlassRoom)
        {
            QuestEventOutlet.Send($"glass_room_off:{name}");
            RestoreSceneVisuals();
            RestoreDefaultSkybox();

            if (glassRoomObject != null)
                glassRoomObject.SetActive(false);
        }

        if (openDoorWhenDone)
            doorManager.Open();

        // Keep trigger disabled until the player exits (OnTriggerExit will re-enable it)
        _running = false;
    }

    private void SetTimerText(float secondsRemaining)
    {
        if (!timerText) return;

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    // -------------------------------------------------
    // Glass room visual hiding logic (no script stopping)
    // -------------------------------------------------
    private void HideSceneVisualsExceptThisRoom()
    {
        _rendererStates.Clear();
        _graphicStates.Clear();
        _lightStates.Clear();

        // Preserve ONLY:
        // - XR rig (whole rig subtree)
        // - This room's door subtree (doorManager.transform)
        // - This room's timer subtree (timerText.transform)
        // - Glass room subtree (glassRoomObject.transform)
        //
        // IMPORTANT: do NOT preserve .root for door/timer, or you'll keep other doors/timers
        // that share a common parent/root.
        var preserved = new List<Transform>(8);

        Transform rigRoot = xrRigRoot != null
            ? xrRigRoot
            : (playerBodyCollider != null ? playerBodyCollider.transform.root : null);
        if (rigRoot != null) preserved.Add(rigRoot);

        if (doorManager != null) preserved.Add(doorManager.transform);
        if (timerText != null) preserved.Add(timerText.transform);
        if (glassRoomObject != null) preserved.Add(glassRoomObject.transform);

        // Hide all 3D visuals except preserved subtrees
        var allRenderers = FindObjectsOfType<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            var r = allRenderers[i];
            if (r == null) continue;

            if (IsUnderAnyRoot(r.transform, preserved))
                continue;

            _rendererStates[r] = r.enabled;
            r.enabled = false;
        }

        // Hide all UI visuals except preserved subtrees
        // This will hide other room timers even if they share the same Canvas.
        var allGraphics = FindObjectsOfType<Graphic>(true);
        for (int i = 0; i < allGraphics.Length; i++)
        {
            var g = allGraphics[i];
            if (g == null) continue;

            if (IsUnderAnyRoot(g.transform, preserved))
                continue;

            _graphicStates[g] = g.enabled;
            g.enabled = false;
        }

        // Optionally hide lights too (purely visual)
        if (hideOtherLights)
        {
            var allLights = FindObjectsOfType<Light>(true);
            for (int i = 0; i < allLights.Length; i++)
            {
                var l = allLights[i];
                if (l == null) continue;

                if (IsUnderAnyRoot(l.transform, preserved))
                    continue;

                _lightStates[l] = l.enabled;
                l.enabled = false;
            }
        }
    }

    private void RestoreSceneVisuals()
    {
        foreach (var kvp in _rendererStates)
        {
            if (kvp.Key != null)
                kvp.Key.enabled = kvp.Value;
        }

        foreach (var kvp in _graphicStates)
        {
            if (kvp.Key != null)
                kvp.Key.enabled = kvp.Value;
        }

        foreach (var kvp in _lightStates)
        {
            if (kvp.Key != null)
                kvp.Key.enabled = kvp.Value;
        }

        _rendererStates.Clear();
        _graphicStates.Clear();
        _lightStates.Clear();
    }

    private static bool IsUnderAnyRoot(Transform t, List<Transform> roots)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            // True if t is root or a descendant of root
            if (t == root || t.IsChildOf(root))
                return true;
        }
        return false;
    }

    // -----------------------------
    // Skybox handling (glass rooms only)
    // -----------------------------
    private void ApplyGlassRoomSkybox()
    {
        if (roomSkybox == null) return;

        RenderSettings.skybox = roomSkybox;
        _skyboxOverridden = true;

        // If you use skybox-based ambient lighting/reflections, update the environment.
        DynamicGI.UpdateEnvironment();
    }

    private void RestoreDefaultSkybox()
    {
        if (!_skyboxOverridden) return;

        // Prefer explicit defaultSkybox; otherwise fall back to what the scene had at Start().
        RenderSettings.skybox = defaultSkybox != null ? defaultSkybox : _startupSkybox;
        _skyboxOverridden = false;

        DynamicGI.UpdateEnvironment();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _running = false;

        SetTimerText(0f);

        // Ensure trigger is usable when re-enabled
        if (_triggerCollider != null)
            _triggerCollider.enabled = true;

        // If disabled mid-glass-room, restore visibility + skybox
        if (isGlassRoom)
        {
            RestoreSceneVisuals();
            RestoreDefaultSkybox();

            if (glassRoomObject != null)
                glassRoomObject.SetActive(false);
        }

        if (resetDoorOnDisable && doorManager != null)
            doorManager.SetOpenImmediate(true);
    }
}
