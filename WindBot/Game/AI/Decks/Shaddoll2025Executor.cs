using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("Shaddoll2025", "AI_Shaddoll2025")]
    public class Shaddoll2025Executor : DefaultExecutor
    {
        public static class CardId
        {
            public const int ReeshaddollWendi = 51023024;
            public const int TohushaddollGrysta = 95072744;
            public const int ShaddollBeast = 3717252;
            public const int ShaddollDragon = 77723643;
            public const int ShaddollSquamata = 30328508;
            public const int BlazingCartesia = 95515789;
            public const int MulcharmyPurulia = 84192580;
            public const int MulcharmyFuwalos = 42141493;
            public const int ShaddollHedgehog = 4939890;
            public const int NaelshaddollAriel = 97518132;
            public const int DrollLockBird = 94145021;
            public const int EffectVeiler = 97268402;
            public const int VoidUnleashing = 61345801;
            public const int ElShaddollFusion = 6417578;
            public const int FusionDeployment = 6498706;
            public const int LevShaddollFusion = 34950192;
            public const int ShaddollFusion = 44394295;
            public const int ShaddollSchism = 21011044;
            public const int DracotailFaimena = 1498449;
            public const int TearlamentsScheiren = 572850;
            public const int MaxxC = 23434538;
            public const int ReshShaddollIncarnation = 23912837;
            public const int ArmageddonKnight = 28985331;
            public const int AshBlossomJoyousSpring = 14558127;

            public const int ElShaddollMeshachrer = 32467459;
            public const int ElShaddollShekhinaga = 74822425;
            public const int ElShaddollAnoyatyllis = 19261966;
            public const int ElShaddollConstruct = 20366274;
            public const int GranguignolDuskDragon = 24915933;
            public const int ElShaddollApkallone = 50907446;
            public const int LunalightPerfumeDancer = 81196066;
            public const int ReeshaddollWendikuruhu = 8852158;
            public const int ElShaddollWinda = 94977269;
            public const int InfernoidEvil = 97051536;
            public const int ShaddollConstruct = 86938484;

            public const int WhiteStoneOfAncients = 71039903;
            public const int BlueEyesSpiritDragon = 59822133;
            public const int ReturnOfTheDragonLords = 6853254;
            public const int DarkMagician = 46986414;
            public const int DarkMagicianTheDragonKnight = 41721210;
            public const int MagicianNavigation = 7922915;

            public const int OrcustKnightmare = 4055337;
            public const int OrcustHarpHorror = 57835716;
            public const int OrcustCymbalSkeleton = 21441617;
            public const int WorldLegacyWorldWand = 93920420;
            public const int OrcustratedClimax = 703897;
            public const int PhantomKnightsFogBlade = 25542642;
            public const int DingirsuOrcustEveningStar = 93854893;
        }

        private const int ShaddollSetcode = 0x9d;

        private enum LevRouteStage
        {
            None,
            ApkalloneSent,
            SquamataDiscarded,
            TohushaddollSent,
            MeshachrerSummoned,
            VoidSearched,
            ConstructSummoned,
            HedgehogSent,
            WendiReady,
            ControlSpellsReady,
            WendikuruhuSummoned,
            ControlShellPrepared
        }

        [Flags]
        private enum OpponentCapability
        {
            None = 0,
            Negate = 1,
            Removal = 2,
            Recursion = 4,
            Protection = 8,
            Floodgate = 16,
            QuickInteraction = 32,
            GraveInteraction = 64,
            RepeatableEngine = 128,
            AdvantageEngine = 256
        }

        private readonly HashSet<int> _usedEffects = new HashSet<int>();
        private readonly HashSet<ClientCard> _usedMeshSearches = new HashSet<ClientCard>();
        private int _specialSummonsThisTurn;
        private int _opponentSpecialSummonsThisTurn;
        private ChainInfo _coveredOpponentLink;
        private int _pendingFusionTargetId;
        private int _pendingFusionOwnerId;
        private CardType _voidRevealedType;
        private int _voidDiscardedMonsterId;
        private bool _shaddollExtraDeckLock;
        private bool _levTributedMesh;
        private bool _levOpponentTurnBranch;
        private bool _levMeshSearchResolved;
        private bool _waitingForLevWendi;
        private ClientCard _preferredSpiritProtectedTrigger;
        private LevRouteStage _levRouteStage;
        // Track a resolved starter, not merely an attempted activation.
        private bool _armageddonRouteStarted;
        private bool _armageddonMeshSummoned;
        private readonly HashSet<ClientCard> _answeredOpponentBodies = new HashSet<ClientCard>();

        private enum RouteGoal { Hold, Knight, LevStart, MeshRecovery, ConstructControl, FinishControl, FlipEngine, RestoreEngine }
        private sealed class RoutePlan
        {
            public RouteGoal Goal;
            public int Score;
            public string Reason;
            public readonly HashSet<int> Reserved = new HashSet<int>();
        }
        private sealed class FusionPlan
        {
            public int Target;
            public int Score;
            public List<ClientCard> Materials;
        }
        private bool _normalSpent;
        private bool _lastPlanLogged;
        private string _lastPlanKey;
        private readonly HashSet<string> _decisionMessages = new HashSet<string>();
        private readonly HashSet<ClientCard> _schismBodies = new HashSet<ClientCard>();
        private readonly HashSet<ClientCard> _pendingRemoval = new HashSet<ClientCard>();
        private readonly HashSet<ClientCard> _pendingNegation = new HashSet<ClientCard>();
        private readonly HashSet<ClientCard> _resolvedFieldEffects = new HashSet<ClientCard>();
        private readonly HashSet<ClientCard> _chosenFusionMaterials = new HashSet<ClientCard>();
        private List<ClientCard> _selectedFusionGroup;
        private int _queuedTargetOwner;
        private int _grystaCopyId;
        private bool _dragonDestroyMode;
        private bool _levRecycledWendikuruhu;
        private readonly HashSet<ClientCard> _properlySummonedFusions = new HashSet<ClientCard>();
        private readonly HashSet<ClientCard> _enemyExtraOrigin = new HashSet<ClientCard>();

        // V10: public-state tactical evaluation shared by the deck's actions.
        private enum EffectUse { Affect, Target, Destroy, Bounce, Flip, Negate, Send }
        private readonly HashSet<ClientCard> _enemySpentNegateBodies = new HashSet<ClientCard>();
        private readonly HashSet<int> _enemySpentNegateNames = new HashSet<int>();

        private int SourceLevel(int id)
        {
            NamedCard source = NamedCard.Get(id);
            return source == null ? 0 : source.Level;
        }
        private bool CanApplyEffect(ClientCard target, CardType sourceType, int sourceId, EffectUse use, bool targets)
        {
            if (target == null) return false;
            if (!target.IsFaceup() || target.IsDisabled()) return true;
            if (LikelyUnaffectedByActivatedEffect(target, sourceType, SourceLevel(sourceId))) return false;
            string text = EffectText(target).Replace('’', '\'');
            // Only infer self-protection from clauses about this card, not effects granting protection to other cards.
            foreach (string part in text.Split(new[] { '.', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = part.Trim();
                if (t.Contains("target 1") || t.Contains("target up to") || t.Contains("you can make") || t.Contains("your opponent controls cannot")) continue;
                if (targets && (t.StartsWith("cannot be targeted by") || t.StartsWith("this card cannot be targeted by") ||
                    t.StartsWith("neither player can target this card") || t.StartsWith("your opponent cannot target this card") || t.StartsWith("cannot be targeted or destroyed by") || t.StartsWith("this card cannot be targeted or destroyed by")))
                {
                    if (t.Contains("card effects") || t.Contains("effects") &&
                        (((sourceType & CardType.Monster) != 0 && t.Contains("monster")) ||
                         ((sourceType & CardType.Spell) != 0 && t.Contains("spell")) ||
                         ((sourceType & CardType.Trap) != 0 && t.Contains("trap")))) return false;
                }
                if (use == EffectUse.Destroy && (t.StartsWith("cannot be destroyed") || t.StartsWith("this card cannot be destroyed") || t.StartsWith("cannot be targeted or destroyed by") || t.StartsWith("this card cannot be targeted or destroyed by")) &&
                    (t.Contains("by card effects") || t.Contains("by your opponent's card effects") || t.Contains("by battle or card effects") ||
                     t.Contains("by battle or your opponent's card effects"))) return false;
            }
            return true;
        }
        private bool CanFlipEnemy(ClientCard target)
        {
            // Wendikuruhu does not target in its printed effect. Target protection is not effect immunity.
            return target != null && target.IsFaceup() && !target.HasType(CardType.Link) &&
                CanApplyEffect(target, CardType.Monster, CardId.ReeshaddollWendikuruhu, EffectUse.Flip, false);
        }
        private bool HasLiveEnemyNegate(ClientCard source, CardType sourceType, bool specialSummoned)
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any(c => EnemyNegateCanAnswer(c, source, sourceType, specialSummoned));
        }
        private bool EnemyNegateCanAnswer(ClientCard threat, ClientCard source, CardType sourceType, bool specialSummoned)
        {
            if (threat == null || !threat.IsFaceup() || threat.IsDisabled() ||
                _enemySpentNegateBodies.Contains(threat) || _enemySpentNegateNames.Contains(threat.Id)) return false;
            string text = EffectText(threat).Replace('’', '\'');
            foreach (string part in text.Split(new[] { '.', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = part.Trim();
                if (!(t.Contains("negate the activation") || t.Contains("negate that effect") || t.Contains("negate the effect"))) continue;
                if (!(t.Contains("quick effect") || t.StartsWith("when ") || t.StartsWith("if "))) continue;
                if (t.Contains("discard this card") || t.Contains("send this card from your hand")) continue;
                if ((t.Contains("special summoned monster") || t.Contains("special summoned monsters")) && !specialSummoned) continue;
                if ((t.Contains("spell/trap card or effect") || t.Contains("spell/trap card is activated")) && !t.Contains("monster effect") && (sourceType & CardType.Monster) != 0) continue;
                if ((t.Contains("monster activates its effect") || t.Contains("monster effect is activated") || t.Contains("monster effect:")) && (sourceType & CardType.Monster) == 0) continue;
                if (t.Contains("would special summon") || t.Contains("would be summoned")) continue;
                if (t.Contains("in your hand") && Enemy.Hand.Count == 0) continue;
                if (source != null && !t.Contains("negate the activation") && !CanApplyEffect(source, threat.IsMonster() ? CardType.Monster : threat.IsTrap() ? CardType.Trap : CardType.Spell, threat.Id, EffectUse.Negate, false))
                {
                    // CanApplyEffect's protection wording is relative to the target's opponent, also true here.
                    continue;
                }
                return true;
            }
            return false;
        }
        private bool HasActiveControlShell()
        {
            return LiveMonster(CardId.ReeshaddollWendikuruhu) && (HasGrystaBody() ||
                Bot.GetMonsters().Any(c=>c.HasType(CardType.Flip) && IsShaddollMonster(c)));
        }
        private bool ThreatenedMaterial(FusionPlan plan)
        {
            return plan != null && plan.Materials.Any(c=>c.Location==CardLocation.MonsterZone && EnemyThreatens(c));
        }
        private bool IsLiveSchism()
        {
            return Bot.GetSpells().Any(c=>c.IsCode(CardId.ShaddollSchism) && !c.IsDisabled());
        }
        private bool WindaBlocksFurtherSummon()
        {
            return _specialSummonsThisTurn >= 1 && (FunctionalWinda() || Enemy.GetMonsters().Any(c=>c.IsCode(CardId.ElShaddollWinda) && c.IsFaceup() && !c.IsDisabled()));
        }
        private bool IsMulcharmy(ClientCard c)
        {
            return c != null && c.IsCode(CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia);
        }
        private bool HasVoidPair(CardType revealed)
        {
            bool monster = ShaddollMainMonsterIds.Any(Bot.HasInDeck);
            bool spell = Bot.HasInDeck(CardId.LevShaddollFusion) || Bot.HasInDeck(CardId.ElShaddollFusion) || Bot.HasInDeck(CardId.ShaddollFusion);
            bool trap = Bot.HasInDeck(CardId.ShaddollSchism) || Bot.HasInDeck(CardId.ReshShaddollIncarnation);
            return revealed == CardType.Monster ? spell && trap : revealed == CardType.Spell ? monster && trap : revealed == CardType.Trap && monster && spell;
        }
        private bool VoidCanSearchNow()
        {
            return Bot.Hand.Any(c=>IsShaddollCard(c) && HasVoidPair(GetBaseType(c)));
        }
        private bool VoidHasPotentialPair()
        {
            return HasVoidPair(CardType.Monster) || HasVoidPair(CardType.Spell) || HasVoidPair(CardType.Trap);
        }

        // A control engine is a repeatable interaction, not simply a large monster.
        private bool FlipEngineLive()
        {
            return LiveMonster(CardId.ReeshaddollWendikuruhu) && Bot.GetMonsters().Any(c => c.HasType(CardType.Flip) && IsShaddollMonster(c));
        }
        private bool HasCopyFuel()
        {
            return Bot.Graveyard.Any(c => c.HasType(CardType.Flip) && IsShaddollMonster(c) && !c.IsCode(CardId.TohushaddollGrysta));
        }
        private bool HasGrystaBody()
        {
            return Bot.GetMonsters().Any(c => c.IsCode(CardId.TohushaddollGrysta));
        }
        private bool HasArielRecovery()
        {
            return Bot.Banished.Any(c => IsShaddollMonster(c) && (!c.HasType(CardType.Fusion) || _properlySummonedFusions.Contains(c))) && Bot.GetMonsterCount() < 5 &&
                (!FunctionalWinda() || _specialSummonsThisTurn == 0);
        }
        private bool EnemyDeckFusionWindow()
        {
            return Enemy.GetMonsters().Any(c => c.IsFaceup() && _enemyExtraOrigin.Contains(c));
        }
        private bool EnemyThreatens(ClientCard c)
        {
            return c != null && Duel.CurrentChainInfo.Any(link => link.ActivatePlayer == 1 && link.Targets.Contains(c));
        }
        private bool UsefulEnemyGrave()
        {
            return Enemy.Graveyard.Any(c => GraveDisruptionScore(c) >= 300);
        }
        private int ControlEngineValue(ClientCard c)
        {
            if (c == null || c.Location != CardLocation.MonsterZone || c.Controller != 0) return 0;
            if (c.IsCode(CardId.ReeshaddollWendikuruhu)) return FlipEngineLive() ? 260 : 155;
            if (c.IsCode(CardId.TohushaddollGrysta)) return HasCopyFuel() || LiveMonster(CardId.ReeshaddollWendikuruhu) ? 225 : 115;
            if (c.IsCode(CardId.NaelshaddollAriel) && (HasArielRecovery() || (LiveMonster(CardId.ReeshaddollWendikuruhu) && Bot.Banished.Any(IsShaddollMonster)))) return 145;
            if (c.IsCode(CardId.ElShaddollWinda) && c.IsFaceup() && !c.IsDisabled()) return 180;
            if (c.IsCode(CardId.ElShaddollMeshachrer) && !_usedMeshSearches.Contains(c)) return 100;
            return 0;
        }
        private int CopyEffectValue(int id)
        {
            // Grysta copies the FLIP effect, not the sent-to-GY effect. The
            // copied monster's own OPT is not Grysta's activation limit.
            if (id == CardId.ShaddollDragon)
                return Enemy.GetMonsters().Concat(Enemy.GetSpells()).Where(c => !PendingRemoval(c) && CanApplyEffect(c,CardType.Monster,CardId.TohushaddollGrysta,EffectUse.Bounce,true))
                    .Select(c => Math.Max(0, RemovalScore(c))).DefaultIfEmpty(0).Max() >= 700 ? 240 :
                    Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any(c=>!PendingRemoval(c) && CanApplyEffect(c,CardType.Monster,CardId.TohushaddollGrysta,EffectUse.Bounce,true)) ? 100 : -100;
            if (id == CardId.ShaddollSquamata)
                return Enemy.GetMonsters().Any(c => !PendingRemoval(c) && CanApplyEffect(c,CardType.Monster,CardId.TohushaddollGrysta,EffectUse.Destroy,true)) ? 145 : -100;
            if (id == CardId.NaelshaddollAriel)
                return HasArielRecovery() ? (Bot.Banished.Any(c => c.IsCode(CardId.TohushaddollGrysta)) && !HasGrystaBody() ? 260 : 170) : -100;
            if (id == CardId.ReeshaddollWendi)
                return Bot.GetMonsterCount() < 5 && (!FunctionalWinda() || _specialSummonsThisTurn == 0) &&
                    ShaddollMainMonsterIds.Any(x => x != CardId.ReeshaddollWendi && Bot.HasInDeck(x)) ?
                    (LiveMonster(CardId.ReeshaddollWendikuruhu) && Bot.GetMonsters().Count(c=>c.HasType(CardType.Flip)) <= 1 ? 270 :
                     !HasGrystaBody() && Bot.HasInDeck(CardId.TohushaddollGrysta) ? 250 : 155) : -100;
            if (id == CardId.ShaddollBeast) return Bot.Deck.Count >= 2 ? (Bot.Hand.Count <= 2 ? 205 : 150) : -100;
            if (id == CardId.ShaddollHedgehog) return ShaddollSpellTrapIds.Any(Bot.HasInDeck) ? (!Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism) ? 210 : 140) : -100;
            return -100;
        }
        private int BestCopyEffectValue()
        {
            return Bot.Graveyard.Where(c => IsShaddollMonster(c) && c.HasType(CardType.Flip) && !c.IsCode(CardId.TohushaddollGrysta))
                .Select(c => CopyEffectValue(c.Id)).DefaultIfEmpty(-100).Max();
        }
        private int FlipDownControlScore(ClientCard c)
        {
            if (c == null || !c.IsFaceup() || c.HasType(CardType.Link)) return -10000;
            if (c.Controller == 1) return CanFlipEnemy(c) && IsWorthWendikuruhuInterruption(c) ? 200 + OpponentThreatScore(c) / 4 : -10000;
            if (!IsShaddollMonster(c) || !c.HasType(CardType.Flip) || c.HasType(CardType.Fusion)) return -10000;
            if (c.IsCode(CardId.TohushaddollGrysta) && HasCopyFuel()) return 360;
            if (c.IsCode(CardId.NaelshaddollAriel) && HasArielRecovery()) return 240;
            return 100 + (EffectUsed(c.Id) ? 45 : 0) + Math.Max(0, CopyEffectValue(c.Id)) / 4;
        }
        private int FlipUpControlScore(ClientCard c)
        {
            if (c == null || c.Controller != 0 || !c.IsFacedown() || !c.HasType(CardType.Flip)) return -10000;
            int ownEffect = FlipUpScore(c);
            // Even a spent Flip monster can fund a meaningful position change.
            // Do not confuse its OPT with Wendikuruhu's separate OPT.
            bool usefulReset = Bot.GetMonsters().Any(x=>x.IsFaceup() && FlipDownControlScore(x)>0);
            bool usefulDisruption = Enemy.GetMonsters().Any(IsWorthWendikuruhuInterruption);
            return Math.Max(ownEffect, usefulDisruption || usefulReset ? 65 : -100);
        }
        private IList<ClientCard> SelectUsefulFlips(IList<ClientCard> cards, int min, int max, Func<ClientCard,int> score)
        {
            List<ClientCard> good = cards.Where(c => score(c) > 0).OrderByDescending(score).ToList();
            int desired = good.Count;
            // Avoid exposing all future flip fuel when there is no meaningful
            // reset target. The engine can request repeated one-card selections.
            if (good.Count > 1 && good.All(c=>c.IsFacedown() && c.Controller==0) &&
                !Bot.GetMonsters().Any(c=>c.IsFaceup() && FlipDownControlScore(c)>0) &&
                !Enemy.GetMonsters().Any(IsWorthWendikuruhuInterruption)) desired = 1;
            int count = Math.Min(max, Math.Max(min, desired));
            if (count <= 0) return new List<ClientCard>();
            IList<ClientCard> result = FillSelection(good, cards, count, count);
            TraceDecision("FLIP", string.Join(",", result.Select(c => c.Id)));
            return result;
        }


        // All policy queries are rebuilt from public current state, not from the original starter.
        // The framework offers legal actions; this bounded route evaluator ranks their strategic purpose.
        private RoutePlan EvaluateRoute()
        {
            RoutePlan best = new RoutePlan { Goal = RouteGoal.Hold, Score = ControlBoardScore(), Reason = "preserve resources" };
            if (Duel.Player != 0 || !IsMainPhase() || HasSafeLethalOnBoard()) return best;
            bool mesh = LiveMonster(CardId.ElShaddollMeshachrer);
            bool construct = LiveMonster(CardId.ElShaddollConstruct);
            bool lev = LiveLev();
            bool wendi = !EffectUsed(CardId.ReeshaddollWendi) && (Bot.HasInHand(CardId.ReeshaddollWendi) ||
                (!EffectUsed(CardId.ShaddollHedgehog) && Bot.HasInDeck(CardId.ShaddollHedgehog) && Bot.HasInDeck(CardId.ReeshaddollWendi)));
            bool el = Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion);
            bool meshSearch = HasLiveMeshSearch();
            bool levDumpPending = Duel.SolvingChainIndex > 0 && PromptIs(CardId.LevShaddollFusion);
            bool canAccessLev = lev || (!EffectUsed(CardId.LevShaddollFusion) &&
                (Bot.HasInHand(CardId.LevShaddollFusion) || (meshSearch && Bot.HasInDeck(CardId.LevShaddollFusion))));
            bool canAccessVoid = VoidHasPotentialPair() && !EffectUsed(CardId.VoidUnleashing) && (Bot.HasInHand(CardId.VoidUnleashing) ||
                (Bot.HasInDeck(CardId.VoidUnleashing) && (meshSearch ||
                 (Bot.HasInExtra(CardId.InfernoidEvil) && (levDumpPending || (!EffectUsed(CardId.LevShaddollFusion) && canAccessLev))))));
            bool controlAccess = (el || (canAccessVoid && Bot.HasInDeck(CardId.ElShaddollFusion))) &&
                Bot.HasInExtra(CardId.ReeshaddollWendikuruhu) && wendi;
            if (!FunctionalWinda() && !HasPreparedLevControlBoard())
            {
                if (!_normalSpent && Bot.HasInHand(CardId.ArmageddonKnight) && CanStartArmageddonRoute() &&
                    Bot.HasInDeck(CardId.ShaddollSquamata) && !mesh && !construct)
                    OfferRoute(ref best, RouteGoal.Knight, 180, "Knight into Mesh", CardId.ArmageddonKnight);
                if (!lev && !mesh && !construct && !EffectUsed(CardId.LevShaddollFusion) &&
                    Bot.HasInHand(CardId.LevShaddollFusion) && CanStartLevPrimaryRoute())
                    OfferRoute(ref best, RouteGoal.LevStart, 205, "Apkallone into Mesh", CardId.LevShaddollFusion);
                if (mesh && !EffectUsed(CardId.LevShaddollFusion + 1) && Bot.HasInExtra(CardId.ElShaddollConstruct) &&
                    canAccessLev && controlAccess)
                    OfferRoute(ref best, RouteGoal.MeshRecovery, 235, "live Mesh can reach Construct control", CardId.ReeshaddollWendi, CardId.ElShaddollFusion, CardId.ShaddollSchism, CardId.LevShaddollFusion);
                if (construct && controlAccess && (Bot.HasInHand(CardId.ReeshaddollWendi) || lev))
                    OfferRoute(ref best, RouteGoal.ConstructControl, 240, "Construct plus Wendi control extension", CardId.ReeshaddollWendi, CardId.ElShaddollFusion, CardId.ShaddollSchism);
                if (LiveMonster(CardId.ReeshaddollWendikuruhu) && Bot.GetMonsters().Any(c => c.IsFacedown() && c.IsCode(CardId.NaelshaddollAriel)) &&
                    Bot.Banished.Any(c => c.IsCode(CardId.ShaddollSquamata)) && !EffectUsed(CardId.NaelshaddollAriel))
                    OfferRoute(ref best, RouteGoal.FinishControl, 230, "Ariel restores face-down flip fuel", CardId.ShaddollSchism);
            }
            if (HasActiveControlShell()) best = new RoutePlan { Goal = RouteGoal.Hold, Score = ControlBoardScore(), Reason = "maintain existing flip engine" };
            if (best.Goal == RouteGoal.Hold && LiveMonster(CardId.ReeshaddollWendikuruhu) &&
                !EffectUsed(CardId.ReeshaddollWendikuruhu) && Bot.GetMonsters().Any(c=>FlipUpControlScore(c)>0) &&
                (Bot.GetMonsters().Any(c => c.IsFaceup() && FlipDownControlScore(c) > 0) || Enemy.GetMonsters().Any(IsWorthWendikuruhuInterruption) ||
                 Bot.GetMonsters().Any(c => c.IsFacedown() && c.IsCode(CardId.NaelshaddollAriel) && HasArielRecovery())))
                OfferRoute(ref best, RouteGoal.FlipEngine, best.Score + 15, "flip for recurring interaction");
            if (best.Goal == RouteGoal.Hold && !LiveMonster(CardId.ReeshaddollWendikuruhu) &&
                Bot.Graveyard.Any(c => c.IsCode(CardId.ReeshaddollWendikuruhu)) &&
                Bot.HasInSpellZone(CardId.ReshShaddollIncarnation) && !EffectUsed(CardId.ReshShaddollIncarnation) && Bot.GetMonsterCount()<5)
                OfferRoute(ref best, RouteGoal.RestoreEngine, best.Score + 10, "recover flip engine when revival is available");
            return best;
        }
        private void OfferRoute(ref RoutePlan best, RouteGoal goal, int score, string reason, params int[] reserved)
        {
            if (score <= best.Score) return;
            best = new RoutePlan { Goal = goal, Score = score, Reason = reason };
            foreach (int id in reserved) best.Reserved.Add(id);
        }
        private RoutePlan CurrentPlan()
        {
            RoutePlan p = EvaluateRoute();
            string key = p.Goal + ":" + p.Reason;
            if (!_lastPlanLogged || key != _lastPlanKey)
            {
                _lastPlanLogged = true; _lastPlanKey = key;
                WindBot.Logger.WriteLine("[Shaddoll/PLAN] " + key + " reserve=" + string.Join(",", p.Reserved));
            }
            return p;
        }
        private void TraceDecision(string category, string message)
        {
            string key = "[Shaddoll/" + category + "] " + message;
            if (_decisionMessages.Add(key)) WindBot.Logger.WriteLine(key);
        }
        private bool LiveMonster(int id)
        {
            return Bot.GetMonsters().Any(c => c.IsCode(id) && c.IsFaceup() && !c.IsDisabled());
        }
        private bool HasLiveMeshSearch()
        {
            return Bot.LifePoints > 1600 && Bot.GetMonsters().Any(c => c.IsCode(CardId.ElShaddollMeshachrer) &&
                c.IsFaceup() && !c.IsDisabled() && (!_usedMeshSearches.Contains(c) ||
                (Duel.SolvingChainIndex > 0 && PromptIs(CardId.ElShaddollMeshachrer))));
        }
        private bool LiveLev()
        {
            return Bot.GetSpells().Any(c => c.IsCode(CardId.LevShaddollFusion) && c.IsFaceup() && !c.IsDisabled());
        }
        private bool ReservedForRoute(ClientCard c)
        {
            return c != null && CurrentPlan().Reserved.Contains(c.Id) && Bot.Hand.Count(h => h.IsCode(c.Id)) <= 1;
        }
        private bool IsHandTrap(ClientCard c)
        {
            return c.IsCode(CardId.MaxxC, CardId.AshBlossomJoyousSpring, CardId.EffectVeiler, CardId.DrollLockBird,
                CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia);
        }
        private static readonly int[] ShaddollMainMonsterIds =
        {
            CardId.ShaddollSquamata,
            CardId.TohushaddollGrysta,
            CardId.ShaddollHedgehog,
            CardId.ReeshaddollWendi,
            CardId.NaelshaddollAriel,
            CardId.ShaddollDragon,
            CardId.ShaddollBeast
        };

        private static readonly int[] ShaddollSpellTrapIds =
        {
            CardId.LevShaddollFusion,
            CardId.ShaddollFusion,
            CardId.ElShaddollFusion,
            CardId.ShaddollSchism,
            CardId.ReshShaddollIncarnation
        };

        private static readonly int[] ShaddollFusionIds =
        {
            CardId.ElShaddollMeshachrer,
            CardId.ElShaddollConstruct,
            CardId.ElShaddollApkallone,
            CardId.ElShaddollWinda,
            CardId.ElShaddollShekhinaga,
            CardId.ElShaddollAnoyatyllis,
            CardId.ReeshaddollWendikuruhu
        };

        public Shaddoll2025Executor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Opponent-turn cards are evaluated first. Each predicate declines
            // redundant responses when the current chain is already covered.
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, CardId.MulcharmyFuwalos, MulcharmyActivate);
            AddExecutor(ExecutorType.Activate, CardId.MulcharmyPurulia, MulcharmyActivate);
            AddExecutor(ExecutorType.Activate, CardId.DrollLockBird, DrollActivate);
            AddExecutor(ExecutorType.Activate, CardId.AshBlossomJoyousSpring,
                AshBlossomActivate);
            AddExecutor(ExecutorType.Activate, CardId.EffectVeiler, EffectVeilerActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollShekhinaga, ShekhinagaActivate);
            AddExecutor(ExecutorType.Activate, CardId.ReeshaddollWendikuruhu, WendikuruhuActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollSchism, SchismActivate);
            AddExecutor(ExecutorType.Activate, CardId.ReshShaddollIncarnation, ReshActivate);
            // Route actions use separate Lev executors so the real idle-command
            // scheduler cannot skip a stage. Lev starts first, Mesh searches,
            // Void converts the reveal, and only then may Lev tribute Mesh.
            AddExecutor(ExecutorType.Activate, CardId.LevShaddollFusion, LevPrimaryActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollMeshachrer, MeshachrerActivate);
            AddExecutor(ExecutorType.Activate, CardId.VoidUnleashing, VoidUnleashingActivate);
            AddExecutor(ExecutorType.Activate, CardId.LevShaddollFusion, LevTributeActivate);

            // Shaddoll and Fusion trigger effects. Selection is owned by the
            // actual resolving chain link. Keep them ahead of optional Quick
            // Fusions so Construct -> Hedgehog finishes the Wendi search before
            // El Shaddoll Fusion is allowed to consume the control materials.
            AddExecutor(ExecutorType.Activate, CardId.ShaddollSquamata, SquamataActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollHedgehog, HedgehogActivate);
            AddExecutor(ExecutorType.Activate, CardId.ReeshaddollWendi, WendiActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollDragon, DragonActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollBeast, BeastActivate);
            AddExecutor(ExecutorType.Activate, CardId.NaelshaddollAriel, ArielActivate);
            AddExecutor(ExecutorType.Activate, CardId.TohushaddollGrysta, TohushaddollGrystaActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollConstruct, ConstructActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollApkallone, ApkalloneActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollWinda, FusionRecoveryActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollAnoyatyllis, FusionRecoveryActivate);
            AddExecutor(ExecutorType.Activate, CardId.InfernoidEvil, InfernoidEvilActivate);
            AddExecutor(ExecutorType.Activate, CardId.LunalightPerfumeDancer, PerfumeDancerActivate);
            AddExecutor(ExecutorType.Activate, CardId.GranguignolDuskDragon, GranguignolActivate);

            // Flexible Fusion effects and extenders are intentionally after the
            // committed Lev route and its triggers. They remain available on the
            // opponent's turn, where Lev's ignition effects are not legal offers.
            AddExecutor(ExecutorType.Activate, CardId.ShaddollFusion, ShaddollDeckFusionActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollFusion, ElShaddollFusionActivate);
            AddExecutor(ExecutorType.Activate, CardId.DracotailFaimena, FaimenaActivate);
            AddExecutor(ExecutorType.Activate, CardId.BlazingCartesia, CartesiaActivate);
            AddExecutor(ExecutorType.Activate, CardId.FusionDeployment, FusionDeploymentActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollFusion, ShaddollFusionActivate);
            AddExecutor(ExecutorType.Activate, CardId.TearlamentsScheiren, ScheirenActivate);
            AddExecutor(ExecutorType.Activate, CardId.ArmageddonKnight, ArmageddonKnightActivate);

            // Establish a flip body only after the non-Normal-Summon starters.
            AddExecutor(ExecutorType.Summon, CardId.ArmageddonKnight, ArmageddonKnightSummon);
            AddExecutor(ExecutorType.Summon, CardId.BlazingCartesia, CartesiaSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollSquamata, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollDragon, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ReeshaddollWendi, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollHedgehog, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.NaelshaddollAriel, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollBeast, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.TohushaddollGrysta, SetShaddollMonster);

            AddExecutor(ExecutorType.SpellSet, SetSpellTrap);
            AddExecutor(ExecutorType.Repos, ShaddollMonsterRepos);
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            _usedEffects.Clear();
            _usedMeshSearches.Clear();
            _specialSummonsThisTurn = 0;
            _opponentSpecialSummonsThisTurn = 0;
            _coveredOpponentLink = null;
            _pendingFusionTargetId = 0; _pendingFusionOwnerId = 0;
            _voidRevealedType = 0;
            _voidDiscardedMonsterId = 0;
            _shaddollExtraDeckLock = false;
            _levTributedMesh = false; _levRecycledWendikuruhu = false;
            _levOpponentTurnBranch = false;
            _levMeshSearchResolved = false;
            _waitingForLevWendi = false;
            _preferredSpiritProtectedTrigger = null;
            _levRouteStage = LevRouteStage.None;
            _armageddonRouteStarted = false;
            _armageddonMeshSummoned = false;
            _answeredOpponentBodies.Clear();
            _normalSpent = false;
            _lastPlanLogged = false;
            _decisionMessages.Clear();
            _resolvedFieldEffects.Clear();
            _enemySpentNegateBodies.Clear(); _enemySpentNegateNames.Clear();
            _pendingRemoval.Clear(); _pendingNegation.Clear(); _chosenFusionMaterials.Clear(); _selectedFusionGroup = null; _queuedTargetOwner = 0; _grystaCopyId = 0;
        }

        public override void OnNewPhase()
        {
            base.OnNewPhase(); CurrentPlan();
        }
        public override void OnDraw(int player)
        {
            base.OnDraw(player); CurrentPlan();
        }
        public override void OnSelectChain(IList<ClientCard> cards)
        {
            base.OnSelectChain(cards); CurrentPlan();
            _preferredSpiritProtectedTrigger = null;
            if (cards == null || !Enemy.GetMonsters().Any(c => c.IsFaceup() &&
                !c.IsDisabled() && c.IsCode(CardId.BlueEyesSpiritDragon)))
                return;
            List<ClientCard> graveTriggers = cards.Where(c => c != null &&
                c.Controller == 0 && c.Location == CardLocation.Grave &&
                IsSpiritOrderableGraveTrigger(c)).ToList();
            if (graveTriggers.Count > 1)
                _preferredSpiritProtectedTrigger = graveTriggers
                    .OrderByDescending(SpiritProtectedTriggerScore)
                    .First();
        }

        public override void OnMove(
            ClientCard card,
            int previousControler,
            int previousLocation,
            int currentControler,
            int currentLocation)
        {
            if (card != null && previousLocation == (int)CardLocation.MonsterZone &&
                (currentLocation != (int)CardLocation.MonsterZone || previousControler != currentControler))
            {
                _schismBodies.Remove(card); _resolvedFieldEffects.Remove(card); _enemyExtraOrigin.Remove(card); _enemySpentNegateBodies.Remove(card);
            }
            if (card != null && currentControler == 1 && currentLocation == (int)CardLocation.MonsterZone && previousLocation == (int)CardLocation.Extra)
                _enemyExtraOrigin.Add(card);
            if (card != null && currentControler == 0 && currentLocation == (int)CardLocation.MonsterZone &&
                previousLocation == (int)CardLocation.Extra)
            {
                _properlySummonedFusions.Add(card);
                ChainInfo link = Duel.GetCurrentSolvingChainInfo();
                if (link != null && link.ActivatePlayer == 0 && link.IsActivateCode(CardId.ShaddollSchism))
                    _schismBodies.Add(card);
            }
            if (card != null && (currentLocation == (int)CardLocation.Extra || currentLocation == (int)CardLocation.Deck || currentLocation == (int)CardLocation.Hand))
                _properlySummonedFusions.Remove(card);
            if (card != null && previousLocation == (int)CardLocation.MonsterZone &&
                (currentLocation != (int)CardLocation.MonsterZone || currentControler != previousControler))
                _answeredOpponentBodies.Remove(card);
            if (card != null && currentControler == 0)
            {
                ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
                if (solving != null && solving.ActivatePlayer == 0)
                {
                    if (CanDevelopArmageddonRoute() && solving.IsActivateCode(CardId.ArmageddonKnight) &&
                        card.IsCode(CardId.ShaddollSquamata) &&
                        currentLocation == (int)CardLocation.Grave)
                        _armageddonRouteStarted = true;
                    if (_armageddonRouteStarted &&
                        solving.IsActivateCode(CardId.TohushaddollGrysta) &&
                        card.IsCode(CardId.ElShaddollMeshachrer) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        _armageddonMeshSummoned = true;

                    if (solving.IsActivateCode(CardId.LevShaddollFusion) &&
                        card.IsCode(CardId.ElShaddollApkallone) &&
                        previousLocation == (int)CardLocation.Extra &&
                        currentLocation == (int)CardLocation.Grave)
                        AdvanceLevRoute(LevRouteStage.ApkalloneSent);
                    else if (solving.IsActivateCode(CardId.ElShaddollApkallone) &&
                        card.IsCode(CardId.ShaddollSquamata) &&
                        currentLocation == (int)CardLocation.Grave)
                        AdvanceLevRoute(LevRouteStage.SquamataDiscarded);
                    else if ((solving.IsActivateCode(CardId.ShaddollSquamata) ||
                        (IsLevRouteActive() && solving.IsActivateCode(CardId.ElShaddollApkallone))) &&
                        card.IsCode(CardId.TohushaddollGrysta) &&
                        currentLocation == (int)CardLocation.Grave)
                        AdvanceLevRoute(LevRouteStage.TohushaddollSent);
                    else if (solving.IsActivateCode(CardId.TohushaddollGrysta) &&
                        card.IsCode(CardId.ElShaddollMeshachrer) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.MeshachrerSummoned);
                    else if (solving.IsActivateCode(CardId.ElShaddollMeshachrer) &&
                        currentLocation == (int)CardLocation.Hand)
                    {
                        _levMeshSearchResolved = true;
                        if (card.IsCode(CardId.VoidUnleashing))
                            AdvanceLevRoute(LevRouteStage.VoidSearched);
                    }
                    else if (solving.IsActivateCode(CardId.LevShaddollFusion) &&
                        card.IsCode(CardId.ElShaddollConstruct) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.ConstructSummoned);
                    else if (solving.IsActivateCode(CardId.ElShaddollConstruct) &&
                        card.IsCode(CardId.ShaddollHedgehog) &&
                        currentLocation == (int)CardLocation.Grave)
                    {
                        _waitingForLevWendi = true;
                        AdvanceLevRoute(LevRouteStage.HedgehogSent);
                    }
                    else if (solving.IsActivateCode(CardId.ShaddollHedgehog) &&
                        card.IsCode(CardId.ReeshaddollWendi) &&
                        currentLocation == (int)CardLocation.Hand)
                    {
                        _waitingForLevWendi = false;
                        AdvanceLevRoute(LevRouteStage.WendiReady);
                    }
                    else if (solving.IsActivateCode(CardId.VoidUnleashing) &&
                        card.IsCode(CardId.ElShaddollFusion, CardId.ShaddollSchism) &&
                        currentLocation == (int)CardLocation.Hand &&
                        Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion) &&
                        Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism))
                        AdvanceLevRoute(LevRouteStage.ControlSpellsReady);
                    else if (solving.IsActivateCode(CardId.ElShaddollFusion) &&
                        card.IsCode(CardId.ReeshaddollWendikuruhu) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.WendikuruhuSummoned);
                    else if (solving.IsActivateCode(CardId.NaelshaddollAriel) &&
                        card.IsCode(CardId.ShaddollSquamata) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.ControlShellPrepared);
                }
            }
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
            if (Duel.SolvingChainIndex == 0 && IsMainPhase()) CurrentPlan();
        }

        public override void OnChainEnd()
        {
            base.OnChainEnd();
            // If Hedgehog resolved without adding Wendi (negated or no longer
            // available), release the planned wait so legal fallback Fusions
            // can be reconsidered instead of deadlocking the turn.
            if (_levRouteStage == LevRouteStage.HedgehogSent &&
                EffectUsed(CardId.ShaddollHedgehog) &&
                !Bot.HasInHand(CardId.ReeshaddollWendi))
                _levRouteStage = LevRouteStage.ConstructSummoned;
            if (_waitingForLevWendi && EffectUsed(CardId.ShaddollHedgehog) &&
                !Bot.HasInHand(CardId.ReeshaddollWendi))
                _waitingForLevWendi = false;
            _coveredOpponentLink = null;
            _pendingFusionTargetId = 0; _pendingFusionOwnerId = 0;
            _voidRevealedType = 0;
            _levTributedMesh = false; _levRecycledWendikuruhu = false;
            _pendingRemoval.Clear(); _pendingNegation.Clear(); _chosenFusionMaterials.Clear(); _selectedFusionGroup = null; _grystaCopyId = 0; _queuedTargetOwner = 0;
            CurrentPlan();
        }

        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            base.OnPosChange(card, previousPosition, currentPosition);
            if (card != null && (currentPosition & (int)CardPosition.FaceDown) != 0)
            { _schismBodies.Remove(card); _resolvedFieldEffects.Remove(card); }
        }

        public override void OnChainSolved(int chainIndex)
        {
            base.OnChainSolved(chainIndex);
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 1 && link.ActivateLocation == CardLocation.MonsterZone && link.RelatedCard != null)
            {
                // A narrow inference: a field quick negate responding directly to our qualifying effect.
                // Never mark a multifunction card's search/recovery as a spent negate merely because it activated.
                var links = Duel.CurrentChainInfo.ToList();
                int idx = links.IndexOf(link);
                string t = EffectText(link.RelatedCard).Replace('’', '\'');
                if (idx > 0 && links[idx-1].ActivatePlayer == 0 && links[idx-1].RelatedCard != null &&
                    t.Contains("quick effect") && t.Contains("negate the activation") &&
                    t.Contains("when a special summoned monster activates its effect") &&
                    links[idx-1].ActivateLocation == CardLocation.MonsterZone && links[idx-1].RelatedCard.IsSpecialSummoned)
                {
                    _enemySpentNegateBodies.Add(link.RelatedCard);
                    if (t.Contains("once per turn")) _enemySpentNegateNames.Add(link.RelatedCard.Id);
                }
            }
            if (link != null && !Duel.IsCurrentSolvingChainNegated() && link.ActivatePlayer == 1 && (link.ActivateLocation == CardLocation.MonsterZone || link.ActivateLocation == CardLocation.Hand) &&
                link.RelatedCard != null && link.RelatedCard.Location == CardLocation.MonsterZone)
                _resolvedFieldEffects.Add(link.RelatedCard);
        }

        public override void OnSpSummoned()
        {
            if (Duel.LastSummonPlayer == 0)
                _specialSummonsThisTurn++;
            else if (Duel.LastSummonPlayer == 1)
                _opponentSpecialSummonsThisTurn++;
            base.OnSpSummoned();
            CurrentPlan();
        }

        public override IList<ClientCard> OnSelectCard(
            IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (cards == null || cards.Count == 0)
                return base.OnSelectCard(cards, min, max, hint, cancelable);
            CurrentPlan();

            if (_queuedTargetOwner != 0 && Duel.SolvingChainIndex == 0 && cards.All(c=>c.Controller==1) &&
                (hint == HintMsg.Target || hint == HintMsg.Disable || hint == HintMsg.Destroy || hint == HintMsg.ReturnToHand))
            {
                int queued = _queuedTargetOwner;
                bool negate = queued == CardId.ElShaddollApkallone;
                _queuedTargetOwner = 0;
                return SelectCoordinatedTarget(cards,min,max,negate,cancelable,false,queued);
            }

            // SelectSubGroup issues repeated SelectUnselect packets after the
            // original chain owner is no longer available through PromptIs.
            // _voidRevealedType stays live until chain end and identifies every
            // search/discard step of the same Void resolution.
            if (PromptIs(CardId.VoidUnleashing) || (_voidRevealedType != 0 && GetPromptOwnerId() == 0))
            {
                if (hint == HintMsg.AddToHand)
                {
                    if (cards.All(c=>c.IsCode(CardId.InfernoidEvil))) return SelectIds(cards,min,max,CardId.InfernoidEvil);
                    return SelectVoidSearch(cards, min, max);
                }
                if (hint == HintMsg.Discard)
                {
                    IList<ClientCard> selected = SelectByScore(cards, min, max, VoidDiscardScore);
                    ClientCard discarded = selected.FirstOrDefault();
                    _voidDiscardedMonsterId = discarded != null && IsShaddollMonster(discarded)
                        ? discarded.Id : 0;
                    return selected;
                }
            }

            if (PromptIs(CardId.ShaddollSchism) && cards.All(c => c.Controller == 1) &&
                (hint == HintMsg.ToGrave || hint == HintMsg.Target))
                return SelectCoordinatedTarget(cards, min, max, false, cancelable, true);

            if (PromptIs(CardId.LevShaddollFusion))
            {
                if (hint == HintMsg.ToGrave && cards.All(c => c.Location == CardLocation.Extra))
                    return SelectByScore(cards, min, max, LevDumpScore);
                if (hint == HintMsg.SpSummon)
                    return SelectFusionTarget(cards, min, max, CardId.LevShaddollFusion);
            }

            if (PromptIs(CardId.FusionDeployment))
            {
                if (hint == HintMsg.Confirm)
                    return SelectIds(cards, min, max, CardId.GranguignolDuskDragon);
                if (hint == HintMsg.SpSummon)
                    return SelectIds(cards, min, max, CardId.BlazingCartesia);
            }

            if (IsFusionPromptOwner())
            {
                if (hint == HintMsg.SpSummon && cards.Any(c => c.Location == CardLocation.Extra))
                    return SelectFusionTarget(cards, min, max, GetPromptOwnerId());
                if (hint == HintMsg.FusionMaterial ||
                    (PromptIs(CardId.TohushaddollGrysta) && hint == HintMsg.Remove))
                    return SelectPlannedMaterials(cards, min, max);
            }

            if (PromptIs(CardId.ElShaddollMeshachrer))
            {
                if (hint == HintMsg.AddToHand)
                    return SelectByScore(cards, min, max, MeshSearchScore);
                if (hint == HintMsg.SpSummon)
                    return SelectByScore(cards, min, max, RevivalScore);
            }

            if (PromptIs(CardId.ElShaddollApkallone))
            {
                if (hint == HintMsg.Disable || hint == HintMsg.Target)
                    return SelectCoordinatedTarget(cards, min, max, true, cancelable);
                if (hint == HintMsg.AddToHand)
                    return SelectByScore(cards, min, max, ApkalloneSearchScore);
                if (hint == HintMsg.Discard)
                    return SelectByScore(cards, min, max, ApkalloneDiscardScore);
            }

            if (PromptIs(CardId.ReshShaddollIncarnation))
            {
                if (hint == HintMsg.SpSummon || hint == HintMsg.Target)
                    return SelectByScore(cards, min, max, RevivalScore);
                if (hint == HintMsg.Remove)
                    return SelectByScore(cards, min, max, ReshCostScore);
                if (hint == HintMsg.PosChange || hint == HintMsg.Position)
                {
                    if (cards.Any(c => c.Controller == 0 && c.IsFacedown()))
                        return SelectByScore(cards, min, max, FlipUpScore);
                    return SelectByScore(cards, min, max, ResetFlipScore);
                }
            }

            if (PromptIs(CardId.InfernoidEvil) && hint == HintMsg.AddToHand)
                return SelectIds(cards, min, max, CardId.VoidUnleashing);

            if (PromptIs(CardId.ElShaddollConstruct) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, ConstructSendScore);

            if (PromptIs(CardId.GranguignolDuskDragon) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, LevDumpScore);

            if (PromptIs(CardId.ShaddollSquamata))
            {
                if (hint == HintMsg.ToGrave)
                    return SelectByScore(cards, min, max, SquamataSendScore);
                if (hint == HintMsg.Destroy || hint == HintMsg.Target)
                    return SelectCoordinatedTarget(cards, min, max, false, cancelable);
            }

            if (PromptIs(CardId.ShaddollHedgehog))
            {
                if (hint == HintMsg.AddToHand)
                {
                    bool monstersOnly = cards.All(c => c.IsMonster());
                    return SelectByScore(cards, min, max,
                        monstersOnly ? (Func<ClientCard, int>)HedgehogMonsterSearchScore : ShaddollSearchScore);
                }
            }

            if (PromptIs(CardId.ArmageddonKnight) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, ArmageddonSendScore);

            if (PromptIs(CardId.ReeshaddollWendi) && hint == HintMsg.SpSummon)
                return SelectByScore(cards, min, max, WendiSummonScore);

            if (PromptIs(CardId.ShaddollDragon) &&
                (hint == HintMsg.Destroy || hint == HintMsg.ReturnToHand || hint == HintMsg.Target))
                return SelectCoordinatedTarget(cards, min, max, false, cancelable);

            if (PromptIs(CardId.NaelshaddollAriel))
            {
                if (hint == HintMsg.Remove)
                    return SelectArielTargets(cards, min, max);
                if (hint == HintMsg.SpSummon || (hint == HintMsg.Target && cards.All(c=>c.Location==CardLocation.Removed)))
                    return SelectByScore(cards, min, max, ArielRevivalScore);
            }

            if (PromptIs(CardId.TohushaddollGrysta) && hint == HintMsg.Target && cards.All(c => c.Controller == 0 && c.Location == CardLocation.Grave && c.HasType(CardType.Flip)))
            {
                IList<ClientCard> selected = SelectByScore(cards, min, max, FlipCopyScore);
                if (selected.Count > 0) { _grystaCopyId = selected[0].Id; TraceDecision("COPY", _grystaCopyId.ToString()); }
                return selected;
            }
            if (PromptIs(CardId.TohushaddollGrysta) && _grystaCopyId != 0)
            {
                if (_grystaCopyId == CardId.ShaddollDragon || _grystaCopyId == CardId.ShaddollSquamata)
                {
                    if (cards.All(c => c.Controller == 1)) return SelectCoordinatedTarget(cards,min,max,false,cancelable);
                }
                if (_grystaCopyId == CardId.ReeshaddollWendi && hint == HintMsg.SpSummon)
                    return SelectByScore(cards,min,max,WendiSummonScore);
                if (_grystaCopyId == CardId.NaelshaddollAriel && (hint == HintMsg.SpSummon || hint == HintMsg.Target) && cards.All(c=>c.Location==CardLocation.Removed))
                    return SelectByScore(cards,min,max,ArielRevivalScore);
                if (_grystaCopyId == CardId.ShaddollHedgehog && hint == HintMsg.AddToHand)
                    return SelectByScore(cards,min,max,ShaddollSearchScore);
                if (_grystaCopyId == CardId.ShaddollBeast && (hint == HintMsg.Discard || hint == HintMsg.ToGrave))
                    return SelectByScore(cards,min,max,DiscardScore);
            }

            if (PromptIs(CardId.ReeshaddollWendikuruhu))
            {
                if (hint == HintMsg.PosChange || hint == HintMsg.Position ||
                    (hint == HintMsg.Target && cards.All(c => c.Location == CardLocation.MonsterZone)))
                {
                    if (cards.Any(c => c.IsFacedown())) return SelectUsefulFlips(cards, min, max, FlipUpControlScore);
                    return SelectUsefulFlips(cards, min, max, FlipDownControlScore);
                }
                if (hint == HintMsg.AddToHand || hint == HintMsg.Target)
                    return SelectByScore(cards, min, max, ShaddollRecoveryScore);
            }

            if (PromptIs(CardId.ElShaddollShekhinaga) && (hint == HintMsg.ToGrave || hint == HintMsg.Discard))
                return SelectByScore(cards, min, max, DiscardScore);

            if (IsFusionRecoveryPrompt() && (hint == HintMsg.AddToHand || hint == HintMsg.Target))
                return SelectByScore(cards, min, max, SpellTrapRecoveryScore);

            if (PromptIs(CardId.TearlamentsScheiren) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, ScheirenSendScore);

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectFusionMaterial(IList<ClientCard> cards, int min, int max)
        {
            if (cards != null && cards.Count > 0 && IsFusionPromptOwner()) return SelectPlannedMaterials(cards, min, max);
            return base.OnSelectFusionMaterial(cards, min, max);
        }

        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.ShaddollSchism, 0))
            {
                NamedCard summoned = NamedCard.Get(_pendingFusionTargetId);
                return summoned != null && Enemy.GetMonsters().Any(c => CanCreditSchismRemoval(c) && (c.Attribute & summoned.Attribute) != 0 && SchismRemovalScore(c)>0);
            }
            if (desc == Util.GetStringId(CardId.ReeshaddollWendikuruhu, 2))
                return Bot.GetMonsters().Concat(Enemy.GetMonsters()).Any(c => FlipDownControlScore(c) > 0);
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectOption(IList<int> options)
        {
            int flipUp = options.IndexOf(Util.GetStringId(CardId.ReshShaddollIncarnation, 2));
            if (flipUp >= 0 && Bot.GetMonsters().Any(IsUsefulFacedownFlip))
                return flipUp;
            int reset = options.IndexOf(Util.GetStringId(CardId.ReshShaddollIncarnation, 3));
            if (reset >= 0 && Bot.GetMonsters().Any(c=>c.IsFaceup() && c.IsCode(CardId.TohushaddollGrysta) && HasCopyFuel())) return reset;
            if (flipUp >= 0) return flipUp;
            if (reset >= 0) return reset; // mandatory resolution fallback
            return base.OnSelectOption(options);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            // Lev sets the summoned Fusion's ATK to 0 after this prompt. Keep
            // that combo body out of Attack Position even though card data still
            // contains its printed ATK here.
            if (PromptIs(CardId.LevShaddollFusion) && positions.Contains(CardPosition.FaceUpDefence))
                return CardPosition.FaceUpDefence;
            // Ariel's revived Squamata is Wendikuruhu's required Flip fuel.
            // Leaving it face-up makes that interruption legally unusable on
            // an otherwise empty board.
            if (PromptIs(CardId.NaelshaddollAriel) && cardId == CardId.ShaddollSquamata &&
                Bot.HasInMonstersZone(CardId.ReeshaddollWendikuruhu) &&
                positions.Contains(CardPosition.FaceDownDefence))
                return CardPosition.FaceDownDefence;
            if (ShaddollMainMonsterIds.Contains(cardId) && positions.Contains(CardPosition.FaceDownDefence))
                return CardPosition.FaceDownDefence;

            NamedCard data = NamedCard.Get(cardId);
            if (data != null && positions.Contains(CardPosition.FaceUpDefence))
            {
                int strongestEnemy = Enemy.GetMonsters()
                    .Where(c => c.IsFaceup())
                    .Select(c => c.GetDefensePower())
                    .DefaultIfEmpty(0)
                    .Max();
                int expectedDamage = Math.Max(0, strongestEnemy - data.Attack);
                bool lethalExposure = Bot.LifePoints > 0 && expectedDamage >= Bot.LifePoints;
                bool lowAttackUtility = data.Attack <= 1000 || cardId == CardId.ReeshaddollWendikuruhu;
                if (lethalExposure || lowAttackUtility)
                    return CardPosition.FaceUpDefence;
            }
            if (positions.Contains(CardPosition.FaceUpAttack))
                return CardPosition.FaceUpAttack;
            return base.OnSelectPosition(cardId, positions);
        }

        private bool MulcharmyActivate()
        {
            if (Duel.Player != 1 || Duel.Phase > DuelPhase.Main1 || Bot.GetFieldCount()>0 ||
                DefaultCheckWhetherCardIsNegated(Card))
                return false;
            int used = 0;
            if (_usedEffects.Contains(CardId.MulcharmyFuwalos)) used++;
            if (_usedEffects.Contains(CardId.MulcharmyPurulia)) used++;
            if (used >= 2)
                return false;
            _usedEffects.Add(Card.Id);
            return true;
        }

        private bool DrollActivate()
        {
            if (Duel.Player != 1 || DefaultCheckWhetherCardIsNegated(Card))
                return false;
            return !_usedEffects.Contains(CardId.DrollLockBird) && MarkUsed(CardId.DrollLockBird);
        }

        private bool EffectVeilerActivate()
        {
            ClientCard last = Util.GetLastChainCard();
            if (last != null && last.Controller==1 && last.Location==CardLocation.MonsterZone &&
                !CanApplyEffect(last,CardType.Monster,CardId.EffectVeiler,EffectUse.Negate,true)) return false;
            if (CurrentOpponentChainIsCovered() || !DefaultEffectVeiler())
                return false;
            CoverCurrentOpponentChain();
            return true;
        }

        private bool AshBlossomActivate()
        {
            ClientCard last = Util.GetLastChainCard();
            if (last != null && last.Controller==1 && last.Location==CardLocation.MonsterZone &&
                !CanApplyEffect(last,CardType.Monster,CardId.AshBlossomJoyousSpring,EffectUse.Negate,false)) return false;
            if (CurrentOpponentChainIsCovered() || !DefaultAshBlossomAndJoyousSpring())
                return false;
            CoverCurrentOpponentChain();
            return true;
        }

        private void RememberAnsweredOpponentBody()
        {
            ClientCard last = Util.GetLastChainCard();
            if (last != null && last.Controller == 1 && last.Location == CardLocation.MonsterZone)
                _answeredOpponentBodies.Add(last);
        }

        private bool ShekhinagaActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return FusionRecoveryActivate();
            if (CurrentOpponentChainIsCovered() || !Bot.Hand.Any(IsShaddollCard))
                return false;
            ClientCard last = Util.GetLastChainCard();
            if (Duel.LastChainPlayer != 1 || last == null || last.IsDisabled())
                return false;
            CoverCurrentOpponentChain();
            return MarkUsed(CardId.ElShaddollShekhinaga);
        }

        private bool WendikuruhuActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return CanUseSpiritOrderedTrigger(Card) && HasRecoverableShaddollCard();
            if (Card.Location != CardLocation.MonsterZone || Card.IsDisabled() ||
                !Bot.GetMonsters().Any(c=>FlipUpControlScore(c)>0)) return false;
            if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 0) return false;
            bool enemyWindow = Enemy.GetMonsters().Any(IsWorthWendikuruhuInterruption);
            bool ownReset = Bot.GetMonsters().Any(c => FlipDownControlScore(c) > 0);
            bool develop = Bot.GetMonsters().Any(c => c.IsFacedown() &&
                ((c.IsCode(CardId.NaelshaddollAriel) && HasArielRecovery()) ||
                 (c.IsCode(CardId.TohushaddollGrysta) && BestCopyEffectValue() >= 145) ||
                 (c.IsCode(CardId.ReeshaddollWendi) && Bot.GetMonsterCount() < 5)));
            bool lateWindow = Duel.Phase == DuelPhase.End || (CurrentTiming != -1 && (CurrentTiming & 0x200) != 0);
            bool threatened = DefaultOnBecomeTarget() || Bot.GetMonsters().Any(c=>c.IsFacedown() && EnemyThreatens(c));
            // Use the repeatable loop on either turn. Preserve reactive flip fuel
            // on their turn until disruption, an endangered body, or a late window.
            bool useful = Duel.Player == 0 ? (develop || CurrentPlan().Goal == RouteGoal.FlipEngine || (enemyWindow && (ownReset || !FunctionalBoard())) || (lateWindow && ownReset)) :
                (enemyWindow || threatened || (lateWindow && (develop || ownReset || Bot.GetMonsters().Any(IsUsefulFacedownFlip))));
            if (!useful) return false;
            if (!threatened && HasLiveEnemyNegate(Card,CardType.Monster,Card.IsSpecialSummoned))
            {
                TraceDecision("HOLD", "Wendikuruhu: live enemy negate; remove or exhaust it before risking the engine");
                return false;
            }
            if (!MarkUsed(CardId.ReeshaddollWendikuruhu)) return false;
            TraceDecision("CONTROL", "Wendikuruhu flip / reset window");
            return true;
        }

        private bool IsWorthWendikuruhuInterruption(ClientCard card)
        {
            if (!CanFlipEnemy(card)) return false;
            bool battleThreat = Bot.GetMonsters().Any(c => c.IsFaceup() &&
                c.IsCode(CardId.ElShaddollWinda) && card.Attack >= c.Attack) ||
                card.Attack >= Bot.LifePoints;
            if (battleThreat || (card.IsAttack() && card.Attack > 0 && Bot.GetMonsters().Any(c =>
                c.IsFaceup() && card.Attack >= c.GetDefensePower()))) return true;
            if (_answeredOpponentBodies.Contains(card)) return false;
            if (card.IsDisabled()) return false;
            return IsExtraDeckMonster(card) || OpponentThreatScore(card) >= 750 ||
                (card.IsSpecialSummoned && OpponentThreatScore(card) >= 500) ||
                (GetOpponentCapabilities(card) & (OpponentCapability.Floodgate |
                    OpponentCapability.QuickInteraction)) != 0;
        }

        private bool SafeSchismMaterial(ClientCard c)
        {
            return c != null && c.IsMonster() &&
                (c.Location == CardLocation.Grave ||
                 (c.Location == CardLocation.MonsterZone && !c.HasType(CardType.Fusion) &&
                  !IsUsefulFacedownFlip(c) && (!c.IsCode(CardId.ArmageddonKnight) || Duel.Player == 1 || _normalSpent) &&
                  (ControlEngineValue(c)<140 || EnemyThreatens(c))));
        }

        private int SchismPlanScore(int id)
        {
            if (!Bot.HasInExtra(id)) return -10000;
            NamedCard fusion = NamedCard.Get(id);
            if (fusion == null) return -10000;
            bool liveWinda = FunctionalWinda();
            int removal = Enemy.GetMonsters().Where(e => CanCreditSchismRemoval(e) && (e.Attribute & fusion.Attribute) != 0)
                .Select(e => Math.Max(0, SchismRemovalScore(e))).DefaultIfEmpty(0).Max();
            if (id == CardId.ReeshaddollWendikuruhu && !LiveMonster(id) && Bot.GetMonsters().Any(IsUsefulFacedownFlip)) return 2800 + Math.Min(1000,removal);
            if (id == CardId.ElShaddollWinda && liveWinda && Bot.GetMonsters().Any(c=>c.IsCode(id) && EnemyThreatens(c))) return 4500 + Math.Min(1000,removal);
            if (removal > 0 && Duel.Player == 1 && _opponentSpecialSummonsThisTurn == 1)
                return 6500 + Math.Min(1500, removal);
            if (removal >= 750) return 5500 + Math.Min(1500, removal);
            if (id == CardId.ElShaddollApkallone && BestPersistentOpponentThreatScore() >= 750 &&
                !EffectUsed(CardId.ElShaddollApkallone + 1)) return 4800;
            if (id == CardId.ElShaddollWinda && !liveWinda && Duel.Player == 1 && !HasUrgentPersistentSpellTrapEngine())
                return 4000 + Math.Min(900, removal);
            if (removal >= 350) return 2000 + Math.Min(1500, removal);
            return -10000;
        }

        private bool SchismActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) || !IsMainPhase() ||
                HasSafeLethalOnBoard() ||
                (FunctionalWinda() && _specialSummonsThisTurn >= 1)) return false;
            if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 0 && !DefaultOnBecomeTarget())
                return false;
            if (Card.IsFacedown() && Duel.Player != 1) return false;
            bool threatened = DefaultOnBecomeTarget();
            if (!threatened && HasPreparedLevControlBoard() && Enemy.GetMonsterCount() == 0 &&
                Duel.CurrentChain.Count > 0 && !HasUrgentPersistentSpellTrapEngine()) return false;
            if (!threatened && Duel.Player == 1 && (ShouldLeadWithElFusion() ||
                (HasPreparedLevControlBoard() && !ShouldEstablishWindaBeforeFirstSummon() &&
                 !ShouldUseSchismMaterialRemovalWindow() && BestPersistentOpponentThreatScore() < 750))) return false;
            if (EffectUsed(CardId.ShaddollSchism)) return false;
            FusionPlan plan = PlanFusion(CardId.ShaddollSchism);
            if (plan == null || plan.Score < 25) return false;
            TraceDecision("SCHISM", "planned " + plan.Target + " value=" + plan.Score);
            // Flip activation alone need not consume the once-per-turn Fusion effect.
            return Card.IsFacedown() || MarkUsed(CardId.ShaddollSchism);
        }

        private bool ReshActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (Card.Location == CardLocation.Grave)
            {
                bool hasCost = Bot.Graveyard.Any(c => c != Card && IsShaddollCard(c));
                bool flipNow = Bot.GetMonsters().Any(IsUsefulFacedownFlip);
                // Never reset a control Fusion. Resh is an optional flip tool, not a way
                // to turn a completed board off. Do not race our own pending flip.
                if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 0) return false;
                bool reset = LiveMonster(CardId.ReeshaddollWendikuruhu) && Bot.GetMonsters().Any(c=>c.IsFaceup() && c.IsCode(CardId.TohushaddollGrysta) && HasCopyFuel()) && !flipNow;
                return hasCost && (flipNow || reset) && MarkUsed(CardId.ReshShaddollIncarnation);
            }
            if (Card.Location != CardLocation.SpellZone || Bot.GetMonsterCount() >= 5)
                return false;
            return Bot.Graveyard.Any(c => IsShaddollMonster(c) ||
                c.IsCode(CardId.ElShaddollWinda, CardId.ElShaddollConstruct,
                    CardId.ElShaddollApkallone, CardId.ReeshaddollWendikuruhu)) &&
                MarkUsed(CardId.ReshShaddollIncarnation);
        }

        private bool IsImmediateControlNeed(FusionPlan plan)
        {
            if (plan == null) return false;
            if (ThreatenedMaterial(plan)) return true;
            bool missingFlipEngine = !LiveMonster(CardId.ReeshaddollWendikuruhu) && plan.Target == CardId.ReeshaddollWendikuruhu;
            if (missingFlipEngine && (HasGrystaBody() || plan.Materials.Any(c=>c.IsCode(CardId.ReeshaddollWendi) && !EffectUsed(c.Id)))) return true;
            if (plan.Target == CardId.ElShaddollApkallone && Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any(c=>ApkalloneNegateScore(c)>=900)) return true;
            if (plan.Target == CardId.ElShaddollWinda)
            {
                if (plan.Materials.Any(c=>c.IsCode(CardId.ElShaddollWinda) && (c.IsDisabled() || EnemyThreatens(c)))) return true;
                if (Duel.Player == 1 && !FunctionalWinda() && !HasActiveControlShell() &&
                    (_opponentSpecialSummonsThisTurn <= 1 || (!FunctionalBoard() && Enemy.GetMonsters().Any(c=>c.IsFaceup() && !c.IsDisabled() && EffectText(c).Contains("special summon"))))) return true;
            }
            return false;
        }
        private bool PreserveControlBeforeFusion(int owner, FusionPlan plan, bool trace = true)
        {
            if (plan == null) return false;
            if (!HasActiveControlShell()) return true;
            bool losesFuel = plan.Materials.Any(c=>c.Location==CardLocation.MonsterZone && !EnemyThreatens(c) &&
                (ControlEngineValue(c)>=140 || IsUsefulFacedownFlip(c)));
            bool urgent = IsImmediateControlNeed(plan);
            // A rescue or a genuinely superior interaction may consume the board. Do not categorically ban materials.
            if (losesFuel && (!urgent || plan.Score < 100))
            {
                if (trace) TraceDecision("HOLD", "fusion " + owner + ": preserve unspent recurring flip resources");
                return false;
            }
            if (plan.Target == CardId.ElShaddollWinda && !urgent && _opponentSpecialSummonsThisTurn > 1)
            {
                if (trace) TraceDecision("HOLD", "fusion " + owner + ": hold for a better Winda window");
                return false;
            }
            return true;
        }
        private bool PreferDeckFusionNow()
        {
            return Duel.Player==0 && IsMainPhase() && Duel.CurrentChain.Count==0 &&
                Bot.HasInHand(CardId.ShaddollFusion) && EnemyDeckFusionWindow() && !WindaBlocksFurtherSummon() &&
                Bot.ExtraDeck.Any(c=>ShaddollFusionIds.Contains(c.Id));
        }

        private bool ElShaddollFusionActivate()
        {
            return CanActivateElShaddollFusionNow();
        }

        private bool ReserveArmageddonStarter()
        {
            return CurrentPlan().Goal == RouteGoal.Knight;
        }

        private bool CanActivateElShaddollFusionNow()
        {
            // A pending draw-tax effect is not a threat to an existing card.
            // Do not add a summon (especially Winda) ahead of our pending starter
            // just to evade one draw. Re-evaluate after the chain resolves.
            if (Duel.Player == 0 && Duel.CurrentChainInfo.Any(link => link.ActivatePlayer == 1 &&
                link.IsActivateCode(CardId.MaxxC, CardId.MulcharmyFuwalos)) &&
                !DefaultOnBecomeTarget() && !Bot.GetMonsters().Any(EnemyThreatens))
            {
                TraceDecision("HOLD", "El Fusion: preserve own combo under pending draw tax");
                return false;
            }
            if (ReserveArmageddonStarter()) return false;
            if (!HasFusionResources(false) || ShouldStopSpecialSummoning(CardLocation.Extra))
                return false;
            if (MustResolveLevMeshFirst() || MustWaitForLevWendiSearch())
                return false;
            if (Duel.SolvingChainIndex == 0 && Duel.CurrentChain.Count > 0 &&
                Duel.LastChainPlayer == 0)
                return false;
            if (HasSafeLethalOnBoard() || (Duel.Player == 0 && FunctionalWinda() && _specialSummonsThisTurn >= 1))
                return false;
            if (Duel.Player == 1)
            {
                bool schismPriority = ShouldEstablishWindaBeforeFirstSummon() ||
                    ShouldUseSchismMaterialRemovalWindow();
                bool threatened = Card != null &&
                    Card.IsCode(CardId.ElShaddollFusion) && DefaultOnBecomeTarget();
                bool answerPersistentThreat = Bot.HasInExtra(CardId.ElShaddollApkallone) &&
                    BestPersistentOpponentThreatScore() >= 750 &&
                    (!schismPriority || HasUrgentPersistentSpellTrapEngine());
                bool schismAvailable = Bot.HasInSpellZone(CardId.ShaddollSchism);
                bool mainEnd = CurrentTiming != -1 && (CurrentTiming & 0x200) != 0;
                bool fallbackWindaWindow = !schismAvailable && Enemy.GetMonsterCount() > 0 &&
                    (_opponentSpecialSummonsThisTurn > 0 || mainEnd);
                FusionPlan response = PlanFusion(CardId.ElShaddollFusion);
                bool rescue = ThreatenedMaterial(response);
                bool extension = IsImmediateControlNeed(response);
                return (threatened || rescue || answerPersistentThreat || fallbackWindaWindow || extension) && response != null &&
                    response.Score >= (threatened || rescue ? -100 : 30) && PreserveControlBeforeFusion(CardId.ElShaddollFusion,response);
            }
            bool plannedExtension = HasLevControlExtension() &&
                Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                Bot.HasInExtra(CardId.ReeshaddollWendikuruhu) &&
                !EffectUsed(CardId.ReeshaddollWendi);
            FusionPlan next = PlanFusion(CardId.ElShaddollFusion);
            if (next == null || !PreserveControlBeforeFusion(CardId.ElShaddollFusion,next)) return false;
            bool improve = next.Target == CardId.ReeshaddollWendikuruhu && !LiveMonster(next.Target) ||
                next.Target == CardId.ElShaddollApkallone && Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any(c=>ApkalloneNegateScore(c)>=750);
            if (HasActiveControlShell() && !IsImmediateControlNeed(next) && !DefaultOnBecomeTarget()) return false;
            if (!plannedExtension && !improve && !ThreatenedMaterial(next) && (HasPreparedLevControlBoard() || ControlBoardScore() >= 100)) return false;
            if (next.Score < (plannedExtension ? -100 : 35)) return false;
            return Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2 ||
                Duel.Phase == DuelPhase.BattleStart;
        }

        private bool FaimenaActivate()
        {
            if (Card.Location != CardLocation.Hand || !IsMainPhase() || HasSafeLethalOnBoard() ||
                (FunctionalWinda() && _specialSummonsThisTurn >= 1) || MustResolveLevMeshFirst()) return false;
            if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 0) return false;
            if (Duel.Player==0 && Duel.CurrentChainInfo.Any(link=>link.ActivatePlayer==1 && link.IsActivateCode(CardId.MaxxC,CardId.MulcharmyFuwalos,CardId.MulcharmyPurulia)) && !Bot.GetMonsters().Any(EnemyThreatens)) return false;
            FusionPlan plan = PlanFusion(CardId.DracotailFaimena);
            if (plan == null) { TraceDecision("HOLD", "Faimena: no acceptable control-preserving group"); return false; }
            if (!PreserveControlBeforeFusion(CardId.DracotailFaimena,plan)) return false;
            bool response = IsImmediateControlNeed(plan);
            bool rescue = ThreatenedMaterial(plan);
            bool refreshWinda = plan.Target==CardId.ElShaddollWinda && plan.Materials.Any(c=>c.IsCode(CardId.ElShaddollWinda) && (c.IsDisabled() || EnemyThreatens(c)));
            if (Duel.Player == 0 && !rescue && !refreshWinda) return false;
            if (Duel.Player == 1 && !response && !refreshWinda)
            {
                TraceDecision("HOLD", "Faimena: no urgent response; keep flexible Fusion for later");
                return false;
            }
            if (ShouldStopSpecialSummoning(CardLocation.Extra) && FunctionalBoard() && !rescue) return false;
            return plan.Score >= (rescue ? -100 : Duel.Player==0 ? 85 : 35);
        }

        private bool CartesiaActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return Duel.Phase == DuelPhase.End;
            if (Card.Location != CardLocation.MonsterZone || !IsMainPhase() ||
                ShouldStopSpecialSummoning(CardLocation.Extra) || (FunctionalWinda() && _specialSummonsThisTurn >= 1) || HasSafeLethalOnBoard())
                return false;
            if (MustResolveLevMeshFirst())
                return false;
            if (Duel.SolvingChainIndex == 0 && Duel.CurrentChain.Count > 0 &&
                Duel.LastChainPlayer == 0)
                return false;
            FusionPlan plan = PlanFusion(CardId.BlazingCartesia);
            return plan != null && plan.Score >= 55 && PreserveControlBeforeFusion(CardId.BlazingCartesia,plan) &&
                (!HasActiveControlShell() || IsImmediateControlNeed(plan));
        }

        private bool VoidUnleashingActivate()
        {
            if (Card.Location == CardLocation.Grave)
            {
                // The supplied MDPro mirror log demonstrates this resolution:
                // Infernoid Evil moves from GY back to the Extra Deck.
                if (DefaultCheckWhetherCardIsNegated(Card) || !Bot.Graveyard.Any(c=>c.IsCode(CardId.InfernoidEvil)) ||
                    !MarkUsed(CardId.VoidUnleashing + 1)) return false;
                AI.SelectCard(Bot.Graveyard.First(c=>c.IsCode(CardId.InfernoidEvil)));
                TraceDecision("RECYCLE", "Void returns Infernoid Evil for future Lev");
                return true;
            }
            if (DefaultCheckWhetherCardIsNegated(Card) || HasSafeLethalOnBoard())
                return false;
            if (Duel.Player == 0 && _armageddonMeshSummoned && IsLevRouteActive() &&
                Bot.HasInMonstersZone(CardId.ElShaddollMeshachrer) &&
                Bot.HasInExtra(CardId.ElShaddollConstruct) &&
                !EffectUsed(CardId.LevShaddollFusion + 1) &&
                (Bot.HasInHand(CardId.ReeshaddollWendi) ||
                    (Bot.HasInDeck(CardId.ShaddollHedgehog) && Bot.HasInDeck(CardId.ReeshaddollWendi))))
                return false;
            if (MustWaitForLevWendiSearch()) return false;
            if (Duel.Player == 0 && IsLevRouteActive() &&
                Bot.GetMonsters().Any(c => c.IsCode(CardId.ElShaddollMeshachrer) &&
                    c.IsFaceup() && !c.IsDisabled()) &&
                !_levMeshSearchResolved)
                return false;
            ClientCard reveal = Bot.Hand
                .Where(c => c != Card && IsShaddollCard(c) && HasVoidPair(GetBaseType(c)))
                .OrderBy(c => IsLevRouteActive() && c.IsCode(CardId.ReeshaddollWendi) ? -1 :
                    c.IsMonster() ? 0 : c.IsTrap() ? 1 : 2)
                .ThenByDescending(ShaddollMonsterSearchScore)
                .FirstOrDefault();
            if (reveal == null)
                return false;
            _voidRevealedType = reveal.IsMonster() ? CardType.Monster : reveal.IsSpell() ? CardType.Spell : CardType.Trap;
            AI.SelectCard(reveal);
            return MarkUsed(CardId.VoidUnleashing);
        }

        private bool LevPrimaryActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) || HasSafeLethalOnBoard())
                return false;
            if (Card.Location == CardLocation.SpellZone && Card.IsFaceup())
                return false;
            if (Bot.GetSpells().Any(c => c != Card && c.IsCode(CardId.LevShaddollFusion) &&
                c.IsFaceup() && !c.IsDisabled())) return false;
            if (Bot.ExtraDeck.Count == 0)
                return false;
            if (CurrentPlan().Goal == RouteGoal.Hold && FunctionalBoard() && !HasRecoverableShaddollCard() &&
                !Bot.HasInExtra(CardId.ElShaddollApkallone) && !Bot.HasInExtra(CardId.InfernoidEvil)) return false;
            _shaddollExtraDeckLock = true;
            return MarkUsed(CardId.LevShaddollFusion);
        }

        private bool LevTributeActivate()
        {
            if (Card.Location != CardLocation.SpellZone || !Card.IsFaceup() ||
                DefaultCheckWhetherCardIsNegated(Card) || HasSafeLethalOnBoard())
                return false;
            ClientCard recycle = Bot.GetMonsters().FirstOrDefault(c=>c.IsCode(CardId.ReeshaddollWendikuruhu) && c.IsFaceup());
            bool recoverSchism = Bot.Graveyard.Any(c=>c.IsCode(CardId.ShaddollSchism)) && !Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism);
            bool meshTrade = Duel.Player==0 && Duel.Phase==DuelPhase.Main1 && Bot.GetMonsters().Any(c=>c.IsCode(CardId.ElShaddollMeshachrer) && c.IsFaceup() && c.IsAttack() &&
                Enemy.GetMonsters().Any(e=>e.IsFaceup() && e.IsAttack() && e.Attack==c.Attack && e.Attack>0 && !ReflectedBattle(e) && !HasBattleImmunity(e)));
            bool reshRecovery = Bot.HasInSpellZone(CardId.ReshShaddollIncarnation) && !EffectUsed(CardId.ReshShaddollIncarnation);
            if (recycle != null && EffectUsed(CardId.ReeshaddollWendikuruhu) && recoverSchism && Bot.HasInExtra(CardId.ElShaddollConstruct) &&
                (meshTrade || reshRecovery) && !ShouldStopSpecialSummoning(CardLocation.Extra) && !FunctionalWinda() && !EffectUsed(CardId.LevShaddollFusion+1))
            {
                AI.SelectCard(recycle);
                _levRecycledWendikuruhu = true; _shaddollExtraDeckLock = true;
                TraceDecision("RECYCLE", "Lev converts used Wendikuruhu; Schism recovery with revival path");
                return MarkUsed(CardId.LevShaddollFusion+1);
            }
            ClientCard tribute = Bot.GetMonsters()
                .Where(c => c.HasType(CardType.Fusion) && c.IsFaceup() && !ShouldPreserveMonster(c))
                .Where(c => !c.IsCode(CardId.ElShaddollConstruct) || c.IsDisabled() ||
                    !Enemy.GetMonsters().Any(e => e.IsFaceup() && IsExtraDeckMonster(e)))
                .OrderByDescending(c => c.IsCode(CardId.ElShaddollMeshachrer) && IsLevRouteActive() ? 1 : 0)
                .ThenBy(c => LevTributeScore(c))
                .FirstOrDefault();
            bool finishCommittedRoute = tribute != null &&
                tribute.IsCode(CardId.ElShaddollMeshachrer) && IsLevRouteActive() &&
                ControlBoardScore() < 100;
            // Mesh has usually spent its search before this offer and supplies
            // no interruption by itself. Once Lev can legally convert it, one
            // additional draw under Maxx C/Fuwalos is preferable to ending on
            // the exact exposed body observed in the broad-run losses.
            if (tribute == null ||
                (!finishCommittedRoute && FunctionalBoard() && !tribute.IsDisabled()) ||
                (ShouldStopSpecialSummoning(CardLocation.Extra) && !finishCommittedRoute))
                return false;
            AI.SelectCard(tribute);
            _levTributedMesh = tribute.IsCode(CardId.ElShaddollMeshachrer);
            _shaddollExtraDeckLock = true;
            return MarkUsed(CardId.LevShaddollFusion + 1);
        }

        private bool FusionDeploymentActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) ||
                ShouldStopSpecialSummoning(CardLocation.Deck) || HasSafeLethalOnBoard())
                return false;
            return Bot.HasInDeck(CardId.BlazingCartesia) && Bot.HasInExtra(CardId.GranguignolDuskDragon);
        }

        private bool ShaddollDeckFusionActivate()
        {
            return EnemyDeckFusionWindow() && ShaddollFusionActivate();
        }

        private bool ShaddollFusionActivate()
        {
            if (Duel.Player != 0 || !IsMainPhase() ||
                DefaultCheckWhetherCardIsNegated(Card) || HasSafeLethalOnBoard())
                return false;
            // The engine offers this activation only when a legal fusion exists.
            // Extra Deck type is a heuristic, not proof of its summon provenance.
            bool deckMaterialWindow = EnemyDeckFusionWindow();
            if (PreferDeckFusionNow()) TraceDecision("PLAN", "deck Fusion available before consuming hand/field engine");
            bool recovery = !Bot.GetMonsters().Any(c => c.IsFaceup() && c.IsMonster()) && deckMaterialWindow;
            if (recovery) return true;
            if (ReserveArmageddonStarter()) return false;
            if ((!deckMaterialWindow && !HasFusionResources(false)) ||
                ShouldStopSpecialSummoning(CardLocation.Extra) ||
                (FunctionalWinda() && _specialSummonsThisTurn >= 1) ||
                (ControlBoardScore() >= 140 && FunctionalBoard() && !deckMaterialWindow))
                return false;
            if (!deckMaterialWindow && !WorthFusion(CardId.ShaddollFusion,25)) return false;
            return true;
        }

        private bool ScheirenActivate()
        {
            if (Card.Location == CardLocation.Hand)
            {
                if (FunctionalWinda() || ShouldStopSpecialSummoning(CardLocation.Hand) ||
                    HasSafeLethalOnBoard() || Bot.GetMonsterCount() >= 5 ||
                    Bot.Deck.Count < 3 ||
                    !Bot.Hand.Any(c => c != Card && IsShaddollMonster(c)))
                    return false;
                return MarkUsed(CardId.TearlamentsScheiren);
            }
            if (Card.Location == CardLocation.Grave)
                return CanUseSpiritOrderedTrigger(Card) &&
                    !FunctionalWinda() && !ShouldStopSpecialSummoning(CardLocation.Extra) &&
                    Bot.HasInExtra(CardId.ElShaddollWinda) && Card.HasAttribute(CardAttribute.Dark) &&
                    Bot.Hand.Concat(Bot.GetMonsters()).Concat(Bot.Graveyard)
                        .Any(c => c != Card && IsShaddollMonster(c)) &&
                    MarkUsed(CardId.TearlamentsScheiren + 1);
            return false;
        }

        private bool ArmageddonKnightSummon()
        {
            if (!IsMainPhase() || Bot.GetMonsterCount() >= 5 || _normalSpent || HasSafeLethalOnBoard()) return false;
            if (!Bot.HasInDeck(CardId.ShaddollSquamata) && !HasShaddollSendTarget()) return false;
            _normalSpent = true;
            TraceDecision("SUMMON", "Armageddon; evaluate next action after resolution");
            return true;
        }

        private bool ArmageddonKnightActivate()
        {
            return Card.Location == CardLocation.MonsterZone && HasShaddollSendTarget();
        }

        private bool SquamataActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
            {
                if (!Enemy.GetMonsters().Any(c=>!PendingRemoval(c) && CanApplyEffect(c,CardType.Monster,CardId.ShaddollSquamata,EffectUse.Destroy,true)) || !MarkUsed(CardId.ShaddollSquamata)) return false;
                _queuedTargetOwner = CardId.ShaddollSquamata;
                return true;
            }
            return CanUseSpiritOrderedTrigger(Card) &&
                HasShaddollSendTarget() && MarkUsed(CardId.ShaddollSquamata);
        }

        private bool HedgehogActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return ShaddollSpellTrapIds.Any(Bot.HasInDeck) && MarkUsed(CardId.ShaddollHedgehog);
            return CanUseSpiritOrderedTrigger(Card) &&
                ShaddollMainMonsterIds.Any(Bot.HasInDeck) && MarkUsed(CardId.ShaddollHedgehog);
        }

        private bool WendiActivate()
        {
            return (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                Bot.GetMonsterCount() < 5 && ShaddollMainMonsterIds.Any(Bot.HasInDeck) &&
                MarkUsed(CardId.ReeshaddollWendi);
        }

        private bool DragonActivate()
        {
            bool useful = Card.Location == CardLocation.MonsterZone ?
                Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any(c => !PendingRemoval(c) && CanApplyEffect(c,CardType.Monster,CardId.ShaddollDragon,EffectUse.Bounce,true)) :
                Enemy.GetSpells().Any(c => !PendingRemoval(c) && CanApplyEffect(c,CardType.Monster,CardId.ShaddollDragon,EffectUse.Destroy,true));
            if (!(Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) || !useful || !MarkUsed(CardId.ShaddollDragon)) return false;
            _dragonDestroyMode = Card.Location == CardLocation.Grave;
            _queuedTargetOwner = CardId.ShaddollDragon;
            return true;
        }

        private bool BeastActivate()
        {
            bool useful = Card.Location == CardLocation.MonsterZone ? Bot.Hand.Count > 0 : Bot.Deck.Count > 0;
            return (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                useful && MarkUsed(CardId.ShaddollBeast);
        }

        private bool ArielActivate()
        {
            bool useful = Card.Location == CardLocation.MonsterZone ?
                HasArielRecovery() : UsefulEnemyGrave();
            return (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                useful && MarkUsed(CardId.NaelshaddollAriel);
        }

        private bool TohushaddollGrystaActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
            {
                ClientCard copy = Bot.Graveyard.Where(c=>IsShaddollMonster(c) && c.HasType(CardType.Flip) && !c.IsCode(CardId.TohushaddollGrysta) && CopyEffectValue(c.Id)>0)
                    .OrderByDescending(FlipCopyScore).FirstOrDefault();
                if (copy == null || !MarkUsed(CardId.TohushaddollGrysta)) return false;
                _grystaCopyId = copy.Id;
                AI.SelectCard(copy);
                TraceDecision("COPY", "Grysta -> " + copy.Id);
                return true;
            }
            if (!CanUseSpiritOrderedTrigger(Card) || FunctionalWinda() ||
                ShouldStopSpecialSummoning(CardLocation.Extra) || !HasFusionResources(true))
                return false;
            bool committed = CanCommitArmageddonMesh() || (IsLevRouteActive() && _levRouteStage < LevRouteStage.MeshachrerSummoned);
            if (!committed && !WorthFusion(CardId.TohushaddollGrysta, FunctionalBoard() ? 170 : 45)) return false;
            _shaddollExtraDeckLock = true;
            return MarkUsed(CardId.TohushaddollGrysta);
        }

        private bool ConstructActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return HasShaddollSendTarget();
            return CanUseSpiritOrderedTrigger(Card) && HasRecoverableSpellTrap();
        }

        private bool ApkalloneActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
            {
                if (Card.IsDisabled() || EffectUsed(CardId.ElShaddollApkallone + 1) || !Enemy.GetMonsters().Concat(Enemy.GetSpells())
                    .Any(c=>c.IsFaceup() && !c.IsDisabled() && ApkalloneNegateScore(c)>0) || !MarkUsed(CardId.ElShaddollApkallone+1)) return false;
                _queuedTargetOwner = CardId.ElShaddollApkallone;
                return true;
            }
            return CanUseSpiritOrderedTrigger(Card) && HasRecoverableShaddollCard() && MarkUsed(CardId.ElShaddollApkallone);
        }

        private bool InfernoidEvilActivate()
        {
            return (Card.Location == CardLocation.Grave || Card.Location == CardLocation.Removed) &&
                (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                Bot.HasInDeck(CardId.VoidUnleashing) && VoidHasPotentialPair();
        }

        private bool MeshachrerActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
            {
                // The Lua script uses SetCountLimit(1) without a shared card
                // code: each properly summoned copy owns its own search.
                if (_usedMeshSearches.Contains(Card) ||
                    Bot.LifePoints <= 1600 || !HasUsefulMeshSearch())
                    return false;
                _usedMeshSearches.Add(Card);
                return true;
            }
            // Meshachrer's Graveyard revival has no count limit in the selected
            // script. Do not turn it into a fabricated hard once-per-turn.
            return Bot.GetMonsterCount() < 5 && Bot.Graveyard.Any(IsShaddollMonster);
        }

        private bool FusionRecoveryActivate()
        {
            return Card.Location == CardLocation.Grave &&
                CanUseSpiritOrderedTrigger(Card) && HasRecoverableSpellTrap();
        }

        private bool PerfumeDancerActivate()
        {
            return Card.Location == CardLocation.Grave && CanWinBattleWithPerfume();
        }

        private bool CanWinBattleWithPerfume()
        {
            if (Duel.Player != 0 || Duel.Turn <= 1 || Duel.Phase > DuelPhase.Battle) return false;
            return Enemy.GetMonsters().Any(enemy => enemy.IsFaceup() && enemy.IsAttack() && enemy.BaseDefense > 0 &&
                !IsBattleObstacle(enemy) && Bot.GetMonsters().Any(attacker => attacker.IsFaceup() && attacker.IsAttack() &&
                    attacker.Attack > 0 && !attacker.Attacked && attacker.Attack <= enemy.Attack &&
                    attacker.Attack > Math.Max(0, enemy.Attack - enemy.BaseDefense)));
        }

        private bool GranguignolActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return Bot.ExtraDeck.Any(c => c.Level >= 6 && c.HasAttribute(CardAttribute.Light | CardAttribute.Dark));
            return false; // No Dogmatika or Despia summon target exists in the supplied deck.
        }

        private bool CartesiaSummon()
        {
            if (Duel.Player != 0 || !IsMainPhase() || _normalSpent || Bot.GetMonsterCount() >= 5 ||
                ReserveArmageddonStarter() || HasSafeLethalOnBoard()) return false;
            FusionPlan plan = PlanFusion(CardId.BlazingCartesia);
            bool canClear = plan != null && plan.Score >= 90 && !FunctionalBoard();
            bool canPressure = Duel.Phase == DuelPhase.Main1 && Enemy.GetMonsterCount()==0 && Enemy.LifePoints <= 1500;
            if (!canClear && !canPressure) return false;
            _normalSpent = true;
            TraceDecision("SUMMON", "Cartesia for available extension or safe pressure");
            return true;
        }
        private bool SetShaddollMonster()
        {
            if (Bot.GetMonsterCount() >= 5 || _normalSpent || Card == null || Card.Level > 4 || Card.IsCode(CardId.ShaddollBeast, CardId.TohushaddollGrysta) || HasSafeLethalOnBoard()) return false;
            if (ReserveArmageddonStarter() || ReservedForRoute(Card)) return false;
            bool useful = LiveMonster(CardId.ReeshaddollWendikuruhu) || !HasImmediateFusionStarter() || Bot.GetMonsterCount() == 0;
            if (useful) _normalSpent = true;
            return useful;
        }

        private bool SetSpellTrap()
        {
            if (Card.IsCode(CardId.ShaddollSchism, CardId.ElShaddollFusion))
                return Bot.GetSpellCountWithoutField() < 4;
            return DefaultSpellSet();
        }

        private bool ShaddollMonsterRepos()
        {
            if (Card != null && Card.IsFacedown() && Card.HasType(CardType.Flip))
            {
                bool liveWendikuruhu = Bot.GetMonsters().Any(c => c.IsFaceup() &&
                    !c.IsDisabled() && c.IsCode(CardId.ReeshaddollWendikuruhu));
                int flipFuel = Bot.GetMonsters().Count(c =>
                    c.IsFacedown() && c.HasType(CardType.Flip));
                // Wendikuruhu cannot perform its opponent-turn position change
                // without one face-down Flip monster. Develop surplus bodies,
                // but never let the generic battle routine expose the last fuel.
                if (liveWendikuruhu && flipFuel <= 1)
                    return false;
            }
            if (Card != null && Card.IsFaceup() && Card.IsCode(CardId.TohushaddollGrysta,CardId.ReeshaddollWendikuruhu) && !HasSafeLethalOnBoard())
            {
                if (Card.IsAttack() && Enemy.GetMonsters().Any(c=>c.IsFaceup() && c.Attack>Card.Attack)) return true;
                if (!Card.IsAttack())
                {
                    int otherDamage = Bot.GetMonsters().Where(c=>c!=Card && c.IsFaceup() && c.IsAttack() && !_schismBodies.Contains(c)).Sum(c=>Math.Max(0,c.Attack));
                    return Enemy.GetMonsterCount()==0 && Enemy.GetSpellCount()==0 && !_schismBodies.Contains(Card) &&
                        Card.Attack>0 && otherDamage+Card.Attack>=Enemy.LifePoints;
                }
            }
            return DefaultMonsterRepos();
        }

        private IList<ClientCard> SelectVoidSearch(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> selected = new List<ClientCard>();
            CardType usedTypes = _voidRevealedType;
            while (selected.Count < max)
            {
                ClientCard choice = cards
                    .Where(c => !selected.Contains(c) && (usedTypes & GetBaseType(c)) == 0)
                    .OrderByDescending(VoidSearchScore)
                    .FirstOrDefault();
                if (choice == null)
                    break;
                selected.Add(choice);
                CardType type = GetBaseType(choice);
                usedTypes |= type;
                // SelectSubGroup normally asks for one card per SelectUnselect
                // packet. Remember committed types when the lifecycle permits;
                // global value ordering also remains correct if that transient
                // state is cleared between packets.
                _voidRevealedType |= type;
            }
            return FillSelection(selected, cards, min, max);
        }

        private bool LegalMaterialGroup(int id, IList<ClientCard> group)
        {
            if (group.Any(c => c == null || !c.IsMonster())) return false;
            if (id == CardId.ElShaddollMeshachrer)
            {
                if (group.Count != 3) return false;
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        if (i != j && IsShaddollMonster(group[i]) && group[j].HasAttribute(CardAttribute.Dark) &&
                            group[3-i-j].HasAttribute(CardAttribute.Earth)) return true;
                return false;
            }
            if (group.Count != 2) return false;
            ClientCard a = group[0], b = group[1];
            if (id == CardId.GranguignolDuskDragon)
                return (a.IsCode(CardId.BlazingCartesia) && b.HasAttribute(CardAttribute.Light | CardAttribute.Dark)) ||
                    (b.IsCode(CardId.BlazingCartesia) && a.HasAttribute(CardAttribute.Light | CardAttribute.Dark));
            if (id == CardId.ElShaddollApkallone)
                return IsShaddollMonster(a) && IsShaddollMonster(b) && a.Attribute != b.Attribute;
            CardAttribute required = id == CardId.ElShaddollConstruct ? CardAttribute.Light :
                id == CardId.ElShaddollWinda ? CardAttribute.Dark :
                id == CardId.ElShaddollShekhinaga ? CardAttribute.Earth :
                id == CardId.ElShaddollAnoyatyllis ? CardAttribute.Water :
                id == CardId.ReeshaddollWendikuruhu ? CardAttribute.Wind : 0;
            return required != 0 && ((IsShaddollMonster(a) && b.HasAttribute(required)) ||
                (IsShaddollMonster(b) && a.HasAttribute(required)));
        }
        private List<ClientCard> AvailableMaterials(int owner)
        {
            IEnumerable<ClientCard> pool = Bot.GetMonsters();
            if (owner != CardId.ShaddollSchism) pool = pool.Concat(Bot.Hand);
            if (owner == CardId.ShaddollSchism || owner == CardId.TohushaddollGrysta || owner == CardId.TearlamentsScheiren)
                pool = pool.Concat(Bot.Graveyard);
            if (owner == CardId.DracotailFaimena && Card != null && Card.IsCode(CardId.DracotailFaimena)) pool = pool.Where(c => c != Card);
            return pool.Where(c => c != null && c.IsMonster()).Distinct().ToList();
        }
        private int MaterialValue(ClientCard c, int owner, int target, RoutePlan route)
        {
            bool banish = owner == CardId.ShaddollSchism || owner == CardId.TohushaddollGrysta;
            int score = c.Location == CardLocation.Grave ? 55 : c.Location == CardLocation.Deck ? 85 : -45;
            if (c.Location == CardLocation.Deck && !IsShaddollMonster(c)) score -= IsHandTrap(c) ? 95 : 60;
            if (c.Location == CardLocation.Deck && IsShaddollMonster(c) && EffectUsed(c.Id)) score -= 60;
            if (!banish && c.Location != CardLocation.Grave && IsShaddollMonster(c))
            {
                if (!EffectUsed(c.Id) && ShaddollMainMonsterIds.Contains(c.Id)) score += Math.Max(0, ShaddollSendScore(c) - 25);
                if (c.HasType(CardType.Fusion) && HasRecoverableSpellTrap()) score += 45;
            }
            if (c.Location == CardLocation.Hand && IsHandTrap(c))
                score += IsMulcharmy(c) && Bot.GetFieldCount()>0 ? 45 : -(c.IsCode(CardId.MaxxC) ? 190 : 125);
            if (c.Location == CardLocation.Hand && route.Reserved.Contains(c.Id) && Bot.Hand.Count(h => h.IsCode(c.Id)) <= 1)
                score -= target == CardId.ReeshaddollWendikuruhu && c.IsCode(CardId.ReeshaddollWendi) ? 0 : 280;
            if (c.Location == CardLocation.MonsterZone && ShouldPreserveMonster(c) && !EnemyThreatens(c)) score -= 350;
            if (c.Location == CardLocation.MonsterZone && !EnemyThreatens(c)) score -= ControlEngineValue(c);
            if (c.Location == CardLocation.MonsterZone && EnemyThreatens(c)) score += 130;
            if (c.Location == CardLocation.MonsterZone && c.IsFaceup() && !c.IsDisabled() && c.Attack >= 1500 && !_schismBodies.Contains(c)) score -= 45;
            if (banish && c.Location == CardLocation.MonsterZone && c.HasType(CardType.Fusion)) score -= 150;
            if (c.Location == CardLocation.MonsterZone && IsUsefulFacedownFlip(c) && LiveMonster(CardId.ReeshaddollWendikuruhu) && !EnemyThreatens(c)) score -= 210;
            if (c.Location == CardLocation.MonsterZone && c.IsCode(CardId.ElShaddollWinda) && c.IsDisabled() && target == CardId.ElShaddollWinda) score += 220;
            if (target == CardId.ElShaddollMeshachrer && owner == CardId.TohushaddollGrysta && CanCommitArmageddonMesh() &&
                c.IsCode(CardId.ArmageddonKnight) && c.Location == CardLocation.MonsterZone) score += 500;
            if (target == CardId.ElShaddollMeshachrer && owner == CardId.TohushaddollGrysta &&
                c.Location == CardLocation.Grave && c.IsCode(CardId.TohushaddollGrysta, CardId.ShaddollSquamata, CardId.ElShaddollApkallone)) score += 120;
            if (target == CardId.ReeshaddollWendikuruhu && route.Goal == RouteGoal.ConstructControl &&
                c.IsCode(CardId.ReeshaddollWendi, CardId.ElShaddollConstruct)) score += 160;
            return score;
        }
        private bool FusionAllowedByEffect(int owner, int id)
        {
            if (id == CardId.GranguignolDuskDragon)
                return !_shaddollExtraDeckLock && (owner == CardId.BlazingCartesia || owner == CardId.DracotailFaimena || owner == CardId.TearlamentsScheiren);
            if (!ShaddollFusionIds.Contains(id)) return false;
            if (owner == CardId.DracotailFaimena)
            {
                NamedCard data = NamedCard.Get(id);
                return data != null && (data.Race & (int)(CardRace.Dragon | CardRace.SpellCaster)) != 0;
            }
            if (owner == CardId.BlazingCartesia)
            {
                NamedCard data = NamedCard.Get(id);
                return data != null && data.Level >= 8;
            }
            return true;
        }
        private bool FusionGroupAllowedByEffect(int owner, IList<ClientCard> group)
        {
            if (owner == CardId.TearlamentsScheiren)
                return group.Any(c=>c.IsCode(CardId.TearlamentsScheiren) && c.Location==CardLocation.Grave);
            return true;
        }
        private FusionPlan BestFusionPlan(int owner, IEnumerable<int> targets, IList<ClientCard> pool, bool screen = false)
        {
            RoutePlan route = CurrentPlan();
            FusionPlan best = null;
            foreach (int id in targets.Distinct())
            {
                if (!FusionAllowedByEffect(owner,id) || !Bot.HasInExtra(id)) continue;
                ClientCard fusion = Bot.ExtraDeck.FirstOrDefault(c => c.IsCode(id));
                if (fusion == null) continue;
                int targetValue = FusionTargetScore(fusion, owner);
                if (owner == CardId.ShaddollSchism && targetValue <= 0) continue;
                // Schism's original score is an activation/target utility gate, normalized here.
                if (owner == CardId.ShaddollSchism) targetValue /= 10;
                for (int i = 0; i < pool.Count; i++)
                    for (int j = i+1; j < pool.Count; j++)
                    {
                        if (id == CardId.ElShaddollMeshachrer)
                        {
                            for (int k = j+1; k < pool.Count; k++)
                                ConsiderFusion(ref best, owner, id, targetValue, route, new List<ClientCard>{pool[i],pool[j],pool[k]},screen);
                        }
                        else ConsiderFusion(ref best, owner, id, targetValue, route, new List<ClientCard>{pool[i],pool[j]},screen);
                    }
            }
            return best;
        }
        private bool OpensDirectFinish(int id,IList<ClientCard> group)
        {
            if (Duel.Player!=0 || Duel.Phase!=DuelPhase.Main1 || Enemy.GetMonsterCount()!=1) return false;
            NamedCard fusion=NamedCard.Get(id);
            ClientCard obstacle=Enemy.GetMonsters().FirstOrDefault();
            if (fusion==null || !CanCreditSchismRemoval(obstacle) || (obstacle.Attribute & fusion.Attribute)==0) return false;
            int damage=Bot.GetMonsters().Where(c=>!group.Contains(c) && c.IsFaceup() && c.IsAttack() && !_schismBodies.Contains(c))
                .Sum(c=>Math.Max(0,c.Attack));
            return damage>=Enemy.LifePoints && Enemy.LifePoints>0;
        }
        private bool SchismGroupAllowed(int id,IList<ClientCard> group)
        {
            if (group.All(SafeSchismMaterial)) return true;
            if (OpensDirectFinish(id,group)) return true; // preserve attackers that can actually deliver damage
            if (id == CardId.ElShaddollWinda && group.Any(c=>c.Location==CardLocation.MonsterZone && c.IsCode(id) && EnemyThreatens(c)) &&
                !group.Any(c=>c.Location==CardLocation.MonsterZone && !c.IsCode(id) && ControlEngineValue(c)>100)) return true;
            if (group.Any(c=>c.Location==CardLocation.MonsterZone && (ShouldPreserveMonster(c) || IsUsefulFacedownFlip(c)) && !EnemyThreatens(c))) return false;
            int fieldFusionCount=group.Count(c=>c.Location==CardLocation.MonsterZone && c.HasType(CardType.Fusion));
            if (fieldFusionCount>1) return false;
            NamedCard fusion=NamedCard.Get(id);
            return fusion!=null && Enemy.GetMonsters().Any(c=>CanCreditSchismRemoval(c) && (c.Attribute&fusion.Attribute)!=0 && SchismRemovalScore(c)>=850);
        }
        private void ConsiderFusion(ref FusionPlan best, int owner, int id, int targetValue, RoutePlan route, List<ClientCard> group, bool screen)
        {
            if (!LegalMaterialGroup(id, group) || !FusionGroupAllowedByEffect(owner,group)) return;
            if (owner == CardId.ShaddollSchism && !SchismGroupAllowed(id,group)) return;
            int score = targetValue + group.Sum(c => MaterialValue(c, owner, id, route));
            if (owner == CardId.ShaddollSchism && OpensDirectFinish(id,group)) score += 1100;
            if (id == CardId.ElShaddollWinda && FunctionalWinda() && owner != CardId.ShaddollSchism) score -= 150;
            if (id == CardId.ElShaddollConstruct && Enemy.GetMonsters().Any(c => IsBattleObstacle(c) && c.IsSpecialSummoned)) score += 45;
            if (id == CardId.ElShaddollMeshachrer && owner != CardId.TohushaddollGrysta && !LiveLev()) score -= 45;
            score += ControlFusionGroupValue(owner,id,group);
            if (id == CardId.ElShaddollShekhinaga && !Bot.Hand.Any(c=>IsShaddollCard(c) && !group.Contains(c))) score -= 160;
            if (best != null && score <= best.Score) return;
            FusionPlan candidate = new FusionPlan { Target = id, Score = score, Materials = group };
            if (screen && (owner == CardId.ElShaddollFusion || owner == CardId.DracotailFaimena || owner == CardId.BlazingCartesia || owner == CardId.ShaddollFusion) &&
                !PreserveControlBeforeFusion(owner,candidate,false)) return;
            best = candidate;
        }
        private int ControlFusionGroupValue(int owner, int id, IList<ClientCard> group)
        {
            bool banish = owner == CardId.ShaddollSchism || owner == CardId.TohushaddollGrysta;
            int value = 0;
            if (id == CardId.ReeshaddollWendikuruhu && !LiveMonster(id))
            {
                bool existingFuel = Bot.GetMonsters().Any(c => c.IsFacedown() && c.HasType(CardType.Flip) && !group.Contains(c));
                bool windaBlocksFollowup = Bot.GetMonsters().Concat(Enemy.GetMonsters()).Any(c=>c.IsCode(CardId.ElShaddollWinda) && c.IsFaceup() && !c.IsDisabled() && !group.Contains(c));
                bool newFuel = !banish && !windaBlocksFollowup && group.Any(c => c.IsCode(CardId.ReeshaddollWendi) && !EffectUsed(c.Id)) && Bot.GetMonsterCount() < 5 && ShaddollMainMonsterIds.Any(x=>x!=CardId.ReeshaddollWendi && Bot.HasInDeck(x));
                if (existingFuel || newFuel) value += 170;
                else value -= 50;
            }
            if (id == CardId.ElShaddollWinda && group.Any(c=>c.IsCode(id) && c.Location==CardLocation.MonsterZone && (EnemyThreatens(c) || c.IsDisabled()))) value += 350;
            if (banish)
            {
                if (HasGrystaBody() || LiveMonster(CardId.ReeshaddollWendikuruhu))
                    value -= group.Where(c=>c.Location==CardLocation.Grave && c.HasType(CardType.Flip) && !c.IsCode(CardId.TohushaddollGrysta))
                        .Sum(c=>Bot.Graveyard.Count(x=>x.IsCode(c.Id))<=1 ? 75 : 15);
                if (LiveMonster(CardId.ReeshaddollWendikuruhu) && !HasGrystaBody() && group.Any(c=>c.IsCode(CardId.TohushaddollGrysta)) && HasArielRecovery()) value += 45;
            }
            return value;
        }
        private FusionPlan PlanFusion(int owner)
        {
            return BestFusionPlan(owner, Bot.ExtraDeck.Select(c => c.Id), AvailableMaterials(owner), true);
        }
        private bool WorthFusion(int owner, int floor = 25)
        {
            FusionPlan p = PlanFusion(owner);
            bool ok = p != null && p.Score >= floor;
            if (ok) TraceDecision("FUSION", owner + " -> " + p.Target + " value=" + p.Score);
            else TraceDecision("HOLD", "fusion " + owner + " rejected; no profitable known group");
            return ok;
        }
        private IList<ClientCard> SelectPlannedMaterials(IList<ClientCard> cards, int min, int max)
        {
            int owner = GetPromptOwnerId();
            if (_pendingFusionTargetId == 0 || (owner != 0 && owner != _pendingFusionOwnerId))
                return SelectByScore(cards, min, max, FusionMaterialScore);
            // The engine can ask for one material at a time. Prefer a group that contains already chosen instances.
            RoutePlan route = CurrentPlan();
            List<ClientCard> available = AvailableMaterials(owner).Concat(cards).Concat(_chosenFusionMaterials)
                .Where(c => c != null && c.IsMonster()).Distinct().ToList();
            List<ClientCard> required = _chosenFusionMaterials.ToList();
            List<ClientCard> best = null; int bestScore = int.MinValue;
            int size = _pendingFusionTargetId == CardId.ElShaddollMeshachrer ? 3 : 2;
            for (int i=0;i<available.Count;i++) for(int j=i+1;j<available.Count;j++)
            {
                if (size == 2) ConsiderPromptGroup(new List<ClientCard>{available[i],available[j]}, required, cards, owner, route, ref best, ref bestScore);
                else for(int k=j+1;k<available.Count;k++) ConsiderPromptGroup(new List<ClientCard>{available[i],available[j],available[k]}, required, cards, owner, route, ref best, ref bestScore);
            }
            IList<ClientCard> result;
            if (best == null || best.Count(c=>cards.Contains(c) && !_chosenFusionMaterials.Contains(c)) < Math.Max(1,min))
                result=SelectByScore(cards,min,max,c=>MaterialValue(c,owner,_pendingFusionTargetId,route)+FusionMaterialScore(c));
            else result=FillSelection(best.Where(c => cards.Contains(c) && !_chosenFusionMaterials.Contains(c)).OrderByDescending(c => MaterialValue(c,owner,_pendingFusionTargetId,route)).ToList(), cards,min,max);
            foreach (ClientCard c in result)
                if (!_chosenFusionMaterials.Add(c)) _chosenFusionMaterials.Remove(c);
            TraceDecision("MATERIAL", _pendingFusionTargetId + ":" + string.Join(",",result.Select(c=>c.Id)));
            return result;
        }
        private void ConsiderPromptGroup(List<ClientCard> group,List<ClientCard> required,IList<ClientCard> offered,int owner,RoutePlan route,ref List<ClientCard> best,ref int score)
        {
            if (!required.All(group.Contains) || !group.Any(c=>offered.Contains(c) && !required.Contains(c)) || !LegalMaterialGroup(_pendingFusionTargetId,group) || !FusionGroupAllowedByEffect(owner,group)) return;
            if (owner==CardId.ShaddollSchism && !SchismGroupAllowed(_pendingFusionTargetId,group)) return;
            int value=group.Sum(c=>MaterialValue(c,owner,_pendingFusionTargetId,route)) + ControlFusionGroupValue(owner,_pendingFusionTargetId,group);
            if (_selectedFusionGroup != null && group.Count == _selectedFusionGroup.Count && group.All(_selectedFusionGroup.Contains)) value += 10000;
            if (owner==CardId.ShaddollSchism && OpensDirectFinish(_pendingFusionTargetId,group)) value+=1100;
            if (value>score) {score=value;best=group;}
        }

        private IList<ClientCard> SelectFusionTarget(IList<ClientCard> cards, int min, int max, int ownerId)
        {
            List<ClientCard> allowed = cards.Where(c => FusionAllowedByEffect(ownerId,c.Id)).ToList();
            // Optional effects are screened at activation. If an already-resolving
            // mandatory prompt has no allowed option, keep the protocol legal.
            IList<ClientCard> pool = allowed.Count >= Math.Max(min, 1) ? allowed : cards;
            _chosenFusionMaterials.Clear(); _selectedFusionGroup = null;
            FusionPlan plan = ownerId == CardId.LevShaddollFusion ? null : BestFusionPlan(ownerId, pool.Select(c => c.Id), AvailableMaterials(ownerId), true);
            if (plan == null && ownerId != CardId.LevShaddollFusion)
                plan = BestFusionPlan(ownerId, pool.Select(c => c.Id), AvailableMaterials(ownerId));
            // Deck material permission belongs to the engine. Do not rule out offered Deck-only lines.
            bool deckWindow = ownerId == CardId.ShaddollFusion && EnemyDeckFusionWindow();
            List<ClientCard> ordered = pool.OrderByDescending(c => !deckWindow && plan != null && c.IsCode(plan.Target) ? 100000 : FusionTargetScore(c, ownerId)).ToList();
            IList<ClientCard> result = FillSelection(ordered, pool, min, max);
            if (result.Count > 0)
            {
                _pendingFusionTargetId = result[0].Id; _pendingFusionOwnerId = ownerId;
                if (plan != null && plan.Target == _pendingFusionTargetId) _selectedFusionGroup = plan.Materials.ToList();
                TraceDecision("TARGET", ownerId + " -> " + _pendingFusionTargetId);
                if (ownerId == CardId.ElShaddollFusion && Duel.Player == 1 &&
                    result[0].IsCode(CardId.ElShaddollApkallone) && HasPreparedLevControlBoard())
                    _levOpponentTurnBranch = true;
            }
            return result;
        }

        private bool PendingRemoval(ClientCard card)
        {
            if (_pendingRemoval.Contains(card)) return true;
            return Duel.CurrentChainInfo.Any(link=>link.ActivatePlayer==0 && link.Targets.Contains(card) &&
                (link.IsActivateCode(CardId.ShaddollDragon,CardId.ShaddollSquamata)));
        }
        private bool PendingNegation(ClientCard card)
        {
            return _pendingNegation.Contains(card) || Duel.CurrentChainInfo.Any(link=>link.ActivatePlayer==0 &&
                link.Targets.Contains(card) && link.IsActivateCode(CardId.ElShaddollApkallone,CardId.EffectVeiler));
        }
        private IList<ClientCard> SelectCoordinatedTarget(IList<ClientCard> cards,int min,int max,bool negate,bool cancelable,bool schism = false,int effectOwner = 0)
        {
            int owner = effectOwner != 0 ? effectOwner : GetPromptOwnerId();
            int action = owner == CardId.TohushaddollGrysta ? _grystaCopyId : owner;
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            bool dragonDestroy = owner == CardId.ShaddollDragon && (solving != null ? solving.ActivateLocation == CardLocation.Grave : _dragonDestroyMode);
            EffectUse use = action == CardId.ShaddollSquamata || dragonDestroy ? EffectUse.Destroy : EffectUse.Bounce;
            Func<ClientCard,int> score;
            if (negate) score = ApkalloneNegateScore;
            else if (schism) score = SchismRemovalScore;
            else score = c => CanApplyEffect(c, CardType.Monster, owner, use, true) ? RemovalScore(c) : -10000;
            List<ClientCard> ranked = cards.OrderByDescending(score).ToList();
            if (cancelable && ranked.All(c=>score(c)<0)) return new List<ClientCard>();
            IList<ClientCard> chosen=FillSelection(ranked,cards,min,max);
            foreach(ClientCard c in chosen) { if (score(c)<0) continue; if(negate) _pendingNegation.Add(c); else _pendingRemoval.Add(c); }
            TraceDecision(negate ? "NEGATE" : "REMOVE",string.Join(",",chosen.Select(c=>c.Id)));
            return chosen;
        }
        private IList<ClientCard> SelectEnemyThreat(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = cards
                .OrderByDescending(RemovalScore)
                .ToList();
            return FillSelection(ordered, cards, min, max);
        }

        private IList<ClientCard> SelectArielTargets(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> enemyTargets = cards
                .Where(c => c.Controller == 1 && GraveDisruptionScore(c) >= 300)
                .OrderByDescending(GraveDisruptionScore)
                .Take(Math.Min(3, max))
                .ToList();
            int targetCount = Math.Max(min, enemyTargets.Count);
            targetCount = Math.Min(targetCount, Math.Min(3, max));
            List<ClientCard> ordered = enemyTargets
                .Concat(cards.Where(c => !enemyTargets.Contains(c)).OrderByDescending(GraveDisruptionScore))
                .ToList();
            return FillSelection(ordered, cards, targetCount, targetCount);
        }

        private IList<ClientCard> SelectIds(IList<ClientCard> cards, int min, int max, params int[] ids)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            foreach (int id in ids)
                ordered.AddRange(cards.Where(c => c.IsCode(id) && !ordered.Contains(c)));
            return FillSelection(ordered, cards, min, max);
        }

        private IList<ClientCard> SelectByScore(
            IList<ClientCard> cards, int min, int max, Func<ClientCard, int> score)
        {
            IList<ClientCard> chosen = FillSelection(cards.OrderByDescending(score).ToList(), cards, min, max);
            if (chosen.Count > 0 && (score.Method.Name == "MeshSearchScore" || score.Method.Name == "LevDumpScore" ||
                score.Method.Name == "ConstructSendScore" || score.Method.Name == "VoidDiscardScore" || score.Method.Name == "ApkalloneDiscardScore"))
                TraceDecision("CHOICE", score.Method.Name + "=" + string.Join(",",chosen.Select(c=>c.Id)));
            return chosen;
        }

        private IList<ClientCard> FillSelection(
            IList<ClientCard> preferred, IList<ClientCard> offered, int min, int max)
        {
            List<ClientCard> result = new List<ClientCard>();
            int targetCount = Math.Min(max, Math.Max(min, 1));
            if (targetCount <= 0) return result;
            foreach (ClientCard card in preferred)
            {
                if (card != null && offered.Contains(card) && !result.Contains(card))
                    result.Add(card);
                if (result.Count == targetCount)
                    break;
            }
            foreach (ClientCard card in offered)
            {
                if (result.Count >= targetCount)
                    break;
                if (!result.Contains(card))
                    result.Add(card);
            }
            return result;
        }

        private int FusionTargetScore(ClientCard card, int ownerId)
        {
            if (card.IsCode(CardId.GranguignolDuskDragon))
                return FusionAllowedByEffect(ownerId, card.Id) ? 200 : -10000;
            if (ownerId == CardId.ShaddollSchism) return SchismPlanScore(card.Id);
            if (_shaddollExtraDeckLock && !card.HasSetcode(ShaddollSetcode))
                return -1000;
            if (ownerId == CardId.DracotailFaimena && Duel.Player == 1 &&
                card.IsCode(CardId.ElShaddollWinda))
                return HasActiveControlShell() && _opponentSpecialSummonsThisTurn>1 ? 45 : 245;
            if (ownerId == CardId.TohushaddollGrysta &&
                (IsLevRouteActive() || CanCommitArmageddonMesh()) &&
                card.IsCode(CardId.ElShaddollMeshachrer))
                return 240;
            if (ownerId == CardId.LevShaddollFusion && _levRecycledWendikuruhu && card.IsCode(CardId.ElShaddollConstruct)) return 2400;
            if (ownerId == CardId.LevShaddollFusion && card.IsCode(CardId.ElShaddollWinda) && FunctionalWinda()) return -500;
            if (ownerId == CardId.LevShaddollFusion && card.IsCode(CardId.ReeshaddollWendikuruhu) && !LiveMonster(card.Id) &&
                (Bot.GetMonsters().Any(c => IsShaddollMonster(c) && c.HasType(CardType.Flip) && c.IsFacedown()) ||
                 (HasGrystaBody() && HasCopyFuel()) ||
                 (_levTributedMesh && Bot.GetMonsterCount() < 4 && Bot.Graveyard.Any(c => IsShaddollMonster(c) && c.HasType(CardType.Flip)))))
                return 275;
            if (ownerId == CardId.LevShaddollFusion &&
                (_levTributedMesh || Bot.HasInMonstersZone(CardId.ElShaddollMeshachrer)))
            {
                if (CanReachLevControlExtension() && card.IsCode(CardId.ElShaddollConstruct))
                    return 240;
                if (!CanReachLevControlExtension() && card.IsCode(CardId.ElShaddollWinda))
                    return 235;
            }
            if (ownerId == CardId.ElShaddollFusion && HasLevControlExtension() &&
                Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                card.IsCode(CardId.ReeshaddollWendikuruhu) && !LiveMonster(CardId.ReeshaddollWendikuruhu))
                return 270;
            if (ownerId == CardId.ElShaddollFusion && Duel.Player == 1 &&
                card.IsCode(CardId.ElShaddollApkallone) &&
                BestPersistentOpponentThreatScore() >= 750)
                return 280;
            if (ownerId == CardId.TohushaddollGrysta && Duel.Player == 1 &&
                _levOpponentTurnBranch && Bot.Hand.Any(IsShaddollCard) &&
                card.IsCode(CardId.ElShaddollShekhinaga))
                return 255;
            if (ownerId == CardId.ShaddollFusion && Duel.Player == 0 && card.IsCode(CardId.ElShaddollApkallone) &&
                (Enemy.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() && c.IsCode(CardId.ElShaddollWinda)) ||
                 Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any(c=>EnemyNegateCanAnswer(c,null,CardType.Monster,true) && ApkalloneNegateScore(c)>0))) return 310;
            int score = 0;
            if (card.IsCode(CardId.ElShaddollWinda))
            {
                score = Duel.Player == 1 ? 130 : Duel.Phase == DuelPhase.Main2 ? 82 : 35;
                if (FunctionalWinda()) score -= 100;
                if (HasActiveControlShell() && (Duel.Player==0 || _opponentSpecialSummonsThisTurn>1)) score -= 180;
            }
            else if (card.IsCode(CardId.ElShaddollConstruct))
            {
                score = 125;
                if (Enemy.GetMonsters().Any(c => IsBattleObstacle(c) && c.IsSpecialSummoned)) score += 45;
            }
            else if (card.IsCode(CardId.ElShaddollApkallone))
            {
                bool liveFaceup = Enemy.GetMonsters().Concat(Enemy.GetSpells())
                    .Any(c => c.IsFaceup() && !c.IsDisabled());
                score = liveFaceup ? (Duel.Player == 1 ? 125 : 104) : 72;
                if (FunctionalWinda() && liveFaceup) score += 15;
            }
            else if (card.IsCode(CardId.ElShaddollMeshachrer))
                score = Bot.LifePoints > 1600 && Bot.Hand.Count >= 2 ? 92 : 58;
            else if (card.IsCode(CardId.ReeshaddollWendikuruhu))
                score = LiveMonster(CardId.ReeshaddollWendikuruhu) ? 40 : Bot.GetMonsters().Any(c => c.IsFacedown() && c.HasType(CardType.Flip)) ? 230 : 145;
            else if (card.IsCode(CardId.ElShaddollShekhinaga))
                score = Bot.Hand.Any(IsShaddollCard) ? 85 : 60;
            else if (card.IsCode(CardId.ElShaddollAnoyatyllis))
                score = 30; // Do not infer a Spell/Trap revival strategy from hand/GY size alone.

            if (ShouldStopSpecialSummoning(CardLocation.Extra) && FunctionalBoard())
                score -= 60;
            return score;
        }

        private int FusionMaterialScore(ClientCard card)
        {
            if (PromptIs(CardId.ShaddollSchism))
                return SafeSchismMaterial(card) ?
                    (card.Location == CardLocation.Grave ? 1000 : 100) : -10000;
            // Reserve the three intended materials before generic scoring.
            if (_pendingFusionTargetId == CardId.ElShaddollMeshachrer &&
                _armageddonRouteStarted && !_armageddonMeshSummoned && !IsLevRouteActive())
            {
                if (card.IsCode(CardId.ArmageddonKnight) &&
                    card.Location == CardLocation.MonsterZone) return 2000;
                if (card.Location == CardLocation.Grave &&
                    card.IsCode(CardId.ShaddollSquamata, CardId.TohushaddollGrysta))
                    return 1900;
                if (card.Location == CardLocation.Hand) return -1000;
            }
            if (_pendingFusionTargetId == CardId.ElShaddollMeshachrer &&
                IsLevRouteActive() && _levRouteStage >= LevRouteStage.TohushaddollSent)
            {
                if (card.Location == CardLocation.Grave && card.IsCode(
                    CardId.ElShaddollApkallone,
                    CardId.ShaddollSquamata,
                    CardId.TohushaddollGrysta))
                    return 500;
                if (card.Location == CardLocation.Hand && IsShaddollCard(card))
                    return -600;
                if (card.Location == CardLocation.Hand)
                    return -500;
            }
            if (_pendingFusionTargetId == CardId.GranguignolDuskDragon)
            {
                if (card.IsCode(CardId.BlazingCartesia)) return 280;
                if (IsShaddollMonster(card)) return 220 + ShaddollSendScore(card);
                if (card.IsCode(CardId.AshBlossomJoyousSpring, CardId.EffectVeiler,
                    CardId.DrollLockBird, CardId.MaxxC,
                    CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia)) return -100;
            }
            if (_pendingFusionTargetId == CardId.ReeshaddollWendikuruhu &&
                HasLevControlExtension())
            {
                if (card.IsCode(CardId.ElShaddollConstruct)) return 260;
                if (card.IsCode(CardId.ReeshaddollWendi)) return 250;
            }
            if (_pendingFusionTargetId == CardId.ElShaddollApkallone &&
                _levOpponentTurnBranch)
            {
                if (card.IsCode(CardId.ShaddollSquamata)) return 270;
                if (card.IsCode(CardId.NaelshaddollAriel)) return 260;
            }
            if (_pendingFusionTargetId == CardId.ElShaddollShekhinaga &&
                _levOpponentTurnBranch)
            {
                if (card.HasAttribute(CardAttribute.Earth) && !IsShaddollCard(card)) return 270;
                if (card.IsCode(CardId.TohushaddollGrysta)) return 260;
            }
            int score = 0;
            if (card.Location == CardLocation.Grave) score += 35;
            if (card.Location == CardLocation.Deck) score += 30;
            if (card.Location == CardLocation.MonsterZone && card.IsFacedown()) score += 16;
            if (card.Location == CardLocation.Hand) score -= 8;
            bool banishMaterial = PromptIs(CardId.ShaddollSchism) || PromptIs(CardId.TohushaddollGrysta);
            if (IsShaddollMonster(card) && !banishMaterial && card.Location != CardLocation.Grave &&
                !EffectUsed(card.Id)) score += Math.Max(0, ShaddollSendScore(card) - 25);
            if (banishMaterial && card.Location == CardLocation.Hand) score -= 50;
            if (card.IsCode(CardId.AshBlossomJoyousSpring, CardId.EffectVeiler,
                CardId.DrollLockBird, CardId.MaxxC,
                CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia)) score -= 18;
            if (card.IsCode(CardId.BlazingCartesia, CardId.DracotailFaimena)) score -= 25;
            if (ShouldPreserveMonster(card)) score -= 300;
            if (banishMaterial && card.Location == CardLocation.MonsterZone &&
                card.IsFaceup() && card.HasType(CardType.Fusion) && !card.IsDisabled()) score -= 160;
            if (card.Id == _pendingFusionTargetId) score -= 100;
            return score;
        }

        private int LevDumpScore(ClientCard card)
        {
            // Preserve the Deployment reveal; do not spend it as a generic dump.
            if (card.IsCode(CardId.GranguignolDuskDragon)) return -10000;
            if (card.IsCode(CardId.LunalightPerfumeDancer) && CanWinBattleWithPerfume()) return 2100;
            if (card.IsCode(CardId.InfernoidEvil) && (IsArmageddonLevRoute() || (VoidCanSearchNow() && LiveMonster(CardId.ElShaddollMeshachrer) &&
                !EffectUsed(CardId.VoidUnleashing) && !Bot.HasInHand(CardId.VoidUnleashing) &&
                (!Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion) || !Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism)) &&
                Bot.HasInDeck(CardId.VoidUnleashing)))) return 2000;
            if (card.IsCode(CardId.ElShaddollApkallone) && CanStartLevPrimaryRoute()) return 1900;
            if (card.IsCode(CardId.InfernoidEvil) && Bot.HasInDeck(CardId.VoidUnleashing) &&
                HasVoidRevealInHand() && !Bot.HasInHandOrInSpellZone(CardId.VoidUnleashing)) return 126;
            if (card.IsCode(CardId.ElShaddollMeshachrer) && Bot.Graveyard.Any(IsShaddollMonster) &&
                Bot.GetMonsterCount() < 5) return 112;
            if (card.IsCode(CardId.ReeshaddollWendikuruhu) && HasRecoverableShaddollCard()) return 105;
            if (card.IsCode(CardId.ElShaddollConstruct) && HasRecoverableSpellTrap()) return 102;
            if (card.IsCode(CardId.ElShaddollApkallone)) return 100;
            if (card.IsCode(CardId.ElShaddollWinda, CardId.ElShaddollShekhinaga,
                CardId.ElShaddollAnoyatyllis)) return HasRecoverableSpellTrap() ? 88 : 40;
            return 10;
        }

        private int LevTributeScore(ClientCard card)
        {
            int score = card.GetDefensePower();
            if (card.Attack == 0) score -= 1000;
            if (card.IsDisabled()) score -= 800;
            if (card.IsCode(CardId.ElShaddollConstruct, CardId.ElShaddollApkallone)) score -= 300;
            return score;
        }

        private int ShaddollSendScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetSpells().Any(c =>
                IsPersistentOpponentThreat(c) && OpponentThreatScore(c) >= 800) &&
                !EffectUsed(CardId.ShaddollDragon)) return 190;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetSpells().Any(c => c.IsFacedown() || !c.IsDisabled()) &&
                !EffectUsed(CardId.ShaddollDragon)) return 115;
            if (card.IsCode(CardId.NaelshaddollAriel) && UsefulEnemyGrave() &&
                !EffectUsed(CardId.NaelshaddollAriel)) return 110;
            if (card.IsCode(CardId.TohushaddollGrysta) && !EffectUsed(CardId.TohushaddollGrysta) &&
                HasFusionResources(true)) return 106;
            if (card.IsCode(CardId.ShaddollSquamata) && !EffectUsed(CardId.ShaddollSquamata)) return 102;
            if (card.IsCode(CardId.ShaddollHedgehog) && !EffectUsed(CardId.ShaddollHedgehog)) return 96;
            if (card.IsCode(CardId.ReeshaddollWendi) && !EffectUsed(CardId.ReeshaddollWendi) &&
                Bot.GetMonsterCount() < 5) return LiveMonster(CardId.ReeshaddollWendikuruhu) ? 175 : 112;
            if (card.IsCode(CardId.ShaddollBeast) && !EffectUsed(CardId.ShaddollBeast)) return 80;
            return IsShaddollCard(card) ? 25 : 5;
        }

        private int DiscardScore(ClientCard card)
        {
            // Used by effect-based discard prompts (Apkallone, Shekhinaga, Void).
            // Higher score = better net value when sent to GY by an effect.
            int copies = Bot.Hand.Count(c => c.IsCode(card.Id));
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                Bot.HasInHand(CardId.ReeshaddollWendi) && Bot.HasInHand(CardId.ElShaddollFusion) &&
                card.IsCode(CardId.ArmageddonKnight)) return 160;
            if (ReservedForRoute(card)) return -800;
            if (card.IsCode(CardId.ArmageddonKnight) && copies <= 1 && !_normalSpent &&
                CanStartArmageddonRoute() && Bot.HasInDeck(CardId.ShaddollSquamata)) return -700;
            int score = copies > 1 ? 35 : 0;
            if (IsMulcharmy(card) && Bot.GetFieldCount()>0) return 105 + (FunctionalBoard() || HasActiveControlShell() ? 70 : 0) + score;
            if (card.IsCode(CardId.VoidUnleashing) && !VoidHasPotentialPair()) return 200 + score;
            if (card.IsCode(CardId.LevShaddollFusion) && LiveLev()) return 115 + score;
            if (card.IsCode(CardId.AshBlossomJoyousSpring, CardId.MaxxC,
                CardId.EffectVeiler, CardId.DrollLockBird,
                CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia))
                return score - (card.IsCode(CardId.MaxxC) ? 100 : 75);
            if (IsShaddollMonster(card) && !EffectUsed(card.Id))
                score += Math.Max(0, ShaddollSendScore(card) - 25);
            if (card.IsCode(CardId.ShaddollBeast) && !EffectUsed(card.Id) && Bot.Deck.Count > 0)
                score += 35;
            if (card.IsCode(CardId.ShaddollSchism, CardId.ElShaddollFusion, CardId.LevShaddollFusion) &&
                !Bot.HasInSpellZone(card.Id) && copies <= 1) score -= 120;
            if (card.IsCode(CardId.ShaddollFusion) && copies<=1 && EnemyDeckFusionWindow() && Bot.ExtraDeck.Any(c=>ShaddollFusionIds.Contains(c.Id))) score -= 145;
            if (card.IsCode(CardId.ShaddollFusion) && copies<=1 && !Bot.HasInSpellZone(card.Id)) score -= 35;
            if (IsShaddollMonster(card) && Bot.Hand.Count(IsShaddollMonster)<=1 && !Bot.HasInMonstersZone(CardId.ReeshaddollWendikuruhu) &&
                Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion)) score -= 80;
            if (IsShaddollCard(card) && LiveMonster(CardId.ElShaddollShekhinaga) && Bot.Hand.Count(IsShaddollCard)<=1) score -= 250;
            if (card.IsCode(CardId.BlazingCartesia, CardId.DracotailFaimena,
                CardId.TearlamentsScheiren, CardId.FusionDeployment, CardId.ArmageddonKnight)) score -= 45;
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                card.IsCode(CardId.ReeshaddollWendi) && copies <= 1) score -= 500;
            return score;
        }

        private int ApkalloneSearchScore(ClientCard card)
        {
            if (IsLevRouteActive() && CanContinueLevThroughSquamata() &&
                card.IsCode(CardId.ShaddollSquamata)) return 240;
            return ShaddollSearchScore(card);
        }

        private int ApkalloneDiscardScore(ClientCard card)
        {
            if (IsLevRouteActive() && CanContinueLevThroughSquamata() &&
                card.IsCode(CardId.ShaddollSquamata)) return 240;
            if (IsLevRouteActive() && !CanContinueLevThroughSquamata() &&
                CanContinueLevThroughGrysta() && card.IsCode(CardId.TohushaddollGrysta)) return 240;
            return DiscardScore(card);
        }

        private int SquamataSendScore(ClientCard card)
        {
            if (Duel.Player == 1 && LiveMonster(CardId.ElShaddollApkallone) &&
                card.IsCode(CardId.TohushaddollGrysta) && !EffectUsed(card.Id) &&
                Bot.Graveyard.Any(c => c.IsCode(CardId.ShaddollSquamata)) && Bot.HasInExtra(CardId.ElShaddollShekhinaga)) return 220;
            if ((IsLevRouteActive() || (_armageddonRouteStarted && !_armageddonMeshSummoned &&
                Bot.HasInMonstersZone(CardId.ArmageddonKnight))) &&
                Bot.HasInExtra(CardId.ElShaddollMeshachrer) &&
                card.IsCode(CardId.TohushaddollGrysta)) return 240;
            return ShaddollSendScore(card);
        }

        private int ConstructSendScore(ClientCard card)
        {
            if (CanReachLevControlExtension())
            {
                if (!Bot.HasInHand(CardId.ReeshaddollWendi) && !EffectUsed(CardId.ShaddollHedgehog) && Bot.HasInDeck(CardId.ReeshaddollWendi) &&
                    card.IsCode(CardId.ShaddollHedgehog)) return 240;
                if (Bot.HasInHand(CardId.ReeshaddollWendi) &&
                    card.IsCode(CardId.ReshShaddollIncarnation)) return 235;
            }
            if (!LiveMonster(CardId.ReeshaddollWendikuruhu) && !EffectUsed(CardId.ShaddollHedgehog) && !Bot.HasInHand(CardId.ReeshaddollWendi) &&
                Bot.HasInDeck(CardId.ReeshaddollWendi) && card.IsCode(CardId.ShaddollHedgehog) &&
                Bot.HasInExtra(CardId.ReeshaddollWendikuruhu) && (Bot.HasInHandOrInSpellZone(CardId.ShaddollFusion) || Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion))) return 230;
            if (card.IsCode(CardId.ReeshaddollWendi) && !EffectUsed(card.Id) && Bot.GetMonsterCount() < 5 &&
                (LiveMonster(CardId.ReeshaddollWendikuruhu) || !FunctionalBoard()) && Bot.HasInDeck(CardId.TohushaddollGrysta)) return 185;
            return ContextualShaddollSendScore(card);
        }

        private int VoidDiscardScore(ClientCard card)
        {
            // Void may discard any card, not only the revealed Shaddoll. Preserve
            // unique continuation pieces, then compare live GY value with the
            // opportunity cost of spending a non-engine card.
            if (Duel.Player == 0 && (IsLevRouteActive() || CurrentPlan().Goal == RouteGoal.MeshRecovery) &&
                Bot.HasInMonstersZone(CardId.ElShaddollMeshachrer) &&
                Bot.HasInExtra(CardId.ElShaddollConstruct) &&
                !EffectUsed(CardId.LevShaddollFusion + 1))
            {
                if (card.IsCode(CardId.ReeshaddollWendi) &&
                    Bot.Hand.Count(c => c.IsCode(CardId.ReeshaddollWendi)) == 1)
                    return -650;
                if (card.IsCode(CardId.ElShaddollFusion, CardId.ShaddollSchism) &&
                    Bot.Hand.Count(c => c.IsCode(card.Id)) == 1)
                    return -625;
                // Compare actual discard value below; non-engine is not automatically expendable.
            }
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                Bot.HasInHand(CardId.ReeshaddollWendi) && Bot.HasInHand(CardId.ElShaddollFusion) &&
                Bot.Hand.All(c => c.IsCode(CardId.ReeshaddollWendi, CardId.ElShaddollFusion,
                    CardId.ShaddollSchism)) && card.IsCode(CardId.ShaddollSchism)) return 1000;
            if (card.IsCode(CardId.LevShaddollFusion) &&
                !EffectUsed(CardId.LevShaddollFusion) &&
                !Bot.HasInSpellZone(CardId.LevShaddollFusion)) return -500;
            if (card.IsCode(CardId.ShaddollSchism) &&
                !Bot.HasInSpellZone(CardId.ShaddollSchism) &&
                Bot.Hand.Count(c => c.IsCode(CardId.ShaddollSchism)) == 1) return -480;
            if (card.IsCode(CardId.ElShaddollFusion) && IsLevRouteActive() &&
                !Bot.HasInSpellZone(CardId.ElShaddollFusion) &&
                Bot.Hand.Count(c => c.IsCode(CardId.ElShaddollFusion)) == 1) return -470;
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollConstruct))
            {
                if (card.IsCode(CardId.ReeshaddollWendi, CardId.ElShaddollFusion,
                    CardId.ShaddollSchism) && Bot.Hand.Count(c => c.IsCode(card.Id)) == 1)
                    return -500;
            }
            return DiscardScore(card);
        }

        private int ScheirenSendScore(ClientCard card)
        {
            if (!IsShaddollMonster(card)) return -300;
            int score = ShaddollSendScore(card) + 40;
            if (card.IsCode(CardId.ShaddollBeast)) score += 20;
            if (ReservedForRoute(card)) score -= 800;
            return score;
        }

        private int ArmageddonSendScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSquamata) && Bot.HasInDeck(CardId.TohushaddollGrysta) &&
                Bot.HasInExtra(CardId.ElShaddollMeshachrer)) return 220;
            return ShaddollSendScore(card);
        }

        private bool CanDevelopArmageddonRoute()
        {
            return Duel.Player == 0 && !FunctionalWinda() && ControlBoardScore() < 100 &&
                !HasPreparedLevControlBoard() && !_armageddonMeshSummoned;
        }

        private bool CanStartArmageddonRoute()
        {
            return CanDevelopArmageddonRoute() && !EffectUsed(CardId.ShaddollSquamata) &&
                !EffectUsed(CardId.TohushaddollGrysta) && Bot.HasInDeck(CardId.TohushaddollGrysta) &&
                Bot.HasInExtra(CardId.ElShaddollMeshachrer);
        }

        private int ContextualShaddollSendScore(ClientCard card)
        {
            if (card.IsCode(CardId.LevShaddollFusion) && (LiveLev() || Bot.HasInHand(CardId.LevShaddollFusion))) return -250;
            if (EffectUsed(card.Id)) return -100;
            if (card.IsCode(CardId.TohushaddollGrysta) &&
                (FunctionalWinda() || ShouldStopSpecialSummoning(CardLocation.Extra))) return -100;
            if (card.IsCode(CardId.ReeshaddollWendi) &&
                (Bot.GetMonsterCount() >= 5 || (FunctionalWinda() && _specialSummonsThisTurn >= 1))) return -100;
            if (card.IsCode(CardId.ShaddollBeast) && Bot.Deck.Count > 0) return 135;
            if (card.IsCode(CardId.ShaddollHedgehog) && ShaddollMainMonsterIds.Any(Bot.HasInDeck))
                return Bot.Hand.Count <= 2 ? 145 : 105;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetSpellCount() > 0) return 175;
            if (card.IsCode(CardId.NaelshaddollAriel) && UsefulEnemyGrave()) return 150;
            return ShaddollSendScore(card);
        }

        private int ReshCostScore(ClientCard card)
        {
            int score = EffectUsed(card.Id) ? 110 : 25;
            if (card.IsCode(CardId.ShaddollBeast, CardId.ShaddollHedgehog,
                CardId.ShaddollDragon, CardId.ShaddollSquamata)) score += 15;
            if (card.IsCode(CardId.ElShaddollWinda, CardId.ElShaddollConstruct,
                CardId.ElShaddollApkallone, CardId.ReeshaddollWendikuruhu)) score -= 100;
            if (card.IsSpell() || card.IsTrap()) score -= 80;
            if (HasGrystaBody() && card.HasType(CardType.Flip) && !card.IsCode(CardId.TohushaddollGrysta) && Bot.Graveyard.Count(c=>c.IsCode(card.Id))<=1)
                score -= 60 + Math.Max(0,CopyEffectValue(card.Id))/3;
            return score;
        }

        private int ShaddollMonsterSearchScore(ClientCard card)
        {
            if (card.IsCode(CardId.TohushaddollGrysta) && !EffectUsed(card.Id) && HasFusionResources(true)) return 110;
            if (card.IsCode(CardId.ShaddollSquamata) && !EffectUsed(card.Id)) return 100;
            if (EffectUsed(card.Id)) return 30;
            if (card.IsCode(CardId.ReeshaddollWendi) && Bot.GetMonsterCount() < 5) return 90;
            if (card.IsCode(CardId.NaelshaddollAriel) && UsefulEnemyGrave()) return 88;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetSpellCount() > 0) return 85;
            if (card.IsCode(CardId.ShaddollBeast)) return 70;
            return 20;
        }

        private int HedgehogMonsterSearchScore(ClientCard card)
        {
            if ((LiveLev() || CurrentPlan().Goal == RouteGoal.ConstructControl || Bot.HasInHandOrInSpellZone(CardId.ShaddollFusion) || Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion)) && LiveMonster(CardId.ElShaddollConstruct) &&
                !Bot.HasInHand(CardId.ReeshaddollWendi) && !EffectUsed(CardId.ReeshaddollWendi) &&
                card.IsCode(CardId.ReeshaddollWendi)) return 240;
            return ShaddollMonsterSearchScore(card);
        }

        private int ShaddollSearchScore(ClientCard card)
        {
            if (card.IsCode(CardId.LevShaddollFusion) &&
                Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion)) return -250;
            if (card.IsCode(CardId.ShaddollSchism) && !Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism)) return 120;
            if (card.IsCode(CardId.ReshShaddollIncarnation) &&
                (Bot.Graveyard.Any(c => c.IsCode(CardId.ElShaddollWinda)) ||
                 Bot.GetMonsters().Any(c => c.IsFacedown() && c.HasType(CardType.Flip)))) return 116;
            if (card.IsCode(CardId.ElShaddollFusion) && !Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion)) return 112;
            if (card.IsCode(CardId.ShaddollFusion) && !Bot.HasInHandOrInSpellZone(CardId.ShaddollFusion)) return 108;
            if (card.IsCode(CardId.LevShaddollFusion) && !Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion)) return 104;
            return card.IsMonster() ? ShaddollMonsterSearchScore(card) : 35;
        }

        private int VoidSearchScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSchism, CardId.ElShaddollFusion) &&
                Bot.HasInHandOrInSpellZone(card.Id)) return -200;
            if (card.IsCode(CardId.LevShaddollFusion) &&
                Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion)) return -250;
            if (IsLevRouteActive() && (Bot.HasInMonstersZone(CardId.ElShaddollMeshachrer) ||
                Bot.HasInMonstersZone(CardId.ElShaddollConstruct)))
            {
                if (card.IsCode(CardId.ShaddollSchism) && !Bot.HasInHandOrInSpellZone(card.Id)) return 240;
                if (card.IsCode(CardId.ElShaddollFusion) && !Bot.HasInHandOrInSpellZone(card.Id)) return 235;
                if ((_voidRevealedType & CardType.Monster) == 0 &&
                    card.IsCode(CardId.ReeshaddollWendi)) return 230;
            }
            if (card.IsCode(CardId.ShaddollSchism)) return 130;
            if (card.IsCode(CardId.LevShaddollFusion) && !_shaddollExtraDeckLock &&
                !Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion)) return 125;
            if (card.IsCode(CardId.ElShaddollFusion)) return 115;
            if (card.IsCode(CardId.ShaddollFusion)) return Enemy.GetMonsters().Any(IsExtraDeckMonster) ? 122 : 105;
            if (card.IsCode(CardId.TohushaddollGrysta) && HasFusionResources(true)) return 118;
            if (card.IsCode(CardId.ShaddollSquamata)) return 112;
            return ShaddollSearchScore(card);
        }

        private int MeshSearchScore(ClientCard card)
        {
            if (!IsLevRouteActive() && IsArmageddonLevRoute() &&
                !Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion) &&
                card.IsCode(CardId.LevShaddollFusion)) return 2000;
            if (IsLevRouteActive() && card.IsCode(CardId.VoidUnleashing) &&
                !EffectUsed(CardId.VoidUnleashing)) return 250;
            if (IsLevRouteActive() && HasVoidRevealInHand() &&
                card.IsCode(CardId.ElShaddollFusion)) return 225;
            if (card.IsCode(CardId.ShaddollSchism) && !Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism)) return 120;
            return ShaddollSearchScore(card);
        }

        private int WendiSummonScore(ClientCard card)
        {
            bool engine = LiveMonster(CardId.ReeshaddollWendikuruhu);
            if (engine && !EffectUsed(CardId.NaelshaddollAriel) && !Bot.HasInHand(CardId.NaelshaddollAriel) && HasArielRecovery() &&
                card.IsCode(CardId.NaelshaddollAriel) && !Bot.GetMonsters().Any(c=>c.IsCode(card.Id)))
                return Bot.Banished.Any(c=>c.IsCode(CardId.TohushaddollGrysta)) && !HasGrystaBody() ? 360 : 270;
            if (card.IsCode(CardId.TohushaddollGrysta) && !HasGrystaBody() && (HasCopyFuel() || engine)) return 310;
            if (engine && card.IsCode(CardId.ShaddollSquamata) && Enemy.GetMonsterCount()>0) return 220;
            if (engine && card.IsCode(CardId.ShaddollDragon) && Enemy.GetFieldCount()>0) return 215;
            if (card.IsCode(CardId.NaelshaddollAriel) && HasArielRecovery()) return 195;
            if (card.IsCode(CardId.ShaddollBeast)) return Bot.Hand.Count<=2 ? 175 : 120;
            if (card.IsCode(CardId.ShaddollHedgehog)) return !Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism) ? 180 : 130;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetFieldCount()>0) return 140;
            if (card.IsCode(CardId.ShaddollSquamata) && Enemy.GetMonsterCount()>0) return 145;
            return 20;
        }

        private int FlipUpScore(ClientCard card)
        {
            if (card.Controller != 0 || !card.IsFacedown() || !card.HasType(CardType.Flip) || EffectUsed(card.Id)) return -100;
            if (card.IsCode(CardId.TohushaddollGrysta)) return BestCopyEffectValue();
            return CopyEffectValue(card.Id);
        }

        private int FlipCopyScore(ClientCard card)
        {
            return CopyEffectValue(card.Id);
        }

        private int ResetFlipScore(ClientCard card)
        {
            return card.Controller == 0 ? FlipDownControlScore(card) : -10000;
        }

        private int GraveDisruptionScore(ClientCard card)
        {
            if (card == null || card.Controller != 1) return -1000;
            if (IsHandTrap(card)) return 0;
            int score = 100;
            if (IsShaddollMonster(card))
            {
                if (card.IsCode(CardId.TohushaddollGrysta)) return 1300;
                if (card.IsCode(CardId.ReeshaddollWendikuruhu, CardId.ReeshaddollWendi, CardId.ShaddollSquamata, CardId.ShaddollDragon, CardId.ShaddollBeast, CardId.NaelshaddollAriel)) return 950;
                return 650;
            }
            if (card.Controller == 1)
            {
                OpponentCapability capability = GetOpponentCapabilities(card);
                if ((capability & OpponentCapability.GraveInteraction) != 0) score += 300;
                if ((capability & OpponentCapability.Negate) != 0) score += 250;
                if ((capability & OpponentCapability.Recursion) != 0) score += 220;
                if ((capability & OpponentCapability.QuickInteraction) != 0) score += 100;
                if (card.IsCode(CardId.OrcustCymbalSkeleton)) score += 800;
                else if (card.IsCode(CardId.OrcustHarpHorror)) score += 780;
                else if (card.IsCode(CardId.OrcustKnightmare)) score += 760;
                else if (card.IsCode(CardId.WorldLegacyWorldWand)) score += 740;
                else if (card.IsCode(CardId.DingirsuOrcustEveningStar)) score += 720;
                else if (card.IsCode(CardId.OrcustratedClimax)) score += 700;
                else if (card.IsCode(CardId.PhantomKnightsFogBlade)) score += 680;
                else if (card.IsCode(CardId.ReturnOfTheDragonLords)) score += 650;
                else if (card.IsCode(CardId.WhiteStoneOfAncients)) score += 630;
                else if (card.IsCode(CardId.MagicianNavigation)) score += 850;
                else if (card.IsCode(CardId.DarkMagicianTheDragonKnight)) score += 760;
                else if (card.IsCode(CardId.DarkMagician)) score += 720;
            }
            if (card.IsMonster()) score += 25;
            if (card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link)) score += 20;
            return score + card.Level;
        }

        private string EffectText(ClientCard c)
        {
            return c == null || c.Data == null ? string.Empty : (c.Data.Description ?? string.Empty).ToLowerInvariant();
        }
        private bool LikelyUnaffectedByActivatedEffect(ClientCard target, CardType sourceType, int originalLevel)
        {
            if (target == null || !target.IsFaceup() || target.IsDisabled()) return false;
            string text = EffectText(target).Replace('’', '\'');
            foreach (string sentence in text.Split(new[] { '.', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int start = sentence.IndexOf("unaffected by", StringComparison.Ordinal);
                if (start < 0) continue;
                string scope = sentence.Substring(start + "unaffected by".Length).Trim();
                // This is a conservative text heuristic, not a complete rules engine.
                // Do not interpret protection from own effects as opponent protection.
                if (scope.StartsWith("your own ") || scope.StartsWith("your card effects")) continue;
                // Protection clauses that grant an effect to another target are not self-immunity.
                string before = sentence.Substring(0, start).Trim();
                if (before.Contains("target 1") || before.Contains("that target") || before.Contains("that face-up monster") || before.Contains("monsters you control") || before.Contains("in your monster zone")) continue;
                if (scope.StartsWith("other cards' effects") || scope.StartsWith("other card effects") ||
                    scope.StartsWith("card effects") || scope.StartsWith("your opponent's card effects") ||
                    scope.StartsWith("activated card effects") || scope.StartsWith("your opponent's activated card effects")) return true;
                bool spell = (sourceType & CardType.Spell) != 0;
                bool trap = (sourceType & CardType.Trap) != 0;
                bool monster = (sourceType & CardType.Monster) != 0;
                string prefix = scope.StartsWith("your opponent's ") ? scope.Substring("your opponent's ".Length) : scope;
                if ((spell || trap) && (prefix.StartsWith("activated spell/trap effects") ||
                    prefix.StartsWith("spell/trap effects") || prefix.StartsWith("spell and trap effects") ||
                    prefix.StartsWith("activated spell and trap effects"))) return true;
                if (spell && (prefix.StartsWith("spell effects") || prefix.StartsWith("activated spell effects"))) return true;
                if (trap && (prefix.StartsWith("trap effects") || prefix.StartsWith("activated trap effects"))) return true;
                if (monster && (prefix.StartsWith("monster effects") || prefix.StartsWith("activated monster effects"))) return true;
                if (monster && originalLevel > 0 && originalLevel < target.Level &&
                    (scope.Contains("activated effects of your opponent's monsters whose original levels/ranks are lower than this card's level") ||
                     scope.Contains("activated effects of monsters whose original levels/ranks are lower than this card's level"))) return true;
            }
            return false;
        }
        private bool CanCreditSchismRemoval(ClientCard target)
        {
            return target != null && target.Controller == 1 && target.IsFaceup() &&
                CanApplyEffect(target, CardType.Trap, CardId.ShaddollSchism, EffectUse.Send, false);
        }
        private int SchismRemovalScore(ClientCard target)
        {
            return CanCreditSchismRemoval(target) ? RemovalScore(target) : -10000;
        }
        private bool HasBattleImmunity(ClientCard c)
        {
            if (c == null || !c.IsFaceup() || c.IsDisabled()) return false;
            string t=EffectText(c);
            return c.IsMonsterInvincible() || t.Contains("cannot be destroyed by battle") || t.Contains("cannot be destroyed by card effects or battle") || t.Contains("cannot be destroyed by battle or card effects");
        }
        private bool HasDamageReflection(ClientCard c)
        {
            if (c == null || !c.IsFaceup() || c.IsDisabled()) return false;
            string t=EffectText(c);
            return t.Contains("your opponent takes any battle damage") || t.Contains("your opponent takes the battle damage") ||
                t.Contains("your opponent takes all battle damage") || t.Contains("your opponent takes double any battle damage") ||
                t.Contains("inflict damage to your opponent equal to that attacking monster");
        }
        private bool IsYubelMonster(ClientCard c)
        {
            return c != null && c.IsFaceup() && c.IsMonster() && (c.Name ?? string.Empty).IndexOf("Yubel", StringComparison.OrdinalIgnoreCase)>=0;
        }
        private bool ReflectedBattle(ClientCard defender)
        {
            if (defender == null || !defender.IsFaceup()) return false;
            if (HasDamageReflection(defender)) return true;
            return Enemy.GetSpells().Any(c => HasDamageReflection(c) &&
                (!EffectText(c).Contains("\"yubel\"") || IsYubelMonster(defender)));
        }
        private bool IsBattleObstacle(ClientCard c)
        {
            return c != null && c.Controller==1 && c.IsFaceup() && (HasBattleImmunity(c) || ReflectedBattle(c));
        }
        private bool CanClearByOrdinaryBattle(ClientCard target)
        {
            if (target == null || !target.IsFaceup() || IsBattleObstacle(target)) return false;
            return Bot.GetMonsters().Any(a=>a.IsFaceup() && a.IsAttack() && a.Attack > target.GetDefensePower() &&
                !target.IsMonsterDangerous());
        }
        private int RemovalScore(ClientCard c)
        {
            if (c == null || c.Controller != 1 || PendingRemoval(c)) return -10000;
            int score = Math.Max(0, OpponentThreatScore(c));
            if (c.IsMonster() && c.IsFaceup())
            {
                if (IsBattleObstacle(c)) score += 1000;
                string t = EffectText(c);
                if (!c.IsDisabled() && (t.Contains("tribute this card; special summon") || t.Contains("tribute this card: special summon"))) score += 700;
                if (t.Contains("banish this card from your gy") || t.Contains("if this card is sent to the gy")) score -= 180;
                if (Duel.Player==0 && CanClearByOrdinaryBattle(c)) score -= 450;
            }
            if (EnemyNegateCanAnswer(c,null,CardType.Monster,true) && HasActiveControlShell()) score += 900;
            if (HasDamageReflection(c)) score += 900;
            if (c.IsCode(CardId.ShaddollSchism,CardId.ReeshaddollWendikuruhu)) score += 650;
            if (c.IsCode(CardId.TohushaddollGrysta) && Enemy.Graveyard.Any(x=>IsShaddollMonster(x) && x.HasType(CardType.Flip))) score += 420;
            if (c.IsFacedown()) score += 180;
            return score;
        }
        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (attacker == null || defender == null) return false;
            // A face-down engine can trigger even when its battle stats are low.
            // Avoid risking the control board for insignificant damage/removal.
            bool enemyFlipBounce = Enemy.GetMonsters().Any(c=>c.IsFaceup() && !c.IsDisabled() && c.IsCode(CardId.ReeshaddollWendikuruhu)) &&
                Enemy.GetMonsters().Any(c=>c.IsFacedown()) && Enemy.Graveyard.Any(c=>c.IsCode(CardId.ShaddollDragon));
            if (enemyFlipBounce && defender.IsFacedown() && (attacker.HasType(CardType.Fusion) || ControlEngineValue(attacker)>100) &&
                (defender.Id == 0 || defender.IsCode(CardId.TohushaddollGrysta,CardId.ShaddollDragon)))
            {
                TraceDecision("BATTLE", "hold engine against live flip-bounce window");
                return false;
            }
            bool effectRemoval = attacker.IsCode(CardId.ElShaddollConstruct) && !attacker.IsDisabled() && defender.IsSpecialSummoned &&
                !HasLiveEnemyNegate(attacker,CardType.Monster,attacker.IsSpecialSummoned) &&
                CanApplyEffect(defender,CardType.Monster,attacker.Id,EffectUse.Destroy,false);
            if (!effectRemoval && (ReflectedBattle(defender) || HasBattleImmunity(defender)))
            {
                TraceDecision("BATTLE", "reject " + attacker.Id + " -> " + defender.Id + " (reflection/immunity)");
                return false;
            }
            if (!base.OnPreBattleBetween(attacker, defender)) return false;
            if (effectRemoval) attacker.RealPower = Math.Max(attacker.RealPower, defender.RealPower + 1);
            return true;
        }
        public override BattlePhaseAction OnBattle(IList<ClientCard> attackers,IList<ClientCard> defenders)
        {
            if (defenders.Count != 0) return null; // engine calls our OnSelectAttackTarget for each attacker
            ClientCard direct=attackers.Where(a=>a.CanDirectAttack && !_schismBodies.Contains(a) && a.Attack>0)
                .OrderByDescending(a=>a.Attack).FirstOrDefault();
            if (direct!=null) return AI.Attack(direct,null);
            if (Duel.BattlePhase.CanMainPhaseTwo) return AI.ToMainPhase2();
            if (Duel.BattlePhase.CanEndPhase) return AI.ToEndPhase();
            return null; // unavoidable engine fallback only when no legal phase exit
        }
        public override bool OnSelectBattleReplay()
        {
            ClientCard attacker=Bot.BattlingMonster;
            if (attacker==null) return false;
            IList<ClientCard> defenders=Enemy.GetMonsters();
            if (defenders.Count==0) return !_schismBodies.Contains(attacker);
            return base.OnSelectBattleReplay();
        }
        public override bool OnSelectBattleDirectAttack(ClientCard attacker,bool preselectedAnswer)
        {
            return preselectedAnswer && (attacker==null || !_schismBodies.Contains(attacker));
        }
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker,IList<ClientCard> defenders)
        {
            // No direct attack from a Schism-created monster; engine flag remains authoritative.
            foreach(ClientCard defender in defenders.OrderByDescending(c=>c.IsFaceup() ? OpponentThreatScore(c) : 0))
            {
                attacker.RealPower=attacker.GetAttackPower(); defender.RealPower=defender.GetDefensePower();
                if (!OnPreBattleBetween(attacker,defender)) continue;
                if (attacker.RealPower>defender.RealPower || (attacker.RealPower==defender.RealPower && defender.IsAttack() && !ShouldPreserveMonster(attacker)))
                    return AI.Attack(attacker,defender);
            }
            if (attacker.CanDirectAttack && !_schismBodies.Contains(attacker)) return AI.Attack(attacker,null);
            return null;
        }
        private int ApkalloneNegateScore(ClientCard card)
        {
            int score = OpponentThreatScore(card);
            if (card == null || card.Controller != 1 || !card.IsFaceup() || card.IsDisabled() || PendingRemoval(card) || PendingNegation(card)) return -10000;
            if (!CanApplyEffect(card, CardType.Monster, CardId.ElShaddollApkallone, EffectUse.Negate, true)) return -10000;
            if (EnemyNegateCanAnswer(card,null,CardType.Monster,true) && HasActiveControlShell()) score += 1050;
            if (HasDamageReflection(card)) score += 1600;
            if (HasBattleImmunity(card)) score += 450;
            if (card.IsMonster() && _resolvedFieldEffects.Contains(card) &&
                (GetOpponentCapabilities(card) & (OpponentCapability.QuickInteraction | OpponentCapability.Floodgate | OpponentCapability.Protection)) == 0) score -= 500;
            if (card.IsMonster() && EffectText(card).Contains("banish this card from your gy") &&
                !EffectText(card).Contains("(quick effect)")) score -= 350;
            // Continuous effects and battle triggers remain relevant even after an ignition search.
            if ((card.IsSpell() || card.IsTrap()) && (GetOpponentCapabilities(card) & OpponentCapability.Recursion) != 0)
                score += 200;
            if (card.IsCode(CardId.ElShaddollWinda)) score += 500;
            if (card.IsCode(CardId.ShaddollSchism)) score += 850;
            if (card.IsCode(CardId.ReeshaddollWendikuruhu) && Enemy.GetMonsters().Any(c=>c.IsFacedown())) score += 700;
            if (card.IsCode(CardId.TohushaddollGrysta) && Enemy.Graveyard.Any(c=>IsShaddollMonster(c) && c.HasType(CardType.Flip))) score += 450;
            if (card.IsCode(CardId.ElShaddollConstruct) &&
                Bot.GetMonsters().Any(c => c.IsFaceup() && IsExtraDeckMonster(c))) score += 400;
            if (card.IsCode(CardId.ElShaddollMeshachrer) && Duel.Player == 1 &&
                Enemy.GetMonsters().Any(c => c.IsFaceup() && c.IsCode(CardId.ElShaddollConstruct)))
                score -= 350;
            return score;
        }

        private int OpponentThreatScore(ClientCard card)
        {
            if (card == null || card.Controller != 1)
                return -1000;
            if (!card.IsFaceup() || card.IsDisabled())
                return -500;

            OpponentCapability capability = GetOpponentCapabilities(card);
            int score = 100;
            if ((capability & OpponentCapability.Negate) != 0) score += 350;
            if ((capability & OpponentCapability.Removal) != 0) score += 220;
            if ((capability & OpponentCapability.Recursion) != 0) score += 220;
            if ((capability & OpponentCapability.Protection) != 0) score += 180;
            if ((capability & OpponentCapability.Floodgate) != 0) score += 450;
            if ((capability & OpponentCapability.QuickInteraction) != 0) score += 140;
            if ((capability & OpponentCapability.GraveInteraction) != 0) score += 80;
            if ((capability & OpponentCapability.RepeatableEngine) != 0) score += 350;
            if ((capability & OpponentCapability.AdvantageEngine) != 0) score += 120;
            if (card.HasType(CardType.Continuous | CardType.Field)) score += 200;
            if (HasDamageReflection(card)) score += 1200;
            if (card.IsMonster() && (EffectText(card).Contains("tribute this card; special summon") || EffectText(card).Contains("tribute this card: special summon"))) score += 500;
            if (card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link))
                score += 220;
            if (card.IsMonster())
            {
                score += Math.Max(0, card.Attack) / 40;
                score += Math.Max(0, card.Defense) / 80;
            }
            return score;
        }

        private OpponentCapability GetOpponentCapabilities(ClientCard card)
        {
            if (card == null || card.Data == null)
                return OpponentCapability.None;

            string text = (card.Data.Description ?? string.Empty).ToLowerInvariant();
            OpponentCapability capability = OpponentCapability.None;
            if (text.Contains("negate the activation") || text.Contains("negate that effect") ||
                text.Contains("negate the effect") || text.Contains("negate its effect") ||
                text.Contains("negate its effects") || text.Contains("negate their effect") ||
                text.Contains("negate their effects"))
                capability |= OpponentCapability.Negate;
            if (text.Contains("banish it") || text.Contains("banish that") ||
                text.Contains("banish them") ||
                text.Contains("destroy it") || text.Contains("destroy that") ||
                text.Contains("destroy those") || text.Contains("attach it to this card") ||
                text.Contains("attach that card to this card") ||
                text.Contains("send it to the gy") || text.Contains("send that target to the gy") ||
                text.Contains("return it to the hand") || text.Contains("return that target to the hand") ||
                text.Contains("shuffle it into the deck") || text.Contains("shuffle that target into the deck"))
                capability |= OpponentCapability.Removal;
            if (text.Contains("special summon") &&
                (text.Contains("from your gy") || text.Contains("from the gy") ||
                 text.Contains("hand or gy") || text.Contains("deck, and/or gy")))
                capability |= OpponentCapability.Recursion;
            if (text.Contains("cannot be destroyed") || text.Contains("unaffected by") ||
                text.Contains("cannot target") || text.Contains("cannot be targeted"))
                capability |= OpponentCapability.Protection;
            if (card.IsFloodgate() || text.Contains("your opponent cannot special summon") ||
                text.Contains("neither player can special summon") ||
                text.Contains("your opponent can only special summon") ||
                text.Contains("neither player can activate"))
                capability |= OpponentCapability.Floodgate;
            if (text.Contains("(quick effect)") || text.Contains("during either player's turn"))
                capability |= OpponentCapability.QuickInteraction;
            if (text.Contains("from your gy") || text.Contains("from the gy") ||
                text.Contains("banish this card from your gy"))
                capability |= OpponentCapability.GraveInteraction;
            if (text.Contains("add it to your hand") || text.Contains("add 1 ") ||
                text.Contains("draw 1 card"))
                capability |= OpponentCapability.AdvantageEngine;
            bool persistentCard = card.IsMonster() ||
                card.HasType(CardType.Continuous | CardType.Field);
            if (persistentCard &&
                text.Contains("once per turn") &&
                (capability & (OpponentCapability.Negate | OpponentCapability.Removal |
                    OpponentCapability.Recursion | OpponentCapability.Protection |
                    OpponentCapability.AdvantageEngine)) != 0)
                capability |= OpponentCapability.RepeatableEngine;
            return capability;
        }

        private int BestPersistentOpponentThreatScore()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(IsPersistentOpponentThreat)
                .Select(OpponentThreatScore)
                .DefaultIfEmpty(-1000)
                .Max();
        }

        private bool IsPersistentOpponentThreat(ClientCard card)
        {
            if (card == null || card.Controller != 1 ||
                !card.IsFaceup() || card.IsDisabled())
                return false;
            if (card.IsMonster())
                return true;
            return card.HasType(CardType.Continuous | CardType.Field |
                CardType.Equip | CardType.Pendulum);
        }

        private bool HasUrgentPersistentSpellTrapEngine()
        {
            return Enemy.GetSpells().Any(c =>
                IsPersistentOpponentThreat(c) && OpponentThreatScore(c) >= 800);
        }

        private bool IsSpiritOrderableGraveTrigger(ClientCard card)
        {
            return card != null && (ShaddollMainMonsterIds.Contains(card.Id) ||
                card.IsCode(CardId.TohushaddollGrysta,
                    CardId.ElShaddollConstruct,
                    CardId.ElShaddollApkallone,
                    CardId.ReeshaddollWendikuruhu,
                    CardId.ElShaddollWinda,
                    CardId.ElShaddollShekhinaga,
                    CardId.ElShaddollAnoyatyllis,
                    CardId.InfernoidEvil,
                    CardId.TearlamentsScheiren));
        }

        private bool CanUseSpiritOrderedTrigger(ClientCard card)
        {
            return _preferredSpiritProtectedTrigger == null ||
                card == _preferredSpiritProtectedTrigger;
        }

        private int SpiritProtectedTriggerScore(ClientCard card)
        {
            // Simultaneous grave triggers are placed from the most important
            // effect first. The final, lower-value link is the only activation
            // Blue-Eyes Spirit Dragon can chain to and negate.
            if (card.IsCode(CardId.TohushaddollGrysta) && HasFusionResources(true)) return 1000;
            if (card.IsCode(CardId.ShaddollHedgehog) &&
                !Bot.HasInHand(CardId.ReeshaddollWendi)) return 940;
            if (card.IsCode(CardId.ElShaddollApkallone)) return 920;
            if (card.IsCode(CardId.InfernoidEvil) &&
                Bot.HasInDeck(CardId.VoidUnleashing)) return 900;
            if (card.IsCode(CardId.ReeshaddollWendi) && Bot.GetMonsterCount() < 5) return 880;
            if (card.IsCode(CardId.ShaddollSquamata) && HasShaddollSendTarget()) return 840;
            if (card.IsCode(CardId.ElShaddollConstruct) && HasRecoverableSpellTrap()) return 810;
            if (card.IsCode(CardId.ReeshaddollWendikuruhu)) return 780;
            if (card.IsCode(CardId.NaelshaddollAriel)) return 740;
            if (card.IsCode(CardId.ShaddollDragon)) return 700;
            if (card.IsCode(CardId.TearlamentsScheiren)) return 680;
            if (card.IsCode(CardId.ElShaddollWinda,
                CardId.ElShaddollShekhinaga, CardId.ElShaddollAnoyatyllis)) return 650;
            if (card.IsCode(CardId.ShaddollBeast)) return 500;
            return 600;
        }

        private bool ShouldEstablishWindaBeforeFirstSummon()
        {
            if (Duel.Player != 1 ||
                _opponentSpecialSummonsThisTurn != 0 ||
                FunctionalWinda() ||
                !Bot.HasInExtra(CardId.ElShaddollWinda) ||
                !Bot.HasInSpellZone(CardId.ShaddollSchism) ||
                !HasSchismWindaResources())
                return false;
            // Winda only counts Special Summons made while this copy is face-up.
            // Waiting for the first Blue-Eyes body still permits the Synchro/Xyz
            // summon that the lock was meant to prevent. Its own destruction
            // protection makes the pre-summon lock worthwhile even when the
            // full Wendikuruhu + Flip protection shell was interrupted.
            return !HasUrgentPersistentSpellTrapEngine();
        }

        private bool ShouldUseSchismMaterialRemovalWindow()
        {
            if (Duel.Player != 1 || _opponentSpecialSummonsThisTurn != 1 ||
                FunctionalWinda() || !Bot.HasInSpellZone(CardId.ShaddollSchism) ||
                !HasSchismFusionResources() || HasUrgentPersistentSpellTrapEngine())
                return false;
            return Enemy.GetMonsters().Any(target => CanCreditSchismRemoval(target) &&
                Bot.ExtraDeck.Any(fusion => ShaddollFusionIds.Contains(fusion.Id) &&
                    fusion.Attribute != 0 && (target.Attribute & fusion.Attribute) != 0));
        }

        private bool ShouldLeadWithElFusion()
        {
            if (Duel.Player != 1 || FunctionalWinda() ||
                ShouldEstablishWindaBeforeFirstSummon() ||
                ShouldUseSchismMaterialRemovalWindow() ||
                Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                    c.IsCode(CardId.ElShaddollApkallone)))
                return false;
            bool readyElFusion = Bot.GetSpells().Any(c =>
                c.IsCode(CardId.ElShaddollFusion) && c.IsFacedown());
            return readyElFusion && CanActivateElShaddollFusionNow() &&
                Bot.HasInExtra(CardId.ElShaddollApkallone) &&
                BestPersistentOpponentThreatScore() >= 750;
        }

        private int RevivalScore(ClientCard card)
        {
            if (card.IsCode(CardId.ReeshaddollWendikuruhu) && !LiveMonster(CardId.ReeshaddollWendikuruhu)) return 400;
            if (card.IsCode(CardId.TohushaddollGrysta) && !HasGrystaBody() && HasCopyFuel()) return 330;
            if (card.IsCode(CardId.NaelshaddollAriel) && HasArielRecovery()) return 255;
            if (card.IsCode(CardId.ElShaddollWinda) && !FunctionalWinda()) return 225;
            if (card.IsCode(CardId.ElShaddollMeshachrer)) return 205;
            if (card.IsCode(CardId.ElShaddollConstruct)) return 190;
            if (card.IsCode(CardId.ElShaddollApkallone)) return 170;
            if (_voidDiscardedMonsterId != 0 && card.IsCode(_voidDiscardedMonsterId) && IsShaddollMonster(card)) return 160;
            return card.HasType(CardType.Flip) ? 110 + Math.Max(0, CopyEffectValue(card.Id))/3 : 10;
        }

        private int ArielRevivalScore(ClientCard card)
        {
            if (card.IsCode(CardId.TohushaddollGrysta) && !HasGrystaBody() && HasCopyFuel()) return 450;
            if (card.IsCode(CardId.ShaddollSquamata) && LiveMonster(CardId.ReeshaddollWendikuruhu)) return 380;
            if (card.IsCode(CardId.ShaddollSquamata) && Enemy.GetMonsterCount()>0) return 310;
            if (card.IsCode(CardId.ReeshaddollWendi) && Bot.GetMonsterCount()<4) return 290;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetFieldCount()>0) return 300;
            return RevivalScore(card);
        }

        private int ShaddollRecoveryScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSchism)) return IsLiveSchism() || Bot.HasInHand(card.Id) ? 25 : 145;
            if (card.IsCode(CardId.ElShaddollFusion)) return Bot.HasInHandOrInSpellZone(card.Id) ? 45 : 120;
            if (card.IsCode(CardId.ShaddollFusion)) return Bot.HasInHand(card.Id) ? 35 : EnemyDeckFusionWindow() ? 150 : 110;
            if (card.IsCode(CardId.LevShaddollFusion)) return LiveLev() || Bot.HasInHand(CardId.LevShaddollFusion) ? 5 : 105;
            return ShaddollMonsterSearchScore(card);
        }

        private int SpellTrapRecoveryScore(ClientCard card)
        {
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ReeshaddollWendikuruhu) &&
                Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism) &&
                card.IsCode(CardId.ElShaddollFusion)) return 250;
            if (card.IsCode(CardId.ShaddollSchism)) return !IsLiveSchism() && !Bot.HasInHand(card.Id) ? 145 : 25;
            if (card.IsCode(CardId.ReshShaddollIncarnation)) return Bot.HasInHandOrInSpellZone(card.Id) ? 45 : 118;
            if (card.IsCode(CardId.ElShaddollFusion)) return Bot.HasInHandOrInSpellZone(card.Id) ? 45 : 115;
            if (card.IsCode(CardId.ShaddollFusion)) return Bot.HasInHand(card.Id) ? 35 : EnemyDeckFusionWindow() ? 150 : 110;
            if (card.IsCode(CardId.LevShaddollFusion)) return LiveLev() || Bot.HasInHand(CardId.LevShaddollFusion) ? 5 : 105;
            return 20;
        }

        private bool MarkUsed(int effectId)
        {
            if (_usedEffects.Contains(effectId))
                return false;
            _usedEffects.Add(effectId);
            return true;
        }

        private bool EffectUsed(int effectId)
        {
            return _usedEffects.Contains(effectId);
        }

        private bool CurrentOpponentChainIsCovered()
        {
            ChainInfo current = Duel.CurrentChainInfo.LastOrDefault(c=>c.ActivatePlayer==1);
            return current != null && object.ReferenceEquals(current, _coveredOpponentLink);
        }

        private void CoverCurrentOpponentChain()
        {
            _coveredOpponentLink = Duel.CurrentChainInfo.LastOrDefault(c=>c.ActivatePlayer==1);
        }

        private bool ShouldStopSpecialSummoning(CardLocation source)
        {
            bool maxx = enemyResolvedEffectIdList.Contains(CardId.MaxxC);
            bool purulia = enemyResolvedEffectIdList.Contains(CardId.MulcharmyPurulia) &&
                (source & CardLocation.Hand) != 0;
            bool fuwalos = enemyResolvedEffectIdList.Contains(CardId.MulcharmyFuwalos) &&
                (source & (CardLocation.Deck | CardLocation.Extra)) != 0;
            if (!maxx && !purulia && !fuwalos)
                return false;
            return FunctionalBoard() || _specialSummonsThisTurn >= 1;
        }

        private bool FunctionalBoard()
        {
            return FunctionalWinda() || (LiveMonster(CardId.ElShaddollShekhinaga) && Bot.Hand.Any(IsShaddollCard)) ||
                (LiveMonster(CardId.ReeshaddollWendikuruhu) && Bot.GetMonsters().Any(c=>c.IsFacedown() && c.HasType(CardType.Flip))) ||
                (IsLiveSchism() && HasSchismFusionResources());
        }

        private int ControlBoardScore()
        {
            int score = 0;
            if (FunctionalWinda()) score += 110;
            if (IsLiveSchism() && HasSchismFusionResources()) score += 65;
            if (Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                c.IsCode(CardId.ElShaddollShekhinaga)) && Bot.Hand.Any(IsShaddollCard)) score += 55;
            if (Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                c.IsCode(CardId.ReeshaddollWendikuruhu)) && Bot.GetMonsters().Any(c=>c.IsFacedown() && c.HasType(CardType.Flip))) score += 55;
            score += Math.Min(40, Bot.GetMonsters().Count(IsUsefulFacedownFlip) * 20);
            if (HasGrystaBody() && HasCopyFuel()) score += 40;
            if (FlipEngineLive() && HasGrystaBody() && HasCopyFuel()) score += 30;
            return score;
        }

        private bool HasSafeLethalOnBoard()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 || Enemy.LifePoints <= 0 ||
                Enemy.GetMonsterCount() > 0 || Enemy.GetSpellCount() > 0)
                return false;
            int availableAttack = Bot.GetMonsters()
                .Where(c => c.IsFaceup() && c.IsAttack() && !_schismBodies.Contains(c))
                .Sum(c => Math.Max(0, c.Attack));
            return availableAttack >= Enemy.LifePoints;
        }

        private bool ShouldPreserveMonster(ClientCard card)
        {
            if (card == null || card.Location != CardLocation.MonsterZone || !card.IsFaceup() || card.IsDisabled())
                return false;
            if (card.IsCode(CardId.ElShaddollWinda)) return true;
            if (card.IsCode(CardId.ReeshaddollWendikuruhu) && FlipEngineLive()) return true;
            if (card.IsCode(CardId.TohushaddollGrysta) && HasCopyFuel()) return true;
            return card.IsCode(CardId.ElShaddollShekhinaga) && Bot.Hand.Any(IsShaddollCard);
        }

        private bool IsUsefulFacedownFlip(ClientCard card)
        {
            return card != null && card.Controller == 0 && card.IsFacedown() &&
                card.HasType(CardType.Flip) && FlipUpScore(card) >= 90;
        }

        private bool FunctionalWinda()
        {
            return Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                c.IsCode(CardId.ElShaddollWinda));
        }

        private bool IsLevRouteActive()
        {
            return EffectUsed(CardId.LevShaddollFusion) ||
                Bot.HasInSpellZone(CardId.LevShaddollFusion);
        }

        private bool MustResolveLevMeshFirst()
        {
            return Duel.Player == 0 && LiveLev() && LiveMonster(CardId.ElShaddollMeshachrer) &&
                !EffectUsed(CardId.LevShaddollFusion + 1) && CurrentPlan().Goal == RouteGoal.MeshRecovery;
        }

        private bool MustWaitForLevWendiSearch()
        {
            return Duel.Player == 0 && IsLevRouteActive() &&
                _waitingForLevWendi &&
                !Bot.HasInHand(CardId.ReeshaddollWendi);
        }

        private void AdvanceLevRoute(LevRouteStage stage)
        {
            if (stage > _levRouteStage)
                _levRouteStage = stage;
        }

        private bool CanStartLevPrimaryRoute()
        {
            return Bot.HasInExtra(CardId.ElShaddollApkallone) &&
                (CanContinueLevThroughSquamata() || CanContinueLevThroughGrysta());
        }

        private bool CanContinueLevThroughSquamata()
        {
            return !EffectUsed(CardId.ShaddollSquamata) &&
                !EffectUsed(CardId.TohushaddollGrysta) && !FunctionalWinda() &&
                (Bot.HasInDeck(CardId.ShaddollSquamata) || Bot.HasInHand(CardId.ShaddollSquamata)) &&
                Bot.HasInDeck(CardId.TohushaddollGrysta) &&
                Bot.HasInExtra(CardId.ElShaddollMeshachrer);
        }

        private bool CanContinueLevThroughGrysta()
        {
            return !EffectUsed(CardId.TohushaddollGrysta) && !FunctionalWinda() &&
                Bot.HasInExtra(CardId.ElShaddollMeshachrer) &&
                (Bot.HasInDeck(CardId.TohushaddollGrysta) || Bot.HasInHand(CardId.TohushaddollGrysta)) &&
                (Bot.Graveyard.Concat(Bot.Hand).Concat(Bot.GetMonsters()).Any(c=>c.IsCode(CardId.ShaddollSquamata)) ||
                 Bot.Graveyard.Concat(Bot.Hand).Concat(Bot.GetMonsters()).Any(c=>c.IsMonster() && c.HasAttribute(CardAttribute.Dark) && !c.IsCode(CardId.ElShaddollApkallone)));
        }

        private bool HasVoidRevealInHand()
        {
            return VoidCanSearchNow();
        }

        private bool HasPreparedLevControlBoard()
        {
            return Bot.GetMonsters().Any(c => c.IsCode(CardId.ReeshaddollWendikuruhu)) &&
                Bot.GetMonsters().Any(c => c.IsCode(CardId.NaelshaddollAriel)) &&
                Bot.GetMonsters().Any(c => c.IsCode(CardId.ShaddollSquamata)) &&
                Bot.GetSpells().Any(c => c.IsCode(CardId.ShaddollSchism));
        }

        private bool CanCommitArmageddonMesh()
        {
            return Duel.Player == 0 && _armageddonRouteStarted && !_armageddonMeshSummoned &&
                !IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ArmageddonKnight) &&
                Bot.Graveyard.Any(c => c.IsCode(CardId.ShaddollSquamata)) &&
                Bot.Graveyard.Any(c => c.IsCode(CardId.TohushaddollGrysta));
        }

        private bool IsArmageddonLevRoute()
        {
            // Legacy name: now recognizes Mesh from any legal summoning route.
            return Duel.Player == 0 && LiveMonster(CardId.ElShaddollMeshachrer) &&
                (CurrentPlan().Goal == RouteGoal.MeshRecovery ||
                 (_armageddonMeshSummoned && (Bot.HasInHand(CardId.VoidUnleashing) ||
                  (Bot.HasInExtra(CardId.InfernoidEvil) && Bot.HasInDeck(CardId.VoidUnleashing)))));
        }

        private bool HasLevControlExtension()
        {
            return Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion) &&
                Bot.HasInExtra(CardId.ReeshaddollWendikuruhu) && !EffectUsed(CardId.ReeshaddollWendi) &&
                (Bot.HasInHand(CardId.ReeshaddollWendi) ||
                 (!EffectUsed(CardId.ShaddollHedgehog) && Bot.HasInDeck(CardId.ShaddollHedgehog) && Bot.HasInDeck(CardId.ReeshaddollWendi))) &&
                (LiveLev() || LiveMonster(CardId.ElShaddollConstruct));
        }

        private bool CanReachLevControlExtension()
        {
            return HasLevControlExtension() || CurrentPlan().Goal == RouteGoal.MeshRecovery ||
                CurrentPlan().Goal == RouteGoal.ConstructControl ||
                (LiveLev() && Bot.HasInHand(CardId.VoidUnleashing) && !EffectUsed(CardId.VoidUnleashing) &&
                 !EffectUsed(CardId.ReeshaddollWendi) && Bot.HasInExtra(CardId.ReeshaddollWendikuruhu) &&
                 (Bot.HasInHand(CardId.ReeshaddollWendi) || (!EffectUsed(CardId.ShaddollHedgehog) &&
                  Bot.HasInDeck(CardId.ShaddollHedgehog) && Bot.HasInDeck(CardId.ReeshaddollWendi))));
        }

        private bool HasImmediateFusionStarter()
        {
            return Bot.HasInHandOrInSpellZone(CardId.ShaddollFusion) ||
                Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion) ||
                Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion) ||
                (Bot.HasInHandOrInSpellZone(CardId.VoidUnleashing) && VoidCanSearchNow());
        }

        private bool HasFusionResources(bool includeGrave)
        {
            int materialCount = Bot.Hand.Count(c => c.IsMonster()) + Bot.GetMonsterCount();
            if (includeGrave)
                materialCount += Bot.Graveyard.Count(c => c.IsMonster());
            return materialCount >= 2 && Bot.ExtraDeck.Any(c => ShaddollFusionIds.Contains(c.Id));
        }

        private bool CanFaimenaSummon(int fusionId)
        {
            List<ClientCard> materials = Bot.Hand.Concat(Bot.GetMonsters())
                .Where(c => c != null && c != Card && c.IsMonster())
                .ToList();
            return CanMakeTwoMaterialShaddoll(fusionId, materials);
        }

        private bool CanMakeTwoMaterialShaddoll(int fusionId, IList<ClientCard> materials)
        {
            if (!Bot.HasInExtra(fusionId)) return false;
            for(int i=0;i<materials.Count;i++) for(int j=i+1;j<materials.Count;j++)
                if (LegalMaterialGroup(fusionId,new List<ClientCard>{materials[i],materials[j]})) return true;
            return false;
        }

        private bool HasSchismFusionResources()
        {
            List<ClientCard> pool = Bot.Graveyard.Concat(Bot.GetMonsters().Where(c=>!c.HasType(CardType.Fusion)))
                .Where(c=>c!=null && c.IsMonster()).Distinct().ToList();
            foreach (int id in ShaddollFusionIds.Where(Bot.HasInExtra))
                for (int i=0;i<pool.Count;i++) for(int j=i+1;j<pool.Count;j++)
                {
                    if (id != CardId.ElShaddollMeshachrer && LegalMaterialGroup(id,new List<ClientCard>{pool[i],pool[j]})) return true;
                    if (id == CardId.ElShaddollMeshachrer)
                        for(int k=j+1;k<pool.Count;k++)
                            if (LegalMaterialGroup(id,new List<ClientCard>{pool[i],pool[j],pool[k]})) return true;
                }
            return false;
        }

        private bool HasSchismWindaResources()
        {
            List<ClientCard> materials = Bot.GetMonsters()
                .Concat(Bot.Graveyard)
                .Where(SafeSchismMaterial)
                .ToList();
            for (int i = 0; i < materials.Count; ++i)
            {
                for (int j = i + 1; j < materials.Count; ++j)
                {
                    ClientCard first = materials[i];
                    ClientCard second = materials[j];
                    if ((IsShaddollMonster(first) && second.HasAttribute(CardAttribute.Dark)) ||
                        (IsShaddollMonster(second) && first.HasAttribute(CardAttribute.Dark)))
                        return true;
                }
            }
            return false;
        }

        private bool HasShaddollSendTarget()
        {
            return ShaddollMainMonsterIds.Any(id => id != CardId.ShaddollSquamata && Bot.HasInDeck(id)) ||
                ShaddollSpellTrapIds.Any(Bot.HasInDeck);
        }

        private bool HasRecoverableSpellTrap()
        {
            return Bot.Graveyard.Any(c => (c.IsSpell() || c.IsTrap()) && IsShaddollCard(c));
        }

        private bool HasRecoverableShaddollCard()
        {
            return Bot.Graveyard.Any(IsShaddollCard) || ShaddollMainMonsterIds.Any(Bot.HasInDeck) ||
                ShaddollSpellTrapIds.Any(Bot.HasInDeck);
        }

        private bool HasUsefulMeshSearch()
        {
            return (VoidCanSearchNow() && Bot.HasInDeck(CardId.VoidUnleashing)) || ShaddollSpellTrapIds.Any(Bot.HasInDeck) ||
                ShaddollMainMonsterIds.Any(Bot.HasInDeck);
        }

        private bool IsMainPhase()
        {
            return Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2;
        }

        private bool IsShaddollCard(ClientCard card)
        {
            return card != null && card.HasSetcode(ShaddollSetcode);
        }

        private bool IsShaddollMonster(ClientCard card)
        {
            return IsShaddollCard(card) && card.IsMonster();
        }

        private bool IsExtraDeckMonster(ClientCard card)
        {
            return card != null && card.IsMonster() && card.HasType(
                CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link);
        }

        private CardType GetBaseType(ClientCard card)
        {
            if (card.IsMonster()) return CardType.Monster;
            if (card.IsSpell()) return CardType.Spell;
            if (card.IsTrap()) return CardType.Trap;
            return 0;
        }

        private int GetPromptOwnerId()
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.ActivateId;
            ClientCard chainCard = Duel.GetCurrentChainCard();
            return chainCard == null ? 0 : chainCard.Id;
        }

        private bool PromptIs(int cardId)
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.IsActivateCode(cardId);
            ClientCard chainCard = Duel.GetCurrentChainCard();
            return chainCard != null && chainCard.IsCode(cardId);
        }

        private bool IsFusionPromptOwner()
        {
            return PromptIs(CardId.ShaddollFusion) ||
                PromptIs(CardId.ElShaddollFusion) ||
                PromptIs(CardId.ShaddollSchism) ||
                PromptIs(CardId.TohushaddollGrysta) ||
                PromptIs(CardId.BlazingCartesia) ||
                PromptIs(CardId.DracotailFaimena) ||
                PromptIs(CardId.TearlamentsScheiren);
        }

        private bool IsFusionRecoveryPrompt()
        {
            return PromptIs(CardId.ElShaddollConstruct) ||
                PromptIs(CardId.ElShaddollWinda) ||
                PromptIs(CardId.ElShaddollShekhinaga) ||
                PromptIs(CardId.ElShaddollAnoyatyllis);
        }
    }
}
