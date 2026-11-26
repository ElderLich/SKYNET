using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("RushDragonDragears", "AI_RushDragonDragears")]
    public class RushDragonDragearsExecutor : RushExecutor
    {
        public class CardId
        {
            public const int BlueEyesWhiteDragon = 120120000;
            public const int BusterBlader = 120170000;
            public const int DarkMagician = 120130000;
            public const int RedEyesBlackDragon = 120125001;
            public const int SummonedSkull = 120145000;
            public const int Jinzo = 120155000;
            public const int MultistrikeDragonDragias = 120110001;
            public const int SevensRoadMagician = 120105001;
            public const int YamirulerTheDarkDelayer = 120120029;
            public const int WhisperingFairy = 120120018;
            public const int PhilanDwarf = 120228031;
            public const int MysticDealer = 120105006;
            public const int LightSorcerer = 120145001;
            public const int DarkSorcerer = 120105005;
            public const int SparkheartsGirl = 120145014;
            public const int SiestaTorero = 120170035;
            public const int PhoenixDragon = 120110009;
            public const int SevensRoadMage = 120130016;
            public const int sionmax = 120150007;
            public const int TributeToTheDoomed = 120151023;
            public const int TrapHole = 120150019;
            public const int DarkRevelation = 120105013;
            public const int RoadArmsSevensLance = 120228050;
        }

        public RushDragonDragearsExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.DarkHole, ActivateDarkHole);
            AddExecutor(ExecutorType.Activate, RushCardId.PotOfGreed);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Activate, CardId.PhilanDwarf);
            AddExecutor(ExecutorType.Activate, CardId.SevensRoadMage);
            AddExecutor(ExecutorType.Activate, CardId.TributeToTheDoomed, () => TributeToTheDoomed(2600));
            AddExecutor(ExecutorType.SummonOrSet, CardId.BlueEyesWhiteDragon, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.BusterBlader, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.SummonedSkull, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.MultistrikeDragonDragias, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.YamirulerTheDarkDelayer, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.DarkMagician, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.RedEyesBlackDragon, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.SevensRoadMagician, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Jinzo, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.MysticDealer);
            AddExecutor(ExecutorType.Summon, CardId.PhilanDwarf, SummonPhilanDwarf);
            AddExecutor(ExecutorType.Activate, CardId.MysticDealer, Draweffect);
            AddExecutor(ExecutorType.SpellSet, CardId.DarkRevelation);
            AddExecutor(ExecutorType.SpellSet, CardId.TrapHole);
            AddExecutor(ExecutorType.Activate, CardId.DarkRevelation, ActivateMirrorForce);
            AddExecutor(ExecutorType.Activate, RushCardId.MagicCylinder, ActivateMagicCylinder);
            AddExecutor(ExecutorType.Activate, RushCardId.SakuretsuArmor, ActivateSakuretsuArmor);
            AddExecutor(ExecutorType.Activate, CardId.TrapHole, ActivateTrapHole);
            AddExecutor(ExecutorType.Activate, RushCardId.TorrentialTribute, ActivateTorrentialTribute);
            AddExecutor(ExecutorType.Activate, RushCardId.MirrorForce, ActivateMirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.TributeToTheDoomed, ActivateTributeToTheDoomed);
            AddExecutor(ExecutorType.SpellSet, CardId.TributeToTheDoomed);
            AddExecutor(ExecutorType.Activate, CardId.PhoenixDragon);
            AddExecutor(ExecutorType.Summon, CardId.PhoenixDragon, SummonPhoenixDragon);
            AddExecutor(ExecutorType.MonsterSet, CardId.PhoenixDragon);
            AddExecutor(ExecutorType.MonsterSet, CardId.PhilanDwarf);
            AddExecutor(ExecutorType.MonsterSet, CardId.SparkheartsGirl, Monsterset);
            AddExecutor(ExecutorType.MonsterSet, CardId.SevensRoadMage, Monsterset);
            AddExecutor(ExecutorType.MonsterSet, CardId.WhisperingFairy, Monsterset);
            AddExecutor(ExecutorType.Summon, CardId.SevensRoadMage);
            AddExecutor(ExecutorType.Summon, RushCardId.BarrierStatueOfTheInferno);

            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.RoadArmsSevensLance, ActivateRoadArmsSevensLance);
            AddExecutor(ExecutorType.SpellSet);
            //AddExecutor(ExecutorType.Activate, CardId.sionmax, sionmaxEffect);
            AddExecutor(ExecutorType.Activate, CardId.WhisperingFairy, ActivateWhisperingFairy);
            AddExecutor(ExecutorType.Activate, CardId.SparkheartsGirl);
            AddExecutor(ExecutorType.Activate, CardId.MultistrikeDragonDragias);
            AddExecutor(ExecutorType.Activate, CardId.SevensRoadMagician);
            AddExecutor(ExecutorType.Activate, CardId.SiestaTorero, ActivateSiestaTorero);
            AddExecutor(ExecutorType.Activate, RushCardId.DestroyerOfDragonSorcerers, ActivateDestroyerOfDragonSorcerers);
            AddExecutor(ExecutorType.Activate, RushCardId.PerionTheStarKnight, ActivatePerionTheStarKnight);
            AddExecutor(ExecutorType.Activate, RushCardId.AgarielTheFinestSushiFairy, ActivateAgarielTheFinestSushiFairy);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, CardId.BusterBlader, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, RushCardId.BarrierStatueOfTheInferno, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.MultistrikeDragonDragias, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.SevensRoadMagician, FlipFaceUp);
            AddExecutor(ExecutorType.Repos, CardId.BlueEyesWhiteDragon, RepositionAgainstBattleTraps);
            AddExecutor(ExecutorType.Repos, CardId.BusterBlader, RepositionAgainstBattleTraps);

        }

        private static readonly int[] TributeMonsters =
        {
            CardId.SevensRoadMagician, CardId.MultistrikeDragonDragias, CardId.BlueEyesWhiteDragon, CardId.Jinzo, CardId.SummonedSkull,
            CardId.YamirulerTheDarkDelayer, CardId.BusterBlader, CardId.RedEyesBlackDragon, CardId.DarkMagician
        };

        public bool Monsterset()
        {
            return Duel.Turn == 1 || !Bot.HasInHandOrHasInMonstersZone(TributeMonsters);
        }
        private bool ActivateRoadArmsSevensLance()
        {
            if (Bot.HasInMonstersZone(CardId.SevensRoadMagician))
                AI.SelectCard(CardId.SevensRoadMagician);
            return true;
        }
        private bool SummonPhilanDwarf()
        {
            return Bot.HasInHandOrHasInMonstersZone(TributeMonsters);
        }

        public override bool OnSelectYesNo(int desc)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (desc == Util.GetStringId(CardId.PhilanDwarf, 1) && link != null &&
                link.ActivatePlayer == 0 && link.IsActivateCode(CardId.PhilanDwarf))
                return false;
            return base.OnSelectYesNo(desc);
        }

        private bool Draweffect()
        {

            if ((Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.IsMonster() && card.Level < 5)) <= 2)
            {
                if ((Bot.Hand.GetMatchingCardsCount(card => card.Level > 5) > 2) && Bot.HasInHand(CardId.SevensRoadMagician))
                    AI.SelectCard(CardId.SevensRoadMagician);
                else
                    return false;
            }
            else
                AI.SelectCard(CardId.SevensRoadMage, CardId.SparkheartsGirl, CardId.PhilanDwarf, CardId.WhisperingFairy, CardId.MysticDealer, CardId.LightSorcerer, CardId.DarkSorcerer);
            return true;
        }
        private bool SummonPhoenixDragon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.MultistrikeDragonDragias, CardId.BlueEyesWhiteDragon }) &&
                Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 4) < 2 &&
                (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.IsMonster() && card.Level < 5)) >= 2)
            {
                return true;
            }
            return false;
        }

    }
}
