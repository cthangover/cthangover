using System.Collections.Generic;
using System.Reflection;

namespace Cthangover.Core.Mods
{
    public class CompileResult
    {
        public bool Success { get; set; }
        public Assembly Assembly { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
