namespace Cthangover.Core.Audio
{

    /// <summary>
    /// Contract for the central audio service. Separates per-bus playback
    /// (music, ambient, SFX) and volume/enabled control. The PlaySound
    /// overload with <c>variations</c> picks a random suffix from 1..N,
    /// enabling sound variation without explicit variant IDs in callers.
    /// </summary>
    public interface IAudioService
    {
        void PlayMusic(string id, MusicType musicType);
        void StopMusic();
        void PauseMusic(bool pause);
        void PlaySound(string id, SoundType soundType);
        void PlaySound(string id, int variations, SoundType soundType);
        void StopSound(SoundType type);
        void PauseSound(SoundType type);
        void PlayAmbient(string id);
        void StopAmbient();
        void SetVolume(MixerType type, float volume);
        void SetEnabled(MixerType type, bool enabled);
    }

}
