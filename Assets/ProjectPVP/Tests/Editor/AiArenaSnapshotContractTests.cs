using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaSnapshotContractTests
    {
        [Test]
        public void AiArenaControllerSnapshot_ExposesExpectedPublicFields()
        {
            AssertPublicField<AiArenaControllerSnapshot, bool>("isValid");
            AssertPublicField<AiArenaControllerSnapshot, int>("slotId");
            AssertPublicField<AiArenaControllerSnapshot, string>("botId");
            AssertPublicField<AiArenaControllerSnapshot, string>("botDisplayName");
            AssertPublicField<AiArenaControllerSnapshot, string>("characterId");
            AssertPublicField<AiArenaControllerSnapshot, string>("displayName");
            AssertPublicField<AiArenaControllerSnapshot, string>("actionKey");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isDead");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isGrounded");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isTouchingWall");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isDashing");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isMeleeActive");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isShootAnimating");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isUltimateActive");
            AssertPublicField<AiArenaControllerSnapshot, bool>("isHitStunned");
            AssertPublicField<AiArenaControllerSnapshot, bool>("canParryProjectile");
            AssertPublicField<AiArenaControllerSnapshot, bool>("canBlockProjectiles");
            AssertPublicField<AiArenaControllerSnapshot, int>("arrows");
            AssertPublicField<AiArenaControllerSnapshot, int>("facing");
            AssertPublicField<AiArenaControllerSnapshot, float>("projectileInheritVelocityFactor");
            AssertPublicField<AiArenaControllerSnapshot, float>("shootCooldownLeft");
            AssertPublicField<AiArenaControllerSnapshot, float>("meleeCooldownLeft");
            AssertPublicField<AiArenaControllerSnapshot, float>("dashCooldownLeft");
            AssertPublicField<AiArenaControllerSnapshot, float>("ultimateCooldownLeft");
            AssertPublicField<AiArenaControllerSnapshot, float>("hitStunTimeLeft");
            AssertPublicField<AiArenaControllerSnapshot, Vector2>("position");
            AssertPublicField<AiArenaControllerSnapshot, Vector2>("velocity");
            AssertPublicField<AiArenaControllerSnapshot, Vector2>("meleeHitboxCenter");
            AssertPublicField<AiArenaControllerSnapshot, Vector2>("meleeHitboxSize");
            AssertPublicField<AiArenaControllerSnapshot, Vector2>("ultimateHitboxCenter");
            AssertPublicField<AiArenaControllerSnapshot, float>("ultimateHitboxRadius");
        }

        [Test]
        public void AiArenaProjectileSnapshot_ExposesExpectedPublicFields()
        {
            AssertPublicField<AiArenaProjectileSnapshot, bool>("isValid");
            AssertPublicField<AiArenaProjectileSnapshot, int>("sourceSlotId");
            AssertPublicField<AiArenaProjectileSnapshot, bool>("isStuck");
            AssertPublicField<AiArenaProjectileSnapshot, bool>("isDisarmed");
            AssertPublicField<AiArenaProjectileSnapshot, bool>("isCollectible");
            AssertPublicField<AiArenaProjectileSnapshot, Vector2>("position");
            AssertPublicField<AiArenaProjectileSnapshot, Vector2>("velocity");
            AssertPublicField<AiArenaProjectileSnapshot, Vector2>("travelDirection");
        }

        [Test]
        public void AiArenaArenaSnapshot_ExposesExpectedPublicFields()
        {
            AssertPublicField<AiArenaArenaSnapshot, Rect>("wrapBounds");
            AssertPublicField<AiArenaArenaSnapshot, bool>("roundResetPending");
            AssertPublicField<AiArenaArenaSnapshot, int>("roundsToChampion");
            AssertPublicField<AiArenaArenaSnapshot, int>("playerOneWins");
            AssertPublicField<AiArenaArenaSnapshot, int>("playerTwoWins");
            AssertPublicField<AiArenaArenaSnapshot, int>("currentRespawnSeedIndex");
            AssertPublicField<AiArenaArenaSnapshot, string>("currentRespawnSeedLabel");
            AssertPublicField<AiArenaArenaSnapshot, int>("pendingRoundWinnerSlot");
            AssertPublicField<AiArenaArenaSnapshot, int>("pendingChampionSlot");
            AssertPublicField<AiArenaArenaSnapshot, int>("championAnnouncementSlot");
        }

        private static void AssertPublicField<TSnapshot, TField>(string fieldName)
        {
            FieldInfo field = typeof(TSnapshot).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(field, Is.Not.Null, "Expected {0} to expose public field '{1}'.", typeof(TSnapshot).Name, fieldName);
            Assert.That(field.FieldType, Is.EqualTo(typeof(TField)), "Unexpected field type for {0}.{1}.", typeof(TSnapshot).Name, fieldName);
        }
    }
}
