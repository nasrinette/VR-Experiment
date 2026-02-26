using UnityEngine;

/// <summary>
/// Tiny runtime component added by BarrierManager to each trigger collider.
/// Forwards OnTriggerEnter back to the parent BarrierManager.
/// </summary>
public class BarrierTriggerForwarder : MonoBehaviour
{
    private BarrierManager _manager;
    private int _index;

    public void Init(BarrierManager manager, int index)
    {
        _manager = manager;
        _index = index;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_manager != null)
            _manager.OnPlayerEnteredTrigger(_index, other);
    }
}
