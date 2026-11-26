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
            public const int 夜魔女 = 120231034;
            public const int 光天女 = 120231031;
            public const int 浮游女 = 120231033;
            public const int 遥控女 = 120231035;
            public const int 救火女 = 120235019; 
            public const int 细剑女 = 120231032;
            public const int 主存女 = 120231036;
            public const int 花草女 = 120231037;
            public const int 劈裂史莱姆 = 120222036;

            public const int 紧急出动 = 120231039;
            public const int 天之选别 = 120231065;       
            public const int 紧急事态 = 120231040;
            public const int 幻影咆哮 = 120235065;

        }

        public ExcutieExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.傲慢之壶);
            AddExecutor(ExecutorType.Activate, CardId.天之选别);
            AddExecutor(ExecutorType.Activate, RushCardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.捕获, 捕获Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.亚龙地狱, 亚龙地狱Effect);
            AddExecutor(ExecutorType.Activate, CardId.紧急事态);
            AddExecutor(ExecutorType.Activate, CardId.幻影咆哮);

            AddExecutor(ExecutorType.SpSummon, CardId.救火女, 救火女Sp);
            AddExecutor(ExecutorType.SpSummon, CardId.夜魔女);
            AddExecutor(ExecutorType.SpSummon, CardId.光天女);
            AddExecutor(ExecutorType.SpSummon, CardId.细剑女);
            AddExecutor(ExecutorType.SpSummon, CardId.救火女);
            AddExecutor(ExecutorType.SpSummon, CardId.花草女);
            AddExecutor(ExecutorType.Activate, CardId.紧急出动, 紧急出动Effect);
            AddExecutor(ExecutorType.SpellSet, RushCardId.黑洞);
            AddExecutor(ExecutorType.SpellSet, RushCardId.圣防);
            AddExecutor(ExecutorType.SpellSet, RushCardId.傲慢之壶);

            AddExecutor(ExecutorType.Activate, CardId.花草女);
            AddExecutor(ExecutorType.Activate, CardId.遥控女, 遥控女Effect1);
            AddExecutor(ExecutorType.Activate, CardId.浮游女, 浮游女Effect);
            AddExecutor(ExecutorType.Activate, CardId.光天女, 光天女Effect);
            AddExecutor(ExecutorType.Activate, CardId.主存女, 主存女Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.海龙骑士, 海龙骑士Effect);
            AddExecutor(ExecutorType.Activate, CardId.劈裂史莱姆, 劈裂史莱姆Effect);

            AddExecutor(ExecutorType.Summon, CardId.夜魔女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.浮游女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.浮游女, 超可爱summon3);
            AddExecutor(ExecutorType.Summon, CardId.光天女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, RushCardId.幻影之龙);
            AddExecutor(ExecutorType.Summon, CardId.救火女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.细剑女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.遥控女, 超可爱summon2);
            AddExecutor(ExecutorType.Summon, CardId.主存女, 超可爱summon2);

            AddExecutor(ExecutorType.Activate, CardId.紧急出动, 紧急出动Effect2);
            AddExecutor(ExecutorType.Activate, CardId.救火女, 救火女Effect1);
            AddExecutor(ExecutorType.MonsterSet, RushCardId.海龙骑士,海龙set);
            AddExecutor(ExecutorType.MonsterSet, RushCardId.结界像,结界像set);
            AddExecutor(ExecutorType.MonsterSet, CardId.劈裂史莱姆, 史莱姆set);
            AddExecutor(ExecutorType.Summon, RushCardId.海龙骑士);
            AddExecutor(ExecutorType.Summon, RushCardId.结界像);
            AddExecutor(ExecutorType.Summon, CardId.劈裂史莱姆);

            AddExecutor(ExecutorType.Activate, CardId.遥控女, 遥控女Effect2);
            AddExecutor(ExecutorType.Activate, CardId.救火女, 救火女Effect2);
            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);

            AddExecutor(ExecutorType.Repos, CardId.救火女, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.光天女, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.夜魔女, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.花草女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.主存女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.细剑女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.救火女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.遥控女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.浮游女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.光天女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.夜魔女, 圣防Repos);
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
            if (link.ActivatePlayer == 0 && link.IsActivateCode(CardId.救火女))
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
                CardId.夜魔女, CardId.光天女, CardId.细剑女, CardId.救火女, CardId.浮游女,CardId.遥控女, CardId.主存女, CardId.花草女};

        private static readonly int[] tar = new[] {
                CardId.花草女, CardId.主存女, CardId.遥控女, CardId.救火女, CardId.浮游女,CardId.光天女,CardId.细剑女};

        private static readonly int[] tar2 = new[] {
                CardId.花草女, CardId.主存女, CardId.遥控女, CardId.浮游女};
        private int FlameAttack(bool summoning)
        {
            int count = Bot.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Level >= 6)
                + Enemy.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Level >= 6);
            return (summoning ? Card.Data.Attack : Card.Attack) + 600 * (count + (summoning ? 1 : 0));
        }

        private bool 救火女Sp()
        {
            return !flameResolved && HasStrongerFaceupEnemy() && Util.GetBestAttack(Enemy) < FlameAttack(true);
        }

        private static readonly int[] TributePriorities = { CardId.花草女, CardId.细剑女, CardId.救火女, CardId.夜魔女 };

        private bool 超可爱summon3()
        {
            return Bot.GetMonsterCount() <= 2 && Bot.HasInMonstersZone(TributePriorities);
        }

        private bool 超可爱summon2()
        {
            return Bot.MonsterZone.Any(c => c != null && c.GetDefensePower() < 1500);
        }

        private bool 超可爱summon()
        {
            return Bot.MonsterZone.Any(c => c != null && c.GetDefensePower() < 1550);
        }

        private bool 救火女Effect1()
        {
            return Bot.Hand.Count > 0 && !flameResolved && HasStrongerFaceupEnemy()
                && Util.GetBestAttack(Enemy) < FlameAttack(false);
        }

        private bool 救火女Effect2()
        {
            if (Bot.Hand.Count == 0 || flameResolved || HasStrongerFaceupEnemy())
                return false;
            ClientCard enemy = Enemy.MonsterZone.Where(c => c != null && c.IsAttack()).OrderBy(c => c.Attack).FirstOrDefault();
            if (enemy == null)
                return false;
            int power = FlameAttack(false);
            return Util.GetTotalAttackingMonsterAttack(0) + 100 <= power || Enemy.LifePoints + enemy.Attack <= power;
        }

        private bool 光天女Effect()
        {
            return Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && (c.Level == 7 || c.Level == 8));
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.光天女) && hint == HintMsg.ToDeck)
                {
                    // The two controllers can be offered together or in successive group prompts.
                    var enemy = cards.Where(c => c.Controller == 1).OrderByDescending(c => c.Attack).FirstOrDefault();
                    var own = cards.Where(c => c.Controller == 0).OrderBy(c => c.Attack).FirstOrDefault();
                    var pair = new List<ClientCard>(2);
                    if (enemy != null) pair.Add(enemy);
                    if (own != null) pair.Add(own);
                    return Util.CheckSelectCount(pair, cards, min, max);
                }
                if (link.IsActivateCode(CardId.劈裂史莱姆))
                {
                    if (hint == Util.GetStringId(CardId.劈裂史莱姆, 1))
                        return cards.OrderByDescending(c => c.Attack).Take(max).ToList();
                    if (hint == HintMsg.SpSummon)
                        return new CardSelector(RushCardId.幻影之龙).Select(cards, min, max);
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private bool 主存女Effect()
        {
                AI.SelectCard(tar);
                AI.SelectNextCard(Enemy.GetSpells());
                return true;
        }
        private bool 浮游女Effect()
        {
            AI.SelectCard(targets);
            return true;
        }
        private bool 遥控女Effect2()
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

        private bool 遥控女Effect1()
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

        private bool 紧急出动Effect()
        {
            if (!Bot.HasInGraveyard(targets) && (Bot.GetMonsterCount() != 0 || !Bot.HasInHand(targets)))
                return false;
            AI.SelectCard(targets);
            return true;
        }

        private bool 捕获Effect()
        {
            ClientCard enemy = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup() && c.Level <= 8)
                .OrderByDescending(c => c.Attack).FirstOrDefault();
            if (enemy == null)
                return false;
            AI.SelectCard(enemy);
            return true;
        }

        private bool 紧急出动Effect2()
        {
            if (!Bot.HasInHand(targets))
                return false;
            AI.SelectCard(CardId.夜魔女, CardId.光天女, CardId.细剑女);
            return true;
        }

        private bool 劈裂史莱姆Effect()
        {
            ClientCard enemy = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup()).OrderByDescending(c => c.Attack).FirstOrDefault();
            ClientCard own = Bot.MonsterZone.GetHighestAttackMonster();
            if (enemy == null || own == null)
                return false;
            if (enemy.Attack > own.Attack && enemy.Attack <= own.Attack + 1000)
                AI.SelectCard(tar);
            else if ((Bot.HasInMonstersZone(CardId.花草女) || Bot.HasInMonstersZone(CardId.主存女)) && Bot.HasInGraveyard(RushCardId.幻影之龙))
                AI.SelectCard(CardId.花草女, CardId.主存女);
            else if (Bot.HasInMonstersZone(RushCardId.幻影之龙))
                AI.SelectCard(RushCardId.幻影之龙);
            else
                return false;
            return true;
        }

        private bool 结界像set()
        {
            return HasStrongerFaceupEnemy();
        }

        private bool 海龙set()
        {
            if ((HasStrongerFaceupEnemy() || Duel.Turn == 1))
            {
                if (Enemy.GetSpellCount() ==0 || Bot.GetSpellCount() < 2)
                return true;
            }         
            return false;
        }
        private bool 史莱姆set()
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





