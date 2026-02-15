using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Singleton that logs every rating submission to a CSV file.
/// Place on a persistent GameObject in the scene.
/// </summary>
public class RatingManager : MonoBehaviour
{
    public static RatingManager Instance { get; private set; }

    [Tooltip("File name (placed in Application.persistentDataPath).")]
    public string fileName = "ratings.csv";

    private string _filePath;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _filePath = Path.Combine(Application.persistentDataPath, fileName);

        // Write header if file doesn't exist yet
        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "timestamp,context,value\n");
            Debug.Log($"[RatingManager] Created CSV at {_filePath}");
        }
        else
        {
            Debug.Log($"[RatingManager] Appending to existing CSV at {_filePath}");
        }
    }

    /// <summary>
    /// Log a rating to the CSV file and send an LSL marker.
    /// </summary>
    public void LogRating(string context, int value)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string line = $"{timestamp},{context},{value}\n";

        try
        {
            File.AppendAllText(_filePath, line);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RatingManager] Failed to write CSV: {e.Message}");
        }

        QuestEventOutlet.Send($"rating_{context}_{value}");
        Debug.Log($"[RatingManager] Logged: context={context}, value={value}");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
