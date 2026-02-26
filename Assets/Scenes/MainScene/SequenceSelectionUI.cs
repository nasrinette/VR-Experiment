using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Drives the "Sequence Menu" World-Space canvas.
///
/// Participant ID is a numeric counter (1-99) controlled by ParticipantIdUp / ParticipantIdDown —
/// no keyboard needed. Wire those to ◄ / ► buttons in the scene.
///
/// Inspector fields:
///   menuRoot           → "Sequence Menu" Canvas root
///   sequenceDropdown   → TMP_Dropdown
///   participantIdLabel → TextMeshProUGUI that displays the current ID (e.g. "P01")
///   startButton        → Start/Confirm button
///
/// Left controller menu button toggles the panel at any time.
/// </summary>
[DefaultExecutionOrder(-200)]
public class SequenceSelectionUI : MonoBehaviour
{
    [Header("Experiment references")]
    public ExperimentSequenceConfig config;
    public SequenceManager          sequenceManager;
    public ExperimentStateManager   stateManager;

    [Header("Scene UI — assign in Inspector")]
    [Tooltip("The 'Sequence Menu' Canvas root — toggled by the left menu button.")]
    public GameObject         menuRoot;

    [Tooltip("TMP_Dropdown — populated from ExperimentSequenceConfig at runtime.")]
    public TMP_Dropdown       sequenceDropdown;

    [Tooltip("Condition jump dropdown — options populated at runtime: blank / C1 / C2.")]
    public TMP_Dropdown       conditionDropdown;

    [Tooltip("TextMeshProUGUI label that shows the current Participant ID (e.g. 1, 2 …).")]
    public TextMeshProUGUI    participantIdLabel;

    [Tooltip("The '+' button — increments participant ID.")]
    public Button             participantIdUpBtn;

    [Tooltip("The '-' button — decrements participant ID.")]
    public Button             participantIdDownBtn;

    [Tooltip("Assign here OR wire the button OnClick to OnStartClicked() directly.")]
    public Button             startButton;

    [Tooltip("Camera to position the menu in front of — leave blank to use Camera.main.")]
    public Camera             headCamera;

    [Tooltip("How far in front of the camera the menu appears (metres).")]
    public float              menuDistance = 1.5f;

    [Tooltip("Vertical offset from camera height (metres). Positive = higher.")]
    public float              menuHeightOffset = 1f;

    // ── private state ──────────────────────────────────────────────────────────
    private bool _initialized          = false;
    private bool _menuVisible          = true;
    private bool _wasMenuButtonPressed = false;
    private int  _participantId        = 1;   // shown as P01 … P99

    // ── lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (stateManager != null)
            stateManager.enabled = false;

        PopulateDropdown();
        PopulateConditionDropdown();
        RefreshParticipantLabel();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (participantIdUpBtn != null)
        {
            participantIdUpBtn.onClick.AddListener(ParticipantIdUp);
            EnsureAboveBlocker(participantIdUpBtn);
        }

        if (participantIdDownBtn != null)
        {
            participantIdDownBtn.onClick.AddListener(ParticipantIdDown);
            EnsureAboveBlocker(participantIdDownBtn);
        }

