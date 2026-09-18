using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    public float maxHealth = 100f;
    public float health = 100f;

    TextMesh hud;
    float regenAt = -1f;
    float fpsSmooth = 72f;
    XRBaseInputInteractor[] interactors;

    public static PlayerHealth Ensure()
    {
        if (Instance != null)
            return Instance;

        Camera cam = Camera.main;
        Transform host = cam != null ? cam.transform : null;
        if (host == null)
        {
            var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            host = origin != null ? origin.transform : new GameObject("PlayerHealthHost").transform;
        }

        Instance = host.GetComponent<PlayerHealth>();
        if (Instance == null)
            Instance = host.gameObject.AddComponent<PlayerHealth>();
        return Instance;
    }

    void Awake()
    {
        Instance = this;
        health = maxHealth;
        interactors = FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None);
        BuildHud();
    }

    void LateUpdate()
    {
        if (regenAt > 0f && Time.time >= regenAt)
        {
            health = maxHealth;
            regenAt = -1f;
        }

        if (hud == null)
            return;

        fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.08f);
        EnemyWaveManager waves = EnemyWaveManager.Instance;
        string wave = waves != null ? waves.StatusLine : "Wave --";
        hud.text = $"{wave}\nHP {Mathf.CeilToInt(health)}  |  {fpsSmooth:0} FPS  {(XrPerformance.Enabled ? "optimized" : "UNOPTIMIZED")}";
        hud.color = fpsSmooth < 68f || health <= 25f ? new Color(1f, 0.45f, 0.3f) : Color.white;
    }

    public void TakeDamage(float amount)
    {
        if (health <= 0f)
            return;

        health = Mathf.Max(0f, health - amount);
        SendHaptic(0.7f, 0.12f);

        if (health <= 0f)
            regenAt = Time.time + 4f;
    }

    void BuildHud()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        var go = new GameObject("StatusHud");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, -0.28f, 0.85f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.016f;

        hud = go.AddComponent<TextMesh>();
        hud.fontSize = 28;
        hud.anchor = TextAnchor.MiddleCenter;
        hud.alignment = TextAlignment.Center;
        hud.characterSize = 1f;
        hud.color = Color.white;
        hud.text = "Ready";
    }

    static void SendHaptic(float amplitude, float duration)
    {
        var cached = Instance != null ? Instance.interactors : null;
        if (cached == null || cached.Length == 0)
            cached = FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None);

        for (int i = 0; i < cached.Length; i++)
        {
            if (cached[i] != null)
                cached[i].SendHapticImpulse(amplitude, duration);
        }
    }
}
