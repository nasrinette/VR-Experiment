using System.Collections;
using UnityEngine;

public class BoxChoiceManager : MonoBehaviour
{
    public GameObject choosePanel;
    public GameObject nextPanel;
    public DoorManager doorManager;

    [Header("Start Room Door Auto-Close")]
    public DoorManager startRoomDoorManager;  // optional; if null, uses doorManager
    public float closeAfterSeconds = 30f;

    [Header("Optional: Reset cubes at condition start")]
    public Transform cubeBlack;
    public Transform cubeWhite;

    [Tooltip("Where black cube should be placed on reset.")]
    public Transform cubeBlackSpawn;

    [Tooltip("Where white cube should be placed on reset.")]
    public Transform cubeWhiteSpawn;

    [Tooltip("Optional; if not assigned, we try GetComponent<Rigidbody>() from cube transforms.")]
    public Rigidbody cubeBlackRb;

    [Tooltip("Optional; if not assigned, we try GetComponent<Rigidbody>() from cube transforms.")]
    public Rigidbody cubeWhiteRb;

    private bool _chosen;
    private Coroutine _closeRoutine;

    void Start()
    {
        _chosen = false;

        if (choosePanel) choosePanel.SetActive(true);
        if (nextPanel) nextPanel.SetActive(false);
    }

    public void ChooseBlack() { QuestEventOutlet.Send("box_choose_black"); Choose(); }
    public void ChooseWhite() { QuestEventOutlet.Send("box_choose_white"); Choose(); }

    private void Choose()
    {
        if (_chosen) return;
        _chosen = true;

        if (choosePanel) choosePanel.SetActive(false);
        if (nextPanel) nextPanel.SetActive(true);

        // Hide boxes after the choice is made
        SetCubesVisible(false);

        // Open the door now
        if (doorManager) doorManager.Open();

        // Auto-close the start room door after delay
        var doorToClose = startRoomDoorManager != null ? startRoomDoorManager : doorManager;
        if (doorToClose != null)
        {
            if (_closeRoutine != null) StopCoroutine(_closeRoutine);
            _closeRoutine = StartCoroutine(CloseDoorAfterDelay(doorToClose, closeAfterSeconds));
        }

        Debug.Log("[Experiment] Box chosen");
    }

    private IEnumerator CloseDoorAfterDelay(DoorManager dm, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (dm != null) dm.Close();
    }

    // -----------------------------
    // New: deterministic reset API
    // -----------------------------
    public void ResetChoice(bool closeDoorImmediately = true)
    {
        _chosen = false;

        if (_closeRoutine != null)
        {
            StopCoroutine(_closeRoutine);
            _closeRoutine = null;
        }

        if (choosePanel) choosePanel.SetActive(true);
        if (nextPanel) nextPanel.SetActive(false);

        // Close doors for a clean start
        if (closeDoorImmediately)
        {
            if (doorManager) doorManager.SetOpenImmediate(false);

            var doorToClose = startRoomDoorManager != null ? startRoomDoorManager : doorManager;
            if (doorToClose != null) doorToClose.SetOpenImmediate(false);
        }

        // Re-enable cubes FIRST so Rigidbody is active, then reset positions
        SetCubesVisible(true);
        ResetCube(cubeBlack, cubeBlackSpawn, ref cubeBlackRb);
        ResetCube(cubeWhite, cubeWhiteSpawn, ref cubeWhiteRb);

        Debug.Log("[Experiment] Box choice reset");
    }

    private void SetCubesVisible(bool visible)
    {
        if (cubeBlack != null) cubeBlack.gameObject.SetActive(visible);
        if (cubeWhite != null) cubeWhite.gameObject.SetActive(visible);
    }

    private static void ResetCube(Transform cube, Transform spawn, ref Rigidbody rb)
    {
        if (cube == null || spawn == null) return;

        // Acquire RB if not assigned
        if (rb == null) rb = cube.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Temporarily go kinematic so physics doesn't fight the teleport
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;

            cube.position = spawn.position;
            cube.rotation = spawn.rotation;

            rb.isKinematic = wasKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
        else
        {
            cube.position = spawn.position;
            cube.rotation = spawn.rotation;
        }
    }
}
