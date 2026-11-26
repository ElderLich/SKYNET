using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("Excutie", "AI_Excutie")]
    public class ExcutieExecutor : RushExecutor
    {
        public class CardId
        {
            public const int ExcutieLilius = 120231034;
            public const int ExcutieLumiere = 120231031;
            public const int ExcutieFloa = 120231033;
            public const int ExcutieLucy = 120231035;
            public const int ExecutieFlame = 120235019;
            public const int ExcutieLeya = 120231032;
            public const int ExcutieRAM = 120231036;
            public const int ExcutiePlawtie = 120231037;
            public const int SplitterSlime = 120222036;

            public const int ExcutieScramble = 120231039;
            public const int HeavenlySelection = 120231065;
            public const int ExecutieEmergency = 120231040;
            public const int PhantomRoar = 120235065;

        }

        public ExcutieExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.DarkHole, ActivateDarkHole);
            AddExecutor(ExecutorType.Activate, RushCardId.PotOfArrogance);
            AddExecutor(ExecutorType.Activate, CardId.HeavenlySelection);
            AddExecutor(ExecutorType.Activate, RushCardId.MirrorForce, ActivateMirrorForce);
            AddExecutor(ExecutorType.Activate, RushCardId.ExcutieCapture, ActivateExcutieCapture);
            AddExecutor(ExecutorType.Activate, RushCardId.DragonlionPitTrap, ActivateDragonlionPitTrap);
            AddExecutor(ExecutorType.Activate, CardId.ExecutieEmergency);
            AddExecutor(ExecutorType.Activate, CardId.PhantomRoar);

            AddExecutor(ExecutorType.SpSummon, CardId.ExecutieFlame, SpecialSummonExecutieFlame);
            AddExecutor(ExecutorType.SpSummon, CardId.ExcutieLilius);
            AddExecutor(ExecutorType.SpSummon, CardId.ExcutieLumiere);
            AddExecutor(ExecutorType.SpSummon, CardId.ExcutieLeya);
            AddExecutor(ExecutorType.SpSummon, CardId.ExecutieFlame);
            AddExecutor(ExecutorType.SpSummon, CardId.ExcutiePlawtie);
            AddExecutor(ExecutorType.Activate, CardId.ExcutieScramble, ActivateExcutieScramble);
            AddExecutor(ExecutorType.SpellSet, RushCardId.DarkHole);
            AddExecutor(ExecutorType.SpellSet, RushCardId.MirrorForce);
            AddExecutor(ExecutorType.SpellSet, RushCardId.PotOfArrogance);

            AddExecutor(ExecutorType.Activate, CardId.ExcutiePlawtie);
            AddExecutor(ExecutorType.Activate, CardId.ExcutieLucy, UseLucyOnAttackPositionMonster);
            AddExecutor(ExecutorType.Activate, CardId.ExcutieFloa, ActivateExcutieFloa);
            AddExecutor(ExecutorType.Activate, CardId.ExcutieLumiere, ActivateExcutieLumiere);
            AddExecutor(ExecutorType.Activate, CardId.ExcutieRAM, ActivateExcutieRAM);
            AddExecutor(ExecutorType.Activate, RushCardId.SeaDragonKnight, ActivateSeaDragonKnight);
            AddExecutor(ExecutorType.Activate, CardId.SplitterSlime, ActivateSplitterSlime);

            AddExecutor(ExecutorType.Summon, CardId.ExcutieLilius, TributeForExcutie);
            AddExecutor(ExecutorType.Summon, CardId.ExcutieFloa, TributeForExcutie);
            AddExecutor(ExecutorType.Summon, CardId.ExcutieFloa, TributeForExcutieFloa);
            AddExecutor(ExecutorType.Summon, CardId.ExcutieLumiere, TributeForExcutie);
            AddExecutor(ExecutorType.Summon, RushCardId.MirageDragon);
            AddExecutor(ExecutorType.Summon, CardId.ExecutieFlame, TributeForExcutie);
            AddExecutor(ExecutorType.Summon, CardId.ExcutieLeya, TributeForExcutie);
            AddExecutor(ExecutorType.Summon, CardId.ExcutieLucy, TributeForSupportExcutie);
            AddExecutor(ExecutorType.Summon, CardId.ExcutieRAM, TributeForSupportExcutie);

            AddExecutor(ExecutorType.Activate, CardId.ExcutieScramble, UseScrambleFromHand);
            AddExecutor(ExecutorType.Activate, CardId.ExecutieFlame, UseFlameToOvercomeMonster);
            AddExecutor(ExecutorType.MonsterSet, RushCardId.SeaDragonKnight,SetSeaDragonKnight);
            AddExecutor(ExecutorType.MonsterSet, RushCardId.BarrierStatueOfTheInferno,SetBarrierStatueOfTheInferno);
            AddExecutor(ExecutorType.MonsterSet, CardId.SplitterSlime, SetSplitterSlime);
            AddExecutor(ExecutorType.Summon, RushCardId.SeaDragonKnight);
            AddExecutor(ExecutorType.Summon, RushCardId.BarrierStatueOfTheInferno);
            AddExecutor(ExecutorType.Summon, CardId.SplitterSlime);

            AddExecutor(ExecutorType.Activate, CardId.ExcutieLucy, UseLucyOnSetMonster);
            AddExecutor(ExecutorType.Activate, CardId.ExecutieFlame, UseFlameForBattleDamage);
            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);

            AddExecutor(ExecutorType.Repos, CardId.ExecutieFlame, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieLumiere, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieLilius, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.ExcutiePlawtie, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieRAM, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieLeya, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.ExecutieFlame, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieLucy, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieFloa, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieLumiere, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.ExcutieLilius, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.SummonOrSet, ImFeelingLazy);
            AddExecutor(ExecutorType.SpellSet);
        }



        private bool flameResolved;

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            flameResolved = false;
        }

        public override void OnChainSolved(int chainIndex)
        {
            base.OnChainSolved(chainIndex);
            if (chainIndex <= 0 || chainIndex > Duel.CurrentChainInfo.Count || Duel.NegatedChainIndexList.Contains(chainIndex))
                return;
            ChainInfo link = Duel.CurrentChainInfo[chainIndex - 1];
            if (link.ActivatePlayer == 0 && link.IsActivateCode(CardId.ExecutieFlame))
                flameResolved = true;
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            ClientCard weakest = null;
            foreach (ClientCard defender in defenders)
            {
                if (defender.IsFaceup() && defender.IsAttack() && defender.Attack <= 1450
                    && (weakest == null || defender.Attack < weakest.Attack))
                    weakest = defender;
            }
            if (weakest != null)
            {
                attacker.RealPower = attacker.GetAttackPower();
                weakest.RealPower = weakest.GetDefensePower();
                if (OnPreBattleBetween(attacker, weakest) && (attacker.RealPower > weakest.RealPower
                    || (attacker.IsLastAttacker && attacker.RealPower == weakest.RealPower)))
                    return AI.Attack(attacker, weakest);
            }
            return base.OnSelectAttackTarget(attacker, defenders);
        }

        private static readonly int[] targets = new[] {
                CardId.ExcutieLilius, CardId.ExcutieLumiere, CardId.ExcutieLeya, CardId.ExecutieFlame, CardId.ExcutieFloa,CardId.ExcutieLucy, CardId.ExcutieRAM, CardId.ExcutiePlawtie};

        private static readonly int[] tar = new[] {
                CardId.ExcutiePlawtie, CardId.ExcutieRAM, CardId.ExcutieLucy, CardId.ExecutieFlame, CardId.ExcutieFloa,CardId.ExcutieLumiere,CardId.ExcutieLeya};

        private static readonly int[] tar2 = new[] {
                CardId.ExcutiePlawtie, CardId.ExcutieRAM, CardId.ExcutieLucy, CardId.ExcutieFloa};
        private int FlameAttack(bool summoning)
        {
            int count = Bot.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Level >= 6)
                + Enemy.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Level >= 6);
            return (summoning ? Card.Data.Attack : Card.Attack) + 600 * (count + (summoning ? 1 : 0));
        }

        private bool SpecialSummonExecutieFlame()
        {
            return !flameResolved && HasStrongerFaceupEnemy() && Util.GetBestAttack(Enemy) < FlameAttack(true);
        }

        private static readonly int[] TributePriorities = { CardId.ExcutiePlawtie, CardId.ExcutieLeya, CardId.ExecutieFlame, CardId.ExcutieLilius };

        private bool TributeForExcutieFloa()
        {
            return Bot.GetMonsterCount() <= 2 && Bot.HasInMonstersZone(TributePriorities);
        }

        private bool TributeForSupportExcutie()
        {
            return Bot.MonsterZone.Any(c => c != null && c.GetDefensePower() < 1500);
        }

        private bool TributeForExcutie()
        {
            return Bot.MonsterZone.Any(c => c != null && c.GetDefensePower() < 1550);
        }

        private bool UseFlameToOvercomeMonster()
        {
            return Bot.Hand.Count > 0 && !flameResolved && HasStrongerFaceupEnemy()
                && Util.GetBestAttack(Enemy) < FlameAttack(false);
        }

        private bool UseFlameForBattleDamage()
        {
            if (Bot.Hand.Count == 0 || flameResolved || HasStrongerFaceupEnemy())
                return false;
            ClientCard enemy = Enemy.MonsterZone.Where(c => c != null && c.IsAttack()).OrderBy(c => c.Attack).FirstOrDefault();
            if (enemy == null)
                return false;
            int power = FlameAttack(false);
            return Util.GetTotalAttackingMonsterAttack(0) + 100 <= power || Enemy.LifePoints + enemy.Attack <= power;
        }

        private bool ActivateExcutieLumiere()
        {
            return Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && (c.Level == 7 || c.Level == 8));
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.ExcutieLumiere) && hint == HintMsg.ToDeck)
                {
                    // The two controllers can be offered together or in successive group prompts.
                    var enemy = cards.Where(c => c.Controller == 1).OrderByDescending(c => c.Attack).FirstOrDefault();
                    var own = cards.Where(c => c.Controller == 0).OrderBy(c => c.Attack).FirstOrDefault();
                    var pair = new List<ClientCard>(2);
                    if (enemy != null) pair.Add(enemy);
                    if (own != null) pair.Add(own);
                    return Util.CheckSelectCount(pair, cards, min, max);
                }
                if (link.IsActivateCode(CardId.SplitterSlime))
                {
                    if (hint == Util.GetStringId(CardId.SplitterSlime, 1))
                        return cards.OrderByDescending(c => c.Attack).Take(max).ToList();
                    if (hint == HintMsg.SpSummon)
                        return new CardSelector(RushCardId.MirageDragon).Select(cards, min, max);
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private bool ActivateExcutieRAM()
        {
                AI.SelectCard(tar);
                AI.SelectNextCard(Enemy.GetSpells());
                return true;
        }
        private bool ActivateExcutieFloa()
        {
            AI.SelectCard(targets);
            return true;
        }
        private bool UseLucyOnSetMonster()
        {
            foreach (ClientCard mon in Enemy.GetMonsters())
                if (mon.IsFacedown())
                {
                    AI.SelectCard(targets);
                    AI.SelectNextCard(mon);
                    return true;
                }
            return false;
        }

        private bool UseLucyOnAttackPositionMonster()
        {
            ClientCard best = Util.GetBestBotMonster();
            if (best == null) return false;
            ClientCard enemy = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup() && c.IsAttack()
                && c.Attack > 1800 && c.Defense < best.Attack).OrderByDescending(c => c.Attack).FirstOrDefault();
            if (enemy == null || !Bot.HasInGraveyard(targets))
                return false;
            AI.SelectCard(targets);
            AI.SelectNextCard(enemy);
            return true;
        }

        private bool ActivateExcutieScramble()
        {
            if (!Bot.HasInGraveyard(targets) && (Bot.GetMonsterCount() != 0 || !Bot.HasInHand(targets)))
                return false;
            AI.SelectCard(targets);
            return true;
        }

        private bool ActivateExcutieCapture()
        {
            ClientCard enemy = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup() && c.Level <= 8)
                .OrderByDescending(c => c.Attack).FirstOrDefault();
            if (enemy == null)
                return false;
            AI.SelectCard(enemy);
            return true;
        }

        private bool UseScrambleFromHand()
        {
            if (!Bot.HasInHand(targets))
                return false;
            AI.SelectCard(CardId.ExcutieLilius, CardId.ExcutieLumiere, CardId.ExcutieLeya);
            return true;
        }

        private bool ActivateSplitterSlime()
        {
            ClientCard enemy = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup()).OrderByDescending(c => c.Attack).FirstOrDefault();
            ClientCard own = Bot.MonsterZone.GetHighestAttackMonster();
            if (enemy == null || own == null)
                return false;
            if (enemy.Attack > own.Attack && enemy.Attack <= own.Attack + 1000)
                AI.SelectCard(tar);
            else if ((Bot.HasInMonstersZone(CardId.ExcutiePlawtie) || Bot.HasInMonstersZone(CardId.ExcutieRAM)) && Bot.HasInGraveyard(RushCardId.MirageDragon))
                AI.SelectCard(CardId.ExcutiePlawtie, CardId.ExcutieRAM);
            else if (Bot.HasInMonstersZone(RushCardId.MirageDragon))
                AI.SelectCard(RushCardId.MirageDragon);
            else
                return false;
            return true;
        }

        private bool SetBarrierStatueOfTheInferno()
        {
            return HasStrongerFaceupEnemy();
        }

        private bool SetSeaDragonKnight()
        {
            if ((HasStrongerFaceupEnemy() || Duel.Turn == 1))
            {
                if (Enemy.GetSpellCount() ==0 || Bot.GetSpellCount() < 2)
                return true;
            }
            return false;
        }
        private bool SetSplitterSlime()
        {
                if ( Duel.Turn == 1 || !Bot.HasInMonstersZone(tar2))
                return true;
            return false;
        }

        private bool ImFeelingLazy()
        {
            if (Executors.Any(exec => (exec.Type == ExecutorType.SummonOrSet || exec.Type == ExecutorType.Summon || exec.Type == ExecutorType.MonsterSet) && exec.CardId == Card.Id))
                return false;
            return RushMonsterSummon();
        }
    }
}





