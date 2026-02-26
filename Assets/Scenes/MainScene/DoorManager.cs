using UnityEngine;

public class DoorManager : MonoBehaviour
{
    public Transform door;
    public float openAngle = -90f;
    public float speed = 2f;

    [Header("Startup Behaviour")]
    [Tooltip("Default OFF = old behaviour: editor pose is CLOSED. " +
             "ON = editor pose is OPEN and door starts open at runtime.")]
    public bool editorPoseIsOpenAndStartOpen = false;   // ONE TOGGLE

    Quaternion _closed;
    Quaternion _open;
    bool _isOpen;

    void Awake()
    {
        if (!door)
        {
            door = transform;
            Debug.LogWarning("[DoorManager] 'door' not assigned. Defaulting to this.transform.", this);
        }

        var baseEuler = door.eulerAngles;

        if (!editorPoseIsOpenAndStartOpen)
        {
            // --- Original behaviour (unchanged) ---
            // Editor pose = CLOSED
            _closed = door.rotation;
            _open   = Quaternion.Euler(baseEuler + new Vector3(0f, openAngle, 0f));
            _isOpen = false;
        }
        else
        {
            // --- New behaviour ---
            // Editor pose = OPEN
            _open   = door.rotation;
            _closed = Quaternion.Euler(baseEuler - new Vector3(0f, openAngle, 0f));
            // If it closes the wrong direction, flip to + instead of -
            _isOpen = true;
        }

        // Snap to initial logical state so there is no pop on first frame
        door.rotation = _isOpen ? _open : _closed;
    }

    void Update()
    {
        if (!door) return;

        var target = _isOpen ? _open : _closed;
        door.rotation = Quaternion.Slerp(door.rotation, target, Time.deltaTime * speed);
    }

    public void Open()  { _isOpen = true;  QuestEventOutlet.Send($"door_open:{name}"); }
    public void Close() { _isOpen = false; QuestEventOutlet.Send($"door_close:{name}"); }

    public void SetOpenImmediate(bool open)
    {
        if (!door) return;
        _isOpen = open;
        door.rotation = open ? _open : _closed;
    }
}
