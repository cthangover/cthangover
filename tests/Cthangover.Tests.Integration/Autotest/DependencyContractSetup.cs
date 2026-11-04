#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System.Linq;
    using System.IO;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Paired process fixtures verify shared setup data and, in opt-in negative mode, blocked dependents.</summary>
    public partial class DependencyContractSetup : Node
    {
        public override void _Ready()
        {
            var arguments = OS.GetCmdlineUserArgs();
            var child = arguments.Contains("--dependency-probe=child");
            var failed = arguments.Contains("--dependency-probe=setup");
            var path = Path.Combine(OS.GetUserDataDir(), "dependency-probe.txt");
            if (failed) GameLogger.Log("TEST", "[FAIL] intentional dependency setup failure");
            else if (child)
            {
                failed = !File.Exists(path) || File.ReadAllText(path) != "prepared";
                if (failed) GameLogger.Log("TEST", "[FAIL] dependency setup data was not preserved");
            }
            else File.WriteAllText(path, "prepared");
            GameLogger.Log("TEST", $"[TEST_RESULT] name={(child ? "dependency_child" : "dependency_setup")} passed={(failed ? 0 : 1)} failed={(failed ? 1 : 0)}");
            ApplicationExit.Quit(GetTree(), failed ? 1 : 0);
        }
    }
}
#endif
