using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SequenceManager : MonoBehaviour
{
    [Header("Participant")]
    public string participantId = "";

    [Header("Sequence (1-8)")]
    [Range(1, 8)]
    public int sequenceNumber = 1;

    [Header("Skyboxes (s1, s2, s3, s4)")]
    public Material[] skyboxes = new Material[4];

    [Header("References")]
    public ExperimentSequenceConfig config;
    public ExperimentStateManager stateManager;

    [Header("Glass Rooms — Condition 1 (2 rooms)")]
    public RoomManager[] condition1Rooms = new RoomManager[2];

    [Header("Glass Rooms — Condition 2 (2 rooms)")]
    public RoomManager[] condition2Rooms = new RoomManager[2];

    // Static properties for LSL outlets to read
    public static string ParticipantId { get; private set; } = "";
    public static int ActiveSequence { get; private set; } = 0;

    private void Awake()
    {
        // Publish to statics so LSL outlets can read them
        ParticipantId = participantId;
        ActiveSequence = sequenceNumber;

        if (config == null)
        {
            Debug.LogError("[SequenceManager] No ExperimentSequenceConfig assigned!");
            return;
        }

        if (sequenceNumber < 1 || sequenceNumber > config.sequences.Length)
        {
            Debug.LogError($"[SequenceManager] Sequence number {sequenceNumber} out of range (1-{config.sequences.Length}).");
            return;
        }

        var entry = config.sequences[sequenceNumber - 1];

        if (entry.skyboxOrder == null || entry.skyboxOrder.Length < 4)
        {
            Debug.LogError($"[SequenceManager] Sequence {sequenceNumber} has invalid skyboxOrder (need 4 indices).");
            return;
        }

        // Determine condition order
        bool c2First = entry.firstCondition == 2;

        if (stateManager != null)
        {
            stateManager.swapConditions = c2First;
        }

        // Assign skyboxes to glass rooms
        // skyboxOrder: [firstRoom1, firstRoom2, secondRoom1, secondRoom2]
        // "first" and "second" refer to the order the participant experiences them
        RoomManager[] firstRooms = c2First ? condition2Rooms : condition1Rooms;
        RoomManager[] secondRooms = c2First ? condition1Rooms : condition2Rooms;

        AssignSkybox(firstRooms, 0, entry.skyboxOrder[0]);
        AssignSkybox(firstRooms, 1, entry.skyboxOrder[1]);
        AssignSkybox(secondRooms, 0, entry.skyboxOrder[2]);
        AssignSkybox(secondRooms, 1, entry.skyboxOrder[3]);

        Debug.Log($"[SequenceManager] Participant={participantId}, Sequence={sequenceNumber}, " +
            $"Order={( c2First ? "C2→C1" : "C1→C2")}, " +
            $"Skyboxes=s{entry.skyboxOrder[0]+1},s{entry.skyboxOrder[1]+1}→s{entry.skyboxOrder[2]+1},s{entry.skyboxOrder[3]+1}");
    }

    private void AssignSkybox(RoomManager[] rooms, int roomIndex, int skyboxIndex)
    {
        if (rooms == null || roomIndex >= rooms.Length || rooms[roomIndex] == null)
        {
            Debug.LogWarning($"[SequenceManager] Room at index {roomIndex} is null — skipping skybox assignment.");
            return;
        }

        if (skyboxes == null || skyboxIndex >= skyboxes.Length || skyboxes[skyboxIndex] == null)
        {
            Debug.LogWarning($"[SequenceManager] Skybox at index {skyboxIndex} is null — skipping.");
            return;
        }

        rooms[roomIndex].roomSkybox = skyboxes[skyboxIndex];
    }
}