        // Defer first placement by one frame so the XR rig has settled its head position.
        menuRoot.SetActive(false);
        StartCoroutine(ShowMenuAfterXRInit());
    }

    private IEnumerator ShowMenuAfterXRInit()
    {
        yield return null; // wait one frame for XR to initialize
        SetMenuVisible(true);
    }

    private void Update()
    {
        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        leftHand.TryGetFeatureValue(CommonUsages.menuButton, out bool pressed);

        if (pressed && !_wasMenuButtonPressed)
            ToggleMenu();

        _wasMenuButtonPressed = pressed;
    }

    // ── public API ─────────────────────────────────────────────────────────────

    /// <summary>Wire to the ◄ button next to the Participant ID label.</summary>
    public void ParticipantIdDown()
    {
        if (_initialized) return;
        if (sequenceDropdown != null && sequenceDropdown.IsExpanded) sequenceDropdown.Hide();
        _participantId = Mathf.Max(1, _participantId - 1);
        RefreshParticipantLabel();
    }

    /// <summary>Wire to the ► button next to the Participant ID label.</summary>
    public void ParticipantIdUp()
    {
        if (_initialized) return;
        if (sequenceDropdown != null && sequenceDropdown.IsExpanded) sequenceDropdown.Hide();
        _participantId = Mathf.Min(99, _participantId + 1);
        RefreshParticipantLabel();
    }

    /// <summary>Called by the Start/Confirm button's OnClick event.</summary>
    public void OnStartClicked()
    {
        int conditionTarget = GetSelectedCondition();

        if (_initialized)
        {
            // Re-entry: jump directly to the selected condition if one is chosen.
            if (conditionTarget != 0 && stateManager != null)
            {
                stateManager.JumpToCondition(conditionTarget);
                Debug.Log($"[SequenceSelectionUI] Jumping to C{conditionTarget}.");
            }
            SetMenuVisible(false);
            return;
        }

        string pid    = $"P{_participantId:D2}";
        int    seqNum = sequenceDropdown != null ? sequenceDropdown.value + 1 : 1;

        if (sequenceManager != null)
            sequenceManager.Initialize(pid, seqNum);
        else
            Debug.LogError("[SequenceSelectionUI] SequenceManager not assigned!");

        // Tell the state manager which condition to start from (0 = first in sequence).
        if (stateManager != null)
            stateManager.startFromCondition = conditionTarget;

        if (stateManager != null)
            stateManager.enabled = true;

        _initialized = true;

        if (sequenceDropdown != null) sequenceDropdown.interactable = false;

        if (startButton != null)
        {
            var lbl = startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = "Close";
        }

        SetMenuVisible(false);
        Debug.Log($"[SequenceSelectionUI] Started — PID: {pid}, Sequence: {seqNum}, ConditionOverride: {(conditionTarget == 0 ? "none" : $"C{conditionTarget}")}");
    }

    public void ToggleMenu() => SetMenuVisible(!_menuVisible);

    // ── helpers ────────────────────────────────────────────────────────────────

    private void SetMenuVisible(bool visible)
    {
        _menuVisible = visible;
        if (menuRoot != null)
        {
            if (visible) PlaceMenuInFrontOfCamera();
            menuRoot.SetActive(visible);
        }
    }

    private void PlaceMenuInFrontOfCamera()
    {
        Camera cam = headCamera ?? Camera.main;
        if (cam == null || menuRoot == null) return;

        // Flatten vertical look so the menu stays upright
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = cam.transform.forward; // fallback if looking straight up/down
        else forward.Normalize();

        menuRoot.transform.position = cam.transform.position
            + forward * menuDistance
            + Vector3.up * menuHeightOffset;
        // Canvas front (readable side) faces the camera — forward matches camera's horizontal look
        menuRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private void RefreshParticipantLabel()
    {
        if (participantIdLabel != null)
            participantIdLabel.text = _participantId.ToString();
    }

    // Gives a button its own Canvas so it renders above TMP_Dropdown's full-canvas Blocker.
    // Adds both GraphicRaycaster (editor/mouse) and TrackedDeviceGraphicRaycaster (XR controllers).
    private static void EnsureAboveBlocker(Button btn)
    {
        if (btn.GetComponent<Canvas>() != null) return;
        var c = btn.gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 100;
        if (btn.GetComponent<GraphicRaycaster>() == null)
            btn.gameObject.AddComponent<GraphicRaycaster>();
        if (btn.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            btn.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
    }

    // Returns 1=C1, 2=C2, or 0 if nothing (blank) is selected.
    private int GetSelectedCondition()
    {
        if (conditionDropdown == null) return 0;
        string text = conditionDropdown.options.Count > conditionDropdown.value
            ? conditionDropdown.options[conditionDropdown.value].text
            : "";
        if (text == "C1") return 1;
        if (text == "C2") return 2;
        return 0;
    }

    private void PopulateConditionDropdown()
    {
        if (conditionDropdown == null) return;
        conditionDropdown.ClearOptions();
        conditionDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("—"),   // index 0 = no override
            new TMP_Dropdown.OptionData("C1"),  // index 1
            new TMP_Dropdown.OptionData("C2"),  // index 2
        });
        conditionDropdown.value = 0;
        conditionDropdown.RefreshShownValue();
    }

    private void PopulateDropdown()
    {
        if (sequenceDropdown == null || config == null) return;

        var options = new List<TMP_Dropdown.OptionData>();

        for (int i = 0; i < config.sequences.Length; i++)
        {
            var    entry   = config.sequences[i];
            bool   c2First = entry.firstCondition == 2;
            string first   = c2First ? "C2" : "C1";
            string second  = c2First ? "C1" : "C2";

            string label;
            if (entry.skyboxOrder != null && entry.skyboxOrder.Length >= 4)
            {
                int[] s = entry.skyboxOrder;
                label = $"Seq {i + 1}:  {first} \u2192 sk{s[0]+1},sk{s[1]+1}  \u00b7  {second} \u2192 sk{s[2]+1},sk{s[3]+1}";
            }
            else
            {
                label = $"Seq {i + 1}: (incomplete)";
            }

            options.Add(new TMP_Dropdown.OptionData(label));
        }

        sequenceDropdown.ClearOptions();
        sequenceDropdown.AddOptions(options);
        sequenceDropdown.value = 0;
        sequenceDropdown.RefreshShownValue();
    }
}
