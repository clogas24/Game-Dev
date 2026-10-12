using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LanternKeeper.EditorTools
{
    // Step 5: character animation authored in Unity by code (no Mixamo, no DCC tool).
    //
    // Ghost: six AnimationClips and an Animator Controller whose states and transitions mirror
    // the ghost FSM in docs/FSM.md one for one. GhostAI already sets the controller's two
    // parameters, Speed (float, m/s) and State (int, the GhostState value). The clips animate
    // the "Pivot" child of the ghost's Visual object (bob, lean, stretch, squash) and the hem
    // blend shapes of "Pivot/Body"; nothing on the ghost's root, so the NavMeshAgent and the
    // trigger collider never move. docs/ANIMATION.md has the state and clip tables.
    //
    // Player: a copy of the Starter Assets controller with IK Pass switched on (LanternProp
    // pins the hands with it) and the parameters the player scripts already look for.
    public static partial class LanternKeeperSetup
    {
        private const string AnimDir = "Assets/_Project/Animations";
        private const string GhostControllerPath = AnimDir + "/Ghost.controller";
        private const string PlayerControllerPath = AnimDir + "/Player.controller";
        // Bump when the controller's states or transitions change: it is rebuilt (once) when this
        // differs from the version stored in the asset's import settings. There is deliberately no
        // "rebuild" menu item: a rebuild gives the controller's states new file IDs every time.
        private const string GhostControllerVersion = "ghost-controller-2";
        private const string GhostPivotPath = "Pivot";
        private const string GhostBodyPath = "Pivot/Body";
        private const float Tau = Mathf.PI * 2f;

        // ------------------------------------------------------------------ ghost clips

        /// <summary>Writes the curves of one clip. Times are seconds; u is the clip's phase, 0..1.</summary>
        private sealed class ClipWriter
        {
            private readonly AnimationClip _clip;
            private readonly float _length;
            private readonly bool _loop;

            public ClipWriter(AnimationClip clip, float length, bool loop)
            {
                _clip = clip;
                _length = length;
                _loop = loop;
            }

            public void Position(Func<float, Vector3> f) => Vector(typeof(Transform), GhostPivotPath, "m_LocalPosition", f);
            public void Rotation(Func<float, Vector3> f) => Vector(typeof(Transform), GhostPivotPath, "localEulerAnglesRaw", f);
            public void Scale(Func<float, Vector3> f) => Vector(typeof(Transform), GhostPivotPath, "m_LocalScale", f);
            public void Shape(string blendShape, Func<float, float> f) => Sample(typeof(SkinnedMeshRenderer), GhostBodyPath, "blendShape." + blendShape, f);

            /// <summary>Body renderer on (1) or off (0) at the given times; the value holds until the next key.</summary>
            public void Visible(params (float time, bool on)[] keys)
            {
                var curve = new AnimationCurve();
                foreach ((float time, bool on) in keys)
                {
                    curve.AddKey(new Keyframe(time, on ? 1f : 0f, float.PositiveInfinity, float.PositiveInfinity));
                }
                AnimationUtility.SetEditorCurve(_clip, EditorCurveBinding.FloatCurve(GhostBodyPath, typeof(SkinnedMeshRenderer), "m_Enabled"), curve);
            }

            private void Vector(Type type, string path, string property, Func<float, Vector3> f)
            {
                Sample(type, path, property + ".x", u => f(u).x);
                Sample(type, path, property + ".y", u => f(u).y);
                Sample(type, path, property + ".z", u => f(u).z);
            }

            // One key every 1/30 s, with the slope measured from the function itself, so a
            // looping clip's last key runs into its first without a hitch.
            private void Sample(Type type, string path, string property, Func<float, float> f)
            {
                int keys = Mathf.Max(2, Mathf.RoundToInt(_length * 30f));
                const float step = 0.002f;
                var curve = new AnimationCurve();
                for (int i = 0; i <= keys; i++)
                {
                    float u = i / (float)keys;
                    float before = u - step, after = u + step;
                    if (_loop)
                    {
                        before = Mathf.Repeat(before, 1f);
                        after = Mathf.Repeat(after, 1f);
                    }
                    else
                    {
                        before = Mathf.Max(0f, before);
                        after = Mathf.Min(1f, after);
                    }
                    float span = _loop ? step * 2f : Mathf.Max(0.0001f, Mathf.Min(1f, u + step) - Mathf.Max(0f, u - step));
                    float slope = (f(after) - f(before)) / (span * _length);
                    curve.AddKey(new Keyframe(u * _length, f(u), slope, slope));
                }
                AnimationUtility.SetEditorCurve(_clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
            }
        }

        private static AnimationClip GhostClip(string name, float length, bool loop, Action<ClipWriter> write)
        {
            string path = $"{AnimDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                clip.ClearCurves();
            }
            clip.frameRate = 30f;
            write(new ClipWriter(clip, length, loop));

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.startTime = 0f;
            settings.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // sin of (cycles x phase + offset) turns: the building block of every looping curve.
        private static float Wave(float u, float cycles, float offset = 0f) => Mathf.Sin(Tau * (cycles * u + offset));

        // The hem ripple: the two blend shapes a quarter turn apart send the wave round the skirt.
        private static void HemWave(ClipWriter clip, float cycles, Func<float, float> billow)
        {
            clip.Shape("WaveA", u => 50f + 50f * Wave(u, cycles));
            clip.Shape("WaveB", u => 50f + 50f * Wave(u, cycles, 0.25f));
            clip.Shape("Billow", billow);
        }

        // Eases between hand-placed keys (time, value); used by the one clip that is not a loop.
        private static float Keys(float time, params (float time, float value)[] keys)
        {
            if (time <= keys[0].time) return keys[0].value;
            for (int i = 0; i + 1 < keys.Length; i++)
            {
                if (time > keys[i + 1].time) continue;
                float t = Mathf.InverseLerp(keys[i].time, keys[i + 1].time, time);
                return Mathf.Lerp(keys[i].value, keys[i + 1].value, Mathf.SmoothStep(0f, 1f, t));
            }
            return keys[keys.Length - 1].value;
        }

        /// <summary>
        /// Creates or updates the six ghost clips and the controller. The clips are always
        /// rewritten in place; the controller is only rebuilt when it is missing or its version changed.
        /// </summary>
        private static RuntimeAnimatorController EnsureGhostAnimator()
        {
            EnsureFolder(AnimDir);

            // Float idle: bob and sway on the spot, the hem drifting slowly.
            AnimationClip idle = GhostClip("Ghost_FloatIdle", 2.4f, true, clip =>
            {
                clip.Position(u => new Vector3(0.035f * Wave(u, 1f, 0.25f), 0.07f * Wave(u, 1f), 0f));
                clip.Rotation(u => new Vector3(2f * Wave(u, 1f, 0.1f), 5f * Wave(u, 1f), 3.5f * Wave(u, 1f, 0.25f)));
                clip.Scale(u => new Vector3(1f + 0.02f * Wave(u, 2f), 1f - 0.03f * Wave(u, 2f), 1f + 0.02f * Wave(u, 2f)));
                HemWave(clip, 1f, u => 12f + 8f * Wave(u, 1f));
            });

            // Drift move: leaning into the direction of travel, the hem streaming.
            AnimationClip move = GhostClip("Ghost_DriftMove", 1.2f, true, clip =>
            {
                clip.Position(u => new Vector3(0f, 0.04f * Wave(u, 2f), 0f));
                clip.Rotation(u => new Vector3(10f + 2f * Wave(u, 2f), 0f, 2.5f * Wave(u, 1f)));
                clip.Scale(u => new Vector3(1f, 1f, 1.03f));
                HemWave(clip, 2f, u => 28f + 8f * Wave(u, 2f));
            });

            // Notice: a startled stretch upward, a rebound, then a held, quivering lean back.
            // 0.9 s long, the same as GhostAI's notice wind-up, and it does not loop.
            AnimationClip notice = GhostClip("Ghost_Notice", 0.9f, false, clip =>
            {
                clip.Position(u => new Vector3(0f, Keys(u * 0.9f, (0f, 0f), (0.12f, 0.2f), (0.3f, 0.04f), (0.45f, 0.08f), (0.9f, 0.08f)), 0f));
                clip.Rotation(u => new Vector3(
                    Keys(u * 0.9f, (0f, 0f), (0.12f, -10f), (0.45f, -5f), (0.9f, -5f)),
                    0f,
                    4f * Wave(u, 5f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.55f, u))));
                clip.Scale(u =>
                {
                    float wide = Keys(u * 0.9f, (0f, 1f), (0.12f, 0.82f), (0.3f, 1.07f), (0.45f, 0.98f), (0.9f, 0.98f));
                    float tall = Keys(u * 0.9f, (0f, 1f), (0.12f, 1.32f), (0.3f, 0.93f), (0.45f, 1.04f), (0.9f, 1.04f));
                    return new Vector3(wide, tall, wide);
                });
                HemWave(clip, 3f, u => Keys(u * 0.9f, (0f, 10f), (0.12f, 70f), (0.45f, 25f), (0.9f, 25f)));
            });

            // Chase: a hard forward lean and a lunge, the hem whipping.
            AnimationClip chase = GhostClip("Ghost_Chase", 0.6f, true, clip =>
            {
                clip.Position(u => new Vector3(0f, 0.05f * Wave(u, 2f, 0.25f), 0.05f));
                clip.Rotation(u => new Vector3(24f + 3f * Wave(u, 2f), 0f, 4f * Wave(u, 1f)));
                clip.Scale(u => new Vector3(0.96f, 1.02f, 1.1f));
                HemWave(clip, 3f, u => 45f + 10f * Wave(u, 3f));
            });

            // Repelled: thrown back and squashed, the skirt blown out, flickering in and out.
            AnimationClip repelled = GhostClip("Ghost_Repelled", 0.5f, true, clip =>
            {
                clip.Position(u => new Vector3(0f, -0.06f + 0.03f * Wave(u, 4f), -0.12f));
                clip.Rotation(u => new Vector3(-16f + 4f * Wave(u, 2f), 0f, 5f * Wave(u, 3f)));
                clip.Scale(u => new Vector3(1.14f + 0.06f * Wave(u, 4f), 0.82f - 0.06f * Wave(u, 4f), 1.14f + 0.06f * Wave(u, 4f)));
                HemWave(clip, 2f, u => 100f);
                clip.Visible((0f, true), (0.1f, false), (0.14f, true), (0.3f, false), (0.33f, true), (0.5f, true));
            });

            // Distracted / eating: dipped down over the candy, nibbling.
            AnimationClip eat = GhostClip("Ghost_Eat", 0.8f, true, clip =>
            {
                clip.Position(u => new Vector3(0f, -0.24f + 0.03f * Wave(u, 6f), 0.1f));
                clip.Rotation(u => new Vector3(30f + 3f * Wave(u, 6f), 0f, 5f * Wave(u, 3f)));
                clip.Scale(u => new Vector3(1.05f, 0.95f, 1.05f));
                HemWave(clip, 1f, u => 35f);
            });

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GhostControllerPath);
            AssetImporter importer = controller != null ? AssetImporter.GetAtPath(GhostControllerPath) : null;
            if (controller != null && importer != null && importer.userData == GhostControllerVersion) return controller;

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(GhostControllerPath);
            }
            else
            {
                // Empty the existing asset rather than deleting it, so its GUID (and the ghost prefab's reference) survives.
                foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(GhostControllerPath))
                {
                    if (sub != null && sub != controller) UnityEngine.Object.DestroyImmediate(sub, true);
                }
                controller.layers = new AnimatorControllerLayer[0];
                controller.parameters = new AnimatorControllerParameter[0];
                controller.AddLayer("Base Layer");
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("State", AnimatorControllerParameterType.Int);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            // One Animator state per GhostState, named after it (GhostVisualFx relies on the names).
            // Drift, Return and Distracted blend on Speed, since the ghost both waits and moves in them.
            AnimatorState drift = TreeState(controller, machine, "Drift", new Vector3(260f, 120f), idle, 0f, move, 1.6f);
            AnimatorState noticeState = ClipState(machine, "Notice", new Vector3(520f, 20f), notice);
            AnimatorState chaseState = ClipState(machine, "Chase", new Vector3(780f, 120f), chase);
            AnimatorState repelledState = ClipState(machine, "Repelled", new Vector3(390f, 420f), repelled);
            AnimatorState distracted = TreeState(controller, machine, "Distracted", new Vector3(650f, 420f), eat, 0f, move, 2.8f);
            AnimatorState returnState = TreeState(controller, machine, "Return", new Vector3(520f, 250f), idle, 0f, move, 2.2f);
            machine.defaultState = drift;

            // The transitions of docs/FSM.md, section 2, and nothing else.
            Link(drift, noticeState, GhostState.Notice, 0.05f);            // sees the lit player
            Link(noticeState, chaseState, GhostState.Chase, 0.12f);        // notice wind-up elapsed
            Link(noticeState, returnState, GhostState.Return, 0.2f);       // player left range / lantern out
            Link(chaseState, returnState, GhostState.Return, 0.2f);        // hit, lost the player, lantern out
            Link(returnState, drift, GhostState.Drift, 0.25f);             // reached its patrol point
            foreach (AnimatorState from in new[] { drift, noticeState, chaseState, returnState })
            {
                Link(from, distracted, GhostState.Distracted, 0.2f);       // candy landed in range
            }
            Link(distracted, returnState, GhostState.Return, 0.2f);        // candy eaten, lost or unreachable
            foreach (AnimatorState from in new[] { drift, noticeState, chaseState, returnState, distracted })
            {
                Link(from, repelledState, GhostState.Repelled, 0.06f);     // flare in range, from any state
            }
            Link(repelledState, returnState, GhostState.Return, 0.2f);     // repel timer ran out
            foreach (AnimatorState from in new[] { noticeState, chaseState, distracted, repelledState })
            {
                Link(from, drift, GhostState.Drift, 0.15f);                // ResetToPatrol (player respawn)
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            importer = AssetImporter.GetAtPath(GhostControllerPath);
            if (importer != null && importer.userData != GhostControllerVersion)
            {
                importer.userData = GhostControllerVersion;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(GhostControllerPath);
        }

        private static AnimatorState ClipState(AnimatorStateMachine machine, string name, Vector3 position, AnimationClip clip)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = clip;
            return state;
        }

        // A state holding a 1D blend tree on Speed: one clip standing still, another at full speed.
        private static AnimatorState TreeState(AnimatorController controller, AnimatorStateMachine machine, string name, Vector3 position,
            AnimationClip still, float stillSpeed, AnimationClip moving, float movingSpeed)
        {
            var tree = new BlendTree
            {
                name = name + " (Speed)",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(still, stillSpeed);
            tree.AddChild(moving, movingSpeed);

            AnimatorState state = machine.AddState(name, position);
            state.motion = tree;
            return state;
        }

        private static void Link(AnimatorState from, AnimatorState to, GhostState when, float seconds)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = seconds;
            transition.offset = 0f;
            transition.canTransitionToSelf = false;
            // A newer State value may cut in while this blend is still running.
            transition.interruptionSource = TransitionInterruptionSource.Destination;
            transition.AddCondition(AnimatorConditionMode.Equals, (int)when, "State");
        }

        // ------------------------------------------------------------------ player controller

        /// <summary>
        /// The player's Animator Controller: the Starter Assets one, copied once into the
        /// project so it can be changed without touching the package. IK Pass is switched on
        /// for LanternProp, and Flaring, Extinguished, State and Throw are added because
        /// PlayerStateTracker and CandyThrower set them whenever they exist.
        /// </summary>
        private static RuntimeAnimatorController EnsurePlayerAnimator()
        {
            EnsureFolder(AnimDir);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null)
            {
                string source = null;
                foreach (string guid in AssetDatabase.FindAssets("StarterAssetsThirdPerson t:AnimatorController"))
                {
                    source = AssetDatabase.GUIDToAssetPath(guid);
                    if (source.EndsWith("/StarterAssetsThirdPerson.controller")) break;
                }
                if (source == null || !AssetDatabase.CopyAsset(source, PlayerControllerPath))
                {
                    Debug.LogError("[Lantern Keeper] Could not copy the Starter Assets Animator Controller; the player keeps the original (no hand IK).");
                    return null;
                }
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            }

            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length > 0 && !layers[0].iKPass)
            {
                layers[0].iKPass = true;
                controller.layers = layers;
            }
            EnsureParameter(controller, "Flaring", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Extinguished", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "State", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "Throw", AnimatorControllerParameterType.Trigger);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name) return;
            }
            controller.AddParameter(name, type);
        }
    }
}
