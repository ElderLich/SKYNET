using System;
using System.Collections.Generic;
using System.Threading;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI
{
    public abstract class RushExecutor : DefaultExecutor
    {
        private static int _seed = Environment.TickCount;
        private readonly Random _random = new Random(Interlocked.Increment(ref _seed));

        protected RushExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
        }

        // Dinocarriage uses the normal WindBot option and position callbacks.
        protected virtual bool UseGenericRushChoices { get { return true; } }

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
            int owner = link != null && link.ActivatePlayer == 0 ? link.ActivateId : 0;
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

        private const int TributeToDoomed = 120151023;
        private const int AceBreaker = 120181001;
        private const int BarrelDragon = 120208000;
        private const int SiestaTorero = 120170035;
        private const int MonsterReborn = 120194004;
        private const int Perion = 120109014;
        private const int SevensRoadMage = 120130016;
        private const int SparkheartsGirl = 120145014;
        private const int AssaultCannon = 120183031;
        private const int Agariel = 120151039;
        private const int DestroyerOfDragonSorcerers = 120231049;
        private const int MirrorInnovator = 120155015;

        private IList<ClientCard> SelectEffectCards(int owner, int hint, IList<ClientCard> cards, int min, int max)
        {
            if (owner == 0 || cards.Count == 0 || max < 1)
                return null;
            if (owner == Perion && hint == Util.GetStringId(owner, 1))
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

            // Steel Mech needs exactly one returned monster to gain piercing.
            bool piercing = owner == MirrorInnovator && hint == HintMsg.ToDeck;
            bool destroy = (owner == TributeToDoomed || owner == AceBreaker || owner == BarrelDragon)
                && hint == HintMsg.Destroy;
            bool boost = (owner == SevensRoadMage || owner == SparkheartsGirl || owner == AssaultCannon ||
                owner == Agariel || owner == DestroyerOfDragonSorcerers) && hint == Util.GetStringId(owner, 1);
            if (min > 1 || !(piercing || destroy || boost || (owner == SiestaTorero && hint == HintMsg.Set)
                || (owner == MonsterReborn && hint == HintMsg.SpSummon)))
                return null;

            ClientCard best = null;
            int bestScore = int.MinValue;
            foreach (ClientCard card in cards)
            {
                if (card == null) continue;
                if (destroy && card.Controller == 0) continue;
                int score = 0;
                if (card.Controller == 0 || card.IsFaceup() || card.Location == CardLocation.Grave)
                    score = piercing ? card.Level : card.Attack;
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
    }
}
