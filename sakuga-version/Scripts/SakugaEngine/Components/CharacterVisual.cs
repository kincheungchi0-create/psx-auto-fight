using Godot;
using SakugaEngine.Global;
using SakugaEngine.Resources;
using System;
using System.Collections.Generic;

namespace SakugaEngine
{
    // Render a 3D model from Sakuga's deterministic animation state and frame.
    public partial class CharacterVisual : Node3D
    {
        private enum Mode { Loop, Attack, Guard, Hit, Down, GetUp, React, Fall, Rise, Air }

        private Node3D model;
        private AnimationPlayer animationPlayer;
        private bool girl;
        private Skeleton3D skeleton;
        private int hips = -1, leftFoot = -1, rightFoot = -1, head = -1;
        private int[] limbs = Array.Empty<int>();
        private string current = "";
        private bool calibrated;
        private float guardTime, downTime, downTilt = 85;
        private string downClip;
        private float lyingTime, riseStart, riseEnd, jumpStart, jumpPeak, jumpLand;
        private readonly Dictionary<string, float> impactTimes = new();
        private int loopTick, sourceTick, lastFrame = -1;
        private string lastSource = "";

        // The Kung Fu attacks all start from "idle guard2"'s stance; "idle guard" is the opposite lead and snaps the body around.
        private string IdleClip => girl ? "idle" : "Kung Fu idle guard2";
        private string GuardClip => girl ? "block" : "Kung Fu idle guard2";
        private string ShowClip => girl ? "victory" : "Kung Fu idle drunk";

        // Impact is timed on the striking limbs only, so a stepping foot can't stand in for the punching hand.
        private static readonly string[] PunchClips = { "jab", "jab_cross", "uppercut", "Kung Fu sexy punch", "Kung Fu phantom punch", "Kung Fu sexy no look punch", "Kung Fu double push punch" };
        private static readonly string[] KickClips = { "roundhouse", "low_kick", "hurricane_kick", "chapa_2", "bencao", "Kung Fu sexy kick" };
        private static readonly string[] AttackSources = { "5A", "5B", "5C", "2A", "2B", "2C", "j_5A", "j_5B", "j_5C", "66B", "236C_1", "236C_2", "236C_3", "623B", "623B_2", "236236A", "Throw_Enter", "Throw_Sequence" };
        // Capoeira clips are .glb next to the model; the upright boxing/MMA set from Mixamo is .fbx in mixamo/.
        private static readonly string[] GirlGlbClips = { "chapa_2", "bencao", "ginga_forward" };
        private static readonly string[] GirlFbxClips = { "idle", "walk_forward", "walk_backward", "run", "jab", "jab_cross", "uppercut", "roundhouse", "low_kick", "hurricane_kick", "block", "hit_light", "hit_big", "knockdown", "get_up", "victory", "jump" };

        public void Initialize(bool useGirl)
        {
            girl = useGirl;
            var packed = GD.Load<PackedScene>(girl ? "res://Models/AnimeGirl/character.glb" : "res://Models/KungFu/character.glb");
            if (packed == null) { GD.PushError("Could not load fighter model"); return; }
            model = packed.Instantiate<Node3D>();
            AddChild(model);
            // Mirror the Kung Fu fighter so his stance opens toward the camera instead of showing his back.
            model.Scale = girl ? Vector3.One * 6f : new Vector3(-0.38f, 0.38f, 0.38f);
            SetTilt(0);
            skeleton = FindNodeOfType<Skeleton3D>(model);
            if (skeleton != null) {
                hips = skeleton.FindBone(girl ? "mixamorig_Hips" : "CC_Base_Hip_02");
                leftFoot = skeleton.FindBone(girl ? "mixamorig_LeftFoot" : "CC_Base_L_Foot_06");
                rightFoot = skeleton.FindBone(girl ? "mixamorig_RightFoot" : "CC_Base_R_Foot_021");
                head = skeleton.FindBone(girl ? "mixamorig_Head" : "CC_Base_Head_038");
                limbs = new[] {
                    skeleton.FindBone(girl ? "mixamorig_LeftHand" : "CC_Base_L_Hand_055"),
                    skeleton.FindBone(girl ? "mixamorig_RightHand" : "CC_Base_R_Hand_083"),
                    leftFoot, rightFoot };
            }
            if (girl) LoadGirlAnimations();
            else animationPlayer = FindNodeOfType<AnimationPlayer>(model);
            if (animationPlayer == null) GD.PushError("Fighter animation player is missing");
            else animationPlayer.SpeedScale = 0; // Sakuga's state frame exclusively drives the pose.
        }

