using System;
using System.Linq;
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
                var tree = (SceneTree)Engine.GetMainLoop();
                if (tree == null)
                    return;

                var viewport = tree.Root;
                if (viewport == null)
                    return;

                var image = viewport.GetTexture().GetImage();
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

        private static Image ScaleImage(Image source, int width, int height)
        {
            source.Resize(width, height, Image.Interpolation.Lanczos);
            return source;
        }
    }
}
