using System.Collections;
using UnityEngine;

public class BoxChoiceManager : MonoBehaviour
{
    public GameObject choosePanel;
    public GameObject nextPanel;
    public DoorManager doorManager;

    [Header("Rating UI (shown after After Select UI)")]
    public GameObject ratingPanel;

    [Header("After Rating UI (shown after rating, before door opens)")]
    public GameObject afterRatingPanel;

    [Header("Start Room Door Auto-Close")]
    public DoorManager startRoomDoorManager;  // optional; if null, uses doorManager
    public float closeAfterSeconds = 30f;

    [Header("Optional: Reset cubes at condition start")]
    public Transform cubeBlack;
    public Transform cubeWhite;
    public Transform cubeGray; 

    [Tooltip("Where black cube should be placed on reset.")]
    public Transform cubeBlackSpawn;
    

    [Tooltip("Where white cube should be placed on reset.")]
    public Transform cubeWhiteSpawn;

    [Tooltip("Where gray cube should be placed on reset.")]
    public Transform cubeGraySpawn;

    [Tooltip("Optional; if not assigned, we try GetComponent<Rigidbody>() from cube transforms.")]
    public Rigidbody cubeBlackRb;

    [Tooltip("Optional; if not assigned, we try GetComponent<Rigidbody>() from cube transforms.")]
    public Rigidbody cubeWhiteRb;
    

    [Tooltip("Optional; if not assigned, we try GetComponent<Rigidbody>() from cube transforms.")]
    public Rigidbody cubeGrayRb;

    private bool _chosen;
    private bool _rated;
    private Coroutine _closeRoutine;

    void Start()
    {
        _chosen = false;
        _rated = false;

        if (choosePanel) choosePanel.SetActive(true);
        if (nextPanel) nextPanel.SetActive(false);
        if (ratingPanel) ratingPanel.SetActive(false);
        if (afterRatingPanel) afterRatingPanel.SetActive(false);
    }

    public void ChooseBlack() { QuestEventOutlet.Send("box_choose_black"); Choose(); }
    public void ChooseWhite() { QuestEventOutlet.Send("box_choose_white"); Choose(); }
    public void ChooseGray() { QuestEventOutlet.Send("box_choose_gray"); Choose(); }
    private void Choose()
    {
        if (_chosen) return;
        if (FamilarityManager.IsFamiliarityActive) return; // ignore input from familiarity env cubes
        _chosen = true;

        if (choosePanel) choosePanel.SetActive(false);

        // Hide boxes after the choice is made
        SetCubesVisible(false);

        // Show After Select UI — user must click "ok" before rating slider appears
        if (nextPanel) nextPanel.SetActive(true);

        Debug.Log("[Experiment] Box chosen — After Select UI shown");
    }

    /// <summary>
    /// Called by the "ok" button on After Select UI to advance to the rating slider.
    /// </summary>
    public void OnAfterSelectOk()
    {
        if (nextPanel) nextPanel.SetActive(false);
        if (ratingPanel) ratingPanel.SetActive(true);

        Debug.Log("[Experiment] After Select UI dismissed — rating slider shown");
    }

    /// <summary>
    /// Called by RatingSliderUI when the user clicks Done.
    /// </summary>
    public void SubmitRating(int rating)
    {
        if (_rated) return;
        _rated = true;

        QuestEventOutlet.Send($"box_rating_{rating}");

        if (ratingPanel) ratingPanel.SetActive(false);
        if (afterRatingPanel) afterRatingPanel.SetActive(true);

        Debug.Log($"[Experiment] Rating submitted: {rating} — After Rating UI shown");
    }

    /// <summary>
    /// Called by the "ok" button on After Rating UI. Opens the door so the user can walk.
    /// </summary>
    public void OnAfterRatingOk()
    {
        if (afterRatingPanel) afterRatingPanel.SetActive(false);

        // Open the door now
        if (doorManager) doorManager.Open();

        // Auto-close the start room door after delay
        var doorToClose = startRoomDoorManager != null ? startRoomDoorManager : doorManager;
        if (doorToClose != null)
        {
            if (_closeRoutine != null) StopCoroutine(_closeRoutine);
            _closeRoutine = StartCoroutine(CloseDoorAfterDelay(doorToClose, closeAfterSeconds));
        }

        Debug.Log("[Experiment] After Rating dismissed — door opened");
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
        _rated = false;

        if (_closeRoutine != null)
        {
            StopCoroutine(_closeRoutine);
            _closeRoutine = null;
        }

        if (choosePanel) choosePanel.SetActive(true);
        if (nextPanel) nextPanel.SetActive(false);
        if (ratingPanel) ratingPanel.SetActive(false);
        if (afterRatingPanel) afterRatingPanel.SetActive(false);

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
        ResetCube(cubeGray, cubeGraySpawn, ref cubeGrayRb);

        Debug.Log("[Experiment] Box choice reset");
    }

    private void SetCubesVisible(bool visible)
    {
        if (cubeBlack != null) cubeBlack.gameObject.SetActive(visible);
        if (cubeWhite != null) cubeWhite.gameObject.SetActive(visible);
        if (cubeGray != null) cubeGray.gameObject.SetActive(visible);
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
