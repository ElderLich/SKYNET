using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("TheLegend", "AI_TheLegend")]
    public class TheLegendExecutor : RushExecutor
    {
        public class CardId
        {
            public const int BlueEyesWhiteDragon = 120120000;
            public const int BusterBlader = 120170000;
            public const int MultistrikeDragonDragias = 120110001;
            public const int Jinzo = 120155000;
            public const int MillenniumShield = 120194001;
            public const int VorseRaider = 120194002;
            public const int MysticalElf = 120194003;
            public const int TwinBarrelDragon = 120223101;
            public const int PitchBlackWarwolf = 120214001;
            public const int SeaDragonKnight = 120199032;
            public const int RitzyKingRex = 120151010;
            public const int Kuribot = 120120017;
            public const int SiestaTorero = 120170035;
            public const int PhoenixDragon = 120110009;
            public const int MonsterReborn = 120194004;
            public const int UpstartGoblin = 120151018;
            public const int PotOfGreed = 120181003;
            public const int PotOfAvarice = 120217100;
            public const int DarkFactoryOfMassProduction = 120214003;
            public const int AncientRules = 120214010;
            public const int TributeToTheDoomed = 120151023;
            public const int TrapHole = 120150019;
            public const int MirrorForce = 120198003;
            public const int SakuretsuArmor = 120194005;
            public const int DarkRevelation = 120105013;
        }

        public TheLegendExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.DarkMagicAttack, ActivateDarkMagicAttack);
            AddExecutor(ExecutorType.Activate, RushCardId.BarrelDragon, ActivateBarrelDragon);
            AddExecutor(ExecutorType.Activate, RushCardId.DarkHole, ActivateDarkHole);
            AddExecutor(ExecutorType.Activate, RushCardId.BurstStreamOfDestruction, ActivateBurstStreamOfDestruction);
            AddExecutor(ExecutorType.Activate, CardId.TwinBarrelDragon, ActivateTwinBarrelDragon);
            AddExecutor(ExecutorType.Activate, CardId.SeaDragonKnight,UseSeaDragonKnightOnSetBackrow);
            AddExecutor(ExecutorType.Activate, CardId.PotOfGreed);
            AddExecutor(ExecutorType.Activate, CardId.UpstartGoblin);
            AddExecutor(ExecutorType.Activate, CardId.PotOfAvarice);
            AddExecutor(ExecutorType.Activate, CardId.RitzyKingRex, ActivateRitzyKingRex);
            AddExecutor(ExecutorType.Activate, CardId.Kuribot);
            AddExecutor(ExecutorType.Repos, CardId.Kuribot, SummonKuribotWithOneMonster);
            AddExecutor(ExecutorType.Activate, CardId.MultistrikeDragonDragias);
            AddExecutor(ExecutorType.Activate, CardId.SiestaTorero, ActivateSiestaTorero);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Repos, CardId.SiestaTorero, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.TwinBarrelDragon, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.PitchBlackWarwolf, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.SeaDragonKnight, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Summon, CardId.BlueEyesWhiteDragon, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, RushCardId.BarrelDragon, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, RushCardId.DarkMagician, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.BusterBlader, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.MultistrikeDragonDragias, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Jinzo, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Kuribot,SummonKuribotOnEmptyField);
            AddExecutor(ExecutorType.Activate, CardId.AncientRules, ActivateAncientRules);
            AddExecutor(ExecutorType.Activate, CardId.MonsterReborn, ActivateMonsterReborn);
            AddExecutor(ExecutorType.Activate, CardId.DarkFactoryOfMassProduction, ActivateDarkFactoryOfMassProduction);
            AddExecutor(ExecutorType.MonsterSet, CardId.SiestaTorero, Monsterset);
            AddExecutor(ExecutorType.Summon, CardId.SeaDragonKnight);
            AddExecutor(ExecutorType.Summon, CardId.SiestaTorero);
            AddExecutor(ExecutorType.Summon, CardId.TwinBarrelDragon);
            AddExecutor(ExecutorType.Summon, CardId.PitchBlackWarwolf);
            AddExecutor(ExecutorType.Activate, CardId.PhoenixDragon);
            AddExecutor(ExecutorType.Activate, CardId.TributeToTheDoomed, ActivateTributeToTheDoomed);
            AddExecutor(ExecutorType.Repos, CardId.BlueEyesWhiteDragon, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, RushCardId.BarrelDragon, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.BusterBlader, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.MultistrikeDragonDragias, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.Jinzo, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.BlueEyesWhiteDragon, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, RushCardId.BarrelDragon, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, RushCardId.DarkMagician, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.TwinBarrelDragon, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.PitchBlackWarwolf, FlipFaceUp);
            AddExecutor(ExecutorType.MonsterSet, CardId.MillenniumShield, RushMonsterSummon);
            AddExecutor(ExecutorType.Repos, CardId.RitzyKingRex, SummonRitzyKingRex);
            AddExecutor(ExecutorType.Summon, CardId.RitzyKingRex, SummonRitzyKingRex);
            AddExecutor(ExecutorType.SummonOrSet, CardId.VorseRaider);
            AddExecutor(ExecutorType.Summon, CardId.PhoenixDragon, SummonPhoenixDragon);
            AddExecutor(ExecutorType.SpellSet, CardId.MirrorForce);
            AddExecutor(ExecutorType.SpellSet, CardId.TrapHole);
            AddExecutor(ExecutorType.Activate, CardId.TrapHole, ActivateTrapHole);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, ActivateMirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.SakuretsuArmor, ActivateSakuretsuArmor);
            AddExecutor(ExecutorType.Activate, RushCardId.MagicCylinder, ActivateMagicCylinder);
            AddExecutor(ExecutorType.Activate, RushCardId.TorrentialTribute, ActivateTorrentialTribute);
            AddExecutor(ExecutorType.SpellSet, CardId.TributeToTheDoomed);
            AddExecutor(ExecutorType.MonsterSet, CardId.MysticalElf);
            AddExecutor(ExecutorType.MonsterSet, CardId.PhoenixDragon);

            AddExecutor(ExecutorType.MonsterSet, CardId.Kuribot);
            AddExecutor(ExecutorType.MonsterSet, CardId.RitzyKingRex);
            AddExecutor(ExecutorType.Activate, RushCardId.DestroyerOfDragonSorcerers, ActivateDestroyerOfDragonSorcerers);
            AddExecutor(ExecutorType.Activate, RushCardId.PerionTheStarKnight, ActivatePerionTheStarKnight);
            AddExecutor(ExecutorType.Activate, RushCardId.AgarielTheFinestSushiFairy, ActivateAgarielTheFinestSushiFairy);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, CardId.BlueEyesWhiteDragon, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.BusterBlader, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.SpellSet);
            //AddExecutor(ExecutorType.Activate, CardId.sionmax, sionmaxEffect);

        }

        private static readonly int[] TributeMonsters = {
            CardId.MultistrikeDragonDragias, CardId.BlueEyesWhiteDragon, RushCardId.DarkMagician, RushCardId.BarrelDragon, CardId.Jinzo, CardId.BusterBlader
        };

        public bool Monsterset()
        {
            return Duel.Turn == 1 || !Bot.HasInHand(TributeMonsters);
        }

        private bool ActivateRitzyKingRex()
        {
            AI.SelectCard(CardId.UpstartGoblin);
            return true;
        }
        private bool ActivateAncientRules()
        {
            if (Bot.HasInHand(RushCardId.DarkMagician) && Bot.HasInHandOrInSpellZone(RushCardId.DarkMagicAttack))
                AI.SelectCard(RushCardId.DarkMagician);
            else
                AI.SelectCard(CardId.BlueEyesWhiteDragon, RushCardId.DarkMagician);
            return true;
        }

        private bool ActivateDarkFactoryOfMassProduction()
        {
            // Dark Factory returns two Normal Monsters in one selection.
            if (Bot.HasInGraveyard(CardId.BlueEyesWhiteDragon) && Bot.HasInHandOrInSpellZone(RushCardId.BurstStreamOfDestruction))
                AI.SelectCard(CardId.BlueEyesWhiteDragon, CardId.VorseRaider, CardId.MysticalElf, RushCardId.DarkMagician);
            else if (Bot.HasInGraveyard(RushCardId.DarkMagician) && Bot.HasInHandOrInSpellZone(RushCardId.DarkMagicAttack))
                AI.SelectCard(RushCardId.DarkMagician, CardId.VorseRaider, CardId.MysticalElf, CardId.BlueEyesWhiteDragon);
            else
                AI.SelectCard(CardId.VorseRaider, CardId.MysticalElf, CardId.BlueEyesWhiteDragon, RushCardId.DarkMagician);
            return true;
        }

        private bool SummonRitzyKingRex()
        {
            if (Enemy.GetHandCount() < 1) return true;
            return false;
        }
        private bool SummonKuribotOnEmptyField()
        {
            if (Bot.HasInGraveyard(CardId.Kuribot) && Bot.GetMonstersInMainZone().Count == 0)
                    return true;
            return false;
        }
        private bool SummonKuribotWithOneMonster()
        {
            if (Bot.HasInGraveyard(CardId.Kuribot) && Bot.GetMonstersInMainZone().Count == 1)
                return true;
            return false;
        }
        private bool SummonPhoenixDragon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.MultistrikeDragonDragias, CardId.BlueEyesWhiteDragon }) &&
                Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 4) < 2 &&
                (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.IsMonster() && card.Level < 5)) <= 2)
            {
                return true;
            }
            return false;
        }
        private bool UseSeaDragonKnightOnSetBackrow()
        {
            ClientCard selected = Enemy.SpellZone.FirstOrDefault(c => c != null && c.IsFacedown());
            if (selected == null)
                return false;
            AI.SelectCard(RushCardId.BarrelDragon, CardId.SeaDragonKnight, CardId.BlueEyesWhiteDragon,CardId.PitchBlackWarwolf, RushCardId.DarkMagician, CardId.MysticalElf, CardId.TwinBarrelDragon, CardId.VorseRaider, CardId.Kuribot);
            AI.SelectNextCard(selected);
            return true;
        }
        private bool ActivateTwinBarrelDragon()
        {
            ClientCard target = Util.GetOneEnemyBetterThanMyBest(true, true);
            if (target == null)
            {
                var backrow = Enemy.GetSpells();
                if (backrow.Count > 0)
                    target = backrow[Program.Rand.Next(backrow.Count)];
            }
            if (target == null)
            {
                var monsters = Enemy.GetMonsters();
                if (monsters.Count > 0)
                    target = monsters[Program.Rand.Next(monsters.Count)];
            }
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

    }
}





