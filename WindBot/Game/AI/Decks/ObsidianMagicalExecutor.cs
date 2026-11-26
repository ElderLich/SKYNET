using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("ObsidianMagical", "AI_ObsidianMagical")]
    public class ObsidianMagicalExecutor : RushExecutor
    {
        public class CardId
        {
            public const int 遂行者 = 120254035;
            public const int 剑黎 = 120246080;
            public const int 黑曜 = 120246081;
            public const int DMG = 120181002;
            public const int DMA = 120130041;
            public const int DMB = 120203027;
            public const int 采掘 = 120203029;
            public const int 魔术的幕帘 = 120217018;
            public const int 二重加速 = 120246018;
            public const int 宝石 = 120246083;
            public const int 魔导书弃却 = 120246084;
            public const int 魔力抽出 = 120246085;
            public const int 七宝船 = 120199052;
            public const int 三猫 = 120249055;
            public const int 天惠枪 = 120244055;
            public const int 水流 = 120105012;
            public const int 幽灵旋风 = 120208053;

        }

        public ObsidianMagicalExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, CardId.DMA, DMAEffect);
            AddExecutor(ExecutorType.Activate, CardId.幽灵旋风);
            AddExecutor(ExecutorType.Activate, CardId.水流);
            AddExecutor(ExecutorType.Activate, CardId.三猫, 三猫Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.黑洞, 黑洞Effect);
                        
            AddExecutor(ExecutorType.Activate, CardId.魔导书弃却);
            AddExecutor(ExecutorType.Activate, CardId.七宝船, 七宝船Effect);
            AddExecutor(ExecutorType.Activate, CardId.遂行者);
            AddExecutor(ExecutorType.Activate, CardId.采掘, 采掘Effect);

            AddExecutor(ExecutorType.SpellSet, CardId.魔导书弃却, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.三猫, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.魔力抽出, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.幽灵旋风, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.水流, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.天惠枪, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.DMB, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.魔术的幕帘, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.宝石, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.采掘, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.DMA, 抽出set);
            AddExecutor(ExecutorType.Activate, CardId.魔力抽出, 魔力抽出Effect);

            AddExecutor(ExecutorType.SpSummon, CardId.剑黎, 抽出sp);
            AddExecutor(ExecutorType.Activate, CardId.宝石, 宝石Effect);
            AddExecutor(ExecutorType.Activate, CardId.采掘, 采掘Effect2);
            AddExecutor(ExecutorType.Activate, CardId.七宝船, 七宝船Effect2);
            AddExecutor(ExecutorType.Activate, CardId.魔术的幕帘, 幕帘Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.七宝船, 抽出set);
            AddExecutor(ExecutorType.Activate, CardId.二重加速, 二重加速Effect);
            AddExecutor(ExecutorType.Activate, CardId.DMB, DMBEffect);
            AddExecutor(ExecutorType.Activate, CardId.天惠枪, 天惠枪Effect);

            AddExecutor(ExecutorType.SpellSet, CardId.魔力抽出, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.魔术的幕帘, 幕帘set);
            AddExecutor(ExecutorType.SpellSet, CardId.七宝船, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.采掘, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.幽灵旋风, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.宝石, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.DMB, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.二重加速, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.水流, speset);           
            AddExecutor(ExecutorType.SpellSet, CardId.天惠枪, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.三猫, speset);            
            AddExecutor(ExecutorType.SpellSet, RushCardId.魔法筒);
            AddExecutor(ExecutorType.SpellSet, CardId.魔术的幕帘);
            AddExecutor(ExecutorType.SpellSet, CardId.宝石);              
            AddExecutor(ExecutorType.SpellSet, RushCardId.黑洞);
            AddExecutor(ExecutorType.SpellSet, CardId.DMA);

            AddExecutor(ExecutorType.Summon, CardId.DMG, DMGsummon);
            AddExecutor(ExecutorType.Activate, CardId.宝石, 宝石Effect2);

            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);          
            
            AddExecutor(ExecutorType.Repos, CardId.黑曜, 反转Repos);
            AddExecutor(ExecutorType.Repos, RushCardId.黑魔术师, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.遂行者, 反转Repos);
            AddExecutor(ExecutorType.Repos, RushCardId.黑魔术师, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.遂行者, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.DMG, 圣防Repos);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);

            //AddExecutor(ExecutorType.SummonOrSet, ImFeelingLazy);
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

        private static readonly int[] over3 = new[] {
                RushCardId.黑洞, CardId.DMA, CardId.宝石, CardId.二重加速, CardId.DMB, CardId.魔力抽出, CardId.幽灵旋风, CardId.水流, CardId.魔导书弃却, CardId.天惠枪, CardId.魔术的幕帘, CardId.七宝船, CardId.采掘, CardId.三猫};

        private static readonly int[] target = new[] {
                CardId.三猫, CardId.宝石, RushCardId.黑洞, CardId.DMB, CardId.魔导书弃却, CardId.幽灵旋风,CardId.魔术的幕帘,  CardId.水流,CardId.天惠枪, CardId.二重加速, CardId.七宝船, CardId.魔力抽出, CardId.DMA, CardId.采掘};

        private static readonly int[] cost2 = new[] {

                CardId.三猫, CardId.幽灵旋风,CardId.水流, CardId.DMB, CardId.二重加速, CardId.采掘,CardId.魔术的幕帘, CardId.七宝船, CardId.魔导书弃却, CardId.天惠枪, CardId.魔力抽出, CardId.宝石,CardId.DMA, RushCardId.黑洞 };

        private static readonly int[] mon = new[] {

                RushCardId.黑魔术师, CardId.遂行者, CardId.黑曜, CardId.剑黎};

        private static readonly int[] mon2 = new[] {
                CardId.黑曜,  CardId.遂行者, CardId.剑黎 ,RushCardId.黑魔术师};

        private static readonly int[] mon3 = new[] {
                CardId.黑曜,  CardId.遂行者, CardId.剑黎 ,RushCardId.黑魔术师 ,CardId.DMG};

        private bool speset()
        {
            if (Bot.GetSpellCountWithoutField() <= 1)
                return true;
            return false;
        }
        private bool 抽出set()
        {
            if (Bot.HasInSpellZone(CardId.魔力抽出) && Bot.GetSpellCountWithoutField() < 3)
                return true;
            if (Bot.HasInHand(CardId.魔力抽出) && Bot.GetSpellCountWithoutField() < 2)
                return true;
            return false;
        }
        private bool 幕帘set()
        {
            if (Bot.LifePoints > 1000 && Bot.GetSpellCountWithoutField() <= 1)
                return true;
            return false;
        }

        private bool 抽出sp()
        {
            if (Bot.HasInHand(CardId.魔力抽出) && Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) == 1 )       
                return false;
            return true;
        }
        private bool DMGsummon()
        {
            int tributecount = 1;
            IList<int> ssmon = new[] {
                CardId.DMG, RushCardId.黑魔术师, CardId.剑黎, CardId.遂行者};
            if ((Bot.GetHandCount() <= 4) && (Bot.HasInMonstersZone(ssmon)) && (Bot.HasInMonstersZoneOrInGraveyard(RushCardId.黑魔术师)))
            for (int j = 0; j < 7; ++j)
            {
                ClientCard tributeCard = Bot.MonsterZone[j];
                if (tributeCard == null) continue;
                if (tributeCard.IsCode(ssmon))
                    tributecount--;
            }
            return tributecount <= 0;
        }

        public override bool OnSelectYesNo(int desc)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.遂行者)
                && desc == Util.GetStringId(CardId.遂行者, 1))
            {
                int spells = Bot.Graveyard.Count(c => c.IsSpell());
                return spells >= 10 && (!Bot.HasInMonstersZone(CardId.黑曜)
                    || Util.GetBestAttack(Bot) - Util.GetBestAttack(Enemy) >= (spells >= 15 ? 1000 : 1500));
            }
            return base.OnSelectYesNo(desc);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.遂行者))
                {
                    if (hint == HintMsg.ToDeck)
                    {
                        int[] order = Bot.Graveyard.Count(c => c.IsMonster()) > 3 ? over3 : target;
                        return new CardSelector(order).Select(cards, min, max);
                    }
                    if (hint == HintMsg.Destroy)
                        return cards.OrderByDescending(c => c.Level).ThenByDescending(c => c.Attack).Take(max).ToList();
                }
                if (link.IsActivateCode(CardId.三猫) && hint == HintMsg.AddToHand)
                {
                    int preferred = CatRecovery(cards);
                    return preferred == 0 ? new CardSelector(target).Select(cards, min, max)
                        : new CardSelector(preferred).Select(cards, min, max);
                }
                if (link.IsActivateCode(CardId.魔力抽出) && hint == HintMsg.Confirm)
                    return new CardSelector(mon3).Select(cards, min, max);
                if (link.IsActivateCode(CardId.宝石) && hint == HintMsg.SpSummon)
                {
                    var choices = new CardSelector(mon3).Select(cards, 0, cards.Count);
                    return choices.Concat(cards).GroupBy(c => c.Attack).Select(g => g.First()).Take(max).ToList();
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private int CatRecovery(IList<ClientCard> cards)
        {
            if (Bot.Graveyard.Count(c => c.IsMonster()) < 2)
                return 0;
            if (Bot.Hand.Count(c => c.Level >= 7) >= 2 && cards.Any(c => c.IsCode(CardId.七宝船)))
                return CardId.七宝船;
            bool needsMonster = !Bot.Hand.Any(c => c.Level >= 6);
            if ((needsMonster || (Bot.GetMonsterCount() == 0 && !Bot.HasInHandOrInSpellZone(CardId.宝石)))
                && cards.Any(c => c.IsCode(CardId.宝石)))
                return CardId.宝石;
            if (Bot.GetMonsterCount() == 0 && Bot.HasInHand(mon3) && !Bot.HasInHandOrInSpellZoneOrInGraveyard(CardId.宝石)
                && cards.Any(c => c.IsCode(CardId.魔术的幕帘)))
                return CardId.魔术的幕帘;
            if (Enemy.GetSpellCount() > 2 && (Bot.HasInMonstersZone(RushCardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者))
                && cards.Any(c => c.IsCode(CardId.DMA)))
                return CardId.DMA;
            if (!Enemy.HasAttackingMonster() && (Bot.HasInMonstersZone(CardId.黑曜) || Bot.HasInHandOrInSpellZone(CardId.宝石))
                && cards.Any(c => c.IsCode(CardId.天惠枪)))
                return CardId.天惠枪;
            return needsMonster && cards.Any(c => c.IsCode(CardId.剑黎)) ? CardId.剑黎 : 0;
        }

        private bool DMAEffect()
        {
            if (Enemy.GetSpellCount() >= 2 
                && (Bot.HasInMonstersZone(RushCardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者)))
                return true;
            if (Enemy.GetSpellCount() == 1 
                && (Bot.HasInMonstersZone(RushCardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者))
                && Bot.MonsterZone.GetMatchingCardsCount(card => card.IsAttack() && card.Attack >= Enemy.LifePoints) >= 2)
                return true;
            return false;
        }

        private bool 七宝船Effect()
        {
            if (!Bot.HasInHand(CardId.魔力抽出) && Bot.HasInHand(mon))
            {
                AI.SelectCard(mon);
                AI.SelectYesNo(true);
                return true;
            }
            return false;
        }
        private bool 七宝船Effect2()
        {
            if (Bot.HasInHand(mon))
            {
                AI.SelectCard(mon);
                AI.SelectYesNo(true);
                return true;
            }
            else if (Bot.HasInHand(CardId.DMG) && Bot.GetSpellCount() >= 2 && Bot.GetHandCount() >=4)
            {
                AI.SelectCard(CardId.DMG);
                return true;
            }
            return false;
        }
        private bool 魔力抽出Effect()
        {
            // The second draw reveals a monster from the hand as it exists after the first draw.
            AI.SelectCard(cost2);
            return true;
        }

        private bool 幕帘Effect()
        {
            if (Bot.LifePoints > 1000)
            {
                if (Bot.HasInHandOrInSpellZone(CardId.DMA) && (Bot.HasInHand(CardId.遂行者) || Bot.HasInHand(RushCardId.黑魔术师)) && Enemy.GetSpellCount() >= 2)
                {
                    AI.SelectCard(CardId.遂行者, RushCardId.黑魔术师);
                    return true;
                }
                if (Bot.HasInHand(CardId.黑曜) && Bot.GetGraveyardSpells().Count >= 5)
                {
                    AI.SelectCard(CardId.黑曜);
                    return true;
                }
                if (Bot.HasInHand(CardId.遂行者))
                {
                    AI.SelectCard(CardId.遂行者);
                    return true;
                }
                if (Bot.HasInHand(mon3)) AI.SelectCard(mon3);
                return true;
            }
            return false;
        }

        private bool 天惠枪Effect()
        {
            if (Bot.HasInMonstersZone(CardId.黑曜))
            {
                AI.SelectCard(CardId.黑曜);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.遂行者))
            {
                AI.SelectCard(CardId.遂行者);
                return true;
            }
            if (Bot.HasInMonstersZone(RushCardId.黑魔术师))
            {
                AI.SelectCard(RushCardId.黑魔术师);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.剑黎))
            {
                AI.SelectCard(CardId.剑黎);
                return true;
            }
            return true;
        }
        private bool 二重加速Effect()
        {
            if (Duel.Turn == 1)
                return false;
            if (!Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster() && Enemy.GetMonsterCount() >= Bot.GetMonsterCount())
                return false;
            AI.SelectCard(CardLocation.Grave);
            AI.SelectNextCard(mon3);
            return true;
        }
        private bool DMBEffect()
        {
            if (Duel.Turn == 1)
                return false;
            if (!Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster() && Enemy.GetMonsterCount() >= Bot.GetMonsterCount())
                return false;
            else
                AI.SelectCard(mon3);
            return true;
        }
        private bool 三猫Effect()
        {
            return Bot.Graveyard.Count(c => c.IsMonster()) <= 3
                || (!Bot.HasInHand(CardId.魔导书弃却) && Bot.HasInMonstersZone(CardId.黑曜))
                || Bot.GetHandCount() >= 5;
        }

        private bool 采掘Effect()
        {
            if (HasStrongerFaceupEnemy() && Util.GetTotalAttackingMonsterAttack(1) >= 3500 && !Enemy.HasInMonstersZone(RushCardId.终焰魔神, faceUp: true) && Bot.HasInGraveyard(RushCardId.黑洞))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(RushCardId.黑洞);
                return true;
            }
            if (Enemy.GetMonsterCount() >0 && Bot.GetGraveyardMonsters().Count < 3 && Bot.HasInGraveyard(CardId.三猫) )
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.三猫);
                return true;
            }
            if (Bot.GetMonsterCount() <= 1 && Bot.HasInGraveyard(CardId.宝石) && (Bot.HasInHandOrInGraveyard(CardId.DMG) && Bot.HasInHandOrInGraveyard(mon2)))
            {
                AI.SelectCard(cost2);
             AI.SelectNextCard(CardId.宝石);
             return true;
            }
            if (Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) >= 3 && Bot.HasInGraveyard(CardId.七宝船))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.七宝船);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.黑曜) && Bot.HasInGraveyard(CardId.天惠枪) && !Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster())
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.天惠枪);
                return true;
            }

            return false;
        }
        private bool 采掘Effect2()
        {
            if (Enemy.GetSpellCount() >= 2 && (Bot.HasInMonstersZone(RushCardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者)) && Bot.HasInGraveyard(CardId.DMA))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.DMA);
                return true;
            }
            if (Bot.Deck.Count >= 10 && Bot.HasInGraveyard(CardId.魔导书弃却))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.魔导书弃却);
                return true;
            }

            if (Duel.Turn != 1 && Bot.HasInGraveyard(CardId.二重加速) && Bot.HasAttackingMonster() && Bot.Graveyard.GetMatchingCardsCount(card => card.Level > 5) >= 2)
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.二重加速);
                return true;
            }
            return false;
        }
        private bool 宝石Effect()
        {
            return Bot.GetMonsterCount() <= 1;
        }

        private bool 宝石Effect2()
        {
            return Bot.GetMonsterCount() == 2;
        }

    }
}





