#if TOOLS
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Headless driver for the battle integration test. Both shipped battle
    /// cores wait for player input, so the driver periodically presses the
    /// "end turn" button of the active core, letting the enemy AI act until the
    /// battle scene reports <c>IsDestroyed</c> (victory or defeat). The core is
    /// selected by <c>--core=</c> (card_battle by default) and only affects how
    /// the end-turn button is located: card_battle nests it under a named node,
    /// ff_battle adds a plain Button directly to ToolPanel. A missing battle
    /// scene or an endless battle is reported as a failure and quits with exit
    /// code 1, so the CI run fails instead of hanging.
    /// <para/>
    /// The battle scene, core registry and autoplay contract live in the
    /// turn-battle mod, so all typed access goes through <see cref="TurnBattleBridge"/>.
    /// </summary>
    public partial class BattleTestDriver : Node
    {
        private const string CardBattleCore = "card_battle";
        private const string EndTurnButtonPath = "ToolPanel/EndTurnButton/BtnEndTurn";
        private const double PollIntervalSeconds = 0.25d;
        private const double SetupTimeoutSeconds = 15d;
        private const double BattleTimeoutSeconds = 90d;
        private const int MaxEndTurnPresses = 30;

        private string _core = BattleTestSetup.DefaultBattleCore;
        private double _elapsed;
        private double _nextPoll;
        private int _turnPresses;
        private bool _finished;
        private bool _speedChecked;

        public override void _Ready()
        {
            foreach (var arg in OS.GetCmdlineArgs())
            {
                if (arg.StartsWith("--core="))
                    _core = arg.Substring("--core=".Length).Trim();
            }
        }

        /// <summary>
        /// Preferred universal path: cores implementing the mod's IBattleAutoPlayable
        /// report when they wait for the player and accept a turn advance directly,
        /// so the driver does not depend on core-specific UI layout. The UI lookup
        /// in <see cref="FindEndTurnButton"/> remains as a fallback for cores
        /// without the contract.
        /// </summary>
        private bool TryAdvanceAutoPlayable(Node battle)
        {
            var core = TurnBattleBridge.GetActiveCore(battle);
            var autoType = TurnBattleBridge.AutoPlayableType();
            if (core == null || autoType == null || !autoType.IsInstanceOfType(core))
                return false;

            var waiting = (bool?)autoType.GetProperty("IsWaitingForPlayerInput")?.GetValue(core) ?? false;
            if (!waiting)
                return false;

            _turnPresses++;
            GameLogger.Log("BATTLE_TEST", $"BattleTestDriver: auto turn #{_turnPresses}");
            autoType.GetMethod("AdvancePlayerTurn")?.Invoke(core, null);
            return true;
        }

        /// <summary>
        /// Locates the active core's end-turn button. card_battle builds a
        /// named widget at a fixed path; ff_battle creates an unnamed Button
        /// and adds it as a direct child of ToolPanel.
        /// </summary>
        private Button FindEndTurnButton(Node battle)
        {
            if (_core != CardBattleCore)
            {
                var toolPanel = battle.GetNodeOrNull<Control>("ToolPanel");
                if (toolPanel == null)
                    return null;
                foreach (var child in toolPanel.GetChildren())
                {
                    if (child is Button button)
                        return button;
                }
                return null;
            }

            return battle.GetNodeOrNull<Button>(EndTurnButtonPath);
        }

        public override void _Process(double delta)
        {
            if (_finished)
                return;

            _elapsed += delta;
            if (_elapsed < _nextPoll)
                return;
            _nextPoll = _elapsed + PollIntervalSeconds;

            var battle = TurnBattleBridge.GetSceneContext();
            if (battle == null)
            {
                if (_elapsed >= SetupTimeoutSeconds)
                    Finish(false, "battle scene never became ready");
                return;
            }

            if (!_speedChecked)
            {
                _speedChecked = true;
                if (Engine.TimeScale < BattleTestSetup.MaxBattleSpeed)
                {
                    GameLogger.Log("BATTLE_TEST",
                        $"BattleTestDriver: core={_core}, battle speed was x{Engine.TimeScale}, forcing x{BattleTestSetup.MaxBattleSpeed}",
                        LogLevel.Warning);
                    Engine.TimeScale = BattleTestSetup.MaxBattleSpeed;
                }
                else
                {
                    GameLogger.Log("BATTLE_TEST", $"BattleTestDriver: core={_core}, battle speed x{Engine.TimeScale}");
                }
            }

            if (TurnBattleBridge.IsDestroyed(battle))
            {
                _finished = true;
                FinishAfterBattle($"battle finished in {_elapsed:F1}s, end-turn presses={_turnPresses}");
                return;
            }

            if (_turnPresses >= MaxEndTurnPresses || _elapsed >= BattleTimeoutSeconds)
            {
                Finish(false, $"battle did not finish (presses={_turnPresses}, elapsed={_elapsed:F1}s)");
                return;
            }

            if (TryAdvanceAutoPlayable(battle))
                return;

            var endTurn = FindEndTurnButton(battle);
            if (endTurn == null || !endTurn.Visible)
                return;

            _turnPresses++;
            GameLogger.Log("BATTLE_TEST", $"BattleTestDriver: end turn #{_turnPresses}");
            endTurn.EmitSignal("pressed");
        }

        /// <summary>
        /// Lets the battle scene's nodes be freed, then changes the party's
        /// health the way an item does between battles: party attributes
        /// outlive the battle, so a battle UI still subscribed to them would
        /// throw from its freed widgets into whatever changed the value.
        /// </summary>
        private async void FinishAfterBattle(string message)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            try
            {
                foreach (var member in Settings.GameData.Instance.Runtime.CharacterData.Characters.Values)
                {
                    var health = member.Attributes?.Health;
                    if (health != null)
                        health.Value = health.Value;
                }
            }
            catch (System.Exception ex)
            {
                Finish(false, $"party health change after the battle threw {ex.GetType().Name}: {ex.Message}");
                return;
            }
            Finish(true, message);
        }

        private void Finish(bool passed, string message)
        {
            _finished = true;
            GameLogger.Log("BATTLE_TEST", $"BattleTestDriver: {(passed ? "PASS" : "FAIL")} - {message}", passed ? LogLevel.Message : LogLevel.Error);
            GameLogger.Log("BATTLE_TEST", $"[TEST_RESULT] name=battle core={_core} passed={(passed ? 1 : 0)} failed={(passed ? 0 : 1)}");
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
#endif
