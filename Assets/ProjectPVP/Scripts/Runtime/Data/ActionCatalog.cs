using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Data
{
    [CreateAssetMenu(fileName = "ActionCatalog", menuName = "ProjectPVP/Action Catalog")]
    public sealed class ActionCatalog : ScriptableObject
    {
        private const string DefaultResourcePath = "ProjectPVP/Config/DefaultActionCatalog";

        [Serializable]
        public sealed class ActionAliasEntry
        {
            public string canonicalKey = string.Empty;
            public List<string> aliases = new List<string>();
        }

        [Serializable]
        public sealed class DirectionAliasEntry
        {
            public string canonicalKey = string.Empty;
            public List<string> aliases = new List<string>();
            public bool sharedFallback;
        }

        [SerializeField] private List<ActionAliasEntry> actionAliases = new List<ActionAliasEntry>();
        [SerializeField] private List<DirectionAliasEntry> directionAliases = new List<DirectionAliasEntry>();

        private static ActionCatalog s_runtimeDefault;

        public static ActionCatalog LoadDefault()
        {
            if (s_runtimeDefault == null)
            {
                s_runtimeDefault = Resources.Load<ActionCatalog>(DefaultResourcePath);
                if (s_runtimeDefault == null)
                {
                    s_runtimeDefault = CreateInstance<ActionCatalog>();
                    s_runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                    s_runtimeDefault.ApplyBuiltInDefaults();
                }
            }

            return s_runtimeDefault;
        }

        public IEnumerable<string> EnumerateActionKeys(string actionKey)
        {
            string normalized = NormalizeKey(actionKey);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            yield return normalized;

            if (!TryFindActionEntry(normalized, out ActionAliasEntry actionEntry))
            {
                yield break;
            }

            string canonicalKey = NormalizeKey(actionEntry.canonicalKey);
            if (!string.IsNullOrWhiteSpace(canonicalKey) && !string.Equals(canonicalKey, normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return canonicalKey;
            }
        }

        public string NormalizeDirectionKey(string directionKey)
        {
            string normalized = NormalizeKey(directionKey);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (!TryFindDirectionEntry(normalized, out DirectionAliasEntry directionEntry))
            {
                return normalized;
            }

            return NormalizeKey(directionEntry.canonicalKey);
        }

        public bool TryMapAnimationFolderDirection(string folderName, out string directionKey, out bool sharedFallback)
        {
            directionKey = string.Empty;
            sharedFallback = false;

            string normalized = NormalizeKey(folderName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (!TryFindDirectionEntry(normalized, out DirectionAliasEntry directionEntry))
            {
                return false;
            }

            directionKey = NormalizeKey(directionEntry.canonicalKey);
            sharedFallback = directionEntry.sharedFallback;
            return true;
        }

        public string ResolveMirroredDirectionKey(string directionKey)
        {
            string normalized = NormalizeDirectionKey(directionKey);
            return normalized switch
            {
                "left" => "right",
                "right" => "left",
                _ => normalized,
            };
        }

        private void ApplyBuiltInDefaults()
        {
            actionAliases = new List<ActionAliasEntry>
            {
                new ActionAliasEntry { canonicalKey = "jump", aliases = new List<string> { "jump_start", "jump_air" } },
                new ActionAliasEntry { canonicalKey = "aiming", aliases = new List<string> { "aim" } },
            };

            directionAliases = new List<DirectionAliasEntry>
            {
                new DirectionAliasEntry { canonicalKey = "left", aliases = new List<string> { "left", "west" } },
                new DirectionAliasEntry { canonicalKey = "right", aliases = new List<string> { "right", "east" } },
                new DirectionAliasEntry { canonicalKey = "shared", aliases = new List<string> { "shared", "default", "north", "south", "up", "down" }, sharedFallback = true },
            };
        }

        private bool TryFindActionEntry(string key, out ActionAliasEntry resolvedEntry)
        {
            EnsureInitialized();
            for (int index = 0; index < actionAliases.Count; index += 1)
            {
                ActionAliasEntry entry = actionAliases[index];
                if (entry == null)
                {
                    continue;
                }

                if (MatchesAlias(entry.canonicalKey, entry.aliases, key))
                {
                    resolvedEntry = entry;
                    return true;
                }
            }

            resolvedEntry = null;
            return false;
        }

        private bool TryFindDirectionEntry(string key, out DirectionAliasEntry resolvedEntry)
        {
            EnsureInitialized();
            for (int index = 0; index < directionAliases.Count; index += 1)
            {
                DirectionAliasEntry entry = directionAliases[index];
                if (entry == null)
                {
                    continue;
                }

                if (MatchesAlias(entry.canonicalKey, entry.aliases, key))
                {
                    resolvedEntry = entry;
                    return true;
                }
            }

            resolvedEntry = null;
            return false;
        }

        private void EnsureInitialized()
        {
            if ((actionAliases == null || actionAliases.Count == 0)
                && (directionAliases == null || directionAliases.Count == 0))
            {
                ApplyBuiltInDefaults();
            }
        }

        private static bool MatchesAlias(string canonicalKey, List<string> aliases, string key)
        {
            if (string.Equals(NormalizeKey(canonicalKey), key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (aliases == null)
            {
                return false;
            }

            for (int index = 0; index < aliases.Count; index += 1)
            {
                if (string.Equals(NormalizeKey(aliases[index]), key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}
