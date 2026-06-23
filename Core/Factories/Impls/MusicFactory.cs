using System;

namespace Cthangover.Core.Factories.Impls
{
    public class MusicFactory : AudioFactory
    {
        private static readonly Lazy<MusicFactory> instance = new(() => new MusicFactory());

        private MusicFactory() : base("music", 64) { }

        public static MusicFactory Instance => instance.Value;

        public override string GroupName => "music";
        
    }
}
