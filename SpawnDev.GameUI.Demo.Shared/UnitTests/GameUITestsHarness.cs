using SpawnDev.UnitTesting;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>
/// PlaywrightMultiTest harness that exposes the existing GameUITests.RunAll()
/// assertion bundle as a single [TestMethod]. RunAll runs 318 real assertions
/// against production code (UIElement hierarchy, hit testing, theming, animation
/// math, focus navigation, layout, input state). The wrapper throws on any
/// failure so PMT sees the aggregate as one pass-or-fail signal.
///
/// Future work: decompose RunAll into per-feature [TestMethod] methods so the
/// PMT report reflects individual test names. For 0.1.0-rc.1 this single-row
/// gate is the minimum-bar exposure of real assertions to the test framework.
/// </summary>
public class GameUITestsHarness
{
    [TestMethod]
    public void GameUITests_AllAssertionsPass()
    {
        var (passed, failed, errors) = GameUITests.RunAll();
        if (failed > 0)
        {
            throw new Exception(
                $"GameUITests.RunAll: {failed} of {passed + failed} assertions failed: " +
                string.Join(", ", errors));
        }
        if (passed == 0)
        {
            throw new Exception("GameUITests.RunAll reported 0 assertions ran - test discovery is broken.");
        }
    }
}
