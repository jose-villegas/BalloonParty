namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Pluggable preview module for <see cref="EffectViewPreviewPlayer" />.
    ///     Each effect view type implements its own module to handle custom
    ///     rendering logic (blobs, line renderers, particles, etc.) while
    ///     the player manages the shared animation loop, color picker, and
    ///     config caching.
    /// </summary>
    internal interface IEffectPreviewModule
    {
        /// <summary>
        ///     Whether this module needs a palette color picker in the inspector.
        ///     When <c>false</c>, the player omits the picker and passes
        ///     <see cref="UnityEngine.Color.white" /> as the tint.
        /// </summary>
        bool UsesColorPicker { get; }

        /// <summary>
        ///     Draw type-specific inspector controls (sliders, labels).
        ///     Called inside the player's disabled scope — controls are
        ///     automatically grayed out during playback.
        /// </summary>
        void DrawGUI();

        /// <summary>
        ///     Initialize the preview with the given context.
        ///     Called once when the user clicks Play.
        /// </summary>
        void Start(EffectPreviewContext context);

        /// <summary>
        ///     Advance the animation by <paramref name="delta" /> seconds.
        ///     Return <c>true</c> to keep running, <c>false</c> when complete.
        /// </summary>
        bool Tick(float delta);

        /// <summary>
        ///     Tear down all preview state (hide objects, destroy temporaries).
        ///     Called on Stop or when the animation finishes.
        /// </summary>
        void CleanUp();
    }
}
