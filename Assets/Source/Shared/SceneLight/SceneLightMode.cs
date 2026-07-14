namespace BalloonParty.Shared.SceneLight
{
    /// <summary>How a consumer reads the scene light. Values match the shader convention the light
    /// helpers branch on (Full = 0, Ambient = 1, Local = 2).</summary>
    internal enum SceneLightMode
    {
        /// <summary>The field: global ambient + any local point/area lights.</summary>
        Full = 0,

        /// <summary>The global scene light only (colour × intensity); ignores the field.</summary>
        Ambient = 1,

        /// <summary>Only local field lights above the ambient — neutral until a light is near.</summary>
        Local = 2,
    }
}
