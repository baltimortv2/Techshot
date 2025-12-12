using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

/// <summary>
/// Ray Traced Shadows Renderer Feature.
/// Supports Directional and Spot lights.
/// All settings are controlled via RayTracedShadowsVolume in Volume Profile.
/// </summary>
public class RayTracedShadowPass : ScriptableRendererFeature
{
    [Header("Resources")]
    public Material material;
    public ComputeShader computeShader;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugForceConstantOutput = false;
    [Range(0f, 1f)]
    public float debugConstantOutput = 0.2f;

    private CustomRenderPass _scriptablePass;

    class CustomRenderPass : ScriptableRenderPass
    {
        public Material material;
        public ComputeShader computeShader;
        public bool debugLogs;
        public bool debugForceConstantOutput;
        public float debugConstantOutput;

        // Shared RTAS for all lights
        private RayTracingAccelerationStructure _sharedRTAS;
        private uint _cameraWidth;
        private uint _cameraHeight;
        private int _frameIndex;
        
        // Per-light temporal data
        private Dictionary<int, LightTemporalData> _lightTemporalData = new Dictionary<int, LightTemporalData>();
        
        private class LightTemporalData
        {
            public int temporalAccumulationStep;
            public Matrix4x4 prevLightMatrix = Matrix4x4.identity;
            public float prevShadowSpread;
            public float prevShadowIntensity;
        }

        private class PassData
        {
            public TextureHandle shadowMap;
            public TextureHandle depthTexture;
            public TextureHandle gbuffer2;

            public Material shadowMapBlitMat;
            public ComputeShader shadowMappingCS;
            public Light light;
            public int lightInstanceId;
            public int lightType; // 0 = Directional, 1 = Spot
            
            public float shadowSpread;
            public float shadowIntensity;
            public int useFixedSampleCount;
            public int fixedSampleCount;
            public int useOccluderDistance;
            public float occluderDistanceScale;
            public float shadowMaxDistance;
            public float shadowReceiverMaxDistance;
            public float shadowDistanceFade;
            public int enableDenoise;
            public float denoiseStrength;
            public float denoiseMinBlend;

            public bool debugLogs;
            public Camera cam;
            public RayTracingAccelerationStructure rtas;
            public bool rtasNeedsBuild;

            public int frameIndex;
            public Dictionary<int, LightTemporalData> lightTemporalData;
            public Matrix4x4 prevCameraMatrix;
            public Matrix4x4 prevProjMatrix;
            public bool transformsChanged;

            public int debugForceConstantOutput;
            public float debugConstantOutput;
        }

        // Camera tracking for temporal reset
        private Matrix4x4 _prevCameraMatrix = Matrix4x4.identity;
        private Matrix4x4 _prevProjMatrix = Matrix4x4.identity;
        private bool _rtasBuiltThisFrame;
        private bool _transformsChangedThisFrame;

        public void Cleanup()
        {
            if (_sharedRTAS != null)
            {
                _sharedRTAS.Dispose();
                _sharedRTAS = null;
            }
            _lightTemporalData.Clear();
        }

        private RayTracingAccelerationStructure GetOrCreateRTAS()
        {
            if (_sharedRTAS == null)
            {
                var settings = new RayTracingAccelerationStructure.Settings
                {
                    rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything,
                    managementMode = RayTracingAccelerationStructure.ManagementMode.Manual,
                    layerMask = 255
                };
                _sharedRTAS = new RayTracingAccelerationStructure(settings);
            }
            return _sharedRTAS;
        }

        private LightTemporalData GetOrCreateLightData(int lightInstanceId)
        {
            if (!_lightTemporalData.TryGetValue(lightInstanceId, out var data))
            {
                data = new LightTemporalData();
                _lightTemporalData[lightInstanceId] = data;
            }
            return data;
        }

