using System;
using System.Collections.Generic;
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
    }
}
