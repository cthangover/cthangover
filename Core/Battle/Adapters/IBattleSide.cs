namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Identifies which side of the battle a character or turn belongs to.
    /// Used by IBattleContext.EndTurn and EndBattle to route results
    /// (victory vs defeat screen).
    /// </summary>
    public enum BattleSide
    {
        Player,
        Enemy,
    }
}
