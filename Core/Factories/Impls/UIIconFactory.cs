using System;

namespace Cthangover.Core.Factories.Impls
{
    public class UIIconFactory : Texture2DFactory
    {
        private static readonly Lazy<UIIconFactory> instance = new(() => new UIIconFactory());
        private UIIconFactory() : base("ui/icons", 64) { }

        public static UIIconFactory Instance => instance.Value;

        public override string GroupName => "ui/icons";

    }
}
