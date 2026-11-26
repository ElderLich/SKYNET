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
            public const int 青眼白龙 = 120120000;
            public const int 破坏之剑士 = 120170000;
            public const int 连击龙 = 120110001;
            public const int 人造人 = 120155000;
            public const int 千年盾 = 120194001; 
            public const int 血腥魔兽人 = 120194002;
            public const int 圣精灵 = 120194003;
            public const int 小手枪龙 = 120223101;
            public const int 狼人 = 120214001;
            public const int 海龙骑士 = 120199032;
            public const int 成金恐龙王 = 120151010;       
            public const int 机器栗子 = 120120017;
            public const int 斗牛士 = 120170035;
            public const int 凤凰龙 = 120110009;
            public const int 死苏 = 120194004;            
            public const int 成金哥布林 = 120151018;
            public const int 强欲之壶 = 120181003;
            public const int 贪欲之壶 = 120217100;
            public const int 工厂 = 120214003;
            public const int 古之规则 = 120214010;
            public const int 对死者的供奉 = 120151023;
            public const int 落穴 = 120150019;
            public const int 圣防 = 120198003;
            public const int 炸甲 = 120194005;
            public const int 暗黑释放 = 120105013;
        }

        public TheLegendExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.HMD, HMDEffect);
            AddExecutor(ExecutorType.Activate, _CardId.手枪龙, 手枪龙Effect);
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.Activate, _CardId.疾风弹, 疾风弹Effect);
            AddExecutor(ExecutorType.Activate, CardId.小手枪龙, 小手枪龙Effect);            
            AddExecutor(ExecutorType.Activate, CardId.海龙骑士,new海龙骑士Effect);
            AddExecutor(ExecutorType.Activate, CardId.强欲之壶);
            AddExecutor(ExecutorType.Activate, CardId.成金哥布林);
            AddExecutor(ExecutorType.Activate, CardId.贪欲之壶);
            AddExecutor(ExecutorType.Activate, CardId.成金恐龙王, 成金恐龙王Effect);
            AddExecutor(ExecutorType.Activate, CardId.机器栗子);
            AddExecutor(ExecutorType.Repos, CardId.机器栗子, 栗子summon2);
            AddExecutor(ExecutorType.Activate, CardId.连击龙);
            AddExecutor(ExecutorType.Activate, CardId.斗牛士, 斗牛士Effect);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Repos, CardId.斗牛士, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.小手枪龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.狼人, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.海龙骑士, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Summon, CardId.青眼白龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, _CardId.手枪龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, _CardId.黑魔术师, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.破坏之剑士, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.连击龙, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.人造人, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.机器栗子,栗子summon);            
            AddExecutor(ExecutorType.Activate, CardId.古之规则, 古之规则Effect);
            AddExecutor(ExecutorType.Activate, CardId.死苏, 死苏Effect);
            AddExecutor(ExecutorType.Activate, CardId.工厂, 工厂Effect);
            AddExecutor(ExecutorType.MonsterSet, CardId.斗牛士, Monsterset);
            AddExecutor(ExecutorType.Summon, CardId.海龙骑士);
            AddExecutor(ExecutorType.Summon, CardId.斗牛士);
            AddExecutor(ExecutorType.Summon, CardId.小手枪龙);
            AddExecutor(ExecutorType.Summon, CardId.狼人);
            AddExecutor(ExecutorType.Activate, CardId.凤凰龙);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, 死供Effect);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, _CardId.手枪龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.破坏之剑士, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.连击龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.人造人, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, _CardId.手枪龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, _CardId.黑魔术师, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.小手枪龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.狼人, 反转Repos);
            AddExecutor(ExecutorType.MonsterSet, CardId.千年盾, RushMonsterSummon);
            AddExecutor(ExecutorType.Repos, CardId.成金恐龙王, 成金恐龙王Summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 成金恐龙王Summon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.血腥魔兽人);
            AddExecutor(ExecutorType.Summon, CardId.凤凰龙, 凤凰龙summon);
            AddExecutor(ExecutorType.SpellSet, CardId.圣防);
            AddExecutor(ExecutorType.SpellSet, CardId.落穴);
            AddExecutor(ExecutorType.Activate, CardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, CardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, _CardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.对死者的供奉);
            AddExecutor(ExecutorType.MonsterSet, CardId.圣精灵);
            AddExecutor(ExecutorType.MonsterSet, CardId.凤凰龙);
            
            AddExecutor(ExecutorType.MonsterSet, CardId.机器栗子);
            AddExecutor(ExecutorType.MonsterSet, CardId.成金恐龙王);
            AddExecutor(ExecutorType.Activate, _CardId.破灭之龙魔导士, 破灭之龙魔导士Effect);
            AddExecutor(ExecutorType.Activate, _CardId.星战骑佩流安, 星战骑佩流安Effect);
            AddExecutor(ExecutorType.Activate, _CardId.加百列热茶, 加百列热茶Effect);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.破坏之剑士, 圣防Repos);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);                               
            AddExecutor(ExecutorType.SpellSet);
            //AddExecutor(ExecutorType.Activate, CardId.sionmax, sionmaxEffect);
 
        }

        private static readonly int[] TributeMonsters = {
            CardId.连击龙, CardId.青眼白龙, _CardId.黑魔术师, _CardId.手枪龙, CardId.人造人, CardId.破坏之剑士
        };

        public bool Monsterset()
        {
            return Duel.Turn == 1 || !Bot.HasInHand(TributeMonsters);
        }

        private bool 成金恐龙王Effect()
        {
            AI.SelectCard(CardId.成金哥布林);
            return true;
        }
        private bool 古之规则Effect()
        {
            if (Bot.HasInHand(_CardId.黑魔术师) && Bot.HasInHandOrInSpellZone(_CardId.HMD))
                AI.SelectCard(_CardId.黑魔术师);
            else
                AI.SelectCard(CardId.青眼白龙, _CardId.黑魔术师);
            return true;
        }

        private bool 工厂Effect()
        {
            // Dark Factory returns two Normal Monsters in one selection.
            if (Bot.HasInGraveyard(CardId.青眼白龙) && Bot.HasInHandOrInSpellZone(_CardId.疾风弹))
                AI.SelectCard(CardId.青眼白龙, CardId.血腥魔兽人, CardId.圣精灵, _CardId.黑魔术师);
            else if (Bot.HasInGraveyard(_CardId.黑魔术师) && Bot.HasInHandOrInSpellZone(_CardId.HMD))
                AI.SelectCard(_CardId.黑魔术师, CardId.血腥魔兽人, CardId.圣精灵, CardId.青眼白龙);
            else
                AI.SelectCard(CardId.血腥魔兽人, CardId.圣精灵, CardId.青眼白龙, _CardId.黑魔术师);
            return true;
        }

        private bool 成金恐龙王Summon()
        {
            if (Enemy.GetHandCount() < 1) return true;
            return false;
        }
        private bool 栗子summon()
        {
            if (Bot.HasInGraveyard(CardId.机器栗子) && Bot.GetMonstersInMainZone().Count == 0)
                    return true;
            return false;
        }
        private bool 栗子summon2()
        {
            if (Bot.HasInGraveyard(CardId.机器栗子) && Bot.GetMonstersInMainZone().Count == 1)
                return true;
            return false;
        }
        private bool 凤凰龙summon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.连击龙, CardId.青眼白龙 }) && 
                Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 4) < 2 && 
                (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.IsMonster() && card.Level < 5)) <= 2)
            {
                return true;
            }                
            return false;
        }
        private bool new海龙骑士Effect()
        {
            ClientCard selected = Enemy.SpellZone.FirstOrDefault(c => c != null && c.IsFacedown());
            if (selected == null)
                return false;
            AI.SelectCard(_CardId.手枪龙, CardId.海龙骑士, CardId.青眼白龙,CardId.狼人, _CardId.黑魔术师, CardId.圣精灵, CardId.小手枪龙, CardId.血腥魔兽人, CardId.机器栗子);
            AI.SelectNextCard(selected);
            return true;
        }
        private bool 小手枪龙Effect()
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





