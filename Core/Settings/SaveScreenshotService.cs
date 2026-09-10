using System;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.View;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Static utility for capturing and loading save-slot thumbnail images.
    /// On save (<see cref="CaptureAndSave"/>), it grabs the current
    /// background texture from the <see cref="Cthangover.Core.UI.View.ViewBox"/>,
    /// scales it down to a 320×180 thumbnail, and writes a PNG next to the
    /// JSON save file. On load (<see cref="LoadScreenshot"/>), it reads the
    /// PNG back and returns a <see cref="Texture2D"/> suitable for the slot
    /// UI. A fallback solid-colour image is used when no background texture
    /// is available.
    /// </summary>
    public static class SaveScreenshotService
    {
        private const string SaveDir = "user://saves";

        /// <summary>
        /// Captures the current background image, downscales it to 320×180,
        /// and saves it as a PNG thumbnail alongside the save file.
        /// If the background texture is unavailable, a solid fallback image
        /// is used. Silently returns on error (logged internally).
        /// </summary>
        public static void CaptureAndSave(string fileName)
        {
            var absDir = ProjectSettings.GlobalizePath(SaveDir);
            if (!DirAccess.DirExistsAbsolute(absDir))
                DirAccess.MakeDirRecursiveAbsolute(absDir);

            var safe = new string(fileName.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            var path = $"{SaveDir}/{safe}.png";

            try
            {
                var image = CaptureBackgroundImage();
                if (image == null)
                    return;

                var smallImage = ScaleImage(image, 320, 180);
                smallImage.SavePng(ProjectSettings.GlobalizePath(path));
            }
            catch (Exception e)
            {
                GameLogger.Log("SAVE", $"Error capturing screenshot: {e.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads the thumbnail PNG for a save slot and returns it as a
        /// <see cref="Texture2D"/>. Returns <c>null</c> if the file does
        /// not exist or fails to decode.
        /// </summary>
        public static Texture2D LoadScreenshot(string fileName)
        {
            var safe = new string(fileName.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            var path = $"{SaveDir}/{safe}.png";
            var absPath = ProjectSettings.GlobalizePath(path);

            if (!System.IO.File.Exists(absPath))
                return null;

            try
            {
                var image = new Image();
                var err = image.Load(path);
                if (err != Error.Ok)
                    return null;

                return ImageTexture.CreateFromImage(image);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves the current background texture from the
        /// <see cref="Cthangover.Core.UI.View.ViewBox"/> node.
        /// Falls back to a 64×64 solid-dark image if no valid texture exists.
        /// </summary>
        private static Image CaptureBackgroundImage()
        {
            var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
            if (viewBox?.Background?.Texture == null)
                return CreateFallbackImage();

            var image = viewBox.Background.Texture.GetImage();
            if (image == null || image.IsEmpty())
                return CreateFallbackImage();

            return image;
        }

        /// <summary>
        /// Produces a 64×64 solid dark-grey fallback image used when
        /// the actual background texture is unavailable.
        /// </summary>
        private static Image CreateFallbackImage()
        {
            var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            image.Fill(new Color(0.12f, 0.12f, 0.12f, 1));
            return image;
        }

        /// <summary>
        /// Resizes <paramref name="source"/> to the given dimensions using
        /// Lanczos interpolation for high-quality downscaling of thumbnails.
        /// </summary>
        private static Image ScaleImage(Image source, int width, int height)
        {
            source.Resize(width, height, Image.Interpolation.Lanczos);
            return source;
        }
    }
}
