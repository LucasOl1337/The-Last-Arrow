using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class KeyboardPlayerInputSourceTests
    {
        private static readonly FieldInfo JumpBufferLeftField =
            typeof(KeyboardPlayerInputSource).GetField("_jumpBufferLeft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ShootBufferLeftField =
            typeof(KeyboardPlayerInputSource).GetField("_shootBufferLeft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MeleeBufferLeftField =
            typeof(KeyboardPlayerInputSource).GetField("_meleeBufferLeft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UltimateBufferLeftField =
            typeof(KeyboardPlayerInputSource).GetField("_ultimateBufferLeft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DashPrimaryBufferLeftField =
            typeof(KeyboardPlayerInputSource).GetField("_dashPrimaryBufferLeft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DashSecondaryBufferLeftField =
            typeof(KeyboardPlayerInputSource).GetField("_dashSecondaryBufferLeft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DashSecondaryAxisHeldLastFrameField =
            typeof(KeyboardPlayerInputSource).GetField("_dashSecondaryAxisHeldLastFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnDisableMethod =
            typeof(KeyboardPlayerInputSource).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void ShouldSuppressDashSecondaryAxis_SuppressesDpadAndRightStickInput()
        {
            Assert.That(
                KeyboardPlayerInputSource.ShouldSuppressDashSecondaryAxis(
                    new Vector2(1f, 0f),
                    Vector2.zero,
                    0.22f),
                Is.True);

            Assert.That(
                KeyboardPlayerInputSource.ShouldSuppressDashSecondaryAxis(
                    Vector2.zero,
                    new Vector2(0.4f, 0f),
                    0.22f),
                Is.True);
        }

        [Test]
        public void ShouldSuppressDashSecondaryAxis_AllowsNeutralAimState()
        {
            Assert.That(
                KeyboardPlayerInputSource.ShouldSuppressDashSecondaryAxis(
                    Vector2.zero,
                    new Vector2(0.1f, 0.1f),
                    0.22f),
                Is.False);
        }

        [Test]
        public void OnDisable_ClearsBufferedAndLatchedInputState()
        {
            Assert.That(JumpBufferLeftField, Is.Not.Null);
            Assert.That(ShootBufferLeftField, Is.Not.Null);
            Assert.That(MeleeBufferLeftField, Is.Not.Null);
            Assert.That(UltimateBufferLeftField, Is.Not.Null);
            Assert.That(DashPrimaryBufferLeftField, Is.Not.Null);
            Assert.That(DashSecondaryBufferLeftField, Is.Not.Null);
            Assert.That(DashSecondaryAxisHeldLastFrameField, Is.Not.Null);
            Assert.That(OnDisableMethod, Is.Not.Null);

            GameObject root = new GameObject("keyboard_input_disable_test");
            KeyboardPlayerInputSource input = root.AddComponent<KeyboardPlayerInputSource>();

            try
            {
                JumpBufferLeftField.SetValue(input, 0.4f);
                ShootBufferLeftField.SetValue(input, 0.4f);
                MeleeBufferLeftField.SetValue(input, 0.4f);
                UltimateBufferLeftField.SetValue(input, 0.4f);
                DashPrimaryBufferLeftField.SetValue(input, 0.4f);
                DashSecondaryBufferLeftField.SetValue(input, 0.4f);
                DashSecondaryAxisHeldLastFrameField.SetValue(input, true);

                OnDisableMethod.Invoke(input, null);

                Assert.That((float)JumpBufferLeftField.GetValue(input), Is.EqualTo(0f));
                Assert.That((float)ShootBufferLeftField.GetValue(input), Is.EqualTo(0f));
                Assert.That((float)MeleeBufferLeftField.GetValue(input), Is.EqualTo(0f));
                Assert.That((float)UltimateBufferLeftField.GetValue(input), Is.EqualTo(0f));
                Assert.That((float)DashPrimaryBufferLeftField.GetValue(input), Is.EqualTo(0f));
                Assert.That((float)DashSecondaryBufferLeftField.GetValue(input), Is.EqualTo(0f));
                Assert.That((bool)DashSecondaryAxisHeldLastFrameField.GetValue(input), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
