using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Opens turnstile barriers after a configurable delay when the player
/// enters corresponding trigger zones.
///
/// Place on each condition's Barriers parent (or any object in the condition).
/// Assign trigger→turnstile pairs in the Inspector.
///
/// Each turnstile must have HingeDoor / HingeDoor1 children (empty pivots
/// positioned at the pipe hinge points) with GlassDoor / GlassDoor1
/// already parented underneath them in the scene hierarchy.
/// </summary>
public class BarrierManager : MonoBehaviour
{
    [Serializable]
    public struct BarrierEntry
    {
        [Tooltip("Trigger collider (BoxCollider, isTrigger) that the player walks through")]
        public Collider triggerZone;

        [Tooltip("Turnstile root transform (parent of HingeDoor, HingeDoor1, etc.)")]
        public Transform turnstile;
    }

    [Header("Barrier Pairs (trigger → turnstile, in order)")]
    public BarrierEntry[] barriers;

    [Header("Player")]
    [Tooltip("Assign the XR Origin body collider so only the player triggers barriers")]
    public Collider playerBodyCollider;

    [Header("Timing")]
    [Tooltip("Per-barrier delays in seconds, mapped by index (e.g. [20,30,60,120,90])")]
    public float[] openDelays;

    [Tooltip("Fallback delay if openDelays is null, empty, or shorter than the barrier count")]
    public float defaultOpenDelay = 10f;

    /// <summary>Raised once when every barrier in the array has opened.</summary>
    public event System.Action OnAllBarriersOpen;

    [Header("Open Animation")]
    [Tooltip("Angle (degrees) each glass panel swings open")]
    public float openAngle = 90f;

    [Tooltip("Duration of the swing animation (seconds)")]
    public float animationDuration = 0.8f;

    // --- runtime state ---
    private Transform[] _hingeA;          // HingeDoor (swings GlassDoor)
    private Transform[] _hingeB;          // HingeDoor1 (swings GlassDoor1)
    private Quaternion[] _closedA, _openA;
    private Quaternion[] _closedB, _openB;
    private bool[] _isOpen;
    private bool[] _timerActive;

    private void Awake()
    {
        int n = barriers.Length;
        _hingeA     = new Transform[n];
        _hingeB     = new Transform[n];
        _closedA    = new Quaternion[n];
        _openA      = new Quaternion[n];
        _closedB    = new Quaternion[n];
        _openB      = new Quaternion[n];
        _isOpen     = new bool[n];
        _timerActive = new bool[n];

        for (int i = 0; i < n; i++)
        {
            var ts = barriers[i].turnstile;
            if (ts == null) continue;

            // Find the pre-placed hinge pivots in the scene
            var hingeDoor  = ts.Find("HingeDoor");
            var hingeDoor1 = ts.Find("HingeDoor1");

            if (hingeDoor != null)
            {
                _hingeA[i]  = hingeDoor;
                _closedA[i] = hingeDoor.localRotation;
                _openA[i]   = _closedA[i] * Quaternion.Euler(0f, openAngle, 0f);
            }

            if (hingeDoor1 != null)
            {
                _hingeB[i]  = hingeDoor1;
                _closedB[i] = hingeDoor1.localRotation;
                _openB[i]   = _closedB[i] * Quaternion.Euler(0f, -openAngle, 0f);
            }

            // Attach a tiny forwarder to each trigger so OnTriggerEnter routes back here
            var zone = barriers[i].triggerZone;
            if (zone != null)
            {
                var fwd = zone.gameObject.AddComponent<BarrierTriggerForwarder>();
                fwd.Init(this, i);
            }
        }
    }

    /// <summary>True when the last barrier in the array has opened.</summary>
    public bool LastBarrierOpen =>
        _isOpen != null && _isOpen.Length > 0 && _isOpen[_isOpen.Length - 1];

    private float GetDelay(int index)
    {
        if (openDelays != null && index < openDelays.Length && openDelays[index] > 0f)
            return openDelays[index];
        return defaultOpenDelay;
    }

    /// <summary>Called by BarrierTriggerForwarder when something enters a trigger zone.</summary>
    public void OnPlayerEnteredTrigger(int index, Collider other)
    {
        if (playerBodyCollider != null && other != playerBodyCollider) return;
        if (_isOpen[index] || _timerActive[index]) return;

        StartCoroutine(DelayThenOpen(index));
    }

    private IEnumerator DelayThenOpen(int index)
    {
        _timerActive[index] = true;

        float delay = GetDelay(index);

        string triggerName = barriers[index].triggerZone != null
            ? barriers[index].triggerZone.name : index.ToString();

        QuestEventOutlet.Send($"barrier_trigger:{triggerName}");
        QuestEventOutlet.Send($"barrier_timer_start:{triggerName}:{delay}s");

        yield return new WaitForSeconds(delay);

        QuestEventOutlet.Send($"barrier_timer_end:{triggerName}");

        // Animate the glass panels swinging open
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationDuration));

            if (_hingeA[index])
                _hingeA[index].localRotation = Quaternion.Slerp(_closedA[index], _openA[index], t);
            if (_hingeB[index])
                _hingeB[index].localRotation = Quaternion.Slerp(_closedB[index], _openB[index], t);

            yield return null;
        }

        // Snap to exact final rotation
        if (_hingeA[index]) _hingeA[index].localRotation = _openA[index];
        if (_hingeB[index]) _hingeB[index].localRotation = _openB[index];

        _isOpen[index] = true;
        _timerActive[index] = false;

        QuestEventOutlet.Send($"barrier_open:{triggerName}");

        if (index == barriers.Length - 1)
        {
            QuestEventOutlet.Send("last_barrier_open");
            OnAllBarriersOpen?.Invoke();
        }
    }

    /// <summary>Reset all barriers to closed (useful when switching conditions).</summary>
    public void ResetAll()
    {
        StopAllCoroutines();
        if (_hingeA == null) return;

        for (int i = 0; i < barriers.Length; i++)
        {
            if (_hingeA[i]) _hingeA[i].localRotation = _closedA[i];
            if (_hingeB[i]) _hingeB[i].localRotation = _closedB[i];
            _isOpen[i] = false;
            _timerActive[i] = false;
        }
    }

    private void OnDisable()
    {
        ResetAll();
    }
}
