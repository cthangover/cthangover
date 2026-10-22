using Cthangover.Core.Actions;
using Cthangover.Core.UI;

namespace Cthangover.Mods.Trading
{
    /// <summary>
    /// <c>action trade.open shop=village_merchant</c> — opens the trade window with
    /// that merchant. Usually the last line of a merchant's dialog. The window is a
    /// hosted overlay of the UI registry (it lives above every scene and is built
    /// once), so the action hands it the shop and then shows it through the
    /// registry, which keeps the host's record of open entries right.
    /// </summary>
    public class TradeOpenAction : IScenarioAction
    {
        /// <summary>DSL name.</summary>
        public string Name => "trade.open";

        /// <summary>Resolves the shop and shows the window for it.</summary>
        public void Run(IActionContext ctx)
        {
            var id = ctx.GetParam("shop");
            var shop = string.IsNullOrWhiteSpace(id) ? null : ShopFactory.Instance.Get(id.Trim());
            if (shop == null)
            {
                ctx.LogWarning("TRADE", $"trade.open: unknown shop '{id}'");
                return;
            }

            var entry = UiRegistry.Get(TradePanel.EntryId);
            if (entry == null || UiHost.Instance?.EnsureInstance(entry) is not TradePanel panel)
            {
                ctx.LogWarning("TRADE", "trade.open: the trade window is not available");
                return;
            }

            panel.SetShop(shop);
            if (!panel.Visible)
                UiRegistry.Toggle(TradePanel.EntryId);
        }
    }
}
