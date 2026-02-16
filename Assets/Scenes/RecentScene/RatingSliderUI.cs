using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to any rating slider canvas. Auto-wires the Slider, value label,
/// and Done button. On submit: logs to RatingManager, closes the popup,
/// and optionally notifies BoxChoiceManager.
/// </summary>
public class RatingSliderUI : MonoBehaviour
{
    [Header("Auto-found if left empty")]
    public Slider slider;
    public Button doneButton;

    [Header("Value display (auto-found child named 'ValueLabel', or created)")]
    public TMP_Text valueLabel;

    [Header("Context tag for LSL marker (box_choice, reveal)")]
    public string ratingContext = "box_choice";

    [Header("Optional: notify BoxChoiceManager when done")]
    public BoxChoiceManager boxChoiceManager;

    void OnEnable()
    {
        // Auto-find components if not assigned
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        if (doneButton == null)
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name == "Done")
                {
                    doneButton = b;
                    break;
                }
            }
        }

        if (valueLabel == null)
        {
            // Look for a child named "ValueLabel"
            var labels = GetComponentsInChildren<TMP_Text>(true);
            foreach (var lbl in labels)
            {
                if (lbl.gameObject.name == "ValueLabel")
                {
                    valueLabel = lbl;
                    break;
                }
            }
        }

        // Wire listeners
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
            slider.onValueChanged.AddListener(OnSliderChanged);
            OnSliderChanged(slider.value); // update label immediately
        }

        if (doneButton != null)
        {
            doneButton.onClick.RemoveListener(OnDoneClicked);
            doneButton.onClick.AddListener(OnDoneClicked);
        }
    }

    void OnDisable()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        if (doneButton != null)
            doneButton.onClick.RemoveListener(OnDoneClicked);
    }

    private void OnSliderChanged(float value)
    {
        if (valueLabel != null)
            valueLabel.text = Mathf.RoundToInt(value).ToString();
    }

    private void OnDoneClicked()
    {
        int rating = slider != null ? Mathf.RoundToInt(slider.value) : 1;

        // Send rating via LSL
        if (RatingManager.Instance != null)
            RatingManager.Instance.LogRating(ratingContext, rating);

        // Notify BoxChoiceManager so it can show nextPanel
        if (boxChoiceManager != null)
            boxChoiceManager.SubmitRating(rating);

        // Close this popup
        gameObject.SetActive(false);
    }
}
