using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class PlayerCameraIntroExitTests
    {
        [Test]
        public void IntroExitSmoothTimeFadesToRegularAndNeverSmoothsFaster()
        {
            EveryCase.Parts(
                ("no blend remaining returns the regular smooth time", () =>
                {
                    Assert.That(PlayerCamera.BlendStableIntroExitSmoothTime(0.1f, 0.6f, 0f, 1.2f), Is.EqualTo(0.1f).Within(1e-6f));
                    Assert.That(PlayerCamera.BlendStableIntroExitSmoothTime(0.1f, 0.6f, 0.5f, 0f), Is.EqualTo(0.1f).Within(1e-6f));
                }),
                ("a full blend starts at the exit smooth time and fades linearly to regular", () =>
                {
                    Assert.That(PlayerCamera.BlendStableIntroExitSmoothTime(0.1f, 0.6f, 1.2f, 1.2f), Is.EqualTo(0.6f).Within(1e-6f));
                    Assert.That(PlayerCamera.BlendStableIntroExitSmoothTime(0.1f, 0.6f, 0.6f, 1.2f), Is.EqualTo(0.35f).Within(1e-6f));
                    Assert.That(PlayerCamera.BlendStableIntroExitSmoothTime(0.1f, 0.6f, 2.4f, 1.2f), Is.EqualTo(0.6f).Within(1e-6f), "remaining above duration clamps to full blend");
                }),
                ("never smooths faster than regular", () =>
                {
                    // The directional offset already uses a longer smooth time than the exit value; it must keep it.
                    Assert.That(PlayerCamera.BlendStableIntroExitSmoothTime(0.75f, 0.6f, 1.2f, 1.2f), Is.EqualTo(0.75f).Within(1e-6f));
                    Assert.That(PlayerCamera.BlendStableIntroExitSmoothTime(0f, 0.6f, 0f, 1.2f), Is.EqualTo(0.001f).Within(1e-6f), "zero regular smooth time is floored like before");
                }));
        }
    }
}
