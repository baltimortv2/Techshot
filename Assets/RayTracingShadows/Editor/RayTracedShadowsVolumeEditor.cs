using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(RayTracedShadowsVolume))]
public class RayTracedShadowsVolumeEditor : VolumeComponentEditor
{
    private SerializedDataParameter _enable;
    private SerializedDataParameter _disableStandardShadows;
    private SerializedDataParameter _shadowIntensity;
    private SerializedDataParameter _shadowSpread;
    private SerializedDataParameter _useFixedSampleCount;
    private SerializedDataParameter _sampleCount;
    private SerializedDataParameter _useOccluderDistance;
    private SerializedDataParameter _occluderDistanceScale;
    private SerializedDataParameter _shadowMaxDistance;
    private SerializedDataParameter _shadowReceiverMaxDistance;
    private SerializedDataParameter _shadowDistanceFade;
    private SerializedDataParameter _enableDenoise;
    private SerializedDataParameter _denoiseStrength;
    private SerializedDataParameter _denoiseMinBlend;

    public override void OnEnable()
    {
        var o = new PropertyFetcher<RayTracedShadowsVolume>(serializedObject);
        
        _enable = Unpack(o.Find(x => x.enable));
        _disableStandardShadows = Unpack(o.Find(x => x.disableStandardShadows));
        _shadowIntensity = Unpack(o.Find(x => x.shadowIntensity));
        _shadowSpread = Unpack(o.Find(x => x.shadowSpread));
        _useFixedSampleCount = Unpack(o.Find(x => x.useFixedSampleCount));
        _sampleCount = Unpack(o.Find(x => x.sampleCount));
        _useOccluderDistance = Unpack(o.Find(x => x.useOccluderDistance));
        _occluderDistanceScale = Unpack(o.Find(x => x.occluderDistanceScale));
        _shadowMaxDistance = Unpack(o.Find(x => x.shadowMaxDistance));
        _shadowReceiverMaxDistance = Unpack(o.Find(x => x.shadowReceiverMaxDistance));
        _shadowDistanceFade = Unpack(o.Find(x => x.shadowDistanceFade));
        _enableDenoise = Unpack(o.Find(x => x.enableDenoise));
        _denoiseStrength = Unpack(o.Find(x => x.denoiseStrength));
        _denoiseMinBlend = Unpack(o.Find(x => x.denoiseMinBlend));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // General
        EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
        DrawPropertyNoOverride(_enable, "Enable");
        DrawPropertyNoOverride(_disableStandardShadows, "Disable Standard Shadows");
        
        EditorGUILayout.Space();
        
        // Quality
        EditorGUILayout.LabelField("Quality", EditorStyles.boldLabel);
        DrawPropertyNoOverride(_shadowIntensity, "Shadow Intensity");
        DrawPropertyNoOverride(_shadowSpread, "Shadow Spread");
        
        EditorGUILayout.Space();
        
        // Sampling
        EditorGUILayout.LabelField("Sampling", EditorStyles.boldLabel);
        DrawPropertyNoOverride(_useFixedSampleCount, "Use Fixed Sample Count");
        DrawPropertyNoOverride(_sampleCount, "Sample Count");
        
        EditorGUILayout.Space();
        
        // Occluder
        EditorGUILayout.LabelField("Occluder Distance", EditorStyles.boldLabel);
        DrawPropertyNoOverride(_useOccluderDistance, "Use Occluder Distance");
        DrawPropertyNoOverride(_occluderDistanceScale, "Occluder Distance Scale");
        
        EditorGUILayout.Space();
        
        // Distance
        EditorGUILayout.LabelField("Distance", EditorStyles.boldLabel);
        DrawPropertyNoOverride(_shadowMaxDistance, "Shadow Max Distance");
        DrawPropertyNoOverride(_shadowReceiverMaxDistance, "Shadow Receiver Max Distance");
        DrawPropertyNoOverride(_shadowDistanceFade, "Shadow Distance Fade");
        
        EditorGUILayout.Space();
        
        // Denoise
        EditorGUILayout.LabelField("Denoise", EditorStyles.boldLabel);
        DrawPropertyNoOverride(_enableDenoise, "Enable Denoise");
        DrawPropertyNoOverride(_denoiseStrength, "Denoise Strength");
        DrawPropertyNoOverride(_denoiseMinBlend, "Denoise Min Blend");
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawPropertyNoOverride(SerializedDataParameter param, string label)
    {
        // Draw only the value property without the override checkbox
        EditorGUILayout.PropertyField(param.value, new GUIContent(label));
    }
}
