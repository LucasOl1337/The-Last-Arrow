using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Data
{
    [CreateAssetMenu(fileName = "CharacterAnimationCatalog", menuName = "ProjectPVP/Character Animation Catalog")]
    public sealed class CharacterAnimationCatalog : ScriptableObject
    {
        public ActionCatalog actionCatalog;
        public List<ActionSpriteAnimation> actionSpriteAnimations = new List<ActionSpriteAnimation>();

        public ActionCatalog ResolveActionCatalog()
        {
            return actionCatalog != null ? actionCatalog : ActionCatalog.LoadDefault();
        }
    }
}
