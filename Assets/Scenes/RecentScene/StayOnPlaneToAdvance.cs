using System.Collections;
using UnityEngine;

public class StayOnPlaneToAdvance : MonoBehaviour
{
    [Header("Player body collider (strict match recommended)")]
    public Collider playerBodyCollider;

    [Header("State machine")]
    public ExperimentStateManager stateManager;

    [Header("Timing")]
    public float secondsRequired = 5f;

    private Coroutine _routine;
    private bool _armed;

    public void SetArmed(bool armed)
    {
        _armed = armed;

        if (!armed && _routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;
        if (!IsPlayerBody(other)) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HoldRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerBody(other)) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private bool IsPlayerBody(Collider other)
    {
        return playerBodyCollider != null && other == playerBodyCollider;
    }

    private IEnumerator HoldRoutine()
    {
        float t = 0f;
        while (t < secondsRequired)
        {
            t += Time.deltaTime;
            yield return null;
        }

        _routine = null;

        QuestEventOutlet.Send("return_plane_held");
        if (stateManager != null)
            stateManager.OnReturnPlaneHeld();
    }
}
