using UnityEngine;

namespace AdaptiveScrolls
{
    public struct AdaptiveScrollItemData
    {
        public readonly Vector2 Position;
        public readonly Vector2 Size;

        public AdaptiveScrollItemData(Vector2 size, Vector2 position)
        {
            Size = size;
            Position = position;
        }
    }
}