        private static T FindNodeOfType<T>(Node node) where T : Node
        {
            if (node is T match) return match;
            foreach (Node child in node.GetChildren())
            {
                var result = FindNodeOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void LoadGirlAnimations()
        {
            var skeleton = FindNodeOfType<Skeleton3D>(model);
            if (skeleton == null) { GD.PushError("Anime Girl skeleton missing"); return; }
            animationPlayer = new AnimationPlayer { Name = "SakugaModelAnimation" };
            model.AddChild(animationPlayer);
            animationPlayer.RootNode = new NodePath("..");
            var library = new AnimationLibrary();
            animationPlayer.AddAnimationLibrary("", library);
            var paths = new List<(string clip, string path)>();
            foreach (string clip in GirlGlbClips) paths.Add((clip, $"res://Models/AnimeGirl/{clip}.glb"));
            foreach (string clip in GirlFbxClips) paths.Add((clip, $"res://Models/AnimeGirl/mixamo/{clip}.fbx"));
            foreach (var (clip, file) in paths)
            {
                var packed = GD.Load<PackedScene>(file);
                if (packed == null) { GD.PushError($"Missing animation {clip}"); continue; }
                var source = packed.Instantiate();
                var player = FindNodeOfType<AnimationPlayer>(source);
                if (player == null || player.GetAnimationList().Length == 0) { GD.PushError($"Missing animation {clip}"); source.Free(); continue; }
                var animation = (Animation)player.GetAnimation(player.GetAnimationList()[0]).Duplicate(true);
                if (clip == "jump") MeasureJump(animation);
                var sourceSkeleton = FindNodeOfType<Skeleton3D>(source);
                Transform3D sourceSpace = sourceSkeleton != null ? SceneTransform(source, sourceSkeleton) : Transform3D.Identity;
                Transform3D targetSpace = SceneTransform(model, skeleton);
                for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
                {
                    var path = animation.TrackGetPath(i).ToString();
                    int colon = path.LastIndexOf(':');
                    var bone = colon >= 0 ? path[(colon + 1)..] : "";
                    int targetBone = skeleton.FindBone(bone), sourceBone = sourceSkeleton?.FindBone(bone) ?? -1;
                    if (animation.TrackGetType(i) != Animation.TrackType.Rotation3D || targetBone < 0) { animation.RemoveTrack(i); continue; }
                    animation.TrackSetPath(i, new NodePath(model.GetPathTo(skeleton) + ":" + bone));
                    if (sourceBone < 0) continue;
                    // Clips exported with a different rest pose (the Mixamo FBX set) are re-expressed in the model's bone frames:
                    // local' = parentOffset^-1 * local * boneOffset, where offset = sourceRest^-1 * bend * targetRest in world space,
                    // and bend turns the target's rest bone onto the source's (her model rests with arms lowered, Mixamo in a T-pose).
                    Quaternion boneOffset = PoseOffset(sourceSkeleton, sourceSpace, sourceBone, skeleton, targetSpace, targetBone);
                    int sourceParent = sourceSkeleton.GetBoneParent(sourceBone), targetParent = skeleton.GetBoneParent(targetBone);
                    Quaternion parentOffset = PoseOffset(sourceSkeleton, sourceSpace, sourceParent, skeleton, targetSpace, targetParent);
                    if (boneOffset.IsEqualApprox(Quaternion.Identity) && parentOffset.IsEqualApprox(Quaternion.Identity)) continue;
                    Quaternion parentInverse = parentOffset.Inverse();
                    for (int k = 0; k < animation.TrackGetKeyCount(i); k++)
                        animation.TrackSetKeyValue(i, k, (parentInverse * (Quaternion)animation.TrackGetKeyValue(i, k) * boneOffset).Normalized());
                }
                library.AddAnimation(clip, animation);
                source.Free();
            }
        }

        // Split the jump clip at take-off, apex and touch-down from the root's height, before position tracks are dropped.
        private void MeasureJump(Animation animation)
        {
            for (int i = 0; i < animation.GetTrackCount(); i++)
            {
                string path = animation.TrackGetPath(i).ToString();
                if (animation.TrackGetType(i) != Animation.TrackType.Position3D || !(path.EndsWith(":mixamorig_Hips") || path.EndsWith(":_rootJoint"))) continue;
                float length = (float)animation.Length;
                var height = new float[121];
                int peak = 0;
                for (int k = 0; k <= 120; k++) { height[k] = animation.PositionTrackInterpolate(i, length * k / 120f).Y; if (height[k] > height[peak]) peak = k; }
                float ground = height[0] + 0.1f * (height[peak] - height[0]);
                int start = 0, land = 120;
                for (int k = peak; k >= 0; k--) if (height[k] <= ground) { start = k; break; }
                for (int k = peak; k <= 120; k++) if (height[k] <= ground) { land = k; break; }
                jumpStart = length * start / 120f;
                jumpPeak = length * peak / 120f;
                jumpLand = length * land / 120f;
                return;
            }
        }

        // Transform of a node relative to a scene root that is not in the tree yet.
        private static Transform3D SceneTransform(Node root, Node3D node)
        {
            var result = Transform3D.Identity;
            for (Node n = node; n != null && n != root; n = n.GetParent())
                if (n is Node3D spatial) result = spatial.Transform * result;
            return result;
        }

        private static Quaternion PoseOffset(Skeleton3D source, Transform3D sourceSpace, int sourceBone, Skeleton3D target, Transform3D targetSpace, int targetBone)
        {
            Quaternion bend = Quaternion.Identity;
            Vector3 from = RestDirection(target, targetSpace, targetBone), to = RestDirection(source, sourceSpace, sourceBone);
            // Only real pose differences (her arms sit ~24 degrees low); larger gaps come from arbitrary "_End" tip bones.
            float dot = from.Dot(to);
            if (from != Vector3.Zero && to != Vector3.Zero && dot < 0.9999f && dot > 0.7f) bend = new Quaternion(from, to);
            return WorldRest(source, sourceSpace, sourceBone).Inverse() * bend * WorldRest(target, targetSpace, targetBone);
        }

        // World direction from a bone to its first child at rest, or zero for the skeleton node and leaf bones.
        private static Vector3 RestDirection(Skeleton3D skeleton, Transform3D space, int bone)
        {
            if (bone < 0) return Vector3.Zero;
            int[] children = skeleton.GetBoneChildren(bone);
            if (children.Length == 0) return Vector3.Zero;
            Vector3 start = (space * skeleton.GetBoneGlobalRest(bone)).Origin, end = (space * skeleton.GetBoneGlobalRest(children[0])).Origin;
            return (end - start).Normalized();
        }

        // World rotation of a bone's rest pose; bone -1 is the skeleton node itself.
        private static Quaternion WorldRest(Skeleton3D skeleton, Transform3D space, int bone)
        {
            var rest = bone < 0 ? space : space * skeleton.GetBoneGlobalRest(bone);
            return rest.Basis.Orthonormalized().GetRotationQuaternion();
        }

        // Sakuga source animations are named after Kaede's sprite clips; pick the closest model clip and how to drive it.
        private (string clip, Mode mode) MapAnimation(string source)
        {
            if (girl) return MapGirlAnimation(source);
            bool Is(string prefix) => source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            if (Is("Block")) return (GuardClip, Mode.Guard);
            if (Is("Hit_GetUp")) return (downClip ?? IdleClip, Mode.GetUp);
            if (Is("Hit_Falling") || Is("Hit_OnTheGround") || Is("Hit_Down") || Is("Hit_Spin") || Is("Hit_Sweep") || Is("Hit_Faint"))
                return (downClip ?? IdleClip, Mode.Down);
            if (Is("Hit") || Is("Throw_Reaction")) return (IdleClip, Mode.Hit);
            if (Is("Intro") || Is("Taunt")) return (ShowClip, Mode.Loop);

            switch (source)
            {
                case "5A": case "2A": case "j_5A":
                    return (girl ? "bencao" : "Kung Fu sexy punch", Mode.Attack);
                case "5B": case "2B": case "j_5B":
                    return (girl ? "bencao" : "Kung Fu phantom punch", Mode.Attack);
                case "66B": case "236C_2":
                    return (girl ? "chapa_2" : "Kung Fu sexy no look punch", Mode.Attack);
                case "5C": case "2C": case "j_5C":
                    return (girl ? "chapa_2" : "Kung Fu sexy kick", Mode.Attack);
                case "236C_1":
                    return (girl ? "chapa_2" : "Kung Fu spinning elbow", Mode.Attack);
                case "236C_3":
                    return (girl ? "bencao" : "Kung Fu fake headbutt push", Mode.Attack);
                case "623B": case "623B_2": case "Throw_Enter":
                    return (girl ? "bencao" : "Kung Fu double push punch", Mode.Attack);
                case "236236A": case "Throw_Sequence":
                    return (girl ? "chapa_2" : "Kung Fu double head slam", Mode.Attack);
            }
            return (IdleClip, Mode.Loop);
        }

        // The girl has real reaction clips, so hits, knockdowns and get-ups play them instead of leaning a stance.
        private (string clip, Mode mode) MapGirlAnimation(string source)
        {
            bool Is(string prefix) => source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            if (Is("Block")) return ("block", Mode.Guard);
            if (Is("Hit_GetUp")) return ("get_up", Mode.Rise);
            if (Is("Hit_Falling") || Is("Hit_OnTheGround") || Is("Hit_Down") || Is("Hit_Spin") || Is("Hit_Sweep") || Is("Hit_Faint"))
                return ("knockdown", Mode.Fall);
            if (Is("Hit_Strong") || Is("Hit_Air_Strong") || Is("Hit_Dizzy")) return ("hit_big", Mode.React);
            if (Is("Hit") || Is("Throw_Reaction")) return ("hit_light", Mode.React);
            if (Is("Taunt")) return ("victory", Mode.Loop);
            if (Is("Walk_Forward")) return ("walk_forward", Mode.Loop);
            if (Is("Walk_Backwards") || Is("Dash_Backwards")) return ("walk_backward", Mode.Loop);
            if (Is("Dash_Forward") || Is("RunStop")) return ("run", Mode.Loop);
            if (Is("Crouch")) return ("ginga_forward", Mode.Loop);
            if (Is("Jump") || Is("Falling")) return ("jump", Mode.Air);

            switch (source)
            {
                case "5A": case "j_5A": case "Throw_Enter": return ("jab", Mode.Attack);
                case "5B": case "j_5B": case "236C_1": return ("jab_cross", Mode.Attack);
                case "5C": case "j_5C": case "236C_2": return ("roundhouse", Mode.Attack);
                case "2A": case "2B": return ("low_kick", Mode.Attack);
                case "2C": return ("chapa_2", Mode.Attack);
                case "66B": case "Throw_Sequence": return ("bencao", Mode.Attack);
                case "623B": case "623B_2": case "236C_3": return ("uppercut", Mode.Attack);
                case "236236A": return ("hurricane_kick", Mode.Attack);
            }
            return ("idle", Mode.Loop);
        }

        private Vector3 JointPosition(int index)
        {
            return ToLocal(skeleton.ToGlobal(skeleton.GetBoneGlobalPose(index).Origin));
        }

        // Lean the model backwards (positive) around its own side axis; the model faces +X here.
        private void SetTilt(float degrees)
        {
            model.RotationDegrees = new Vector3(-degrees, 90, 0);
        }

        private void Pose(string clip, float time)
        {
            if (current != clip) { animationPlayer.Play(clip); current = clip; }
            animationPlayer.Seek(Mathf.Clamp(time, 0, (float)animationPlayer.GetAnimation(clip).Length), true);
        }

        // Find when each attack clip reaches furthest forward, plus the frames used for the guard and knockdown poses.
        private void Calibrate()
        {
            calibrated = true;
            if (skeleton == null || hips < 0) return;
            SetTilt(0);
            var clips = new HashSet<string>();
            foreach (string name in AttackSources)
                clips.Add(MapAnimation(name).clip);
            foreach (string clip in clips)
            {
                if (!animationPlayer.HasAnimation(clip)) continue;
                float length = (float)animationPlayer.GetAnimation(clip).Length, best = float.MinValue, bestTime = length * 0.4f;
                int first = Array.IndexOf(KickClips, clip) >= 0 ? 2 : 0, last = Array.IndexOf(PunchClips, clip) >= 0 ? 1 : 3;
                for (int i = 0; i <= 120; i++)
                {
                    float t = length * i / 120f;
                    Pose(clip, t);
                    var hip = JointPosition(hips);
                    for (int l = first; l <= last && l < limbs.Length; l++)
                    {
                        int limb = limbs[l];
                        if (limb < 0) continue;
                        float reach = JointPosition(limb).X - hip.X;
                        if (reach > best) { best = reach; bestTime = t; }
                    }
                }
                impactTimes[clip] = bestTime;
            }
            if (girl && animationPlayer.HasAnimation(GuardClip))
            {
                // Hold the block where a hand comes up highest in front of the body.
                float length = (float)animationPlayer.GetAnimation(GuardClip).Length, highest = float.MinValue;
                for (int i = 0; i <= 60; i++)
                {
                    float t = length * i / 60f;
                    Pose(GuardClip, t);
                    var hip = JointPosition(hips);
                    for (int h = 0; h < 2; h++)
                    {
                        if (limbs[h] < 0) continue;
                        var hand = JointPosition(limbs[h]);
                        if (hand.X > hip.X && hand.Y > highest) { highest = hand.Y; guardTime = t; }
                    }
                }
            }
            if (girl && animationPlayer.HasAnimation("knockdown") && animationPlayer.HasAnimation("get_up"))
            {
                // Hold the knockdown once the hips reach the floor, and play only the get-up's actual rise.
                float fallLength = (float)animationPlayer.GetAnimation("knockdown").Length, lowest = float.MaxValue;
                var fall = new float[61];
                for (int i = 0; i <= 60; i++) { Pose("knockdown", fallLength * i / 60f); fall[i] = JointPosition(hips).Y; lowest = Mathf.Min(lowest, fall[i]); }
                for (int i = 0; i <= 60; i++) if (fall[i] <= lowest + 0.05f * Mathf.Abs(fall[0] - lowest)) { lyingTime = fallLength * i / 60f; break; }
                float riseLength = (float)animationPlayer.GetAnimation("get_up").Length;
                var rise = new float[121];
                float riseLow = float.MaxValue, riseHigh = float.MinValue;
                for (int i = 0; i <= 120; i++) { Pose("get_up", riseLength * i / 120f); rise[i] = JointPosition(hips).Y; riseLow = Mathf.Min(riseLow, rise[i]); riseHigh = Mathf.Max(riseHigh, rise[i]); }
                int endIndex = 120, startIndex = 0;
                for (int i = 0; i <= 120; i++) if (rise[i] >= riseLow + 0.95f * (riseHigh - riseLow)) { endIndex = i; break; }
                for (int i = endIndex; i >= 0; i--) if (rise[i] <= riseLow + 0.1f * (riseHigh - riseLow)) { startIndex = i; break; }
                riseStart = riseLength * startIndex / 120f;
                riseEnd = riseLength * endIndex / 120f;
            }
            if (!girl && head >= 0 && leftFoot >= 0 && rightFoot >= 0)
            {
                // Knockdowns lay the straightest planted standing frame flat on its back.
                float best = float.MinValue;
                foreach (string clip in animationPlayer.GetAnimationList())
                {
                    if (clip == ShowClip || clip == "RESET") continue;
                    float length = (float)animationPlayer.GetAnimation(clip).Length;
                    for (int i = 0; i <= 30; i++)
                    {
                        float t = length * i / 30f;
                        Pose(clip, t);
                        Vector3 hip = JointPosition(hips), top = JointPosition(head);
                        float leftY = JointPosition(leftFoot).Y, rightY = JointPosition(rightFoot).Y;
                        float score = top.Y - Mathf.Min(leftY, rightY) - 2 * Mathf.Abs(leftY - rightY);
                        if (score <= best) continue;
                        best = score; downClip = clip; downTime = t;
                        downTilt = 90 + Mathf.RadToDeg(Mathf.Atan2(top.X - hip.X, top.Y - hip.Y));
                    }
                }
            }
        }

        private void AlignModel()
        {
            if (skeleton == null || hips < 0 || leftFoot < 0 || rightFoot < 0) return;
            var hip = JointPosition(hips);
            model.Position -= new Vector3(hip.X, 0, hip.Z);
            float floorY = Mathf.Min(Mathf.Min(JointPosition(leftFoot).Y, JointPosition(rightFoot).Y), hip.Y);
            if (head >= 0) floorY = Mathf.Min(floorY, JointPosition(head).Y);
            model.Position += new Vector3(0, 0.05f - floorY, 0);
        }

        public void ViewAnimation(AnimationSettings settings, int frame, float referenceLength, AnimationData data = null)
        {
            if (animationPlayer == null || settings == null || string.IsNullOrEmpty(settings.SourceAnimation)) return;
            if (!calibrated) Calibrate();
            string source = settings.SourceAnimation;
            var (clip, mode) = MapAnimation(source);
            if (!animationPlayer.HasAnimation(clip)) return;
            float length = (float)animationPlayer.GetAnimation(clip).Length;
            int duration = data != null && data.Duration > 0 ? data.Duration : Mathf.Max(1, Mathf.RoundToInt(referenceLength * GlobalVariables.TicksPerSecond));
            int f = Math.Max(0, frame);

            // Loops run on their own clock so short looping states don't restart the clip; hitstop freezes it.
            if (source != lastSource) { if (MapAnimation(lastSource).clip != clip) loopTick = 0; sourceTick = 0; lastSource = source; }
            else if (frame != lastFrame) { loopTick++; sourceTick++; }
            lastFrame = frame;

            float tilt = 0, time = 0;
            switch (mode)
            {
                case Mode.Loop:
                    time = (loopTick / (float)GlobalVariables.TicksPerSecond) % length;
                    break;
                case Mode.Attack:
                    float impact = impactTimes.TryGetValue(clip, out float found) ? found : length * 0.4f;
                    float start = Math.Max(0, impact - 0.45f), end = Math.Min(length, impact + 0.6f);
                    int active = duration * 35 / 100;
                    if (data?.Hitboxes != null)
                        foreach (var hitbox in data.Hitboxes)
                            if (hitbox != null && hitbox.animationStage == AnimationStage.ACTIVE) { active = hitbox.AtFrame; break; }
                    active = Mathf.Clamp(active, 1, duration);
                    time = f < active
                        ? Mathf.Lerp(start, impact, f / (float)active)
                        : Mathf.Lerp(impact, end, Mathf.Clamp((f - active) / (float)Math.Max(1, duration - active), 0, 1));
                    break;
                case Mode.Guard:
                    time = guardTime;
                    tilt = 4 + (source.StartsWith("Blockstun") ? 10 * Mathf.Exp(-f / 4f) : 0);
                    break;
                case Mode.Hit:
                    if (source.StartsWith("Hit_Dizzy")) tilt = 6 + 6 * Mathf.Sin(f * 0.15f);
                    else tilt = (source.Contains("Strong") ? 28 : 18) * Mathf.Exp(-f / 10f);
                    break;
                case Mode.Down:
                    time = downTime;
                    bool grounded = source.StartsWith("Hit_OnTheGround") || source.StartsWith("Hit_Down");
                    tilt = grounded ? downTilt : Mathf.Lerp(30, downTilt, Mathf.SmoothStep(0, 1, Math.Min(1, f / 10f)));
                    break;
                case Mode.GetUp:
                    time = downTime;
                    tilt = downTilt * (1 - Mathf.SmoothStep(0, 1, f / (float)duration));
                    break;
                case Mode.React:
                    time = f / (float)GlobalVariables.TicksPerSecond;
                    break;
                case Mode.Fall:
                    bool lying = source.StartsWith("Hit_OnTheGround") || source.StartsWith("Hit_Down");
                    time = lying ? lyingTime : Mathf.Lerp(0, lyingTime, Math.Min(1, f / (float)duration));
                    break;
                case Mode.Air:
                    time = source.StartsWith("Falling")
                        ? Mathf.Lerp(jumpPeak, jumpLand, Math.Min(1, sourceTick / 20f)) // Falling is a short looping state
                        : Mathf.Lerp(jumpStart, jumpPeak, Math.Min(1, f / (float)duration));
                    break;
                case Mode.Rise:
                    time = Mathf.Lerp(riseStart, riseEnd, Math.Min(1, f / (float)duration));
                    break;
            }

            SetTilt(tilt);
            Pose(clip, time);
            AlignModel();
        }
    }
}
