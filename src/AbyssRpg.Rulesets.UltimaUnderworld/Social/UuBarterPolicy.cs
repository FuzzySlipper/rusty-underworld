namespace AbyssRpg.Rulesets.UltimaUnderworld.Social;

/// <summary>
/// Barter resolution: relative value (NPC ask minus player offer) reduced
/// by Charm, jittered by trader appraisal ((30-skill)/3), banded by fifths
/// — good deals close, even/poor deals coin-flip, bad deals refuse — with
/// a patience counter that tires the trader. Accepted deals swap the tray
/// contents (caller moves real inventory). Appraise readings report the
/// same band. Donor: Critter.TryTrade, Inventory do_judgement.
/// </summary>
public static class UuBarterPolicy
{
    public enum TradeResult
    {
        NoDeal,
        Accepted,
        Refused,
        BadDeal,
        TraderTired,
    }

    public static int AppraisalVariance(int appraisalSkill) => (30 - appraisalSkill) / 3;

    public static int JudgeValue(int npcValue, int playerValue, int charmSkill, int appraisalSkill, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int variance = AppraisalVariance(appraisalSkill);
        return npcValue - playerValue - charmSkill + rng.Next(-variance, variance + 1);
    }

    public static int DealBand(int judgedValue) => judgedValue / 5;

    /// <summary>
    /// Caller contract: map an empty tray to NoDeal without calling, and
    /// reset patience at each conversation start. Divergence note: the donor
    /// increments patience even on accepted good deals (an apparent flag
    /// oversight letting Yes flip to Tired with no swap); this gate counts
    /// non-deals only, deliberately.
    /// </summary>
    public static (TradeResult Result, int Patience) Offer(
        int npcValue, int playerValue, int charmSkill, int appraisalSkill,
        int patience, int maxPatience, Random rng)
    {
        int deal = DealBand(JudgeValue(npcValue, playerValue, charmSkill, appraisalSkill, rng));
        if (deal < 0) return (TradeResult.Accepted, patience);
        bool took = false;
        TradeResult result;
        if (deal <= 1)
        {
            took = rng.NextDouble() < 0.5;
            result = took ? TradeResult.Accepted : TradeResult.Refused;
        }
        else
        {
            result = TradeResult.BadDeal;
        }

        if (!took)
        {
            patience++;
            if (patience >= maxPatience) result = TradeResult.TraderTired;
        }

        return (result, patience);
    }

    /// <summary>
    /// Appraisal readings reuse JudgeValue with charm 0 and the player
    /// Appraise skill over likes-unadjusted values (offer/demand values
    /// carry likes adjustments and the trader's own appraisal).
    ///
    /// Demands: player presence (Charm/6 + level + health fraction) against
    /// trader resolve (ask/10 + attitude/2 + level + health fraction).
    /// Yielding costs one attitude step and the tray; refusal means hostility.
    /// Donor: Conversations do_demand.
    /// </summary>
    public enum DemandResult
    {
        Yielded,
        RefusedHostile,
    }

    public static (DemandResult Result, int AttitudeShift) Demand(
        int charmSkill, int playerLevel, int playerHp, int playerVitality,
        int npcAskValue, int attitude, int npcLevel, int npcHp, int npcMaxHp)
    {
        int playerScore = charmSkill / 6 + playerLevel + 1 + (2 * playerHp - 1) / Math.Max(1, playerVitality);
        int npcScore = npcAskValue / 10 + attitude / 2 + npcLevel + 1 + (2 * npcHp - 1) / Math.Max(1, npcMaxHp);
        if (playerScore <= npcScore) return (DemandResult.RefusedHostile, 0);
        // Yielding costs a step only above Upset (donor attitude>1 guard).
        return attitude > 1 ? (DemandResult.Yielded, -1) : (DemandResult.Yielded, 0);
    }
}
