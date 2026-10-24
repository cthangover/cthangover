using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Cthangover.Mods.Dialog;
using Godot;

namespace Mods.InteractiveBase.Tests
{
    /// <summary>
    /// Entry point of the interactives itest: the suite is mounted (and this
    /// initializer compiled) only with <c>--itest=interactives</c>; run it on
    /// <c>--scene=interactives_test</c>, where the suite scenario adds the lamp.
    /// </summary>
    public class InteractivesTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains("--itest=interactives"))
                return;
            queued = true;
            GameLogger.Log("TEST", "InteractivesTest: activating");
            ModRoot.AddDeferred(new InteractivesTestDriverNode());
        }
    }

    /// <summary>
    /// Drives the lamp like a player: waits until the scenario command
    /// <c>interactive_add kitchen_lamp</c> placed it, hovers it, clicks it
    /// through the object's own input handler and expects the <c>onClick</c>
    /// scenario to run in the shared DialogBox (its <c>set lamp_clicked=yes</c>
    /// lands in the box runtime). Prints <c>[TEST_RESULT] name=interactives</c>.
    /// </summary>
    public partial class InteractivesTestDriverNode : Node
    {
        private const string ResultName = "interactives";
        private const string LampId = "kitchen_lamp";
        private const double TimeoutSeconds = 60;

        private int passed;
        private int failed;
        private double elapsed;
        private double stepStarted;
        private int step;
        private InteractiveObject lamp;

        public override void _Process(double delta)
        {
            if (step < 0)
                return;
            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                Check($"finished in {TimeoutSeconds:0}s (stuck at step {step})", false);
                Finish();
                return;
            }

            try
            {
                Step();
            }
            catch (Exception ex)
            {
                Check($"step {step}: no exception ({ex.GetType().Name}: {ex.Message})", false);
                Finish();
            }
        }

        private void Step()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady || (GetTree().CurrentScene == null))
                        return;
                    Check("definition kitchen_lamp is collected", Interactives.Collect().ContainsKey(LampId));
                    Next();
                    return;

                case 1:
                    lamp = InteractiveManager.Instance?.Get(LampId);
                    if (lamp == null)
                    {
                        if (Waited(15))
                        {
                            Check("interactive_add placed kitchen_lamp", false);
                            Finish();
                        }

                        return;
                    }

                    Check("interactive_add placed kitchen_lamp", true);
                    Check("lamp is enabled and inside the tree", lamp.IsEnabled && lamp.IsInsideTree());
                    lamp.EmitSignal(Control.SignalName.MouseEntered);
                    Next();
                    return;

                case 2:
                    // Hover DSL runs in a throw-away runtime: the observable part is that it does not break the object.
                    lamp.EmitSignal(Control.SignalName.MouseExited);
                    Check("hover enter/exit handled, lamp still enabled", lamp.IsEnabled && lamp.IsInsideTree());
                    lamp._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = lamp.Size / 2 });
                    Next();
                    return;

                case 3:
                    var box = SceneContextNode.FindNode<DialogBox>("DialogBox");
                    var clicked = box?.Runtime.GetVariable("lamp_clicked");
                    if (clicked != "yes" && !Waited(5))
                        return;
                    Check("click started the onClick dialog (waits on its text)", box is { IsDialogEnded: false });
                    Check("onClick scenario ran (lamp_clicked=yes)" + (clicked == "yes" ? string.Empty : $" (got '{clicked}')"), clicked == "yes");
                    Finish();
                    return;
            }
        }

        private bool Waited(double seconds) => elapsed - stepStarted > seconds;

        private void Next()
        {
            step++;
            stepStarted = elapsed;
        }

        private void Check(string what, bool ok)
        {
            if (ok)
                passed++;
            else
                failed++;
            GameLogger.Log("TEST", $"  [{(ok ? "PASS" : "FAIL")}] {what}");
        }

        private void Finish()
        {
            step = -1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name={ResultName} passed={passed} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }
    }
}
