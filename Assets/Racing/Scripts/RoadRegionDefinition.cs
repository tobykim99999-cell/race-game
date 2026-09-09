using UnityEngine;

namespace CircuitRacing
{
    public enum RoadRegion { Plains, Forest, Mountain, Desert }
    public enum RoadTime { Random, Day, Sunset, Night }

    [CreateAssetMenu(menuName = "Racing/Road Region")]
    public sealed class RoadRegionDefinition : ScriptableObject
    {
        public RoadRegion region;
        public string displayName;
        public string sceneName;
        public float distanceMetres;
        public Texture2D preview;
    }

    public static class RoadLaunchSettings
    {
        public static RoadTime TimeOfDay = RoadTime.Random;
    }
}
