using System;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.View;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    public static class SaveScreenshotService
    {
        private const string SaveDir = "user://saves";

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

        private static Image CreateFallbackImage()
        {
            var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            image.Fill(new Color(0.12f, 0.12f, 0.12f, 1));
            return image;
        }

        private static Image ScaleImage(Image source, int width, int height)
        {
            source.Resize(width, height, Image.Interpolation.Lanczos);
            return source;
        }
    }
}
