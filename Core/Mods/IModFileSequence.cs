namespace Cthangover.Core.Mods
{
    using System;
    using System.IO;
    using System.Threading;

    /// <summary>Visits package files in storage order with borrowed streams, allowing one solid-block decode pass.</summary>
    public interface IModFileSequence
    {
        /// <summary>The visitor must consume the stream synchronously and must not retain it after returning.</summary>
        void VisitFiles(Action<string, Stream> visitor, CancellationToken cancellationToken = default);
    }
}
