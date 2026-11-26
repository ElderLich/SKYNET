using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("Mituzi", "AI_Mituzi")]
    public class MituziExecutor : RushExecutor
    {
        public class CardId
        {
            public const int PhantasmSpiralDragon = 120247063;
            public const int ConstructorSkyWyrmGantryDragon = 120199020;
            public const int WyrmExcavatorTheHeavyCavalryDraco = 120155022;
            public const int ConstructorWyrmBuildragon = 120155024;
            public const int ConstructorDemolisherWyrmDrakeCrush = 120151029;
            public const int ConstructorWarriorShovelon = 120155002;
            public const int ConstructorFighterMixer = 120192010;
            public const int CoilDragonOfDesolation = 120192011;
            public const int Rindrmuhle = 120183034;
            public const int CoilDragonOfTheFertileLand = 120170037;
            public const int DestructorSoldierSteeplejack = 120247046;
            public const int PicklonTheConstructorFairy = 120155025;
            public const int Pierce = 120120037;
            public const int TalismanicSealArray = 120183053;
            public const int MonsterReborn = 120194004;
            public const int PeaksOfBlisstopia = 120155042;
            public const int PrimalHowling = 120155044;
            public const int ConstructorResurrection = 120151033;
            public const int ConstructorAmbush = 120155053;
            public const int ConstructorBlockade = 120155054;
            public const int DestructorArtTotalDestruction = 120247058;
            public const int DarkRevelation = 120105013;
        }


        private static readonly int[] priList = { CardId.ConstructorSkyWyrmGantryDragon, CardId.ConstructorWyrmBuildragon, CardId.WyrmExcavatorTheHeavyCavalryDraco,  CardId.PhantasmSpiralDragon, CardId.ConstructorDemolisherWyrmDrakeCrush };
        private static readonly int[] rePriList = { CardId.ConstructorDemolisherWyrmDrakeCrush, CardId.PhantasmSpiralDragon, CardId.WyrmExcavatorTheHeavyCavalryDraco, CardId.ConstructorWyrmBuildragon, CardId.ConstructorSkyWyrmGantryDragon, };
        public MituziExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.DarkHole, ActivateDarkHole);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, RushCardId.MagicCylinder, ActivateMagicCylinder);
            AddExecutor(ExecutorType.Activate, RushCardId.SakuretsuArmor, ActivateSakuretsuArmor);
            AddExecutor(ExecutorType.Activate, RushCardId.TrapHole, ActivateTrapHole);
            AddExecutor(ExecutorType.Activate, RushCardId.TorrentialTribute, ActivateTorrentialTribute);
            AddExecutor(ExecutorType.Activate, RushCardId.MirrorForce, ActivateMirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.DestructorArtTotalDestruction, ActivateDestructorArtTotalDestruction);
            AddExecutor(ExecutorType.Activate, CardId.TalismanicSealArray, ActivateTalismanicSealArray);
            AddExecutor(ExecutorType.SpellSet, CardId.TalismanicSealArray);
            AddExecutor(ExecutorType.Activate, CardId.PeaksOfBlisstopia,DefaultField);
            AddExecutor(ExecutorType.Repos, RepositionConstructorAmbush);
            AddExecutor(ExecutorType.Activate, CardId.PrimalHowling);
            AddExecutor(ExecutorType.Activate, CardId.CoilDragonOfTheFertileLand,ActivateCoilDragonOfTheFertileLand);
            AddExecutor(ExecutorType.Activate, CardId.CoilDragonOfDesolation, ActivateCoilDragonOfDesolation);
            AddExecutor(ExecutorType.Activate, CardId.ConstructorFighterMixer, ActivateConstructorFighterMixer);
            AddExecutor(ExecutorType.Activate, CardId.Rindrmuhle);
            AddExecutor(ExecutorType.Activate, CardId.ConstructorSkyWyrmGantryDragon);
            AddExecutor(ExecutorType.Activate, CardId.ConstructorWyrmBuildragon, ActivateConstructorWyrmBuildragon);
            AddExecutor(ExecutorType.Activate, CardId.PicklonTheConstructorFairy, ActivatePicklonTheConstructorFairy);
            AddExecutor(ExecutorType.Summon, CardId.PhantasmSpiralDragon, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, RushCardId.GreatMajuGarzett, SummonGreatMajuGarzettWithWyrm);
            AddExecutor(ExecutorType.Summon, SummonConstructorAmbush);

            AddExecutor(ExecutorType.Summon, CardId.PicklonTheConstructorFairy, SummonPicklonTheConstructorFairy);
            AddExecutor(ExecutorType.Summon, CardId.ConstructorFighterMixer, SummonConstructorFighterMixer);
            AddExecutor(ExecutorType.MonsterSet, SetConstructorAmbush);
            AddExecutor(ExecutorType.Activate, CardId.MonsterReborn, ActivateMonsterReborn);
            AddExecutor(ExecutorType.Activate, CardId.Pierce, ActivatePierce);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.ConstructorResurrection, ActivateConstructorResurrection);
            AddExecutor(ExecutorType.Activate, CardId.ConstructorBlockade, ActivateConstructorBlockade);
            AddExecutor(ExecutorType.Activate, CardId.ConstructorAmbush, ActivateConstructorAmbush);
            AddExecutor(ExecutorType.Activate, CardId.PeaksOfBlisstopia);
            AddExecutor(ExecutorType.Activate, RushCardId.DestroyerOfDragonSorcerers, ActivateDestroyerOfDragonSorcerers);
            AddExecutor(ExecutorType.Activate, RushCardId.PerionTheStarKnight, ActivatePerionTheStarKnight);
            AddExecutor(ExecutorType.Activate, RushCardId.AgarielTheFinestSushiFairy, ActivateAgarielTheFinestSushiFairy);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.PhantasmSpiralDragon,FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.ConstructorSkyWyrmGantryDragon,FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.ConstructorWyrmBuildragon, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.SpellSet, TrapCardSet);
            AddExecutor(ExecutorType.SpellSet);
        }

         private bool TrapCardSet()
         {
             if (Card.HasType(CardType.Trap)) return true;
             return false;
         }
         private bool MonsRepos()
         {
             if (Card.Id == CardId.PicklonTheConstructorFairy)
             {
                 if (Card.IsAttack()) return true;
                 if (Card.IsFacedown())
                     return Discord1Draw1Eff(CardRace.Wyrm, false);

                 return false;
             }
             if (Card.Id == CardId.Rindrmuhle)
             {
                 if (Card.IsFacedown())
                 {
                     ClientCard card = WeakestFaceupEnemy();
                     if (card == null) return true;
                     if (Bot.HasInSpellZone(CardId.PeaksOfBlisstopia))
                     {
                         if (Card.Attack + 400 >= (card.IsAttack() ? card.Attack : card.Defense))
                             return true;
                         else return false;
                     }
                     else
                     {
                         if (Card.Attack >= (card.IsAttack() ? card.Attack : card.Defense))
                             return true;
                         else return false;
                     }
                 }
             }
             if (Card.Id == CardId.CoilDragonOfTheFertileLand)
             {
                 if (Card.IsFacedown())
                     return Bot.GetMonsterCount()>1&&
                         Bot.Hand.GetMatchingCardsCount(card => card.Level > 4) > 0;
             }
             ClientCard emMon = WeakestFaceupEnemy();
             if (emMon == null) return Card.IsDefense();
             else
             {
                 if (Card.Attack >= emMon.GetDefensePower()) return Card.IsDefense();
                 else return Card.IsAttack();
             }

         }

         private bool SummonPicklonTheConstructorFairy()
         {
             if (Bot.HasInMonstersZone(CardId.ConstructorDemolisherWyrmDrakeCrush) || (Bot.HasInMonstersZone(CardId.PhantasmSpiralDragon)) && Bot.HasInHand(CardId.ConstructorWyrmBuildragon) && Bot.HasInHand(CardId.ConstructorSkyWyrmGantryDragon))
                 return Discord1Draw1Eff(CardRace.Wyrm, false, true);
             return Discord1Draw1Eff(CardRace.Wyrm, false, false);
         }
         private bool SummonRindrmuhle()
         {
             ClientCard card = WeakestFaceupEnemy();
             if (card == null) return true;
             if (Bot.HasInSpellZone(CardId.PeaksOfBlisstopia))
             {
                 if (Card.Attack + 400 >= (card.IsAttack() ? card.Attack : card.Defense))
                     return true;
                 else return false;
             }
             else
             {
                 if (Card.Attack >= (card.IsAttack() ? card.Attack : card.Defense))
                     return true;
                 else return false;
             }

         }
         private bool SummonConstructorFighterMixer()
         {
             return HasFaceupFieldSpell();
         }


        protected bool SummonGreatMajuGarzettWithWyrm()
        {
            int[] materials = new[] { CardId.ConstructorDemolisherWyrmDrakeCrush, CardId.ConstructorWyrmBuildragon, CardId.WyrmExcavatorTheHeavyCavalryDraco, CardId.ConstructorSkyWyrmGantryDragon };
            if (Bot.HasInMonstersZone(materials))
            {
                AI.SelectCard(materials);
                return true;
            }
            return false;
        }
        private bool SummonConstructorAmbush()
        {
            return !KeepWyrmForConstructorAmbush() && MonsSummon();
        }

        private bool RepositionConstructorAmbush()
        {
            if (Bot.HasInSpellZone(CardId.ConstructorAmbush)&&
                Bot.Hand.GetMatchingCardsCount(card => card.Level < 7 && (card.Race & (int)CardRace.Wyrm) > 0)>0)
            {
                ClientCard bestMon = Bot.MonsterZone.GetMonsters().OrderByDescending(card => card.Attack).FirstOrDefault();
                if (bestMon == null) return false;
                if(Card.Id == bestMon.Id)
                {
                    return !Card.IsAttack();
                }
            }
            return MonsRepos();
        }
        private bool SetConstructorAmbush()
        {
            return !KeepWyrmForConstructorAmbush() && MonsSet();
        }

        private bool KeepWyrmForConstructorAmbush()
        {
            if (Card.Level >= 5 || !Card.HasRace(CardRace.Wyrm) ||
                !Bot.HasInSpellZone(CardId.ConstructorAmbush) || IsUpperMonSummon())
                return false;
            ClientCard strongest = null;
            int copies = 0;
            foreach (ClientCard wyrm in Bot.Hand)
            {
                if (wyrm.Id == Card.Id) copies++;
                if (wyrm.Level < 5 && wyrm.HasRace(CardRace.Wyrm) &&
                    (strongest == null || wyrm.Attack > strongest.Attack))
                    strongest = wyrm;
            }
            return copies == 1 && strongest != null && strongest.Id == Card.Id;
        }
        private bool MonsSet()
        {
            ClientCard EmWorstMon = WeakestFaceupEnemy();
            if (Card.Level < 5)
            {
                if (Card.Id == CardId.PicklonTheConstructorFairy)
                {
                    return !Discord1Draw1Eff(CardRace.Wyrm, false);
                }

                if (Duel.Turn == 1)
                { if (Bot.HasInHandOrInSpellZone(CardId.ConstructorBlockade) &&
                    (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsFaceup() && card.Level < 5) <= 0))
                        return false;
                    else
                        return true;
                }
                if (EmWorstMon == null) return false;

                if (Card.Id == CardId.Rindrmuhle)
                {
                    return !SummonRindrmuhle();
                }
                if (Bot.HasInSpellZone(CardId.PeaksOfBlisstopia))
                {
                    return Card.Attack + 400 < (EmWorstMon.GetDefensePower());
                }
                else
                return Card.Attack < (EmWorstMon.GetDefensePower());
            }
            else
                return false;
        }

        private bool UpperMonSummon(ClientCard _card)
        {
            if (_card.Level < 5) return true;
            int tributeAllow = Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5);
            if (HasFaceupFieldSpell())
            {
                if (_card.Id == CardId.ConstructorWyrmBuildragon)
                    tributeAllow += Bot.HasInMonstersZone(CardId.ConstructorDemolisherWyrmDrakeCrush) ? 1 : 0;
                tributeAllow += Bot.HasInMonstersZone(CardId.PhantasmSpiralDragon) ? 1 : 0;
                if (_card.Id == CardId.ConstructorSkyWyrmGantryDragon || Bot.HasInMonstersZone(CardId.ConstructorSkyWyrmGantryDragon))
                    tributeAllow += Bot.HasInMonstersZone(CardId.ConstructorWyrmBuildragon) ? 1 : 0;
            }
            if ((_card.Id != CardId.ConstructorDemolisherWyrmDrakeCrush && _card.Id != CardId.PhantasmSpiralDragon) && Bot.Hand.GetMatchingCardsCount(card => card.Level > 6) > 2 &&
                Bot.Hand.Count(card => card.IsMonster() && card.Level < 5) + Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) < 2)
                tributeAllow = Bot.GetMonsterCount();
            return tributeAllow >= (_card.Level > 6 ? 2 : 1);
        }

        private bool IsUpperMonSummon()
        {
            int tributeCandidates = Bot.MonsterZone.Count(c => c != null && c.Level < 5)
                + Bot.Hand.Count(c => c.IsMonster() && c.Level < 5);
            bool hasSpace = Bot.MonsterZone.Count(c => c != null && c.Level > 4) <= 1;
            return Bot.Hand.Any(c => c.Level > 4 && (UpperMonSummon(c)
                || (hasSpace && tributeCandidates >= (c.Level > 6 ? 2 : 1))));
        }
        private bool MonsSummon()
        {
            if (Card.Id == CardId.PicklonTheConstructorFairy )
            {
                return SummonPicklonTheConstructorFairy();
            }
            if(Card.Id == CardId.Rindrmuhle)
            {
                return SummonRindrmuhle();
            }
            if(Card.Level>6)
            {
                return UpperMonSummon(Card);
            }
            return RushMonsterSummon();
        }
        private bool ActivatePicklonTheConstructorFairy()
        {
            if (Bot.HasInMonstersZone(CardId.ConstructorDemolisherWyrmDrakeCrush) || (Bot.HasInMonstersZone(CardId.PhantasmSpiralDragon)) && Bot.HasInHand(CardId.ConstructorWyrmBuildragon) && Bot.HasInHand(CardId.ConstructorSkyWyrmGantryDragon))
                return Discord1Draw1Eff(CardRace.Wyrm, true, true);
            return Discord1Draw1Eff(CardRace.Wyrm, true, false);
        }
        private bool Discord1Draw1Eff(CardRace race, bool isAISelect = true, bool isReplaceUpperMon = false)
        {
            if (Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0) > 0)
            {
                int triCost = Bot.Hand.GetMatchingCardsCount(card => card.Level > 4 && card.Level < 7) +
                    Bot.Hand.GetMatchingCardsCount(card => card.Level > 6) * 2;
                int tributeNum = (isReplaceUpperMon ?
                    Bot.GetMonsterCount() : Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5)) +
                    Bot.Hand.Count(card => card.IsMonster() && card.Level < 5);

                if (triCost > tributeNum &&
                    Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0 && card.Level > 4) > 0)
                {
                    if (isAISelect && triCost > 2) AI.SelectCard(rePriList);
                }
                else if (triCost < tributeNum -1 && triCost >0 && Bot.GetSpellCountWithoutField()<3 &&
                     Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0 && card.Level < 5) > 0)
                {
                    if (isAISelect) AI.SelectCard(Bot.Hand.GetFirstMatchingCard(card => (card.Race & (int)race) > 0 && card.Level < 5));
                }
                else return false;
                return true;
            }
            return false;
        }
        private bool sealResolved;

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            sealResolved = false;
        }

        public override void OnChainSolved(int chainIndex)
        {
            base.OnChainSolved(chainIndex);
            if (chainIndex <= 0 || chainIndex > Duel.CurrentChainInfo.Count || Duel.NegatedChainIndexList.Contains(chainIndex))
                return;
            ChainInfo link = Duel.CurrentChainInfo[chainIndex - 1];
            if (link.ActivatePlayer == 0 && link.IsActivateCode(CardId.TalismanicSealArray))
                sealResolved = true;
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (sealResolved && Bot.GetMonsterCount() <= Enemy.GetMonsterCount())
            {
                for (int i = 0; i < defenders.Count; i++)
                {
                    ClientCard defender = defenders[i];
                    if (!defender.IsFaceup() || !defender.IsAttack()) continue;
                    attacker.RealPower = attacker.Attack;
                    defender.RealPower = defender.GetDefensePower();
                    if (!OnPreBattleBetween(attacker, defender))
                        continue;
                    if (attacker.RealPower > defender.RealPower && defender.Level > 6 && defender.IsAttack() && defender.Attack < 1800)
                    {
                        return AI.Attack(attacker, defender);
                    }
                }
            }
            return base.OnSelectAttackTarget(attacker, defenders);
        }
        private ClientCard WeakestFaceupEnemy()
        {
            return Enemy.MonsterZone.Where(c => c != null && c.IsFaceup()).OrderBy(c => c.GetDefensePower()).FirstOrDefault();
        }

        private bool HasFaceupFieldSpell()
        {
            return Bot.GetFieldSpellCard()?.IsFaceup() == true || Enemy.GetFieldSpellCard()?.IsFaceup() == true;
        }

        private bool ActivateConstructorWyrmBuildragon()
        {
            if (!HasFaceupFieldSpell() || Bot.Hand.Any(c => c.Level > 6 && UpperMonSummon(c)))
                return false;
            ClientCard monster = Bot.Graveyard.Where(c => c.HasRace(CardRace.Wyrm) && c.HasType(CardType.Normal))
                .OrderByDescending(c => c.Level).FirstOrDefault();
            if (monster == null)
                return false;
            ClientCard enemy = WeakestFaceupEnemy();
            int power = monster.Attack + (Bot.HasInSpellZone(CardId.PeaksOfBlisstopia) ? 400 : 0);
            AI.SelectCard(monster);
            AI.SelectPosition(enemy == null || power >= enemy.GetDefensePower() ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence);
            return true;
        }

        private bool ActivateConstructorFighterMixer()
        {
            return HasFaceupFieldSpell();
        }

        private bool ActivateCoilDragonOfTheFertileLand()
        {
            return Bot.Graveyard.Any(c => c.HasType(CardType.Field));
        }

        private bool ActivateCoilDragonOfDesolation()
        {
            ClientCard field = Bot.GetFieldSpellCard();
            if (field == null || !field.IsFaceup() || (Enemy.LifePoints > 600 && !Bot.HasInHand(CardId.PeaksOfBlisstopia)))
                return false;
            AI.SelectCard(field);
            return true;
        }

        private bool ActivateDestructorArtTotalDestruction()
        {
            int enemyAttack = Util.GetTotalAttackingMonsterAttack(1);
            if (!(enemyAttack >= 1900 && Enemy.MonsterZone.Where(c => c != null && c.IsFaceup()).GetImmuneTrapMonster() == null)
                && !(enemyAttack - Util.GetTotalAttackingMonsterAttack(0) > Bot.LifePoints && !Bot.HasDefendingMonster()))
                return false;
            var targets = new List<ClientCard>(6);
            int levels = 0;
            foreach (ClientCard monster in Bot.Graveyard.Where(c => c.HasRace(CardRace.Wyrm)).OrderByDescending(c => c.Level).Take(6))
            {
                targets.Add(monster);
                levels += monster.Level;
                if (levels >= 16)
                {
                    AI.SelectCard(targets);
                    return true;
                }
            }
            return false;
        }

        private bool ActivatePierce()
        {
            if (!Enemy.HasDefendingMonster() || !Bot.HasAttackingMonster())
                return false;
            ClientCard defender = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup() && c.IsDefense())
                .OrderByDescending(c => c.Defense).FirstOrDefault();
            var attackers = Bot.MonsterZone.Where(c => c != null && c.IsFaceup() && c.IsAttack());
            ClientCard target;
            if (defender == null)
                target = attackers.OrderByDescending(c => c.Attack).FirstOrDefault();
            else
            {
                var tributeMonsters = attackers.Where(c => c.Level >= 7);
                if (tributeMonsters.Count() <= Enemy.MonsterZone.Count(c => c != null && c.IsFaceup() && c.IsAttack() && c.Level >= 7))
                    return false;
                target = tributeMonsters.Where(c => c.Attack > defender.Defense).OrderBy(c => c.Attack).FirstOrDefault();
            }
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool ActivateConstructorResurrection()
        {
            bool canReviveNormal = Bot.HasInMonstersZone(CardId.ConstructorWyrmBuildragon) && HasFaceupFieldSpell()
                && Bot.Graveyard.Any(c => c.HasRace(CardRace.Wyrm) && c.HasType(CardType.Normal));
            var revivals = Bot.Graveyard.Where(c => c.Level >= 7 && c.HasRace(CardRace.Wyrm));
            ClientCard monster = revivals.OrderBy(c =>
            {
                if (canReviveNormal && c.IsCode(CardId.ConstructorWyrmBuildragon, CardId.ConstructorDemolisherWyrmDrakeCrush))
                    return c.IsCode(CardId.ConstructorWyrmBuildragon) ? -2 : -1;
                int index = Array.IndexOf(priList, c.Id);
                return index < 0 ? int.MaxValue : index;
            }).FirstOrDefault();
            if (monster == null)
                return false;
            int attack = monster.Attack + (Bot.HasInSpellZone(CardId.PeaksOfBlisstopia) ? 400 : 0);
            bool defend = Enemy.MonsterZone.Any(c => c != null && c.IsAttack() && !c.Attacked && c.Attack > attack);
            AI.SelectCard(monster);
            AI.SelectPosition(defend ? CardPosition.FaceUpDefence : CardPosition.FaceUpAttack);
            return true;
        }

        private bool ActivateConstructorAmbush()
        {
            ClientCard own = Bot.BattlingMonster, enemy = Enemy.BattlingMonster;
            if (own == null || !own.IsFaceup() || !own.HasRace(CardRace.Wyrm) || enemy == null)
                return false;
            ClientCard cost = Bot.Hand.Where(c => c.Level < 7 && c.HasRace(CardRace.Wyrm))
                .OrderByDescending(c => c.Attack).FirstOrDefault();
            if (cost == null || own.GetDefensePower() + cost.Attack < enemy.Attack)
                return false;
            AI.SelectCard(cost);
            return true;
        }

        private bool ActivateConstructorBlockade()
        {
            var costs = Bot.MonsterZone.Where(c => c != null && c.IsFaceup() && c.HasRace(CardRace.Wyrm));
            ClientCard cost;
            if (!costs.Any(c => c.Level >= 7))
                cost = costs.OrderBy(c => c.GetDefensePower()).FirstOrDefault();
            else
            {
                cost = Bot.HasInMonstersZone(CardId.ConstructorWyrmBuildragon) && HasFaceupFieldSpell()
                    ? costs.Where(c => c.HasType(CardType.Normal)).OrderBy(c => c.Level).FirstOrDefault() : null;
                if (cost == null)
                    cost = costs.Where(c => c.Level < 5).OrderBy(c => c.Attack).FirstOrDefault();
                if (cost == null)
                {
                    int power = costs.Max(c => c.GetDefensePower());
                    int threats = Enemy.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Attack > power);
                    bool hasBackup = (Bot.HasInSpellZone(CardId.ConstructorAmbush) && Bot.Hand.Any(c => c.Level < 7 && c.HasRace(CardRace.Wyrm)))
                        || (Bot.HasInSpellZone(CardId.ConstructorResurrection) && Bot.Graveyard.Any(c => c.Level >= 7 && c.HasRace(CardRace.Wyrm)));
                    if (threats <= (hasBackup ? 1 : 0))
                        return false;
                    cost = new CardSelector(rePriList).Select(costs.ToList(), 1, 1).First();
                }
            }
            if (cost == null)
                return false;
            AI.SelectCard(cost);
            return true;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.ConstructorBlockade) && hint == Util.GetStringId(CardId.ConstructorBlockade, 1))
                    return cards.OrderByDescending(c => Duel.LastSummonedCards.Contains(c)).ThenByDescending(c => c.Attack).Take(max).ToList();
                if (link.IsActivateCode(CardId.TalismanicSealArray) && hint == Util.GetStringId(CardId.TalismanicSealArray, 1))
                    return cards.OrderByDescending(c => c.IsAttack() && c.Level >= 7).ThenByDescending(c => c.Attack).Take(max).ToList();
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private bool ActivateTalismanicSealArray()
        {
            if (!Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsAttack() && c.Level >= 7))
                return false;
            // Keep one heavy Wyrm for revival while other monsters can pay the four-card cost.
            ClientCard reserve = Bot.Graveyard.FirstOrDefault(c => c.IsCode(CardId.WyrmExcavatorTheHeavyCavalryDraco));
            var cost = Bot.Graveyard.Where(c => c.IsMonster()).OrderBy(c =>
            {
                if (c == reserve) return int.MaxValue;
                int rank = Array.IndexOf(priList, c.Id);
                return rank < 0 ? priList.Length : rank;
            }).Take(4).ToList();
            if (cost.Count < 4)
                return false;
            AI.SelectCard(cost);
            return true;
        }

    }
}