        static void ExecuteComputePass(PassData data, ComputeGraphContext context)
        {
            try
            {
                if (!SystemInfo.supportsInlineRayTracing)
                {
                    if (data.debugLogs)
                        Debug.LogWarning("RTS: Inline Ray Tracing (DXR 1.1) not supported.");
                    return;
                }

                if (data.light == null)
                {
                    if (data.debugLogs)
                        Debug.LogWarning("RTS: No valid light assigned.");
                    return;
                }

                if (!data.cam) return;
                if (data.rtas == null) return;

                // Build RTAS only once per frame (first light triggers build)
                if (data.rtasNeedsBuild)
                {
                    context.cmd.BuildRayTracingAccelerationStructure(data.rtas);
                }

                // Get or create temporal data for this light
                var lightData = data.lightTemporalData.TryGetValue(data.lightInstanceId, out var ld) ? ld : new LightTemporalData();

                // Check if we need to reset temporal accumulation
                bool needsReset = data.frameIndex == 0 ||
                    data.prevCameraMatrix != data.cam.cameraToWorldMatrix ||
                    data.prevProjMatrix != data.cam.projectionMatrix ||
                    lightData.prevLightMatrix != data.light.transform.localToWorldMatrix ||
                    lightData.prevShadowSpread != data.shadowSpread ||
                    lightData.prevShadowIntensity != data.shadowIntensity ||
                    data.transformsChanged;

                if (needsReset)
                    lightData.temporalAccumulationStep = 0;

                int kernelIndex = data.shadowMappingCS.FindKernel("CSMain");
                if (kernelIndex == -1) return;

                if (!data.shadowMappingCS.IsSupported(kernelIndex))
                {
                    if (data.debugLogs)
                        Debug.LogWarning($"RTS: Compute shader {data.shadowMappingCS.name} not supported.");
                    return;
                }

                data.shadowMappingCS.GetKernelThreadGroupSizes(kernelIndex, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);

                float tanHalfFOV = Mathf.Tan(Mathf.Deg2Rad * data.cam.fieldOfView * 0.5f);
                float aspectRatio = data.cam.pixelWidth / (float)data.cam.pixelHeight;

                var depthToViewParams = new Vector4(
                    2.0f * tanHalfFOV * aspectRatio / data.cam.pixelWidth,
                    2.0f * tanHalfFOV / data.cam.pixelHeight,
                    tanHalfFOV * aspectRatio,
                    tanHalfFOV
                );

                // Common parameters
                context.cmd.SetComputeVectorParam(data.shadowMappingCS, "g_DepthToViewParams", depthToViewParams);
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_FrameIndex", data.frameIndex);
                context.cmd.SetComputeVectorParam(data.shadowMappingCS, "g_LightDir", data.light.transform.forward);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_ShadowSpread", data.shadowSpread * 0.1f);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_ShadowIntensity", data.shadowIntensity);
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_DebugForceConstantOutput", data.debugForceConstantOutput);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_DebugConstantOutput", data.debugConstantOutput);
                
                // Light type specific parameters
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_LightType", data.lightType);
                context.cmd.SetComputeVectorParam(data.shadowMappingCS, "g_LightPosition", data.light.transform.position);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_LightRange", data.light.range);
                
                // Spot light angles
                if (data.lightType == 1) // Spot
                {
                    float spotAngleRad = data.light.spotAngle * Mathf.Deg2Rad * 0.5f;
                    float innerSpotAngleRad = data.light.innerSpotAngle * Mathf.Deg2Rad * 0.5f;
                    context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_SpotAngleCos", Mathf.Cos(spotAngleRad));
                    context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_SpotInnerAngleCos", Mathf.Cos(innerSpotAngleRad));
                }
                else
                {
                    context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_SpotAngleCos", 0);
                    context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_SpotInnerAngleCos", 0);
                }
                
