using KJ.Workflows;
using Windows.Foundation;

namespace KJ.Modules.Monitoring.Workflows;

/// <summary>
/// 正交网格走线：A* 绕开所有节点，连线在节点下层绘制时不穿模。
/// </summary>
public static class WorkflowGridLinkRouter
{
    public const int GridSize = 20;
    public const int NodeClearance = 24;
    public const int PortStubLength = 60;

    public readonly record struct GridCell(int X, int Y);

    private readonly record struct Cell(int X, int Y);

    public readonly record struct NodeObstacle(Guid StepId, double X, double Y, double Width, double Height)
    {
        public Rect Inflated => new(
            X - NodeClearance,
            Y - NodeClearance,
            Width + NodeClearance * 2,
            Height + NodeClearance * 2);
    }

    public static IReadOnlyList<Point> Route(
        Point startPort,
        WorkflowPort fromPort,
        Point endPort,
        WorkflowPort toPort,
        IReadOnlyList<NodeObstacle> obstacles,
        Guid fromStepId,
        Guid toStepId,
        IReadOnlySet<GridCell>? reservedCells = null,
        int laneIndex = 0)
    {
        _ = fromStepId;
        _ = toStepId;

        var exit = EnsureFreeStub(startPort, fromPort, obstacles, laneIndex);
        var entry = EnsureFreeStub(endPort, toPort, obstacles, laneIndex + 2);

        List<Point> core;
        var direct = TrySimpleOrthogonalRoute(exit, entry, obstacles);
        if (direct is not null)
            core = direct;
        else
            core = FindGridPath(exit, entry, obstacles, reservedCells);

        var path = new List<Point> { startPort };
        foreach (var p in core)
        {
            if (!NearlyEqual(path[^1], p))
                path.Add(p);
        }

        if (!NearlyEqual(path[^1], endPort))
            path.Add(endPort);

        return FinalizeOrthogonalPath(path, obstacles);
    }

