#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System.Threading.Tasks;
    using System.IO;
    using Cthangover.Core.Mods.Testing;
    using Godot;

    /// <summary>Deliberately controlled runtime fixtures for recovery and timeout containment; selected only by the contract scene.</summary>
    public static class ModRuntimeCases
    {
        public static bool LaterTestRan { get; set; }
        public static bool LateTaskFinished { get; set; }

        [ModTest("cleanup/create")]
        public static void A_CreateRootNode(ModTestContext context)
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Root.AddChild(new Node { Name = "ContractStrayNode" });
            File.WriteAllText(Path.Combine(context.SandboxDir, "leftover.txt"), "first case artifact");
            ModAssert.NotNull(tree.Root.GetNodeOrNull("ContractStrayNode"));
        }

        [ModTest("cleanup/next_case")]
        public static void B_NextCaseSeesARecoveredTree(ModTestContext context)
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            ModAssert.Null(tree.Root.GetNodeOrNull("ContractStrayNode"), "deferred deletion completed before next case");
            ModAssert.False(File.Exists(Path.Combine(context.SandboxDir, "leftover.txt")), "file sandbox belongs to this case");
        }

        [ModTest("timeout/ignores_cancellation", TimeoutSeconds = 1)]
        public static async Task C_Timeout(ModTestContext context)
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree.CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
            LateTaskFinished = true;
        }

        [ModTest("timeout/must_not_run")]
        public static void D_AfterTimeout(ModTestContext context) => LaterTestRan = true;
    }
}
#endif
