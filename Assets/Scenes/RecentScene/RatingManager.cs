using UnityEngine;

/// <summary>
/// Singleton that sends every rating submission as an LSL event marker.
/// Place on a persistent GameObject in the scene.
/// </summary>
public class RatingManager : MonoBehaviour
{
    public static RatingManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Send a rating as an LSL event marker.
    /// </summary>
    public void LogRating(string context, int value)
    {
        QuestEventOutlet.Send($"rating_{context}_{value}");
        Debug.Log($"[RatingManager] Sent LSL marker: rating_{context}_{value}");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
