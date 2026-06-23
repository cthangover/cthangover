using System;

namespace Cthangover.Core.Factories.Impls
{
    public class UITextureFactory : Texture2DFactory
    {
        private static readonly Lazy<UITextureFactory> instance = new(() => new UITextureFactory());
        private UITextureFactory() : base("ui", 64) { }

        public static UITextureFactory Instance => instance.Value;

        public override string GroupName => "ui";
    }
}
