using UnityEngine;
using System;

public class RoomTrigger : MonoBehaviour
{
    [Tooltip("Assign the UIController in the scene.")]
    public UIController manager;

    public event Action onPlayerEnter;

    void OnTriggerEnter(Collider other)
    {
        // Tag your XR Origin or player capsule as "Player"
        if (!other.CompareTag("Player")) return;

        onPlayerEnter?.Invoke();
        // Also forward directly to manager if assigned via Inspector events
        // (kept generic so you can reuse this component).
    }
}
