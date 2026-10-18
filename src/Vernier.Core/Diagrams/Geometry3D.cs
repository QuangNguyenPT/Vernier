namespace Vernier.Core.Diagrams;

public readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public double Length => Math.Sqrt(Dot(this, this));
    public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static V3 Cross(V3 a, V3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static V3 From(double[]? v) => v is { Length: >= 2 } ? new(v[0], v[1], v.Length > 2 ? v[2] : 0) : default;
}

/// <summary>The numbers a 3D problem asks for, computed in code (the model never does the arithmetic).</summary>
public static class Geometry3D
{
    /// <summary>Shortest distance between the lines p+t·u and q+s·v, with the feet of the common perpendicular. Parallel lines give the distance between them.</summary>
    public static (double Distance, V3 OnFirst, V3 OnSecond, bool Skew) LineLine(V3 p, V3 u, V3 q, V3 v)
    {
        var n = V3.Cross(u, v);
        if (n.Length < 1e-9)
        {
            var w = q - p;
            var along = u.Length < 1e-12 ? 0 : V3.Dot(w, u) / V3.Dot(u, u);
            var foot = p + u * along;
            return ((q - foot).Length, foot, q, false);
        }
        // p + t·u + k·n = q + s·v, solved for t and s.
        var d = q - p;
        var nn = V3.Dot(n, n);
        var t = V3.Dot(V3.Cross(d, v), n) / nn;
        var s = V3.Dot(V3.Cross(d, u), n) / nn;
        var a = p + u * t;
        var b = q + v * s;
        return ((b - a).Length, a, b, true);
    }

    /// <summary>Distance from a point to the plane through <paramref name="planePoint"/> with normal <paramref name="normal"/>, and the foot of the perpendicular.</summary>
    public static (double Distance, V3 Foot) PointPlane(V3 point, V3 planePoint, V3 normal)
    {
        var len = normal.Length;
        var signed = V3.Dot(point - planePoint, normal) / len;
        return (Math.Abs(signed), point - normal * (signed / len));
    }

    /// <summary>The resultant of forces given as (size, direction in degrees anticlockwise from +x), and its components along and across a slope of <paramref name="slopeDeg"/> (along = up the slope).</summary>
    public static (double Fx, double Fy, double Size, double Direction, double Along, double Across) Resultant(IEnumerable<(double Size, double Degrees)> forces, double slopeDeg = 0)
    {
        double fx = 0, fy = 0;
        foreach (var (size, deg) in forces)
        {
            fx += size * Math.Cos(deg * Math.PI / 180);
            fy += size * Math.Sin(deg * Math.PI / 180);
        }
        double s = slopeDeg * Math.PI / 180;
        var direction = Math.Atan2(fy, fx) * 180 / Math.PI;
        return (Clean(fx), Clean(fy), Clean(Math.Sqrt(fx * fx + fy * fy)), direction < 0 ? direction + 360 : direction, Clean(fx * Math.Cos(s) + fy * Math.Sin(s)), Clean(-fx * Math.Sin(s) + fy * Math.Cos(s)));

        static double Clean(double v) => Math.Abs(v) < 1e-9 ? 0 : v;
    }

    public static double AngleDegrees(V3 a, V3 b) => Math.Acos(Math.Clamp(V3.Dot(a, b) / (a.Length * b.Length), -1, 1)) * 180 / Math.PI;
}

/// <summary>Orthographic view of 3D (z up) onto a plane (x right, y up), and a rotating 4D cube.</summary>
public static class Projection
{
    public const double DefaultAzimuth = -35, DefaultElevation = 22;

    public static (double X, double Y, double Depth) Project(V3 p, double azimuthDeg = DefaultAzimuth, double elevationDeg = DefaultElevation)
    {
        double az = azimuthDeg * Math.PI / 180, el = elevationDeg * Math.PI / 180;
        var xr = p.X * Math.Cos(az) - p.Y * Math.Sin(az);
        var yr = p.X * Math.Sin(az) + p.Y * Math.Cos(az);
        return (xr, p.Z * Math.Cos(el) - yr * Math.Sin(el), yr * Math.Cos(el) + p.Z * Math.Sin(el));
    }

    /// <summary>The corners and edges of a cube (dim 3: 8 and 12) or a tesseract (dim 4: 16 and 32), rotated by <paramref name="angleDeg"/> in the x–w plane and projected to 3D with the far cube smaller.</summary>
    public static (V3[] Corners, (int A, int B)[] Edges) Hypercube(int dim, double angleDeg)
    {
        var n = dim >= 4 ? 16 : 8;
        var corners = new V3[n];
        var edges = new List<(int, int)>();
        var a = angleDeg * Math.PI / 180;
        for (var i = 0; i < n; i++)
        {
            double x = (i & 1) == 0 ? -1 : 1, y = (i & 2) == 0 ? -1 : 1, z = (i & 4) == 0 ? -1 : 1, w = dim >= 4 ? ((i & 8) == 0 ? -1 : 1) : 0;
            var xr = x * Math.Cos(a) - w * Math.Sin(a);
            var wr = x * Math.Sin(a) + w * Math.Cos(a);
            var k = dim >= 4 ? 3 / (3 - wr) : 1;
            corners[i] = new V3(xr * k, y * k, z * k);
            for (var bit = 1; bit < n; bit <<= 1) if ((i & bit) == 0) edges.Add((i, i | bit));
        }
        return (corners, [.. edges]);
    }
}