                // Quality parameters
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_UseFixedSampleCount", data.useFixedSampleCount);
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_FixedSampleCount", data.fixedSampleCount);
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_UseOccluderDistance", data.useOccluderDistance);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_OccluderDistanceScale", data.occluderDistanceScale);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_ShadowMaxDistance", data.shadowMaxDistance);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_ShadowReceiverMaxDistance", data.shadowReceiverMaxDistance);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_ShadowDistanceFade", data.shadowDistanceFade);
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_EnableDenoise", data.enableDenoise);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_DenoiseStrength", data.denoiseStrength);
                context.cmd.SetComputeFloatParam(data.shadowMappingCS, "g_DenoiseMinBlend", data.denoiseMinBlend);
                context.cmd.SetRayTracingAccelerationStructure(data.shadowMappingCS, kernelIndex, "g_AccelStruct", data.rtas);
                context.cmd.SetComputeIntParam(data.shadowMappingCS, "g_TemporalAccumulationStep", lightData.temporalAccumulationStep);
                context.cmd.SetComputeMatrixParam(data.shadowMappingCS, "g_CameraToWorld", data.cam.cameraToWorldMatrix);
                context.cmd.SetComputeTextureParam(data.shadowMappingCS, kernelIndex, "g_Output", data.shadowMap);
                context.cmd.SetComputeTextureParam(data.shadowMappingCS, kernelIndex, "_DepthBuffer", data.depthTexture);
                context.cmd.SetComputeTextureParam(data.shadowMappingCS, kernelIndex, "_GBuffer2", data.gbuffer2);

                Matrix4x4 lightMatrix = data.light.transform.localToWorldMatrix;
                lightMatrix.SetColumn(2, -lightMatrix.GetColumn(2));
                context.cmd.SetComputeMatrixParam(data.shadowMappingCS, "g_LightMatrix", lightMatrix);

                context.cmd.DispatchCompute(data.shadowMappingCS, kernelIndex,
                    (int)((data.cam.pixelWidth + threadGroupSizeX - 1) / threadGroupSizeX),
                    (int)((data.cam.pixelHeight + threadGroupSizeY - 1) / threadGroupSizeY), 1);

                // Update temporal data
                lightData.prevLightMatrix = data.light.transform.localToWorldMatrix;
                lightData.prevShadowSpread = data.shadowSpread;
                lightData.prevShadowIntensity = data.shadowIntensity;
                lightData.temporalAccumulationStep++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RTS ExecuteComputePass: {e}");
            }
        }

        static void ExecuteBlitPass(PassData data, RasterGraphContext context)
        {
            try
            {
                data.shadowMapBlitMat.SetFloat("_ShadowIntensity", data.shadowIntensity);
                Blitter.BlitTexture(context.cmd, data.shadowMap, new Vector4(1, 1, 0, 0), data.shadowMapBlitMat, 0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RTS ExecuteBlitPass: {e}");
            }
        }


        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (computeShader == null || material == null)
                return;

            // Get Volume settings
            var stack = VolumeManager.instance.stack;
            var rtsVolume = stack.GetComponent<RayTracedShadowsVolume>();
            
            // Skip if RTS is not enabled in Volume
            if (rtsVolume == null || !rtsVolume.IsActive())
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle depthTextureHandle = resourceData.cameraDepthTexture;
            if (!depthTextureHandle.IsValid())
            {
                if (debugLogs)
                    Debug.LogWarning("RTS: cameraDepthTexture is not valid.");
                return;
            }

            TextureHandle[] gBuffer = resourceData.gBuffer;
            if (gBuffer == null)
            {
                if (debugLogs)
                    Debug.LogWarning("RTS: resourceData.gBuffer is null (deferred GBuffers not accessible). Check renderer settings (Native Render Pass / framebuffer fetch).");
                return;
            }

            if (gBuffer.Length <= 2)
            {
                if (debugLogs)
                    Debug.LogWarning($"RTS: resourceData.gBuffer has unexpected length: {gBuffer.Length}.");
                return;
            }

            if (!gBuffer[2].IsValid())
            {
                if (debugLogs)
                    Debug.LogWarning("RTS: gBuffer[2] (normals/smoothness) is not valid.");
                return;
            }

            TextureHandle normalsHandle = gBuffer[2];

            // Get all RTS lights (only Directional and Spot)
            var rtsLights = new List<Light>();
            var allRtsLights = RayTracedShadowsVolume.GetRTSLights();
            foreach (var light in allRtsLights)
            {
                if (light != null && (light.type == LightType.Directional || light.type == LightType.Spot))
                    rtsLights.Add(light);
            }
            
            // Fallback to URP main light if no RTS lights
            if (rtsLights.Count == 0)
            {
                var lightData = frameData.Get<UniversalLightData>();
                int mainLightIndex = lightData.mainLightIndex;
                if (mainLightIndex >= 0 && mainLightIndex < lightData.visibleLights.Length)
                {
                    var mainLight = lightData.visibleLights[mainLightIndex].light;
                    if (mainLight != null && (mainLight.type == LightType.Directional || mainLight.type == LightType.Spot))
                        rtsLights.Add(mainLight);
                }
            }

            if (rtsLights.Count == 0)
            {
                if (debugLogs)
                    Debug.LogWarning("RTS: No valid lights found.");
                return;
            }

            if (debugLogs)
            {
                for (int i = 0; i < rtsLights.Count; i++)
                {
                    var l = rtsLights[i];
                    if (l == null) continue;
                    if (l.type == LightType.Spot)
                    {
                        Debug.Log($"RTS: Light[{i}] Spot '{l.name}' range={l.range} spotAngle={l.spotAngle} innerSpotAngle={l.innerSpotAngle}");
                    }
                    else
                    {
                        Debug.Log($"RTS: Light[{i}] {l.type} '{l.name}'");
                    }
                }
            }

            // Check camera resolution change
            if (_cameraWidth != cameraData.camera.pixelWidth || _cameraHeight != cameraData.camera.pixelHeight)
            {
                _cameraWidth = (uint)cameraData.camera.pixelWidth;
                _cameraHeight = (uint)cameraData.camera.pixelHeight;
                // Reset all temporal data on resolution change
                foreach (var kvp in _lightTemporalData)
                    kvp.Value.temporalAccumulationStep = 0;
            }

            // Build RTAS once per frame
            var rtas = GetOrCreateRTAS();
            
            var cullingConfig = new RayTracingInstanceCullingConfig
            {
                flags = RayTracingInstanceCullingFlags.EnableLODCulling
            };
            cullingConfig.lodParameters.fieldOfView = cameraData.camera.fieldOfView;
            cullingConfig.lodParameters.cameraPixelHeight = cameraData.camera.pixelHeight;
            cullingConfig.lodParameters.isOrthographic = false;
            cullingConfig.lodParameters.cameraPosition = cameraData.camera.transform.position;
            cullingConfig.subMeshFlagsConfig.opaqueMaterials = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly;
            cullingConfig.subMeshFlagsConfig.transparentMaterials = RayTracingSubMeshFlags.Disabled;
            cullingConfig.subMeshFlagsConfig.alphaTestedMaterials = RayTracingSubMeshFlags.Disabled;

            var instanceTest = new RayTracingInstanceCullingTest
            {
                allowOpaqueMaterials = true,
                allowAlphaTestedMaterials = true,
                allowTransparentMaterials = false,
                layerMask = -1,
                shadowCastingModeMask = (1 << (int)ShadowCastingMode.Off) | (1 << (int)ShadowCastingMode.On) | (1 << (int)ShadowCastingMode.TwoSided),
                instanceMask = 1 << 0
            };
            cullingConfig.instanceTests = new[] { instanceTest };

            rtas.ClearInstances();
            var cullingResults = rtas.CullInstances(ref cullingConfig);
            
            _transformsChangedThisFrame = cullingResults.transformsChanged;
            _rtasBuiltThisFrame = false;

            // Get settings from Volume
            float intensity = rtsVolume.shadowIntensity.value;
            float spread = rtsVolume.shadowSpread.value;
            bool useFixed = rtsVolume.useFixedSampleCount.value;
            int samples = rtsVolume.sampleCount.value;
            bool useOccluder = rtsVolume.useOccluderDistance.value;
            float occluderScale = rtsVolume.occluderDistanceScale.value;
            float maxDist = rtsVolume.shadowMaxDistance.value;
            float receiverMaxDist = rtsVolume.shadowReceiverMaxDistance.value;
            float distFade = rtsVolume.shadowDistanceFade.value;
            bool denoise = rtsVolume.enableDenoise.value;
            float denoiseStr = rtsVolume.denoiseStrength.value;
            float denoiseMin = rtsVolume.denoiseMinBlend.value;

            // Process each light
            for (int i = 0; i < rtsLights.Count; i++)
            {
                var light = rtsLights[i];
                if (light == null) continue;

                int lightInstanceId = light.GetInstanceID();
                GetOrCreateLightData(lightInstanceId);

                var desc = new TextureDesc(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight)
                {
                    name = $"RTS_Shadowmap_{i}",
                    colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat,
                    enableRandomWrite = true
                };
                TextureHandle shadowTextureHandle = renderGraph.CreateTexture(desc);

                int lightType = light.type == LightType.Directional ? 0 : 1;

                // Compute pass
                using (var builder = renderGraph.AddComputePass<PassData>($"RTS Compute Shadows {light.name}", out var passData))
                {
                    passData.light = light;
                    passData.lightInstanceId = lightInstanceId;
                    passData.lightType = lightType;
                    passData.shadowMap = shadowTextureHandle;
                    passData.depthTexture = depthTextureHandle;
                    passData.gbuffer2 = normalsHandle;
                    passData.shadowMappingCS = computeShader;
                    passData.shadowSpread = spread;
                    passData.shadowIntensity = intensity;
                    passData.useFixedSampleCount = useFixed ? 1 : 0;
                    passData.fixedSampleCount = samples;
                    passData.useOccluderDistance = useOccluder ? 1 : 0;
                    passData.occluderDistanceScale = occluderScale;
                    passData.shadowMaxDistance = maxDist;
                    passData.shadowReceiverMaxDistance = receiverMaxDist;
                    passData.shadowDistanceFade = distFade;
                    passData.enableDenoise = denoise ? 1 : 0;
                    passData.denoiseStrength = denoiseStr;
                    passData.denoiseMinBlend = denoiseMin;
                    passData.cam = cameraData.camera;
                    passData.debugLogs = debugLogs;
                    passData.rtas = rtas;
                    passData.rtasNeedsBuild = !_rtasBuiltThisFrame;
                    passData.frameIndex = _frameIndex;
                    passData.lightTemporalData = _lightTemporalData;
                    passData.prevCameraMatrix = _prevCameraMatrix;
                    passData.prevProjMatrix = _prevProjMatrix;
                    passData.transformsChanged = _transformsChangedThisFrame;

                    passData.debugForceConstantOutput = debugForceConstantOutput ? 1 : 0;
                    passData.debugConstantOutput = debugConstantOutput;

                    builder.UseTexture(passData.gbuffer2, AccessFlags.Read);
                    builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                    builder.UseTexture(passData.shadowMap, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData data, ComputeGraphContext context) => ExecuteComputePass(data, context));
                    
                    _rtasBuiltThisFrame = true;
                }

                // Blit pass
                using (var builder = renderGraph.AddRasterRenderPass<PassData>($"RTS Apply Shadows {light.name}", out var passData))
                {
                    passData.shadowMapBlitMat = material;
                    passData.shadowMap = shadowTextureHandle;
                    passData.shadowIntensity = intensity;

                    builder.UseTexture(passData.shadowMap, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecuteBlitPass(data, context));
                }
            }

            // Update camera tracking
            _prevCameraMatrix = cameraData.camera.cameraToWorldMatrix;
            _prevProjMatrix = cameraData.camera.projectionMatrix;
            _frameIndex++;
        }
    }

    public override void Create()
    {
        _scriptablePass = new CustomRenderPass
        {
            material = material,
            computeShader = computeShader,
            debugLogs = debugLogs,
            debugForceConstantOutput = debugForceConstantOutput,
            debugConstantOutput = debugConstantOutput,
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };
    }

    protected override void Dispose(bool disposing)
    {
        _scriptablePass?.Cleanup();
        base.Dispose(disposing);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_scriptablePass == null)
        {
            Create();
            if (_scriptablePass == null) return;
        }

        if (material == null || computeShader == null) return;

        _scriptablePass.material = material;
        _scriptablePass.computeShader = computeShader;
        _scriptablePass.debugLogs = debugLogs;
        _scriptablePass.debugForceConstantOutput = debugForceConstantOutput;
        _scriptablePass.debugConstantOutput = debugConstantOutput;

        renderer.EnqueuePass(_scriptablePass);
    }
}