    public static void ReservePathCells(IReadOnlyList<Point> path, ISet<GridCell> reserved)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            foreach (var cell in CellsAlongSegment(path[i], path[i + 1]))
                reserved.Add(ToGridCell(cell));
        }
    }

    public static IReadOnlyList<Point> RoutePreview(
        Point startPort,
        WorkflowPort fromPort,
        Point cursor,
        IReadOnlyList<NodeObstacle> obstacles,
        Guid fromStepId)
    {
        _ = fromStepId;

        var exit = EnsureFreeStub(startPort, fromPort, obstacles, laneIndex: 0);
        var target = Snap(cursor);
        var core = FindGridPath(exit, target, obstacles, reservedCells: null);

        var path = new List<Point> { startPort };
        foreach (var p in core)
        {
            if (!NearlyEqual(path[^1], p))
                path.Add(p);
        }

        if (!NearlyEqual(path[^1], target))
            path.Add(target);

        return FinalizeOrthogonalPath(path, obstacles);
    }

    public static Point Snap(Point p) => new(Snap(p.X), Snap(p.Y));

    public static double Snap(double v) => Math.Round(v / GridSize) * GridSize;

    private static Point EnsureFreeStub(Point port, WorkflowPort portSide, IReadOnlyList<NodeObstacle> obstacles, int laneIndex)
    {
        var laneOffset = (laneIndex % 5 - 2) * (GridSize / 2);
        var baseStub = PortStubLength + Math.Abs(laneIndex % 3) * (GridSize / 2);

        for (var len = baseStub; len <= baseStub + GridSize * 10; len += GridSize)
        {
            var p = ExtendFromPort(port, portSide, len);
            p = OffsetPerpendicular(p, portSide, laneOffset);
            if (!PointInsideAnyObstacle(p, obstacles))
                return Snap(p);
        }

        return Snap(OffsetPerpendicular(ExtendFromPort(port, portSide, baseStub), portSide, laneOffset));
    }

    private static Point OffsetPerpendicular(Point p, WorkflowPort portSide, double offset) =>
        portSide switch
        {
            WorkflowPort.Top or WorkflowPort.Bottom => new Point(p.X + offset, p.Y),
            WorkflowPort.Left or WorkflowPort.Right => new Point(p.X, p.Y + offset),
            _ => p,
        };

    private static Point ExtendFromPort(Point port, WorkflowPort portSide, int distance) =>
        portSide switch
        {
            WorkflowPort.Top => new Point(port.X, port.Y - distance),
            WorkflowPort.Bottom => new Point(port.X, port.Y + distance),
            WorkflowPort.Left => new Point(port.X - distance, port.Y),
            WorkflowPort.Right => new Point(port.X + distance, port.Y),
            _ => port,
        };

    private static List<Point> FindGridPath(
        Point from,
        Point to,
        IReadOnlyList<NodeObstacle> obstacles,
        IReadOnlySet<GridCell>? reservedCells)
    {
        from = Snap(from);
        to = Snap(to);

        if (NearlyEqual(from, to))
            return [from];

        var start = WorldToCell(from);
        var goal = WorldToCell(to);

        if (start == goal)
            return [from, to];

        var bounds = ComputeBounds(from, to, obstacles);
        var cellPath = AStar(start, goal, bounds, obstacles, reservedCells);
        if (cellPath is null || cellPath.Count == 0)
            return FallbackCorridor(from, to, obstacles);

        var simplified = SimplifyCellPath(cellPath);
        var points = new List<Point> { from };
        foreach (var cell in simplified)
        {
            var world = CellToWorld(cell);
            if (!NearlyEqual(points[^1], world))
                points.Add(world);
        }

        if (!NearlyEqual(points[^1], to))
            points.Add(to);

        return FinalizeOrthogonalPath(points, obstacles);
    }

    private static List<Point> FinalizeOrthogonalPath(IReadOnlyList<Point> path, IReadOnlyList<NodeObstacle> obstacles)
    {
        var current = SimplifyPolyline(path);
        for (var pass = 0; pass < 3; pass++)
        {
            var next = CompressOrthogonalPath(current, obstacles);
            next = MergeCollinearOrthogonal(next);
            next = RemoveMicroZigZags(next);
            next = SimplifyPolyline(next);
            if (next.Count == current.Count && PointsEqual(next, current))
                break;
            current = next;
        }

        return current;
    }

    private static bool PointsEqual(IReadOnlyList<Point> a, IReadOnlyList<Point> b)
    {
        if (a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!NearlyEqual(a[i], b[i]))
                return false;
        }

        return true;
    }

    private static List<Point>? TrySimpleOrthogonalRoute(
        Point from,
        Point to,
        IReadOnlyList<NodeObstacle> obstacles)
    {
        var candidates = BuildOrthogonalCandidates(from, to);
        return PickBestClearPath(candidates, obstacles);
    }

    private static List<Point> CompressOrthogonalPath(
        IReadOnlyList<Point> path,
        IReadOnlyList<NodeObstacle> obstacles)
    {
        if (path.Count <= 2)
            return path.ToList();

        var result = new List<Point> { path[0] };
        var index = 0;
        while (index < path.Count - 1)
        {
            var jumped = false;
            for (var j = path.Count - 1; j > index; j--)
            {
                if (!TrySmoothBridge(result[^1], path[j], obstacles, out var bridge))
                    continue;

                for (var k = 1; k < bridge.Count; k++)
                    result.Add(bridge[k]);

                index = j;
                jumped = true;
                break;
            }

            if (jumped)
                continue;

            if (!NearlyEqual(result[^1], path[index + 1]))
                result.Add(path[index + 1]);
            index++;
        }

        return SimplifyPolyline(result);
    }

    private static bool TrySmoothBridge(
        Point from,
        Point to,
        IReadOnlyList<NodeObstacle> obstacles,
        out List<Point> bridge)
    {
        bridge = new List<Point>();
        if (NearlyEqual(from, to))
        {
            bridge.Add(from);
            return true;
        }

        var best = PickBestClearPath(BuildOrthogonalCandidates(from, to), obstacles);
        if (best is null)
            return false;

        bridge = best;
        return true;
    }

    private static List<List<Point>> BuildOrthogonalCandidates(Point from, Point to)
    {
        var candidates = new List<List<Point>>
        {
            new() { from, new Point(to.X, from.Y), to },
            new() { from, new Point(from.X, to.Y), to },
        };

        var midX = Snap((from.X + to.X) * 0.5);
        var midY = Snap((from.Y + to.Y) * 0.5);
        candidates.Add(new() { from, new Point(midX, from.Y), new Point(midX, to.Y), to });
        candidates.Add(new() { from, new Point(from.X, midY), new Point(to.X, midY), to });

        for (var i = 1; i <= 12; i++)
        {
            var off = GridSize * i;
            candidates.Add(new() { from, new Point(from.X, Snap(from.Y - off)), new Point(to.X, Snap(from.Y - off)), to });
            candidates.Add(new() { from, new Point(from.X, Snap(from.Y + off)), new Point(to.X, Snap(from.Y + off)), to });
            candidates.Add(new() { from, new Point(Snap(from.X - off), from.Y), new Point(Snap(from.X - off), to.Y), to });
            candidates.Add(new() { from, new Point(Snap(from.X + off), from.Y), new Point(Snap(from.X + off), to.Y), to });
        }

        return candidates;
    }

    private static List<Point>? PickBestClearPath(
        IEnumerable<List<Point>> candidates,
        IReadOnlyList<NodeObstacle> obstacles)
    {
        List<Point>? best = null;
        var bestScore = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var simplified = SimplifyPolyline(candidate);
            var score = ScorePath(simplified, obstacles);
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = simplified;
        }

        return bestScore < 100_000 ? best : null;
    }

    private static List<Cell>? AStar(
        Cell start,
        Cell goal,
        Rect bounds,
        IReadOnlyList<NodeObstacle> obstacles,
        IReadOnlySet<GridCell>? reservedCells)
    {
        var open = new PriorityQueue<Cell, int>();
        var cameFrom = new Dictionary<Cell, Cell>();
        var gScore = new Dictionary<Cell, int> { [start] = 0 };
        open.Enqueue(start, Heuristic(start, goal));

        var maxIterations = 80_000;
        var iterations = 0;

        while (open.Count > 0 && iterations++ < maxIterations)
        {
            var current = open.Dequeue();
            if (current == goal)
                return Reconstruct(cameFrom, current);

            foreach (var next in Neighbors(current))
            {
                if (!IsInsideBounds(next, bounds) || IsCellBlocked(next, obstacles))
                    continue;

                var tentative = gScore[current] + GridSize;
                if (cameFrom.TryGetValue(current, out var prev))
                {
                    var prevDirX = current.X - prev.X;
                    var prevDirY = current.Y - prev.Y;
                    var nextDirX = next.X - current.X;
                    var nextDirY = next.Y - current.Y;
                    if (prevDirX != nextDirX || prevDirY != nextDirY)
                        tentative += GridSize * 8;
                }

                if (reservedCells?.Contains(ToGridCell(next)) == true)
                    tentative += GridSize;
                if (gScore.TryGetValue(next, out var known) && tentative >= known)
                    continue;

                cameFrom[next] = current;
                gScore[next] = tentative;
                open.Enqueue(next, tentative + Heuristic(next, goal));
            }
        }

        return null;
    }

    private static List<Cell> Reconstruct(Dictionary<Cell, Cell> cameFrom, Cell current)
    {
        var path = new List<Cell> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Add(current);
        }

        path.Reverse();
        return SimplifyCellPath(path);
    }

    private static List<Cell> SimplifyCellPath(List<Cell> path)
    {
        if (path.Count <= 2)
            return path;

        var result = new List<Cell> { path[0] };
        for (var i = 1; i < path.Count - 1; i++)
        {
            var prev = result[^1];
            var cur = path[i];
            var next = path[i + 1];
            var sameDir = (cur.X - prev.X == next.X - cur.X) && (cur.Y - prev.Y == next.Y - cur.Y);
            if (!sameDir)
                result.Add(cur);
        }

        result.Add(path[^1]);
        return result;
    }

    private static int Heuristic(Cell a, Cell b) =>
        (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)) * GridSize;

    private static IEnumerable<Cell> Neighbors(Cell c)
    {
        yield return new Cell(c.X + 1, c.Y);
        yield return new Cell(c.X - 1, c.Y);
        yield return new Cell(c.X, c.Y + 1);
        yield return new Cell(c.X, c.Y - 1);
    }

    private static bool IsCellBlocked(Cell cell, IReadOnlyList<NodeObstacle> obstacles)
    {
        var rect = new Rect(
            cell.X * GridSize - GridSize / 2.0,
            cell.Y * GridSize - GridSize / 2.0,
            GridSize,
            GridSize);

        foreach (var o in obstacles)
        {
            if (RectsIntersect(rect, o.Inflated))
                return true;
        }

        return false;
    }

    private static bool PointInsideAnyObstacle(Point p, IReadOnlyList<NodeObstacle> obstacles)
    {
        foreach (var o in obstacles)
        {
            if (p.X >= o.Inflated.Left && p.X <= o.Inflated.Right
                && p.Y >= o.Inflated.Top && p.Y <= o.Inflated.Bottom)
                return true;
        }

        return false;
    }

    private static Rect ComputeBounds(Point from, Point to, IReadOnlyList<NodeObstacle> obstacles)
    {
        var left = Math.Min(from.X, to.X);
        var top = Math.Min(from.Y, to.Y);
        var right = Math.Max(from.X, to.X);
        var bottom = Math.Max(from.Y, to.Y);

        foreach (var o in obstacles)
        {
            left = Math.Min(left, o.Inflated.Left);
            top = Math.Min(top, o.Inflated.Top);
            right = Math.Max(right, o.Inflated.Right);
            bottom = Math.Max(bottom, o.Inflated.Bottom);
        }

        var pad = GridSize * 4;
        return new Rect(left - pad, top - pad, right - left + pad * 2, bottom - top + pad * 2);
    }

    private static bool IsInsideBounds(Cell cell, Rect bounds)
    {
        var x = cell.X * GridSize;
        var y = cell.Y * GridSize;
        return x >= bounds.Left && x <= bounds.Right && y >= bounds.Top && y <= bounds.Bottom;
    }

    private static Cell WorldToCell(Point p) =>
        new((int)Math.Round(p.X / GridSize), (int)Math.Round(p.Y / GridSize));

    private static Point CellToWorld(Cell c) =>
        new(c.X * GridSize, c.Y * GridSize);

    private static List<Point> FallbackCorridor(Point from, Point to, IReadOnlyList<NodeObstacle> obstacles)
    {
        var minX = Math.Min(from.X, to.X) - GridSize * 3;
        var maxX = Math.Max(from.X, to.X) + GridSize * 3;
        var minY = Math.Min(from.Y, to.Y) - GridSize * 3;
        var maxY = Math.Max(from.Y, to.Y) + GridSize * 3;

        foreach (var o in obstacles)
        {
            minX = Math.Min(minX, o.Inflated.Left - GridSize * 2);
            maxX = Math.Max(maxX, o.Inflated.Right + GridSize * 2);
            minY = Math.Min(minY, o.Inflated.Top - GridSize * 2);
            maxY = Math.Max(maxY, o.Inflated.Bottom + GridSize * 2);
        }

        var topY = Snap(minY);
        var bottomY = Snap(maxY);
        var leftX = Snap(minX);
        var rightX = Snap(maxX);

        var candidates = new List<List<Point>>
        {
            new() { from, new Point(rightX, from.Y), new Point(rightX, to.Y), to },
            new() { from, new Point(leftX, from.Y), new Point(leftX, to.Y), to },
            new() { from, new Point(from.X, topY), new Point(to.X, topY), to },
            new() { from, new Point(from.X, bottomY), new Point(to.X, bottomY), to },
        };
        candidates.AddRange(BuildOrthogonalCandidates(from, to));

        return PickBestClearPath(candidates, obstacles) ?? [from, to];
    }

    private static int ScorePath(IReadOnlyList<Point> path, IReadOnlyList<NodeObstacle> obstacles)
    {
        var hits = 0;
        var length = 0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            length += (int)(Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y));
            if (SegmentHitsAnyObstacle(a, b, obstacles))
                hits++;
        }

        var bends = Math.Max(0, path.Count - 2);
        return hits * 100_000 + bends * 8_000 + length;
    }

    private static bool SegmentHitsAnyObstacle(Point a, Point b, IReadOnlyList<NodeObstacle> obstacles)
    {
        foreach (var o in obstacles)
        {
            var r = o.Inflated;
            if (Math.Abs(a.Y - b.Y) < 0.01)
            {
                var y = a.Y;
                var xMin = Math.Min(a.X, b.X);
                var xMax = Math.Max(a.X, b.X);
                if (y >= r.Top && y <= r.Bottom && xMax >= r.Left && xMin <= r.Right)
                    return true;
            }
            else if (Math.Abs(a.X - b.X) < 0.01)
            {
                var x = a.X;
                var yMin = Math.Min(a.Y, b.Y);
                var yMax = Math.Max(a.Y, b.Y);
                if (x >= r.Left && x <= r.Right && yMax >= r.Top && yMin <= r.Bottom)
                    return true;
            }
        }

        return false;
    }

    private static bool RectsIntersect(Rect a, Rect b) =>
        a.Right >= b.Left && a.Left <= b.Right && a.Bottom >= b.Top && a.Top <= b.Bottom;

    private static List<Point> SimplifyPolyline(IEnumerable<Point> points)
    {
        var list = new List<Point>();
        foreach (var p in points)
        {
            if (list.Count == 0 || !NearlyEqual(list[^1], p))
                list.Add(p);
        }

        if (list.Count <= 2)
            return list;

        var result = new List<Point> { list[0] };
        for (var i = 1; i < list.Count - 1; i++)
        {
            var prev = result[^1];
            var cur = list[i];
            var next = list[i + 1];
            var collinear = Math.Abs((prev.X - cur.X) * (cur.Y - next.Y) - (prev.Y - cur.Y) * (cur.X - next.X)) < 0.01;
            if (!collinear)
                result.Add(cur);
        }

        result.Add(list[^1]);
        return result;
    }

    private static bool NearlyEqual(Point a, Point b) =>
        Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;

    private static GridCell ToGridCell(Cell c) => new(c.X, c.Y);

    private static IEnumerable<Cell> CellsAlongSegment(Point a, Point b)
    {
        a = Snap(a);
        b = Snap(b);
        if (Math.Abs(a.X - b.X) < 0.01)
        {
            var x = WorldToCell(a).X;
            var y0 = Math.Min(WorldToCell(a).Y, WorldToCell(b).Y);
            var y1 = Math.Max(WorldToCell(a).Y, WorldToCell(b).Y);
            for (var y = y0; y <= y1; y++)
                yield return new Cell(x, y);
            yield break;
        }

        if (Math.Abs(a.Y - b.Y) < 0.01)
        {
            var y = WorldToCell(a).Y;
            var x0 = Math.Min(WorldToCell(a).X, WorldToCell(b).X);
            var x1 = Math.Max(WorldToCell(a).X, WorldToCell(b).X);
            for (var x = x0; x <= x1; x++)
                yield return new Cell(x, y);
        }
    }

    private static List<Point> MergeCollinearOrthogonal(List<Point> path)
    {
        if (path.Count <= 2)
            return path;

        var result = new List<Point> { path[0] };
        for (var i = 1; i < path.Count - 1; i++)
        {
            var prev = result[^1];
            var cur = path[i];
            var next = path[i + 1];

            var horizontal = Math.Abs(prev.Y - cur.Y) < 0.01 && Math.Abs(cur.Y - next.Y) < 0.01;
            var vertical = Math.Abs(prev.X - cur.X) < 0.01 && Math.Abs(cur.X - next.X) < 0.01;
            if (horizontal || vertical)
                continue;

            result.Add(cur);
        }

        result.Add(path[^1]);
        return result;
    }

    private static List<Point> RemoveMicroZigZags(List<Point> path)
    {
        if (path.Count <= 3)
            return path;

        var result = new List<Point> { path[0] };
        for (var i = 1; i < path.Count - 1; i++)
        {
            var prev = result[^1];
            var cur = path[i];
            var next = path[i + 1];
            var sameAxisBack =
                (Math.Abs(prev.X - cur.X) < 0.01 && Math.Abs(cur.X - next.X) < 0.01 && Math.Sign(cur.Y - prev.Y) != Math.Sign(next.Y - cur.Y))
                || (Math.Abs(prev.Y - cur.Y) < 0.01 && Math.Abs(cur.Y - next.Y) < 0.01 && Math.Sign(cur.X - prev.X) != Math.Sign(next.X - cur.X));
            if (!sameAxisBack)
                result.Add(cur);
        }

        result.Add(path[^1]);
        return result;
    }
}
