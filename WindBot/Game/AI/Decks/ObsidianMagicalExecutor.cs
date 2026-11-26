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
            public const int DarkEradicatorSorcerer = 120254035;
            public const int DarkSwordMagician = 120246080;
            public const int ObsidianMagicalSoldier = 120246081;
            public const int DarkMagicianGirl = 120181002;
            public const int DarkMagicAttack = 120130041;
            public const int DarkResonanceBurst = 120203027;
            public const int MagicalStoneExcavation = 120203029;
            public const int MagicCurtain = 120217018;
            public const int HighMagicDoubleAcceleration = 120246018;
            public const int StoneOfTheDarkMagicians = 120246083;
            public const int SpellbookRejection = 120246084;
            public const int SpellExtraction = 120246085;
            public const int ShipOfSevenTreasures = 120199052;
            public const int CatTroisChoice = 120249055;
            public const int MagicalLanceGraceSpear = 120244055;
            public const int MagicalStream = 120105012;
            public const int GhostCyclone = 120208053;

        }

        public ObsidianMagicalExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.MagicCylinder, ActivateMagicCylinder);
            AddExecutor(ExecutorType.Activate, CardId.DarkMagicAttack, ActivateDarkMagicAttackWithSpellcaster);
            AddExecutor(ExecutorType.Activate, CardId.GhostCyclone);
            AddExecutor(ExecutorType.Activate, CardId.MagicalStream);
            AddExecutor(ExecutorType.Activate, CardId.CatTroisChoice, ActivateCatTroisChoice);
            AddExecutor(ExecutorType.Activate, RushCardId.DarkHole, ActivateDarkHole);

            AddExecutor(ExecutorType.Activate, CardId.SpellbookRejection);
            AddExecutor(ExecutorType.Activate, CardId.ShipOfSevenTreasures, ActivateShipOfSevenTreasures);
            AddExecutor(ExecutorType.Activate, CardId.DarkEradicatorSorcerer);
            AddExecutor(ExecutorType.Activate, CardId.MagicalStoneExcavation, ActivateMagicalStoneExcavation);

            AddExecutor(ExecutorType.SpellSet, CardId.SpellbookRejection, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.CatTroisChoice, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.SpellExtraction, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.GhostCyclone, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicalStream, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicalLanceGraceSpear, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.DarkResonanceBurst, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicCurtain, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.StoneOfTheDarkMagicians, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicalStoneExcavation, SetSpellExtraction);
            AddExecutor(ExecutorType.SpellSet, CardId.DarkMagicAttack, SetSpellExtraction);
            AddExecutor(ExecutorType.Activate, CardId.SpellExtraction, ActivateSpellExtraction);

            AddExecutor(ExecutorType.SpSummon, CardId.DarkSwordMagician, SpecialSummonForSpellExtraction);
            AddExecutor(ExecutorType.Activate, CardId.StoneOfTheDarkMagicians, ActivateStoneOfTheDarkMagicians);
            AddExecutor(ExecutorType.Activate, CardId.MagicalStoneExcavation, RecoverUtilitySpellWithMagicalStoneExcavation);
            AddExecutor(ExecutorType.Activate, CardId.ShipOfSevenTreasures, UseAlternativeSevenTreasuresDiscard);
            AddExecutor(ExecutorType.Activate, CardId.MagicCurtain, ActivateMagicCurtain);
            AddExecutor(ExecutorType.SpellSet, CardId.ShipOfSevenTreasures, SetSpellExtraction);
            AddExecutor(ExecutorType.Activate, CardId.HighMagicDoubleAcceleration, ActivateHighMagicDoubleAcceleration);
            AddExecutor(ExecutorType.Activate, CardId.DarkResonanceBurst, ActivateDarkResonanceBurst);
            AddExecutor(ExecutorType.Activate, CardId.MagicalLanceGraceSpear, ActivateMagicalLanceGraceSpear);

            AddExecutor(ExecutorType.SpellSet, CardId.SpellExtraction, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicCurtain, SetMagicCurtain);
            AddExecutor(ExecutorType.SpellSet, CardId.ShipOfSevenTreasures, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicalStoneExcavation, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.GhostCyclone, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.StoneOfTheDarkMagicians, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.DarkResonanceBurst, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.HighMagicDoubleAcceleration, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicalStream, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicalLanceGraceSpear, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.CatTroisChoice, speset);
            AddExecutor(ExecutorType.SpellSet, RushCardId.MagicCylinder);
            AddExecutor(ExecutorType.SpellSet, CardId.MagicCurtain);
            AddExecutor(ExecutorType.SpellSet, CardId.StoneOfTheDarkMagicians);
            AddExecutor(ExecutorType.SpellSet, RushCardId.DarkHole);
            AddExecutor(ExecutorType.SpellSet, CardId.DarkMagicAttack);

            AddExecutor(ExecutorType.Summon, CardId.DarkMagicianGirl, SummonDarkMagicianGirl);
            AddExecutor(ExecutorType.Activate, CardId.StoneOfTheDarkMagicians, UseStoneWithOneFreeZone);

            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);

            AddExecutor(ExecutorType.Repos, CardId.ObsidianMagicalSoldier, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, RushCardId.DarkMagician, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.DarkEradicatorSorcerer, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, RushCardId.DarkMagician, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.DarkEradicatorSorcerer, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.DarkMagicianGirl, RepositionAgainstBattleTraps);
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
                RushCardId.DarkHole, CardId.DarkMagicAttack, CardId.StoneOfTheDarkMagicians, CardId.HighMagicDoubleAcceleration, CardId.DarkResonanceBurst, CardId.SpellExtraction, CardId.GhostCyclone, CardId.MagicalStream, CardId.SpellbookRejection, CardId.MagicalLanceGraceSpear, CardId.MagicCurtain, CardId.ShipOfSevenTreasures, CardId.MagicalStoneExcavation, CardId.CatTroisChoice};

        private static readonly int[] target = new[] {
                CardId.CatTroisChoice, CardId.StoneOfTheDarkMagicians, RushCardId.DarkHole, CardId.DarkResonanceBurst, CardId.SpellbookRejection, CardId.GhostCyclone,CardId.MagicCurtain,  CardId.MagicalStream,CardId.MagicalLanceGraceSpear, CardId.HighMagicDoubleAcceleration, CardId.ShipOfSevenTreasures, CardId.SpellExtraction, CardId.DarkMagicAttack, CardId.MagicalStoneExcavation};

        private static readonly int[] cost2 = new[] {

                CardId.CatTroisChoice, CardId.GhostCyclone,CardId.MagicalStream, CardId.DarkResonanceBurst, CardId.HighMagicDoubleAcceleration, CardId.MagicalStoneExcavation,CardId.MagicCurtain, CardId.ShipOfSevenTreasures, CardId.SpellbookRejection, CardId.MagicalLanceGraceSpear, CardId.SpellExtraction, CardId.StoneOfTheDarkMagicians,CardId.DarkMagicAttack, RushCardId.DarkHole };

        private static readonly int[] mon = new[] {

                RushCardId.DarkMagician, CardId.DarkEradicatorSorcerer, CardId.ObsidianMagicalSoldier, CardId.DarkSwordMagician};

        private static readonly int[] mon2 = new[] {
                CardId.ObsidianMagicalSoldier,  CardId.DarkEradicatorSorcerer, CardId.DarkSwordMagician ,RushCardId.DarkMagician};

        private static readonly int[] mon3 = new[] {
                CardId.ObsidianMagicalSoldier,  CardId.DarkEradicatorSorcerer, CardId.DarkSwordMagician ,RushCardId.DarkMagician ,CardId.DarkMagicianGirl};

        private bool speset()
        {
            if (Bot.GetSpellCountWithoutField() <= 1)
                return true;
            return false;
        }
        private bool SetSpellExtraction()
        {
            if (Bot.HasInSpellZone(CardId.SpellExtraction) && Bot.GetSpellCountWithoutField() < 3)
                return true;
            if (Bot.HasInHand(CardId.SpellExtraction) && Bot.GetSpellCountWithoutField() < 2)
                return true;
            return false;
        }
        private bool SetMagicCurtain()
        {
            if (Bot.LifePoints > 1000 && Bot.GetSpellCountWithoutField() <= 1)
                return true;
            return false;
        }

        private bool SpecialSummonForSpellExtraction()
        {
            if (Bot.HasInHand(CardId.SpellExtraction) && Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) == 1 )
                return false;
            return true;
        }
        private bool SummonDarkMagicianGirl()
        {
            int tributecount = 1;
            IList<int> ssmon = new[] {
                CardId.DarkMagicianGirl, RushCardId.DarkMagician, CardId.DarkSwordMagician, CardId.DarkEradicatorSorcerer};
            if ((Bot.GetHandCount() <= 4) && (Bot.HasInMonstersZone(ssmon)) && (Bot.HasInMonstersZoneOrInGraveyard(RushCardId.DarkMagician)))
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
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.DarkEradicatorSorcerer)
                && desc == Util.GetStringId(CardId.DarkEradicatorSorcerer, 1))
            {
                int spells = Bot.Graveyard.Count(c => c.IsSpell());
                return spells >= 10 && (!Bot.HasInMonstersZone(CardId.ObsidianMagicalSoldier)
                    || Util.GetBestAttack(Bot) - Util.GetBestAttack(Enemy) >= (spells >= 15 ? 1000 : 1500));
            }
            return base.OnSelectYesNo(desc);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.DarkEradicatorSorcerer))
                {
                    if (hint == HintMsg.ToDeck)
                    {
                        int[] order = Bot.Graveyard.Count(c => c.IsMonster()) > 3 ? over3 : target;
                        return new CardSelector(order).Select(cards, min, max);
                    }
                    if (hint == HintMsg.Destroy)
                        return cards.OrderByDescending(c => c.Level).ThenByDescending(c => c.Attack).Take(max).ToList();
                }
                if (link.IsActivateCode(CardId.CatTroisChoice) && hint == HintMsg.AddToHand)
                {
                    int preferred = CatRecovery(cards);
                    return preferred == 0 ? new CardSelector(target).Select(cards, min, max)
                        : new CardSelector(preferred).Select(cards, min, max);
                }
                if (link.IsActivateCode(CardId.SpellExtraction) && hint == HintMsg.Confirm)
                    return new CardSelector(mon3).Select(cards, min, max);
                if (link.IsActivateCode(CardId.StoneOfTheDarkMagicians) && hint == HintMsg.SpSummon)
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
            if (Bot.Hand.Count(c => c.Level >= 7) >= 2 && cards.Any(c => c.IsCode(CardId.ShipOfSevenTreasures)))
                return CardId.ShipOfSevenTreasures;
            bool needsMonster = !Bot.Hand.Any(c => c.Level >= 6);
            if ((needsMonster || (Bot.GetMonsterCount() == 0 && !Bot.HasInHandOrInSpellZone(CardId.StoneOfTheDarkMagicians)))
                && cards.Any(c => c.IsCode(CardId.StoneOfTheDarkMagicians)))
                return CardId.StoneOfTheDarkMagicians;
            if (Bot.GetMonsterCount() == 0 && Bot.HasInHand(mon3) && !Bot.HasInHandOrInSpellZoneOrInGraveyard(CardId.StoneOfTheDarkMagicians)
                && cards.Any(c => c.IsCode(CardId.MagicCurtain)))
                return CardId.MagicCurtain;
            if (Enemy.GetSpellCount() > 2 && (Bot.HasInMonstersZone(RushCardId.DarkMagician) || Bot.HasInMonstersZone(CardId.DarkEradicatorSorcerer))
                && cards.Any(c => c.IsCode(CardId.DarkMagicAttack)))
                return CardId.DarkMagicAttack;
            if (!Enemy.HasAttackingMonster() && (Bot.HasInMonstersZone(CardId.ObsidianMagicalSoldier) || Bot.HasInHandOrInSpellZone(CardId.StoneOfTheDarkMagicians))
                && cards.Any(c => c.IsCode(CardId.MagicalLanceGraceSpear)))
                return CardId.MagicalLanceGraceSpear;
            return needsMonster && cards.Any(c => c.IsCode(CardId.DarkSwordMagician)) ? CardId.DarkSwordMagician : 0;
        }

        private bool ActivateDarkMagicAttackWithSpellcaster()
        {
            if (Enemy.GetSpellCount() >= 2
                && (Bot.HasInMonstersZone(RushCardId.DarkMagician) || Bot.HasInMonstersZone(CardId.DarkEradicatorSorcerer)))
                return true;
            if (Enemy.GetSpellCount() == 1
                && (Bot.HasInMonstersZone(RushCardId.DarkMagician) || Bot.HasInMonstersZone(CardId.DarkEradicatorSorcerer))
                && Bot.MonsterZone.GetMatchingCardsCount(card => card.IsAttack() && card.Attack >= Enemy.LifePoints) >= 2)
                return true;
            return false;
        }

        private bool ActivateShipOfSevenTreasures()
        {
            if (!Bot.HasInHand(CardId.SpellExtraction) && Bot.HasInHand(mon))
            {
                AI.SelectCard(mon);
                AI.SelectYesNo(true);
                return true;
            }
            return false;
        }
        private bool UseAlternativeSevenTreasuresDiscard()
        {
            if (Bot.HasInHand(mon))
            {
                AI.SelectCard(mon);
                AI.SelectYesNo(true);
                return true;
            }
            else if (Bot.HasInHand(CardId.DarkMagicianGirl) && Bot.GetSpellCount() >= 2 && Bot.GetHandCount() >=4)
            {
                AI.SelectCard(CardId.DarkMagicianGirl);
                return true;
            }
            return false;
        }
        private bool ActivateSpellExtraction()
        {
            // The second draw reveals a monster from the hand as it exists after the first draw.
            AI.SelectCard(cost2);
            return true;
        }

        private bool ActivateMagicCurtain()
        {
            if (Bot.LifePoints > 1000)
            {
                if (Bot.HasInHandOrInSpellZone(CardId.DarkMagicAttack) && (Bot.HasInHand(CardId.DarkEradicatorSorcerer) || Bot.HasInHand(RushCardId.DarkMagician)) && Enemy.GetSpellCount() >= 2)
                {
                    AI.SelectCard(CardId.DarkEradicatorSorcerer, RushCardId.DarkMagician);
                    return true;
                }
                if (Bot.HasInHand(CardId.ObsidianMagicalSoldier) && Bot.GetGraveyardSpells().Count >= 5)
                {
                    AI.SelectCard(CardId.ObsidianMagicalSoldier);
                    return true;
                }
                if (Bot.HasInHand(CardId.DarkEradicatorSorcerer))
                {
                    AI.SelectCard(CardId.DarkEradicatorSorcerer);
                    return true;
                }
                if (Bot.HasInHand(mon3)) AI.SelectCard(mon3);
                return true;
            }
            return false;
        }

        private bool ActivateMagicalLanceGraceSpear()
        {
            if (Bot.HasInMonstersZone(CardId.ObsidianMagicalSoldier))
            {
                AI.SelectCard(CardId.ObsidianMagicalSoldier);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.DarkEradicatorSorcerer))
            {
                AI.SelectCard(CardId.DarkEradicatorSorcerer);
                return true;
            }
            if (Bot.HasInMonstersZone(RushCardId.DarkMagician))
            {
                AI.SelectCard(RushCardId.DarkMagician);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.DarkSwordMagician))
            {
                AI.SelectCard(CardId.DarkSwordMagician);
                return true;
            }
            return true;
        }
        private bool ActivateHighMagicDoubleAcceleration()
        {
            if (Duel.Turn == 1)
                return false;
            if (!Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster() && Enemy.GetMonsterCount() >= Bot.GetMonsterCount())
                return false;
            AI.SelectCard(CardLocation.Grave);
            AI.SelectNextCard(mon3);
            return true;
        }
        private bool ActivateDarkResonanceBurst()
        {
            if (Duel.Turn == 1)
                return false;
            if (!Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster() && Enemy.GetMonsterCount() >= Bot.GetMonsterCount())
                return false;
            else
                AI.SelectCard(mon3);
            return true;
        }
        private bool ActivateCatTroisChoice()
        {
            return Bot.Graveyard.Count(c => c.IsMonster()) <= 3
                || (!Bot.HasInHand(CardId.SpellbookRejection) && Bot.HasInMonstersZone(CardId.ObsidianMagicalSoldier))
                || Bot.GetHandCount() >= 5;
        }

        private bool ActivateMagicalStoneExcavation()
        {
            if (HasStrongerFaceupEnemy() && Util.GetTotalAttackingMonsterAttack(1) >= 3500 && !Enemy.HasInMonstersZone(RushCardId.DoomblazeFiendOverlordDespairacion, faceUp: true) && Bot.HasInGraveyard(RushCardId.DarkHole))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(RushCardId.DarkHole);
                return true;
            }
            if (Enemy.GetMonsterCount() >0 && Bot.GetGraveyardMonsters().Count < 3 && Bot.HasInGraveyard(CardId.CatTroisChoice) )
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.CatTroisChoice);
                return true;
            }
            if (Bot.GetMonsterCount() <= 1 && Bot.HasInGraveyard(CardId.StoneOfTheDarkMagicians) && (Bot.HasInHandOrInGraveyard(CardId.DarkMagicianGirl) && Bot.HasInHandOrInGraveyard(mon2)))
            {
                AI.SelectCard(cost2);
             AI.SelectNextCard(CardId.StoneOfTheDarkMagicians);
             return true;
            }
            if (Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) >= 3 && Bot.HasInGraveyard(CardId.ShipOfSevenTreasures))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.ShipOfSevenTreasures);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.ObsidianMagicalSoldier) && Bot.HasInGraveyard(CardId.MagicalLanceGraceSpear) && !Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster())
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.MagicalLanceGraceSpear);
                return true;
            }

            return false;
        }
        private bool RecoverUtilitySpellWithMagicalStoneExcavation()
        {
            if (Enemy.GetSpellCount() >= 2 && (Bot.HasInMonstersZone(RushCardId.DarkMagician) || Bot.HasInMonstersZone(CardId.DarkEradicatorSorcerer)) && Bot.HasInGraveyard(CardId.DarkMagicAttack))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.DarkMagicAttack);
                return true;
            }
            if (Bot.Deck.Count >= 10 && Bot.HasInGraveyard(CardId.SpellbookRejection))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.SpellbookRejection);
                return true;
            }

            if (Duel.Turn != 1 && Bot.HasInGraveyard(CardId.HighMagicDoubleAcceleration) && Bot.HasAttackingMonster() && Bot.Graveyard.GetMatchingCardsCount(card => card.Level > 5) >= 2)
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.HighMagicDoubleAcceleration);
                return true;
            }
            return false;
        }
        private bool ActivateStoneOfTheDarkMagicians()
        {
            return Bot.GetMonsterCount() <= 1;
        }

        private bool UseStoneWithOneFreeZone()
        {
            return Bot.GetMonsterCount() == 2;
        }

    }
}





