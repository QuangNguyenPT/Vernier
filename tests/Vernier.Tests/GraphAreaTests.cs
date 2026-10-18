using Vernier.Core.Geometry;
using Xunit;

namespace Vernier.Tests;

/// <summary>The area on a graph is computed by code from the model's reading of the axes, never by the model.</summary>
public class GraphAreaTests
{
    // A v–t graph in a screenshot: t = 0 at x = 100 px, t = 10 s at x = 600 px; v = 0 at y = 500 px, v = 20 m/s at y = 100 px (y grows downward).
    private static readonly AxisCalibration Axes = new(new(100, 0), new(600, 10), new(500, 0), new(100, 20));

    [Fact]
    public void Data_points_land_on_the_right_pixels_with_the_y_axis_pointing_up()
    {
        Assert.Equal(new PxPoint(100, 500), Axes.ToImage(0, 0));
        Assert.Equal(new PxPoint(300, 300), Axes.ToImage(4, 10));
        Assert.Equal(new PxPoint(600, 100), Axes.ToImage(10, 20));
        Assert.Null(Axes.Problem());
    }

    [Fact]
    public void Areas_are_exact_for_triangles_rectangles_and_trapezoids_in_either_order()
    {
        Assert.Equal(24, AxisCalibration.Area([(0, 0), (4, 12), (4, 0)]));            // ½·4·12
        Assert.Equal(24, AxisCalibration.Area([(4, 0), (4, 12), (0, 0)]));            // clockwise: same area
        Assert.Equal(60, AxisCalibration.Area([(4, 0), (4, 12), (9, 12), (9, 0)]));   // 5·12
        Assert.Equal(40, AxisCalibration.Area([(0, 0), (0, 6), (4, 14), (4, 0)]));    // ½(6+14)·4
    }

    [Fact]
    public void Readings_that_cannot_give_a_scale_and_outlines_that_cross_are_refused()
    {
        Assert.Contains("apart", new AxisCalibration(new(100, 0), new(110, 10), new(500, 0), new(100, 20)).Problem());
        Assert.Contains("same value", new AxisCalibration(new(100, 5), new(600, 5), new(500, 0), new(100, 20)).Problem());
        Assert.Contains("numbers", new AxisCalibration(new(double.NaN, 0), new(600, 10), new(500, 0), new(100, 20)).Problem());
        Assert.True(AxisCalibration.SelfIntersects([(0, 0), (4, 4), (4, 0), (0, 4)]));  // a bow tie
        Assert.False(AxisCalibration.SelfIntersects([(0, 0), (0, 4), (4, 4), (4, 0)]));
    }
}
