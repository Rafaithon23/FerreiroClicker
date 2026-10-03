// Shared by gameplay and the completion checks. Enemy tiers are zero-based.
public static class CampaignRules
{
    public const int LegendaryHammerTier = 3;
    public const int DarkKnightEnemyTier = 4;
    public static bool IsVictory(int hammerTier, int enemyTier, bool cleared)
        => cleared && hammerTier >= LegendaryHammerTier && enemyTier == DarkKnightEnemyTier;
}
