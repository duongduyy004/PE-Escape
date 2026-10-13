using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class RunnerStrideTests
    {
        [TestCase(9f, 1f)]
        [TestCase(4.5f, .5f)]
        [TestCase(1f, .5f)]
        [TestCase(20f, 1.5f)]
        [TestCase(0f, 1f)]
        public void RunCyclePlaysAtTheSpeedTheRunnerCoversGround(float metersPerSecond, float expected) =>
            Assert.That(RunnerStride.Playback(metersPerSecond), Is.EqualTo(expected).Within(1e-4f));
    }
}
