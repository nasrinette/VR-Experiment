using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExperimentStateManager : MonoBehaviour
{
    private enum Phase { Condition1, Middle, Condition2 }

    [Header("Condition roots (required)")]
    public GameObject condition1Root;
    public GameObject condition2Root;

    [Header("Reveal (required)")]
    public RevealManager revealTrigger;

    [Header("Start room reset (recommended)")]
    public BoxChoiceManager boxChoiceManager;

    [Header("Return plane (Middle -> Condition2)")]
    public StayOnPlaneToAdvance returnPlaneTrigger; // plane GO disabled by default

    [Header("Timing (unused – middle now starts on button click)")]
    [HideInInspector] public float revealDisplaySeconds = 20f;

    [Header("Force-enable visuals at Condition 2 start (optional)")]
    public GameObject[] forceEnableOnCondition2Start;

    [HideInInspector] public bool swapConditions = false;

    // -----------------------------
    // Middle: visual isolation + collider isolation + skybox
    // -----------------------------
    [Header("Middle: Preserve Roots (required)")]
    public Transform xrRigRoot;
    public Transform[] middlePreservedRoots;

    [Header("Middle: Collider Handling")]
    public bool disableNonPreservedColliders = true;
    public Transform[] middleColliderPreservedRoots; // put FLOOR roots here
    public bool hideOtherLights = false;

    [Header("Middle: Skybox")]
    public Material middleSkybox;
    public Material defaultSkybox;

    private readonly Dictionary<Renderer, bool> _rendererStates = new(1024);
    private readonly Dictionary<Graphic, bool> _graphicStates = new(512);
    private readonly Dictionary<Light, bool> _lightStates = new(128);
    private readonly Dictionary<Collider, bool> _colliderStates = new(1024);

    private Material _startupSkybox;
    private bool _middleActive;

    private Phase _phase = Phase.Condition1;
    private bool _advancing;
    private bool _ended;

    private void Start()
    {
        // If SequenceManager flagged a swap, exchange condition roots
        if (swapConditions)
        {
            (condition1Root, condition2Root) = (condition2Root, condition1Root);
        }

        _startupSkybox = RenderSettings.skybox;

        SetReturnPlaneActive(false);

        if (revealTrigger != null)
            revealTrigger.ResetRevealRoom(closeDoor: true);

        EnterCondition1();
    }

    // Called by RevealManager
    public void OnRevealReached()
    {
        if (_ended || _advancing) return;

        if (_phase == Phase.Condition1)
        {
            // Don't auto-advance; wait for user to click "Continue" on Fail UI
            _advancing = true;
            QuestEventOutlet.Send("condition1_reveal_waiting");
            Debug.Log("[ExperimentStateManager] Condition 1 reveal shown. Waiting for user to press Continue.");
        }
        else if (_phase == Phase.Condition2)
        {
            EndAfterCondition2KeepSuccessUI();
        }
    }

    /// <summary>
    /// Called by the Continue button on the Fail UI to start the washout (middle) period.
    /// </summary>
    public void OnFailContinueClicked()
    {
        if (_phase != Phase.Condition1 || !_advancing) return;

        // Reset reveal room (close door + hide fail UI)
        if (revealTrigger != null)
            revealTrigger.ResetRevealRoom(closeDoor: true);

        EnterMiddle();
        _advancing = false;
    }

    /// <summary>
    /// Called by the End button on the Success UI to quit the application.
    /// </summary>
    public void OnEndClicked()
    {
        QuestEventOutlet.Send("app_quit");
        Debug.Log("[ExperimentStateManager] User clicked End. Quitting application.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Called by StayOnPlaneToAdvance after hold duration
    public void OnReturnPlaneHeld()
    {
        if (_ended || _advancing) return;
        if (_phase != Phase.Middle) return;

        EnterCondition2();
    }

    private void EnterCondition1()
    {
        _phase = Phase.Condition1;

        if (condition1Root) condition1Root.SetActive(true);
        if (condition2Root) condition2Root.SetActive(false);

        ExitMiddleMode();
        SetReturnPlaneActive(false);

        if (boxChoiceManager != null)
            boxChoiceManager.ResetChoice(closeDoorImmediately: true);

        if (revealTrigger != null)
        {
            revealTrigger.ResetRevealRoom(closeDoor: true);
            revealTrigger.ConfigureForCondition(activeRevealTrigger: true, showSuccess: false, resetAndHideUI: true);
        }

        int firstLabel = swapConditions ? 2 : 1;
        QuestEventOutlet.Send($"condition{firstLabel}_start");
        Debug.Log($"[ExperimentStateManager] Condition {firstLabel} active (first).");
    }

    private void EnterMiddle()
    {
        _phase = Phase.Middle;

        if (condition1Root) condition1Root.SetActive(false);
        if (condition2Root) condition2Root.SetActive(false);

        if (revealTrigger != null)
            revealTrigger.ConfigureForCondition(activeRevealTrigger: false, showSuccess: false, resetAndHideUI: true);

        SetReturnPlaneActive(true);
        EnterMiddleMode();

        QuestEventOutlet.Send("middle_start");
        Debug.Log("[ExperimentStateManager] Middle active.");
    }

    private void EnterCondition2()
    {
        _phase = Phase.Condition2;

        ExitMiddleMode();
        SetReturnPlaneActive(false);

        if (condition1Root) condition1Root.SetActive(false);
        if (condition2Root) condition2Root.SetActive(true);

        if (boxChoiceManager != null)
            boxChoiceManager.ResetChoice(closeDoorImmediately: true);

        ForceEnableVisuals(forceEnableOnCondition2Start);

        if (revealTrigger != null)
        {
            revealTrigger.ResetRevealRoom(closeDoor: true);
            revealTrigger.ConfigureForCondition(activeRevealTrigger: true, showSuccess: true, resetAndHideUI: true);
        }

        int secondLabel = swapConditions ? 1 : 2;
        QuestEventOutlet.Send($"condition{secondLabel}_start");
        Debug.Log($"[ExperimentStateManager] Condition {secondLabel} active (second).");
    }

    /// <summary>
    /// End the experiment after Condition 2 reveal, but KEEP the success UI visible.
    /// </summary>
    private void EndAfterCondition2KeepSuccessUI()
    {
        _ended = true;

        // Disarm reveal trigger WITHOUT hiding UI
        if (revealTrigger != null)
            revealTrigger.ConfigureForCondition(activeRevealTrigger: false, showSuccess: true, resetAndHideUI: false);

        ExitMiddleMode();
        SetReturnPlaneActive(false);

        QuestEventOutlet.Send("experiment_end");
        int endLabel = swapConditions ? 1 : 2;
        Debug.Log($"[ExperimentStateManager] Condition {endLabel} complete. Experiment ends here (success UI remains).");
    }

    private void SetReturnPlaneActive(bool active)
    {
        if (returnPlaneTrigger == null) return;

        returnPlaneTrigger.gameObject.SetActive(active);
        returnPlaneTrigger.SetArmed(active);
    }

    // ---------------- Middle isolation ----------------
    private void EnterMiddleMode()
    {
        if (_middleActive) return;
        _middleActive = true;

        HideSceneExceptPreserved();
        ApplyMiddleSkybox();
    }

    private void ExitMiddleMode()
    {
        if (!_middleActive) return;
        _middleActive = false;

        RestoreScene();
        RestoreSkybox();
    }

    private void HideSceneExceptPreserved()
    {
        _rendererStates.Clear();
        _graphicStates.Clear();
        _lightStates.Clear();
        _colliderStates.Clear();

        var preservedVisualRoots = new List<Transform>(16);
        if (xrRigRoot != null) preservedVisualRoots.Add(xrRigRoot);

        if (middlePreservedRoots != null)
            foreach (var t in middlePreservedRoots)
                if (t != null) preservedVisualRoots.Add(t);

        var preservedColliderRoots = new List<Transform>(32);
        preservedColliderRoots.AddRange(preservedVisualRoots);

        if (middleColliderPreservedRoots != null)
            foreach (var t in middleColliderPreservedRoots)
                if (t != null) preservedColliderRoots.Add(t);

        var allRenderers = FindObjectsOfType<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (r == null) continue;
            if (IsUnderAnyRoot(r.transform, preservedVisualRoots)) continue;
            _rendererStates[r] = r.enabled;
            r.enabled = false;
        }

        var allGraphics = FindObjectsOfType<Graphic>(true);
        foreach (var g in allGraphics)
        {
            if (g == null) continue;
            if (IsUnderAnyRoot(g.transform, preservedVisualRoots)) continue;
            _graphicStates[g] = g.enabled;
            g.enabled = false;
        }

        if (hideOtherLights)
        {
            var allLights = FindObjectsOfType<Light>(true);
            foreach (var l in allLights)
            {
                if (l == null) continue;
                if (IsUnderAnyRoot(l.transform, preservedVisualRoots)) continue;
                _lightStates[l] = l.enabled;
                l.enabled = false;
            }
        }

        if (disableNonPreservedColliders)
        {
            var allColliders = FindObjectsOfType<Collider>(true);
            foreach (var c in allColliders)
            {
                if (c == null) continue;
                if (IsUnderAnyRoot(c.transform, preservedColliderRoots)) continue;
                _colliderStates[c] = c.enabled;
                c.enabled = false;
            }
        }
    }

    private void RestoreScene()
    {
        foreach (var kvp in _rendererStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        foreach (var kvp in _graphicStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        foreach (var kvp in _lightStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        foreach (var kvp in _colliderStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        _rendererStates.Clear();
        _graphicStates.Clear();
        _lightStates.Clear();
        _colliderStates.Clear();
    }

    private static bool IsUnderAnyRoot(Transform t, List<Transform> roots)
    {
        foreach (var root in roots)
        {
            if (root == null) continue;
            if (t == root || t.IsChildOf(root)) return true;
        }
        return false;
    }

    private void ApplyMiddleSkybox()
    {
        if (middleSkybox == null) return;
        RenderSettings.skybox = middleSkybox;
        DynamicGI.UpdateEnvironment();
    }

    private void RestoreSkybox()
    {
        RenderSettings.skybox = defaultSkybox != null ? defaultSkybox : _startupSkybox;
        DynamicGI.UpdateEnvironment();
    }

    private static void ForceEnableVisuals(GameObject[] roots)
    {
        if (roots == null) return;

        foreach (var root in roots)
        {
            if (root == null) continue;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = true;

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].enabled = true;
        }
    }
}
