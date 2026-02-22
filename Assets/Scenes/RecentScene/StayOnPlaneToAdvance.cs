using UnityEngine;
using UnityEngine.XR;

public class StayOnPlaneToAdvance : MonoBehaviour
{
    [Header("Player body collider (strict match recommended)")]
    public Collider playerBodyCollider;

    [Header("State machine")]
    public ExperimentStateManager stateManager;

    private bool _armed;
    private bool _playerOnPlane;
    private bool _advanced;

    public void SetArmed(bool armed)
    {
        _armed = armed;
        _playerOnPlane = false;
        _advanced = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;
        if (!IsPlayerBody(other)) return;

        _playerOnPlane = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerBody(other)) return;

        _playerOnPlane = false;
    }

    private void Update()
    {
        if (!_armed || !_playerOnPlane || _advanced) return;

        if (IsYButtonPressed())
        {
            _advanced = true;
            QuestEventOutlet.Send("return_plane_held");
            if (stateManager != null)
                stateManager.OnReturnPlaneHeld();
        }
    }

    private bool IsYButtonPressed()
    {
        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid &&
            leftHand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) &&
            pressed)
        {
            return true;
        }
        return false;
    }

    private bool IsPlayerBody(Collider other)
    {
        return playerBodyCollider != null && other == playerBodyCollider;
    }
}
