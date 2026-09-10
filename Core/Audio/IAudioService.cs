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
        /// <summary>
        /// Loads a music track by asset <paramref name="id"/> through
        /// <c>MusicFactory</c> and assigns it to the dedicated Music
        /// <c>AudioStreamPlayer</c>. The <paramref name="musicType"/>
        /// tag is informational — it does not change playback routing;
        /// the caller is responsible for using the correct type so that
        /// auto-advance logic in <c>MusicPlayerBehaviour</c> picks the
        /// right playlist bucket on track end.
        /// </summary>
        void PlayMusic(string id, MusicType musicType);

        /// <summary>
        /// Immediately stops the dedicated music player. Any saved
        /// playback position in <c>PlaylistContext</c> is preserved,
        /// allowing a later <c>PlayMusic</c> or auto-advance to pick
        /// a fresh track or resume the saved one.
        /// </summary>
        void StopMusic();

        /// <summary>
        /// Pauses or resumes the music stream in-place via
        /// <c>StreamPaused</c>. The current track is not evicted, so
        /// a subsequent <c>PauseMusic(false)</c> continues from the
        /// exact sample where audio was suspended.
        /// </summary>
        void PauseMusic(bool pause);

        /// <summary>
        /// Plays a single sound effect identified by <paramref name="id"/>
        /// through the per-<see cref="SoundType"/> player pool. If a
        /// sound of the same <paramref name="soundType"/> is already
        /// playing it is cut off, while sounds of different types
        /// stack independently.
        /// </summary>
        void PlaySound(string id, SoundType soundType);

        /// <summary>
        /// Plays a sound effect with random variation. When
        /// <paramref name="variations"/> is greater than 1, a suffix
        /// <c>"_N"</c> (1..variations) is appended to <paramref name="id"/>
        /// so the caller can provide a base name like <c>"footstep"</c>
        /// and get <c>"footstep_3"</c> at runtime without enumerating
        /// variants manually.
        /// </summary>
        void PlaySound(string id, int variations, SoundType soundType);

        /// <summary>
        /// Stops the player associated with the given
        /// <paramref name="type"/> pool. Other <see cref="SoundType"/>
        /// players are unaffected.
        /// </summary>
        void StopSound(SoundType type);

        /// <summary>
        /// Pauses the per-type sound player. Only the specified
        /// <paramref name="type"/> is affected; other pools keep
        /// playing. Use <c>PlaySound</c> to restart — the stream
        /// resumes from where it was paused because <c>Play()</c>
        /// on an already-loaded <c>AudioStreamPlayer</c> continues
        /// from the paused position.
        /// </summary>
        void PauseSound(SoundType type);

        /// <summary>
        /// Starts or restarts an ambient loop on the dedicated Ambient
        /// bus. If the same ambient stream is already playing the call
        /// is silently ignored, preventing an audible reset glitch.
        /// On track end the player automatically re-plays the stream.
        /// </summary>
        void PlayAmbient(string id);

        /// <summary>
        /// Stops the ambient loop. Unlike the music bus there is no
        /// auto-advance or saved state — calling <c>PlayAmbient</c>
        /// again starts the stream from the beginning.
        /// </summary>
        void StopAmbient();

        /// <summary>
        /// Sets the linear volume (0–1) for the audio bus identified by
        /// <paramref name="type"/>. The value is converted to dB via
        /// <c>LinearToDb</c> before being pushed to
        /// <c>AudioServer.SetBusVolumeDb</c>. Changes are immediate
        /// and independent across the three buses.
        /// </summary>
        void SetVolume(MixerType type, float volume);

        /// <summary>
        /// Mutes the bus by forcing its volume to -80 dB when
        /// <paramref name="enabled"/> is <c>false</c>. When
        /// <paramref name="enabled"/> is <c>true</c> the method
        /// is a no-op — normal volume is restored by a subsequent
        /// <c>SetVolume</c> or settings poll.
        /// </summary>
        void SetEnabled(MixerType type, bool enabled);
    }

}
