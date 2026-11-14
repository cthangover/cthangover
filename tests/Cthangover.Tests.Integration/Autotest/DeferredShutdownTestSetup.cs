#if TOOLS
using System;
using System.Linq;
using System.Threading.Tasks;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest;

/// <summary>Shutdown owns detached deferred nodes, including when requested inside a deferred node's Ready callback.</summary>
public partial class DeferredShutdownTestSetup : Node
{
    private Node queued;
    private Node borrowed;
    private string testName;
    private int passed;
    private int failed;

    /// <summary>Exercises exit both before the deferred batch and from inside that batch, without timing delays.</summary>
    public override void _Ready()
    {
        var duringFlush = OS.GetCmdlineUserArgs().Contains("--shutdown-during-flush");
        testName = duringFlush ? "shutdown_deferred_flush" : "shutdown_deferred_ready";
        borrowed = new Node { Name = "ShutdownBorrowedProbe" };
        AddChild(borrowed);
        queued = new Node { Name = "ShutdownQueuedProbe" };
        ApplicationExit.Preparing += VerifyOwnership;
        if (duringFlush)
        {
            var trigger = new Node { Name = "ShutdownTrigger" };
            trigger.Ready += RequestQuit;
            ModRoot.AddDeferred(trigger);
        }
        ModRoot.AddDeferred(queued);
        if (!duringFlush) RequestQuit();
    }

    private void RequestQuit() => ApplicationExit.Quit(GetTree());

    private Task VerifyOwnership(SceneTree tree)
    {
        ApplicationExit.Preparing -= VerifyOwnership;
        Check("queued detached node freed before cleanup handlers", !IsInstanceValid(queued));
        var late = new Node { Name = "ShutdownLateProbe" };
        ModRoot.AddDeferred(late);
        Check("late detached node freed instead of starting", !IsInstanceValid(late));
        ModRoot.AddDeferred(borrowed);
        Check("parented node remains owned by its tree", IsInstanceValid(borrowed) && borrowed.GetParent() == this);
        GameLogger.Log("TEST", $"[TEST_RESULT] name={testName} passed={passed} failed={failed}");
        return failed == 0 ? Task.CompletedTask : Task.FromException(new InvalidOperationException("deferred shutdown ownership failed"));
    }

    private void Check(string label, bool condition)
    {
        if (condition) passed++;
        else failed++;
        GameLogger.Log("TEST", (condition ? "PASS " : "FAIL ") + label, condition ? LogLevel.Message : LogLevel.Error);
    }
}
#endif
