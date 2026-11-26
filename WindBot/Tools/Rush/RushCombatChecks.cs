using System;
using System.Linq;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

internal static partial class RushChecks
{
    private static void CombatChecks()
    {
        DragonAndBusterChecks();
        DinocarriageChecks();
        BattleFlagChecks();
        BicornChecks();
    }

    private static void StartCombatLink(GameAI ai, ClientCard source)
    {
        ai.Duel.CurrentChain.Clear();
        ai.Duel.CurrentChainInfo.Clear();
        ai.Duel.NegatedChainIndexList.Clear();
        ai.OnChaining(source, source.Controller);
        ai.Duel.CurrentChain.Add(source);
        ai.Duel.CurrentChainInfo.Add(new ChainInfo(source));
        ai.Duel.SolvingChainIndex = 1;
    }

    // Some decisions have an intentional lower-priority fallback for the same card.
    // Isolate the first registered predicate when the test concerns that priority.
    private static bool CombatPriority(GameAI ai, ExecutorType type, ClientCard card)
    {
        ai.Executor.SetCard(type, card, -1);
        var rule = ai.Executor.Executors.First(e => e.Type == type && e.CardId == card.Id);
        return rule.Func == null || rule.Func();
    }

    private static void DragonAndBusterChecks()
    {
        foreach (string name in new[] { "RushDragonDragears", "BusterBlader" })
        {
            var ai = CreateBot(name);
            var dwarf = Card(120228031, CardLocation.MonsterZone);
            int recycle = dwarf.Id * 16 + 1;
            Check(name + " accepts Philan Dwarf's mill", ai.OnSelectEffectYn(dwarf, -1));
            Check(name + " an interrupted mill does not decline an unrelated option", ai.OnSelectYesNo(1));
            StartCombatLink(ai, dwarf);
            Check(name + " declines only the optional grave recycle",
                !ai.OnSelectYesNo(recycle) && ai.OnSelectYesNo(1));

            var weak = Card(120181001, CardLocation.MonsterZone, 1);
            var strong = Card(120120000, CardLocation.MonsterZone, 1);
            foreach (int id in new[] { 120130016, 120145014 })
            {
                StartCombatLink(ai, Card(id, CardLocation.MonsterZone));
                Check(name + " selects the stronger offered monster for " + id,
                    ai.OnSelectCard(new[] { weak, strong }, 1, 1, id * 16 + 1, false).Single() == strong);
            }
            var doom = Card(120151023, CardLocation.Hand);
            var hidden = Card(120120000, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
            ai.Duel.Fields[1].MonsterZone[0] = hidden;
            Check(name + " hidden stats do not qualify a Tribute to the Doomed priority",
                !CombatPriority(ai, ExecutorType.Activate, doom) && !ai.HaveSelectedCards());
            ai.Duel.Fields[1].MonsterZone[1] = strong;
            Check(name + " visible 3000 ATK qualifies without a target queue",
                CombatPriority(ai, ExecutorType.Activate, doom) && !ai.HaveSelectedCards());
            StartCombatLink(ai, doom);
            var own = Card(120120000, CardLocation.MonsterZone);
            Check(name + " Doomed resolves against a remaining opponent monster",
                ai.OnSelectCard(new[] { own, weak }, 1, 1, HintMsg.Destroy, false).Single() == weak);
        }
    }

    private static void DinocarriageChecks()
    {
        var ai = CreateBot("DinocarriageDynarmix");
        var left = Card(120151005, CardLocation.Hand);
        var anotherLeft = Card(120151005, CardLocation.Hand);
        var right = Card(120151007, CardLocation.Hand);
        int maximum = 120000000 * 16 + 1;
        Check("Maximum Summon chooses distinct left and right copies",
            ai.OnSelectCard(new[] { left, anotherLeft, right }, 2, 2, maximum, false).SequenceEqual(new[] { left, right }));
        Check("Maximum Summon accepts the remaining half of a subgroup",
            ai.OnSelectCard(new[] { right }, 1, 1, maximum, false).Single() == right);
        Check("Maximum Summon finishes its completed subgroup",
            ai.OnSelectCard(new[] { anotherLeft }, 0, 1, maximum, true).Count == 0);

        var crush = Card(120151017, CardLocation.Hand);
        ai.Duel.Fields[0].Hand.Add(crush);
        ai.Duel.Fields[0].Hand.Add(left);
        ai.Duel.Fields[0].Hand.Add(anotherLeft);
        var monster = Card(0, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
        var backrow = Card(0, CardLocation.SpellZone, 1, CardPosition.FaceDownDefence);
        ai.Duel.Fields[1].MonsterZone[0] = monster;
        Check("Dino Crush activates against a single occupied monster zone", ai.OnSelectEffectYn(crush, -1));
        Check("Dino Crush spends exactly one duplicate",
            ai.OnSelectCard(new[] { left, right }, 1, 1, HintMsg.ToGrave, false).Single() == left && !ai.HaveSelectedCards());
        StartCombatLink(ai, crush);
        Check("Dino Crush accepts its only facedown monster",
            ai.OnSelectCard(new[] { monster }, 1, 1, HintMsg.Destroy, false).Single() == monster);
        Check("Dino Crush accepts a backrow-only offer",
            ai.OnSelectCard(new[] { backrow }, 1, 1, HintMsg.Destroy, false).Single() == backrow);
        Check("Dino Crush prefers a set backrow with no center on field",
            ai.OnSelectCard(new[] { monster, backrow }, 1, 1, HintMsg.Destroy, false).Single() == backrow);
        var dyna = Card(120151006, CardLocation.MonsterZone);
        ai.Duel.Fields[0].MonsterZone[0] = dyna;
        Check("Dino Crush protects Dynarmix by choosing the set monster",
            ai.OnSelectCard(new[] { backrow, monster }, 1, 1, HintMsg.Destroy, false).Single() == monster);
        var visible = Card(120181001, CardLocation.MonsterZone, 1);
        var otherSet = Card(0, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
        StartCombatLink(ai, dyna);
        Check("Dynarmix can destroy two different set monsters",
            ai.OnSelectCard(new[] { visible, monster, otherSet }, 1, 2, HintMsg.Destroy, false)
                .SequenceEqual(new[] { monster, otherSet }));
    }

    private static void BattleFlagChecks()
    {
        var ai = CreateBot("SaikyoBattleFlag");
        var own = ai.Duel.Fields[0];
        var boss = Card(120181001, CardLocation.Hand);
        var officer = Card(120183024, CardLocation.Hand);
        var eagle = Card(120193003, CardLocation.MonsterZone);
        var drone = Card(120183030, CardLocation.MonsterZone);
        foreach (var card in new[] { boss, officer, Card(120155015, CardLocation.Hand) })
            own.Hand.Add(card);
        own.MonsterZone[0] = drone;
        Check("Crafter Drone accepts a hand clogged with three bosses", CombatPriority(ai, ExecutorType.Activate, drone));
        Check("Crafter Drone spends one Machine from the offered hand",
            ai.OnSelectCard(new[] { boss, officer }, 1, 1, HintMsg.ToGrave, false).Single() == boss && !ai.HaveSelectedCards());

        own.Hand.Clear();
        own.Hand.Add(boss);
        own.MonsterZone[0] = eagle;
        own.MonsterZone[1] = Card(120130016, CardLocation.MonsterZone);
        Check("Aim Eagle preserves the boss when a second low LIGHT cost is missing",
            !CombatPriority(ai, ExecutorType.Activate, eagle) && !ai.HaveSelectedCards());
        var bicorn = Card(120181011, CardLocation.MonsterZone);
        own.MonsterZone[1] = bicorn;
        var fieldBoss = Card(120181001, CardLocation.MonsterZone);
        own.MonsterZone[2] = fieldBoss;
        Check("Aim Eagle accepts two low LIGHT monsters", CombatPriority(ai, ExecutorType.Activate, eagle));
        var materials = ai.OnSelectCard(new[] { fieldBoss, eagle, bicorn }, 2, 2, HintMsg.ToGrave, false);
        Check("Aim Eagle preserves the field boss when paying a grouped cost",
            materials.Count == 2 && materials.Contains(eagle) && materials.Contains(bicorn));
        StartCombatLink(ai, eagle);
        Check("Aim Eagle summons the preferred offered Machine",
            ai.OnSelectCard(new[] { officer, boss }, 1, 1, HintMsg.SpSummon, false).Single() == boss);
        var revival = Card(120194004, CardLocation.Hand);
        var enemyBoss = Card(120181001, CardLocation.Grave, 1);
        ai.Duel.Fields[1].Graveyard.Add(enemyBoss);
        Check("Monster Reborn accepts a strong opposing effect monster",
            CombatPriority(ai, ExecutorType.Activate, revival) && !ai.HaveSelectedCards());
        var salary = Card(120183062, CardLocation.Grave);
        var demotion = Card(120183063, CardLocation.Grave);
        StartCombatLink(ai, officer);
        var traps = ai.OnSelectCard(new[] { salary, Card(120183062, CardLocation.Grave), demotion }, 1, 2, HintMsg.Set, false);
        Check("Sinister CEO sets two differently named traps", traps.Count == 2 && traps.Contains(salary) && traps.Contains(demotion));

        var demotionTrap = Card(120183063, CardLocation.SpellZone);
        bool skippedMissingAttacker;
        try
        {
            skippedMissingAttacker = !CombatPriority(ai, ExecutorType.Activate, demotionTrap) && !ai.HaveSelectedCards();
        }
        catch (NullReferenceException)
        {
            // The pre-fix executor dereferences the missing battler here.
            skippedMissingAttacker = false;
        }
        Check("Battle Demotion skips its attacker priority after battle state clears", skippedMissingAttacker);
        StartCombatLink(ai, demotionTrap);
        var weak = Card(120193003, CardLocation.MonsterZone, 1);
        var strong = Card(120120000, CardLocation.MonsterZone, 1);
        Check("Battle Demotion picks the strongest remaining offered attacker",
            ai.OnSelectCard(new[] { weak, strong }, 1, 1, HintMsg.PosChange, false).Single() == strong);
    }

    private static void BicornChecks()
    {
        var ai = CreateBot("SaikyoBattleFlag");
        var bicorn = Card(120181011, CardLocation.MonsterZone);
        var boss = Card(120181001, CardLocation.Hand);
        ai.Duel.Fields[0].MonsterZone[0] = bicorn;
        ai.Duel.Fields[0].Hand.Add(boss);
        Check("Surge Bicorn can activate with its boss in hand", ai.OnSelectEffectYn(bicorn, -1));
        Check("Accepting Surge Bicorn does not grant the double tribute", !CombatPriority(ai, ExecutorType.Summon, boss));
        StartCombatLink(ai, bicorn);
        ai.Duel.NegatedChainIndexList.Add(1);
        ai.OnChainSolved(1);
        Check("A negated Surge Bicorn never counts twice", !CombatPriority(ai, ExecutorType.Summon, boss));
        StartCombatLink(ai, bicorn);
        ai.OnChainSolved(1);
        Check("A resolved Surge Bicorn retains its grant until actually spent",
            CombatPriority(ai, ExecutorType.Summon, boss) && CombatPriority(ai, ExecutorType.Summon, boss));
        var ordinary = Card(120183030, CardLocation.MonsterZone);
        ordinary.OpParam1 = 1;
        bicorn.OpParam1 = 1;
        bicorn.OpParam2 = 2;
        Check("The tribute prompt spends the offered two-weight Bicorn alone",
            ai.OnSelectTribute(new[] { ordinary, bicorn }, 2, 2, HintMsg.Release, false).Single() == bicorn);
        ai.OnPosChange(bicorn, (int)CardPosition.FaceUpAttack, (int)CardPosition.FaceDownDefence);
        Check("Setting Surge Bicorn clears its grant", !CombatPriority(ai, ExecutorType.Summon, boss));
        StartCombatLink(ai, bicorn);
        ai.OnMove(bicorn, 0, (int)CardLocation.MonsterZone, 0, (int)CardLocation.Grave);
        ai.OnChainSolved(1);
        Check("A Bicorn that leaves before resolving cannot regain its grant on return",
            !CombatPriority(ai, ExecutorType.Summon, boss));
        StartCombatLink(ai, bicorn);
        ai.OnChainSolved(1);
        ai.OnNewTurn();
        Check("Surge Bicorn's grant expires at the turn boundary", !CombatPriority(ai, ExecutorType.Summon, boss));
    }
}
