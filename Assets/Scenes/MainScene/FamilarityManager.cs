using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Handles both the X-button toggle for the familiarity environment and
/// the in-environment door/cube interactions.
///
/// IMPORTANT: This component must live on an always-active top-level GameObject
/// (NOT inside Familarity Env itself), so the X-button input keeps working
/// even while Familarity Env is disabled.
///
/// Press X  →  hides renderers/graphics AND disables colliders + XR interactables
///             on everything outside the preserved roots, then enables familiarityEnv.
/// Press X  →  disables familiarityEnv, fully restores the normal environment.
/// </summary>
public class FamilarityManager : MonoBehaviour
{
    // ── Toggle ─────────────────────────────────────────────────────────────────

    [Header("Familiarity Environment")]
    public GameObject familiarityEnv;

    [Header("Preserved Roots (always visible in both modes)")]
    [Tooltip("XR rig is always kept. Add the washout plane or any other objects " +
             "that must remain visible when familiarity mode is active.")]
    public Transform xrRigRoot;
    public Transform[] preservedRoots;

    [Header("Input — left controller X button")]
    [Tooltip("Drag 'XRI Left/Primary Button' here. Leave empty to auto-bind.")]
    public InputActionReference xButtonActionReference;

    // ── Door / Cube Interaction ────────────────────────────────────────────────

    [Header("Door (opens via handle hover after box choice)")]
    public DoorManager doorManager;

    [Header("Door Handle Interaction (hover to open)")]
    public DoorHandleInteractable[] doorHandleInteractables;

    [Header("Cubes (player grabs one to choose)")]
    public Transform cubeBlack;
    public Transform cubeWhite;
    public Transform cubeGray;

    // ── Private state ──────────────────────────────────────────────────────────

    // Static flag so other systems (e.g. BoxChoiceManager) can guard against
    // reacting to input that belongs to the familiarity environment.
    public static bool IsFamiliarityActive { get; private set; }

    private InputAction _xAction;
    private bool _familiarityActive;
    private bool _chosen;

    private readonly Dictionary<Renderer,          bool> _rendererStates     = new(512);
    private readonly Dictionary<Graphic,           bool> _graphicStates      = new(256);
    private readonly Dictionary<Collider,          bool> _colliderStates     = new(512);
    private readonly Dictionary<XRBaseInteractable, bool> _interactableStates = new(128);

    private bool HasHandles => doorHandleInteractables != null && doorHandleInteractables.Length > 0;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        // Familiarity env starts hidden; X-press shows it.
        if (familiarityEnv != null)
            familiarityEnv.SetActive(false);

        _familiarityActive = false;
        _chosen = false;

        // Wire cube grab events — works even while cubes are inactive inside familiarityEnv.
        WireCube(cubeBlack);
        WireCube(cubeWhite);
        WireCube(cubeGray);

        // Wire door handle callbacks.
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.onHandleActivated.AddListener(OnDoorHandleActivated);

