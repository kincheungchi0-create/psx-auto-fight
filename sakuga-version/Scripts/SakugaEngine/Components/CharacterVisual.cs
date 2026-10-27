using Godot;
using SakugaEngine.Resources;
using System;

namespace SakugaEngine
{
    // Render a 3D model from Sakuga's deterministic animation state and frame.
    public partial class CharacterVisual : Node3D
    {
        private Node3D model;
        private AnimationPlayer animationPlayer;
        private bool girl;
        private Skeleton3D skeleton;
        private int hips = -1, leftFoot = -1, rightFoot = -1;
        private string current = "";

        public void Initialize(bool useGirl)
        {
            girl = useGirl;
            var packed = GD.Load<PackedScene>(girl ? "res://Models/AnimeGirl/character.glb" : "res://Models/KungFu/character.glb");
            if (packed == null) { GD.PushError("Could not load fighter model"); return; }
            model = packed.Instantiate<Node3D>();
            AddChild(model);
            model.Scale = Vector3.One * (girl ? 6f : 0.38f);
            model.RotationDegrees = new Vector3(0, 90, 0);
            skeleton = FindNodeOfType<Skeleton3D>(model);
            if (skeleton != null) {
                hips = skeleton.FindBone(girl ? "mixamorig_Hips" : "CC_Base_Hip_02");
                leftFoot = skeleton.FindBone(girl ? "mixamorig_LeftFoot" : "CC_Base_L_Foot_06");
                rightFoot = skeleton.FindBone(girl ? "mixamorig_RightFoot" : "CC_Base_R_Foot_021");
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
            foreach (string clip in new[] { "capoeira", "bencao", "chapa_2", "ginga_forward", "esquiva_1" })
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

        private string MapAnimation(string state)
        {
            if (state.StartsWith("Hit", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Block", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Throw_Reaction", StringComparison.OrdinalIgnoreCase))
                return girl ? "esquiva_1" : "Kung Fu the misleader";
            if (state.StartsWith("Walk", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Run", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Dash", StringComparison.OrdinalIgnoreCase))
                return girl ? "ginga_forward" : "Kung Fu idle guard";
            if (state.StartsWith("Throw", StringComparison.OrdinalIgnoreCase))
                return girl ? "chapa_2" : "Kung Fu double push punch";
            if (state.Contains("5C") || state.Contains("2C") || state.Contains("66B"))
                return girl ? "chapa_2" : "Kung Fu sexy kick";
            if (state.Contains("5A") || state.Contains("5B") || state.StartsWith("2") || state.StartsWith("6") || state.StartsWith("j_5"))
                return girl ? "bencao" : "Kung Fu sexy punch";
            return girl ? "capoeira" : "Kung Fu idle guard";
        }

        private Vector3 JointPosition(int index)
        {
            return ToLocal(skeleton.ToGlobal(skeleton.GetBoneGlobalPose(index).Origin));
        }

        private void AlignModel()
        {
            if (skeleton == null || hips < 0 || leftFoot < 0 || rightFoot < 0) return;
            var hip = JointPosition(hips);
            model.Position -= new Vector3(hip.X, 0, hip.Z);
            float footY = Mathf.Min(JointPosition(leftFoot).Y, JointPosition(rightFoot).Y);
            model.Position += new Vector3(0, 0.05f - footY, 0);
        }

        public void ViewAnimation(AnimationSettings settings, int frame, float referenceLength)
        {
            if (animationPlayer == null || settings == null || string.IsNullOrEmpty(settings.SourceAnimation)) return;
            string name = MapAnimation(settings.SourceAnimation);
            if (!animationPlayer.HasAnimation(name)) return;
            if (current != name) { animationPlayer.Play(name); current = name; }
            var clip = animationPlayer.GetAnimation(name);
            float originalTime = Math.Max(0, (frame - settings.AtFrame) * settings.Speed / 60f);
            float progress = referenceLength > 0.001f ? Mathf.Clamp(originalTime / referenceLength, 0, 1) : 0;
            animationPlayer.Seek(progress * clip.Length, true);
            AlignModel();
        }
    }
}
