using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class GamepadActionMapTests
    {
        [Test]
        public void CreateDefault_DisablesMoveStickAimFallback()
        {
            GamepadActionMap actionMap = GamepadActionMap.CreateDefault();

            Assert.That(actionMap.useMoveStickAsAimFallback, Is.False);
        }

        [Test]
        public void Clone_PreservesMoveStickAimFallback()
        {
            GamepadActionMap actionMap = new GamepadActionMap
            {
                useMoveStickAsAimFallback = true,
            };

            GamepadActionMap clone = actionMap.Clone();

            Assert.That(clone.useMoveStickAsAimFallback, Is.True);
        }
    }
}
