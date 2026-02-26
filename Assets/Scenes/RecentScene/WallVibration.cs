using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Add to a wall to vibrate controllers and shake the wall when controllers come close.
/// Uses HapticImpulsePlayer (XRI 3.x) found automatically at startup.
/// No Rigidbody or trigger collider needed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallVibration : MonoBehaviour
{
    [Header("Proximity")]
    [Tooltip("Distance (meters) from wall surface at which vibration starts")]
    public float proximityDistance = 0.15f;

    [Header("Haptic Feedback")]
    [Range(0f, 1f)]
    public float maxAmplitude = 0.7f;

    [Tooltip("Duration of each haptic pulse (seconds)")]
    public float pulseDuration = 0.05f;

    [Tooltip("How often to send a pulse at max proximity (seconds)")]
    public float pulseInterval = 0.08f;

    [Header("Wall Shake")]
    public bool enableShake = true;

    [Tooltip("Side-to-side displacement (meters)")]
    public float shakeAmount = 0.005f;

    [Tooltip("Oscillations per second")]
    public float shakeFrequency = 25f;

    // ── runtime ──────────────────────────────────────────────────────────────
    private Collider _col;
    private HapticImpulsePlayer[] _hapticPlayers;
    private float[] _nextPulseTime;

    private Vector3 _restLocalPos;
    private bool _shaking;
    private Coroutine _shakeRoutine;
    private float _shakeTime;

    // Retry finding controllers with a delay instead of every frame
    private float _nextFindAttempt = 0f;
    private const float FindRetryInterval = 1f;

    private void Start()
    {
        _col = GetComponent<Collider>();
        _restLocalPos = transform.localPosition;
        FindControllers();
    }

    private void FindControllers()
    {
        _hapticPlayers = FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None);
        _nextPulseTime = new float[_hapticPlayers.Length];
        _nextFindAttempt = Time.time + FindRetryInterval;

        Debug.Log($"[VIBR] {name}: found {_hapticPlayers.Length} HapticImpulsePlayer(s)");
        for (int i = 0; i < _hapticPlayers.Length; i++)
            Debug.Log($"[VIBR]   [{i}] {_hapticPlayers[i].gameObject.name}");
    }

    private void Update()
    {
        if (_hapticPlayers == null || _hapticPlayers.Length == 0)
        {
            if (Time.time >= _nextFindAttempt)
                FindControllers();
            return;
        }

        // Both ExperimentStateManager (middle) and FamilarityManager disable wall
        // colliders when entering their isolation modes. Stop everything if that happens.
        if (!_col.enabled)
        {
            if (_shaking)
            {
                Debug.Log($"[VIBR] {name}: collider disabled — stopping shake");
                StopShake();
            }
            return;
        }

        bool anyClose = false;

        for (int i = 0; i < _hapticPlayers.Length; i++)
        {
            var player = _hapticPlayers[i];
            if (player == null) continue;

            // Measure distance from controller to closest point on wall surface
            Vector3 closest = _col.ClosestPoint(player.transform.position);
            float dist = Vector3.Distance(player.transform.position, closest);

            if (dist > proximityDistance) continue;

            anyClose = true;

            // Scale: 1.0 when touching surface, 0.0 at proximityDistance
            float t = 1f - (dist / proximityDistance);
            float amplitude = t * maxAmplitude;
            float interval = pulseInterval * Mathf.Lerp(1f, 0.3f, t);

            if (Time.time >= _nextPulseTime[i])
            {
                Debug.Log($"[VIBR] {name}: haptic on '{player.gameObject.name}' | dist={dist:F3}m  amp={amplitude:F2}");
                player.SendHapticImpulse(amplitude, pulseDuration);
                _nextPulseTime[i] = Time.time + interval;
            }
        }

        if (anyClose && enableShake && !_shaking)
        {
            Debug.Log($"[VIBR] {name}: shake START");
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeLoop());
        }
        else if (!anyClose && _shaking)
        {
            Debug.Log($"[VIBR] {name}: shake STOP");
            StopShake();
        }
    }

    private IEnumerator ShakeLoop()
    {
        _shaking = true;
        _shakeTime = 0f;
        while (true)
        {
            float offset = Mathf.Sin(_shakeTime * shakeFrequency * Mathf.PI * 2f) * shakeAmount;
            transform.localPosition = _restLocalPos + transform.right * offset;
            _shakeTime += Time.deltaTime;
            yield return null;
        }
    }

    private void StopShake()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }
        _shaking = false;
        transform.localPosition = _restLocalPos;
    }

    private void OnDisable()
    {
        StopShake();
    }
}
