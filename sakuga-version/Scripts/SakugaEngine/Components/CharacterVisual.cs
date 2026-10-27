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
        private enum Mode { Loop, Attack, Guard, Hit, Down, GetUp }

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
        private readonly Dictionary<string, float> impactTimes = new();
        private int loopTick, lastFrame = -1;
        private string lastSource = "";

        // The Kung Fu attacks all start from "idle guard2"'s stance; "idle guard" is the opposite lead and snaps the body around.
        private string IdleClip => girl ? "ginga_forward" : "Kung Fu idle guard2";
        private string GuardClip => girl ? "ginga_forward" : "Kung Fu idle guard2";
        private string ShowClip => girl ? "capoeira" : "Kung Fu idle drunk";

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
            foreach (string clip in new[] { "capoeira", "bencao", "chapa_2", "ginga_forward" })
            {
                var packed = GD.Load<PackedScene>($"res://Models/AnimeGirl/{clip}.glb");
                var source = packed.Instantiate();
                var player = FindNodeOfType<AnimationPlayer>(source);
                if (player == null || player.GetAnimationList().Length == 0) { GD.PushError($"Missing animation {clip}"); source.Free(); continue; }
                var animation = (Animation)player.GetAnimation(player.GetAnimationList()[0]).Duplicate(true);
                for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
                {
                    var path = animation.TrackGetPath(i).ToString();
                    int colon = path.LastIndexOf(':');
                    var bone = colon >= 0 ? path[(colon + 1)..] : "";
                    if (animation.TrackGetType(i) != Animation.TrackType.Rotation3D || skeleton.FindBone(bone) < 0) animation.RemoveTrack(i);
                    else animation.TrackSetPath(i, new NodePath(model.GetPathTo(skeleton) + ":" + bone));
                }
                library.AddAnimation(clip, animation);
                source.Free();
            }
        }

        // Sakuga source animations are named after Kaede's sprite clips; pick the closest model clip and how to drive it.
        private (string clip, Mode mode) MapAnimation(string source)
        {
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
            foreach (string name in new[] { "5A", "5B", "5C", "66B", "236C_1", "236C_3", "623B", "236236A" })
                clips.Add(MapAnimation(name).clip);
            foreach (string clip in clips)
            {
                if (!animationPlayer.HasAnimation(clip)) continue;
                float length = (float)animationPlayer.GetAnimation(clip).Length, best = float.MinValue, bestTime = length * 0.4f;
                for (int i = 0; i <= 60; i++)
                {
                    float t = length * i / 60f;
                    Pose(clip, t);
                    var hip = JointPosition(hips);
                    foreach (int limb in limbs)
                    {
                        if (limb < 0) continue;
                        float reach = JointPosition(limb).X - hip.X;
                        if (reach > best) { best = reach; bestTime = t; }
                    }
                }
                impactTimes[clip] = bestTime;
            }
            if (girl && animationPlayer.HasAnimation(GuardClip))
            {
                // Guard on the ginga beat where a hand comes up highest in front of the body.
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
            if (head >= 0 && leftFoot >= 0 && rightFoot >= 0)
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
            if (source != lastSource) { if (MapAnimation(lastSource).clip != clip) loopTick = 0; lastSource = source; }
            else if (frame != lastFrame) loopTick++;
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
            }

            SetTilt(tilt);
            Pose(clip, time);
            AlignModel();
        }
    }
}
