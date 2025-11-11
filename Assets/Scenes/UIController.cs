using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class UIController : MonoBehaviour
{
    [Header("XR Grabbables (boxes)")]
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable[] boxes;     // assign both boxes

    [Header("UI")]
    public GameObject uiSelect;            // "Welcome... select a box"
    public GameObject uiGoWait;            // "Thanks... go to Waiting Room"
    public GameObject uiWaiting;           // optional "Please wait..."
    public GameObject uiFail;              // 1..3
    public GameObject uiSuccess;           // 4th
    public GameObject rewardPlaceholder;   // hidden until last trial

    [Header("Doors")]
    public DoorManager doorStartToWaiting;
    public DoorManager doorWaitingToReveal;
    public DoorManager doorRevealToStart;

    [Header("Timing")]
    public float waitingSeconds = 20f;     // same duration every trial

    [Header("Room Triggers")]
    public RoomTrigger triggerEnteredWaiting;
    public RoomTrigger triggerEnteredReveal;
    public RoomTrigger triggerEnteredStartFromReveal;

    [Header("Optional cues")]
    public GameObject playfulCue;
    public GameObject boringCue;

    [Header("Door Behavior")]
    public float returnDoorCloseDelay = 0.75f; // close Reveal->Start a bit after re-entry

    const int kTotalTrials = 4;
    int _trialIndex = 0;                   // 0..3
    int _choiceIndex = -1;                 // box chosen this trial
    bool _selectionLocked;
    Coroutine _waitCo;

    // Box spawn caches
    Vector3[] _boxStartPos;
    Quaternion[] _boxStartRot;
    Transform[] _boxStartParent;
    Rigidbody[] _boxRBs;

    enum Phase { StartSelect, GoWaiting, Waiting, WaitingDone, Reveal, Complete }
    Phase _phase = Phase.StartSelect;

    const string LogTag = "[UIController] ";
    void Log(string msg) => Debug.Log(LogTag + msg, this);
    void Warn(string msg) => Debug.LogWarning(LogTag + msg, this);

    void Awake()
    {
        // Hook triggers
        if (triggerEnteredWaiting != null) triggerEnteredWaiting.onPlayerEnter += OnEnteredWaiting; else Warn("Missing triggerEnteredWaiting");
        if (triggerEnteredReveal != null) triggerEnteredReveal.onPlayerEnter += OnEnteredReveal; else Warn("Missing triggerEnteredReveal");
        if (triggerEnteredStartFromReveal != null) triggerEnteredStartFromReveal.onPlayerEnter += OnReturnedToStart; else Warn("Missing triggerEnteredStartFromReveal");

        // Hook grabs
        int hooked = 0;
        foreach (var box in boxes)
        {
            if (box == null) { Warn("Boxes array has a null entry"); continue; }
            box.selectEntered.AddListener(OnGrab);
            hooked++;
        }
        Log($"Awake: hooked {hooked} box select listeners.");

        // Cache box spawns
        int n = boxes != null ? boxes.Length : 0;
        _boxStartPos = new Vector3[n];
        _boxStartRot = new Quaternion[n];
        _boxStartParent = new Transform[n];
        _boxRBs = new Rigidbody[n];
        for (int i = 0; i < n; i++)
        {
            var b = boxes[i];
            if (!b) continue;
            var t = b.transform;
            _boxStartPos[i] = t.position;
            _boxStartRot[i] = t.rotation;
            _boxStartParent[i] = t.parent;
            var rb = b.GetComponent<Rigidbody>();
            if (!rb) rb = b.GetComponentInChildren<Rigidbody>();
            _boxRBs[i] = rb;
        }

        // Initial state
        rewardPlaceholder?.SetActive(false);
        SetCueVariant(true);               // keep playful placeholder
        CloseAllDoorsImmediate();
        StartNewTrial();
    }

    void OnDestroy()
    {
        foreach (var box in boxes)
        {
            if (box == null) continue;
            box.selectEntered.RemoveListener(OnGrab);
        }
        if (triggerEnteredWaiting != null) triggerEnteredWaiting.onPlayerEnter -= OnEnteredWaiting;
        if (triggerEnteredReveal != null) triggerEnteredReveal.onPlayerEnter -= OnEnteredReveal;
        if (triggerEnteredStartFromReveal != null) triggerEnteredStartFromReveal.onPlayerEnter -= OnReturnedToStart;
        Log("OnDestroy: listeners removed.");
    }

    // ===== Flow =====
    void StartNewTrial()
    {
        // Reset boxes to their spawn points and re-enable
        ResetBoxesToStart();

        _choiceIndex = -1;
        _selectionLocked = false;
        _phase = Phase.StartSelect;

        EnableBoxes(true);
        ShowOnly(uiSelect);

        doorStartToWaiting.Close();
        doorWaitingToReveal.Close();
        // Intentionally do NOT close doorRevealToStart here

        Log($"StartNewTrial: trial={_trialIndex + 1}/{kTotalTrials}, phase={_phase}");
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (_phase != Phase.StartSelect || _selectionLocked)
        {
            Log($"OnGrab ignored. phase={_phase}, selectionLocked={_selectionLocked}");
            return;
        }

        var grabbed = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
        _choiceIndex = System.Array.IndexOf(boxes, grabbed);
        if (_choiceIndex < 0) _choiceIndex = 0;

        _selectionLocked = true;
        EnableBoxes(false);

        // Close return door now that player is back inside Start room
        if (doorRevealToStart) doorRevealToStart.Close();

        ShowOnly(uiGoWait);
        doorStartToWaiting.Open();
        Log($"OnGrab: chose box index={_choiceIndex} name={(grabbed ? grabbed.name : "unknown")}. Closed Reveal->Start, opened Start->Waiting. phase=GoWaiting");

        _phase = Phase.GoWaiting;
    }

    void OnEnteredWaiting()
    {
        Log($"OnEnteredWaiting: phase={_phase}");
        if (_phase != Phase.GoWaiting)
        {
            Log("OnEnteredWaiting ignored. Not in GoWaiting.");
            return;
        }

        // waiting-room UI
        ShowOnly(uiWaiting);
        _phase = Phase.Waiting;

        if (_waitCo != null) StopCoroutine(_waitCo);
        _waitCo = StartCoroutine(WaitingRoutine());
        Log($"Waiting started for {waitingSeconds:0.##}s.");
    }

    IEnumerator WaitingRoutine()
    {
        yield return new WaitForSeconds(waitingSeconds);
        doorWaitingToReveal.Open();
        _phase = Phase.WaitingDone;
        Log("Waiting done. Door Waiting->Reveal OPEN. phase=WaitingDone");
    }

    void OnEnteredReveal()
    {
        Log($"OnEnteredReveal: phase={_phase}");
        if (_phase != Phase.WaitingDone)
        {
            Log("OnEnteredReveal ignored. Not in WaitingDone.");
            return;
        }

        bool success = (_trialIndex == kTotalTrials - 1);

        if (success)
        {
            ShowOnly(uiSuccess);
            rewardPlaceholder?.SetActive(true);
            _phase = Phase.Complete;
            Log($"SUCCESS on trial {_trialIndex + 1}. Reward shown. phase=Complete");
        }
        else
        {
            ShowOnly(uiFail);
            doorRevealToStart.Open();
            _phase = Phase.Reveal;
            Log($"FAIL on trial {_trialIndex + 1}. Door Reveal->Start OPEN. phase=Reveal");
        }
    }

    void OnReturnedToStart()
    {
        Log($"OnReturnedToStart: phase={_phase}");
        if (_phase != Phase.Reveal) { Log("OnReturnedToStart ignored. Not in Reveal."); return; }

        _trialIndex++;
        Log($"Returning to Start. Next trial index={_trialIndex}");

        if (_trialIndex >= kTotalTrials)
        {
            ShowOnly(uiSuccess);
            rewardPlaceholder?.SetActive(true);
            _phase = Phase.Complete;
            Log("Reached max trials unexpectedly. Forcing SUCCESS and Complete.");
            return;
        }

        rewardPlaceholder?.SetActive(false);

        // Close only the forward doors now
        if (doorStartToWaiting)  doorStartToWaiting.SetOpenImmediate(false);
        if (doorWaitingToReveal) doorWaitingToReveal.SetOpenImmediate(false);

        // Leave Reveal->Start open briefly so it never pinches the player
        if (doorRevealToStart) StartCoroutine(CloseDoorAfterDelay(doorRevealToStart, returnDoorCloseDelay));

        StartNewTrial();
    }

    IEnumerator CloseDoorAfterDelay(DoorManager door, float delay)
    {
        yield return new WaitForSeconds(delay);
        door?.Close();
        Log($"Return door closed after {delay:0.##}s.");
    }

    // ===== Helpers =====
    void ResetBoxesToStart()
    {
        for (int i = 0; i < (boxes?.Length ?? 0); i++)
        {
            var box = boxes[i];
            if (!box) continue;

            var t = box.transform;
            if (_boxStartParent[i]) t.SetParent(_boxStartParent[i], worldPositionStays: true);
            t.SetPositionAndRotation(_boxStartPos[i], _boxStartRot[i]);

            var rb = _boxRBs[i];
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }

            // Ensure fully enabled for next trial
            box.enabled = true;
            foreach (var col in box.GetComponentsInChildren<Collider>()) col.enabled = true;
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
        }
        Log("Boxes reset to spawn.");
    }

    void EnableBoxes(bool enabled)
    {
        foreach (var box in boxes)
        {
            if (box == null) continue;
            box.enabled = enabled;
            foreach (var col in box.GetComponentsInChildren<Collider>())
                col.enabled = enabled;
        }
        Log($"EnableBoxes: {enabled}");
    }

    void ShowOnly(GameObject go)
    {
        if (uiSelect)  uiSelect.SetActive(false);
        if (uiGoWait)  uiGoWait.SetActive(false);
        if (uiWaiting) uiWaiting.SetActive(false);
        if (uiFail)    uiFail.SetActive(false);
        if (uiSuccess) uiSuccess.SetActive(false);
        if (go) go.SetActive(true);
        Log($"ShowOnly -> {(go ? go.name : "none")}");
    }

    void CloseAllDoorsImmediate()
    {
        doorStartToWaiting?.SetOpenImmediate(false);
        doorWaitingToReveal?.SetOpenImmediate(false);
        doorRevealToStart?.SetOpenImmediate(false);
        Log("CloseAllDoorsImmediate called.");
    }

    void SetCueVariant(bool playful)
    {
        if (playfulCue) playfulCue.SetActive(playful);
        if (boringCue)  boringCue.SetActive(!playful);
        Log($"SetCueVariant: playful={playful}");
    }
}
