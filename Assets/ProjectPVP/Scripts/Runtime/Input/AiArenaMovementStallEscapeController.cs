using UnityEngine;

namespace ProjectPVP.Input
{
    internal sealed class AiArenaMovementStallEscapeController
    {
        private const float AxisThreshold = 0.85f;
        private const float MinimumDisplacement = 12f;
        private const int FrameThreshold = 18;
        private const int EscapeFrameDuration = 8;

        private int _axisSign;
        private int _frameCount;
        private float _startX;
        private bool _latched;
        private int _escapeFramesLeft;

        public bool TriggeredThisFrame { get; private set; }
        public bool EscapedThisFrame { get; private set; }

        public PlayerInputFrame Observe(AiArenaSnapshotEnvelope snapshot, PlayerInputFrame frame)
        {
            TriggeredThisFrame = false;
            EscapedThisFrame = false;

            if (snapshot == null
                || snapshot.self == null
                || snapshot.semantics == null
                || snapshot.arena == null
                || snapshot.arena.roundResetPending
                || !snapshot.semantics.hasTarget)
            {
                Reset();
                return frame;
            }

            int currentAxisSign = ResolveStrongAxisSign(frame.axis);
            if (currentAxisSign == 0)
            {
                Reset();
                return frame;
            }

            float currentX = snapshot.self.position.x;
            if (currentAxisSign != _axisSign)
            {
                _axisSign = currentAxisSign;
                _frameCount = 1;
                _startX = currentX;
                _latched = false;
                _escapeFramesLeft = 0;
                return frame;
            }

            _frameCount += 1;
            if (Mathf.Abs(currentX - _startX) > MinimumDisplacement)
            {
                _frameCount = 1;
                _startX = currentX;
                _latched = false;
                _escapeFramesLeft = 0;
                return frame;
            }

            if (_latched)
            {
                if (_escapeFramesLeft > 0)
                {
                    return ConsumeEscapeFrame(snapshot, frame, currentAxisSign);
                }

                _latched = false;
                _frameCount = 1;
                _startX = currentX;
                return frame;
            }

            if (_frameCount < FrameThreshold)
            {
                return frame;
            }

            TriggeredThisFrame = true;
            _latched = true;
            _escapeFramesLeft = EscapeFrameDuration;
            return ConsumeEscapeFrame(snapshot, frame, currentAxisSign);
        }

        public void Reset()
        {
            _axisSign = 0;
            _frameCount = 0;
            _startX = 0f;
            _latched = false;
            _escapeFramesLeft = 0;
            TriggeredThisFrame = false;
            EscapedThisFrame = false;
        }

        private PlayerInputFrame ConsumeEscapeFrame(
            AiArenaSnapshotEnvelope snapshot,
            PlayerInputFrame frame,
            int stalledAxisSign)
        {
            if (_escapeFramesLeft <= 0)
            {
                return frame;
            }

            _escapeFramesLeft -= 1;
            EscapedThisFrame = true;
            return BuildEscapeFrame(snapshot, frame, stalledAxisSign);
        }

        private static PlayerInputFrame BuildEscapeFrame(
            AiArenaSnapshotEnvelope snapshot,
            PlayerInputFrame frame,
            int stalledAxisSign)
        {
            float escapeAxis = Mathf.Clamp(-stalledAxisSign, -1f, 1f);
            bool jump = snapshot.self.isGrounded;
            bool dash = snapshot.self.dashCooldownLeft <= 0.01f && !snapshot.self.isDashing;

            frame.axis = escapeAxis;
            frame.left = escapeAxis < -0.1f;
            frame.right = escapeAxis > 0.1f;
            frame.up = frame.up || jump;
            frame.down = false;
            frame.jumpPressed = frame.jumpPressed || jump;
            frame.jumpHeld = frame.jumpHeld || jump;
            frame.dashPrimaryPressed = frame.dashPrimaryPressed || dash;
            frame.dashSecondaryPressed = false;
            frame.shootPressed = false;
            frame.shootHeld = false;
            frame.meleePressed = false;
            frame.ultimatePressed = false;
            return frame;
        }

        private static int ResolveStrongAxisSign(float axis)
        {
            if (axis >= AxisThreshold)
            {
                return 1;
            }

            if (axis <= -AxisThreshold)
            {
                return -1;
            }

            return 0;
        }
    }
}
