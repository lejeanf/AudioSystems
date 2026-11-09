using NUnit.Framework;

namespace jeanf.audiosystems.tests
{
    /// <summary>
    /// The main-thread phase rule of <see cref="BpmSampler.BeatPhase"/>: 0 at the beat, 1 at the
    /// next one, clamped at both ends. A phase that wrapped past 1 would make a follower jump a
    /// whole cycle when the bpm has just been lowered and the next beat is later than the published
    /// period predicts.
    /// </summary>
    [TestFixture]
    public class BpmSamplerBeatClockTests
    {
        private const double Period = 60.0 / 84.0;

        [Test]
        public void BeforeFirstBeat_IsZero()
        {
            Assert.AreEqual(0.0, BpmSampler.BeatPhase(100.0, 0.0, Period));
        }

        [Test]
        public void ZeroPeriod_IsZero()
        {
            Assert.AreEqual(0.0, BpmSampler.BeatPhase(100.0, 99.0, 0.0));
        }

        [Test]
        public void AtBeat_IsZero()
        {
            Assert.AreEqual(0.0, BpmSampler.BeatPhase(100.0, 100.0, Period));
        }

        [Test]
        public void HalfwayThroughPeriod_IsHalf()
        {
            Assert.AreEqual(0.5, BpmSampler.BeatPhase(100.0 + Period / 2.0, 100.0, Period), 1e-9);
        }

        [Test]
        public void PastNextBeat_HoldsAtOne()
        {
            Assert.AreEqual(1.0, BpmSampler.BeatPhase(100.0 + Period * 1.7, 100.0, Period));
        }

        [Test]
        public void ClockAheadOfCaller_IsZero()
        {
            // The audio thread can schedule a beat slightly ahead of the main thread's dspTime reading.
            Assert.AreEqual(0.0, BpmSampler.BeatPhase(99.99, 100.0, Period));
        }
    }
}
