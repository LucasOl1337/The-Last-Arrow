using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Data
{
    [Serializable]
    public sealed class DirectionalSpriteAnimation
    {
        public string directionKey = "right";
        public float framesPerSecond = 12f;
        public bool loop = true;
        public List<Sprite> frames = new List<Sprite>();
    }

    [Serializable]
    public sealed class CharacterActionConfig
    {
        public string actionName = string.Empty;
        public bool skipLeft;
        public float targetVisualHeight;
        [Range(0f, 1f)] public float groundAnchorRatio = 1f;
        public float duration;
        public bool cancelable;
        public float speed = 12f;
        public float spriteScale = 1f;
        public Vector2 spriteOffset = Vector2.zero;
        public ActionColliderOverride colliderOverride;
        public List<DirectionalSpriteAnimation> animations = new List<DirectionalSpriteAnimation>();
    }
}
