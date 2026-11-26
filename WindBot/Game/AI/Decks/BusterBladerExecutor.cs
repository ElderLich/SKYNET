using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("BusterBlader", "AI_BusterBlader")]
   
    public class BusterBladerExecutor : RushExecutor
    {
        public class CardId
        {
            public const int 青眼白龙 = 120120000;
            public const int 破坏之剑士 = 120170000;
            public const int 黑魔术师 = 120130000;
            public const int 真红眼黑龙 = 120125001;
            public const int 恶魔召唤 = 120145000;
            public const int 人造人 = 120155000;
            public const int 连击龙 = 120110001;
            public const int 七星道魔术师 = 120105001;
            public const int 雅灭鲁拉 = 120120029;
            public const int 耳语妖精 = 120120018;
            public const int 神秘庄家 = 120105006;
            public const int 侏儒兔 = 120228031;
            public const int 光芒巫师 = 120145001;
            public const int 暗黑巫师 = 120105005;
            public const int 火星心少女 = 120145014;
            public const int 斗牛士 = 120170035;
            public const int 凤凰龙 = 120110009;
            public const int 七星道法师 = 120130016;
            public const int sionmax = 120150007;
            public const int 对死者的供奉 = 120151023;
            public const int 落穴 = 120150019;
            public const int 暗黑释放 = 120105013;
           
        }

        public BusterBladerExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.Activate, _CardId.疾风弹, 疾风弹Effect);
            AddExecutor(ExecutorType.Activate, _CardId.强欲之壶);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, CardId.七星道法师);
            AddExecutor(ExecutorType.Activate, CardId.侏儒兔);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, () => TributeToTheDoomed(2600));
            AddExecutor(ExecutorType.Summon, CardId.青眼白龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.破坏之剑士, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.恶魔召唤, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.连击龙, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.雅灭鲁拉, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.黑魔术师, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.真红眼黑龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.七星道魔术师, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.人造人, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.神秘庄家);
            AddExecutor(ExecutorType.Summon, CardId.侏儒兔, 侏儒兔Summon);
            AddExecutor(ExecutorType.Activate, CardId.神秘庄家, Draweffect);
            AddExecutor(ExecutorType.SpellSet, CardId.暗黑释放);
            AddExecutor(ExecutorType.SpellSet, CardId.落穴);
            AddExecutor(ExecutorType.Activate, CardId.暗黑释放, 圣防Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, _CardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, CardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, _CardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, 死供Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.对死者的供奉);
            AddExecutor(ExecutorType.Activate, CardId.凤凰龙);
            AddExecutor(ExecutorType.Summon, CardId.凤凰龙, 凤凰龙summon);
            AddExecutor(ExecutorType.MonsterSet, CardId.凤凰龙);
            AddExecutor(ExecutorType.MonsterSet, CardId.侏儒兔);
            AddExecutor(ExecutorType.MonsterSet, CardId.火星心少女, Monsterset);
            AddExecutor(ExecutorType.MonsterSet, CardId.七星道法师, Monsterset);
            AddExecutor(ExecutorType.MonsterSet, CardId.耳语妖精, Monsterset);
            AddExecutor(ExecutorType.Summon, CardId.七星道法师);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.连击龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.七星道魔术师, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.破坏之剑士, 圣防Repos);                   
            AddExecutor(ExecutorType.Activate, CardId.耳语妖精, 耳语妖精Effect);
            AddExecutor(ExecutorType.Activate, CardId.火星心少女);
            AddExecutor(ExecutorType.Activate, CardId.连击龙);
            AddExecutor(ExecutorType.Activate, CardId.七星道魔术师);
            AddExecutor(ExecutorType.Activate, CardId.斗牛士, 斗牛士Effect);
            AddExecutor(ExecutorType.Activate, _CardId.破灭之龙魔导士, 破灭之龙魔导士Effect);
            AddExecutor(ExecutorType.Activate, _CardId.星战骑佩流安, 星战骑佩流安Effect);
            AddExecutor(ExecutorType.Activate, _CardId.加百列热茶, 加百列热茶Effect);
            AddExecutor(ExecutorType.Activate, _CardId.死苏, 死苏Effect);
            AddExecutor(ExecutorType.Activate, _CardId.死苏2, 死苏Effect);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.SpellSet);
        }

        private static readonly int[] TributeMonsters =
        {
            CardId.七星道魔术师, CardId.连击龙, CardId.青眼白龙, CardId.人造人, CardId.恶魔召唤,
            CardId.雅灭鲁拉, CardId.破坏之剑士, CardId.真红眼黑龙, CardId.黑魔术师
        };

        public bool Monsterset()
        {
            return Duel.Turn == 1 || !Bot.HasInHandOrHasInMonstersZone(TributeMonsters);
        }

        private bool 侏儒兔Summon()
        {
            return Bot.HasInHandOrHasInMonstersZone(TributeMonsters);
        }

        public override bool OnSelectYesNo(int desc)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (desc == Util.GetStringId(CardId.侏儒兔, 1) && link != null &&
                link.ActivatePlayer == 0 && link.IsActivateCode(CardId.侏儒兔))
                return false;
            return base.OnSelectYesNo(desc);
        }

        private bool Draweffect()
        {

            if ((Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.IsMonster() && card.Level < 5)) <= 2)
            {
                if ((Bot.Hand.GetMatchingCardsCount(card => card.Level > 5) > 2) && Bot.HasInHand(CardId.七星道魔术师))
                    AI.SelectCard(CardId.七星道魔术师);
                else
                    return false;
            }
            else
                AI.SelectCard(CardId.七星道法师, CardId.火星心少女,CardId.侏儒兔, CardId.耳语妖精, CardId.神秘庄家, CardId.光芒巫师, CardId.暗黑巫师);
            return true;
        }
        private bool 凤凰龙summon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.连击龙, CardId.青眼白龙 }) &&
                Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 4) < 2 &&
                (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.IsMonster() && card.Level < 5)) >= 2)
            {
                return true;
            }
            return false;
        }

    }
}
