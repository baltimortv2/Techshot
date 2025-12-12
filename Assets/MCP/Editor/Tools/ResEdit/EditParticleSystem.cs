using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
// Migrated from Newtonsoft.Json to SimpleJson
using UnityEditor;
using UnityEngine;
using UniMcp.Models;

namespace UniMcp.Tools
{
    /// <summary>
    /// ???????????,??????????????????
    /// ?????: edit_particle_system
    /// </summary>
    [ToolName("edit_particle_system", "Resources")]
    public class EditParticleSystem : DualStateMethodBase
    {
        private IObjectSelector objectSelector;

        /// <summary>
        /// ??????????????
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                // ??????
                new MethodKey("instance_id", "GameObject???ID", false),
                new MethodKey("path", "GameObject???????", false),
                
                // ????
                new MethodKey("action", "????:init_component, get_properties, set_properties, play, pause, stop, clear, simulate, restart", false),
                
                // ?????
                new MethodKey("duration", "????????"),
                new MethodKey("looping", "??????"),
                new MethodKey("prewarm", "????"),
                new MethodKey("start_delay", "????"),
                new MethodKey("start_lifetime", "??????"),
                new MethodKey("start_speed", "??????"),
                new MethodKey("start_size", "??????"),
                new MethodKey("start_rotation", "??????"),
                new MethodKey("start_color", "?????? [r,g,b,a]"),
                new MethodKey("gravity_modifier", "??????"),
                new MethodKey("simulation_space", "????:Local, World, Custom"),
                new MethodKey("simulation_speed", "????"),
                new MethodKey("scaling_mode", "????:Hierarchy, Local, Shape"),
                new MethodKey("play_on_awake", "?????"),
                new MethodKey("max_particles", "?????"),
                
                // ????
                new MethodKey("emission_enabled", "??????"),
                new MethodKey("emission_rate_over_time", "?????"),
                new MethodKey("emission_rate_over_distance", "????????"),
                
                // ????
                new MethodKey("shape_enabled", "????????"),
                new MethodKey("shape_type", "????:Sphere, Hemisphere, Cone, Box, Circle, Edge, Rectangle"),
                new MethodKey("shape_angle", "????"),
                new MethodKey("shape_radius", "??"),
                new MethodKey("shape_box_thickness", "???? [x,y,z]"),
                new MethodKey("shape_arc", "????"),
                new MethodKey("shape_random_direction", "????"),
                
                // ????
                new MethodKey("velocity_over_lifetime_enabled", "??????????"),
                new MethodKey("velocity_linear", "???? [x,y,z]"),
                new MethodKey("velocity_orbital", "???? [x,y,z]"),
                
                // ??????
                new MethodKey("limit_velocity_enabled", "????????"),
                new MethodKey("limit_velocity_dampen", "????"),
                
                // ????
                new MethodKey("force_over_lifetime_enabled", "?????????"),
                new MethodKey("force_x", "X??"),
                new MethodKey("force_y", "Y??"),
                new MethodKey("force_z", "Z??"),
                
                // ????
                new MethodKey("color_over_lifetime_enabled", "??????????"),
                new MethodKey("color_gradient", "??????"),
                
                // ????
                new MethodKey("size_over_lifetime_enabled", "??????????"),
                new MethodKey("size_curve", "??????"),
                
                // ????
                new MethodKey("rotation_over_lifetime_enabled", "??????????"),
                new MethodKey("rotation_angular_velocity", "???"),
                
                // ????
                new MethodKey("noise_enabled", "??????"),
                new MethodKey("noise_strength", "????"),
                new MethodKey("noise_frequency", "????"),
                
                // ????
                new MethodKey("collision_enabled", "??????"),
                new MethodKey("collision_type", "????:Planes, World"),
                new MethodKey("collision_dampen", "????"),
                new MethodKey("collision_bounce", "????"),
                
                // ????
                new MethodKey("render_mode", "????:Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh"),
                new MethodKey("material", "????"),
                new MethodKey("trail_material", "??????"),
                new MethodKey("sorting_layer", "???"),
                new MethodKey("sorting_order", "????"),
                
                // ???????
                new MethodKey("texture_sheet_animation_enabled", "?????????"),
                new MethodKey("texture_sheet_tiles", "????? [x,y]"),
                new MethodKey("texture_sheet_animation_type", "????:WholeSheet, SingleRow"),
                new MethodKey("texture_sheet_fps", "????"),
                
