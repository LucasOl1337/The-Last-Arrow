using System;

namespace ProjectPVP.Match
{
    internal readonly struct RoundTimerTickResult
    {
        internal RoundTimerTickResult(bool respawnFreezeEnded, bool championAnnouncementEnded)
        {
            RespawnFreezeEnded = respawnFreezeEnded;
            ChampionAnnouncementEnded = championAnnouncementEnded;
        }

        internal bool RespawnFreezeEnded { get; }
        internal bool ChampionAnnouncementEnded { get; }
    }

    internal sealed class RoundTimerService
    {
        private float _respawnFreezeTimeLeft;
        private CombatantSlotId _championAnnouncementSlot = CombatantSlotId.None;
        private float _championAnnouncementTimeLeft;

        internal bool IsRespawnFreezeActive => _respawnFreezeTimeLeft > 0f;
        internal float RespawnFreezeTimeLeft => _respawnFreezeTimeLeft;
        internal CombatantSlotId ChampionAnnouncementSlot => _championAnnouncementTimeLeft > 0f
            ? _championAnnouncementSlot
            : CombatantSlotId.None;
        internal float ChampionAnnouncementTimeLeft => _championAnnouncementTimeLeft;

        internal bool BeginRespawnFreeze(float duration)
        {
            if (duration <= 0f)
            {
                _respawnFreezeTimeLeft = 0f;
                return false;
            }

            _respawnFreezeTimeLeft = duration;
            return true;
        }

        internal void ClearRespawnFreeze()
        {
            _respawnFreezeTimeLeft = 0f;
        }

        internal void ShowChampionAnnouncement(CombatantSlotId championSlot, float duration)
        {
            _championAnnouncementSlot = championSlot;
            _championAnnouncementTimeLeft = Math.Max(0f, duration);
        }

        internal RoundTimerTickResult Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return default;
            }

            bool freezeWasActive = _respawnFreezeTimeLeft > 0f;
            bool announcementWasActive = _championAnnouncementTimeLeft > 0f;

            if (freezeWasActive)
            {
                _respawnFreezeTimeLeft = Math.Max(0f, _respawnFreezeTimeLeft - deltaTime);
            }

            if (announcementWasActive)
            {
                _championAnnouncementTimeLeft = Math.Max(0f, _championAnnouncementTimeLeft - deltaTime);
                if (_championAnnouncementTimeLeft <= 0f)
                {
                    _championAnnouncementSlot = CombatantSlotId.None;
                }
            }

            return new RoundTimerTickResult(
                freezeWasActive && _respawnFreezeTimeLeft <= 0f,
                announcementWasActive && _championAnnouncementTimeLeft <= 0f);
        }
    }
}
