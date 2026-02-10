using System;
using System.Collections.Generic;
using UnityEngine;
using LSL;
using LSL4Unity.Utils;

/// <summary>
/// Streams head position (XYZ) and orientation (quaternion XYZW) via LSL.
/// Extends LSL4Unity's AFloatOutlet for proper integration.
/// Attach to the XR Camera (Main Camera under XR Origin).
/// Uses FixedUpdate (~50 Hz) by default.
/// </summary>
public class HeadPoseOutlet : AFloatOutlet
{
    private static readonly List<string> _channelNames = new()
    {
        "PosX", "PosY", "PosZ",
        "RotX", "RotY", "RotZ", "RotW"
    };

    private int _pushCount;
    private float _logTimer;
    private const float LOG_INTERVAL = 10f;
    private bool _outletReady;

    public override List<string> ChannelNames => _channelNames;

    public void Reset()
    {
        StreamName = "Quest.HeadPose";
        StreamType = "MoCap";
        moment = MomentForSampling.FixedUpdate;
    }

    protected override void Start()
    {
        Debug.Log($"LOG [HeadPoseOutlet] Initializing... StreamName={StreamName}, StreamType={StreamType}, Moment={moment}");

        if (string.IsNullOrEmpty(StreamName) || string.IsNullOrEmpty(StreamType))
        {
            Debug.LogError("LOG [HeadPoseOutlet] StreamName or StreamType is empty! Set them in the Inspector.");
            return;
        }

        // Check network before creating outlet
        LogNetworkStatus();

        try
        {
            int lslVersion = LSL.LSL.library_version();
            Debug.Log($"LOG [HeadPoseOutlet] liblsl loaded — version {lslVersion / 100}.{lslVersion % 100}");
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError($"LOG [HeadPoseOutlet] FATAL — liblsl native library not found! " +
                $"Ensure liblsl.so is in Assets/Plugins/Android/arm64-v8a/ for Quest builds. Exception: {e.Message}");
            return;
        }
        catch (Exception e)
        {
            Debug.LogError($"LOG [HeadPoseOutlet] FATAL — failed to load liblsl: {e.GetType().Name}: {e.Message}");
            return;
        }

        try
        {
            base.Start();
            _outletReady = outlet != null;

            if (_outletReady)
            {
                Debug.Log($"LOG [HeadPoseOutlet] Outlet created successfully — " +
                    $"{ChannelCount} channels, rate={Time.fixedDeltaTime:F4}s ({1.0 / Time.fixedDeltaTime:F1} Hz)");
                Debug.Log($"LOG [HeadPoseOutlet] Waiting for consumers (LabRecorder) on the local network...");
            }
            else
            {
                Debug.LogError("LOG [HeadPoseOutlet] Outlet is null after Start() — stream creation failed!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"LOG [HeadPoseOutlet] Failed to create LSL outlet: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            _outletReady = false;
        }
    }

    protected override void FillChannelsHeader(XMLElement channels)
    {
        string[] labels = { "PosX", "PosY", "PosZ", "RotX", "RotY", "RotZ", "RotW" };
        string[] units  = { "m", "m", "m", "quat", "quat", "quat", "quat" };

        for (int i = 0; i < labels.Length; i++)
        {
            var ch = channels.append_child("channel");
            ch.append_child_value("label", labels[i]);
            ch.append_child_value("unit", units[i]);
        }
    }

    protected override bool BuildSample()
    {
        if (!_outletReady) return false;

        var pos = transform.position;
        var rot = transform.rotation;

        sample[0] = pos.x;
        sample[1] = pos.y;
        sample[2] = pos.z;
        sample[3] = rot.x;
        sample[4] = rot.y;
        sample[5] = rot.z;
        sample[6] = rot.w;

        _pushCount++;

        // Periodic heartbeat log
        _logTimer += Time.fixedDeltaTime;
        if (_logTimer >= LOG_INTERVAL)
        {
            int consumers = 0;
            try { consumers = outlet.have_consumers() ? 1 : 0; } catch { }

            if (consumers > 0)
                Debug.Log($"LOG [HeadPoseOutlet] Streaming OK — {_pushCount} samples sent, consumer connected");
            else
                Debug.LogWarning($"LOG [HeadPoseOutlet] Streaming but NO consumer — {_pushCount} samples sent. " +
                    "Is LabRecorder running on the same network?");

            _logTimer = 0f;
        }

        return true;
    }

    private void LogNetworkStatus()
    {
        var reachability = Application.internetReachability;
        switch (reachability)
        {
            case NetworkReachability.NotReachable:
                Debug.LogError("LOG [HeadPoseOutlet] NETWORK: Not reachable! WiFi is OFF or disconnected. " +
                    "LSL requires the Quest and laptop to be on the same WiFi network.");
                break;
            case NetworkReachability.ReachableViaLocalAreaNetwork:
                Debug.Log("LOG [HeadPoseOutlet] NETWORK: Connected via WiFi/LAN — good");
                break;
            case NetworkReachability.ReachableViaCarrierDataNetwork:
                Debug.LogWarning("LOG [HeadPoseOutlet] NETWORK: Connected via mobile data — " +
                    "LSL needs WiFi/LAN, not cellular. Ensure Quest and laptop are on the same WiFi.");
                break;
        }
    }

    void OnDestroy()
    {
        Debug.Log($"LOG [HeadPoseOutlet] Destroyed — total samples sent: {_pushCount}");
    }
}
