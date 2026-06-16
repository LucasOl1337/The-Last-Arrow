using NUnit.Framework;
using ProjectPVP.Match;

namespace ProjectPVP.Tests.Editor
{
    public sealed class RoundTimerServiceTests
    {
        [Test]
        public void BeginRespawnFreeze_ReturnsWhetherControlsShouldLock()
        {
            RoundTimerService timers = new RoundTimerService();

            bool zeroDurationLocks = timers.BeginRespawnFreeze(0f);

            Assert.That(zeroDurationLocks, Is.False);
            Assert.That(timers.IsRespawnFreezeActive, Is.False);

            bool positiveDurationLocks = timers.BeginRespawnFreeze(0.5f);

            Assert.That(positiveDurationLocks, Is.True);
            Assert.That(timers.IsRespawnFreezeActive, Is.True);
            Assert.That(timers.RespawnFreezeTimeLeft, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Tick_ReportsFreezeEndOnlyOnTransition()
        {
            RoundTimerService timers = new RoundTimerService();
            timers.BeginRespawnFreeze(0.5f);

            RoundTimerTickResult firstTick = timers.Tick(0.2f);
            RoundTimerTickResult secondTick = timers.Tick(0.3f);
            RoundTimerTickResult thirdTick = timers.Tick(0.3f);

            Assert.That(firstTick.RespawnFreezeEnded, Is.False);
            Assert.That(secondTick.RespawnFreezeEnded, Is.True);
            Assert.That(thirdTick.RespawnFreezeEnded, Is.False);
            Assert.That(timers.IsRespawnFreezeActive, Is.False);
        }

        [Test]
        public void ClearRespawnFreeze_DeactivatesWithoutTickTransition()
        {
            RoundTimerService timers = new RoundTimerService();
            timers.BeginRespawnFreeze(1f);

            timers.ClearRespawnFreeze();
            RoundTimerTickResult tick = timers.Tick(1f);

            Assert.That(timers.IsRespawnFreezeActive, Is.False);
            Assert.That(tick.RespawnFreezeEnded, Is.False);
        }

        [Test]
        public void ChampionAnnouncement_IsVisibleUntilDurationExpires()
        {
            RoundTimerService timers = new RoundTimerService();

            timers.ShowChampionAnnouncement(CombatantSlotId.SlotTwo, 1f);
            RoundTimerTickResult firstTick = timers.Tick(0.4f);
            RoundTimerTickResult secondTick = timers.Tick(0.6f);

            Assert.That(firstTick.ChampionAnnouncementEnded, Is.False);
            Assert.That(timers.ChampionAnnouncementSlot, Is.EqualTo(CombatantSlotId.SlotTwo));
            Assert.That(secondTick.ChampionAnnouncementEnded, Is.True);
            Assert.That(timers.ChampionAnnouncementSlot, Is.EqualTo(CombatantSlotId.None));
        }

        [Test]
        public void NonPositiveDelta_DoesNotAdvanceTimers()
        {
            RoundTimerService timers = new RoundTimerService();
            timers.BeginRespawnFreeze(0.25f);
            timers.ShowChampionAnnouncement(CombatantSlotId.SlotOne, 0.25f);

            RoundTimerTickResult tick = timers.Tick(0f);

            Assert.That(tick.RespawnFreezeEnded, Is.False);
            Assert.That(tick.ChampionAnnouncementEnded, Is.False);
            Assert.That(timers.RespawnFreezeTimeLeft, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(timers.ChampionAnnouncementTimeLeft, Is.EqualTo(0.25f).Within(0.001f));
        }
    }
}
