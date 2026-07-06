namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Pluggable preview module for <see cref="EffectViewPreviewPlayer" />; handles per-effect rendering while the player manages the shared loop.
    /// </summary>
    internal interface IEffectPreviewModule
    {
        /// <summary>
        ///     Whether this module needs a palette color picker in the inspector.
        /// </summary>
        bool UsesColorPicker { get; }

        /// <summary>
        ///     Draws type-specific inspector controls.
        /// </summary>
        void DrawGUI();

        /// <summary>
        ///     Initializes the preview; called once when the user clicks Play.
        /// </summary>
        void Start(EffectPreviewContext context);

        /// <summary>
        ///     Advances the animation by <paramref name="delta" /> seconds; returns <c>false</c> when complete.
        /// </summary>
        bool Tick(float delta);

        /// <summary>
        ///     Tears down all preview state; called on Stop or when the animation finishes.
        /// </summary>
        void CleanUp();

        /// <summary>
        ///     Draws persistent Scene-view guides via <see cref="UnityEditor.Handles" />; runs whether or not the preview is playing.
        /// </summary>
        void DrawSceneGizmos();
    }
}
