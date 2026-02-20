using UnityEngine;
using UnityEditor;

public static class ReparentGlassDoors
{
    [MenuItem("Tools/Reparent Glass Doors Under Hinges")]
    public static void Execute()
    {
        string[] turnstilePaths = new string[]
        {
            "Conditions/Condition1/Barriers/Turnstile",
            "Conditions/Condition1/Barriers/Turnstile (1)",
            "Conditions/Condition1/Barriers/Turnstile (2)",
            "Conditions/Condition1/Barriers/Turnstile (3)",
            "Conditions/Condition1/Barriers/Turnstile (4)",
            "Conditions/Condition2/Barriers/Turnstile",
            "Conditions/Condition2/Barriers/Turnstile (1)",
            "Conditions/Condition2/Barriers/Turnstile (2)",
            "Conditions/Condition2/Barriers/Turnstile (3)",
            "Conditions/Condition2/Barriers/Turnstile (4)",
        };

        int count = 0;
        foreach (var path in turnstilePaths)
        {
            var go = GameObject.Find(path);
            if (go == null)
                go = FindInactive(path);
            if (go == null)
            {
                Debug.LogWarning($"[ReparentGlassDoors] Could not find: {path}");
                continue;
            }

            // Unpack prefab instance so we can reparent children
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                PrefabUtility.UnpackPrefabInstance(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(go),
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                Debug.Log($"[ReparentGlassDoors] Unpacked prefab: {path}");
            }

            var ts = go.transform;
            ReparentChild(ts, "GlassDoor", "HingeDoor", ref count);
            ReparentChild(ts, "Opening", "HingeDoor", ref count);
            ReparentChild(ts, "GlassDoor1", "HingeDoor1", ref count);
            ReparentChild(ts, "Opening1", "HingeDoor1", ref count);
        }

        Debug.Log($"[ReparentGlassDoors] Done. Reparented {count} objects.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ReparentGlassDoors] Scene saved.");
    }

    static void ReparentChild(Transform turnstile, string glassName, string hingeName, ref int count)
    {
        var glass = turnstile.Find(glassName);
        var hinge = turnstile.Find(hingeName);
        if (glass == null) { Debug.LogWarning($"[ReparentGlassDoors] Missing {glassName} in {turnstile.name}"); return; }
        if (hinge == null) { Debug.LogWarning($"[ReparentGlassDoors] Missing {hingeName} in {turnstile.name}"); return; }

        if (glass.parent == hinge)
        {
            Debug.Log($"[ReparentGlassDoors] {glassName} already under {hingeName} in {turnstile.name}");
            return;
        }

        glass.SetParent(hinge, true);
        Debug.Log($"[ReparentGlassDoors] Reparented {glassName} under {hingeName} in {turnstile.name}");
        count++;
    }

    static GameObject FindInactive(string path)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var result = FindChildByPath(root.transform, path);
            if (result != null) return result.gameObject;
        }
        return null;
    }

    static Transform FindChildByPath(Transform root, string path)
    {
        if (root.name == path) return root;

        string rootPrefix = root.name + "/";
        if (!path.StartsWith(rootPrefix)) return null;

        string remaining = path.Substring(rootPrefix.Length);
        Transform current = root;

        foreach (var part in remaining.Split('/'))
        {
            Transform child = null;
            for (int i = 0; i < current.childCount; i++)
            {
                if (current.GetChild(i).name == part)
                {
                    child = current.GetChild(i);
                    break;
                }
            }
            if (child == null) return null;
            current = child;
        }
        return current;
    }
}
