using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("SaikyoBattleFlag", "AI_SaikyoBattleFlag")]
    public class SaikyoBattleFlagExecutor : RushExecutor
    {
        public class CardId
        {
            public const int UltimateFlagMechAceBreaker = 120181001;
            public const int SteelMechLordMirrorInnovator = 120155015;
            public const int MultistrikeDragonDragias = 120110001;
            public const int WorkerWarriorSinisterCEO = 120183024;
            public const int UltimateFlagBeastAvantWolf = 120193002;
            public const int WorkerWarriorNewRecruit = 120183002;

            public const int RitzyKingRex = 120151010;
            public const int Tapanchula = 120224010;
            public const int AssaultCannon = 120183031;
            public const int CrafterDrone = 120183030;
            public const int UltimateFlagBeastSurgeBicorn = 120181011;
            public const int UltimateFlagBeastAimEagle = 120193003;
            public const int MonsterReborn = 120194004;
            public const int ApocalypseBeastGearWorld = 120130039;
            public const int BarrageOfSteel = 120183054;
            public const int TributeToTheDoomed = 120151023;
            public const int ShipOfSevenTreasures = 120199052;

            public const int BeastGearSecretArtPrimalFist = 120155060;
            public const int SalarySlash = 120183062;
            public const int BattleDemotion = 120183063;
            public const int ForeverFreeze = 120183064;
            public const int DarkRevelation = 120105013;
        }

        public SaikyoBattleFlagExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.DarkHole, ActivateDarkHole);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, CardId.BarrageOfSteel);
            AddExecutor(ExecutorType.Activate, CardId.RitzyKingRex);
            AddExecutor(ExecutorType.Activate, CardId.Tapanchula);
            AddExecutor(ExecutorType.Activate, CardId.WorkerWarriorSinisterCEO, ActivateWorkerWarriorSinisterCEO);
            AddExecutor(ExecutorType.Activate, CardId.UltimateFlagMechAceBreaker, ActivateAceBreakerAgainstBoss);
            AddExecutor(ExecutorType.Activate, CardId.AssaultCannon);
            AddExecutor(ExecutorType.Activate, CardId.UltimateFlagBeastAimEagle, ActivateUltimateFlagBeastAimEagle);
            AddExecutor(ExecutorType.Activate, CardId.UltimateFlagBeastSurgeBicorn, ActivateUltimateFlagBeastSurgeBicorn);
            AddExecutor(ExecutorType.Activate, RushCardId.BarrelDragon, ActivateBarrelDragon);
            AddExecutor(ExecutorType.Repos, CardId.UltimateFlagBeastSurgeBicorn, SummonUltimateFlagBeastSurgeBicorn);
            AddExecutor(ExecutorType.Repos, CardId.UltimateFlagBeastAimEagle, SummonUltimateFlagBeastAimEagle);
            AddExecutor(ExecutorType.Repos, CardId.UltimateFlagMechAceBreaker, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.SteelMechLordMirrorInnovator, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, RushCardId.BarrelDragon, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.WorkerWarriorSinisterCEO, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagMechAceBreaker, DefaultDoubleSummon);
            AddExecutor(ExecutorType.Summon, CardId.SteelMechLordMirrorInnovator, DefaultDoubleSummon);
            AddExecutor(ExecutorType.Summon, CardId.WorkerWarriorSinisterCEO, DefaultDoubleSummon);

            AddExecutor(ExecutorType.Repos, CardId.RitzyKingRex, SummonRitzyKingRexForAimEagle);
            AddExecutor(ExecutorType.Repos, CardId.AssaultCannon, SummonAssaultCannonForAimEagle);
            AddExecutor(ExecutorType.Repos, CardId.UltimateFlagBeastSurgeBicorn, SummonSurgeBicornForAimEagle);
            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagBeastSurgeBicorn, SummonSurgeBicornForAimEagle);
            AddExecutor(ExecutorType.Summon, CardId.RitzyKingRex, SummonRitzyKingRexForAimEagle);
            AddExecutor(ExecutorType.Summon, CardId.AssaultCannon, SummonAssaultCannonForAimEagle);
            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagBeastAimEagle, SummonUltimateFlagBeastAimEagle);
            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagBeastAimEagle, SummonAimEagleWithSecondCopy);
            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagBeastAimEagle, SummonAimEagleBeforeMaterial);

            AddExecutor(ExecutorType.Summon, RushCardId.BarrelDragon, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagMechAceBreaker, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.SteelMechLordMirrorInnovator, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.WorkerWarriorSinisterCEO, RushMonsterSummon);

            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagBeastSurgeBicorn, SummonUltimateFlagBeastSurgeBicorn);
            AddExecutor(ExecutorType.Summon, CardId.Tapanchula, SummonTapanchula);
            AddExecutor(ExecutorType.Summon, CardId.RitzyKingRex, SummonRitzyKingRex);

            AddExecutor(ExecutorType.Summon, CardId.UltimateFlagMechAceBreaker, DefaultOverSummon);
            AddExecutor(ExecutorType.Summon, CardId.SteelMechLordMirrorInnovator, DefaultOverSummon);
            AddExecutor(ExecutorType.Activate, CardId.MonsterReborn, ReviveHighAttackMonster);
            AddExecutor(ExecutorType.Activate, CardId.UltimateFlagMechAceBreaker, ActivateAceBreakerAgainstThreat);
            AddExecutor(ExecutorType.Activate, CardId.TributeToTheDoomed, ActivateTributeToTheDoomed);
            AddExecutor(ExecutorType.Activate, CardId.SteelMechLordMirrorInnovator, ActivateMirrorInnovatorForPiercing);
            AddExecutor(ExecutorType.Activate, CardId.SteelMechLordMirrorInnovator, ActivateMirrorInnovatorAfterOpeningTurn);
            AddExecutor(ExecutorType.MonsterSet, CardId.RitzyKingRex);
            AddExecutor(ExecutorType.Activate, CardId.CrafterDrone,ActivateCrafterDrone);

            AddExecutor(ExecutorType.MonsterSet, CardId.CrafterDrone);
            AddExecutor(ExecutorType.MonsterSet, CardId.AssaultCannon);
            AddExecutor(ExecutorType.MonsterSet, CardId.Tapanchula);
            AddExecutor(ExecutorType.Summon, CardId.CrafterDrone, SummonCrafterDrone);
            AddExecutor(ExecutorType.Summon, CardId.AssaultCannon, SummonAssaultCannon);
            AddExecutor(ExecutorType.MonsterSet, CardId.UltimateFlagBeastAvantWolf);
            AddExecutor(ExecutorType.MonsterSet, CardId.WorkerWarriorNewRecruit);
            AddExecutor(ExecutorType.MonsterSet, CardId.UltimateFlagBeastAimEagle);
            AddExecutor(ExecutorType.MonsterSet, CardId.UltimateFlagBeastSurgeBicorn);
            AddExecutor(ExecutorType.Activate, CardId.ApocalypseBeastGearWorld, DefaultField);
            AddExecutor(ExecutorType.Activate, CardId.BeastGearSecretArtPrimalFist, ActivateBeastGearSecretArtPrimalFist);
            AddExecutor(ExecutorType.Activate, CardId.BattleDemotion, ActivateBattleDemotion);
            AddExecutor(ExecutorType.Activate, CardId.SalarySlash);
            AddExecutor(ExecutorType.Activate, CardId.BattleDemotion);
            AddExecutor(ExecutorType.Activate, CardId.BeastGearSecretArtPrimalFist);
            AddExecutor(ExecutorType.Activate, CardId.ForeverFreeze,ActivateForeverFreeze);
            AddExecutor(ExecutorType.Activate, RushCardId.MagicCylinder, ActivateMagicCylinder);
            AddExecutor(ExecutorType.Activate, RushCardId.SakuretsuArmor, ActivateSakuretsuArmor);
            AddExecutor(ExecutorType.Activate, RushCardId.TrapHole, ActivateTrapHole);
            AddExecutor(ExecutorType.Activate, RushCardId.TorrentialTribute, ActivateTorrentialTribute);
            AddExecutor(ExecutorType.Activate, RushCardId.MirrorForce, ActivateMirrorForce);

            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.ShipOfSevenTreasures, ActivateShipOfSevenTreasures);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Activate, CardId.ApocalypseBeastGearWorld, DefaultField2);
            AddExecutor(ExecutorType.Repos, CardId.SteelMechLordMirrorInnovator, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.UltimateFlagMechAceBreaker, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.WorkerWarriorSinisterCEO, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.UltimateFlagMechAceBreaker, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.SteelMechLordMirrorInnovator, RepositionAgainstBattleTraps);
        }

        private readonly HashSet<ClientCard> _doubleTributes = new HashSet<ClientCard>();
        private ClientCard _resolvingBicorn;
        private bool _payingEagleCost;

        public override void OnChaining(int player, ClientCard card)
        {
            base.OnChaining(player, card);
            _payingEagleCost = false;
            if (player == 0 && card.IsCode(CardId.UltimateFlagBeastSurgeBicorn))
                _resolvingBicorn = card;
        }

        public override void OnChainSolved(int chainIndex)
        {
            base.OnChainSolved(chainIndex);
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (_resolvingBicorn != null && link != null && link.RelatedCard == _resolvingBicorn)
            {
                if (!Duel.IsCurrentSolvingChainNegated() && _resolvingBicorn.IsFaceup() &&
                    Bot.MonsterZone.Contains(_resolvingBicorn))
                    _doubleTributes.Add(_resolvingBicorn);
                _resolvingBicorn = null;
            }
        }

        public override void OnChainEnd()
        {
            base.OnChainEnd();
            _payingEagleCost = false;
            _resolvingBicorn = null;
        }

        public override void OnMove(ClientCard card, int previousControler, int previousLocation, int currentControler, int currentLocation)
        {
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
            if (previousControler == 0 && previousLocation == (int)CardLocation.MonsterZone &&
                (currentControler != 0 || currentLocation != (int)CardLocation.MonsterZone))
                ForgetBicorn(card);
        }

        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            base.OnPosChange(card, previousPosition, currentPosition);
            if ((currentPosition & (int)CardPosition.FaceDown) != 0)
                ForgetBicorn(card);
        }

        private void ForgetBicorn(ClientCard card)
        {
            if (_resolvingBicorn == card)
                _resolvingBicorn = null;
            _doubleTributes.Remove(card);
        }

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            _payingEagleCost = false;
            _resolvingBicorn = null;
            _doubleTributes.Clear();
        }

        public override void OnNewPhase()
        {
            base.OnNewPhase();
            _payingEagleCost = false;
        }

        private bool DefaultDoubleSummon()
        {
            // The core supplies each offered tribute's actual weight to GameAI.OnSelectTribute.
            return _doubleTributes.Count > 0;
        }

        private static readonly int[] EagleSummonPriority =
        {
            RushCardId.BarrelDragon, CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO, CardId.UltimateFlagBeastAvantWolf, CardId.WorkerWarriorNewRecruit
        };

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards())
                return null;
            if (_payingEagleCost && hint == HintMsg.ToGrave && min >= 1 && min <= 2 &&
                cards.All(c => c.Controller == 0 && c.Location == CardLocation.MonsterZone &&
                    c.IsFaceup() && c.HasAttribute(CardAttribute.Light)))
            {
                var materials = cards.OrderBy(c => c.Level > 4).ThenBy(c => c.Attack).Take(min).ToList();
                if (materials.Count == min)
                    return materials;
            }
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.UltimateFlagBeastAimEagle) && hint == HintMsg.SpSummon && min == 1 && max == 1)
                {
                    foreach (int id in EagleSummonPriority)
                    {
                        var machine = cards.FirstOrDefault(c => c.Controller == 0 && c.Location == CardLocation.Hand && c.IsCode(id));
                        if (machine != null)
                            return new[] { machine };
                    }
                }
                if (link.IsActivateCode(CardId.BattleDemotion) && hint == HintMsg.PosChange && min == 1 && max == 1)
                {
                    ClientCard attacker = Enemy.BattlingMonster;
                    if (attacker != null && attacker.Attack > 2000 && cards.Contains(attacker))
                        return new[] { attacker };
                    var target = cards.Where(c => c.Controller == 1 && c.IsFaceup() && c.IsAttack())
                        .OrderByDescending(c => c.Attack).FirstOrDefault();
                    if (target != null)
                        return new[] { target };
                }
                if (link.IsActivateCode(CardId.WorkerWarriorSinisterCEO) && hint == HintMsg.Set && max > 0)
                {
                    var traps = new List<ClientCard>(2);
                    foreach (int id in TrapPriority)
                    {
                        var trap = cards.FirstOrDefault(c => c.Controller == 0 && c.Location == CardLocation.Grave && c.IsCode(id));
                        if (trap != null && traps.Count < max)
                            traps.Add(trap);
                    }
                    if (traps.Count >= min)
                        return traps;
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private static readonly int[] TrapPriority = { CardId.BattleDemotion, CardId.SalarySlash };

        private bool DefaultOverSummon()
        {
            if (Bot.Hand.Count >= 6 && Bot.HasInMonstersZone(CardId.WorkerWarriorSinisterCEO)) return true;
            else return false;
        }
        private bool ActivateBattleDemotion()
        {
            return Enemy.BattlingMonster != null && Enemy.BattlingMonster.Attack > 2000;
        }

        private bool ActivateForeverFreeze()
        {
            foreach (ClientCard monster in Bot.GetMonsters())
            {
                if (monster.IsFacedown())
                {
                    AI.SelectCard(monster);
                    return true;
                }
                else if (monster.IsDefense())
                {
                    AI.SelectCard(monster);
                    return true;
                }
            }
            AI.SelectCard(Bot.MonsterZone.GetLowestAttackMonster());
            return true;
        }
        private bool ActivateMirrorInnovatorAfterOpeningTurn()
        {
            return Duel.Turn != 1;
        }
        private bool ActivateWorkerWarriorSinisterCEO()
        {
            AI.SelectCard(CardLocation.Hand);
            return true;
        }
        private bool ActivateBeastGearSecretArtPrimalFist()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && attacker.HasRace(CardRace.Dragon | CardRace.Fairy | CardRace.SpellCaster);
        }

        private bool ReviveHighAttackMonster()
        {
            return Bot.Graveyard.Any(IsRevivalTarget) || Enemy.Graveyard.Any(IsRevivalTarget);
        }

        private static bool IsRevivalTarget(ClientCard card)
        {
            return card.IsMonster() && card.Attack >= 2000 && card.IsCanRevive();
        }
        private bool SummonRitzyKingRex()
        {
            if (Enemy.GetHandCount() < 1) return true;
            return false;
        }
        private bool SummonAssaultCannon()
        {
            if (Bot.MonsterZone.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) == 2 || Util.GetOneEnemyBetterThanMyBest(true, true) == null) return true;
            return false;
        }
        private bool SummonTapanchula()
        {
            if (Bot.HasInGraveyard(new[] { CardId.UltimateFlagBeastAvantWolf, CardId.WorkerWarriorNewRecruit }) && Enemy.GetSpellCount()  >= 2)
                return true;
            return false;
        }
        private bool SummonUltimateFlagBeastSurgeBicorn()
        {
            if (Bot.Graveyard.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) >= 3 && Bot.HasInHand(new[] { CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }) && !Bot.HasInMonstersZone(new[] { CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator }))
                return true;
            return false;
        }
        private bool ActivateUltimateFlagBeastSurgeBicorn()
        {
            if (Bot.HasInHand(new[] { CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }) && !Bot.HasInMonstersZone(new[] { CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator }))
            {
                AI.SelectCard(CardId.UltimateFlagBeastAimEagle, CardId.Tapanchula, CardId.UltimateFlagBeastSurgeBicorn, CardId.CrafterDrone, CardId.AssaultCannon, CardId.WorkerWarriorNewRecruit, CardId.UltimateFlagBeastAvantWolf, CardId.WorkerWarriorSinisterCEO, CardId.SteelMechLordMirrorInnovator, CardId.UltimateFlagMechAceBreaker);
                return true;
            }
            return false;
        }
        private bool SummonSurgeBicornForAimEagle()
        {
            if (Bot.HasInMonstersZone(CardId.UltimateFlagBeastAimEagle) && Bot.HasInHand(new[] { CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }) && Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                return true;
            return false;
        }
        private bool SummonAssaultCannonForAimEagle()
        {
            if (Bot.HasInMonstersZone(CardId.UltimateFlagBeastAimEagle) && Bot.HasInHand(new[] { RushCardId.BarrelDragon, CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }) && Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                return true;
            return false;
        }
        private bool SummonRitzyKingRexForAimEagle()
        {
            if (Bot.HasInMonstersZone(CardId.UltimateFlagBeastAimEagle) && Bot.HasInHand(new[] { RushCardId.BarrelDragon, CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }) && Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                return true;
            return false;
        }
        private bool SummonUltimateFlagBeastAimEagle()
        {
          foreach (ClientCard m in Bot.GetMonsters())
               if (m.HasAttribute(CardAttribute.Light) && m.Level <= 4 && m.IsFaceup())
              if (Bot.HasInHand(new[] { RushCardId.BarrelDragon, CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }))
             if (Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
              return true;
            return false;
        }
        private bool SummonAimEagleWithSecondCopy()
        {
            if (Bot.Hand.GetCardCount(CardId.UltimateFlagBeastAimEagle) >= 2 )
                if (Bot.HasInHand(new[] { RushCardId.BarrelDragon, CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }))
                  if (Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                             return true;
            return false;
        }
        private bool SummonAimEagleBeforeMaterial()
        {
                     if (Bot.HasInHand(CardId.UltimateFlagBeastSurgeBicorn) || Bot.HasInHand(CardId.AssaultCannon) || Bot.HasInHand(CardId.RitzyKingRex))
                if (Bot.HasInHand(new[] { RushCardId.BarrelDragon, CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO }))
                            if (Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                                return true;
            return false;
        }
        private bool ActivateUltimateFlagBeastAimEagle()
        {
            _payingEagleCost = Bot.MonsterZone.GetMatchingCardsCount(c => c.IsFaceup() &&
                c.Level <= 4 && c.HasAttribute(CardAttribute.Light)) >= 2 && Bot.HasInHand(EagleSummonPriority);
            return _payingEagleCost;
        }
        private bool ActivateShipOfSevenTreasures()
        {
            if (!Bot.HasInHand(SevenTreasuresPriority))
                return false;
            AI.SelectCard(SevenTreasuresPriority);
            return true;
        }

        private static readonly int[] SevenTreasuresPriority = { CardId.WorkerWarriorSinisterCEO, CardId.SteelMechLordMirrorInnovator, CardId.UltimateFlagMechAceBreaker, RushCardId.BarrelDragon };
        private bool ActivateCrafterDrone()
        {
            int fodder = Bot.MonsterZone.GetMatchingCardsCount(c => c.Level < 5)
                + Bot.Hand.GetMatchingCardsCount(c => c.IsMonster() && c.Level < 5);
            if (fodder <= 2)
            {
                int bosses = Bot.Hand.GetMatchingCardsCount(c => c.Level > 5);
                if (bosses > 2)
                    AI.SelectCard(CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, CardId.WorkerWarriorSinisterCEO, RushCardId.BarrelDragon);
                else if (bosses == 1 && fodder == 1)
                    AI.SelectCard(CardId.UltimateFlagMechAceBreaker, CardId.SteelMechLordMirrorInnovator, RushCardId.BarrelDragon, CardId.WorkerWarriorSinisterCEO);
                else
                    return false;
            }
            else
                AI.SelectCard(CardId.UltimateFlagBeastAimEagle, CardId.AssaultCannon, CardId.CrafterDrone, CardId.WorkerWarriorNewRecruit, CardId.UltimateFlagBeastAvantWolf);
            return true;
        }
        private bool SummonCrafterDrone()
        {
            if (Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) >=1 || Util.GetOneEnemyBetterThanMyBest(true, true) == null)
                return true;
            return false;
        }

        private bool DefaultField2()
        {
            if (Bot.GetHandCount() >= 2)
            {
                return true;
            }
            return false;
        }

    }
}
