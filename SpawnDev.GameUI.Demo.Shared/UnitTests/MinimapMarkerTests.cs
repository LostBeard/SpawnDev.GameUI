using System.Drawing;
using SpawnDev.GameUI.Elements;
using SpawnDev.UnitTesting;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>Where the minimap's markers land: map units to pixels, out-of-range markers dropped or pinned to the border.</summary>
public class MinimapMarkerTests
{
    private static void Near(float a, float b, string what)
    {
        if (MathF.Abs(a - b) > 0.01f) throw new Exception($"{what}: {a}, expected {b}");
    }

    [TestMethod]
    public void TheCentre_IsTheMiddleOfTheMap_AndTheEdgesAreTheBorders_WithTheWholeDotInside()
    {
        var c = UIMinimapFrame.PlaceMarker(new MinimapMarker(0, 0, Color.Red, 4), 100)!.Value;
        Near(c.X, 50, "centre x"); Near(c.Y, 50, "centre y");
        var right = UIMinimapFrame.PlaceMarker(new MinimapMarker(1, 0, Color.Red, 4), 100)!.Value;
        Near(right.X, 98, "the right edge: the dot's centre is half a dot from the border"); Near(right.Y, 50, "right edge y");
        var topLeft = UIMinimapFrame.PlaceMarker(new MinimapMarker(-1, -1, Color.Red, 6), 100)!.Value;
        Near(topLeft.X, 3, "top left x"); Near(topLeft.Y, 3, "top left y");
        var down = UIMinimapFrame.PlaceMarker(new MinimapMarker(0, 0.5f, Color.Red, 4), 100)!.Value;
        Near(down.Y, 50 + 0.5f * 48, "y grows downward");
    }

    [TestMethod]
    public void AMarkerOutsideTheMap_IsLeftOut_OrPinnedToTheBorderInItsDirection()
    {
        if (UIMinimapFrame.PlaceMarker(new MinimapMarker(1.5f, 0, Color.Red), 100) != null) throw new Exception("out of the map: left out");
        if (UIMinimapFrame.PlaceMarker(new MinimapMarker(0, -2, Color.Red), 100) != null) throw new Exception("out of the map (above): left out");
        var pinned = UIMinimapFrame.PlaceMarker(new MinimapMarker(2, 1, Color.Red, 4, PinToEdge: true), 100)!.Value;
        Near(pinned.X, 98, "pinned: on the right border"); Near(pinned.Y, 50 + 0.5f * 48, "pinned: in the same direction (the ratio 2:1 kept)");
        var diagonal = UIMinimapFrame.PlaceMarker(new MinimapMarker(-3, 3, Color.Red, 4, PinToEdge: true), 100)!.Value;
        Near(diagonal.X, 2, "pinned diagonally: the corner"); Near(diagonal.Y, 98, "pinned diagonally: the corner");
    }

    [TestMethod]
    public void ANonsenseMarker_IsLeftOut_NotDrawnAtNaN()
    {
        if (UIMinimapFrame.PlaceMarker(new MinimapMarker(float.NaN, 0, Color.Red), 100) != null) throw new Exception("NaN is left out");
        if (UIMinimapFrame.PlaceMarker(new MinimapMarker(float.PositiveInfinity, 0, Color.Red, 4, PinToEdge: true), 100) != null) throw new Exception("infinity is left out");
    }
}
