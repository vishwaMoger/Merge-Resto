using UnityEngine;

namespace MergeResto.Look
{
    /// <summary>
    /// Scene-wide values for the toon shader. One per scene.
    /// </summary>
    [ExecuteAlways]
    public class ToonLookSettings : MonoBehaviour
    {
        static readonly int ToonAmbientId = Shader.PropertyToID("_ToonAmbient");

        [Tooltip("Flat fill light added to every toon surface, lit and shaded sides alike.")]
        [ColorUsage(false, true)]
        public Color ambient = new Color(0.16f, 0.13f, 0.18f);

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void Apply() => Shader.SetGlobalColor(ToonAmbientId, ambient);
    }
}
