using Godot;
using SakugaEngine.Global;

namespace SakugaEngine.Resources
{
    [GlobalClass]
    public partial class PlaySoundAction : FrameDataAction
    {
        [Export] public SoundType SoundType;
        [Export] public int Source;
        [Export] public int Index;
        [Export] public bool IsRandom;
        [Export] public int Range;
        [Export] public int FromExtraVariable = -1;
        public override void Execute(ref SakugaActor Actor)
        {
            if (Actor.Parameters.SoundSources == null || Actor.Parameters.SoundSources.Length == 0) return;
            if (SoundType == SoundType.SFX && Actor.SFXList == null) return;
            if (SoundType == SoundType.VOICE && Actor.VoiceLines == null) return;
            
            int ind = IsRandom ? RNG.Next(Index, Range) : Index;
            if (Index < 0 && FromExtraVariable >= 0)
            {
                ind = Actor.Parameters.Variables[FromExtraVariable].CurrentValue;
                Actor.Parameters.Variables[FromExtraVariable].ChangeBehavior(CustomVariableBehaviorTarget.ON_USE);
            }

            AudioStream selectedSound = null;
            switch (SoundType)
            {
                case SoundType.SFX:
                    if (ind >= 0 && ind < Actor.SFXList.Sounds.Length)
                        selectedSound = Actor.SFXList.Sounds[ind];
                    break;
                case SoundType.VOICE:
                    if (ind >= 0 && ind < Actor.VoiceLines.Sounds.Length)
                        selectedSound = Actor.VoiceLines.Sounds[ind];
                    break;
            }

            if (selectedSound == null) return;
            Actor.Parameters.SoundSources[Source].QueueSound(selectedSound);                
        }
    }
}
