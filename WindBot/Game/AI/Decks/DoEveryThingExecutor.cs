using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("Test", "AI_Test", "Manual")]
    public class DoEverythingExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int LeoWizard = 4392470;
            public const int Bunilla = 69380702;
            public const int DemiseKingOfArmageddon = 72426662;
        }

        private bool demiseActivated;

        public DoEverythingExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.SpSummon);
            // DefaultDontChainMyself excludes cards with an explicit handler,
            // so declining this wipe cannot fall through to generic activation.
            AddExecutor(ExecutorType.Activate, CardId.DemiseKingOfArmageddon, DemiseEffect);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.SpellSet);
        }

        private bool DemiseEffect()
        {
            if (demiseActivated || Bot.LifePoints <= 2000)
                return false;

            // Demise survives its own wipe. Require a favorable field trade,
            // with a blocker, opposing backrow or multiple opposing cards.
            int friendlyLosses = Bot.GetMonsterCount() + Bot.GetSpellCount() - 1;
            int opposingCards = Enemy.GetMonsterCount() + Enemy.GetSpellCount();
            return opposingCards > friendlyLosses
                && (DefaultDarkHole() || DefaultHeavyStorm() || opposingCards > 1);
        }

        public override void OnChaining(int player, ClientCard card)
        {
            base.OnChaining(player, card);
            if (player == 0 && card.IsCode(CardId.DemiseKingOfArmageddon))
                demiseActivated = true;
        }

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            // A surviving/negated opposing board must not provoke another
            // paid wipe in the same turn, but later turns can reassess it.
            demiseActivated = false;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (Duel.Phase == DuelPhase.BattleStart)
                return null;

            IList<ClientCard> selected = new List<ClientCard>();

            // select the last cards
            for (int i = 1; i <= max; ++i)
                selected.Add(cards[cards.Count-i]);

            return selected;
        }

        public override int OnSelectOption(IList<int> options)
        {
            return Program.Rand.Next(options.Count);
        }

    }
}
