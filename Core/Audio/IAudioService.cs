namespace Cthangover.Core.Audio
{

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