                // ??????
                new MethodKey("sub_emitters_enabled", "????????"),
                
                // ????
                new MethodKey("lights_enabled", "??????"),
                new MethodKey("lights_ratio", "????"),
                
                // ????
                new MethodKey("trails_enabled", "??????"),
                new MethodKey("trails_ratio", "????"),
                new MethodKey("trails_lifetime", "??????"),
                
                // ????
                new MethodKey("simulate_time", "????(?)"),
                new MethodKey("with_children", "?????????"),
                new MethodKey("restart_mode", "????:Default, Fast")
            };
        }

        /// <summary>
        /// ?????????
        /// </summary>
        protected override StateTree CreateTargetTree()
        {
            objectSelector = objectSelector ?? new HierarchySelector<GameObject>();
            return objectSelector.BuildStateTree();
        }

        /// <summary>
        /// ?????????????
        /// </summary>
        protected override StateTree CreateActionTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("init_component", (Func<StateTreeContext, object>)HandleInitComponentAction)
                    .Leaf("get_properties", (Func<StateTreeContext, object>)HandleGetPropertiesAction)
                    .Leaf("set_properties", (Func<StateTreeContext, object>)HandleSetPropertiesAction)
                    .Leaf("play", (Func<StateTreeContext, object>)HandlePlayAction)
                    .Leaf("pause", (Func<StateTreeContext, object>)HandlePauseAction)
                    .Leaf("stop", (Func<StateTreeContext, object>)HandleStopAction)
                    .Leaf("clear", (Func<StateTreeContext, object>)HandleClearAction)
                    .Leaf("simulate", (Func<StateTreeContext, object>)HandleSimulateAction)
                    .Leaf("restart", (Func<StateTreeContext, object>)HandleRestartAction)
                    .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultAction)
                .Build();
        }

        // --- ???? ---

        private GameObject ExtractTargetFromContext(StateTreeContext context)
        {
            if (context.TryGetObjectReference("_resolved_targets", out object targetsObj))
            {
                if (targetsObj is GameObject singleGameObject)
                {
                    return singleGameObject;
                }
                else if (targetsObj is GameObject[] gameObjectArray && gameObjectArray.Length > 0)
                {
                    return gameObjectArray[0];
                }
                else if (targetsObj is System.Collections.IList list && list.Count > 0)
                {
                    if (list[0] is GameObject go)
                        return go;
                }
            }

            if (context.TryGetJsonValue("_resolved_targets", out JsonNode targetToken))
            {
                if (targetToken is JsonArray arr && arr.Count > 0)
                {
                    int instanceId = arr[0].AsInt;
                    return EditorUtility.EntityIdToObject(instanceId) as GameObject;
                }
                else if (targetToken.type == JsonNodeType.Integer)
                {
                    int instanceId = targetToken.AsInt;
                    return EditorUtility.EntityIdToObject(instanceId) as GameObject;
                }
            }

            return null;
        }

        // --- ???? ---

        private object HandleDefaultAction(StateTreeContext context)
        {
            JsonClass args = context.JsonData;
            if (args.ContainsKey("duration") || args.ContainsKey("start_lifetime") ||
                args.ContainsKey("emission_rate_over_time") || args.ContainsKey("start_color"))
            {
                return HandleSetPropertiesAction(context);
            }
            return Response.Error("Action is required. Valid actions: init_component, get_properties, set_properties, play, pause, stop, clear, simulate, restart");
        }

        private object HandleInitComponentAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            try
            {
                ParticleSystem ps = target.GetComponent<ParticleSystem>();
                bool isNewComponent = false;

                // ???????????,???
                if (ps == null)
                {
                    ps = Undo.AddComponent<ParticleSystem>(target);
                    isNewComponent = true;
                    McpLogger.Log($"[EditParticleSystem] Added ParticleSystem component to '{target.name}'");
                }
                else
                {
                    Undo.RecordObject(ps, "Initialize ParticleSystem");
                    McpLogger.Log($"[EditParticleSystem] Found existing ParticleSystem on '{target.name}', initializing properties");
                }

                // ??????
                JsonClass args = context.JsonData;
                if (args.Count > 0)
                {
                    ApplyParticleSystemProperties(ps, args);
                }

                string message = isNewComponent
                    ? $"ParticleSystem added and initialized on '{target.name}'."
                    : $"ParticleSystem initialized on '{target.name}'.";

                return Response.Success(message, GetParticleSystemData(ps));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to initialize ParticleSystem: {e.Message}");
            }
        }

        private object HandleGetPropertiesAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            return Response.Success($"ParticleSystem properties retrieved from '{target.name}'.", GetParticleSystemData(ps));
        }

        private object HandleSetPropertiesAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            try
            {
                Undo.RecordObject(ps, "Set ParticleSystem Properties");
                JsonClass args = context.JsonData;
                ApplyParticleSystemProperties(ps, args);

                McpLogger.Log($"[EditParticleSystem] Set properties on '{target.name}'");
                return Response.Success($"ParticleSystem properties updated on '{target.name}'.", GetParticleSystemData(ps));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to set ParticleSystem properties: {e.Message}");
            }
        }

        private object HandlePlayAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            JsonClass args = context.JsonData;
            bool withChildren = args["with_children"].AsBoolDefault(true);

            ps.Play(withChildren);
            McpLogger.Log($"[EditParticleSystem] Playing ParticleSystem on '{target.name}'");

            return Response.Success($"ParticleSystem playing on '{target.name}'.", new JsonClass
            {
                ["isPlaying"] = ps.isPlaying,
                ["isPaused"] = ps.isPaused,
                ["isStopped"] = ps.isStopped
            });
        }

        private object HandlePauseAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            JsonClass args = context.JsonData;
            bool withChildren = args["with_children"].AsBoolDefault(true);

            ps.Pause(withChildren);
            McpLogger.Log($"[EditParticleSystem] Paused ParticleSystem on '{target.name}'");

            return Response.Success($"ParticleSystem paused on '{target.name}'.", new JsonClass
            {
                ["isPlaying"] = ps.isPlaying,
                ["isPaused"] = ps.isPaused
            });
        }

        private object HandleStopAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            JsonClass args = context.JsonData;
            bool withChildren = args["with_children"].AsBoolDefault(true);

            ps.Stop(withChildren);
            McpLogger.Log($"[EditParticleSystem] Stopped ParticleSystem on '{target.name}'");

            return Response.Success($"ParticleSystem stopped on '{target.name}'.", new JsonClass
            {
                ["isStopped"] = ps.isStopped
            });
        }

        private object HandleClearAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            JsonClass args = context.JsonData;
            bool withChildren = args["with_children"].AsBoolDefault(true);

            ps.Clear(withChildren);
            McpLogger.Log($"[EditParticleSystem] Cleared ParticleSystem on '{target.name}'");

            return Response.Success($"ParticleSystem cleared on '{target.name}'.");
        }

        private object HandleSimulateAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            JsonClass args = context.JsonData;
            float time = args["simulate_time"].AsFloatDefault(1.0f);
            bool withChildren = args["with_children"].AsBoolDefault(true);

            ps.Simulate(time, withChildren, true);
            McpLogger.Log($"[EditParticleSystem] Simulated {time}s on '{target.name}'");

            return Response.Success($"ParticleSystem simulated {time}s on '{target.name}'.", new JsonClass
            {
                ["time"] = ps.time,
                ["particleCount"] = ps.particleCount
            });
        }

        private object HandleRestartAction(StateTreeContext context)
        {
            GameObject target = ExtractTargetFromContext(context);
            if (target == null)
                return Response.Error("Target GameObject not found.");

            ParticleSystem ps = target.GetComponent<ParticleSystem>();
            if (ps == null)
                return Response.Error($"No ParticleSystem found on '{target.name}'.");

            JsonClass args = context.JsonData;
            bool withChildren = args["with_children"].AsBoolDefault(true);

            ps.Stop(withChildren);
            ps.Clear(withChildren);
            ps.Play(withChildren);

            McpLogger.Log($"[EditParticleSystem] Restarted ParticleSystem on '{target.name}'");
            return Response.Success($"ParticleSystem restarted on '{target.name}'.");
        }

        // --- ????????? ---

        private void ApplyParticleSystemProperties(ParticleSystem ps, JsonClass args)
        {
            var main = ps.main;

            // ?????
            if (args.TryGetValue("duration", out JsonNode durationToken))
                main.duration = durationToken.AsFloat;

            if (args.TryGetValue("looping", out JsonNode loopingToken))
                main.loop = loopingToken.AsBool;

            if (args.TryGetValue("prewarm", out JsonNode prewarmToken))
                main.prewarm = prewarmToken.AsBool;

            if (args.TryGetValue("start_delay", out JsonNode delayToken))
                main.startDelay = delayToken.AsFloat;

            if (args.TryGetValue("start_lifetime", out JsonNode lifetimeToken))
                main.startLifetime = lifetimeToken.AsFloat;

            if (args.TryGetValue("start_speed", out JsonNode speedToken))
                main.startSpeed = speedToken.AsFloat;

            if (args.TryGetValue("start_size", out JsonNode sizeToken))
                main.startSize = sizeToken.AsFloat;

            if (args.TryGetValue("start_rotation", out JsonNode rotationToken))
                main.startRotation = rotationToken.AsFloat * Mathf.Deg2Rad;

            if (args.TryGetValue("start_color", out JsonNode colorToken))
            {
                JsonArray colorJsonArray = colorToken as JsonArray;
                if (colorJsonArray != null && colorJsonArray.Count >= 3)
                {
                    main.startColor = new Color(
                        colorJsonArray[0].AsFloat,
                        colorJsonArray[1].AsFloat,
                        colorJsonArray[2].AsFloat,
                        colorJsonArray.Count > 3 ? colorJsonArray[3].AsFloat : 1.0f
                    );
                }
            }

            if (args.TryGetValue("gravity_modifier", out JsonNode gravityToken))
                main.gravityModifier = gravityToken.AsFloat;

            if (args.TryGetValue("simulation_space", out JsonNode simSpaceToken))
            {
                if (Enum.TryParse(simSpaceToken.Value, out ParticleSystemSimulationSpace simSpace))
                    main.simulationSpace = simSpace;
            }

            if (args.TryGetValue("simulation_speed", out JsonNode simSpeedToken))
                main.simulationSpeed = simSpeedToken.AsFloat;

            if (args.TryGetValue("scaling_mode", out JsonNode scalingToken))
            {
                if (Enum.TryParse(scalingToken.Value, out ParticleSystemScalingMode scalingMode))
                    main.scalingMode = scalingMode;
            }

            if (args.TryGetValue("play_on_awake", out JsonNode playOnAwakeToken))
                main.playOnAwake = playOnAwakeToken.AsBool;

            if (args.TryGetValue("max_particles", out JsonNode maxParticlesToken))
                main.maxParticles = maxParticlesToken.AsInt;

            // ????
            if (args.ContainsKey("emission_enabled") || args.ContainsKey("emission_rate_over_time") ||
                args.ContainsKey("emission_rate_over_distance"))
            {
                var emission = ps.emission;

                if (args.TryGetValue("emission_enabled", out JsonNode emissionEnabledToken))
                    emission.enabled = emissionEnabledToken.AsBool;

                if (args.TryGetValue("emission_rate_over_time", out JsonNode rateTimeToken))
                    emission.rateOverTime = rateTimeToken.AsFloat;

                if (args.TryGetValue("emission_rate_over_distance", out JsonNode rateDistToken))
                    emission.rateOverDistance = rateDistToken.AsFloat;
            }

            // ????
            if (args.ContainsKey("shape_enabled") || args.ContainsKey("shape_type") ||
                args.ContainsKey("shape_radius") || args.ContainsKey("shape_angle"))
            {
                var shape = ps.shape;

                if (args.TryGetValue("shape_enabled", out JsonNode shapeEnabledToken))
                    shape.enabled = shapeEnabledToken.AsBool;

                if (args.TryGetValue("shape_type", out JsonNode shapeTypeToken))
                {
                    if (Enum.TryParse(shapeTypeToken.Value, out ParticleSystemShapeType shapeType))
                        shape.shapeType = shapeType;
                }

                if (args.TryGetValue("shape_angle", out JsonNode angleToken))
                    shape.angle = angleToken.AsFloat;

                if (args.TryGetValue("shape_radius", out JsonNode radiusToken))
                    shape.radius = radiusToken.AsFloat;

                if (args.TryGetValue("shape_arc", out JsonNode arcToken))
                    shape.arc = arcToken.AsFloat;

                if (args.TryGetValue("shape_random_direction", out JsonNode randomDirToken))
                    shape.randomDirectionAmount = randomDirToken.AsFloat;
            }

            // ????
            if (args.ContainsKey("velocity_over_lifetime_enabled") || args.ContainsKey("velocity_linear"))
            {
                var velocity = ps.velocityOverLifetime;

                if (args.TryGetValue("velocity_over_lifetime_enabled", out JsonNode velEnabledToken))
                    velocity.enabled = velEnabledToken.AsBool;

                if (args.TryGetValue("velocity_linear", out JsonNode linearToken))
                {
                    JsonArray linearArray = linearToken as JsonArray;
                    if (linearArray != null && linearArray.Count >= 3)
                    {
                        velocity.x = linearArray[0].AsFloat;
                        velocity.y = linearArray[1].AsFloat;
                        velocity.z = linearArray[2].AsFloat;
                    }
                }

                if (args.TryGetValue("velocity_orbital", out JsonNode orbitalToken))
                {
                    JsonArray orbitalArray = orbitalToken as JsonArray;
                    if (orbitalArray != null && orbitalArray.Count >= 3)
                    {
                        velocity.orbitalX = orbitalArray[0].AsFloat;
                        velocity.orbitalY = orbitalArray[1].AsFloat;
                        velocity.orbitalZ = orbitalArray[2].AsFloat;
                    }
                }
            }

            // ????
            if (args.ContainsKey("color_over_lifetime_enabled"))
            {
                var colorOverLifetime = ps.colorOverLifetime;

                if (args.TryGetValue("color_over_lifetime_enabled", out JsonNode colorEnabledToken))
                    colorOverLifetime.enabled = colorEnabledToken.AsBool;
            }

            // ????
            if (args.ContainsKey("size_over_lifetime_enabled"))
            {
                var sizeOverLifetime = ps.sizeOverLifetime;

                if (args.TryGetValue("size_over_lifetime_enabled", out JsonNode sizeEnabledToken))
                    sizeOverLifetime.enabled = sizeEnabledToken.AsBool;
            }

            // ????
            if (args.ContainsKey("rotation_over_lifetime_enabled") || args.ContainsKey("rotation_angular_velocity"))
            {
                var rotationOverLifetime = ps.rotationOverLifetime;

                if (args.TryGetValue("rotation_over_lifetime_enabled", out JsonNode rotEnabledToken))
                    rotationOverLifetime.enabled = rotEnabledToken.AsBool;

                if (args.TryGetValue("rotation_angular_velocity", out JsonNode angVelToken))
                    rotationOverLifetime.z = angVelToken.AsFloat * Mathf.Deg2Rad;
            }

            // ????
            if (args.ContainsKey("collision_enabled") || args.ContainsKey("collision_type"))
            {
                var collision = ps.collision;

                if (args.TryGetValue("collision_enabled", out JsonNode collisionEnabledToken))
                    collision.enabled = collisionEnabledToken.AsBool;

                if (args.TryGetValue("collision_type", out JsonNode collisionTypeToken))
                {
                    if (Enum.TryParse(collisionTypeToken.Value, out ParticleSystemCollisionType collisionType))
                        collision.type = collisionType;
                }

                if (args.TryGetValue("collision_dampen", out JsonNode dampenToken))
                    collision.dampen = dampenToken.AsFloat;

                if (args.TryGetValue("collision_bounce", out JsonNode bounceToken))
                    collision.bounce = bounceToken.AsFloat;
            }

            // ????
            if (args.ContainsKey("noise_enabled") || args.ContainsKey("noise_strength"))
            {
                var noise = ps.noise;

                if (args.TryGetValue("noise_enabled", out JsonNode noiseEnabledToken))
                    noise.enabled = noiseEnabledToken.AsBool;

                if (args.TryGetValue("noise_strength", out JsonNode strengthToken))
                    noise.strength = strengthToken.AsFloat;

                if (args.TryGetValue("noise_frequency", out JsonNode freqToken))
                    noise.frequency = freqToken.AsFloat;
            }

            // ?????
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                if (args.TryGetValue("render_mode", out JsonNode renderModeToken))
                {
                    if (Enum.TryParse(renderModeToken.Value, out ParticleSystemRenderMode renderMode))
                        renderer.renderMode = renderMode;
                }

                if (args.TryGetValue("material", out JsonNode materialToken))
                {
                    string materialPath = materialToken.Value;
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (mat != null)
                        renderer.material = mat;
                }

                if (args.TryGetValue("sorting_layer", out JsonNode sortingLayerToken))
                    renderer.sortingLayerName = sortingLayerToken.Value;

                if (args.TryGetValue("sorting_order", out JsonNode sortingOrderToken))
                    renderer.sortingOrder = sortingOrderToken.AsInt;
            }

            // ?????
            if (args.ContainsKey("texture_sheet_animation_enabled") || args.ContainsKey("texture_sheet_tiles"))
            {
                var textureSheet = ps.textureSheetAnimation;

                if (args.TryGetValue("texture_sheet_animation_enabled", out JsonNode texSheetEnabledToken))
                    textureSheet.enabled = texSheetEnabledToken.AsBool;

                if (args.TryGetValue("texture_sheet_tiles", out JsonNode tilesToken))
                {
                    JsonArray tilesArray = tilesToken as JsonArray;
                    if (tilesArray != null && tilesArray.Count >= 2)
                    {
                        textureSheet.numTilesX = tilesArray[0].AsInt;
                        textureSheet.numTilesY = tilesArray[1].AsInt;
                    }
                }

                if (args.TryGetValue("texture_sheet_fps", out JsonNode fpsToken))
                    textureSheet.fps = fpsToken.AsFloat;
            }

            // ????
            if (args.ContainsKey("trails_enabled") || args.ContainsKey("trails_ratio"))
            {
                var trails = ps.trails;

                if (args.TryGetValue("trails_enabled", out JsonNode trailsEnabledToken))
                    trails.enabled = trailsEnabledToken.AsBool;

                if (args.TryGetValue("trails_ratio", out JsonNode ratioToken))
                    trails.ratio = ratioToken.AsFloat;

                if (args.TryGetValue("trails_lifetime", out JsonNode trailLifetimeToken))
                    trails.lifetime = trailLifetimeToken.AsFloat;
            }
        }

        private JsonClass GetParticleSystemData(ParticleSystem ps)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            var data = new JsonClass
            {
                ["name"] = ps.name,
                ["isPlaying"] = ps.isPlaying,
                ["isPaused"] = ps.isPaused,
                ["isStopped"] = ps.isStopped,
                ["time"] = ps.time,
                ["particleCount"] = ps.particleCount,

                // ???
                ["main"] = new JsonClass
                {
                    ["duration"] = main.duration,
                    ["looping"] = main.loop,
                    ["prewarm"] = main.prewarm,
                    ["startDelay"] = main.startDelay.constant,
                    ["startLifetime"] = main.startLifetime.constant,
                    ["startSpeed"] = main.startSpeed.constant,
                    ["startSize"] = main.startSize.constant,
                    ["startRotation"] = main.startRotation.constant * Mathf.Rad2Deg,
                    ["startColor"] = new JsonArray
                    {
                        main.startColor.color.r,
                        main.startColor.color.g,
                        main.startColor.color.b,
                        main.startColor.color.a
                    },
                    ["gravityModifier"] = main.gravityModifier.constant,
                    ["simulationSpace"] = main.simulationSpace.ToString(),
                    ["simulationSpeed"] = main.simulationSpeed,
                    ["scalingMode"] = main.scalingMode.ToString(),
                    ["playOnAwake"] = main.playOnAwake,
                    ["maxParticles"] = main.maxParticles
                },

                // ????
                ["emission"] = new JsonClass
                {
                    ["enabled"] = emission.enabled,
                    ["rateOverTime"] = emission.rateOverTime.constant,
                    ["rateOverDistance"] = emission.rateOverDistance.constant
                },

                // ????
                ["shape"] = new JsonClass
                {
                    ["enabled"] = shape.enabled,
                    ["shapeType"] = shape.shapeType.ToString(),
                    ["radius"] = shape.radius,
                    ["angle"] = shape.angle,
                    ["arc"] = shape.arc
                }
            };

            if (renderer != null)
            {
                data["renderer"] = new JsonClass
                {
                    ["renderMode"] = renderer.renderMode.ToString(),
                    ["materialName"] = renderer.sharedMaterial?.name,
                    ["sortingLayer"] = renderer.sortingLayerName,
                    ["sortingOrder"] = renderer.sortingOrder
                };
            }

            return data;
        }

        // --- ???? ---

        private bool AssetExists(string path)
        {
            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        private string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            path = path.Replace("\\", "/");
            if (!path.StartsWith("Assets/"))
                path = "Assets/" + path;

            return path;
        }
    }
}
