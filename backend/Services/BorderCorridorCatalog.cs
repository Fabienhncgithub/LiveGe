namespace FrontiereLiveGe.Api.Services;

public static class BorderCorridorCatalog
{
    public static readonly IReadOnlyList<BorderCorridor> All =
    [
        new("Bardonnex", new(46.1248, 6.1207), new(46.1406, 6.1279), new(46.1564, 6.1349)),
        new("Perly", new(46.0929, 6.0643), new(46.1083, 6.0754), new(46.1237, 6.0867)),
        new("Moillesulaz", new(46.1805, 6.2228), new(46.1876, 6.2101), new(46.1978, 6.1905)),
        new("Thônex-Vallard", new(46.1842, 6.2340), new(46.1935, 6.2156), new(46.2014, 6.1980)),
        new("Anières", new(46.2837, 6.2363), new(46.2760, 6.2220), new(46.2632, 6.2110)),
        new("Meyrin", new(46.2459, 6.0642), new(46.2340, 6.0790), new(46.2263, 6.0974)),
        new("Ferney-Voltaire", new(46.2663, 6.1047), new(46.2550, 6.1080), new(46.2397, 6.1200))
    ];

    public static BorderCorridor? Find(string name) =>
        All.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    public static double DistanceToApproachKm(BorderCorridor corridor, double latitude, double longitude)
    {
        var point = Project(latitude, longitude, latitude);
        var france = Project(corridor.France.Latitude, corridor.France.Longitude, latitude);
        var crossing = Project(corridor.Crossing.Latitude, corridor.Crossing.Longitude, latitude);
        var geneva = Project(corridor.Geneva.Latitude, corridor.Geneva.Longitude, latitude);

        return Math.Min(
            DistanceToSegment(point, france, crossing),
            DistanceToSegment(point, crossing, geneva));
    }

    private static ProjectedPoint Project(double latitude, double longitude, double referenceLatitude)
    {
        const double kilometersPerDegreeLatitude = 110.574;
        var kilometersPerDegreeLongitude = 111.320 * Math.Cos(referenceLatitude * Math.PI / 180d);
        return new ProjectedPoint(
            longitude * kilometersPerDegreeLongitude,
            latitude * kilometersPerDegreeLatitude);
    }

    private static double DistanceToSegment(ProjectedPoint point, ProjectedPoint start, ProjectedPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
        {
            return Math.Sqrt(Math.Pow(point.X - start.X, 2) + Math.Pow(point.Y - start.Y, 2));
        }

        var t = Math.Clamp(
            ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy),
            0d,
            1d);
        var nearestX = start.X + t * dx;
        var nearestY = start.Y + t * dy;
        return Math.Sqrt(Math.Pow(point.X - nearestX, 2) + Math.Pow(point.Y - nearestY, 2));
    }

    public sealed record Coordinate(double Latitude, double Longitude);
    public sealed record BorderCorridor(
        string Name,
        Coordinate France,
        Coordinate Crossing,
        Coordinate Geneva);

    private sealed record ProjectedPoint(double X, double Y);
}
