using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenu("TechShot/Ray Traced Shadows Volume")]
public class RayTracedShadowsVolume : VolumeComponent, IPostProcessComponent
{
    // General
    [Tooltip("Enable Ray Traced Shadows")]
    public BoolParameter enable = new BoolParameter(false, true);
    
    [Tooltip("Disable standard shadow maps when RTS is enabled")]
    public BoolParameter disableStandardShadows = new BoolParameter(true, true);

    // Quality
    [Tooltip("Shadow intensity (0 = no shadow, 1 = full shadow)")]
    public ClampedFloatParameter shadowIntensity = new ClampedFloatParameter(0.6f, 0f, 1f, true);
    
    [Tooltip("Shadow spread/softness")]
    public ClampedFloatParameter shadowSpread = new ClampedFloatParameter(0.5f, 0f, 1f, true);

    // Sampling
    [Tooltip("Use fixed sample count instead of adaptive")]
    public BoolParameter useFixedSampleCount = new BoolParameter(true, true);
    
    [Tooltip("Number of samples per pixel")]
    public ClampedIntParameter sampleCount = new ClampedIntParameter(16, 1, 64, true);

    // Occluder Distance
    [Tooltip("Use occluder distance for shadow softness")]
    public BoolParameter useOccluderDistance = new BoolParameter(true, true);
    
    [Tooltip("Scale factor for occluder distance effect")]
    public ClampedFloatParameter occluderDistanceScale = new ClampedFloatParameter(0.1f, 0f, 1f, true);

    // Distance
    [Tooltip("Maximum ray distance (0 = unlimited)")]
    public MinFloatParameter shadowMaxDistance = new MinFloatParameter(0f, 0f, true);
    
    [Tooltip("Maximum distance from camera for shadow receivers (0 = unlimited)")]
    public MinFloatParameter shadowReceiverMaxDistance = new MinFloatParameter(50f, 0f, true);
    
    [Tooltip("Shadow fade based on distance to occluder")]
    public ClampedFloatParameter shadowDistanceFade = new ClampedFloatParameter(0.5f, 0f, 20f, true);

    // Denoise
    [Tooltip("Enable temporal denoising")]
    public BoolParameter enableDenoise = new BoolParameter(true, true);
    
    [Tooltip("Denoise strength")]
    public ClampedFloatParameter denoiseStrength = new ClampedFloatParameter(1f, 0f, 1f, true);
    
    [Tooltip("Minimum blend factor to reduce ghosting")]
    public ClampedFloatParameter denoiseMinBlend = new ClampedFloatParameter(0.7f, 0f, 1f, true);

    public bool IsActive() => enable.value;
    public bool IsTileCompatible() => false;
    
    /// <summary>
    /// Get all lights marked for RTS processing
    /// </summary>
    public static List<Light> GetRTSLights()
    {
        var lights = new List<Light>();
        var rtsLightComponents = UnityEngine.Object.FindObjectsByType<RayTracedShadowsLight>(FindObjectsSortMode.None);
        
        foreach (var rtsLight in rtsLightComponents)
        {
            if (rtsLight.UseRayTracedShadows && rtsLight.Light != null)
                lights.Add(rtsLight.Light);
        }
        
        return lights;
    }
    
    /// <summary>
    /// Get the primary directional light for RTS
    /// </summary>
    public static Light GetPrimaryRTSLight()
    {
        // First try to get from RayTracedShadowsLight.MainLight
        if (RayTracedShadowsLight.MainLight != null && RayTracedShadowsLight.MainLight.Light != null)
            return RayTracedShadowsLight.MainLight.Light;
        
        // Fallback: find first directional light with RayTracedShadowsLight component
        var rtsLightComponents = UnityEngine.Object.FindObjectsByType<RayTracedShadowsLight>(FindObjectsSortMode.None);
        foreach (var rtsLight in rtsLightComponents)
        {
            if (rtsLight.UseRayTracedShadows && rtsLight.Light != null && rtsLight.Light.type == LightType.Directional)
                return rtsLight.Light;
        }
        
        return null;
    }
}
