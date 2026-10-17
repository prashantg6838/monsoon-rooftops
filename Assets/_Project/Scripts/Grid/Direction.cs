namespace MonsoonRooftops.Grid
{
    // North = +Z, East = +X. Rotation steps are clockwise quarter turns.
    public enum Direction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class DirectionExtensions
    {
        public static Direction Rotate(this Direction d, int steps)
        {
            return (Direction)((((int)d + steps) % 4 + 4) % 4);
        }

        public static Direction Opposite(this Direction d) => d.Rotate(2);

        public static int OffsetX(this Direction d) => d == Direction.East ? 1 : d == Direction.West ? -1 : 0;

        public static int OffsetZ(this Direction d) => d == Direction.North ? 1 : d == Direction.South ? -1 : 0;
    }
}
