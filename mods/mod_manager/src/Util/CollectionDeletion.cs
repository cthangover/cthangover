using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Deleting a player's game collection, shared by the gallery card and the
    /// scheme editor header so both ask the same question. Only the player's
    /// own collections can go: a collection a mod or the catalog ships would
    /// come back on the next merge (its edited version is dropped with
    /// "Revert to built-in" instead). Deleting touches only
    /// <c>user://collections.json</c> — the mods of the collection stay
    /// installed and its saves stay on disk (they reappear if a collection
    /// with the same id comes back), which the confirmation spells out.
    /// </summary>
    public static class CollectionDeletion
    {
        /// <summary>True for a player-made collection (saved or still a draft) that no mod or catalog ships.</summary>
        public static bool CanDelete(ModCollection collection)
        {
            return collection != null
                   && collection.Origin == ModCollectionOrigin.User
                   && !ModCollectionStore.HasShippedVersion(collection.Id);
        }

        /// <summary>
        /// Asks for confirmation under <paramref name="host"/>; on "delete"
        /// removes the saved collection and calls <paramref name="deleted"/>
        /// (the caller also drops its unsaved draft and refreshes its view).
        /// </summary>
        public static void Confirm(Node host, ModCollection collection, Action deleted)
        {
            if (host == null || !CanDelete(collection))
                return;

            var name = CollectionText.Resolve(collection.GetLocalizedName(TranslationServer.GetLocale()));
            var dialog = new ConfirmationDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_delete"),
                DialogText = string.Format(TranslationServer.Translate("mod_manager/collection_delete_confirm"),
                    name, SaveService.ListSaves(collection.Id).Count),
                OkButtonText = TranslationServer.Translate("mod_manager/collection_delete"),
                CancelButtonText = TranslationServer.Translate("mod_manager/cancel"),
                Exclusive = true,
            };
            dialog.Confirmed += () =>
            {
                ModCollectionStore.RemoveUser(collection.Id);
                dialog.QueueFree();
                deleted?.Invoke();
            };
            dialog.Canceled += dialog.QueueFree;
            host.AddChild(dialog);
            dialog.PopupCentered();
        }
    }
}
