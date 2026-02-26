using UnityEngine;

[System.Serializable]
public struct SequenceEntry
{
    [Tooltip("Which condition goes first: 1 or 2")]
    public int firstCondition;

    [Tooltip("4 skybox indices (0-based) in order: firstRoom1, firstRoom2, secondRoom1, secondRoom2")]
    public int[] skyboxOrder;
}

[CreateAssetMenu(fileName = "SequenceConfig", menuName = "Experiment/Sequence Config")]
public class ExperimentSequenceConfig : ScriptableObject
{
    [Tooltip("The 8 predefined sequences")]
    public SequenceEntry[] sequences = new SequenceEntry[8];
}
