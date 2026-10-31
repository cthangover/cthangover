namespace Cthangover.Core.Mods.Patches
{
    using System;

    /// <summary>
    /// A patch that cannot be applied as written: an operation on a value of the wrong
    /// kind (<c>Name$add</c> on a string), a malformed explicit form, an unknown
    /// <c>$</c>-operation. <see cref="Path"/> names the place inside the entity
    /// (<c>Encounters.Groups$add</c>, <c>Tiles[Index=7].Light</c>) so the resolver can
    /// report <c>mod/file#id: path: message</c> and skip just this patch.
    /// </summary>
    public sealed class JsonPatchException : Exception
    {
        /// <summary>Creates the error for one place of the patched entity.</summary>
        public JsonPatchException(string path, string message)
            : base(message)
        {
            Path = path;
        }

        /// <summary>Dotted path of the failing patch key inside the entity.</summary>
        public string Path { get; }
    }
}
