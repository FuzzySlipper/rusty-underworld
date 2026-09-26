namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// What the avatar can see: a straight line between two tiles that passes only
/// through tiles which let light through. Walls block; a door blocks while it is
/// shut and not once it is opened; and the tiles the line starts and ends on never
/// block, so a thing standing in a doorway is not hidden by the doorway.
/// </summary>
public static class UuSight
{
    /// <summary>
    /// Whether an unobstructed line runs between two tiles.
    /// </summary>
    /// <param name="blocks">Whether a tile blocks sight, by tile coordinate.</param>
    public static bool HasLineOfSight(
        int fromX,
        int fromY,
        int toX,
        int toY,
        Func<int, int, bool> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        int x = fromX;
        int y = fromY;
        int dx = Math.Abs(toX - fromX);
        int dy = Math.Abs(toY - fromY);
        int stepX = Math.Sign(toX - fromX);
        int stepY = Math.Sign(toY - fromY);
        int error = dx - dy;
        while (x != toX || y != toY)
        {
            int doubled = error * 2;
            int nextX = x;
            int nextY = y;
            if (doubled > -dy)
            {
                error -= dy;
                nextX += stepX;
            }

            if (doubled < dx)
            {
                error += dx;
                nextY += stepY;
            }

            // Both axes moving at once is a diagonal step, which crosses the corner
            // of the two tiles beside it: a wall on either one closes it.
            if (nextX != x && nextY != y && (blocks(nextX, y) || blocks(x, nextY))) return false;
            x = nextX;
            y = nextY;
            if (x == toX && y == toY) break;
            if (blocks(x, y)) return false;
        }

        return true;
    }
}
