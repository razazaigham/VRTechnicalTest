using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

public static class XrPerformance
{
    public const int TargetFps = 72;

    public static bool Enabled { get; set; } = true;

    public static void Apply()
    {
        if (Enabled)
            ApplyOptimized();
        else
            ApplyUnoptimized();
    }

    static void ApplyOptimized()
    {
        Application.targetFrameRate = TargetFps;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadowDistance = 20f;
        QualitySettings.antiAliasing = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.particleRaycastBudget = 32;
        QualitySettings.skinWeights = SkinWeights.TwoBones;
        QualitySettings.lodBias = 0.8f;

        Physics.defaultSolverIterations = 4;
        Physics.defaultSolverVelocityIterations = 1;
        Physics.sleepThreshold = 0.04f;

        RenderSettings.fog = false;

        ConfigureCameras(optimized: true);
        ConfigureDirectionalLight(optimized: true);
        ConfigureUrpAsset(optimized: true);
        ConfigurePostProcessing(optimized: true);
        TryEnableFoveation(1f);
    }

    static void ApplyUnoptimized()
    {
        Application.targetFrameRate = TargetFps;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowDistance = 50f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.particleRaycastBudget = 256;
        QualitySettings.skinWeights = SkinWeights.FourBones;
        QualitySettings.lodBias = 2f;

        Physics.defaultSolverIterations = 6;
        Physics.defaultSolverVelocityIterations = 1;
        Physics.sleepThreshold = 0.005f;

        ConfigureCameras(optimized: false);
        ConfigureDirectionalLight(optimized: false);
        ConfigureUrpAsset(optimized: false);
        ConfigurePostProcessing(optimized: false);
        TryEnableFoveation(0f);
    }

    static void ConfigureCameras(bool optimized)
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            cam.allowHDR = !optimized;
            cam.allowMSAA = true;
            cam.farClipPlane = optimized ? 45f : 1000f;

            UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
            if (urp == null)
                continue;

            urp.antialiasing = optimized ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
            urp.renderPostProcessing = true;
            urp.requiresColorOption = optimized ? CameraOverrideOption.Off : CameraOverrideOption.On;
            urp.requiresDepthOption = optimized ? CameraOverrideOption.Off : CameraOverrideOption.On;
            urp.allowXRRendering = true;
        }

        if (XRSettings.enabled)
            XRSettings.eyeTextureResolutionScale = optimized ? 0.92f : 1f;
    }

    static void ConfigureDirectionalLight(bool optimized)
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light.type != LightType.Directional)
                continue;

            light.shadows = optimized ? LightShadows.Hard : LightShadows.Soft;
            light.shadowStrength = optimized ? 0.65f : 1f;
            light.intensity = optimized ? Mathf.Min(light.intensity, 1.8f) : 2f;
        }
    }

    static UniversalRenderPipelineAsset cachedUrp;
    static bool cachedHdr;
    static int cachedMsaa;
    static float cachedScale;
    static float cachedShadowDistance;
    static int cachedCascades;
    static int cachedAdditionalLightCount;
    static bool urpCached;

    static void ConfigureUrpAsset(bool optimized)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
            return;

        if (!urpCached)
        {
            cachedUrp = urp;
            cachedHdr = urp.supportsHDR;
            cachedMsaa = urp.msaaSampleCount;
            cachedScale = urp.renderScale;
            cachedShadowDistance = urp.shadowDistance;
            cachedCascades = urp.shadowCascadeCount;
            cachedAdditionalLightCount = urp.maxAdditionalLightsCount;
            urpCached = true;
        }

        urp.supportsHDR = !optimized;
        urp.msaaSampleCount = optimized ? 4 : 8;
        urp.renderScale = optimized ? 0.9f : 1f;
        urp.shadowDistance = optimized ? 20f : 50f;
        urp.shadowCascadeCount = optimized ? 1 : 4;
        urp.maxAdditionalLightsCount = optimized ? 0 : 4;
    }

    public static void RestorePipeline()
    {
        if (!urpCached || cachedUrp == null)
            return;

        cachedUrp.supportsHDR = cachedHdr;
        cachedUrp.msaaSampleCount = cachedMsaa;
        cachedUrp.renderScale = cachedScale;
        cachedUrp.shadowDistance = cachedShadowDistance;
        cachedUrp.shadowCascadeCount = cachedCascades;
        cachedUrp.maxAdditionalLightsCount = cachedAdditionalLightCount;
        urpCached = false;
    }

    static void ConfigurePostProcessing(bool optimized)
    {
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume.profile == null)
                continue;

            if (volume.profile.TryGet(out Bloom bloom))
                bloom.active = !optimized;
            if (volume.profile.TryGet(out Vignette vignette))
                vignette.active = !optimized;
        }
    }

    static void TryEnableFoveation(float level)
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        for (int i = 0; i < displays.Count; i++)
        {
            XRDisplaySubsystem display = displays[i];
            if (display == null)
                continue;
            display.foveatedRenderingLevel = level;
        }
    }
}
