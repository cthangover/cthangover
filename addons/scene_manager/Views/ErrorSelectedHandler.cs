#if TOOLS
namespace SceneManagerAddon
{
    /// <summary>
    /// Raised by <see cref="ValidationPanel"/> when the user clicks
    /// a validation error row. The <c>filePath</c> argument identifies
    /// the scene JSON or scenario file that produced the error so the
    /// handler can navigate to it and display its content.
    /// </summary>
    public delegate void ErrorSelectedHandler(string filePath);
}
#endif
