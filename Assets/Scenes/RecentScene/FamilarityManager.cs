using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FamilarityManager : MonoBehaviour
{
    [Header("Door (opens via handle hover after box choice)")]
    public DoorManager doorManager;

    [Header("Door Handle Interaction (hover to open)")]
    public DoorHandleInteractable[] doorHandleInteractables;

    [Header("Cubes (player grabs one to choose)")]
    public Transform cubeBlack;
    public Transform cubeWhite;
    public Transform cubeGray;

    private bool _chosen;

    private bool HasHandles => doorHandleInteractables != null && doorHandleInteractables.Length > 0;

    void Start()
    {
        _chosen = false;

        // Auto-wire cube grab events
        WireCube(cubeBlack);
        WireCube(cubeWhite);
        WireCube(cubeGray);

        // Wire door handle callbacks
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.onHandleActivated.AddListener(OnDoorHandleActivated);

        DisarmHandles();
    }

    void OnDestroy()
    {
        UnwireCube(cubeBlack);
        UnwireCube(cubeWhite);
        UnwireCube(cubeGray);

        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.onHandleActivated.RemoveListener(OnDoorHandleActivated);
    }

    // ---- Cube grab wiring ----

    private void WireCube(Transform cube)
    {
        if (cube == null) return;
        var grab = cube.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.AddListener(OnCubeGrabbed);
    }

    private void UnwireCube(Transform cube)
    {
        if (cube == null) return;
        var grab = cube.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.RemoveListener(OnCubeGrabbed);
    }

    private void OnCubeGrabbed(SelectEnterEventArgs args)
    {
        Choose();
    }

    // ---- Box Choice ----

    private void Choose()
    {
        if (_chosen) return;
        _chosen = true;

        SetCubesVisible(false);

        // Arm door handles so the player can hover to open
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.SetArmed(true);

        Debug.Log("[FamilarityManager] Box chosen — door handle armed");
    }

    // ---- Door Handle Hover ----

    private void OnDoorHandleActivated()
    {
        if (!_chosen) return;

        if (doorManager) doorManager.Open();

        DisarmHandles();

        Debug.Log("[FamilarityManager] Door opened via handle hover");
    }

    // ---- Helpers ----

    private void DisarmHandles()
    {
        if (HasHandles)
            foreach (var handle in doorHandleInteractables)
                if (handle != null)
                    handle.SetArmed(false);
    }

    private void SetCubesVisible(bool visible)
    {
        if (cubeBlack != null) cubeBlack.gameObject.SetActive(visible);
        if (cubeWhite != null) cubeWhite.gameObject.SetActive(visible);
        if (cubeGray != null)  cubeGray.gameObject.SetActive(visible);
    }
}
