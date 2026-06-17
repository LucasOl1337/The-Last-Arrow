using System;
using System.Collections.Generic;
using ProjectPVP.Data;
using UnityEngine;

namespace ProjectPVP.Characters
{
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "ProjectPVP/Characters/Character Catalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        public List<CharacterBootstrapProfile> characters = new List<CharacterBootstrapProfile>();

        public IReadOnlyList<CharacterBootstrapProfile> Characters => characters;

        public CharacterBootstrapProfile FindById(string id)
        {
            if (characters == null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            string normalizedId = id.Trim();
            for (int index = 0; index < characters.Count; index += 1)
            {
                CharacterBootstrapProfile candidate = characters[index];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                {
                    continue;
                }

                if (string.Equals(candidate.id.Trim(), normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        public CharacterBootstrapProfile FindByDefinition(CharacterDefinition definition)
        {
            if (characters == null || definition == null)
            {
                return null;
            }

            for (int index = 0; index < characters.Count; index += 1)
            {
                CharacterBootstrapProfile candidate = characters[index];
                if (candidate != null && candidate.ResolveCharacterDefinition() == definition)
                {
                    return candidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(definition.id))
            {
                return FindById(definition.id);
            }

            return null;
        }
    }
}
