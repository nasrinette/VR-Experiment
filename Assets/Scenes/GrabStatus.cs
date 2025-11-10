using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GrabStatus : MonoBehaviour
{
    public bool IsGrabbed { get; private set; }
    XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(_ => IsGrabbed = true);
        grabInteractable.selectExited.AddListener(_ => IsGrabbed = false);
    }
}
