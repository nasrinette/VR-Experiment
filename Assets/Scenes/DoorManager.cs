using UnityEngine;

public class DoorManager : MonoBehaviour
{
    public Transform door;         // rotating part
    public float openAngle = -90f;
    public float speed = 2f;

    Quaternion _closed;
    Quaternion _open;
    bool _isOpen;

    void Awake()
    {
        if (door == null)
        {
            door = transform;
            Debug.LogWarning("[DoorManager] 'door' not assigned. Defaulting to this.transform.", this);
        }
        _closed = door.rotation;
        _open = Quaternion.Euler(door.eulerAngles + new Vector3(0f, openAngle, 0f));
    }

    void Update()
    {
        if (!door) return;
        var target = _isOpen ? _open : _closed;
        door.rotation = Quaternion.Slerp(door.rotation, target, Time.deltaTime * speed);
    }

    public void Open()  { _isOpen = true;  }
    public void Close() { _isOpen = false; }

    public void SetOpenImmediate(bool open)
    {
        if (!door) return;
        _isOpen = open;
        door.rotation = open ? _open : _closed;
    }
}
