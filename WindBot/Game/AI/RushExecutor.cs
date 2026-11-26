using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI
{
    public abstract class RushExecutor : DefaultExecutor
    {
        protected static class RushCardId
        {
            public const int SeaDragonKnight = 120199032;
            public const int SiestaTorero = 120170035;
            public const int BarrierStatueOfTheInferno = 120226013;
            public const int DoomblazeFiendOverlordDespairacion = 120231008;
            public const int UltimateFlagMechAceBreaker = 120181001;
            public const int SteelMechLordMirrorInnovator = 120155015;
            public const int DestroyerOfDragonSorcerers = 120231049;
            public const int PerionTheStarKnight = 120109014;
            public const int AgarielTheFinestSushiFairy = 120151039;
            public const int YamirulerTheDarkDelayerSupremeSoldierSpear = 120231052;

            public const int BurstStreamOfDestruction = 120203028;
            public const int DarkMagicAttack = 120130041;
            public const int MonsterReborn = 120194004;
            public const int MonsterRebornAlternateArt = 120195004;
            public const int TributeToTheDoomed = 120151023;
            public const int PotOfGreed = 120181003;
            public const int PotOfArrogance = 120238006;
            public const int DarkHole = 120231069;
            public const int MagicCylinder = 120232005;
            public const int MirrorForce = 120198003;
            public const int TorrentialTribute = 120223105;
            public const int MalevolentCatastrophe = 120238028;
            public const int SakuretsuArmor = 120194005;
            public const int WidespreadRuin = 120234005;
            public const int WidespreadRuinAlternateArt = 120237001;
            public const int TrapHole = 120150019;
            public const int IcarusAttack = 120247065;
            public const int NegateAttack = 120249065;
            public const int QuakingMirrorForce = 120263038;
            public const int TreacherousTrapHole = 120272063;
            public const int SixthSense = 120272064;
            public const int DrowningMirrorForce = 120274084;
            public const int CompulsoryEvacuationDevice = 120254032;
            public const int DarkRevelation = 120105013;
            public const int DragonlionPitTrap = 120231068;
            public const int ExcutieCapture = 120231041;

            public const int Jinzo = 120155000;
            public const int PitchBlackWarwolf = 120214001;
            public const int MirageDragon = 120223102;
            public const int BarrelDragon = 120208000;
            public const int GreatMajuGarzett = 120238019;
            public const int DarkMagician = 120130000;
            public const int MillenniumShield = 120194001;


            public const int SevensRoadMage = 120130016;
            public const int SparkheartsGirl = 120145014;
            public const int AssaultCannon = 120183031;
        }

        private static int _seed = Environment.TickCount;
        private readonly Random _random = new Random(Interlocked.Increment(ref _seed));

        protected RushExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
        }

        // Dinocarriage uses the normal WindBot option and position callbacks.
        protected virtual bool UseGenericRushChoices { get { return true; } }

        public override int GetBattleTargetPower(ClientCard defender)
        {
            // A set monster has the same unknown power whether the client cached its identity or not.
            return defender.IsFacedown() ? 0 : defender.GetDefensePower();
        }

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            return defender.IsFacedown() || base.OnPreBattleBetween(attacker, defender);
        }

        protected override bool AllRepositionThreatsStronger()
        {
            int ownPower = Util.GetBestPower(Bot);
            bool hasOpponent = false;
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster == null || !monster.IsFaceup()) continue;
                if (monster.GetDefensePower() <= ownPower) return false;
                hasOpponent = true;
            }
            return hasOpponent;
        }

        protected override int StrongestRepositionThreat()
        {
            int strongest = -1;
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster != null && monster.IsFaceup())
                    strongest = Math.Max(strongest, monster.GetDefensePower());
            }
            return strongest;
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override int OnSelectOption(IList<int> options)
        {
            if (!UseGenericRushChoices || options.Count == 0)
                return base.OnSelectOption(options);
            return _random.Next(options.Count);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (!UseGenericRushChoices)
                return base.OnSelectPosition(cardId, positions);
            if (AI.HaveSelectedPosition())
                return 0;
            NamedCard data = NamedCard.Get(cardId);
            if (data == null || data.Attack > 1000)
                return 0;
            CardPosition preferred = data.Attack < 0 ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence;
            return positions.Contains(preferred) ? preferred : 0;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards())
                return null;

            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            int owner = link != null && link.ActivatePlayer == 0 ? SharedRushEffect(link.ActivateId) : 0;
            IList<ClientCard> effectChoice = SelectEffectCards(owner, hint, cards, min, max);
            if (effectChoice != null || Duel.Phase == DuelPhase.BattleStart)
                return effectChoice;

            int limit = Math.Min(max, cards.Count);
            if (limit <= 0)
                return Array.Empty<ClientCard>();
            var selected = limit > 1 ? new List<ClientCard>(limit) : null;
            var used = limit > 1 ? new HashSet<ClientCard>() : null;
            bool material = IsMaterial(hint);
            bool enemyFirst = material || hint == HintMsg.Destroy || hint == HintMsg.Remove ||
                hint == HintMsg.ToGrave || hint == HintMsg.ReturnToHand || hint == HintMsg.ToDeck || hint == HintMsg.Disable;
            bool deckFirst = hint == HintMsg.SpSummon || hint == HintMsg.ToGrave || hint == HintMsg.Remove ||
                hint == HintMsg.AddToHand || hint == HintMsg.FusionMaterial;
            bool fill = hint == HintMsg.SpSummon || hint == HintMsg.ToGrave || hint == HintMsg.AddToHand ||
                hint == HintMsg.FusionMaterial || hint == HintMsg.Equip || hint == HintMsg.Confirm;

            // Most prompts ask for a single card. Scan the offered instances directly.
            for (int pick = 0; pick < limit; ++pick)
            {
                ClientCard best = null;
                int bestPriority = -1;
                int ties = 0;
                for (int i = 0; i < cards.Count; ++i)
                {
                    ClientCard card = cards[i];
                    if (card == null || (pick > 0 && used.Contains(card)))
                        continue;
                    int priority = 0;
                    if (enemyFirst && card.Controller == 1)
                        priority = 4;
                    else if (deckFirst && card.Location == CardLocation.Deck)
                        priority = 3;
                    else if (deckFirst && card.Location == CardLocation.Extra)
                        priority = 2;
                    else if (hint == HintMsg.Equip && card.Controller == 0)
                        priority = 1;
                    if (priority < bestPriority)
                        continue;
                    // Keep the weakest own material. Opponent face-down stats are private.
                    if (priority == bestPriority && material && priority == 0 && best != null)
                    {
                        if (card.Attack >= best.Attack)
                            continue;
                    }
                    else if (priority == bestPriority && _random.Next(++ties) != 0)
                        continue;
                    if (priority != bestPriority)
                        ties = 1;
                    best = card;
                    bestPriority = priority;
                }
                if (best == null || (pick >= min && !fill && bestPriority == 0))
                    break;
                if (limit == 1)
                    return new[] { best };
                selected.Add(best);
                used.Add(best);
            }
            return selected ?? (IList<ClientCard>)Array.Empty<ClientCard>();
        }

        private static bool IsMaterial(int hint)
        {
            return hint == HintMsg.Release || hint == HintMsg.Tribute || hint == HintMsg.FusionMaterial ||
                hint == HintMsg.SynchroMaterial || hint == HintMsg.XyzMaterial || hint == HintMsg.LinkMaterial;
        }


        private static int SharedRushEffect(int printedId)
        {
            // Only equivalent printings verified in the bundled Rush scripts.
            switch (printedId)
            {
                case 120195004:
                case 129022671:
                    return RushCardId.MonsterReborn;
                case 120197003:
                    return RushCardId.SevensRoadMage;
                case 120239059:
                case 129018890:
                    return RushCardId.SteelMechLordMirrorInnovator;
                case 129018567:
                    return RushCardId.DestroyerOfDragonSorcerers;
                case 129022509:
                    return RushCardId.UltimateFlagMechAceBreaker;
                default:
                    return printedId;
            }
        }

        private IList<ClientCard> SelectEffectCards(int owner, int hint, IList<ClientCard> cards, int min, int max)
        {
            if (owner == 0 || cards.Count == 0 || max < 1)
                return null;
            if (owner == RushCardId.PerionTheStarKnight && hint == Util.GetStringId(owner, 1))
            {
                // The script's subgroup must contain one face-up monster from each field.
                // The core can offer the two picks together or as select/unselect steps.
                ClientCard own = null, enemy = null;
                foreach (ClientCard card in cards)
                {
                    if (card == null || !card.IsFaceup()) continue;
                    if (card.Controller == 0 && (own == null || card.Attack > own.Attack))
                        own = card;
                    if (card.Controller == 1 && (enemy == null || card.Attack < enemy.Attack))
                        enemy = card;
                }
                if (min == 2 && max >= 2 && own != null && enemy != null)
                    return new[] { own, enemy };
                if (max == 1 && (own != null || enemy != null))
                    return new[] { own ?? enemy };
                return null;
            }

            if (owner == RushCardId.SteelMechLordMirrorInnovator && hint == HintMsg.ToDeck)
            {
                // Returning one monster grants piercing. Otherwise each returned level adds 100 ATK.
                int count = ActivateMirrorInnovatorForPiercing() ? Math.Max(1, min) : Math.Min(3, max);
                return cards.OrderByDescending(c => c.Level).Take(count).ToList();
            }
            bool destroy = (owner == RushCardId.TributeToTheDoomed || owner == RushCardId.UltimateFlagMechAceBreaker || owner == RushCardId.BarrelDragon)
                && hint == HintMsg.Destroy;
            bool statTarget = (owner == RushCardId.SevensRoadMage || owner == RushCardId.SparkheartsGirl || owner == RushCardId.AssaultCannon ||
                owner == RushCardId.AgarielTheFinestSushiFairy || owner == RushCardId.DestroyerOfDragonSorcerers) && hint == Util.GetStringId(owner, 1);
            if (min > 1 || !(destroy || statTarget || (owner == RushCardId.SiestaTorero && hint == HintMsg.Set)
                || (owner == RushCardId.MonsterReborn && hint == HintMsg.SpSummon)))
                return null;

            ClientCard best = null;
            int bestScore = int.MinValue;
            foreach (ClientCard card in cards)
            {
                if (card == null) continue;
                if (destroy && card.Controller == 0) continue;
                int score = 0;
                if (card.Controller == 0 || card.IsFaceup() || card.Location == CardLocation.Grave)
                {
                    if (destroy || owner == RushCardId.AssaultCannon)
                        score = card.GetDefensePower();
                    else if (owner == RushCardId.SevensRoadMage || owner == RushCardId.SparkheartsGirl)
                        score = card.IsAttack() ? card.Attack : -1;
                    else
                        score = card.Attack;
                }
                if (best == null || score > bestScore)
                {
                    best = card;
                    bestScore = score;
                }
            }
            if (best != null)
                return new[] { best };
            return null;
        }

        private static readonly int[] LegendTrapIds =
        {
            RushCardId.MirrorForce, RushCardId.TrapHole, RushCardId.SakuretsuArmor,
            RushCardId.MagicCylinder, RushCardId.WidespreadRuin, RushCardId.WidespreadRuinAlternateArt,
            RushCardId.MalevolentCatastrophe, RushCardId.IcarusAttack, RushCardId.NegateAttack,
            RushCardId.CompulsoryEvacuationDevice, RushCardId.QuakingMirrorForce, RushCardId.TreacherousTrapHole,
            RushCardId.SixthSense, RushCardId.DrowningMirrorForce, RushCardId.TorrentialTribute
        };

        private static readonly int[] BattleTrapSuppressors =
        {
            RushCardId.Jinzo, RushCardId.PitchBlackWarwolf, RushCardId.YamirulerTheDarkDelayerSupremeSoldierSpear,
            RushCardId.MirageDragon
        };

        protected bool ActivateBarrelDragon()
        {
            // The host asks for the target before tossing the three coins.
            return Enemy.GetMonsterCount() > 0;
        }

        protected bool ActivatePerionTheStarKnight()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsFaceup()) &&
                Enemy.MonsterZone.Any(c => c != null && c.IsFaceup());
        }

        protected bool ActivateAgarielTheFinestSushiFairy()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsFaceup());
        }

        protected bool ActivateDestroyerOfDragonSorcerers()
        {
            // The host offers four eligible graveyard cards together for the cost.
            return Bot.MonsterZone.Any(c => c != null && c.IsFaceup());
        }

        protected bool ActivateSiestaTorero()
        {
            return Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsAttack() && c.Level <= 8);
        }

        protected bool ActivateSeaDragonKnight()
        {
            return Enemy.SpellZone.Any(c => c != null && c.IsFacedown());
        }

        protected bool ActivateWhisperingFairy()
        {
            // The discard is paid before the opposing graveyard cards are offered.
            return Enemy.Graveyard.Any(c => c.IsMonster());
        }

        protected bool ActivateMirrorInnovatorForPiercing()
        {
            return Enemy.MonsterZone.Any(c => c != null && c.IsDefense()) && !HasFaceupRushThreat(2500);
        }

        protected bool ActivateAceBreakerAgainstThreat()
        {
            return HasFaceupRushThreat(1900);
        }

        protected bool ActivateAceBreakerAgainstBoss()
        {
            return HasFaceupRushThreat(2500);
        }

        protected bool FlipFaceUp()
        {
            if (Card.IsFacedown())
                return true;
            return false;
        }
        protected bool RepositionAgainstBattleTraps()
        {

            if (Enemy.GetSpellCountWithoutField() >= 1 && !Bot.HasInMonstersZone(BattleTrapSuppressors) && !Bot.HasDefendingMonster())
            {

                if (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsAttack() && card.Level > 5) >= 2
                    && Enemy.Graveyard.GetMatchingCardsCount(card => (card.Race & (int)CardRace.SpellCaster) > 0) >= 4
                    && Enemy.Graveyard.GetMatchingCardsCount(card => card.IsCode(RushCardId.DarkRevelation)) < 3)
                    return true;
                if (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsAttack()
                    && card.Attack >= Enemy.LifePoints) >= 2
                    && Enemy.GetMonsterCount() == 0
                    && Enemy.Graveyard.GetMatchingCardsCount(card => card.IsCode(LegendTrapIds)) == 0)
                    return true;
            }
            return false;
        }
        protected bool ActivateMonsterReborn()
        {
            return Enemy.Graveyard.Any(c => c.Level > 6) || Bot.Graveyard.Any(c => c.Level > 6);
        }

        protected bool ActivateDarkMagicAttack()
        {
            return Enemy.GetSpellCount()  >= 2;
        }
        protected bool ActivateDarkHole()
        {
            return (HasStrongerFaceupEnemy() && Util.GetTotalAttackingMonsterAttack(1) >= 3000 && !Enemy.HasInMonstersZone(RushCardId.DoomblazeFiendOverlordDespairacion, faceUp: true)) || Enemy.GetMonsterCount() == 3;
        }
        protected bool ActivateBurstStreamOfDestruction()
        {
            return (Util.GetTotalAttackingMonsterAttack(1) >= 3000 && !Enemy.HasInMonstersZone(RushCardId.DoomblazeFiendOverlordDespairacion, faceUp: true)) || Enemy.GetMonsterCount() == 3 ;
        }
        protected bool ActivateTributeToTheDoomed()
        {
            return TributeToTheDoomed(1900) && !Enemy.HasInMonstersZone(RushCardId.DoomblazeFiendOverlordDespairacion, faceUp: true);
        }

        protected bool TributeToTheDoomed(int minimumThreat)
        {
            // The script collects one discard, then asks for a monster only at resolution.
            return HasFaceupRushThreat(minimumThreat);
        }

        protected bool HasStrongerFaceupEnemy()
        {
            return HasFaceupRushThreat(Util.GetBestPower(Bot));
        }

        private bool HasFaceupRushThreat(int power)
        {
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster != null && monster.IsFaceup() && monster.GetDefensePower() > power)
                    return true;
            }
            return false;
        }

        protected bool ActivateMagicCylinder()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && (Bot.LifePoints <= attacker.Attack || Enemy.LifePoints <= attacker.Attack ||
                attacker.Attack > 1800) && DefaultUniqueTrap();
        }

        protected bool ActivateMirrorForce()
        {
            if (Util.GetTotalAttackingMonsterAttack(1) >= 1900 && (Enemy.GetMonsters().GetImmuneTrapMonster() == null)) return true;
            if (Util.GetTotalAttackingMonsterAttack(1) - Util.GetTotalAttackingMonsterAttack(0) > Bot.LifePoints && !Bot.HasDefendingMonster()) return true;
            return false;
        }
        protected bool ActivateDragonlionPitTrap()
        {
            int enemySixes = 0;
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster == null || !monster.IsFaceup()) continue;
                if (monster.Level >= 7) return true;
                if (monster.Level == 6) enemySixes++;
            }
            return enemySixes > 0 && (enemySixes == 3
                || enemySixes > Bot.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Level == 6));
        }

        protected bool ActivateTrapHole()
        {
            foreach (ClientCard n in Duel.LastSummonedCards)
            {
                if (n.Attack >= 1900 && !n.IsCode(RushCardId.YamirulerTheDarkDelayerSupremeSoldierSpear)) return true;
                if (Util.GetTotalAttackingMonsterAttack(1) - Util.GetTotalAttackingMonsterAttack(0) > Bot.LifePoints && !Bot.HasDefendingMonster() && !n.IsCode(RushCardId.YamirulerTheDarkDelayerSupremeSoldierSpear)) return true;
            }
            return false;
        }
        protected bool ActivateTorrentialTribute()
            {
                return !Util.HasChainedTrap(0) && Util.IsAllEnemyBetter(true) && (Enemy.GetMonsters().GetImmuneTrapMonster() == null) || Enemy.GetMonsterCount() == 3;
            }

        protected bool ActivateSakuretsuArmor()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && ((attacker.Attack > 2000 && Enemy.GetMonsters().GetImmuneTrapMonster() == null)
                || Bot.LifePoints <= attacker.Attack || (!Util.IsOneEnemyBetterThanValue(1500, true) && Bot.GetSpellCountWithoutField() == 3));
        }

        protected bool RushMonsterSummon()
        {
            if (Card.Level <= 4)
                return true;
            int needed = Card.Level <= 6 ? 1 : 2;
            foreach (ClientCard tribute in Bot.MonsterZone)
            {
                if (tribute != null && tribute.Level <= 4 && tribute.GetDefensePower() < Card.Attack && --needed == 0)
                    return true;
            }
            return false;
        }

        protected bool SummonGreatMajuGarzett()
        {
            int tributecount = 1;
            int maxAttack = 0;
            for (int k = 0; k < 7; ++k)
            {
                ClientCard tributeCard2 = Bot.MonsterZone[k];
                if (tributeCard2 == null) continue;
                if (tributeCard2.Attack > maxAttack && tributeCard2.Attack > 1600)
                {
                    maxAttack = tributeCard2.Attack;
                }
            }
            for (int k = 0; k < 7; ++k)
            {
                ClientCard tributeCard2 = Bot.MonsterZone[k];
                if (tributeCard2 == null) continue;
                if (tributeCard2.Attack == maxAttack)
                {
                    tributecount--;
                    AI.SelectMaterials(tributeCard2);
                }
            }
            return tributecount <= 0;
        }
    }
}
