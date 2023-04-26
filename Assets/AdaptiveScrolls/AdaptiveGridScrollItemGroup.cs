using System.Collections.Generic;

namespace AdaptiveScrolls
{
    public class AdaptiveGridScrollItemGroup
    {
        public readonly float Position;
        public readonly float Size;
        public readonly List<int> ItemIndexes;
        public bool Visible;

        public AdaptiveGridScrollItemGroup(float position, float size, bool visible, List<int> itemIndexes)
        {
            Position = position;
            Size = size;
            Visible = visible;
            ItemIndexes = itemIndexes;
        }
    }
}