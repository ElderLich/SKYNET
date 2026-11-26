using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("DinocarriageDynarmix", "AI_DinocarriageDynarmix")]
    public class DinocarriageDynarmixExecutor : RushExecutor
    {
        public class CardId
        {
            public const int 超级恐龙王 = 120130029;
            public const int 凶怒甲龙 = 120231056;
            public const int 巨身多角龙 = 120130005;
            public const int 荒野盗龙 = 120151012;
            public const int 名流雷龙 = 120151013;
            public const int 抑龙 = 120151011;
            public const int 机械镰刀盗龙 = 120151009;
            public const int 成金恐龙王 = 120151010;
            public const int 侏罗纪世界 = 120151015;
            public const int 奇迹的共进化 = 120151016;
            public const int 成金哥布林 = 120151018;
            public const int 恐龙粉碎 = 120151017;
            public const int 超越进化 = 120151019;
            public const int 恐力重压 = 120151020;
            public const int 大恐龙驾L = 120151005;
            public const int 大恐龙驾R = 120151007;
            public const int 大恐龙驾 = 120151006;
        }

        public DinocarriageDynarmixExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.SpSummon, CardId.大恐龙驾, MAXsummon);
            AddExecutor(ExecutorType.Activate, CardId.大恐龙驾, 大恐龙驾Effect);
            AddExecutor(ExecutorType.Activate, CardId.成金哥布林);
            AddExecutor(ExecutorType.Activate, CardId.抑龙);
            AddExecutor(ExecutorType.Activate, CardId.成金恐龙王, 成金恐龙王Effect);
            AddExecutor(ExecutorType.Activate, CardId.机械镰刀盗龙, 机械镰刀盗龙Effect);

            AddExecutor(ExecutorType.Summon, CardId.大恐龙驾L, MAXLsummon);
            AddExecutor(ExecutorType.Summon, CardId.大恐龙驾R, MAXRsummon);
            AddExecutor(ExecutorType.Activate, CardId.奇迹的共进化, 奇迹的共进化Effect);
            AddExecutor(ExecutorType.Repos, CardId.大恐龙驾L, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.大恐龙驾R, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.抑龙, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.机械镰刀盗龙, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.成金恐龙王, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.名流雷龙, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.荒野盗龙, 共进化repos);
            AddExecutor(ExecutorType.Summon, CardId.巨身多角龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.超级恐龙王, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.凶怒甲龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.超级恐龙王, 超级恐龙王Effect);
            AddExecutor(ExecutorType.Summon, CardId.机械镰刀盗龙);
            AddExecutor(ExecutorType.Summon, CardId.抑龙, 抑龙Summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 成金恐龙王Summon);
            AddExecutor(ExecutorType.Activate, CardId.恐龙粉碎, 恐龙粉碎Effect);
            AddExecutor(ExecutorType.SummonOrSet, CardId.荒野盗龙, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.名流雷龙, RushMonsterSummon);

            AddExecutor(ExecutorType.MonsterSet, _CardId.千年盾, RushMonsterSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.大恐龙驾L, MAXLset);
            AddExecutor(ExecutorType.MonsterSet, CardId.大恐龙驾R, MAXRset);
            AddExecutor(ExecutorType.MonsterSet, CardId.抑龙);
            AddExecutor(ExecutorType.MonsterSet, CardId.成金恐龙王);
            AddExecutor(ExecutorType.MonsterSet, CardId.名流雷龙, monsterset);
            AddExecutor(ExecutorType.Activate, CardId.侏罗纪世界);
            AddExecutor(ExecutorType.Activate, CardId.凶怒甲龙);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, CardId.恐力重压);
            AddExecutor(ExecutorType.Activate, CardId.超越进化, 超越进化Effect);
            AddExecutor(ExecutorType.SpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, 圣防Repos);
            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
        }
        protected override bool UseGenericRushChoices { get { return false; } }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards())
                return null;
            if (hint == Util.GetStringId(120000000, 1) && cards.All(c => c.Controller == 0 &&
                c.Location == CardLocation.Hand && (c.IsCode(CardId.大恐龙驾L) || c.IsCode(CardId.大恐龙驾R))))
            {
                // RD.MaximumSummonTarget can offer the two pieces in successive subgroup prompts.
                var pieces = new List<ClientCard>(2);
                foreach (int id in MaximumPieces)
                {
                    var piece = cards.FirstOrDefault(c => c.IsCode(id));
                    if (piece != null && pieces.Count < min)
                        pieces.Add(piece);
                }
                if (pieces.Count == min)
                    return pieces;
            }

            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.恐龙粉碎) &&
                hint == HintMsg.Destroy && min == 1 && max == 1 && cards.Count > 0)
            {
                CardLocation preferred = Bot.HasInMonstersZone(CardId.大恐龙驾) ? CardLocation.MonsterZone : CardLocation.SpellZone;
                ClientCard target = cards.FirstOrDefault(c => c.Controller == 1 && c.IsFacedown() && c.Location == preferred)
                    ?? cards.FirstOrDefault(c => c.Controller == 1 && c.IsFacedown());
                if (target != null)
                    return new[] { target };
            }
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.奇迹的共进化) &&
                hint == HintMsg.SpSummon && min == 1 && max == 1)
            {
                int preferred = Bot.HasInGraveyard(CardId.巨身多角龙) || Bot.HasInGraveyard(CardId.荒野盗龙)
                    ? CardId.超级恐龙王 : CardId.凶怒甲龙;
                var dinosaur = cards.FirstOrDefault(c => c.IsCode(preferred))
                    ?? cards.FirstOrDefault(c => c.IsCode(CardId.凶怒甲龙, CardId.超级恐龙王));
                if (dinosaur != null)
                    return new[] { dinosaur };
            }
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.大恐龙驾) &&
                hint == HintMsg.Destroy && min == 1 && max <= 2 && max >= min)
            {
                var targets = cards.Where(c => c.Controller == 1 && c.Location == CardLocation.MonsterZone)
                    .OrderByDescending(c => c.IsFacedown()).Take(max).ToList();
                if (targets.Count >= min)
                    return targets;
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private static readonly int[] MaximumPieces = { CardId.大恐龙驾L, CardId.大恐龙驾R };

        private bool 大恐龙驾Effect()
        {
            AI.SelectCard(CardId.抑龙, CardId.名流雷龙, CardId.荒野盗龙, CardId.巨身多角龙,
                CardId.成金恐龙王, CardId.机械镰刀盗龙, CardId.凶怒甲龙, CardId.超级恐龙王,
                CardId.大恐龙驾, CardId.大恐龙驾L, CardId.大恐龙驾R);
            return true;
        }

        private bool MAXsummon()
        {
            return !Bot.HasInMonstersZone(CardId.大恐龙驾);
        }

        public bool monsterset()
        {
            if (Duel.Turn == 1)
            {
                return true;
            }
            else if (Bot.HasInHand(new[] {
                CardId.巨身多角龙,
                CardId.超级恐龙王,
                CardId.凶怒甲龙,
                }))
                return false;
            return true;
        }

        private bool 抑龙Summon()
        {
            if (Duel.Turn == 1) return false;
                if (Enemy.GetHandCount() > 1)  return true;
            return false;
        }
        private bool 成金恐龙王Summon()
        {
            if (Enemy.GetHandCount() < 1) return true;            
            return false;
        }

        private bool 成金恐龙王Effect()
        {           
            AI.SelectCard(CardId.成金哥布林);
            return true;
        }
        private bool 奇迹的共进化Effect()
        {
            if (Bot.HasInMonstersZone(CardId.大恐龙驾))
                return false;
            bool hasBoss = Bot.HasInHand(CardId.超级恐龙王);
            bool canRevive = Bot.HasInGraveyard(CardId.巨身多角龙) || Bot.HasInGraveyard(CardId.荒野盗龙);
            int maxLevel = hasBoss && canRevive ? 6 : 4;
            if ((!hasBoss && !Bot.HasInHand(CardId.凶怒甲龙)) || !Bot.MonsterZone.Any(c => c != null &&
                c.IsFaceup() && c.HasRace(CardRace.Dinosaur) && c.Level <= maxLevel))
                return false;
            AI.SelectCard(CardId.大恐龙驾L, CardId.大恐龙驾R, CardId.抑龙, CardId.名流雷龙, CardId.荒野盗龙, CardId.巨身多角龙);
            return true;
        }

        private bool 机械镰刀盗龙Effect()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾) >= 2) AI.SelectCard(CardId.大恐龙驾);
            else
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾L) >= 2) AI.SelectCard(CardId.大恐龙驾L);
            else
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾R) >= 2) AI.SelectCard(CardId.大恐龙驾R);
            else
            if (Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 2) AI.SelectCard(CardId.超级恐龙王);
            else
                AI.SelectCard(
                    CardId.抑龙,
                    CardId.名流雷龙,
                    CardId.荒野盗龙,
                    CardId.巨身多角龙,
                    CardId.成金恐龙王,
                    CardId.机械镰刀盗龙
                    );
            return true;
        }
        private static readonly int[] DuplicateDiscardPriority =
        {
            CardId.侏罗纪世界, CardId.大恐龙驾, CardId.大恐龙驾R, CardId.大恐龙驾L, CardId.巨身多角龙
        };

        private bool 恐龙粉碎Effect()
        {
            foreach (int id in DuplicateDiscardPriority)
            {
                if (Bot.Hand.GetCardCount(id) >= 2)
                {
                    AI.SelectCard(id);
                    return true;
                }
            }
            AI.SelectCard(CardId.成金恐龙王, CardId.抑龙, CardId.奇迹的共进化, CardId.名流雷龙,
                CardId.超级恐龙王, CardId.恐力重压, CardId.超越进化, CardId.恐龙粉碎, CardId.荒野盗龙);
            return true;
        }
        private bool 超越进化Effect()
        {
            AI.SelectCard(
                CardId.大恐龙驾,
                CardId.超级恐龙王,
                CardId.凶怒甲龙
                );
            return true;
        }
        private bool 超级恐龙王Effect()
        {
            AI.SelectCard(
                   CardId.巨身多角龙,
                   CardId.荒野盗龙,
                   CardId.名流雷龙
                   );
            return true;
        }
        private bool 共进化repos()
        {
            if (Bot.HasInHandOrInSpellZone(CardId.奇迹的共进化) && (Bot.Hand.GetCardCount(CardId.凶怒甲龙) + Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 1))
                return true;
            return false;
        }
        private bool MAXLsummon()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾L) >= 2 && Bot.HasInHandOrInSpellZone(CardId.奇迹的共进化) && (Bot.Hand.GetCardCount(CardId.凶怒甲龙) + Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 1))
                return true;
            return false;
        }
        private bool MAXRsummon()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾R) >= 2 && Bot.HasInHandOrInSpellZone(CardId.奇迹的共进化) && (Bot.Hand.GetCardCount(CardId.凶怒甲龙)+ Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 1))
                return true;
            return false;
        }
        private bool MAXLset()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾L) < 2) return false;
            return true;
        }
        private bool MAXRset()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾R) < 2) return false;
            return true;
        }

    }
}
