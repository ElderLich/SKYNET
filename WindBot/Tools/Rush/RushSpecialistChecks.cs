using System.Collections.Generic;
using System.Linq;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

internal static partial class RushChecks
{
    private static void ResolveSpecialistEffect(GameAI ai, ClientCard source)
    {
        ai.Duel.CurrentChain.Clear();
        ai.Duel.CurrentChainInfo.Clear();
        ai.Duel.CurrentChain.Add(source);
        ai.Duel.CurrentChainInfo.Add(new ChainInfo(source));
        ai.Duel.SolvingChainIndex = 1;
    }

    private static void SpecialistChecks()
    {
        AmbushHandChecks();
        SharedResolutionChecks();
        HiddenBoardChecks();
        FactoryChecks();
        EradicatorChecks();
        SpellcasterRecoveryChecks();
        ExcutieChecks();
        ConstructorChecks();
    }

    private static void AmbushHandChecks()
    {
        var ai = CreateBot("Mituzi");
        var wyrm = Card(120155002, CardLocation.Hand);
        ai.Duel.Fields[0].SpellZone[0] = Card(120155053, CardLocation.SpellZone, 0, CardPosition.FaceDownDefence);
        ai.Executor.SetCard(ExecutorType.Summon, wyrm, -1);
        var summon = ai.Executor.Executors.First(e => e.Type == ExecutorType.Summon && e.CardId == -1).Func;
        ai.Duel.Fields[0].Hand.Add(wyrm);
        Check("Constructor Ambush reserves its only strongest Wyrm", !summon());
        ai.Duel.Fields[0].Hand.Add(Card(wyrm.Id, CardLocation.Hand));
        Check("Constructor Ambush can summon a spare copy", summon());
        ai.Duel.Fields[0].Hand.Clear();
        bool acceptsUnreserved = false;
        try
        {
            acceptsUnreserved = summon();
        }
        catch (System.NullReferenceException)
        {
            // Older Mituzi builds dereference an empty hand selection.
        }
        Check("Constructor Ambush handles a summon offer absent from hand", acceptsUnreserved);
    }

