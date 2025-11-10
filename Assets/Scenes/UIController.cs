using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class UIController : MonoBehaviour
{
    [Header("UI")]
    public GameObject selectUI;
    public GameObject failUI;
    public GameObject successUI;

    [Header("Grabbables")]
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabbable1;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabbable2;

    private int grabCount;     // number of distinct grabs so far
    private bool successShown; // latch after success

    void Awake()
    {
        HideAllUI();
        ShowSelectUI();
        grabCount = 0;
        successShown = false;
    }

    void OnEnable()
    {
        if (grabbable1)
        {
            grabbable1.selectEntered.AddListener(OnGrab);
            grabbable1.selectExited.AddListener(OnRelease);
        }
        if (grabbable2)
        {
            grabbable2.selectEntered.AddListener(OnGrab);
            grabbable2.selectExited.AddListener(OnRelease);
        }
    }

    void OnDisable()
    {
        if (grabbable1)
        {
            grabbable1.selectEntered.RemoveListener(OnGrab);
            grabbable1.selectExited.RemoveListener(OnRelease);
        }
        if (grabbable2)
        {
            grabbable2.selectEntered.RemoveListener(OnGrab);
            grabbable2.selectExited.RemoveListener(OnRelease);
        }
    }

    // Called once per grab action
    private void OnGrab(SelectEnterEventArgs _)
    {
        if (successShown)
        {
            ShowSuccessUI(); // keep success persistent
            return;
        }

        grabCount++; // 1..5...

        if (grabCount < 5)
        {
            ShowFailUI();     // grabs 1–4 => Fail
        }
        else
        {
            successShown = true;
            ShowSuccessUI();  // grab 5 => Success immediately
        }
    }

    // For the first 4 grabs, return to Select on release
    private void OnRelease(SelectExitEventArgs _)
    {
        if (!successShown)
            ShowSelectUI();
        else
            ShowSuccessUI(); // optional: keep success after release
    }

    private void ShowSelectUI()
    {
        HideAllUI();
        if (selectUI) selectUI.SetActive(true);
    }

    private void ShowFailUI()
    {
        HideAllUI();
        if (failUI) failUI.SetActive(true);
    }

    private void ShowSuccessUI()
    {
        HideAllUI();
        if (successUI) successUI.SetActive(true);
    }

    private void HideAllUI()
    {
        if (selectUI)  selectUI.SetActive(false);
        if (failUI)    failUI.SetActive(false);
        if (successUI) successUI.SetActive(false);
    }
}
