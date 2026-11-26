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
            public const int SuperKingRex = 120130029;
            public const int RageWreckerDragonAngerogue = 120231056;
            public const int Megazowler = 120130005;
            public const int Uraby = 120151012;
            public const int Celebrontosaurus = 120151013;
            public const int SupressaurusSternptera = 120151011;
            public const int MechTherizinoraptor = 120151009;
            public const int RitzyKingRex = 120151010;
            public const int JurassicWorld = 120151015;
            public const int CoevolutionaryMiracle = 120151016;
            public const int UpstartGoblin = 120151018;
            public const int DinosaurCrush = 120151017;
            public const int AscendingEvolution = 120151019;
            public const int DinomicPressure = 120151020;
            public const int DynamicDinoDynamixL = 120151005;
            public const int DynamicDinoDynamixR = 120151007;
            public const int DynamicDinoDynamix = 120151006;
        }

        public DinocarriageDynarmixExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.SpSummon, CardId.DynamicDinoDynamix, MAXsummon);
            AddExecutor(ExecutorType.Activate, CardId.DynamicDinoDynamix, ActivateDynamicDinoDynamix);
            AddExecutor(ExecutorType.Activate, CardId.UpstartGoblin);
            AddExecutor(ExecutorType.Activate, CardId.SupressaurusSternptera);
            AddExecutor(ExecutorType.Activate, CardId.RitzyKingRex, ActivateRitzyKingRex);
            AddExecutor(ExecutorType.Activate, CardId.MechTherizinoraptor, ActivateMechTherizinoraptor);

            AddExecutor(ExecutorType.Summon, CardId.DynamicDinoDynamixL, MAXLsummon);
            AddExecutor(ExecutorType.Summon, CardId.DynamicDinoDynamixR, MAXRsummon);
            AddExecutor(ExecutorType.Activate, CardId.CoevolutionaryMiracle, ActivateCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Repos, CardId.DynamicDinoDynamixL, RepositionCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Repos, CardId.DynamicDinoDynamixR, RepositionCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Repos, CardId.SupressaurusSternptera, RepositionCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Repos, CardId.MechTherizinoraptor, RepositionCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Repos, CardId.RitzyKingRex, RepositionCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Repos, CardId.Celebrontosaurus, RepositionCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Repos, CardId.Uraby, RepositionCoevolutionaryMiracle);
            AddExecutor(ExecutorType.Summon, CardId.Megazowler, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.SuperKingRex, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.RageWreckerDragonAngerogue, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.SuperKingRex, ActivateSuperKingRex);
            AddExecutor(ExecutorType.Summon, CardId.MechTherizinoraptor);
            AddExecutor(ExecutorType.Summon, CardId.SupressaurusSternptera, SummonSupressaurusSternptera);
            AddExecutor(ExecutorType.Summon, CardId.RitzyKingRex, SummonRitzyKingRex);
            AddExecutor(ExecutorType.Activate, CardId.DinosaurCrush, ActivateDinosaurCrush);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Uraby, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Celebrontosaurus, RushMonsterSummon);

            AddExecutor(ExecutorType.MonsterSet, RushCardId.MillenniumShield, RushMonsterSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.DynamicDinoDynamixL, MAXLset);
            AddExecutor(ExecutorType.MonsterSet, CardId.DynamicDinoDynamixR, MAXRset);
            AddExecutor(ExecutorType.MonsterSet, CardId.SupressaurusSternptera);
            AddExecutor(ExecutorType.MonsterSet, CardId.RitzyKingRex);
            AddExecutor(ExecutorType.MonsterSet, CardId.Celebrontosaurus, monsterset);
            AddExecutor(ExecutorType.Activate, CardId.JurassicWorld);
            AddExecutor(ExecutorType.Activate, CardId.RageWreckerDragonAngerogue);
            AddExecutor(ExecutorType.Activate, RushCardId.MirrorForce, ActivateMirrorForce);
            AddExecutor(ExecutorType.Activate, RushCardId.MagicCylinder, ActivateMagicCylinder);
            AddExecutor(ExecutorType.Activate, CardId.DinomicPressure);
            AddExecutor(ExecutorType.Activate, CardId.AscendingEvolution, ActivateAscendingEvolution);
            AddExecutor(ExecutorType.SpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, RepositionAgainstBattleTraps);
            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
        }
        protected override bool UseGenericRushChoices { get { return false; } }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards())
                return null;
            if (hint == Util.GetStringId(120000000, 1) && cards.All(c => c.Controller == 0 &&
                c.Location == CardLocation.Hand && (c.IsCode(CardId.DynamicDinoDynamixL) || c.IsCode(CardId.DynamicDinoDynamixR))))
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
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.DinosaurCrush) &&
                hint == HintMsg.Destroy && min == 1 && max == 1 && cards.Count > 0)
            {
                CardLocation preferred = Bot.HasInMonstersZone(CardId.DynamicDinoDynamix) ? CardLocation.MonsterZone : CardLocation.SpellZone;
                ClientCard target = cards.FirstOrDefault(c => c.Controller == 1 && c.IsFacedown() && c.Location == preferred)
                    ?? cards.FirstOrDefault(c => c.Controller == 1 && c.IsFacedown());
                if (target != null)
                    return new[] { target };
            }
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.CoevolutionaryMiracle) &&
                hint == HintMsg.SpSummon && min == 1 && max == 1)
            {
                int preferred = Bot.HasInGraveyard(CardId.Megazowler) || Bot.HasInGraveyard(CardId.Uraby)
                    ? CardId.SuperKingRex : CardId.RageWreckerDragonAngerogue;
                var dinosaur = cards.FirstOrDefault(c => c.IsCode(preferred))
                    ?? cards.FirstOrDefault(c => c.IsCode(CardId.RageWreckerDragonAngerogue, CardId.SuperKingRex));
                if (dinosaur != null)
                    return new[] { dinosaur };
            }
            if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.DynamicDinoDynamix) &&
                hint == HintMsg.Destroy && min == 1 && max <= 2 && max >= min)
            {
                var targets = cards.Where(c => c.Controller == 1 && c.Location == CardLocation.MonsterZone)
                    .OrderByDescending(c => c.IsFacedown()).Take(max).ToList();
                if (targets.Count >= min)
                    return targets;
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private static readonly int[] MaximumPieces = { CardId.DynamicDinoDynamixL, CardId.DynamicDinoDynamixR };

        private bool ActivateDynamicDinoDynamix()
        {
            AI.SelectCard(CardId.SupressaurusSternptera, CardId.Celebrontosaurus, CardId.Uraby, CardId.Megazowler,
                CardId.RitzyKingRex, CardId.MechTherizinoraptor, CardId.RageWreckerDragonAngerogue, CardId.SuperKingRex,
                CardId.DynamicDinoDynamix, CardId.DynamicDinoDynamixL, CardId.DynamicDinoDynamixR);
            return true;
        }

        private bool MAXsummon()
        {
            return !Bot.HasInMonstersZone(CardId.DynamicDinoDynamix);
        }

        public bool monsterset()
        {
            if (Duel.Turn == 1)
            {
                return true;
            }
            else if (Bot.HasInHand(new[] {
                CardId.Megazowler,
                CardId.SuperKingRex,
                CardId.RageWreckerDragonAngerogue,
                }))
                return false;
            return true;
        }

        private bool SummonSupressaurusSternptera()
        {
            if (Duel.Turn == 1) return false;
                if (Enemy.GetHandCount() > 1)  return true;
            return false;
        }
        private bool SummonRitzyKingRex()
        {
            if (Enemy.GetHandCount() < 1) return true;
            return false;
        }

        private bool ActivateRitzyKingRex()
        {
            AI.SelectCard(CardId.UpstartGoblin);
            return true;
        }
        private bool ActivateCoevolutionaryMiracle()
        {
            if (Bot.HasInMonstersZone(CardId.DynamicDinoDynamix))
                return false;
            bool hasBoss = Bot.HasInHand(CardId.SuperKingRex);
            bool canRevive = Bot.HasInGraveyard(CardId.Megazowler) || Bot.HasInGraveyard(CardId.Uraby);
            int maxLevel = hasBoss && canRevive ? 6 : 4;
            if ((!hasBoss && !Bot.HasInHand(CardId.RageWreckerDragonAngerogue)) || !Bot.MonsterZone.Any(c => c != null &&
                c.IsFaceup() && c.HasRace(CardRace.Dinosaur) && c.Level <= maxLevel))
                return false;
            AI.SelectCard(CardId.DynamicDinoDynamixL, CardId.DynamicDinoDynamixR, CardId.SupressaurusSternptera, CardId.Celebrontosaurus, CardId.Uraby, CardId.Megazowler);
            return true;
        }

        private bool ActivateMechTherizinoraptor()
        {
            if (Bot.Hand.GetCardCount(CardId.DynamicDinoDynamix) >= 2) AI.SelectCard(CardId.DynamicDinoDynamix);
            else
            if (Bot.Hand.GetCardCount(CardId.DynamicDinoDynamixL) >= 2) AI.SelectCard(CardId.DynamicDinoDynamixL);
            else
            if (Bot.Hand.GetCardCount(CardId.DynamicDinoDynamixR) >= 2) AI.SelectCard(CardId.DynamicDinoDynamixR);
            else
            if (Bot.Hand.GetCardCount(CardId.SuperKingRex) >= 2) AI.SelectCard(CardId.SuperKingRex);
            else
                AI.SelectCard(
                    CardId.SupressaurusSternptera,
                    CardId.Celebrontosaurus,
                    CardId.Uraby,
                    CardId.Megazowler,
                    CardId.RitzyKingRex,
                    CardId.MechTherizinoraptor
                    );
            return true;
        }
        private static readonly int[] DuplicateDiscardPriority =
        {
            CardId.JurassicWorld, CardId.DynamicDinoDynamix, CardId.DynamicDinoDynamixR, CardId.DynamicDinoDynamixL, CardId.Megazowler
        };

        private bool ActivateDinosaurCrush()
        {
            foreach (int id in DuplicateDiscardPriority)
            {
                if (Bot.Hand.GetCardCount(id) >= 2)
                {
                    AI.SelectCard(id);
                    return true;
                }
            }
            AI.SelectCard(CardId.RitzyKingRex, CardId.SupressaurusSternptera, CardId.CoevolutionaryMiracle, CardId.Celebrontosaurus,
                CardId.SuperKingRex, CardId.DinomicPressure, CardId.AscendingEvolution, CardId.DinosaurCrush, CardId.Uraby);
            return true;
        }
        private bool ActivateAscendingEvolution()
        {
            AI.SelectCard(
                CardId.DynamicDinoDynamix,
                CardId.SuperKingRex,
                CardId.RageWreckerDragonAngerogue
                );
            return true;
        }
        private bool ActivateSuperKingRex()
        {
            AI.SelectCard(
                   CardId.Megazowler,
                   CardId.Uraby,
                   CardId.Celebrontosaurus
                   );
            return true;
        }
        private bool RepositionCoevolutionaryMiracle()
        {
            if (Bot.HasInHandOrInSpellZone(CardId.CoevolutionaryMiracle) && (Bot.Hand.GetCardCount(CardId.RageWreckerDragonAngerogue) + Bot.Hand.GetCardCount(CardId.SuperKingRex) >= 1))
                return true;
            return false;
        }
        private bool MAXLsummon()
        {
            if (Bot.Hand.GetCardCount(CardId.DynamicDinoDynamixL) >= 2 && Bot.HasInHandOrInSpellZone(CardId.CoevolutionaryMiracle) && (Bot.Hand.GetCardCount(CardId.RageWreckerDragonAngerogue) + Bot.Hand.GetCardCount(CardId.SuperKingRex) >= 1))
                return true;
            return false;
        }
        private bool MAXRsummon()
        {
            if (Bot.Hand.GetCardCount(CardId.DynamicDinoDynamixR) >= 2 && Bot.HasInHandOrInSpellZone(CardId.CoevolutionaryMiracle) && (Bot.Hand.GetCardCount(CardId.RageWreckerDragonAngerogue)+ Bot.Hand.GetCardCount(CardId.SuperKingRex) >= 1))
                return true;
            return false;
        }
        private bool MAXLset()
        {
            if (Bot.Hand.GetCardCount(CardId.DynamicDinoDynamixL) < 2) return false;
            return true;
        }
        private bool MAXRset()
        {
            if (Bot.Hand.GetCardCount(CardId.DynamicDinoDynamixR) < 2) return false;
            return true;
        }

    }
}