        DisarmHandles();
    }

    private void OnEnable()
    {
        if (xButtonActionReference != null)
        {
            _xAction = xButtonActionReference.action;
        }
        else
        {
            _xAction = new InputAction(
                name:    "FamilarityToggle_X",
                type:    InputActionType.Button,
                binding: "<XRController>{LeftHand}/primaryButton");
        }

        _xAction.performed += OnXPressed;
        _xAction.Enable();
    }

    private void OnDisable()
    {
        if (_xAction == null) return;

        _xAction.performed -= OnXPressed;

        if (xButtonActionReference == null)
        {
            _xAction.Disable();
            _xAction.Dispose();
        }

        _xAction = null;
    }

    private void OnDestroy()
    {
        UnwireCube(cubeBlack);
        UnwireCube(cubeWhite);
        UnwireCube(cubeGray);

        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.onHandleActivated.RemoveListener(OnDoorHandleActivated);
    }

    // ── X Button Toggle ────────────────────────────────────────────────────────

    private void OnXPressed(InputAction.CallbackContext ctx)
    {
        if (_familiarityActive) ExitFamiliarity();
        else                    EnterFamiliarity();
    }

    private void EnterFamiliarity()
    {
        _familiarityActive = true;
        IsFamiliarityActive = true;

        // Reset interaction state so the player starts fresh each visit.
        _chosen = false;
        SetCubesVisible(true);
        DisarmHandles();

        // Hide normal scene first, THEN activate familiarityEnv so its own
        // renderers are never included in the hide pass.
        HideSceneExceptPreserved();

        if (familiarityEnv != null)
            familiarityEnv.SetActive(true);

        Debug.Log("[FamilarityManager] Entered familiarity mode.");
    }

    private void ExitFamiliarity()
    {
        _familiarityActive = false;
        IsFamiliarityActive = false;

        if (familiarityEnv != null)
            familiarityEnv.SetActive(false);

        RestoreScene();

        Debug.Log("[FamilarityManager] Exited familiarity mode.");
    }

    // ── Cube Grab ──────────────────────────────────────────────────────────────

    private void WireCube(Transform cube)
    {
        if (cube == null) return;
        var grab = cube.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectEntered.AddListener(OnCubeGrabbed);
    }

    private void UnwireCube(Transform cube)
    {
        if (cube == null) return;
        var grab = cube.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectEntered.RemoveListener(OnCubeGrabbed);
    }

    private void OnCubeGrabbed(SelectEnterEventArgs args) => Choose();

    private void Choose()
    {
        if (_chosen) return;
        _chosen = true;

        SetCubesVisible(false);

        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null) handle.SetArmed(true);

        Debug.Log("[FamilarityManager] Box chosen — door handle armed.");
    }

    // ── Door Handle ────────────────────────────────────────────────────────────

    private void OnDoorHandleActivated()
    {
        if (!_chosen) return;

        if (doorManager) doorManager.Open();

        DisarmHandles();

        Debug.Log("[FamilarityManager] Door opened via handle hover.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void DisarmHandles()
    {
        if (!HasHandles) return;
        foreach (var handle in doorHandleInteractables)
            if (handle != null) handle.SetArmed(false);
    }

    private void SetCubesVisible(bool visible)
    {
        if (cubeBlack != null) cubeBlack.gameObject.SetActive(visible);
        if (cubeWhite != null) cubeWhite.gameObject.SetActive(visible);
        if (cubeGray  != null) cubeGray.gameObject.SetActive(visible);
    }

    // ── Scene Isolation ────────────────────────────────────────────────────────

    private void HideSceneExceptPreserved()
    {
        _rendererStates.Clear();
        _graphicStates.Clear();
        _colliderStates.Clear();
        _interactableStates.Clear();

        var preserved = BuildPreservedList();

        // Visuals
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r == null || IsUnderAnyRoot(r.transform, preserved)) continue;
            _rendererStates[r] = r.enabled;
            r.enabled = false;
        }

        foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
        {
            if (g == null || IsUnderAnyRoot(g.transform, preserved)) continue;
            _graphicStates[g] = g.enabled;
            g.enabled = false;
        }

        // Physics — disable colliders so the player can't bump into real-env geometry
        foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (c == null || IsUnderAnyRoot(c.transform, preserved)) continue;
            _colliderStates[c] = c.enabled;
            c.enabled = false;
        }

        // XR interaction — disable interactables so nothing in the real env can be grabbed/hovered
        foreach (var xi in FindObjectsByType<XRBaseInteractable>(FindObjectsSortMode.None))
        {
            if (xi == null || IsUnderAnyRoot(xi.transform, preserved)) continue;
            _interactableStates[xi] = xi.enabled;
            xi.enabled = false;
        }
    }

    private void RestoreScene()
    {
        foreach (var kvp in _rendererStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        foreach (var kvp in _graphicStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        foreach (var kvp in _colliderStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        foreach (var kvp in _interactableStates)
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;

        _rendererStates.Clear();
        _graphicStates.Clear();
        _colliderStates.Clear();
        _interactableStates.Clear();
    }

    private List<Transform> BuildPreservedList()
    {
        var list = new List<Transform>(16);

        if (xrRigRoot != null) list.Add(xrRigRoot);

        if (preservedRoots != null)
            foreach (var t in preservedRoots)
                if (t != null) list.Add(t);

        return list;
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
}
