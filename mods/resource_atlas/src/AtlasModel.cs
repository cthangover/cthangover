using System.Collections.Generic;

namespace Cthangover.ResourceAtlas
{
    /// <summary>One resource a mod makes available under a resolver key.</summary>
    public sealed class AtlasProvision
    {
        /// <summary>Resource kind: validator kinds (background, sound, character, scene...) plus texture, shader, action, entity_kind, locale, scene_file.</summary>
        public string Kind { get; init; }

        /// <summary>Resolver key inside the kind (texture basename, sound path, entity id, "lang:key"...).</summary>
        public string Id { get; init; }

        public string ModId { get; init; }

        public string File { get; init; }
    }

    /// <summary>One place where a mod asks for a resource by key.</summary>
    public sealed class AtlasRequest
    {
        public string Kind { get; init; }

        public string Id { get; init; }

        public string ModId { get; init; }

        public string File { get; init; }

        public int Line { get; init; }

        /// <summary>Channel the request was found in: data, code, scenario, res, entity_data.</summary>
        public string Via { get; init; }

        /// <summary>Provider named by the reference itself (<c>res://mods/&lt;id&gt;/...</c>); null when resolved by key.</summary>
        public string ExplicitProvider { get; init; }
    }

    /// <summary>An intentional edit of another mod's resource through <c>patches/</c>.</summary>
    public sealed class AtlasPatch
    {
        public string Kind { get; init; }

        public string Id { get; init; }

        public string ModId { get; init; }

        public string File { get; init; }

        /// <summary>Explicit target owner from the patch header, or null to resolve by key.</summary>
        public string ExplicitOwner { get; init; }
    }

    public enum AtlasSeverity
    {
        Info,
        Warning,
        Error,
    }

    public enum AtlasFindingKind
    {
        /// <summary>A mod uses another mod's resource and declares that mod in its depends closure.</summary>
        DeclaredUse,

        /// <summary>A mod uses another mod's resource without a depends path to it — breaks once that mod is absent.</summary>
        UndeclaredUse,

        /// <summary>No loaded mod provides the requested key.</summary>
        Unresolved,

        /// <summary>The winner of a shared key depends on the losers — the override is intended and ordered.</summary>
        DeclaredOverride,

        /// <summary>Several unrelated mods provide one key; the winner is decided by incidental load order.</summary>
        UnorderedOverride,

        /// <summary>A mod patches another mod's resource and depends on it.</summary>
        DeclaredPatch,

        /// <summary>A mod patches another mod's resource without depending on it.</summary>
        UndeclaredPatch,

        /// <summary>A patch targets a key no other loaded mod provides.</summary>
        PatchTargetMissing,

        /// <summary>
        /// Inside a game: the key is provided only by an installed mod that is
        /// not part of the game — the isolated game will not find it.
        /// </summary>
        OutsideGame,
    }

    public sealed class AtlasFinding
    {
        public AtlasFindingKind Kind { get; init; }

        public AtlasSeverity Severity { get; init; }

        /// <summary>Mod the finding is attributed to (requester, overriding winner, patcher).</summary>
        public string ModId { get; init; }

        /// <summary>The other side: providers, overridden mods or patch owners.</summary>
        public IReadOnlyList<string> OtherMods { get; init; } = new List<string>();

        public string ResourceKind { get; init; }

        public string ResourceId { get; init; }

        public string File { get; init; }

        public int Line { get; init; }

        public string Message { get; init; }
    }
}
