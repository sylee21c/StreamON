namespace StreamOn.Minigames.Runner
{
    /// <summary>
    /// One shared pause menu (SharedBroadcastPause) is baked into every broadcast scene,
    /// but each scene owns its audio through a different component. Scene audio owners
    /// implement this so the pause sliders drive whichever scene they were opened in
    /// instead of only the StreamerRoom and Runner controllers.
    /// </summary>
    public interface IBroadcastVolumeTarget
    {
        void ApplyBgmVolume(float value);
        void ApplySfxVolume(float value);
    }
}
