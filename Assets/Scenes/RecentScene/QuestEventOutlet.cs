using System;
using UnityEngine;
using LSL;

/// <summary>
/// Sends irregular string event markers via LSL (separate outlet from head pose).
/// Singleton — call QuestEventOutlet.Send("event_name") from any script.
/// </summary>
public class QuestEventOutlet : MonoBehaviour
{
    public static QuestEventOutlet Instance { get; private set; }

    [Header("LSL Stream Settings")]
    public string streamName = "Quest.Events";
    public string streamType = "Markers";

    private StreamOutlet _outlet;
    private string[] _sample = new string[1];
    private int _markerCount;
    private bool _outletReady;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"LOG [QuestEventOutlet] Duplicate instance on '{gameObject.name}' — destroying. " +
                $"Singleton already on '{Instance.gameObject.name}'.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log($"LOG [QuestEventOutlet] Singleton assigned to '{gameObject.name}'");
    }

    void Start()
    {
        Debug.Log($"LOG [QuestEventOutlet] Initializing... StreamName={streamName}, StreamType={streamType}");

        // Check network
        LogNetworkStatus();

        // Verify liblsl loaded
        try
        {
            int lslVersion = LSL.LSL.library_version();
            Debug.Log($"LOG [QuestEventOutlet] liblsl loaded — version {lslVersion / 100}.{lslVersion % 100}");
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError($"LOG [QuestEventOutlet] FATAL — liblsl native library not found! " +
                $"Ensure liblsl.so is in Assets/Plugins/Android/arm64-v8a/ for Quest builds. Exception: {e.Message}");
            return;
        }
        catch (Exception e)
        {
            Debug.LogError($"LOG [QuestEventOutlet] FATAL — failed to load liblsl: {e.GetType().Name}: {e.Message}");
            return;
        }

        // Create outlet
        try
        {
            var hash = new Hash128();
            hash.Append(streamName);
            hash.Append(streamType);
            hash.Append(gameObject.GetInstanceID());

            var info = new StreamInfo(streamName, streamType, 1, LSL.LSL.IRREGULAR_RATE,
                channel_format_t.cf_string, hash.ToString());

            var channels = info.desc().append_child("channels");
            var ch = channels.append_child("channel");
            ch.append_child_value("label", "EventMarker");

            // Embed participant metadata into stream description
            var subject = info.desc().append_child("subject");
            subject.append_child_value("participant_id", SequenceManager.ParticipantId);
            subject.append_child_value("sequence_number", SequenceManager.ActiveSequence.ToString());

            _outlet = new StreamOutlet(info);
            _outletReady = _outlet != null;

            if (_outletReady)
            {
                Debug.Log($"LOG [QuestEventOutlet] Outlet created successfully — " +
                    $"irregular rate, string markers");
                Debug.Log($"LOG [QuestEventOutlet] Waiting for consumers (LabRecorder) on the local network...");
            }
            else
            {
                Debug.LogError("LOG [QuestEventOutlet] Outlet is null — stream creation failed!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"LOG [QuestEventOutlet] Failed to create LSL outlet: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            _outletReady = false;
            return;
        }

        Send($"app_start:participant:{SequenceManager.ParticipantId}:sequence:{SequenceManager.ActiveSequence}");
    }

    /// <summary>
    /// Send an event marker string through LSL.
    /// </summary>
    public static void Send(string marker)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"LOG [QuestEventOutlet] Cannot send '{marker}' — no QuestEventOutlet in scene!");
            return;
        }

        if (!Instance._outletReady || Instance._outlet == null)
        {
            Debug.LogWarning($"LOG [QuestEventOutlet] Cannot send '{marker}' — outlet not ready " +
                "(liblsl failed to load or network issue)");
            return;
        }

        try
        {
            Instance._sample[0] = marker;
            double timestamp = LSL.LSL.local_clock();
            Instance._outlet.push_sample(Instance._sample, timestamp);
            Instance._markerCount++;

            bool hasConsumer = false;
            try { hasConsumer = Instance._outlet.have_consumers(); } catch { }

            if (hasConsumer)
                Debug.Log($"LOG [QuestEventOutlet] Marker #{Instance._markerCount}: '{marker}' (t={timestamp:F3}) — consumer connected");
            else
                Debug.LogWarning($"LOG [QuestEventOutlet] Marker #{Instance._markerCount}: '{marker}' (t={timestamp:F3}) — " +
                    "NO consumer! Is LabRecorder running on the same network?");
        }
        catch (Exception e)
        {
            Debug.LogError($"LOG [QuestEventOutlet] Failed to push marker '{marker}': {e.GetType().Name}: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        Send("app_quit");
        Debug.Log($"LOG [QuestEventOutlet] App quitting — total markers sent: {_markerCount}");
    }

    void OnDestroy()
    {
        Debug.Log($"LOG [QuestEventOutlet] Destroyed — total markers sent: {_markerCount}");
        try
        {
            _outlet?.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LOG [QuestEventOutlet] Error disposing outlet: {e.Message}");
        }
        _outlet = null;
        _outletReady = false;
        if (Instance == this) Instance = null;
    }

    private void LogNetworkStatus()
    {
        var reachability = Application.internetReachability;
        switch (reachability)
        {
            case NetworkReachability.NotReachable:
                Debug.LogError("LOG [QuestEventOutlet] NETWORK: Not reachable! WiFi is OFF or disconnected. " +
                    "LSL requires the Quest and laptop to be on the same WiFi network.");
                break;
            case NetworkReachability.ReachableViaLocalAreaNetwork:
                Debug.Log("LOG [QuestEventOutlet] NETWORK: Connected via WiFi/LAN — good");
                break;
            case NetworkReachability.ReachableViaCarrierDataNetwork:
                Debug.LogWarning("LOG [QuestEventOutlet] NETWORK: Connected via mobile data — " +
                    "LSL needs WiFi/LAN, not cellular. Ensure Quest and laptop are on the same WiFi.");
                break;
        }
    }
}
