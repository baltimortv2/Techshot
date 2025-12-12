using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Attach this component to a Light to control shadow behavior with RTS.
/// When RTS is enabled and UseRayTracedShadows is true, standard shadows are disabled.
/// </summary>
[RequireComponent(typeof(Light))]
[ExecuteAlways]
public class RayTracedShadowsLight : MonoBehaviour
{
    [SerializeField] 
    [Tooltip("When enabled, this light will use RTS instead of standard shadows (if RTS Volume is active)")]
    private bool _useRayTracedShadows = true;
    
    [SerializeField]
    [Tooltip("Shadow type to restore when RTS is disabled (Soft recommended)")]
    private LightShadows _cachedShadowType = LightShadows.Soft;
    
    [SerializeField]
    [Tooltip("Auto-cache shadow type on enable")]
    private bool _autoCacheShadowType = true;
    
    private Light _light;
    private bool _shadowsDisabledByRTS;
    private bool _initialized;
    
    private static RayTracedShadowsLight _mainLight;
    
    /// <summary>
    /// The main directional light for RTS
    /// </summary>
    public static RayTracedShadowsLight MainLight => _mainLight;
    
    /// <summary>
    /// The Light component
    /// </summary>
    public Light Light
    {
        get
        {
            if (_light == null)
                _light = GetComponent<Light>();
            return _light;
        }
    }
    
    /// <summary>
    /// Whether this light should use ray traced shadows
    /// </summary>
    public bool UseRayTracedShadows
    {
        get => _useRayTracedShadows;
        set
        {
            if (_useRayTracedShadows != value)
            {
                _useRayTracedShadows = value;
                UpdateShadowState();
            }
        }
    }
    
    /// <summary>
    /// Cached shadow type for restoration
    /// </summary>
    public LightShadows CachedShadowType
    {
        get => _cachedShadowType;
        set => _cachedShadowType = value;
    }

    private void Awake()
    {
        _light = GetComponent<Light>();
        
        // Cache shadow type on first awake if light has shadows
        if (_autoCacheShadowType && _light != null && _light.shadows != LightShadows.None)
            _cachedShadowType = _light.shadows;
        
        // Ensure we have a valid cached shadow type (default to Soft if None)
        if (_cachedShadowType == LightShadows.None)
            _cachedShadowType = LightShadows.Soft;
    }

    private void OnEnable()
    {
        if (_light == null)
            _light = GetComponent<Light>();
        
        // Cache shadow type if not yet cached
        if (_autoCacheShadowType && !_initialized && _light != null && _light.shadows != LightShadows.None)
            _cachedShadowType = _light.shadows;
        
        // Ensure we have a valid cached shadow type
        if (_cachedShadowType == LightShadows.None)
            _cachedShadowType = LightShadows.Soft;
        
        _initialized = true;
        
        // Register as main light if directional
        if (_light != null && _light.type == LightType.Directional && _mainLight == null)
            _mainLight = this;
        
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        UpdateShadowState();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        
        if (_mainLight == this)
            _mainLight = null;
        
        // Restore shadows when component is disabled
        ForceRestoreShadows();
    }

    private void OnValidate()
    {
        // React to Inspector changes
        if (_light == null)
            _light = GetComponent<Light>();
        
        // Delay to avoid issues during serialization
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                UpdateShadowState();
        };
        #else
        UpdateShadowState();
        #endif
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        UpdateShadowState();
    }
    
    private void UpdateShadowState()
    {
        if (Light == null)
            return;
        
        bool shouldUseRTS = ShouldUseRayTracedShadows();
        
        if (shouldUseRTS)
        {
            // Disable standard shadows for RTS
            if (!_shadowsDisabledByRTS)
            {
                // Save current shadow type before disabling (only if not None)
                if (Light.shadows != LightShadows.None)
                    _cachedShadowType = Light.shadows;
                
                Light.shadows = LightShadows.None;
                _shadowsDisabledByRTS = true;
            }
        }
        else
        {
            // Restore standard shadows
            if (_shadowsDisabledByRTS || Light.shadows == LightShadows.None)
            {
                Light.shadows = _cachedShadowType;
                _shadowsDisabledByRTS = false;
            }
        }
    }
    
    private bool ShouldUseRayTracedShadows()
    {
        // If UseRayTracedShadows is off on this light, use standard shadows
        if (!_useRayTracedShadows)
            return false;
        
        // Check Volume settings
        var stack = VolumeManager.instance?.stack;
        if (stack == null)
        {
            // Fallback: try to update stack
            VolumeManager.instance?.Update(transform, 0);
            stack = VolumeManager.instance?.stack;
            if (stack == null)
                return false;
        }
        
        var rtsVolume = stack.GetComponent<RayTracedShadowsVolume>();
        if (rtsVolume == null)
            return false;
        
        // RTS must be enabled in Volume
        if (!rtsVolume.IsActive())
            return false;
        
        // If disableStandardShadows is enabled, we should use RTS
        // Otherwise, both RTS and standard shadows can coexist
        return rtsVolume.disableStandardShadows.value;
    }
    
    /// <summary>
    /// Cache current shadow type from Light component
    /// </summary>
    [ContextMenu("Cache Current Shadow Type")]
    public void CacheCurrentShadowType()
    {
        if (Light != null && Light.shadows != LightShadows.None)
            _cachedShadowType = Light.shadows;
    }
    
    /// <summary>
    /// Force restore shadows immediately
    /// </summary>
    [ContextMenu("Force Restore Shadows")]
    public void ForceRestoreShadows()
    {
        if (Light != null)
        {
            Light.shadows = _cachedShadowType;
            _shadowsDisabledByRTS = false;
        }
    }
    
    /// <summary>
    /// Force enable RTS for this light
    /// </summary>
    [ContextMenu("Enable RTS")]
    public void EnableRTS()
    {
        UseRayTracedShadows = true;
    }
    
    /// <summary>
    /// Force disable RTS for this light (restore standard shadows)
    /// </summary>
    [ContextMenu("Disable RTS")]
    public void DisableRTS()
    {
        UseRayTracedShadows = false;
    }
}