    private static void SharedResolutionChecks()
    {
        var ai = CreateBot("SaikyoBattleFlag");
        var mirror = Card(120155015, CardLocation.MonsterZone);
        ResolveSpecialistEffect(ai, mirror);
        var high = Card(120181001, CardLocation.Grave);
        var medium = Card(120208000, CardLocation.Grave);
        var low = Card(120183031, CardLocation.Grave);
        var offered = new[] { low, medium, high };
        var power = ai.OnSelectCard(offered, 1, 3, HintMsg.ToDeck, false);
        Check("Mirror Innovator returns up to three monsters for attack", power.Distinct().Count() == 3);
        var limited = ai.OnSelectCard(offered, 1, 2, HintMsg.ToDeck, false);
        Check("Mirror Innovator uses the highest levels within the offered limit", limited.Count == 2
            && limited.Sum(c => c.Level) == offered.OrderByDescending(c => c.Level).Take(2).Sum(c => c.Level));
        var enemy = ai.Duel.Fields[1];
        enemy.MonsterZone[0] = Card(120194001, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
        var piercing = ai.OnSelectCard(offered, 1, 3, HintMsg.ToDeck, false);
        enemy.MonsterZone[0] = Card(120194002, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
        var hidden = ai.OnSelectCard(offered, 1, 3, HintMsg.ToDeck, false);
        Check("Mirror Innovator uses one high-level monster for piercing regardless of hidden defense",
            piercing.Count == 1 && hidden.Count == 1 && hidden[0] == piercing[0] && piercing[0].Level == offered.Max(c => c.Level));
        var wall = Card(120194001, CardLocation.MonsterZone, 1, CardPosition.FaceUpDefence);
        enemy.MonsterZone[0] = wall;
        Check("Mirror Innovator returns multiple monsters to overcome a visible wall",
            ai.OnSelectCard(offered, 1, 3, HintMsg.ToDeck, false).Count == 3);

        var destruction = Card(120151023, CardLocation.Hand);
        ResolveSpecialistEffect(ai, destruction);
        var opponent = Card(120194002, CardLocation.MonsterZone, 1);
        var removed = ai.OnSelectCard(new[] { opponent, wall }, 1, 1, HintMsg.Destroy, false);
        Check("Tribute to the Doomed destroys a high-defense threat in defense position", removed.Single() == wall);

        var reborn = Card(120195004, CardLocation.SpellZone);
        var champion = Card(120120000, CardLocation.Grave, 1);
        ResolveSpecialistEffect(ai, reborn);
        Check("Alternate Monster Reborn uses the strongest offered graveyard monster",
            ai.OnSelectCard(new[] { low, champion }, 1, 1, HintMsg.SpSummon, false).Single() == champion);
        ResolveSpecialistEffect(ai, Card(120130016, CardLocation.MonsterZone));
        var defender = Card(120120000, CardLocation.MonsterZone, 1, CardPosition.FaceUpDefence);
        var attacker = Card(120130000, CardLocation.MonsterZone, 1);
        Check("Sevens Road Mage applies its attack reduction to an attacking threat",
            ai.OnSelectCard(new[] { defender, attacker }, 1, 1, 120130016 * 16 + 1, false).Single() == attacker);
    }

    private static void HiddenBoardChecks()
    {
        var ai = CreateBot("Excutie");
        var bot = ai.Duel.Fields[0];
        var enemy = ai.Duel.Fields[1];
        var high = Card(120238001, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
        var low = Card(120120017, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
        var unknown = Card(0, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence);
        var gate = Card(120231068, CardLocation.SpellZone);
        var hole = Card(120231069, CardLocation.Hand);
        bot.MonsterZone[0] = Card(120120000, CardLocation.MonsterZone);
        enemy.MonsterZone[0] = Card(120120000, CardLocation.MonsterZone, 1);
        bool consistent = true;
        foreach (var set in new[] { high, low, unknown })
        {
            enemy.MonsterZone[1] = set;
            consistent &= !ai.OnSelectEffectYn(hole, -1);
        }
        Check("Dark Hole ignores cached defense on opposing set cards", consistent);
        enemy.MonsterZone[0] = Card(120194002, CardLocation.MonsterZone, 1);
        consistent = true;
        foreach (var set in new[] { high, low, unknown })
        {
            enemy.MonsterZone[1] = set;
            consistent &= !ai.OnSelectEffectYn(gate, -1);
        }
        Check("Wyrm Hell ignores opposing set monster levels", consistent);
        enemy.MonsterZone[1] = Card(120120000, CardLocation.MonsterZone, 1);
        Check("Wyrm Hell recognizes a visible high-level monster", ai.OnSelectEffectYn(gate, -1));

        var flame = Card(120235019, CardLocation.MonsterZone);
        bot.MonsterZone[0] = flame;
        bot.Hand.Add(Card(120231035, CardLocation.Hand));
        enemy.MonsterZone[0] = null;
        consistent = true;
        foreach (var set in new[] { high, low, unknown })
        {
            enemy.MonsterZone[1] = set;
            consistent &= !ai.OnSelectEffectYn(flame, -1);
        }
        Check("Excutie Flame spends no discard solely for a cached face-down threat", consistent);

        consistent = true;
        foreach (var set in new[] { high, low, unknown })
        {
            ai = CreateBot("ObsidianMagical");
            ai.Game._chat = false;
            var own = Card(120194002, CardLocation.MonsterZone);
            ai.Duel.Fields[0].MonsterZone[0] = own;
            ai.Duel.Fields[1].MonsterZone[0] = set;
            var phase = new MainPhase { CanEndPhase = true };
            phase.ReposableCards.Add(own);
            consistent &= ai.OnSelectIdleCmd(phase).Action != MainPhaseAction.MainAction.Repos;
        }
        Check("Rush repositioning keeps its stance against the same unseen defender", consistent);

        consistent = true;
        foreach (var set in new[] { high, low, unknown })
        {
            ai = CreateBot("Mituzi");
            var builder = Card(120155024, CardLocation.MonsterZone);
            var normal = Card(120151029, CardLocation.Grave);
            ai.Duel.Fields[0].SpellZone[5] = Card(120155042, CardLocation.SpellZone);
            ai.Duel.Fields[0].MonsterZone[0] = builder;
            ai.Duel.Fields[0].Graveyard.Add(normal);
            ai.Duel.Fields[1].MonsterZone[0] = set;
            var allowed = new[] { CardPosition.FaceUpAttack, CardPosition.FaceUpDefence };
            consistent &= ai.OnSelectEffectYn(builder, -1)
                && ai.OnSelectPosition(normal.Id, allowed) == CardPosition.FaceUpAttack;
        }
        Check("Buildragon's revival position ignores a set defender's concealed power", consistent);

        var positions = new List<int>();
        foreach (var pair in new[] { new[] { high, low }, new[] { low, high } })
        {
            ai = CreateBot("TheLegend");
            ai.Game._chat = false;
            var attacker = Card(120194002, CardLocation.MonsterZone);
            ai.Duel.Fields[0].MonsterZone[0] = attacker;
            ai.Duel.Fields[1].MonsterZone[0] = pair[0];
            ai.Duel.Fields[1].MonsterZone[1] = pair[1];
            var battle = new BattlePhase { CanEndPhase = true };
            battle.AttackableCards.Add(attacker);
            var action = ai.OnSelectBattleCmd(battle);
            if (action.Action == BattlePhaseAction.BattleAction.Attack)
            {
                var picked = ai.OnSelectCard(pair, 1, 1, HintMsg.AttackTarget, false);
                positions.Add(System.Array.IndexOf(pair, picked.Single()));
            }
        }
        Check("Rush combat chooses the same set zone after hidden identities swap",
            positions.Count == 2 && positions[0] == positions[1]);
    }

    private static void FactoryChecks()
    {
        var ai = CreateBot("TheLegend");
        var blueEyes = Card(120120000, CardLocation.Grave);
        var ox = Card(120194002, CardLocation.Grave);
        var elf = Card(120194003, CardLocation.Grave);
        var offered = new[] { elf, ox, blueEyes };
        foreach (var monster in offered) ai.Duel.Fields[0].Graveyard.Add(monster);
        ai.Duel.Fields[0].Hand.Add(Card(120203028, CardLocation.Hand));
        var factory = Card(120214003, CardLocation.Hand);
        Check("Dark Factory activates with two recoverable normals", ai.OnSelectEffectYn(factory, -1));
        ResolveSpecialistEffect(ai, factory);
        var selected = ai.OnSelectCard(offered, 2, 2, HintMsg.AddToHand, false);
        Check("Dark Factory recovers its Burst Stream partner and a tribute in one prompt",
            selected.Count == 2 && selected.Contains(blueEyes) && selected.Contains(ox) && !ai.HaveSelectedCards());
    }

    private static void EradicatorChecks()
    {
        var ai = CreateBot("ObsidianMagical");
        var sorcerer = Card(120254035, CardLocation.MonsterZone);
        ai.Duel.Fields[0].MonsterZone[0] = sorcerer;
        var spells = new List<ClientCard>();
        for (int i = 0; i < 10; i++)
        {
            var spell = Card(120249055, CardLocation.Grave);
            ai.Duel.Fields[0].Graveyard.Add(spell);
            spells.Add(spell);
        }
        Check("Dark Eradicator accepts its name change with ten spells", ai.OnSelectEffectYn(sorcerer, -1));
        Check("Dark Eradicator waits for the optional recycle prompt", !ai.HaveSelectedCards());
        ResolveSpecialistEffect(ai, sorcerer);
        Check("Dark Eradicator accepts recycling without Obsidian",
            ai.OnSelectYesNo(sorcerer.Id * 16 + 1) && ai.OnSelectCard(spells, 10, 10, HintMsg.ToDeck, false).Distinct().Count() == 10);
        var low = Card(120194002, CardLocation.MonsterZone, 1);
        var levelEight = Card(120120000, CardLocation.MonsterZone, 1);
        Check("Dark Eradicator can destroy a level-eight monster",
            ai.OnSelectCard(new[] { low, levelEight }, 1, 1, HintMsg.Destroy, false).Single() == levelEight);
        ai.Duel.Fields[0].MonsterZone[1] = Card(120246081, CardLocation.MonsterZone);
        ai.Duel.Fields[1].MonsterZone[0] = levelEight;
        Check("Dark Eradicator preserves Obsidian's spells on a contested board", !ai.OnSelectYesNo(sorcerer.Id * 16 + 1));
    }

    private static void SpellcasterRecoveryChecks()
    {
        var ai = CreateBot("ObsidianMagical");
        var cat = Card(120249055, CardLocation.Hand);
        var enemy = Card(120194002, CardLocation.MonsterZone, 1);
        ai.Duel.Fields[1].MonsterZone[0] = enemy;
        var grave = ai.Duel.Fields[0].Graveyard;
        grave.Add(Card(120130000, CardLocation.Grave));
        grave.Add(Card(120181002, CardLocation.Grave));
        Check("Cat-trois Choice mills before choosing a recovery", ai.OnSelectEffectYn(cat, -1) && !ai.HaveSelectedCards());
        var stone = Card(120246083, CardLocation.Grave);
        grave.Add(stone);
        ResolveSpecialistEffect(ai, cat);
        Check("Cat-trois Choice can recover a Stone milled during the effect",
            ai.OnSelectCard(grave, 1, 1, HintMsg.AddToHand, false).Single() == stone);

        ResolveSpecialistEffect(ai, stone);
        var eradicator = Card(120254035, CardLocation.Hand);
        var obsidian = Card(120246081, CardLocation.Grave);
        var girl = Card(120181002, CardLocation.Grave);
        var summons = ai.OnSelectCard(new[] { eradicator, obsidian, girl }, 1, 2, HintMsg.SpSummon, false);
        Check("Stone of the Dark Magicians selects two different attack values", summons.Count == 2
            && summons.Contains(girl) && summons.Contains(obsidian));
    }

    private static void ExcutieChecks()
    {
        var ai = CreateBot("Excutie");
        var lumiere = Card(120231031, CardLocation.MonsterZone);
        var flame = Card(120235019, CardLocation.MonsterZone);
        var strong = Card(120120000, CardLocation.MonsterZone, 1);
        var weaker = Card(120130000, CardLocation.MonsterZone, 1);
        ResolveSpecialistEffect(ai, lumiere);
        var pair = ai.OnSelectCard(new[] { weaker, strong, lumiere, flame }, 2, 2, HintMsg.ToDeck, false);
        Check("Excutie Lumiere returns exactly one monster per field",
            pair.Count == 2 && pair.Contains(strong) && pair.Count(c => c.Controller == 0) == 1);
        var own = ai.OnSelectCard(new[] { lumiere, flame }, 1, 1, HintMsg.ToDeck, false);
        Check("Excutie Lumiere releases the weaker own monster on a split prompt",
            own.Single().Attack == System.Math.Min(flame.Attack, lumiere.Attack));

        ai = CreateBot("Excutie");
        ai.Duel.Fields[0].MonsterZone[0] = flame;
        ai.Duel.Fields[0].MonsterZone[1] = lumiere;
        ai.Duel.Fields[1].MonsterZone[0] = weaker;
        var hand = new[] { Card(120231035, CardLocation.Hand), Card(120231032, CardLocation.Hand) };
        foreach (var cost in hand) ai.Duel.Fields[0].Hand.Add(cost);
        Check("Excutie Flame accepts its attack boost against a stronger monster", ai.OnSelectEffectYn(flame, -1));
        ai.OnSelectCard(hand, 1, 1, HintMsg.ToGrave, false);
        Check("Excutie Flame consumes exactly one discard prompt", !ai.HaveSelectedCards());
        ResolveSpecialistEffect(ai, flame);
        ai.Duel.NegatedChainIndexList.Add(1);
        ai.OnChainSolved(1);
        Check("Negated Excutie Flame leaves the turn restriction unused", ai.OnSelectEffectYn(flame, -1));
        ai.Duel.NegatedChainIndexList.Clear();
        ai.OnChainSolved(1);
        Check("Resolved Excutie Flame preserves the turn restriction", !ai.OnSelectEffectYn(flame, -1));
        ai.OnNewTurn();
        Check("Excutie Flame can boost again next turn", ai.OnSelectEffectYn(flame, -1));
    }

    private static void ConstructorChecks()
    {
        var ai = CreateBot("Mituzi");
        var seal = Card(120183053, CardLocation.Hand);
        var target = Card(120120000, CardLocation.MonsterZone, 1);
        ai.Duel.Fields[1].MonsterZone[0] = target;
        var grave = ai.Duel.Fields[0].Graveyard;
        grave.Add(Card(120183053, CardLocation.Grave));
        grave.Add(Card(120155022, CardLocation.Grave));
        grave.Add(Card(120199020, CardLocation.Grave));
        grave.Add(Card(120155024, CardLocation.Grave));
        Check("Seal Array declines without four monsters and leaves no pending choice",
            !ai.OnSelectEffectYn(seal, -1) && !ai.HaveSelectedCards());
        grave.Add(Card(120151029, CardLocation.Grave));
        grave.Add(Card(120247063, CardLocation.Grave));
        Check("Seal Array can pay four monsters", ai.OnSelectEffectYn(seal, -1));
        var paid = ai.OnSelectCard(grave.Where(c => c.IsMonster()).ToList(), 4, 4, HintMsg.ToDeck, false);
        Check("Seal Array pays one grouped cost while preserving a heavy Wyrm",
            paid.Distinct().Count() == 4 && paid.All(c => c.Id != 120155022) && !ai.HaveSelectedCards());
        ResolveSpecialistEffect(ai, seal);
        Check("Seal Array weakens the strongest legal attack-position monster",
            ai.OnSelectCard(new[] { Card(120194002, CardLocation.MonsterZone, 1), target },
                1, 1, seal.Id * 16 + 1, false).Single() == target);

        ai = CreateBot("Mituzi");
        var resurrection = Card(120151033, CardLocation.SpellZone);
        var buildragon = Card(120155024, CardLocation.Grave);
        var normal = Card(120151029, CardLocation.Grave);
        ai.Duel.Fields[0].SpellZone[5] = Card(120155042, CardLocation.SpellZone);
        ai.Duel.Fields[0].MonsterZone[0] = Card(120155024, CardLocation.MonsterZone);
        ai.Duel.Fields[0].Graveyard.Add(buildragon);
        ai.Duel.Fields[0].Graveyard.Add(normal);
        ai.Duel.Fields[1].MonsterZone[0] = Card(120120000, CardLocation.MonsterZone, 1);
        Check("Constructor Resurrection activates with a Buildragon follow-up", ai.OnSelectEffectYn(resurrection, -1));
        ResolveSpecialistEffect(ai, resurrection);
        var revived = ai.OnSelectCard(new[] { normal, buildragon }, 1, 1, HintMsg.SpSummon, false);
        Check("Constructor Resurrection selects only its own summon",
            revived.Single() == buildragon && !ai.HaveSelectedCards());
        var positions = new[] { CardPosition.FaceUpAttack, CardPosition.FaceUpDefence };
        Check("Constructor Resurrection defends against a stronger attacker in a legal position",
            ai.OnSelectPosition(buildragon.Id, positions) == CardPosition.FaceUpDefence && !ai.HaveSelectedPosition());

        ai = CreateBot("Mituzi");
        var coil = Card(120192011, CardLocation.MonsterZone);
        var field = Card(120155042, CardLocation.SpellZone);
        ai.Duel.Fields[0].SpellZone[5] = field;
        ai.Duel.Fields[1].LifePoints = 601;
        Check("Coil Dragon keeps its last field above lethal", !ai.OnSelectEffectYn(coil, -1) && !ai.HaveSelectedCards());
        ai.Duel.Fields[1].LifePoints = 600;
        Check("Coil Dragon spends its field for exactly lethal damage", ai.OnSelectEffectYn(coil, -1)
            && ai.OnSelectCard(new[] { field }, 1, 1, HintMsg.ToGrave, false).Single() == field);
    }
}
