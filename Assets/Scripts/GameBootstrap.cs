using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-40)]
public class GameBootstrap : MonoBehaviour
{
    [Tooltip("On = Quest 72 FPS path (combined meshes, fewer lights). Off = unoptimized scene for before/after recording.")]
    [SerializeField]
    bool enableQuestOptimizations;

    Transform[] spawnPoints;

    void Awake()
    {
        XrPerformance.Enabled = enableQuestOptimizations;
#if !UNITY_EDITOR
        DisableDeviceSimulator();
#endif
        XrPerformance.Apply();
        DisableLegacyGround();
        Transform arena = ArenaBuilder.Build(transform, out spawnPoints, out int propCount, out int physicsCount);
        BakeNavMesh(arena);
        Debug.Log($"Arena ready ({(XrPerformance.Enabled ? "optimized" : "unoptimized")}): {propCount} visible props, {physicsCount} physics objects, {spawnPoints.Length} spawn points. Target {XrPerformance.TargetFps} FPS.");
    }

    void Start()
    {
        XrPerformance.Apply();
#if UNITY_EDITOR
        LockCursor();
#endif
        PlayerHealth.Ensure();
        var waves = gameObject.AddComponent<EnemyWaveManager>();
        waves.spawnPoints = spawnPoints;
        waves.maxAlive = 10;
        waves.Begin();
    }

    void OnDestroy()
    {
        XrPerformance.RestorePipeline();
#if UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#endif
    }

#if UNITY_EDITOR
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            LockCursor();
    }

    static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
#endif

    static void DisableDeviceSimulator()
    {
        GameObject simulator = GameObject.Find("XR Device Simulator");
        if (simulator != null)
            simulator.SetActive(false);
    }

    static void DisableLegacyGround()
    {
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
            plane.SetActive(false);
    }

    static void BakeNavMesh(Transform arena)
    {
        var surface = arena.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.minRegionArea = XrPerformance.Enabled ? 1f : 0.5f;
        surface.overrideVoxelSize = true;
        surface.voxelSize = XrPerformance.Enabled ? 0.18f : 0.12f;
        surface.BuildNavMesh();
    }
}
