using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    [Deck("Witchcraft2026", "AI_Witchcraft2026")]
    public class Witchcraft2026Executor : DefaultExecutor
    {
        public class CardId
        {
            public const int MadameVerre = 21522601;
            public const int Haine = 84523092;
            public const int Marka = 6071005;
            public const int Edel = 58139997;
            public const int Schmietta = 21744288;
            public const int Seed = 59106048;
            public const int MaidVerre = 22623509;
            public const int Pittore = 95245544;
            public const int AshBlossom = 14558127;
            public const int MaxxC = 23434538;
            public const int Potterie = 59851535;
            public const int Terracottan = 70686400;
            public const int MaidGenni = 7656689;
            public const int Genni = 64756282;
            public const int RainbowBridgeBifrost = 32236916;
            public const int Terraforming = 73628505;
            public const int Walpurgis = 32353566;
            public const int Creation = 57916305;
            public const int Lacrima = 73664385;
            public const int Unveiling = 70226289;
            public const int Bystreet = 83289866;
            public const int SpellPowerMastery = 38943357;
            public const int GazeOfTimaeus = 22283204;
            public const int ConfusionConfession = 35098357;
            public const int Celebration = 6958567;
            public const int Distortion = 69748261;
            public const int Draping = 56894757;

            public const int MadameRillie = 33475154;
            public const int RedEyesDarkDragoon = 37818794;
            public const int DarkMagicianOfDestruction = 59400890;
            public const int ViceMadame = 9603252;
            public const int Pupils = 69964858;
            public const int CosmicQuasarDragon = 37442336;
            public const int DespianLuluwalilith = 53971455;
            public const int ElfnoteJunePride = 5559570;
            public const int CharmerQuartet = 27519978;
            public const int Selene = 45819647;
            public const int SpLittleKnight = 29301451;
            public const int IpMasquerena = 65741789;
            public const int CrossSheep = 50277355;
        }

        private const int WitchcrafterSetcode = 0x128;
        private bool _spellActivity, _normalUsed, _extraLock, _maxx;
        private bool _awaitingChain;
        private readonly HashSet<string> _used = new HashSet<string>();
        private readonly OpponentAwareness _opponent = new OpponentAwareness();
        private readonly Dictionary<int, WAction> _prompts = new Dictionary<int, WAction>();
        private IList<ClientCard> _chainOffers;
        private MainPhase _main;
        private WPlan _plan;
        private WAction _accepted;
        private string _planKey;
        private readonly System.Threading.CancellationTokenSource _shutdown =
            new System.Threading.CancellationTokenSource();

        public Witchcraft2026Executor(GameAI ai, Duel duel) : base(ai, duel)
        {
            if (AI.Game != null && AI.Game.Connection != null)
                AI.Game.Connection.Disconnected += error => _shutdown.Cancel();
            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, GraveWindowAction);
            AddExecutor(ExecutorType.Activate, CardId.Distortion, DistortionEffect);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesDarkDragoon, DragoonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Haine, HaineEffect);
            AddExecutor(ExecutorType.Activate, CardId.MadameVerre, VerreEffect);
            AddExecutor(ExecutorType.Activate, CardId.ViceMadame, ViceResponse);
            AddExecutor(ExecutorType.Activate, CardId.Lacrima, QuickResourceAction);
            AddExecutor(ExecutorType.Activate, CardId.MaidGenni, QuickResourceAction);
            foreach (int id in LowIds)
                AddExecutor(ExecutorType.Activate, id, QuickResourceAction);
            AddExecutor(ExecutorType.Activate, CardId.Seed, SeedResponse);
            AddExecutor(ExecutorType.GoToBattlePhase, TryBattleForLethal);
            // All planned effects share one comparison, independent of registration order.
            foreach (int id in KnownIds)
                AddExecutor(ExecutorType.Activate, id, PlannedAction);
            foreach (int id in NormalIds)
                AddExecutor(ExecutorType.Summon, id, PlannedAction);
            AddExecutor(ExecutorType.SpSummon, CardId.DarkMagicianOfDestruction, PlannedAction);
            AddExecutor(ExecutorType.SpellSet, CardId.Distortion, PlannedAction);
            AddExecutor(ExecutorType.Activate, EndPhaseRecovery);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        public override bool OnSelectHand() { return true; }

        private void Invalidate()
        {
            _plan = null;
            _planKey = null;
        }

        public override void OnNewTurn()
        {
            _opponent.NewTurn();
            _freshBystreetShields.Clear();
            _bystreetSeenThisTurn = Bot.GetSpells().Any(c => c.IsFaceup() && c.Id == CardId.Bystreet);
            _spellActivity = _normalUsed = _extraLock = _maxx = false;
            _unveilingProtection = false;
            _used.Clear();
            _prompts.Clear();
            _seedPicked.Clear();
            _accepted = null;
            _awaitingChain = false;
            _chainOffers = null;
            _viceRemoval = null;
            _lastTacticalDiagnostic = null;
            _lastOpponentDiagnostic = null;
            _limitCounts.Clear();
            _summonLimitSources.Clear();
            _predictions.Clear();
            _graveFusionSpent.Clear();
            Invalidate();
            base.OnNewTurn();
        }

        public override void OnSummoning()
        {
            if (Duel.LastSummonPlayer == 0) _normalUsed = true;
            Invalidate();
            base.OnSummoning();
        }

        public override void OnSpSummoned()
        {
            SummonOutcomes();
            Invalidate();
            base.OnSpSummoned();
        }

        public override void OnSelectChain(IList<ClientCard> cards)
        {
            _main = Duel.MainPhase;
            _chainOffers = cards;
            Invalidate();
            base.OnSelectChain(cards);
        }

        public override void OnChaining(int player, ClientCard card)
        {
            // Any intervening effect can consume an unreported destruction shield.
            // Retire certainty; never infer an unused counter from the card surviving.
            _freshBystreetShields.Clear();
            _opponent.Observe(Duel.CurrentChainInfo); // current link is appended after this hook
            if (player == 1 && card != null && card.Id == GraveFusionTrigger && card.Location == CardLocation.Grave)
                _graveFusionSpent.Add(card.Id);
            if (card != null && card.IsSpell()) _spellActivity = true;
            if (player == 0 && _awaitingChain && _accepted != null && card != null &&
                card.Id == _accepted.Source)
            {
                // GameBehavior appends ChainInfo immediately after this callback.
                _prompts[Duel.CurrentChain.Count + 1] = _accepted;
                _awaitingChain = false;
            }
            Invalidate();
            base.OnChaining(player, card);
        }

        public override void OnChainSolved(int chainIndex)
        {
            _opponent.Solved(Duel.CurrentChainInfo, chainIndex, Duel.NegatedChainIndexList.Contains(chainIndex));
            ChainInfo info = Duel.GetCurrentSolvingChainInfo();
            if (info != null && !Duel.IsCurrentSolvingChainNegated())
            {
                ObserveBystreetResolution(info);
                ObserveGraveResolution(info);
                if (info.ActivatePlayer == 1 && info.ActivateId == CardId.MaxxC)
                    _maxx = true;
                if (info.ActivatePlayer == 0 && info.ActivateId == CardId.Terracottan &&
                    info.ActivateLocation == CardLocation.Hand)
                    _extraLock = true;
            }
            Invalidate();
            base.OnChainSolved(chainIndex);
        }

        public override void OnChainEnd()
        {
            _seedPicked.Clear();
            _opponent.Observe(Duel.CurrentChainInfo);
            ReconcilePredictions();
            _opponent.EndChain();
            _viceRemoval = null;
            _lastTacticalDiagnostic = null;
            _prompts.Clear();
            _accepted = null;
            _awaitingChain = false;
            _chainOffers = null;
            _summonLimitSources.Clear();
            Invalidate();
            base.OnChainEnd();
        }

        public override void OnMove(ClientCard card, int previousController, int previousLocation,
            int currentController, int currentLocation)
        {
            ObserveGraveMove(card, previousLocation, currentController, currentLocation);
            _opponent.Observe(Duel.CurrentChainInfo);
            if (previousLocation != currentLocation)
            {
                _opponent.ResetInstance(card);
                _freshBystreetShields.Remove(card);
            }
            if (previousLocation == (int)CardLocation.MonsterZone &&
                currentLocation != (int)CardLocation.MonsterZone)
            {
                _limitCounts.Remove(card);
                _summonLimitSources.Remove(card);
                _expires.Remove(card);
            }
            Invalidate();
            base.OnMove(card, previousController, previousLocation, currentController, currentLocation);
        }

        private static WZone Zone(CardLocation location)
        {
            switch (location)
            {
                case CardLocation.Hand: return WZone.Hand;
                case CardLocation.MonsterZone: return WZone.Board;
                case CardLocation.Grave: return WZone.Grave;
                case CardLocation.Removed: return WZone.Banished;
                case CardLocation.Extra: return WZone.Extra;
                case CardLocation.SpellZone: return WZone.Backrow;
                default: return WZone.Deck;
            }
        }

        private WState Observe()
        {
            _opponent.Observe(Duel.CurrentChainInfo);
            WState s = new WState
            {
                Normal = !_normalUsed, SpellActivity = _spellActivity,
                ExtraLock = _extraLock, Maxx = _maxx,
                OpponentTurn = Duel.Player == 1, EnemyChain = Duel.LastChainPlayer == 1,
                IncomingAttack = Enemy.GetMonsters().Where(c => c.IsFaceup()).Sum(c => Math.Max(0, c.Attack)),
                LifePoints = Bot.LifePoints,
                Used = new HashSet<string>(_used)
            };
            foreach (int id in KnownIds)
            {
                int count = Bot.GetCardCountInDeck(id);
                if (count > 0) s.Add(id, WZone.Deck, count);
            }
            foreach (ClientCard card in Bot.Hand.Concat(Bot.GetMonsters()).Concat(Bot.Graveyard)
                .Concat(Bot.Banished).Concat(Bot.ExtraDeck).Concat(Bot.GetSpells()))
            {
                if (card == null || !KnownIds.Contains(card.Id)) continue;
                if ((card.Location == CardLocation.MonsterZone ||
                    card.Location == CardLocation.Removed) && card.IsFacedown()) continue;
                s.Add(card.Id, Zone(card.Location));
                if ((card.Location == CardLocation.MonsterZone || card.Location == CardLocation.SpellZone)
                    && card.IsDisabled()) s.Disabled.Add(card.Id);
            }
            s.UnknownHand = Bot.Hand.Count - s.Size(WZone.Hand);
            s.OtherMonsters = Bot.GetMonsters().Count - s.Size(WZone.Board);
            ClientCard extraZoneCard = Bot.GetMonsters().FirstOrDefault(c => c.Sequence >= 5);
            s.ExtraOccupant = extraZoneCard == null ? 0 : extraZoneCard.Id;
            ObserveOutcomes(s);
            ObserveCoverage(s);
            ObserveGraveState(s);
            // A multi-trigger window may offer several monsters. Pending belongs to
            // this candidate, not whichever unrelated offer happens to be listed last.
            if ((AI.SelectingEffectYn || _chainOffers != null) && Card != null)
            {
                if (Card.Controller == 0 && Card.Location == CardLocation.MonsterZone)
                {
                    if (Card.IsCode(CardId.DarkMagicianOfDestruction, CardId.MadameRillie, CardId.MaidVerre))
                        s.Pending = Card.Id;
                    if (Card.Id == CardId.ViceMadame) s.ViceWindow = true;
                }
            }
            return s;
        }

        private static ExecutorType ActionType(WAction action)
        {
            if (action.Kind == WKind.Normal) return ExecutorType.Summon;
            if (action.Kind == WKind.DarkSummon) return ExecutorType.SpSummon;
            if (action.Kind == WKind.SetTrap) return ExecutorType.SpellSet;
            return ExecutorType.Activate;
        }

        private static bool Matches(WAction action, ClientCard card, int description)
        {
            // MSG_SELECT_EFFECTYN uses the generic 221 prompt (normalized to -1)
            // for optional summon triggers; it does not always carry Stringid.
            bool genericTrigger = description <= 0 && (action.Kind == WKind.GazeSearch ||
                action.Kind == WKind.RillieRecruit || action.Kind == WKind.MaidSearch);
            return card != null && card.Controller == 0 && card.Id == action.Source &&
                Zone(card.Location) == action.Location && (action.Description < 0 || genericTrigger ||
                description == UtilString(action.Source, action.Description));
        }

        private static int UtilString(int id, int offset) { return id * 16 + offset; }

        private bool IsOffered(WAction action)
        {
            if (AI.SelectingEffectYn)
                return ActionType(action) == Type && Matches(action, Card, ActivateDescription);
            if (_chainOffers != null)
            {
                if (ActionType(action) != ExecutorType.Activate) return false;
                // Passing a fast-effect window preserves the next idle action. Do not
                // spend Celebration just because Terra's ignition effect is not offered
                // in this window. Only expiring trigger/recovery opportunities run here.
                if (action.Kind != WKind.GazeSearch && action.Kind != WKind.RillieRecruit &&
                    action.Kind != WKind.MaidSearch && action.Kind != WKind.ViceRecover && action.Kind != WKind.DistortionSearch) return false;
                // Descriptions are available for the currently evaluated offer only.
                // Never choose a different effect of this same card in a chain window.
                return action.Source == Card.Id && Matches(action, Card, ActivateDescription);
            }
            MainPhase main = Duel.MainPhase;
            if (main == null) return false;
            IList<ClientCard> cards = ActionType(action) == ExecutorType.Summon ? main.SummonableCards :
                ActionType(action) == ExecutorType.SpSummon ? main.SpecialSummonableCards :
                ActionType(action) == ExecutorType.SpellSet ? main.SpellSetableCards : main.ActivableCards;
            for (int i = 0; i < cards.Count; i++)
                if (Matches(action, cards[i], ActionType(action) == ExecutorType.Activate ?
                    main.ActivableDescs[i] : -1)) return true;
            return false;
        }

        private bool PlannedAction()
        {
            if (!ReferenceEquals(_main, Duel.MainPhase))
            {
                _main = Duel.MainPhase;
                _chainOffers = null;
                Invalidate();
            }
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                return false;
            if (Duel.CurrentChain.Count == 0 && !_awaitingChain) ReconcilePredictions();
            WState observed = Observe();
            string key = observed.Key() + (AI.SelectingEffectYn || _chainOffers != null ?
                "|" + Card.Id + ":" + ActivateDescription : "");
            if (_plan == null || key != _planKey)
            {
                _plan = WPlanner.Plan(observed, IsOffered, _shutdown.Token);
                _planKey = key;
                Logger.DebugWriteLine("Witchcraft2026 compare: hold=" + WPlanner.Evaluate(observed).ToString("0.0") +
                    " best=" + _plan.Score.ToString("0.0") + " nodes=" + _plan.Expanded +
                    " bounded=" + _plan.TimedOut + " alternatives=" + string.Join(" | ", _plan.Alternatives));
                if (Duel.Player == 0)
                    Logger.DebugWriteLine("Witchcraft2026 Action Coverage Audit: " + ActionCoverageAudit(observed, _plan));
            }
            if (_plan.Steps.Count == 0) return false;
            WAction action = _plan.Steps[0].Action;
            if (ActionType(action) != Type || !Matches(action, Card, ActivateDescription)) return false;
            AcceptAction(action);
            Logger.DebugWriteLine("Witchcraft2026 action: " + action + " route=" + action.Route +
                " forecast=" + string.Join(" > ", _plan.Steps.Select(step => step.Action.Kind.ToString())));
            Invalidate();
            return true;
        }

        private void AcceptAction(WAction action)
        {
            if (action.Kind != WKind.Tactical)
            {
                WState before = Observe();
                _predictions.Add(new WPrediction { Action = action, Before = before, After = WPlanner.Apply(before, action) });
            }
            _accepted = action;
            _awaitingChain = Type == ExecutorType.Activate;
            _used.Add(action.Usage);
            MarkSharedUsage(_used, action);
            if (action.Source == CardId.Pupils && (action.Kind == WKind.Fusion || action.Kind == WKind.PupilsSearch))
            {
                _used.Add(CardId.Pupils + ":" + WKind.Fusion);
                _used.Add(CardId.Pupils + ":" + WKind.PupilsSearch);
            }
            if (action.Kind == WKind.GenniCopy || action.Kind == WKind.GenniLacrima)
            {
                _used.Add(CardId.Genni + ":" + WKind.GenniCopy);
                _used.Add(CardId.Genni + ":" + WKind.GenniLacrima);
            }
        }

        private bool DistortionEffect()
        {
            return Card.Location == CardLocation.SpellZone && ValuableNegate("Distortion", 5) &&
                Bot.GetMonsters().Any(c => c.IsFaceup() && c.HasSetcode(WitchcrafterSetcode) && c.Level >= 5);
        }

        private bool DragoonEffect()
        {
            if (ActivateDescription == UtilString(CardId.RedEyesDarkDragoon, 1) &&
                Bot.Hand.Count > 0 && ValuableNegate("Dragoon", 10))
            {
                AI.SelectCard(DiscardOrder(Bot.Hand));
                return true;
            }
            return false;
        }

        private bool HaineEffect()
        {
            ClientCard target = RemovalTarget(true);
            if (target == null || !Bot.Hand.Any(c => c.IsSpell())) return false;
            if (!TacticalDecision("Haine", RemovalValue(target, true), InterruptionCost(6),
                "discard plus once-per-turn; destruction is not negation", target)) return false;
            AcceptAction(new WAction { Kind = WKind.Tactical, Source = CardId.Haine,
                Location = WZone.Board, Description = 0, Target = target.Id,
                Cost = DiscardOrder(Bot.Hand.Where(c => c.IsSpell()))[0].Id, CostZone = WZone.Hand });
            return true;
        }

        private bool VerreEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.MadameVerre, 1) &&
                Duel.LastChainPlayer == 1 && Enemy.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled()) &&
                Bot.Hand.Any(c => c.IsSpell()) && WorthVerre())
            {
                AI.SelectCard(DiscardOrder(Bot.Hand.Where(c => c.IsSpell())));
                _used.Add(CardId.MadameVerre + ":" + WKind.NegateBoard);
                return true;
            }
            return false;
        }

        private bool ViceResponse()
        {
            if (Duel.Player == 0) return false; // Own-turn choices belong to the planner.
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 0))
            {
                ClientCard target = RemovalTarget(false);
                if (target == null || !TacticalDecision("Vice-destroy", RemovalValue(target, false), 4,
                    "coordinate with reverse-chain removals; choose again at resolution", target)) return false;
                _viceRemoval = target;
                return true;
            }
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 1))
            {
                AI.SelectCard(CardId.Seed, CardId.Schmietta);
                return true;
            }
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 2))
            {
                AI.SelectCard(CardId.Celebration, CardId.ConfusionConfession, CardId.Creation);
                return true;
            }
            return false;
        }

        private bool SeedResponse()
        {
            ClientCard target = Enemy.GetMonsters().Concat(Enemy.GetSpells()).Where(c => c.IsFaceup() &&
                !c.IsShouldNotBeTarget() && !c.IsShouldNotBeMonsterTarget() && !ChainForecast().Removed.Contains(c))
                .OrderByDescending(c => c.IsFloodgate()).ThenByDescending(c => c.Attack).FirstOrDefault();
            if (Card.Location != CardLocation.MonsterZone || target == null) return false;
            AcceptAction(new WAction { Kind = WKind.Tactical, Source = CardId.Seed,
                Location = WZone.Board, Description = 0, Target = target.Id });
            return true;
        }

        private bool EndPhaseRecovery()
        {
            if (Duel.Phase != DuelPhase.End || Duel.Player != 0 || Card.Controller != 0) return false;
            if (Card.Id == CardId.Pupils && Card.Location == CardLocation.MonsterZone)
            {
                AI.SelectCard(CardId.Creation, CardId.Genni, CardId.Marka);
                return true;
            }
            return Card.Location == CardLocation.Grave && Card.HasSetcode(WitchcrafterSetcode) &&
                Card.IsSpell() && Bot.GetMonsters().Any(c => c.IsFaceup() && c.HasSetcode(WitchcrafterSetcode));
        }

        private WAction PromptAction()
        {
            if (_awaitingChain) return _accepted; // Cost selection precedes MSG_CHAINING.
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
            {
                WAction action;
                return solving.ActivatePlayer == 0 && _prompts.TryGetValue(Duel.SolvingChainIndex, out action)
                    ? action : null;
            }
            ClientCard current = Duel.GetCurrentChainCard();
            if (current != null)
            {
                WAction action;
                return current.Controller == 0 && _prompts.TryGetValue(Duel.CurrentChain.Count, out action) ? action : null;
            }
            return _accepted; // Summon procedure material selection has no chain.
        }

        private static IList<ClientCard> Pick(IList<ClientCard> cards, IEnumerable<int> ids,
            int min, int max, int wanted = 1)
        {
            List<int> order = ids.ToList();
            return cards.OrderBy(c => order.IndexOf(c.Id) < 0 ? 999 : order.IndexOf(c.Id))
                .Take(Math.Min(max, Math.Max(min, wanted))).ToList();
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max,
            int hint, bool cancelable)
        {
            IList<ClientCard> graveSelection = SelectGraveCards(cards, min, max, hint, cancelable);
            if (graveSelection != null) return graveSelection;
            ChainInfo resolving = Duel.GetCurrentSolvingChainInfo();
            if (hint == HintMsg.Destroy && resolving != null && resolving.ActivatePlayer == 0 &&
                (resolving.ActivateId == CardId.Celebration || resolving.ActivateId == CardId.Pupils ||
                (resolving.ActivateId == CardId.Genni && PromptAction() != null && PromptAction().CopiedSpell == CardId.Celebration)))
                return SelectDestructionPair(cards, min, max, cancelable);
            if (hint == HintMsg.Destroy && resolving != null && resolving.ActivatePlayer == 0 &&
                resolving.ActivateId == CardId.ViceMadame)
            {
                IList<ClientCard> result = cards.OrderByDescending(c => RemovalValue(c, false))
                    .ThenByDescending(c => c.Controller == 1).Take(Math.Min(max, Math.Max(1, min))).ToList();
                WChainForecast forecast = ChainForecast();
                Logger.DebugWriteLine("Witchcraft2026 opponent resolution-choice: " + string.Join(" | ", cards.Take(8).Select(c =>
                    c.Id + " value=" + RemovalValue(c, false).ToString("0.0") + " " + OpponentAssessment(c, forecast).Explain())) +
                    " selected=" + string.Join(",", result.Select(c => c.Id)) + "; offered targets only");
                return result;
            }
            WAction action = PromptAction();
            if (action != null)
            {
                if (hint == HintMsg.Remove && action.CopiedSpell != 0)
                    return Pick(cards, new[] { action.CopiedSpell }, min, max);
                if (action.Kind == WKind.Tactical && (hint == HintMsg.Destroy || hint == HintMsg.ReturnToHand))
                    return Pick(cards, new[] { action.Target }, min, max);
                if ((hint == HintMsg.Discard || hint == HintMsg.ToGrave) && action.Cost != 0)
                {
                    ClientCard cost = cards.FirstOrDefault(c => c.Id == action.Cost && Zone(c.Location) == action.CostZone);
                    if (cost != null && min <= 1) return new[] { cost };
                }
                if (hint == HintMsg.SpSummon && action.Kind == WKind.LacrimaRecruit)
                {
                    WState state = Observe();
                    WZone origin = RecruitOrigin(state, action);
                    ClientCard target = cards.FirstOrDefault(c => c.Id == action.Target && Zone(c.Location) == origin);
                    if (target != null && min <= 1) return new[] { target };
                }
                if (hint == HintMsg.SpSummon && action.Kind == WKind.RillieRecruit)
                    return Pick(cards, action.Recruits, min, max, action.Recruits.Length);
                if (hint == HintMsg.AddToHand || hint == HintMsg.SpSummon ||
                    (hint == HintMsg.ToGrave && (action.Kind == WKind.Send || action.Kind == WKind.LacrimaSend)))
                    if (action.Target != 0) return Pick(cards, new[] { action.Target }, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.GenniCopy)
                    return Pick(cards, new[] { CardId.Creation, CardId.Genni }, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.GenniLacrima)
                    return Pick(cards, new[] { CardId.Lacrima }, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.MaidExchange)
                    return Pick(cards, action.Materials, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.DarkSummon)
                    return Pick(cards, action.Materials, min, max);
                if (hint == HintMsg.Remove && action.Source == CardId.RainbowBridgeBifrost)
                    return Pick(cards, new[] { CardId.ElfnoteJunePride, CardId.CosmicQuasarDragon,
                        CardId.DespianLuluwalilith }, min, max, 3);
                if (hint == HintMsg.ToDeck && action.Kind == WKind.Gaze)
                    return Pick(cards, action.Materials, min, max);
                if (hint == HintMsg.Confirm && action.Source == CardId.Pupils)
                    return Pick(cards, new[] { CardId.Celebration }, min, max);
            }
            if (hint == HintMsg.Destroy || hint == HintMsg.Disable || hint == HintMsg.ReturnToHand)
                return cards.OrderByDescending(c => c.Controller == 1).ThenByDescending(c => c.IsFloodgate())
                    .ThenByDescending(c => c.Attack).Take(Math.Min(max, Math.Max(1, min))).ToList();
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectFusionMaterial(IList<ClientCard> cards, int min, int max)
        {
            WAction action = PromptAction();
            if (action == null || action.Materials.Length == 0)
                return base.OnSelectFusionMaterial(cards, min, max);
            List<ClientCard> selected = new List<ClientCard>();
            for (int i = 0; i < action.Materials.Length; i++)
            {
                ClientCard material = cards.FirstOrDefault(c => c.Id == action.Materials[i] &&
                    Zone(c.Location) == action.MaterialZones[i] && !selected.Contains(c));
                if (material != null) selected.Add(material);
            }
            // The engine can request fusion materials one at a time.
            if (selected.Count >= min)
                return selected.Take(Math.Min(max, selected.Count)).ToList();
            Logger.DebugWriteLine("Witchcraft2026 material mismatch: " + action);
            return base.OnSelectFusionMaterial(cards, min, max);
        }

        public override int OnSelectOption(IList<int> options)
        {
            WAction action = PromptAction();
            if (action != null && action.Kind == WKind.CelebrationDestroy)
            {
                int destroy = options.IndexOf(UtilString(CardId.Celebration, 2));
                if (destroy >= 0) return destroy;
                Logger.DebugWriteLine("Witchcraft2026 mode mismatch: wanted destruction, offered=" + string.Join(",", options));
                Invalidate();
            }
            if (action != null && action.Kind == WKind.PupilsSearch)
            {
                int search = options.IndexOf(UtilString(CardId.Pupils, 2));
                if (search >= 0) return search;
            }
            if (action != null && (action.Kind == WKind.LacrimaSend ||
                action.Kind == WKind.LacrimaRecruit || action.Kind == WKind.GenniLacrima))
            {
                int option = options.IndexOf(UtilString(CardId.Lacrima, action.Kind == WKind.LacrimaSend ? 1 : 2));
                if (option >= 0) return option;
            }
            if (action != null && action.Kind == WKind.Fusion)
            {
                foreach (int id in new[] { CardId.Pupils, CardId.Celebration })
                {
                    int index = options.IndexOf(Util.GetStringId(id, 3));
                    if (index >= 0) return index;
                }
                // We cannot fabricate a cancel option after activation. Retire the
                // fusion expectation and let the owned destruction callback minimize
                // loss if the engine supplies only that mandatory mode.
                Logger.DebugWriteLine("Witchcraft2026 mode mismatch: wanted Fusion, offered=" +
                    string.Join(",", options) + "; forecast invalidated, no invented cancel");
                Invalidate();
            }
            return base.OnSelectOption(options);
        }

        public override int OnSelectPlace(int cardId, int player, CardLocation location, int available)
        {
            if (player == 0 && location == CardLocation.MonsterZone &&
                cardId == CardId.Pupils)
            {
                if ((available & 0x20) != 0) return 0x20;
                if ((available & 0x40) != 0) return 0x40;
            }
            return base.OnSelectPlace(cardId, player, location, available);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (Duel.Turn <= 1 && cardId != CardId.RedEyesDarkDragoon && cardId != CardId.Haine &&
                positions.Contains(CardPosition.FaceUpDefence)) return CardPosition.FaceUpDefence;
            return base.OnSelectPosition(cardId, positions);
        }

        private static IList<ClientCard> DiscardOrder(IEnumerable<ClientCard> cards)
        {
            return cards.OrderBy(c => GetWalpurgisDiscardRank(c.Id, true)).ToList();
        }

        private static int GetNextGuideLowWitchcrafter(int id)
        {
            if (id == CardId.Genni) return CardId.Schmietta;
            if (id == CardId.Schmietta) return CardId.Pittore;
            if (id == CardId.Pittore) return CardId.Potterie;
            if (id == CardId.Potterie) return CardId.Marka;
            return 0;
        }

        private static int GetWalpurgisDiscardRank(int id, bool guide)
        {
            if (guide && id == CardId.MadameVerre) return -100;
            int[] order = { CardId.RainbowBridgeBifrost, CardId.MaidVerre, CardId.Distortion,
                CardId.Celebration, CardId.Bystreet, CardId.Unveiling, CardId.ConfusionConfession,
                CardId.Creation, CardId.Lacrima, CardId.Terraforming, CardId.SpellPowerMastery };
            int index = Array.IndexOf(order, id);
            if (index >= 0) return index;
            if (guide && (id == CardId.Genni || id == CardId.GazeOfTimaeus)) return 1000;
            return 100;
        }

        #region Strategic planner

        // Each edge owns its costs and choices. No predicted state is written into Duel.
        internal enum WZone { Deck, Hand, Board, Grave, Banished, Extra, Backrow }
        internal enum WKind
        {
            FieldAccess, Mastery, Creation, Marka, ActivateField, FieldSearch,
            Normal, Recruit, DarkSummon, GazeSearch, Gaze, GenniCopy, Recover,
            Fusion, Send, RillieRecruit, EdelRevive, ViceRecover, MaidSummon,
            MaidSearch, SetTrap, Unveiling, LacrimaSend, LacrimaRecruit,
            GenniLacrima, MaidExchange, NegateBoard, PupilsSearch, Tactical,
            ActivateBystreet, CelebrationDestroy, SeedRedraw, PittoreDraw, PotterieRecover,
            DistortionSearch, CopiedDraping
        }

        internal sealed class WAction
        {
            public WKind Kind;
            public int Source;
            public WZone Location;
            public int Description;
            public int Target;
            public int Cost;
            public WZone CostZone;
            public int[] Materials = new int[0];
            public WZone[] MaterialZones = new WZone[0];
            public int[] Recruits = new int[0];
            public string Route;
            public int EnemyKey;
            public int CopiedSpell;
            public string Usage { get { return Source + ":" + Kind; } }
            public string Identity { get { return Kind + ":" + Source + ":" + Location; } }
            public override string ToString()
            {
                return Kind + "(" + Source + ")->" + Target + " cost=" + Cost +
                    " materials=" + string.Join(",", Materials) + (EnemyKey == 0 ? "" : " enemy-slot=" + EnemyKey) +
                    (CopiedSpell == 0 ? "" : " copy=" + CopiedSpell);
            }
        }

        internal sealed class WState
        {
            public Dictionary<long, int> Cards = new Dictionary<long, int>();
            public HashSet<string> Used = new HashSet<string>();
            public HashSet<int> Disabled = new HashSet<int>();
            public List<WRestriction> Restrictions = new List<WRestriction>();
            public Dictionary<int, int> Temporary = new Dictionary<int, int>();
            public int Turn;
            public bool Normal = true;
            public bool SpellActivity;
            public bool ExtraLock;
            public bool Maxx;
            public bool ViceWindow;
            public bool OpponentTurn;
            public bool EnemyChain;
            public int IncomingAttack;
            public int LifePoints = 8000;
            public int Pending;
            public int UnknownHand;
            public int OtherMonsters;
            public int ExtraOccupant;
            public int FutureSummons;
            public int Steps;
            public OpponentResponse OpposingResponses;
            public double? DenialFloor;
            public double CommitmentCost;
            public string Vulnerability;
            public List<WRemovalTarget> EnemyTargets = new List<WRemovalTarget>();
            public HashSet<int> FreshShields = new HashSet<int>();
            public bool CanEstablishShield = true;
            public int OccupiedBackrow;
            public double RemovedThreat;
            public bool RandomBoundary;
            public double DrawUtility;
            public bool DistortionReady;
            public bool SpellcasterProtected;
            public HashSet<int> BanishOnLeave = new HashSet<int>();
            public WState Copy()
            {
                WState copy = (WState)MemberwiseClone();
                copy.Cards = new Dictionary<long, int>(Cards);
                copy.Used = new HashSet<string>(Used);
                copy.Disabled = new HashSet<int>(Disabled);
                copy.Restrictions = Restrictions.Select(r => r.Copy()).ToList();
                copy.Temporary = new Dictionary<int, int>(Temporary);
                copy.EnemyTargets = new List<WRemovalTarget>(EnemyTargets);
                copy.FreshShields = new HashSet<int>(FreshShields);
                copy.BanishOnLeave = new HashSet<int>(BanishOnLeave);
                return copy;
            }
            public int Count(int id, WZone zone)
            {
                int count;
                return Cards.TryGetValue((long)id * 8 + (int)zone, out count) ? count : 0;
            }
            public bool Has(int id, WZone zone) { return Count(id, zone) > 0; }
            public void Add(int id, WZone zone, int amount = 1)
            {
                long key = (long)id * 8 + (int)zone;
                int count = Count(id, zone) + amount;
                if (count < 0) throw new InvalidOperationException("Model spent absent card " + id);
                if (count == 0) Cards.Remove(key); else Cards[key] = count;
            }
            public void Move(int id, WZone from, WZone to)
            {
                if (from == WZone.Board && BanishOnLeave.Remove(id)) to = WZone.Banished;
                if (id == CardId.Distortion && to == WZone.Grave) DistortionReady = false;
                Add(id, from, -1);
                Add(id, to);
                if (from == WZone.Board && ExtraOccupant == id) ExtraOccupant = 0;
                if (from == WZone.Board) Temporary.Remove(id);
            }
            public IEnumerable<int> In(WZone zone)
            {
                return Cards.Where(pair => pair.Key % 8 == (int)zone && pair.Value > 0)
                    .Select(pair => (int)(pair.Key / 8));
            }
            public int Size(WZone zone) { return In(zone).Sum(id => Count(id, zone)); }
            public bool Active(int id)
            {
                return Has(id, WZone.Board) && !Disabled.Contains(id);
            }
            public string Key()
            {
                return string.Join(",", Cards.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)) +
                    "|" + string.Join(",", Used.OrderBy(k => k)) + "|" +
                    string.Join(",", Disabled.OrderBy(k => k)) + "|" +
                    Normal + "|" + SpellActivity + "|" + ExtraLock + "|" + Maxx +
                    "|" + Pending + "|" + ViceWindow + "|" + FutureSummons + "|" +
                    ExtraOccupant + "|" + UnknownHand + "|" + OtherMonsters + "|" +
                    OpponentTurn + "|" + EnemyChain + "|" + IncomingAttack + "|" + LifePoints +
                    "|" + RestrictionSummary(this) + "|" + Turn + "|" +
                    string.Join(",", Temporary.OrderBy(p => p.Key).Select(p => p.Key + "@" + p.Value)) +
                    "|" + OpposingResponses + "|" + DenialFloor + "|" + CommitmentCost +
                    "|" + string.Join(",", EnemyTargets.Select(t => t.Key + ":" + t.Id + ":" + t.Value + ":" + t.Responses)) +
                    "|" + string.Join(",", FreshShields.OrderBy(id => id)) + "|" + CanEstablishShield +
                    "|" + OccupiedBackrow + "|" + RemovedThreat + "|" + RandomBoundary + "|" + DrawUtility +
                    "|" + DistortionReady + "|" + SpellcasterProtected + "|" + string.Join(",", BanishOnLeave.OrderBy(id => id));
            }
        }

        internal sealed class WMove
        {
            public WAction Action;
            public WState After;
        }

        internal sealed class WPlan
        {
            public List<WMove> Steps = new List<WMove>();
            public List<string> Alternatives = new List<string>();
            public double Score;
            public int Expanded;
            public bool TimedOut;
        }

        internal static readonly int[] LowIds =
        {
            CardId.Genni, CardId.Schmietta, CardId.Pittore, CardId.Potterie
        };
        internal static readonly int[] WitchMonsters =
        {
            CardId.Genni, CardId.Schmietta, CardId.Pittore, CardId.Potterie,
            CardId.Marka, CardId.Terracottan, CardId.Haine, CardId.MadameVerre,
            CardId.Edel, CardId.Seed, CardId.MaidVerre, CardId.MaidGenni
        };
        internal static readonly int[] NormalIds = LowIds.Concat(new[]
        {
            CardId.Seed, CardId.MaidGenni, CardId.MaidVerre, CardId.Terracottan
        }).ToArray();
        internal static readonly int[] WitchSpells =
        {
            CardId.Walpurgis, CardId.Creation, CardId.Celebration,
            CardId.ConfusionConfession, CardId.Unveiling, CardId.Bystreet, CardId.Draping
        };
        internal static readonly int[] KnownIds = typeof(CardId).GetFields()
            .Where(field => field.IsLiteral).Select(field => (int)field.GetRawConstantValue()).ToArray();

        internal static bool IsModelSpell(int id)
        {
            return WitchSpells.Contains(id) || id == CardId.Lacrima ||
                id == CardId.Terraforming || id == CardId.RainbowBridgeBifrost ||
                id == CardId.GazeOfTimaeus || id == CardId.SpellPowerMastery;
        }
        internal static bool IsWitchMaterial(int id)
        {
            return WitchMonsters.Contains(id) || id == CardId.Pupils ||
                id == CardId.MadameRillie || id == CardId.ViceMadame;
        }
        internal static bool IsSpellcasterMaterial(int id)
        {
            return IsWitchMaterial(id) || id == CardId.MaidVerre ||
                id == CardId.MaidGenni || id == CardId.RedEyesDarkDragoon ||
                id == CardId.DarkMagicianOfDestruction;
        }

        internal static class WPlanner
        {
            private const int MaxDepth = 30;
            private const int BeamWidth = 64;
            private const int MaxNodes = 5000;
            private const int BudgetMs = 180;
            private const int MaxCandidates = 256;
            private sealed class Node
            {
                public WState State;
                public List<WMove> Path;
                public double Score;
            }

            public static WPlan Plan(WState root, Func<WAction, bool> offered,
                CancellationToken cancellation)
            {
                Stopwatch watch = Stopwatch.StartNew();
                WPlan result = new WPlan { Score = Evaluate(root) };
                List<Node> frontier = new List<Node>
                {
                    new Node { State = root, Path = new List<WMove>(), Score = result.Score }
                };
                Dictionary<string, Node> alternatives = new Dictionary<string, Node>();
                // Evaluate the documented route to its endpoint before beam pruning.
                // Every edge still comes from the same resource-checked action generator.
                // This keeps a long bridge from losing to a shallow, immediately valuable boss.
                foreach (WAction opening in Actions(root).Take(MaxCandidates).OrderByDescending(a => BookRank(root, a)))
                {
                    if (cancellation.IsCancellationRequested)
                        return new WPlan { Score = Evaluate(root) };
                    if (watch.ElapsedMilliseconds >= BudgetMs || result.Expanded >= MaxNodes)
                    {
                        result.TimedOut = true;
                        break;
                    }
                    if (offered != null && !offered(opening)) continue;
                    WState state = root;
                    WAction action = opening;
                    List<WMove> path = new List<WMove>();
                    while (action != null && path.Count < MaxDepth && watch.ElapsedMilliseconds < BudgetMs &&
                        result.Expanded < MaxNodes)
                    {
                        if (cancellation.IsCancellationRequested)
                            return new WPlan { Score = Evaluate(root) };
                        state = Apply(state, action);
                        result.Expanded++;
                        path.Add(new WMove { Action = action, After = state });
                        double score = Evaluate(state);
                        if (score > result.Score + .01)
                        {
                            result.Score = score;
                            result.Steps = new List<WMove>(path);
                        }
                        Node previous;
                        string key = opening.ToString();
                        if (!alternatives.TryGetValue(key, out previous) || score > previous.Score)
                            alternatives[key] = new Node { State = state, Path = new List<WMove>(path), Score = score };
                        action = Actions(state).Take(MaxCandidates).Where(a => BookRank(state, a) > 0)
                            .OrderByDescending(a => BookRank(state, a)).FirstOrDefault();
                    }
                }
                for (int depth = 0; depth < MaxDepth && frontier.Count > 0; depth++)
                {
                    List<Node> next = new List<Node>();
                    foreach (Node node in frontier)
                    {
                        if (cancellation.IsCancellationRequested)
                            return new WPlan { Score = Evaluate(root) };
                        if (watch.ElapsedMilliseconds >= BudgetMs || result.Expanded >= MaxNodes)
                        {
                            result.TimedOut = true;
                            break;
                        }
                        result.Expanded++;
                        foreach (WAction action in Actions(node.State).Take(MaxCandidates))
                        {
                            if (watch.ElapsedMilliseconds >= BudgetMs)
                            {
                                result.TimedOut = true;
                                break;
                            }
                            if (depth == 0 && offered != null && !offered(action)) continue;
                            WState after = Apply(node.State, action);
                            WMove move = new WMove { Action = action, After = after };
                            List<WMove> path = new List<WMove>(node.Path) { move };
                            double score = Evaluate(after);
                            Node child = new Node { State = after, Path = path, Score = score };
                            next.Add(child);
                            string first = path[0].Action.ToString();
                            Node previous;
                            if (!alternatives.TryGetValue(first, out previous) || score > previous.Score)
                                alternatives[first] = child;
                            // A reference route is a search prior only. The selected endpoint must
                            // beat preserving the observed board/resources under the same evaluator.
                            if (score > result.Score + 0.01)
                            {
                                result.Score = score;
                                result.Steps = path;
                            }
                        }
                        if (result.TimedOut) break;
                    }
                    if (result.TimedOut) break;
                    // Preserve different openings so an early engine extension cannot erase all
                    // candidates belonging to the book or the low-summon control alternative.
                    frontier = next.GroupBy(node => node.State.Key())
                        .Select(group => group.OrderByDescending(n => n.Score).First())
                        .GroupBy(n => n.Path[0].Action.Identity)
                        .SelectMany(group => group.OrderByDescending(n =>
                            n.Score + Potential(n.State)).Take(10))
                        .OrderByDescending(n => n.Score + Potential(n.State))
                        .Take(BeamWidth).ToList();
                }
                result.Alternatives = alternatives.Values.OrderByDescending(n => n.Score)
                    .Take(4).Select(n => n.Path[0].Action + " => " +
                        n.Score.ToString("0.0") + " [" + BoardSummary(n.State) + "]").ToList();
                // If independent access and a normal summon commute, resolve access first.
                // This preserves the normal choice if the search is interrupted. Validate
                // both reordered edges against the model, not just their printed names.
                if (result.Steps.Count >= 2 && result.Steps[0].Action.Kind == WKind.Normal)
                {
                    WAction access = result.Steps[1].Action;
                    WAction normal = result.Steps[0].Action;
                    if ((access.Kind == WKind.FieldAccess || access.Kind == WKind.ActivateField) &&
                        Actions(root).Any(a => a.ToString() == access.ToString()) &&
                        (offered == null || offered(access)))
                    {
                        WState first = Apply(root, access);
                        if (Actions(first).Any(a => a.ToString() == normal.ToString()))
                        {
                            result.Steps[0] = new WMove { Action = access, After = first };
                            result.Steps[1] = new WMove { Action = normal, After = Apply(first, normal) };
                        }
                    }
                }
                return result;
            }

            private static WAction A(WKind kind, int source, WZone zone,
                int target = 0, int description = -1)
            {
                return new WAction { Kind = kind, Source = source, Location = zone,
                    Target = target, Description = description, Route = "resource-search" };
            }

            private static int BookRank(WState s, WAction a)
            {
                if (s.Pending != 0) return 1000;
                if (a.Kind == WKind.ViceRecover && a.Target == CardId.Distortion) return 980;
                if (a.Kind == WKind.SetTrap && s.Active(CardId.ViceMadame)) return 970;
                if (a.Kind == WKind.EdelRevive && a.Target == CardId.RedEyesDarkDragoon &&
                    s.Active(CardId.ViceMadame)) return 960;
                if (a.Kind == WKind.Fusion && a.Source == CardId.Celebration &&
                    a.Target == CardId.ViceMadame && s.Active(CardId.MadameRillie) &&
                    a.Materials.Contains(CardId.Terracottan) && a.Materials.Contains(CardId.Marka)) return 950;
                if (a.Kind == WKind.Fusion && a.Source == CardId.Pupils &&
                    a.Target == CardId.MadameRillie &&
                    a.Materials.Contains(CardId.Genni) && a.Materials.Contains(CardId.Schmietta)) return 940;
                if (a.Kind == WKind.Send && a.Target == CardId.MadameVerre &&
                    s.Active(CardId.Pupils) && !s.Has(CardId.MadameVerre, WZone.Grave) &&
                    !s.Has(CardId.MadameVerre, WZone.Banished)) return 930;
                if (a.Kind == WKind.Fusion && a.Source == CardId.Terracottan &&
                    a.Target == CardId.Pupils && a.Materials.Contains(CardId.Terracottan) &&
                    a.Materials.Contains(CardId.RedEyesDarkDragoon)) return 920;
                if (a.Kind == WKind.Recover && a.Target == CardId.Celebration &&
                    s.Active(CardId.RedEyesDarkDragoon)) return 910;
                if (a.Kind == WKind.GenniCopy && a.Target == CardId.Terracottan &&
                    s.Active(CardId.RedEyesDarkDragoon) && !s.Has(CardId.Terracottan, WZone.Hand)) return 900;
                if (a.Kind == WKind.Gaze) return 890;
                if (a.Kind == WKind.DarkSummon) return 880;
                if (a.Kind == WKind.Recruit && a.Target == GetNextGuideLowWitchcrafter(a.Source))
                {
                    int cost = a.Source == CardId.Genni ? CardId.Creation :
                        a.Source == CardId.Schmietta ? CardId.Celebration :
                        a.Source == CardId.Pittore ? CardId.Distortion : CardId.ConfusionConfession;
                    return a.Cost == cost ? 870 : 860;
                }
                // Missing a one-of in the ladder is not the end of the route.
                if (a.Kind == WKind.Recruit && a.Target == CardId.Marka &&
                    !s.Has(GetNextGuideLowWitchcrafter(a.Source), WZone.Deck) && !s.ExtraLock)
                    return 855;
                if (a.Kind == WKind.Normal && a.Source == CardId.Genni &&
                    s.Has(CardId.Walpurgis, WZone.Backrow)) return 850;
                if (a.Kind == WKind.FieldSearch && a.Target == CardId.Genni &&
                    !s.Has(CardId.Genni, WZone.Hand) && s.Normal)
                    return 840 - Math.Min(20, Math.Max(-1, GetWalpurgisDiscardRank(a.Cost, true)));
                if (a.Kind == WKind.ActivateField) return 810;
                if (a.Kind == WKind.FieldAccess && !s.Has(CardId.Walpurgis, WZone.Hand)) return 800;
                if (a.Kind == WKind.Marka) return 790;
                if (a.Kind == WKind.Creation && a.Target == CardId.Marka &&
                    !s.Has(CardId.Marka, WZone.Hand) && !s.Has(CardId.Walpurgis, WZone.Backrow) &&
                    !s.Has(CardId.Walpurgis, WZone.Hand)) return 780;
                return 0;
            }

            public static IEnumerable<WAction> Actions(WState s)
            {
                foreach (WAction a in Generate(s))
                {
                    if (s.OpponentTurn && a.Kind != WKind.Recruit && a.Kind != WKind.MaidExchange &&
                        a.Kind != WKind.LacrimaRecruit && a.Kind != WKind.ViceRecover &&
                        a.Kind != WKind.RillieRecruit && a.Kind != WKind.MaidSearch && a.Kind != WKind.DistortionSearch) continue;
                    if (a.Source == CardId.Genni && a.Location == WZone.Grave && s.Used.Contains("64756282:grave-copy")) continue;
                    if (s.Used.Contains(a.Usage)) continue;
                    if (a.Location == WZone.Board && s.Disabled.Contains(a.Source)) continue;
                    if (!SummonAllowed(s, a)) continue;
                    yield return a;
                }
            }

            private static IEnumerable<WAction> Generate(WState s)
            {
                if (s.RandomBoundary) yield break; // unknown draw identities require a real observation
                int board = s.Size(WZone.Board) + s.OtherMonsters;
                if (s.Pending == CardId.DarkMagicianOfDestruction)
                {
                    if (s.Has(CardId.GazeOfTimaeus, WZone.Deck))
                        yield return A(WKind.GazeSearch, s.Pending, WZone.Board, CardId.GazeOfTimaeus, 0);
                    yield break;
                }
                if (s.Pending == CardId.MadameRillie)
                {
                    int[] ids = new[] { CardId.Haine, CardId.Edel, CardId.MaidGenni };
                    int free = Math.Max(0, 5 - MainMonsters(s));
                    int[] recruit = ids.Where(id => s.Has(id, WZone.Deck) || s.Has(id, WZone.Hand))
                        .Take(free).ToArray();
                    if (recruit.Length > 0)
                    {
                        WAction a = A(WKind.RillieRecruit, s.Pending, WZone.Board, 0, 0);
                        a.Recruits = recruit;
                        a.Route = "guide:Rilliona recruits";
                        yield return a;
                    }
                    yield break;
                }
                if (s.Pending == CardId.MaidVerre)
                {
                    foreach (int target in new[] { CardId.Walpurgis, CardId.Creation,
                        CardId.Distortion, CardId.Celebration })
                        if (s.Has(target, WZone.Deck))
                            yield return A(WKind.MaidSearch, s.Pending, WZone.Board, target, 1);
                    yield break;
                }

                foreach (WAction coverage in CoverageActions(s)) yield return coverage;
                foreach (WAction grave in GraveActions(s)) yield return grave;

                foreach (int source in new[] { CardId.Terraforming, CardId.RainbowBridgeBifrost })
                {
                    if (s.Has(source, WZone.Hand) && s.Has(CardId.Walpurgis, WZone.Deck) &&
                        !s.Has(CardId.Walpurgis, WZone.Backrow) &&
                        (source != CardId.RainbowBridgeBifrost ||
                        new[] { CardId.CosmicQuasarDragon, CardId.DespianLuluwalilith,
                            CardId.ElfnoteJunePride }.All(id => s.Has(id, WZone.Extra))))
                        yield return A(WKind.FieldAccess, source, WZone.Hand, CardId.Walpurgis);
                }
                if (s.Has(CardId.SpellPowerMastery, WZone.Hand))
                    foreach (int id in new[] { CardId.MaidVerre, CardId.MaidGenni })
                        if (s.Has(id, WZone.Deck))
                            yield return A(WKind.Mastery, CardId.SpellPowerMastery, WZone.Hand, id);

                if (s.Has(CardId.Walpurgis, WZone.Hand) && !s.Has(CardId.Walpurgis, WZone.Backrow))
                    yield return A(WKind.ActivateField, CardId.Walpurgis, WZone.Hand);
                if (s.Has(CardId.Walpurgis, WZone.Backrow) && !s.Disabled.Contains(CardId.Walpurgis))
                {
                    foreach (int target in new[] { CardId.Genni, CardId.Schmietta,
                        CardId.Terracottan, CardId.Marka }.Concat(WitchMonsters).Distinct())
                    {
                        if (!s.Has(target, WZone.Deck)) continue;
                        foreach (int discard in s.In(WZone.Hand).Concat(new[] { target }).Distinct().OrderBy(id =>
                            GetWalpurgisDiscardRank(id, true)).Take(3))
                        {
                            WAction a = A(WKind.FieldSearch, CardId.Walpurgis, WZone.Backrow, target, 0);
                            a.Cost = discard; a.CostZone = WZone.Hand;
                            a.Route = "guide:Walpurgis access";
                            yield return a;
                        }
                    }
                }
                if (s.Active(CardId.MadameVerre) && s.Restrictions.Any(r => r.Player == 1 && r.Active))
                    foreach (int cost in s.In(WZone.Hand).Where(IsModelSpell))
                    {
                        WAction a = A(WKind.NegateBoard, CardId.MadameVerre, WZone.Board, 0, 1);
                        a.Cost = cost; a.CostZone = WZone.Hand;
                        a.Route = "disable observed summon restrictions before extending";
                        yield return a;
                    }
                if (s.Active(CardId.Pupils))
                    foreach (int target in WitchSpells.Concat(new[] { CardId.Lacrima }))
                        if (s.Has(target, WZone.Deck))
                            yield return A(WKind.PupilsSearch, CardId.Pupils, WZone.Board, target, 0);
                if (s.Has(CardId.Creation, WZone.Hand))
                    foreach (int target in new[] { CardId.Marka, CardId.Genni,
                        CardId.Schmietta, CardId.Terracottan }.Concat(WitchMonsters).Distinct())
                        if (s.Has(target, WZone.Deck))
                            yield return A(WKind.Creation, CardId.Creation, WZone.Hand, target);
                foreach (WZone zone in new[] { WZone.Hand, WZone.Board })
                    if (s.Has(CardId.Marka, zone) && !s.Has(CardId.Walpurgis, WZone.Backrow) &&
                        !s.Has(CardId.Walpurgis, WZone.Hand) && s.Has(CardId.Walpurgis, WZone.Deck))
                        yield return A(WKind.Marka, CardId.Marka, zone, CardId.Walpurgis, 0);

                if (s.Normal && MainMonsters(s) < 5)
                    foreach (int low in NormalIds)
                        if (s.Has(low, WZone.Hand))
                            yield return A(WKind.Normal, low, WZone.Hand);
                foreach (int low in LowIds)
                {
                    if (!s.Active(low)) continue;
                    int next = GetNextGuideLowWitchcrafter(low);
                    IEnumerable<int> targets = new[] { next, CardId.Haine, CardId.MadameVerre }
                        .Concat(WitchMonsters.Where(id => id != low));
                    foreach (int target in targets.Distinct())
                    {
                        if (!s.Has(target, WZone.Deck)) continue;
                        foreach (var cost in Costs(s, low).Take(2))
                        {
                            WAction a = A(WKind.Recruit, low, WZone.Board, target, 0);
                            a.Cost = cost.Item1; a.CostZone = cost.Item2;
                            a.Route = target == next ? "guide:low ladder" : "control:stop on boss";
                            yield return a;
                        }
                    }
                }
                if (s.SpellActivity && !s.ExtraLock && s.Active(CardId.Marka) &&
                    s.Has(CardId.DarkMagicianOfDestruction, WZone.Extra))
                {
                    WAction a = A(WKind.DarkSummon, CardId.DarkMagicianOfDestruction, WZone.Extra);
                    a.Materials = new[] { CardId.Marka }; a.MaterialZones = new[] { WZone.Board };
                    a.Route = "guide:Dragoon bridge";
                    yield return a;
                }
                if (!s.ExtraLock && s.Has(CardId.GazeOfTimaeus, WZone.Hand) &&
                    s.Has(CardId.RedEyesDarkDragoon, WZone.Extra))
                    foreach (WZone zone in new[] { WZone.Board, WZone.Grave })
                        if (s.Has(CardId.DarkMagicianOfDestruction, zone))
                        {
                            WAction a = A(WKind.Gaze, CardId.GazeOfTimaeus, WZone.Hand,
                                CardId.RedEyesDarkDragoon, 0);
                            a.Materials = new[] { CardId.DarkMagicianOfDestruction };
                            a.MaterialZones = new[] { zone };
                            yield return a;
                        }
                if (s.Has(CardId.Genni, WZone.Grave) && s.Has(CardId.Creation, WZone.Grave))
                    foreach (int target in WitchMonsters)
                        if (s.Has(target, WZone.Deck))
                            yield return A(WKind.GenniCopy, CardId.Genni, WZone.Grave, target, 1);
                if (s.Has(CardId.Genni, WZone.Grave) && s.Has(CardId.Lacrima, WZone.Grave) &&
                    MainMonsters(s) < 5)
                    foreach (int target in WitchMonsters)
                        if (s.Has(target, WZone.Deck) || s.Has(target, WZone.Hand))
                            yield return A(WKind.GenniLacrima, CardId.Genni, WZone.Grave, target, 1);
                if (s.Has(CardId.Lacrima, WZone.Hand))
                {
                    if (s.In(WZone.Board).Any(IsWitchMaterial))
                        foreach (int target in new[] { CardId.Genni, CardId.Creation,
                            CardId.Celebration, CardId.Schmietta })
                            if (s.Has(target, WZone.Deck))
                                yield return A(WKind.LacrimaSend, CardId.Lacrima, WZone.Hand, target, 0);
                    if (s.EnemyChain && MainMonsters(s) < 5)
                        foreach (int target in new[] { CardId.MaidVerre, CardId.Marka,
                            CardId.Haine, CardId.MadameVerre, CardId.Genni })
                            if (s.Has(target, WZone.Deck) || s.Has(target, WZone.Hand))
                                yield return A(WKind.LacrimaRecruit, CardId.Lacrima, WZone.Hand, target, 0);
                }
                if (s.Has(CardId.Terracottan, WZone.Hand) && MainMonsters(s) < 5)
                    foreach (int target in new[] { CardId.Celebration, CardId.Creation, CardId.ConfusionConfession }
                        .Concat(WitchMonsters.Where(id => id != CardId.Terracottan)))
                        if (s.Has(target, WZone.Grave))
                            yield return A(WKind.Recover, CardId.Terracottan, WZone.Hand, target, 0);
                if (s.Has(CardId.Schmietta, WZone.Grave))
                    foreach (int target in WitchMonsters.Concat(WitchSpells).Concat(new[] { CardId.Lacrima, CardId.Distortion }).Where(id => id != CardId.Schmietta))
                        if (s.Has(target, WZone.Deck) && !s.Has(target, WZone.Grave))
                            yield return A(WKind.Send, CardId.Schmietta, WZone.Grave, target, 1);

                foreach (WAction fusion in Fusions(s)) yield return fusion;
                if (s.Active(CardId.Edel))
                    foreach (int target in s.In(WZone.Grave).Where(id => IsSpellcasterMaterial(id) &&
                        id != CardId.Edel && !IsExtra(id)))
                        yield return A(WKind.EdelRevive, CardId.Edel, WZone.Board, target, 1);
                if (s.Active(CardId.Edel) && s.Has(CardId.RedEyesDarkDragoon, WZone.Grave))
                    yield return A(WKind.EdelRevive, CardId.Edel, WZone.Board, CardId.RedEyesDarkDragoon, 1);
                if (s.Active(CardId.ViceMadame) && s.ViceWindow)
                    foreach (int target in new[] { CardId.Distortion, CardId.Celebration, CardId.Creation })
                        if (s.Has(target, WZone.Grave))
                            yield return A(WKind.ViceRecover, CardId.ViceMadame, WZone.Board, target, 2);
                if (s.Has(CardId.MaidVerre, WZone.Hand) && s.SpellActivity && MainMonsters(s) < 5)
                    yield return A(WKind.MaidSummon, CardId.MaidVerre, WZone.Hand, 0, 0);
                if (s.Has(CardId.MaidGenni, WZone.Hand) && s.In(WZone.Board).Any(IsSpellcasterMaterial) &&
                    MainMonsters(s) < 5)
                    yield return A(WKind.MaidSummon, CardId.MaidGenni, WZone.Hand, 0, 0);
                if (s.Active(CardId.MaidGenni))
                    foreach (int material in s.In(WZone.Board).Where(IsSpellcasterMaterial))
                        foreach (int target in new[] { CardId.MaidVerre, CardId.Genni,
                            CardId.Marka, CardId.Haine, CardId.MadameVerre })
                            if (s.Has(target, WZone.Deck))
                            {
                                WAction a = A(WKind.MaidExchange, CardId.MaidGenni, WZone.Board, target, 1);
                                a.Materials = new[] { material }; a.MaterialZones = new[] { WZone.Board };
                                yield return a;
                            }
                if (s.Has(CardId.Unveiling, WZone.Hand) && MainMonsters(s) < 5)
                    foreach (int target in WitchMonsters)
                        if (s.Has(target, WZone.Hand))
                            yield return A(WKind.Unveiling, CardId.Unveiling, WZone.Hand, target);
                if (s.Has(CardId.Distortion, WZone.Hand) && !s.Has(CardId.Distortion, WZone.Backrow))
                    yield return A(WKind.SetTrap, CardId.Distortion, WZone.Hand);
            }

            internal static int MainMonsters(WState s)
            {
                return s.Size(WZone.Board) + s.OtherMonsters - (s.ExtraOccupant != 0 ? 1 : 0);
            }

            private static IEnumerable<Tuple<int, WZone>> Costs(WState s, int low)
            {
                int preferred = low == CardId.Genni ? CardId.Creation :
                    low == CardId.Schmietta ? CardId.Celebration :
                    low == CardId.Pittore ? CardId.Distortion : CardId.ConfusionConfession;
                int[] order = new[] { preferred, CardId.Bystreet, CardId.Creation,
                    CardId.Unveiling, CardId.Celebration, CardId.ConfusionConfession };
                if (!s.OpponentTurn && s.Has(CardId.Walpurgis, WZone.Backrow) && !s.Disabled.Contains(CardId.Walpurgis))
                    foreach (int id in order.Distinct())
                        if (s.Has(id, WZone.Deck)) yield return Tuple.Create(id, WZone.Deck);
                if (s.Has(CardId.Bystreet, WZone.Backrow) && !s.Disabled.Contains(CardId.Bystreet) &&
                    !s.Used.Contains(CardId.Bystreet + ":cost-or-recovery"))
                    yield return Tuple.Create(CardId.Bystreet, WZone.Backrow);
                foreach (int id in s.In(WZone.Hand).Where(IsModelSpell)
                    .OrderBy(id => Array.IndexOf(order, id) < 0 ? 100 : Array.IndexOf(order, id)))
                    yield return Tuple.Create(id, WZone.Hand);
            }

            internal static IEnumerable<WAction> Fusions(WState s)
            {
                foreach (int source in new[] { CardId.Terracottan, CardId.Pupils,
                    CardId.Celebration, CardId.ConfusionConfession })
                {
                    bool boardSource = source == CardId.Terracottan || source == CardId.Pupils;
                    if (boardSource ? !s.Active(source) : !s.Has(source, WZone.Hand)) continue;
                    bool recycle = source == CardId.Celebration || source == CardId.Pupils;
                    if (source == CardId.Pupils && !s.Has(CardId.Celebration, WZone.Hand)) continue;
                    WZone[] zones = recycle ? new[] { WZone.Grave, WZone.Banished } :
                        new[] { WZone.Board, WZone.Hand };
                    List<Tuple<int, WZone>> materials = zones.SelectMany(zone =>
                        s.In(zone).Where(IsSpellcasterMaterial).SelectMany(id =>
                            Enumerable.Repeat(Tuple.Create(id, zone), s.Count(id, zone)))).Take(16).ToList();
                    foreach (int target in new[] { CardId.MadameRillie, CardId.Pupils, CardId.ViceMadame })
                    {
                        if (!s.Has(target, WZone.Extra)) continue;
                        // Limit material candidates by preservation value; keep the documented pair
                        // available even if a boss is also a legal material.
                        IEnumerable<Tuple<int, WZone>[]> sets = MaterialSets(materials,
                            target == CardId.MadameRillie ? 3 : 2);
                        foreach (var set in sets)
                        {
                            if (target == CardId.MadameRillie ?
                                !set.Any(m => m.Item1 == CardId.MadameVerre) :
                                !set.Any(m => IsWitchMaterial(m.Item1))) continue;
                            int fieldConsumed = set.Count(m => m.Item2 == WZone.Board);
                            if (s.Size(WZone.Board) + s.OtherMonsters - fieldConsumed >= 6) continue;
                            WAction a = A(WKind.Fusion, source,
                                boardSource ? WZone.Board : WZone.Hand, target,
                                source == CardId.Terracottan ? 1 : source == CardId.Pupils ? 0 : -1);
                            a.Materials = set.Select(m => m.Item1).ToArray();
                            a.MaterialZones = set.Select(m => m.Item2).ToArray();
                            a.Route = source == CardId.Pupils ? "guide:copy Celebration" :
                                target == CardId.Pupils ? "guide:Pupils bridge" : "fusion alternatives";
                            yield return a;
                        }
                    }
                }
            }

            private static IEnumerable<Tuple<int, WZone>[]> MaterialSets(
                IList<Tuple<int, WZone>> cards, int count)
            {
                for (int i = 0; i < cards.Count; i++)
                    for (int j = i + 1; j < cards.Count; j++)
                    {
                        if (count == 2) yield return new[] { cards[i], cards[j] };
                        else for (int k = j + 1; k < cards.Count; k++)
                            yield return new[] { cards[i], cards[j], cards[k] };
                    }
            }

            public static WState Apply(WState state, WAction a)
            {
                if (!SummonAllowed(state, a)) throw new InvalidOperationException("Restricted summon: " + a);
                WState s = state.Copy();
                s.Used.Add(a.Usage);
                s.Steps++;
                s.Pending = 0;
                if (a.CopiedSpell != 0)
                {
                    s.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                    s.Move(a.CopiedSpell, WZone.Grave, WZone.Banished);
                }
                s.ViceWindow = a.Kind != WKind.Normal && a.Kind != WKind.SetTrap &&
                    a.Kind != WKind.Fusion && a.Source != CardId.ViceMadame &&
                    (IsModelSpell(a.Source) || IsSpellcasterMaterial(a.Source));
                if (a.Location == WZone.Hand && IsModelSpell(a.Source) &&
                    a.Kind != WKind.ActivateField && a.Kind != WKind.SetTrap && a.Kind != WKind.ActivateBystreet)
                {
                    s.Move(a.Source, WZone.Hand, WZone.Grave);
                    s.SpellActivity = true;
                }
                switch (a.Kind)
                {
                    case WKind.SeedRedraw:
                    case WKind.PittoreDraw:
                    case WKind.PotterieRecover:
                    case WKind.DistortionSearch:
                    case WKind.CopiedDraping:
                        ApplyGraveAction(s, a); break;
                    case WKind.FieldAccess:
                        if (a.Source == CardId.RainbowBridgeBifrost)
                            foreach (int id in new[] { CardId.CosmicQuasarDragon,
                                CardId.DespianLuluwalilith, CardId.ElfnoteJunePride })
                                s.Move(id, WZone.Extra, WZone.Banished);
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.Mastery:
                    case WKind.Creation:
                    case WKind.GazeSearch:
                    case WKind.MaidSearch:
                    case WKind.PupilsSearch:
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.NegateBoard:
                        s.Move(a.Cost, WZone.Hand, WZone.Grave);
                        s.OpposingResponses &= OpponentResponse.ConditionalFieldMonster;
                        foreach (WRestriction restriction in s.Restrictions.Where(r => r.Player == 1))
                            restriction.Active = false;
                        break;
                    case WKind.Marka:
                        s.Move(a.Source, a.Location, WZone.Grave);
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.ActivateField:
                        s.Move(a.Source, WZone.Hand, WZone.Backrow); s.SpellActivity = true; break;
                    case WKind.ActivateBystreet:
                        s.Move(a.Source, WZone.Hand, WZone.Backrow); s.SpellActivity = true;
                        s.OccupiedBackrow++;
                        if (s.CanEstablishShield)
                            s.FreshShields.UnionWith(s.In(WZone.Board).Where(id => IsWitchMaterial(id) && s.Count(id, WZone.Board) == 1));
                        s.CanEstablishShield = false;
                        break;
                    case WKind.CelebrationDestroy:
                        ApplyDestructionPair(s, a);
                        break;
                    case WKind.FieldSearch:
                        s.Move(a.Target, WZone.Deck, WZone.Hand);
                        s.Move(a.Cost, WZone.Hand, WZone.Grave); break;
                    case WKind.Normal:
                        s.Move(a.Source, WZone.Hand, WZone.Board); s.Normal = false; break;
                    case WKind.Recruit:
                        s.Move(a.Source, WZone.Board, WZone.Grave);
                        s.Move(a.Cost, a.CostZone, WZone.Grave);
                        s.Move(a.Target, WZone.Deck, WZone.Board); s.FutureSummons++; break;
                    case WKind.DarkSummon:
                        s.Move(CardId.Marka, WZone.Board, WZone.Banished);
                        s.Move(a.Source, WZone.Extra, WZone.Board);
                        if (s.ExtraOccupant == 0) s.ExtraOccupant = a.Source;
                        s.Pending = a.Source; s.FutureSummons++; break;
                    case WKind.Gaze:
                        s.Move(a.Materials[0], a.MaterialZones[0], WZone.Extra);
                        s.Move(a.Target, WZone.Extra, WZone.Board);
                        s.Temporary[a.Target] = s.Turn + 1;
                        s.FutureSummons++; break;
                    case WKind.GenniCopy:
                        s.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                        s.Move(CardId.Creation, WZone.Grave, WZone.Banished);
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.GenniLacrima:
                        s.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                        s.Move(CardId.Lacrima, WZone.Grave, WZone.Banished);
                        goto case WKind.LacrimaRecruit;
                    case WKind.LacrimaRecruit:
                        s.Move(a.Target, RecruitOrigin(s, a), WZone.Board);
                        if (a.Target == CardId.MaidVerre) s.Pending = a.Target;
                        s.FutureSummons++; break;
                    case WKind.LacrimaSend:
                        s.Move(a.Target, WZone.Deck, WZone.Grave); break;
                    case WKind.MaidExchange:
                        s.Move(a.Materials[0], WZone.Board, WZone.Banished);
                        s.Move(a.Target, WZone.Deck, WZone.Board);
                        if (a.Target == CardId.MaidVerre) s.Pending = a.Target;
                        s.FutureSummons++; break;
                    case WKind.Recover:
                        s.Move(a.Target, WZone.Grave, WZone.Hand);
                        s.Move(a.Source, WZone.Hand, WZone.Board);
                        s.ExtraLock = true; s.FutureSummons++; break;
                    case WKind.Send:
                        s.Move(a.Source, WZone.Grave, WZone.Banished);
                        s.Move(a.Target, WZone.Deck, WZone.Grave); break;
                    case WKind.Fusion:
                        bool recycle = a.Source == CardId.Pupils || a.Source == CardId.Celebration || a.CopiedSpell == CardId.Celebration;
                        for (int i = 0; i < a.Materials.Length; i++)
                            s.Move(a.Materials[i], a.MaterialZones[i],
                                recycle ? (IsExtra(a.Materials[i]) ? WZone.Extra : WZone.Deck) : WZone.Grave);
                        s.Move(a.Target, WZone.Extra, WZone.Board);
                        if (s.ExtraOccupant == 0) s.ExtraOccupant = a.Target;
                        if (a.Target == CardId.MadameRillie) s.Pending = a.Target;
                        s.FutureSummons++; break;
                    case WKind.RillieRecruit:
                        foreach (int id in a.Recruits)
                            s.Move(id, s.Has(id, WZone.Hand) ? WZone.Hand : WZone.Deck, WZone.Board);
                        s.FutureSummons++; break; // simultaneous summon = one Maxx C draw
                    case WKind.EdelRevive:
                        s.Move(a.Source, WZone.Board, WZone.Grave);
                        s.Move(a.Target, WZone.Grave, WZone.Board); s.FutureSummons++; break;
                    case WKind.ViceRecover:
                        s.Move(a.Target, WZone.Grave, WZone.Hand); break;
                    case WKind.MaidSummon:
                        s.Move(a.Source, WZone.Hand, WZone.Board);
                        if (a.Source == CardId.MaidVerre) s.Pending = CardId.MaidVerre;
                        s.FutureSummons++; break;
                    case WKind.Unveiling:
                        s.Move(a.Target, WZone.Hand, WZone.Board); s.FutureSummons++;
                        s.SpellcasterProtected = true; break;
                    case WKind.SetTrap:
                        s.Move(a.Source, WZone.Hand, WZone.Backrow); break;
                }
                if (IsSummon(a))
                    foreach (WRestriction restriction in s.Restrictions.Where(r => r.SummonLimit > 0))
                        restriction.Used++;
                MarkSharedUsage(s.Used, a);
                if (a.Source == CardId.Pupils && (a.Kind == WKind.Fusion || a.Kind == WKind.PupilsSearch))
                {
                    s.Used.Add(CardId.Pupils + ":" + WKind.Fusion);
                    s.Used.Add(CardId.Pupils + ":" + WKind.PupilsSearch);
                }
                if ((a.Kind == WKind.Recruit || a.Kind == WKind.EdelRevive || a.Kind == WKind.Unveiling) &&
                    a.Target == CardId.MaidVerre) s.Pending = a.Target;
                // Copied spell shares Genni's once-per-turn with the Creation copy.
                if (a.Kind == WKind.GenniCopy || a.Kind == WKind.GenniLacrima)
                {
                    s.Used.Add(CardId.Genni + ":" + WKind.GenniCopy);
                    s.Used.Add(CardId.Genni + ":" + WKind.GenniLacrima);
                }
                s.EnemyChain = false; // a speculative next action cannot reuse this response window
                AssessRouteExposure(state, a, s);
                return s;
            }

            private static bool IsExtra(int id)
            {
                return id == CardId.Pupils || id == CardId.ViceMadame ||
                    id == CardId.MadameRillie || id == CardId.RedEyesDarkDragoon ||
                    id == CardId.DarkMagicianOfDestruction;
            }

            private static double Potential(WState s)
            {
                // Route-book milestones guide beam survival but never inflate terminal quality.
                double p = 0;
                if (s.Has(CardId.Walpurgis, WZone.Backrow)) p += 12;
                if (s.Active(CardId.Genni)) p += 8;
                if (s.Has(CardId.Genni, WZone.Grave) && s.Has(CardId.Creation, WZone.Grave)) p += 15;
                if (s.Has(CardId.Celebration, WZone.Grave)) p += 8;
                if (s.Active(CardId.Marka) && !s.ExtraLock) p += 20;
                if (s.Active(CardId.DarkMagicianOfDestruction)) p += 30;
                if (s.Has(CardId.GazeOfTimaeus, WZone.Hand)) p += 20;
                if (s.Active(CardId.Terracottan) && s.Active(CardId.RedEyesDarkDragoon)) p += 35;
                if (s.Active(CardId.Pupils) && s.Has(CardId.Celebration, WZone.Hand)) p += 40;
                if (s.Pending != 0) p += 30;
                return s.Maxx ? p * .2 : p;
            }

            public static double Evaluate(WState s)
            {
                double value = UninterruptedValue(s);
                // One adversarial denial anywhere in the supported route, not one
                // imaginary negate per edge. The 45% sensitivity blend is NOT odds.
                if (s.DenialFloor.HasValue) value -= .45 * Math.Max(0, value - s.DenialFloor.Value);
                return value - s.CommitmentCost;
            }

            internal static double UninterruptedValue(WState s)
            {
                // One defended turn plus the following recovery board, not a permanent
                // boss valuation for a card whose installed script schedules its exit.
                if (s.Temporary.Count > 0)
                {
                    WState present = s.Copy(); present.Temporary.Clear();
                    WState future = ProjectDelayed(s, s.Turn + 1); future.Temporary.Clear();
                    return (UninterruptedValue(present) + UninterruptedValue(future)) / 2;
                }
                int spells = s.In(WZone.Hand).Where(IsModelSpell).Sum(id => s.Count(id, WZone.Hand));
                bool witchBoard = s.In(WZone.Board).Any(IsWitchMaterial);
                // Evaluate usable interaction after the upcoming End Phase, including
                // the named recovery restrictions. Genni's copy does not spend Creation.
                int recoverable = witchBoard && !s.OpponentTurn ? new[] { CardId.Creation, CardId.ConfusionConfession,
                    CardId.Unveiling }.Count(id => s.Has(id, WZone.Grave) &&
                    !s.Used.Contains(id + ":" + (id == CardId.Creation ? WKind.Creation :
                    id == CardId.Unveiling ? WKind.Unveiling : WKind.Fusion))) : 0;
                bool cost = spells + recoverable > 0 || s.Has(CardId.Bystreet, WZone.Backrow);
                double score = s.Size(WZone.Hand) * 2 + s.UnknownHand * 2 + recoverable * 2 + s.RemovedThreat + s.DrawUtility;
                foreach (int id in s.In(WZone.Board))
                {
                    double value = 3;
                    if (id == CardId.RedEyesDarkDragoon)
                        value = s.Size(WZone.Hand) + s.UnknownHand > 0 ? 40 : 18;
                    if (id == CardId.Haine) value = cost ? 23 : 9;
                    if (id == CardId.MadameVerre) value = cost ? 24 : 10;
                    if (id == CardId.ViceMadame) value = 32;
                    if (id == CardId.Pupils) value = 16;
                    if (id == CardId.MadameRillie) value = 8;
                    if (id == CardId.MaidGenni) value = 14;
                    if (id == CardId.Edel && s.Has(CardId.RedEyesDarkDragoon, WZone.Grave)) value = 8;
                    if (s.Disabled.Contains(id)) value = 2;
                    // Multiple copies share hard once-per-name interaction limits.
                    // Count extra bodies, not another full set of negates/recoveries.
                    score += value + 3 * (s.Count(id, WZone.Board) - 1);
                }
                bool highWitch = s.In(WZone.Board).Any(id => new[]
                {
                    CardId.Marka, CardId.Haine, CardId.MadameVerre, CardId.Edel,
                    CardId.Pupils, CardId.MadameRillie, CardId.ViceMadame
                }.Contains(id));
                if (s.Has(CardId.Distortion, WZone.Backrow)) score += highWitch ? 25 : 1;
                if (s.Has(CardId.Walpurgis, WZone.Backrow)) score += 3;
                // A shield preserves a real body once, not immunity or another negate.
                // Existing counters are unobservable: only freshly established shields
                // earn this value; unverified shields never justify a free sacrifice.
                if (s.Has(CardId.Bystreet, WZone.Backrow) && !s.Disabled.Contains(CardId.Bystreet))
                    score += s.FreshShields.Count(id => s.Has(id, WZone.Board)) * 3;
                foreach (int id in WitchSpells.Where(id => s.Has(id, WZone.Grave))) score += 1;
                if (s.Active(CardId.Pupils) && s.Has(CardId.ConfusionConfession, WZone.Hand)) score += 5;
                // Public damage pressure makes a durable stop materially different from
                // another combo body. Reward a defense upgrade once, never per summon.
                // This is a survival heuristic, not a simulated enemy battle sequence.
                if (s.IncomingAttack >= s.LifePoints && s.LifePoints > 0 &&
                    (s.Active(CardId.MadameVerre) || s.Active(CardId.RedEyesDarkDragoon) ||
                    (s.Active(CardId.Haine) && cost))) score += 18;
                score -= s.FutureSummons * (s.Maxx ? 21 : .15);
                score -= s.Steps * .3;
                // A board waiting on an optional trigger is not a completed endpoint.
                if (s.Pending != 0) score -= 15;
                return score;
            }

            public static string BoardSummary(WState s)
            {
                return string.Join(",", s.In(WZone.Board)) + ";hand=" +
                    s.Size(WZone.Hand) + ";draws=" + s.FutureSummons +
                    ";visible-response=" + s.OpposingResponses + ";denial-floor=" + s.DenialFloor +
                    ";vulnerable=" + s.Vulnerability + ";commitment=" + s.CommitmentCost.ToString("0.0");
            }
        }

        #endregion

        #region Tactical decisions

        // Deliberately bounded, public-information forecast. Unknown effects stay unknown;
        // this is not an invocation of the engine or a universal card-effect interpreter.
        internal sealed class WChainForecast
        {
            public readonly HashSet<ClientCard> Removed = new HashSet<ClientCard>();
            public readonly HashSet<ClientCard> Disabled = new HashSet<ClientCard>();
            public readonly HashSet<int> Negated = new HashSet<int>();
            public readonly List<string> Trace = new List<string>();
            public readonly HashSet<ClientCard> GraveFollowups = new HashSet<ClientCard>();
        }

        private string _lastTacticalDiagnostic;
        private ClientCard _viceRemoval;

        internal static WChainForecast ForecastChain(IList<ChainInfo> links, IEnumerable<int> negated,
            ClientCard viceRemoval, IEnumerable<ClientCard> publicMonsters = null)
        {
            WChainForecast f = new WChainForecast();
            OpponentAwareness semantics = new OpponentAwareness();
            foreach (int index in negated) f.Negated.Add(index - 1);
            for (int i = links.Count - 1; i >= 0; i--)
            {
                ChainInfo link = links[i];
                ClientCard source = link.RelatedCard;
                if (f.Negated.Contains(i)) { f.Trace.Add(i + ":negated"); continue; }
                OpponentEffect known = semantics.Identify(link);
                if (known != null && known.RequiresFaceup && source != null &&
                    (f.Removed.Contains(source) || !source.IsFaceup() || source.Location != link.ActivateLocation))
                { f.Trace.Add(i + ":reviewed source-presence dependency fails"); continue; }
                if (link.ActivateLocation == CardLocation.MonsterZone && source != null &&
                    (f.Disabled.Contains(source) || (source.Location == CardLocation.MonsterZone && source.IsDisabled())))
                { f.Trace.Add(i + ":disabled"); continue; }
                if ((link.ActivateId == CardId.RedEyesDarkDragoon &&
                    link.ActivateDescription == UtilString(CardId.RedEyesDarkDragoon, 1)) ||
                    (link.ActivateId == CardId.Distortion && link.ActivateLocation == CardLocation.SpellZone) ||
                    link.ActivateId == CardId.AshBlossom)
                {
                    if (i > 0)
                    {
                        f.Negated.Add(i - 1);
                        ClientCard destroyed = links[i - 1].RelatedCard;
                        if (link.ActivateId != CardId.AshBlossom && destroyed != null &&
                            (destroyed.Location == CardLocation.MonsterZone || destroyed.Location == CardLocation.SpellZone ||
                            destroyed.Location == CardLocation.Hand) &&
                            !DestructionProtected(destroyed, link.ActivatePlayer, f.Disabled.Contains(destroyed)))
                        {
                            f.Removed.Add(destroyed);
                            if (HasGraveFusionFollowup(destroyed)) f.GraveFollowups.Add(destroyed);
                        }
                    }
                    f.Trace.Add(i + ":negate preceding link");
                }
                else if (link.ActivateId == CardId.Haine || link.ActivateId == CardId.Seed ||
                    (link.ActivateId == CardId.MaidGenni && link.ActivateDescription == UtilString(CardId.MaidGenni, 1)))
                {
                    foreach (ClientCard target in link.Targets)
                    {
                        if (f.Removed.Contains(target) || (target.Location != CardLocation.MonsterZone &&
                            target.Location != CardLocation.SpellZone))
                        { f.Trace.Add(i + ":target already absent"); continue; }
                        if (link.ActivateId == CardId.Haine && DestructionProtected(target, link.ActivatePlayer, f.Disabled.Contains(target)))
                        { f.Trace.Add(i + ":destruction uncertain/protected"); continue; }
                        f.Removed.Add(target);
                        f.Trace.Add(i + ":remove " + target.Id);
                    }
                }
                else if (link.ActivateId == CardId.ViceMadame &&
                    link.ActivateDescription == UtilString(CardId.ViceMadame, 0) && viceRemoval != null)
                {
                    if (!DestructionProtected(viceRemoval, link.ActivatePlayer, f.Disabled.Contains(viceRemoval))) f.Removed.Add(viceRemoval);
                    f.Trace.Add(i + ":resolution-choice removal " + viceRemoval.Id);
                }
                else if (link.ActivateId == CardId.MadameVerre &&
                    link.ActivateDescription == UtilString(CardId.MadameVerre, 1))
                {
                    foreach (ClientCard target in publicMonsters ?? Enumerable.Empty<ClientCard>())
                        if (target.Controller != link.ActivatePlayer && target.IsFaceup() &&
                            target.Location == CardLocation.MonsterZone && !f.Removed.Contains(target)) f.Disabled.Add(target);
                    f.Trace.Add(i + ":negate currently surviving opposing monsters");
                }
                else if (link.ActivateId == 98338152) // Widow Anchor: removing its spell is NOT negation.
                {
                    foreach (ClientCard target in link.Targets)
                        if (!f.Removed.Contains(target) && target.Location == CardLocation.MonsterZone)
                            f.Disabled.Add(target);
                    f.Trace.Add(i + ":Anchor checks surviving monster, not spell presence");
                }
                else f.Trace.Add(i + ":" + (known == null ? "unknown/other outcome " + link.ActivateId :
                    known.Name + (known.RequiresFaceup ? "/requires face-up source" : "/removal alone does not negate")));
            }
            return f;
        }

        private WChainForecast ChainForecast()
        {
            _opponent.Observe(Duel.CurrentChainInfo);
            if (Duel.SolvingChainIndex > 0)
                return ForecastChain(Duel.CurrentChainInfo.Take(Duel.SolvingChainIndex - 1).ToList(),
                    Duel.NegatedChainIndexList, null, Bot.GetMonsters().Concat(Enemy.GetMonsters()));
            return ForecastChain(Duel.CurrentChainInfo, Duel.NegatedChainIndexList, _viceRemoval,
                Bot.GetMonsters().Concat(Enemy.GetMonsters()));
        }

        private bool TacticalDecision(string action, double gain, double cost, string reason, ClientCard target = null)
        {
            bool accept = gain > cost;
            string text = "Witchcraft2026 tactic: " + action + " target=" + (target == null ? 0 : target.Id) +
                " pass=0 gain=" + gain.ToString("0.0") + " cost/opportunity=" + cost.ToString("0.0") +
                " choose=" + (accept ? action : "Pass") + " reason=" + reason +
                " chain=" + string.Join(",", Duel.CurrentChainInfo.Select((c, i) =>
                    (i + 1) + ":" + c.ActivatePlayer + "/" + c.ActivateId + "/targets=" +
                    string.Join("+", c.Targets.Select(t => t.Id)))) +
                " reverse=" + string.Join(";", ChainForecast().Trace);
            if (text != _lastTacticalDiagnostic) Logger.DebugWriteLine(text);
            _lastTacticalDiagnostic = text;
            return accept;
        }

        private double RemovalValue(ClientCard target, bool targeting)
        {
            if (target == null || target.Controller != 1 || KnownDestructionProtected(target) ||
                (targeting && (target.IsFacedown() || target.IsShouldNotBeTarget() || target.IsShouldNotBeMonsterTarget()))) return 0;
            WChainForecast forecast = ChainForecast();
            if (forecast.Removed.Contains(target)) return 0;
            // A resolving normal/quick-play spell will go to GY anyway, and destruction
            // does not stop its effect. Continuous/field/equip spells are different.
            if (Duel.CurrentChainInfo.Any(c => ReferenceEquals(c.RelatedCard, target)) &&
                target.IsSpell() && !target.HasType(CardType.Continuous) &&
                !target.HasType(CardType.Field) && !target.HasType(CardType.Equip)) return 0;
            OpponentThreat threat = OpponentAssessment(target, forecast);
            // Floodgate restriction profiles are separately verified by Outcomes.
            double restriction = target.IsCode(SummonLimiter, SpellSummonLimiter) && !target.IsDisabled() &&
                !forecast.Disabled.Contains(target) ? 20 : 0;
            return Math.Max(0, threat.Removal + restriction);
        }

        private OpponentThreat OpponentAssessment(ClientCard target, WChainForecast forecast)
        {
            IList<ChainInfo> links = Duel.SolvingChainIndex > 0 ?
                Duel.CurrentChainInfo.Take(Duel.SolvingChainIndex - 1).ToList() : Duel.CurrentChainInfo;
            return _opponent.Assess(target, links, forecast.Negated,
                forecast.Disabled.Contains(target), Duel.Player == target.Controller);
        }

        private double InterruptionCost(double baseCost)
        {
            int spells = Bot.Hand.Count(c => c.IsSpell());
            // Last Spell also funds the remaining Verre/Haine defense. This is an
            // opportunity cost, not knowledge of an unseen future activation.
            return baseCost + (spells <= 1 ? 4 : 0);
        }

        private bool WorthVerre()
        {
            WChainForecast forecast = ChainForecast();
            double gain = 0;
            List<string> reasons = new List<string>();
            foreach (ClientCard monster in Enemy.GetMonsters().Where(c => c.IsFaceup() && !c.IsDisabled() &&
                !forecast.Removed.Contains(c) && !forecast.Disabled.Contains(c)))
            {
                OpponentThreat threat = OpponentAssessment(monster, forecast);
                double live = threat.NegateNow;
                if (!threat.Supported && Duel.CurrentChainInfo.Select((c, i) => new { c, i }).Any(x =>
                    ReferenceEquals(x.c.RelatedCard, monster) && !forecast.Negated.Contains(x.i))) live = 12;
                gain += live + threat.Future * .25; // temporary disable, not permanent removal
                reasons.Add(monster.Id + ":" + threat.Explain());
            }
            return TacticalDecision("Verre", gain, InterruptionCost(7),
                "only surviving face-up monsters; spell link is not negated; reserve OPT vs " + string.Join(" | ", reasons.Take(5)));
        }

        private static bool KnownDestructionProtected(ClientCard card)
        {
            // Battle invincibility helpers do not imply effect-destruction immunity.
            return DestructionProtected(card, 0, false);
        }

        private static bool HasGraveFusionFollowup(ClientCard card)
        {
            return card != null && card.Id == GraveFusionTrigger && card.Location != CardLocation.Grave &&
                card.Location != CardLocation.Removed && card.Location != CardLocation.Deck &&
                card.Location != CardLocation.Extra;
        }

        private ClientCard RemovalTarget(bool targeting)
        {
            var candidates = Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Select(c => new { Card = c, Value = RemovalValue(c, targeting) })
                .OrderByDescending(c => c.Value).ToList();
            ClientCard selected = candidates.Where(c => c.Value > 0).Select(c => c.Card).FirstOrDefault();
            WChainForecast forecast = ChainForecast();
            string diagnostic = "Witchcraft2026 opponent targets: " + string.Join(" | ", candidates.Take(8).Select(c =>
                c.Card.Id + " value=" + c.Value.ToString("0.0") + " " + OpponentAssessment(c.Card, forecast).Explain())) +
                " selected=" + (selected == null ? 0 : selected.Id) + " method=largest-legal-marginal-removal; pending removals/protection excluded";
            if (_lastOpponentDiagnostic != diagnostic) Logger.DebugWriteLine(diagnostic);
            _lastOpponentDiagnostic = diagnostic;
            return selected;
        }

        private string _lastOpponentDiagnostic;

        private bool ValuableNegate(string name, double cost)
        {
            if (Duel.LastChainPlayer != 1 || Duel.CurrentChainInfo.Count == 0) return false;
            ChainInfo threat = Duel.CurrentChainInfo.Last();
            WChainForecast forecast = ChainForecast();
            double gain = 12; // unknown live effect: one interaction, never assumed a board wipe
            OpponentEffect semantics = _opponent.Identify(threat);
            if (semantics != null) gain = semantics.Utility;
            if (forecast.Negated.Contains(Duel.CurrentChainInfo.Count - 1)) gain = 0;
            else if (threat.ActivateLocation == CardLocation.MonsterZone && threat.RelatedCard != null &&
                (threat.RelatedCard.IsDisabled() || forecast.Disabled.Contains(threat.RelatedCard))) gain = 0;
            else if (threat.ActivateId == 98338152 && threat.Targets.Count > 0 &&
                threat.Targets.All(t => forecast.Removed.Contains(t) || t.Location != CardLocation.MonsterZone)) gain = 0;
            else if (threat.ActivateId == CardId.AshBlossom)
            {
                ChainInfo protectedLink = Duel.CurrentChainInfo.Count > 1 ?
                    Duel.CurrentChainInfo[Duel.CurrentChainInfo.Count - 2] : null;
                // A search on an established board is optional advantage. An opening
                // search with no functioning boss is an engine survival requirement.
                bool established = Bot.GetMonsters().Count(c => c.IsFaceup() && !c.IsDisabled() &&
                    c.IsCode(CardId.RedEyesDarkDragoon, CardId.ViceMadame, CardId.Haine, CardId.MadameVerre)) >= 2;
                gain = protectedLink != null && protectedLink.ActivatePlayer == 0 ? (established ? 3 : 24) : 0;
            }
            else if (threat.ActivateId == 76072561 && threat.ActivateDescription == UtilString(76072561, 1))
                gain = 30; // verified Zero: two recruits plus removal, after tribute already paid
            else if (threat.Targets.Any(t => t.Controller == 0 && !forecast.Removed.Contains(t)))
                gain = 24;
            double reserve = Enemy.GetMonsters().Where(c => c.IsFaceup() && !c.IsDisabled() &&
                !forecast.Removed.Contains(c)).Select(c => OpponentAssessment(c, forecast).Future * .25).DefaultIfEmpty(0).Max();
            // Destroying the known hand activation by EFFECT sends it to GY and
            // enables a new Fusion trigger. Its materials/Extra are hidden: do not
            // claim Winda is guaranteed. With no identified urgent protected target,
            // retain this negate for the actual GY offer instead of trading it for
            // another live activation. A higher-priority observed threat can override.
            if (HasGraveFusionFollowup(threat.RelatedCard) && !_graveFusionSpent.Contains(threat.ActivateId) && gain <= 12)
                return TacticalDecision(name, 0, cost + reserve,
                    "negate+destroy -> GY by effect -> possible Fusion trigger; hidden materials unknown; reserve for actual follow-up");
            return TacticalDecision(name, gain, cost + reserve, "live effect=" +
                (semantics == null ? "unsupported/uncertain" : semantics.Name) +
                "; committed activation != source removal; future reserve=" + reserve.ToString("0.0"));
        }

        private bool QuickResourceAction()
        {
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            bool lacrima = Card.Id == CardId.Lacrima && Duel.LastChainPlayer == 1;
            bool low = LowIds.Contains(Card.Id) && Card.Location == CardLocation.MonsterZone &&
                ActivateDescription == UtilString(Card.Id, 0);
            bool maid = Card.Id == CardId.MaidGenni && Card.Location == CardLocation.MonsterZone &&
                ActivateDescription == UtilString(Card.Id, 1);
            bool fieldThreat = Duel.LastChainPlayer == 1 && Duel.CurrentChainInfo.Count > 0 &&
                Duel.CurrentChainInfo.Last().Targets.Any(t => t.Controller == 0 && t.Id == CardId.Walpurgis);
            if (!lacrima && !(Duel.Player == 1 && (low || maid)) && !(low && fieldThreat)) return false;
            WState state = Observe();
            // Quick responses compare only executable consequences, not an imagined
            // own Main Phase after a hostile chain. Costs and material loss are included.
            WAction best = null;
            double delta = 0;
            List<string> choices = new List<string> { "Pass=0 (retain current board/resources)" };
            foreach (WAction action in WPlanner.Actions(state).Where(a => Matches(a, Card, ActivateDescription)))
            {
                if (lacrima && action.Kind != WKind.LacrimaRecruit) continue;
                WState after = WPlanner.Apply(state, action);
                // A resolved Maid Verre summon offers its real search trigger next.
                if (after.Pending == CardId.MaidVerre)
                {
                    WAction search = WPlanner.Actions(after).FirstOrDefault(a => a.Kind == WKind.MaidSearch);
                    if (search != null) after = WPlanner.Apply(after, search);
                }
                double value = WPlanner.Evaluate(after) - WPlanner.Evaluate(state);
                choices.Add(action + " delta=" + value.ToString("0.0") + " endpoint=" + WPlanner.BoardSummary(after));
                if (value > delta) { delta = value; best = action; }
            }
            if (!TacticalDecision("quick-resource", delta, .01,
                (best == null ? "no improvement over retaining board" : best.ToString()) +
                " candidates=" + string.Join(" | ", choices.Take(5)) + " total=" + choices.Count)) return false;
            AcceptAction(best);
            return true;
        }

        private bool TryBattleForLethal()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 || Duel.MainPhase == null ||
                !Duel.MainPhase.CanBattlePhase || Duel.CurrentChain.Count != 0) return false;
            // Conservative empty-field case only. No assumed removal of blockers,
            // no attacks from defense position, no extra attacks, no unknown backrow.
            int damage = Bot.GetMonsters().Where(c => c.IsFaceup() && c.IsAttack() && !c.Attacked &&
                IsSpellcasterMaterial(c.Id)).Sum(c => Math.Max(0, c.Attack));
            bool clear = Enemy.GetMonsters().Count == 0 && Enemy.GetSpells().Count == 0;
            bool lethal = clear && damage >= Enemy.LifePoints && Enemy.LifePoints > 0;
            if (lethal) Logger.DebugWriteLine("Witchcraft2026 lethal: potential=" + damage +
                " LP=" + Enemy.LifePoints + " empty public field; enter Battle, engine attack offers remain authoritative; hidden responses unknown");
            return lethal;
        }

        public override BattlePhaseAction OnBattle(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            // Unlike the Main Phase forecast these are authoritative attack offers.
            // Recompute after every attack/response; never execute a cached attack list.
            if (defenders.Count == 0 && Enemy.GetSpells().Count == 0)
            {
                List<ClientCard> direct = attackers.Where(c => c.CanDirectAttack && !c.Attacked)
                    .OrderByDescending(c => c.Attack).ToList();
                if (direct.Count > 0 && direct.Sum(c => Math.Max(0, c.Attack)) >= Enemy.LifePoints)
                {
                    Logger.DebugWriteLine("Witchcraft2026 lethal: engine-offered direct attack=" +
                        direct[0].Id + " attack=" + direct[0].Attack + " remainingLP=" + Enemy.LifePoints);
                    return AI.Attack(direct[0], null);
                }
            }
            return base.OnBattle(attackers, defenders);
        }

        internal double EqualTradeValue(ClientCard attacker, ClientCard defender)
        {
            WState before = Observe();
            if (!before.Has(attacker.Id, WZone.Board)) return double.NegativeInfinity;
            WState after = before.Copy(); after.Move(attacker.Id, WZone.Board, WZone.Grave);
            WRestriction removed = after.Restrictions.FirstOrDefault(r => r.Id == defender.Id && r.Player == 1);
            if (removed != null) after.Restrictions.Remove(removed);
            after.IncomingAttack = Math.Max(0, after.IncomingAttack - defender.Attack);
            // Compare actual modeled continuations, not a blanket floodgate bonus.
            double preserved, traded;
            using (var budget = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token))
            {
                budget.CancelAfter(180); // one budget for both sides of this comparison
                preserved = WPlanner.Plan(before, null, budget.Token).Score;
                traded = WPlanner.Plan(after, null, budget.Token).Score;
            }
            bool recovers = defender.HasSetcode(0x9d) && Enemy.Graveyard.Any(c =>
                c.HasSetcode(0x9d) && (c.IsSpell() || c.IsTrap()));
            return traded - preserved + 10 + Math.Max(0, defender.Attack) / 500.0 - (recovers ? 2 : 0);
        }

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (!base.OnPreBattleBetween(attacker, defender)) return false;
            if (defender.IsFaceup() && defender.IsAttack() && attacker.RealPower == defender.RealPower &&
                attacker.RealPower > 0 && IsSpellcasterMaterial(attacker.Id))
            {
                double value = EqualTradeValue(attacker, defender);
                return TacticalDecision("equal-ATK trade", value, 0,
                    "both bodies lost; public GY recovery and legal Main2 continuation vs preserve; unexposed protection not assumed", defender);
            }
            return true;
        }

        #endregion

        #region Effect outcomes

        // Semantic profiles from the selected MDPro4 script.zip, not card-name or
        // keyword guesses. Unknown effects remain unknown. This is not an engine.
        private const int SummonLimiter = 94977269;
        private const int SpellSummonLimiter = 19261966;
        private const int GraveFusionTrigger = 572850;

        internal sealed class WRestriction
        {
            public int Id, Player, Used, SummonLimit;
            public bool Active, SpellHandGrave;
            public WRestriction Copy() { return (WRestriction)MemberwiseClone(); }
        }

        private readonly Dictionary<ClientCard, int> _limitCounts = new Dictionary<ClientCard, int>();
        private readonly Dictionary<ClientCard, int> _expires = new Dictionary<ClientCard, int>();
        private readonly HashSet<ClientCard> _summonLimitSources = new HashSet<ClientCard>();
        private readonly List<WPrediction> _predictions = new List<WPrediction>();
        private readonly HashSet<int> _graveFusionSpent = new HashSet<int>();

        private sealed class WPrediction
        {
            public WAction Action;
            public WState Before, After;
        }

        internal static bool IsSummon(WAction a)
        {
            return a.Kind == WKind.Recruit || a.Kind == WKind.DarkSummon || a.Kind == WKind.Gaze ||
                a.Kind == WKind.GenniLacrima || a.Kind == WKind.LacrimaRecruit || a.Kind == WKind.MaidExchange ||
                a.Kind == WKind.Recover || a.Kind == WKind.Fusion || a.Kind == WKind.RillieRecruit ||
                a.Kind == WKind.EdelRevive || a.Kind == WKind.MaidSummon || a.Kind == WKind.Unveiling;
        }

        internal static WZone RecruitOrigin(WState s, WAction a)
        {
            // A copied spell resolves as a monster effect. The same printed spell
            // activated directly has a different restriction profile.
            bool blockedHand = a.Kind == WKind.LacrimaRecruit &&
                s.Restrictions.Any(r => r.Active && r.SpellHandGrave);
            return !blockedHand && s.Has(a.Target, WZone.Hand) ? WZone.Hand : WZone.Deck;
        }

        internal static bool SummonAllowed(WState s, WAction a)
        {
            if (!IsSummon(a)) return true;
            if (s.Restrictions.Any(r => r.Active && r.SummonLimit > 0 && r.Used >= r.SummonLimit)) return false;
            if (!s.Restrictions.Any(r => r.Active && r.SpellHandGrave)) return true;
            if (a.Kind == WKind.Unveiling && a.CopiedSpell == 0) return false;
            if (a.Kind == WKind.LacrimaRecruit) return s.Has(a.Target, WZone.Deck);
            // Fusion/Gaze summon FROM Extra; materials in hand/GY are not summon origins.
            // Recruit, Edel, Rillie, Genni, Terra and Maid are monster effects.
            return true;
        }

        internal static string RestrictionSummary(WState s)
        {
            return string.Join(",", s.Restrictions.Select(r => r.Id + "/p" + r.Player +
                "/active=" + r.Active + "/used=" + r.Used + "/limit=" + r.SummonLimit +
                "/spell-hand-gy=" + r.SpellHandGrave));
        }

        private void ObserveOutcomes(WState s)
        {
            s.Turn = Duel.Turn;
            foreach (ClientCard card in Bot.GetMonsters().Concat(Enemy.GetMonsters()).Where(c => c.IsFaceup()))
            {
                if (card.Id == SummonLimiter || card.Id == SpellSummonLimiter)
                {
                    int used;
                    _limitCounts.TryGetValue(card, out used);
                    s.Restrictions.Add(new WRestriction { Id = card.Id, Player = card.Controller,
                        Active = !card.IsDisabled(), Used = used, SummonLimit = card.Id == SummonLimiter ? 1 : 0,
                        SpellHandGrave = card.Id == SpellSummonLimiter });
                }
                int expires;
                if (card.Controller == 0 && _expires.TryGetValue(card, out expires)) s.Temporary[card.Id] = expires;
            }
        }

        public override void OnSpSummoning()
        {
            // Capture existing limit sources before the summon completes. A newly
            // entering Winda does not retroactively count summons made before it.
            foreach (ClientCard card in Bot.GetMonsters().Concat(Enemy.GetMonsters()))
                if (card.Id == SummonLimiter && card.IsFaceup() && !Duel.SummoningCards.Contains(card))
                    _summonLimitSources.Add(card);
            base.OnSpSummoning();
        }

        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            _freshBystreetShields.Remove(card);
            if ((currentPosition & (int)CardPosition.FaceDown) != 0) _opponent.ResetInstance(card);
            if ((currentPosition & (int)CardPosition.FaceDown) != 0)
            {
                _markaRevived.Remove(card);
                _limitCounts.Remove(card);
                _summonLimitSources.Remove(card);
                _expires.Remove(card);
            }
            Invalidate();
            base.OnPosChange(card, previousPosition, currentPosition);
        }

        private void SummonOutcomes()
        {
            if (Duel.LastSummonPlayer == 0)
            {
                foreach (ClientCard card in _summonLimitSources)
                {
                    int used; _limitCounts.TryGetValue(card, out used);
                    _limitCounts[card] = used + 1; // simultaneous group = one event
                }
                ChainInfo resolving = Duel.GetCurrentSolvingChainInfo();
                WAction action = PromptAction();
                if (resolving != null && resolving.ActivatePlayer == 0 &&
                    (resolving.ActivateId == CardId.Unveiling ||
                    (resolving.ActivateId == CardId.Genni && action != null && action.CopiedSpell == CardId.Unveiling)))
                    _unveilingProtection = true;
                if (resolving != null && resolving.ActivatePlayer == 0 && resolving.ActivateId == CardId.GazeOfTimaeus)
                    foreach (ClientCard card in Duel.LastSummonedCards.Where(c => c.Controller == 0))
                        _expires[card] = Duel.Turn + 1;
            }
            _summonLimitSources.Clear();
        }

        internal static WState ProjectDelayed(WState source, int endTurn)
        {
            WState result = source.Copy();
            foreach (var pair in source.Temporary.Where(p => p.Value <= endTurn))
                if (result.Has(pair.Key, WZone.Board)) result.Move(pair.Key, WZone.Board, WZone.Banished);
            return result;
        }

        // sourcePlayer and negation at the moment of resolution are essential:
        // Winda resists the opponent, not its controller, and Verre BELOW Vice has
        // not resolved yet. Do not feed a whole-chain endpoint into this predicate.
        internal static bool DestructionProtected(ClientCard card, int sourcePlayer, bool negated)
        {
            if (card == null || card.Location != CardLocation.MonsterZone || !card.IsFaceup() ||
                card.IsDisabled() || negated) return false;
            return card.Id == CardId.RedEyesDarkDragoon ||
                (card.Id == SummonLimiter && card.Controller != sourcePlayer);
        }

        private double FriendlyDestructionLoss(ClientCard card)
        {
            if (DestructionProtected(card, 0, false)) return 0;
            WState before = Observe();
            if (!before.Has(card.Id, Zone(card.Location))) return 3;
            WState after = before.Copy(); after.Move(card.Id, Zone(card.Location), WZone.Grave);
            // Bystreet's per-card prevention counter is not exposed by this client.
            // Do not assume that its once-per-turn shield remains unused.
            return Math.Max(0, WPlanner.Evaluate(before) - WPlanner.Evaluate(after));
        }

        internal double DestructionPairValue(ClientCard own, ClientCard enemy)
        {
            return RemovalValue(enemy, false) - FriendlyDestructionLoss(own);
        }

        private IList<ClientCard> SelectDestructionPair(IList<ClientCard> cards, int min, int max, bool cancelable)
        {
            // Lua SelectSubGroup can ask for one card at a time. Respect each actual
            // offered list; never substitute an absent enemy or two friendly cards.
            var pairs = (from own in cards where own.Controller == 0
                         from enemy in cards where enemy.Controller == 1
                         select new { Own = own, Enemy = enemy, Value = DestructionPairValue(own, enemy) })
                         .OrderByDescending(p => p.Value).ToList();
            if (pairs.Count > 0)
            {
                if (cancelable && pairs[0].Value <= 0) return new List<ClientCard>();
                return new[] { pairs[0].Own, pairs[0].Enemy }.Take(max).ToList();
            }
            return cards.OrderByDescending(c => c.Controller == 1 ? RemovalValue(c, false) : -FriendlyDestructionLoss(c))
                .Take(Math.Min(max, Math.Max(1, min))).ToList();
        }

        private void ReconcilePredictions()
        {
            if (_predictions.Count == 0) return;
            WState actual = Observe();
            foreach (WPrediction prediction in _predictions)
            {
                List<string> differences = new List<string>();
                foreach (long key in prediction.Before.Cards.Keys.Union(prediction.After.Cards.Keys).Union(actual.Cards.Keys))
                {
                    int id = (int)(key / 8); WZone zone = (WZone)(key % 8);
                    int expectedDelta = prediction.After.Count(id, zone) - prediction.Before.Count(id, zone);
                    int actualDelta = actual.Count(id, zone) - prediction.Before.Count(id, zone);
                    if (expectedDelta != actualDelta) differences.Add(id + "/" + zone + ":expectedDelta=" + expectedDelta + "/observedDelta=" + actualDelta);
                }
                bool restrictionsChanged = RestrictionSummary(prediction.After) != RestrictionSummary(actual);
                Logger.DebugWriteLine("Witchcraft2026 reconcile: " + prediction.Action +
                    " status=" + (differences.Count == 0 && !restrictionsChanged ? "matched" : "changed-or-interrupted") +
                    " differences=" + string.Join(";", differences) +
                    " expectedRestrictions=" + RestrictionSummary(prediction.After) +
                    " observedRestrictions=" + RestrictionSummary(actual) +
                    " expectedExpiry=" + string.Join(",", prediction.After.Temporary.Select(p => p.Key + "@" + p.Value)) +
                    " observedExpiry=" + string.Join(",", actual.Temporary.Select(p => p.Key + "@" + p.Value)) +
                    " expectedScore=" + WPlanner.Evaluate(prediction.After).ToString("0.0") +
                    " observedScore=" + WPlanner.Evaluate(actual).ToString("0.0") +
                    " modelActions=" + WPlanner.Actions(actual).Take(257).Count() +
                    " concurrent chain effects included; retire dependent forecast");
            }
            _predictions.Clear(); Invalidate();
        }

        #endregion

        #region Interruption recovery

        // Small sensitivity model over observed opposing interactions, never hidden
        // hand traps. Costs below have existing script-backed action coverage.
        internal static WState DeniedAction(WState before, WAction action)
        {
            WState failed = before.Copy();
            failed.DenialFloor = null; failed.CommitmentCost = 0;
            failed.Pending = 0; failed.ViceWindow = false;
            failed.Used.Add(action.Usage);
            MarkSharedUsage(failed.Used, action);
            if (action.CopiedSpell != 0)
            {
                failed.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                failed.Move(action.CopiedSpell, WZone.Grave, WZone.Banished);
                return failed; // copied operation has not resolved; materials stay put
            }
            if (action.Kind == WKind.SeedRedraw || action.Kind == WKind.PittoreDraw ||
                action.Kind == WKind.PotterieRecover || action.Kind == WKind.DistortionSearch)
            {
                failed.Move(action.Source, WZone.Grave, WZone.Banished);
                return failed; // hand selection/discard occurs only at resolution
            }
            if (action.Location == WZone.Hand && IsModelSpell(action.Source) &&
                action.Kind != WKind.SetTrap && action.Kind != WKind.ActivateField)
                failed.Move(action.Source, WZone.Hand, WZone.Grave);
            if (action.Kind == WKind.Recruit)
            {
                failed.Move(action.Source, WZone.Board, WZone.Grave);
                failed.Move(action.Cost, action.CostZone, WZone.Grave);
            }
            else if (action.Kind == WKind.EdelRevive)
                failed.Move(action.Source, WZone.Board, WZone.Grave);
            else if (action.Kind == WKind.Send)
                failed.Move(action.Source, WZone.Grave, WZone.Banished);
            else if (action.Kind == WKind.GenniCopy || action.Kind == WKind.GenniLacrima)
            {
                failed.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                failed.Move(action.Kind == WKind.GenniCopy ? CardId.Creation : CardId.Lacrima, WZone.Grave, WZone.Banished);
            }
            else if (action.Kind == WKind.Marka)
                failed.Move(action.Source, WZone.Hand, WZone.Grave);
            else if (action.Kind == WKind.NegateBoard)
                failed.Move(action.Cost, WZone.Hand, WZone.Grave);
            return failed;
        }

        internal static double IndependentAccess(WState state)
        {
            // Value an actual spare access card only with an unused effect and a
            // remaining own-deck target. No card is invented after a failed search.
            double access = 0;
            if (state.Has(CardId.SpellPowerMastery, WZone.Hand) && !state.Used.Contains(CardId.SpellPowerMastery + ":" + WKind.Mastery) &&
                (state.Has(CardId.MaidGenni, WZone.Deck) || state.Has(CardId.MaidVerre, WZone.Deck))) access += 6;
            if (state.Has(CardId.Creation, WZone.Hand) && !state.Used.Contains(CardId.Creation + ":" + WKind.Creation) &&
                WitchMonsters.Any(id => state.Has(id, WZone.Deck))) access += 4;
            return access;
        }

        private static void AssessRouteExposure(WState before, WAction action, WState after)
        {
            if (action.Kind == WKind.Normal || action.Kind == WKind.DarkSummon || action.Kind == WKind.SetTrap) return;
            bool monster = !IsModelSpell(action.Source);
            if (monster && IsSpellcasterMaterial(action.Source) && before.SpellcasterProtected) return;
            OpponentResponse kind = monster ? (action.Location == WZone.Board ? OpponentResponse.FieldMonster :
                OpponentResponse.HandGraveMonster) : OpponentResponse.Spell;
            bool conditionalBox = kind == OpponentResponse.FieldMonster &&
                (before.OpposingResponses & OpponentResponse.ConditionalFieldMonster) != 0 &&
                action.Kind != WKind.Recruit && action.Kind != WKind.EdelRevive && action.Kind != WKind.Marka &&
                action.Source != CardId.RedEyesDarkDragoon;
            bool exposed = (before.OpposingResponses & kind) != 0 || conditionalBox;
            bool paidRecruit = action.Kind == WKind.Recruit && action.CostZone == WZone.Hand &&
                !before.Has(CardId.Walpurgis, WZone.Backrow);
            if (!exposed && !paidRecruit) return;
            WState failed = DeniedAction(before, action);
            if (conditionalBox && failed.Has(action.Source, WZone.Board)) failed.Disabled.Add(action.Source);
            double floor = WPlanner.UninterruptedValue(failed) + IndependentAccess(failed);
            if (exposed)
            {
                // Jacks is removal, not negation. Model the vulnerable board after
                // successful resolution separately from C104's failed field effect.
                if (kind == OpponentResponse.HandGraveMonster)
                {
                    int[] bodies = after.In(WZone.Board).Where(id => before.Has(id, WZone.Board) &&
                        (id != CardId.RedEyesDarkDragoon || before.Disabled.Contains(id))).ToArray();
                    if (bodies.Length == 0) return;
                    floor = double.PositiveInfinity;
                    // At most seven existing bodies: compare actual resource loss,
                    // rather than assigning an opponent a fixed target priority.
                    foreach (int body in bodies)
                    {
                        failed = after.Copy(); failed.DenialFloor = null;
                        failed.Move(body, WZone.Board, WZone.Grave);
                        floor = Math.Min(floor, WPlanner.UninterruptedValue(failed) + IndependentAccess(failed));
                    }
                }
                after.DenialFloor = before.DenialFloor.HasValue ? Math.Min(before.DenialFloor.Value, floor) : floor;
                if (!before.DenialFloor.HasValue || floor < before.DenialFloor.Value)
                    after.Vulnerability = action.Kind + "/" + kind + (conditionalBox ?
                        "/conditional-Box:counter-unobserved,coin,hidden-Deck;not-guaranteed" : "/single-response-sensitivity");
            }
            if (paidRecruit)
            {
                // This is paid-resource insurance, not an assertion that the hidden
                // opponent has Impulse/Ash. Prefer preserving independent access.
                after.CommitmentCost += Math.Max(0, IndependentAccess(before) - IndependentAccess(failed));
            }
        }

        #endregion

        #region Action coverage and protection

        // Immutable public target snapshots. Keys identify slots, never hidden cards.
        internal sealed class WRemovalTarget
        {
            public int Key, Id;
            public double Value;
            public OpponentResponse Responses;
        }

        private readonly HashSet<ClientCard> _freshBystreetShields = new HashSet<ClientCard>();
        private bool _bystreetSeenThisTurn;

        public override void OnNewPhase()
        {
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                _freshBystreetShields.Clear(); // battles can consume prevention without a chain
            Invalidate();
            base.OnNewPhase();
        }

        private static IEnumerable<WAction> CoverageActions(WState s)
        {
            if (s.Has(CardId.Bystreet, WZone.Hand) && !s.Has(CardId.Bystreet, WZone.Backrow) && s.OccupiedBackrow < 5)
                yield return new WAction { Kind = WKind.ActivateBystreet, Source = CardId.Bystreet,
                    Location = WZone.Hand, Description = -1, Route = "continuous protection / preserve hand alternative" };
            if (!s.Has(CardId.Celebration, WZone.Hand) || s.OccupiedBackrow >= 5) yield break;
            foreach (int own in s.In(WZone.Board).Where(IsWitchMaterial))
                foreach (WRemovalTarget enemy in s.EnemyTargets.Where(t => t.Value > 0).Take(12))
                    yield return new WAction { Kind = WKind.CelebrationDestroy, Source = CardId.Celebration,
                        Location = WZone.Hand, Description = 0, Target = enemy.Id, EnemyKey = enemy.Key,
                        Materials = new[] { own }, MaterialZones = new[] { WZone.Board },
                        Route = "non-targeting paired destruction; friendly loss and enemy consequence" };
        }

        private static void MarkSharedUsage(HashSet<string> used, WAction a)
        {
            if (a.Source == CardId.Genni && a.Location == WZone.Grave)
                used.Add("64756282:grave-copy");
            if (a.Source == CardId.Celebration && (a.Kind == WKind.Fusion || a.Kind == WKind.CelebrationDestroy))
            {
                used.Add(CardId.Celebration + ":" + WKind.Fusion);
                used.Add(CardId.Celebration + ":" + WKind.CelebrationDestroy);
            }
            if (a.Cost == CardId.Bystreet && a.CostZone == WZone.Backrow)
                used.Add(CardId.Bystreet + ":cost-or-recovery");
        }

        private static void ApplyDestructionPair(WState s, WAction action)
        {
            int own = action.Materials[0];
            bool shield = s.Has(CardId.Bystreet, WZone.Backrow) && !s.Disabled.Contains(CardId.Bystreet) && s.FreshShields.Remove(own);
            if (!shield) s.Move(own, WZone.Board, WZone.Grave);
            WRemovalTarget enemy = s.EnemyTargets.First(t => t.Key == action.EnemyKey);
            s.RemovedThreat += enemy.Value;
            s.EnemyTargets.Remove(enemy);
            s.OpposingResponses &= s.EnemyTargets.Aggregate(OpponentResponse.None, (mask, t) => mask | t.Responses);
            if (!s.EnemyTargets.Any(t => t.Id == enemy.Id))
                s.Restrictions.RemoveAll(r => r.Player == 1 && r.Id == enemy.Id);
            // A removal cannot cancel an interruption already paid earlier in a route.
        }

        private void ObserveCoverage(WState s)
        {
            s.OccupiedBackrow = Bot.GetSpells().Count(c => c.Sequence < 5);
            s.CanEstablishShield = !_bystreetSeenThisTurn;
            foreach (ClientCard card in Bot.GetMonsters().Where(c => _freshBystreetShields.Contains(c)))
                if (s.Count(card.Id, WZone.Board) == 1) s.FreshShields.Add(card.Id);
            foreach (ClientCard card in Enemy.GetMonsters().Concat(Enemy.GetSpells()))
            {
                s.EnemyTargets.Add(new WRemovalTarget
                {
                    Key = ((int)card.Location * 16) + card.Sequence + 1,
                    Id = card.IsFaceup() ? card.Id : 0,
                    Value = RemovalValue(card, false),
                    Responses = _opponent.VisibleResponses(new[] { card })
                });
            }
            s.OpposingResponses = s.EnemyTargets.Aggregate(OpponentResponse.None, (mask, t) => mask | t.Responses);
        }

        private void ObserveBystreetResolution(ChainInfo info)
        {
            if (info.ActivatePlayer != 0 || info.ActivateId != CardId.Bystreet ||
                info.ActivateLocation != CardLocation.SpellZone) return;
            ClientCard source = info.RelatedCard;
            if (!_bystreetSeenThisTurn && source != null && source.Location == CardLocation.SpellZone &&
                source.IsFaceup() && !source.IsDisabled())
                _freshBystreetShields.UnionWith(Bot.GetMonsters().Where(c => c.IsFaceup() && c.HasSetcode(WitchcrafterSetcode)));
            _bystreetSeenThisTurn = true;
        }

        // Only an audit, never a fallback that executes an unsupported offer. Main
        // phase lists are stale during chain windows: explicitly label that boundary.
        private string ActionCoverageAudit(WState state, WPlan plan)
        {
            List<WAction> generated = WPlanner.Actions(state).Take(257).ToList();
            List<string> entries = new List<string>();
            if (_chainOffers != null || AI.SelectingEffectYn)
                return "window=chain/trigger; idle-list-not-current; optional idle actions deferred; current=" +
                    (Card == null ? "none" : Card.Id + "/" + Card.Location + "/" + ActivateDescription);
            MainPhase main = Duel.MainPhase;
            if (main == null) return "window=none; no idle offers";
            for (int i = 0; i < main.ActivableCards.Count && i < 16; i++)
            {
                ClientCard card = main.ActivableCards[i]; int desc = main.ActivableDescs[i];
                List<WAction> actions = generated.Where(a => ActionType(a) == ExecutorType.Activate && Matches(a, card, desc)).ToList();
                string status;
                if (actions.Count > 0)
                    status = "modeled=" + string.Join(",", actions.Select(a => a.Kind).Distinct()) +
                        "; immediate=" + string.Join(",", actions.Take(3).Select(a => WPlanner.Evaluate(WPlanner.Apply(state, a)).ToString("0.0"))) +
                        "; " + (plan.Steps.Count > 0 && actions.Any(a => a.ToString() == plan.Steps[0].Action.ToString()) ? "selected" :
                        plan.TimedOut ? "not-selected/budget-limited" : "not-selected/by-bounded-comparison");
                else status = UnmodeledOfferReason(state, card, desc);
                entries.Add(card.Id + "/" + card.Location + "/" + desc + " " + status);
            }
            int offered = generated.Count(IsOffered);
            return "window=idle model=" + generated.Count + " offered-model=" + offered +
                " unavailable-model=" + (generated.Count - offered) + " (projection-only, not engine-authorized)" +
                " unavailable-examples=[" + string.Join(" | ", generated.Where(a => !IsOffered(a)).Take(4)) + "]" +
                " other-offers=[normal:" + string.Join(",", main.SummonableCards.Take(8).Select(c => c.Id)) +
                ";special:" + string.Join(",", main.SpecialSummonableCards.Take(8).Select(c => c.Id)) +
                ";set:" + string.Join(",", main.SpellSetableCards.Take(8).Select(c => c.Id)) + "]" +
                " hold=" + WPlanner.Evaluate(state).ToString("0.0") + " best=" + plan.Score.ToString("0.0") +
                " decision=" + (plan.Steps.Count == 0 ? "HOLD" : "PLAY " + plan.Steps[0].Action.Kind) +
                " offers=[" + string.Join(" | ", entries) + "]";
        }

        private static string UnmodeledOfferReason(WState state, ClientCard card, int description)
        {
            if (card.Id == CardId.Lacrima)
                return "no-modeled-action: verify zone/used send/hostile chain; send-used=" +
                    state.Used.Contains(CardId.Lacrima + ":" + WKind.LacrimaSend) +
                    "; recruit-used=" + state.Used.Contains(CardId.Lacrima + ":" + WKind.LacrimaRecruit) +
                    "; opponent-chain=" + state.EnemyChain + "; no GY activation in installed script";
            if (card.Id == CardId.Celebration)
                return "no-modeled-action: shared activation used, no modeled Fusion materials or no useful public destruction pair; partial-coverage-check-required";
            if (card.Id == CardId.Bystreet)
                return card.Location == CardLocation.Grave ? "delegated: own End Phase recovery handler; shared cost/recovery limit engine-owned" :
                    "policy-omission: duplicate Bystreet or occupied S/T zones; no new shield assumed";
            if (card.Location == CardLocation.Grave && IsGraveSupported(card.Id))
                return "grave mode: conditions/usage or event window; draws use unknown-outcome boundary; Pittore preserves non-Witch hand";
            if (card.Id == CardId.AshBlossom || card.Id == CardId.MaxxC || card.Id == CardId.Haine ||
                card.Id == CardId.MadameVerre || card.Id == CardId.ViceMadame || card.Id == CardId.Distortion || card.Id == CardId.Seed)
                return "delegated/partial: tactical handler owns applicable mode; not evidence every mode is supported";
            return "UNCLASSIFIED/MISSING: no generated candidate; inspect conditions/usage/mode; not a strategic rejection";
        }

        #endregion

        #region Graveyard effects

        private readonly Dictionary<ClientCard, int> _graveEntered = new Dictionary<ClientCard, int>();
        private readonly HashSet<ClientCard> _markaRevived = new HashSet<ClientCard>();
        private readonly List<ClientCard> _seedPicked = new List<ClientCard>();
        private bool _unveilingProtection;

        private static bool IsGraveSupported(int id)
        {
            return LowIds.Contains(id) || id == CardId.Seed || id == CardId.Marka ||
                id == CardId.Distortion || id == CardId.MadameRillie || id == CardId.DespianLuluwalilith;
        }

        private void ObserveGraveMove(ClientCard card, int previous, int controller, int current)
        {
            if (previous == current) return;
            if (controller == 0 && current == (int)CardLocation.Grave) _graveEntered[card] = Duel.Turn;
            else _graveEntered.Remove(card);
            if (previous == (int)CardLocation.MonsterZone) _markaRevived.Remove(card);
        }

        private void ObserveGraveResolution(ChainInfo info)
        {
            if (info.ActivatePlayer == 0 && info.ActivateId == CardId.Marka &&
                info.ActivateLocation == CardLocation.Grave && info.RelatedCard != null &&
                info.RelatedCard.Location == CardLocation.MonsterZone) _markaRevived.Add(info.RelatedCard);
        }

        private void ObserveGraveState(WState s)
        {
            s.SpellcasterProtected = _unveilingProtection;
            s.DistortionReady = Bot.Graveyard.Any(c => c.Id == CardId.Distortion &&
                _graveEntered.ContainsKey(c) && _graveEntered[c] < Duel.Turn);
            // A current engine offer is stronger evidence than a missing history
            // snapshot. Never use an idle list retained during a chain window.
            if (Card != null && Card.Id == CardId.Distortion && Card.Location == CardLocation.Grave &&
                ActivateDescription == UtilString(CardId.Distortion, 1) &&
                (AI.SelectingEffectYn || (_chainOffers != null && _chainOffers.Contains(Card)) ||
                (Duel.CurrentChain.Count == 0 && Duel.MainPhase != null && Duel.MainPhase.ActivableCards.Contains(Card))))
                s.DistortionReady = true;
            foreach (ClientCard c in _markaRevived.Where(c => c.Location == CardLocation.MonsterZone))
                s.BanishOnLeave.Add(c.Id);
        }

        // Marginal hand utility for cycling only; it does not rewrite the main
        // board evaluator or pretend an unknown draw is a specific combo card.
        internal static double CycleValue(WState s, int id)
        {
            bool duplicate = s.Count(id, WZone.Hand) > 1;
            bool spent = s.Used.Any(u => u.StartsWith(id + ":"));
            if (id == CardId.AshBlossom || id == CardId.MaxxC) return spent ? 1 : 4;
            if (id == CardId.Walpurgis) return s.Has(id, WZone.Backrow) || duplicate ? .5 : 5;
            if (id == CardId.Bystreet && s.Has(id, WZone.Backrow)) return .5;
            if (spent && IsModelSpell(id)) return 1;
            if (id == CardId.Creation || id == CardId.SpellPowerMastery || id == CardId.Terraforming ||
                id == CardId.RainbowBridgeBifrost) return duplicate ? 1 : 4;
            if (LowIds.Contains(id)) return s.Normal ? 3 : 1;
            if (id == CardId.Marka || id == CardId.MaidGenni || id == CardId.MaidVerre || id == CardId.Terracottan)
                return spent || duplicate ? 1 : 3;
            if (id == CardId.Haine || id == CardId.MadameVerre || id == CardId.Edel)
                return s.Has(CardId.Unveiling, WZone.Hand) || s.Active(CardId.Edel) ? 3 : 1;
            return IsModelSpell(id) ? 2.5 : 1.5;
        }

        private static double AverageDrawValue(WState s)
        {
            int total = s.Size(WZone.Deck);
            return total == 0 ? 0 : s.In(WZone.Deck).Sum(id => CycleValue(s, id) * s.Count(id, WZone.Deck)) / total;
        }

        private static double DrawRisk(WState s, int draws)
        {
            double mean = AverageDrawValue(s);
            double variance = s.In(WZone.Deck).Sum(id => s.Count(id, WZone.Deck) *
                Math.Pow(CycleValue(s, id) - mean, 2)) / Math.Max(1, s.Size(WZone.Deck));
            // Downside sensitivity, not a probability or a promised next hand.
            return Math.Sqrt(variance * draws);
        }

        private static IEnumerable<WAction> GraveActions(WState s)
        {
            if (!s.OpponentTurn && s.Has(CardId.Potterie, WZone.Grave) && s.Size(WZone.Hand) + s.UnknownHand == 0)
                foreach (int id in s.In(WZone.Grave).Where(id => IsWitchCard(id) &&
                    (id != CardId.Potterie || s.Count(id, WZone.Grave) > 1)))
                    yield return GraveAction(WKind.PotterieRecover, CardId.Potterie, id, -1);
            if (s.Has(CardId.Distortion, WZone.Grave) && s.DistortionReady)
                foreach (int id in s.In(WZone.Deck).Where(IsHighSpellcaster))
                    foreach (int discard in s.In(WZone.Hand).Concat(new[] { id }).Distinct())
                    {
                        WAction a = GraveAction(WKind.DistortionSearch, CardId.Distortion, id, 1);
                        a.Cost = discard; a.CostZone = WZone.Hand;
                        yield return a;
                    }
            if (!s.OpponentTurn && s.Has(CardId.Seed, WZone.Grave) && s.Size(WZone.Deck) > 0)
            {
                List<int> hand = s.In(WZone.Hand).SelectMany(id => Enumerable.Repeat(id, s.Count(id, WZone.Hand)))
                    .OrderBy(id => CycleValue(s, id)).Take(12).ToList();
                foreach (int spell in hand.Where(IsModelSpell).Distinct())
                {
                    List<int> returns = new List<int> { spell };
                    List<int> rest = new List<int>(hand); rest.Remove(spell);
                    for (int i = 0; i <= rest.Count; i++)
                    {
                        if (i > 0) returns.Add(rest[i - 1]);
                        WAction a = GraveAction(WKind.SeedRedraw, CardId.Seed, 0, 1);
                        a.Materials = returns.ToArray(); yield return a;
                    }
                }
            }
            if (!s.OpponentTurn && s.Has(CardId.Pittore, WZone.Grave) && s.Size(WZone.Deck) > 0)
            {
                // With a nonempty non-Witch hand, a miss banishes the whole hand.
                // Deliberate policy omission, not a missing activation handler.
                foreach (int discard in s.In(WZone.Hand).Where(IsWitchCard))
                {
                    WAction a = GraveAction(WKind.PittoreDraw, CardId.Pittore, 0, -1);
                    a.Cost = discard; a.CostZone = WZone.Hand; yield return a;
                }
                if (s.Size(WZone.Hand) + s.UnknownHand == 0)
                    yield return GraveAction(WKind.PittoreDraw, CardId.Pittore, 0, -1);
            }
            if (!s.OpponentTurn && s.Has(CardId.Genni, WZone.Grave))
                foreach (WAction a in ExtraGenniCopies(s)) yield return a;
        }

        private static bool IsWitchCard(int id)
        {
            return IsWitchMaterial(id) || WitchSpells.Contains(id) || id == CardId.Lacrima || id == CardId.Distortion;
        }

        private static bool IsHighSpellcaster(int id)
        {
            // All Level 5+ main-deck Spellcasters in the selected 40/15/7 list.
            // Pure planning must not call the runtime-only NamedCardsManager.
            return id == CardId.Marka || id == CardId.Edel || id == CardId.Haine || id == CardId.MadameVerre;
        }

        private static WAction GraveAction(WKind kind, int source, int target, int desc)
        {
            return new WAction { Kind = kind, Source = source, Target = target, Location = WZone.Grave,
                Description = desc, Route = "grave effect; actual resources and replan after resolution" };
        }

        private static IEnumerable<WAction> ExtraGenniCopies(WState s)
        {
            // Remove both costs BEFORE selecting recycle materials. Creation and
            // Lacrima recruit retain their existing edges; add their other choices.
            foreach (int spell in new[] { CardId.Celebration, CardId.ConfusionConfession,
                CardId.Unveiling, CardId.Lacrima, CardId.Draping })
            {
                if (!s.Has(spell, WZone.Grave)) continue;
                WState available = s.Copy();
                available.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                available.Move(spell, WZone.Grave, WZone.Banished);
                List<WAction> candidates = new List<WAction>();
                if (spell == CardId.Celebration || spell == CardId.ConfusionConfession)
                {
                    available.Add(spell, WZone.Hand);
                    candidates.AddRange(WPlanner.Fusions(available).Where(a => a.Source == spell));
                    if (spell == CardId.Celebration)
                    {
                        available.OccupiedBackrow = 0; // copying does not place a Spell
                        candidates.AddRange(CoverageActions(available).Where(a => a.Kind == WKind.CelebrationDestroy));
                    }
                }
                if (spell == CardId.Unveiling && WPlanner.MainMonsters(available) < 5)
                    candidates.AddRange(available.In(WZone.Hand).Where(IsWitchMaterial).Select(id =>
                        GraveAction(WKind.Unveiling, CardId.Genni, id, 1)));
                if (spell == CardId.Lacrima)
                    candidates.AddRange(available.In(WZone.Deck).Where(id => IsModelSpell(id) || IsSpellcasterMaterial(id))
                        .Select(id => GraveAction(WKind.LacrimaSend, CardId.Genni, id, 1)));
                if (spell == CardId.Draping && available.In(WZone.Board).Any(IsWitchMaterial))
                    foreach (WRemovalTarget target in available.EnemyTargets.Where(t => t.Key >= 128 && t.Value > 0))
                    {
                        WAction a = GraveAction(WKind.CopiedDraping, CardId.Genni, target.Id, 1);
                        a.EnemyKey = target.Key; candidates.Add(a);
                    }
                foreach (WAction a in candidates)
                {
                    a.Source = CardId.Genni; a.Location = WZone.Grave; a.Description = 1;
                    a.CopiedSpell = spell; a.Route = "Genni copies installed Spell activation mode";
                    yield return a;
                }
            }
        }

        private static void ApplyGraveAction(WState s, WAction a)
        {
            if (a.CopiedSpell == 0) s.Move(a.Source, WZone.Grave, WZone.Banished);
            if (a.Kind == WKind.PotterieRecover)
                s.Move(a.Target, WZone.Grave, IsWitchMaterial(a.Target) && !WitchMonsters.Contains(a.Target) ? WZone.Extra : WZone.Hand);
            if (a.Kind == WKind.DistortionSearch)
            {
                s.Move(a.Target, WZone.Deck, WZone.Hand);
                s.Move(a.Cost, WZone.Hand, WZone.Grave); // resolution, NOT activation cost
            }
            if (a.Kind == WKind.CopiedDraping)
            {
                WRemovalTarget t = s.EnemyTargets.First(e => e.Key == a.EnemyKey);
                s.RemovedThreat += t.Value * .5; // bounce preserves their card, unlike destruction
                s.EnemyTargets.Remove(t);
                s.OpposingResponses &= s.EnemyTargets.Aggregate(OpponentResponse.None, (m, e) => m | e.Responses);
            }
            if (a.Kind == WKind.SeedRedraw)
            {
                double returned = a.Materials.Sum(id => CycleValue(s, id));
                foreach (int id in a.Materials) s.Move(id, WZone.Hand, WZone.Deck);
                s.UnknownHand += a.Materials.Length;
                s.DrawUtility += AverageDrawValue(s) * a.Materials.Length - returned - DrawRisk(s, a.Materials.Length);
                s.RandomBoundary = true;
            }
            if (a.Kind == WKind.PittoreDraw)
            {
                if (a.Cost != 0)
                {
                    s.DrawUtility += AverageDrawValue(s) - CycleValue(s, a.Cost) - DrawRisk(s, 1);
                    s.Move(a.Cost, WZone.Hand, WZone.Grave); s.UnknownHand++;
                }
                else
                {
                    // Empty hand: a Witch draw is sent to GY; another draw is
                    // banished. No retained hand card or invented combo access.
                    s.DrawUtility += s.In(WZone.Deck).Where(IsWitchCard).Sum(id => s.Count(id, WZone.Deck)) /
                        (double)Math.Max(1, s.Size(WZone.Deck));
                }
                s.RandomBoundary = true;
            }
        }

        private bool GraveWindowAction()
        {
            if (Card == null || Card.Controller != 0 || Card.Location != CardLocation.Grave) return false;
            if (Card.Id == CardId.Distortion)
            {
                if (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2) return PlannedAction();
                WState s = Observe();
                WAction best = GraveActions(s).Where(a => a.Kind == WKind.DistortionSearch && !s.Used.Contains(a.Usage))
                    .OrderByDescending(a => CycleValue(s, a.Target) - CycleValue(s, a.Cost)).FirstOrDefault();
                if (best == null || CycleValue(s, best.Target) <= CycleValue(s, best.Cost)) return false;
                AcceptAction(best); return true;
            }
            if (Card.Id == CardId.Marka && (ActivateDescription <= 0 || ActivateDescription == UtilString(Card.Id, 1)))
            {
                ClientCard target = Enemy.GetMonsters().OrderByDescending(c => OpponentAssessment(c, ChainForecast()).Removal).FirstOrDefault();
                if (target == null || Bot.GetMonsters().Count(c => c.Sequence < 5) >= 5) return false;
                AcceptAction(new WAction { Kind = WKind.Tactical, Source = Card.Id, Location = WZone.Grave,
                    Description = 1, Target = target.Id, Route = "offered Marka trigger: revive then non-targeting tribute" });
                return true;
            }
            if (Duel.Phase != DuelPhase.End) return false;
            if (Card.Id == CardId.MadameRillie && Duel.Player == 0 && Bot.Hand.Any(c => c.IsSpell()) &&
                (ActivateDescription <= 0 || ActivateDescription == UtilString(Card.Id, 1)))
            {
                AcceptAction(new WAction { Kind = WKind.Tactical, Source = Card.Id, Location = WZone.Grave,
                    Description = 1, Route = "offered properly-summoned Rilliona revival; reveal not discard" });
                return true;
            }
            if (Card.Id == CardId.DespianLuluwalilith &&
                (ActivateDescription <= 0 || ActivateDescription == UtilString(Card.Id, 1)))
            {
                AcceptAction(new WAction { Kind = WKind.Tactical, Source = Card.Id, Location = WZone.Grave,
                    Description = 1, Route = "offered sent-this-turn Luluwalilith End Phase recruit" });
                return true;
            }
            return false;
        }

        private IList<ClientCard> SelectGraveCards(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            WAction a = PromptAction();
            ChainInfo resolving = Duel.GetCurrentSolvingChainInfo();
            int id = a != null && a.Location == WZone.Grave ? a.Source :
                resolving != null && resolving.ActivatePlayer == 0 && resolving.ActivateLocation == CardLocation.Grave ? resolving.ActivateId : 0;
            if (id == 0) return null;
            if (id == CardId.Seed && hint == HintMsg.ToDeck)
            {
                // SelectUnselect can offer one card at a time. Always start with a
                // Spell so the subgroup constraint is satisfiable; finish legally.
                List<int> remaining = a == null ? cards.Select(c => c.Id).ToList() : a.Materials.ToList();
                foreach (ClientCard picked in _seedPicked) remaining.Remove(picked.Id);
                List<ClientCard> desired = new List<ClientCard>();
                foreach (ClientCard offered in cards)
                    if (remaining.Remove(offered.Id)) desired.Add(offered);
                List<ClientCard> ordered = desired.OrderByDescending(c => c.IsSpell()).ThenBy(c => CycleValue(Observe(), c.Id)).ToList();
                if (ordered.Count == 0 && cancelable && min == 0) return new List<ClientCard>();
                if (ordered.Count < min) ordered.AddRange(cards.Where(c => !ordered.Contains(c)).OrderByDescending(c => c.IsSpell()));
                List<ClientCard> selected = ordered.Take(Math.Min(max, Math.Max(min, ordered.Count))).ToList();
                _seedPicked.AddRange(selected);
                return selected;
            }
            if ((id == CardId.Pittore && hint == HintMsg.ToGrave) || (id == CardId.Distortion && hint == HintMsg.Discard))
            {
                WState s = Observe();
                // Re-evaluate after the actual draw/search, not a queued pre-draw ID.
                return cards.OrderBy(c => CycleValue(s, c.Id) - (IsWitchCard(c.Id) ? 1 : 0))
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            }
            if (id == CardId.Marka && hint == HintMsg.Release)
                return cards.OrderByDescending(c => OpponentAssessment(c, ChainForecast()).Removal)
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            if (id == CardId.MadameRillie && hint == HintMsg.Confirm)
                return cards.OrderBy(c => CycleValue(Observe(), c.Id)).Take(Math.Min(max, Math.Max(1, min))).ToList();
            if (id == CardId.DespianLuluwalilith && hint == HintMsg.SpSummon)
                return cards.OrderByDescending(c => c.Id == CardId.MaidVerre).ThenByDescending(c => c.Attack)
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            if (a != null && a.Kind == WKind.CopiedDraping && hint == HintMsg.ReturnToHand)
                return cards.OrderByDescending(c => ((int)c.Location * 16) + c.Sequence + 1 == a.EnemyKey)
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            return null;
        }

        #endregion
    }
}

#region Opponent awareness

namespace WindBot.Game.AI
{
    [Flags]
    public enum OpponentOutcome
    {
        None = 0, Search = 1, Summon = 2, Send = 4, Destroy = 8,
        Negate = 16, Control = 32, Set = 64, Recover = 128, Draw = 256
    }

    [Flags]
    public enum OpponentResponse { None = 0, FieldMonster = 1, HandGraveMonster = 2, Spell = 4, ConditionalFieldMonster = 8 }
    public enum OpponentWindow { Activation, Ignition, Quick, Summoned, Destroyed }
    public enum OpponentLimit { NameTurn, InstanceTurn, UnknownActivation, None }

    // Reviewed semantics, not card-name priorities. Weights below are shared outcome
    // utilities, not probabilities or a prediction of private resources/legal offers.
    public sealed class OpponentEffect
    {
        public int CardId, Offset, Group, RequiredOverlay;
        public string Name;
        public CardLocation Location;
        public OpponentWindow Window;
        public OpponentLimit Limit = OpponentLimit.NameTurn;
        public OpponentOutcome Outcome;
        public OpponentResponse RespondsTo;
        public bool RequiresFaceup, HiddenCost, OverlayCost, OnePerChain;
        public double Units = 1;

        public double Utility
        {
            get
            {
                double value = 0;
                if ((Outcome & (OpponentOutcome.Search | OpponentOutcome.Draw | OpponentOutcome.Recover | OpponentOutcome.Set)) != 0) value += 8;
                if ((Outcome & OpponentOutcome.Summon) != 0) value += 10;
                if ((Outcome & OpponentOutcome.Send) != 0) value += 8;
                if ((Outcome & (OpponentOutcome.Destroy | OpponentOutcome.Negate)) != 0) value += 12;
                if ((Outcome & OpponentOutcome.Control) != 0) value += 18;
                return value * Units;
            }
        }
    }

    public sealed class OpponentEffectUse
    {
        public int Activated, Resolved, NegatedOrDisabled, Unidentified;
        // Protocol currently combines activation-negated and effect-disabled. An
        // activation-only oath's remaining availability cannot always be established.
        public bool ConsumptionUncertain;
    }

    public sealed class OpponentThreat
    {
        public double Body, Future, PreventedResolution, DestructionBenefit, NegateNow;
        public bool Supported;
        public readonly List<string> Effects = new List<string>();
        public double Removal { get { return Body + Future + PreventedResolution - DestructionBenefit; } }
        public string Explain()
        {
            return "body=" + Body.ToString("0.0") + " future=" + Future.ToString("0.0") +
                " prevents-resolution=" + PreventedResolution.ToString("0.0") +
                " destruction-followup=" + DestructionBenefit.ToString("0.0") +
                " negate-now=" + NegateNow.ToString("0.0") + " confidence=" +
                (Supported ? "reviewed-semantics/conditional-resources" : "unknown-effect/bounded-reserve") +
                " effects=[" + string.Join(";", Effects.Take(6)) + "]";
        }
    }

    public sealed class OpponentAwareness
    {
        private sealed class Entry
        {
            public OpponentEffect Effect;
            public OpponentEffectUse Use;
            public bool Finished;
        }

        private readonly List<OpponentEffect> _effects;
        private readonly Dictionary<string, OpponentEffectUse> _usage = new Dictionary<string, OpponentEffectUse>();
        private readonly Dictionary<ClientCard, int> _instances = new Dictionary<ClientCard, int>();
        private readonly Dictionary<ChainInfo, Entry> _links = new Dictionary<ChainInfo, Entry>();
        private int _serial;

        public OpponentAwareness(IEnumerable<OpponentEffect> effects = null)
        {
            _effects = (effects ?? VerifiedEffects()).ToList();
        }

        public void NewTurn()
        {
            _usage.Clear(); _links.Clear(); _instances.Clear(); _serial = 0;
        }

        public void ResetInstance(ClientCard card)
        {
            if (card != null) _instances.Remove(card);
        }

        private string Key(OpponentEffect effect, ClientCard card, int player)
        {
            int instance = 0;
            if (effect.Limit == OpponentLimit.InstanceTurn && card != null && !_instances.TryGetValue(card, out instance))
            { instance = ++_serial; _instances[card] = instance; }
            return player + ":" + effect.CardId + ":" + effect.Group + ":" + instance;
        }

        public OpponentEffectUse Usage(OpponentEffect effect, ClientCard card, int player)
        {
            OpponentEffectUse result;
            return _usage.TryGetValue(Key(effect, card, player), out result) ? result : new OpponentEffectUse();
        }

        public OpponentEffect Identify(ChainInfo link)
        {
            List<OpponentEffect> candidates = _effects.Where(e => e.CardId == link.ActivateId &&
                (e.Location & link.ActivateLocation) != 0).ToList();
            OpponentEffect exact = candidates.FirstOrDefault(e => link.ActivateDescription == e.CardId * 16 + e.Offset);
            if (exact != null) return exact;
            // 0/-1 carry no effect identity. Infer only if the location is unambiguous.
            return (link.ActivateDescription == 0 || link.ActivateDescription == -1) && candidates.Count == 1 ? candidates[0] : null;
        }

        public void Observe(IList<ChainInfo> chain)
        {
            foreach (ChainInfo link in chain)
            {
                if (_links.ContainsKey(link)) continue;
                OpponentEffect effect = Identify(link);
                Entry entry = new Entry { Effect = effect };
                if (effect != null)
                {
                    string key = Key(effect, link.RelatedCard, link.ActivatePlayer);
                    if (!_usage.TryGetValue(key, out entry.Use)) _usage[key] = entry.Use = new OpponentEffectUse();
                    entry.Use.Activated++;
                }
                else if (link.ActivateDescription == 0 || link.ActivateDescription == -1)
                {
                    // An ambiguous notification is evidence of activity, not proof
                    // that every mode was used (nor that every mode remains unused).
                    foreach (OpponentEffect candidate in _effects.Where(e => e.CardId == link.ActivateId &&
                        (e.Location & link.ActivateLocation) != 0).GroupBy(e => e.Group).Select(g => g.First()))
                    {
                        string key = Key(candidate, link.RelatedCard, link.ActivatePlayer);
                        OpponentEffectUse uncertain;
                        if (!_usage.TryGetValue(key, out uncertain)) _usage[key] = uncertain = new OpponentEffectUse();
                        uncertain.Unidentified++;
                    }
                }
                _links.Add(link, entry);
            }
        }

        public void Solved(IList<ChainInfo> chain, int index, bool negated)
        {
            Observe(chain);
            if (index < 1 || index > chain.Count) return;
            Entry entry = _links[chain[index - 1]];
            if (entry.Finished) return;
            entry.Finished = true;
            if (entry.Use == null) return;
            if (negated) entry.Use.NegatedOrDisabled++; else entry.Use.Resolved++;
            if (negated && entry.Effect.Limit == OpponentLimit.UnknownActivation) entry.Use.ConsumptionUncertain = true;
        }

        public void EndChain() { _links.Clear(); }

        private bool Available(OpponentEffect effect, ClientCard card)
        {
            if (effect.OnePerChain && _links.Any(p => p.Key.ActivateId == effect.CardId &&
                p.Key.ActivatePlayer == card.Controller)) return false;
            OpponentEffectUse use = Usage(effect, card, card.Controller);
            return effect.Limit == OpponentLimit.None || use.Activated == 0 || use.ConsumptionUncertain;
        }

        private static bool PublicCondition(OpponentEffect effect, ClientCard card)
        {
            return (!effect.OverlayCost || card.Overlays.Count > 0) &&
                (effect.RequiredOverlay == 0 || card.Overlays.Contains(effect.RequiredOverlay));
        }

        public OpponentThreat Assess(ClientCard card, IList<ChainInfo> chain, ISet<int> negatedZeroBased,
            bool projectedDisabled, bool ownerTurn)
        {
            Observe(chain);
            OpponentThreat result = new OpponentThreat();
            if (card == null || card.Id == 0 || card.IsFacedown())
            { result.Body = 2; result.Future = 3; return result; }
            result.Body = card.IsMonster() ? 2 + Math.Min(3, Math.Max(0, card.Attack) / 1500.0) : 1;
            List<OpponentEffect> effects = _effects.Where(e => e.CardId == card.Id).ToList();
            result.Supported = effects.Count > 0;
            if (!result.Supported) { result.Future = card.IsDisabled() || projectedDisabled ? 0 : 4; return result; }
            Dictionary<int, double> future = new Dictionary<int, double>();
            foreach (OpponentEffect effect in effects)
            {
                OpponentEffectUse used = Usage(effect, card, card.Controller);
                bool live = !card.IsDisabled() && !projectedDisabled;
                string status = used.Activated > 0 ? "committed/used" : "not-observed-used";
                if (used.ConsumptionUncertain) status += "/oath-uncertain";
                if (used.Unidentified > 0) status += "/unidentified-activation-availability-uncertain";
                result.Effects.Add(effect.Name + ":" + status + "/resolved=" + used.Resolved +
                    "/negated-or-disabled=" + used.NegatedOrDisabled + (effect.HiddenCost ? "/hidden-cost-unknown" : ""));
                if (effect.Window == OpponentWindow.Destroyed && Available(effect, card) &&
                    card.Location == CardLocation.SpellZone)
                    result.DestructionBenefit += effect.Utility * .75; // target in hidden Deck is not guaranteed
                if ((effect.Location & card.Location) == 0 || !PublicCondition(effect, card)) continue;
                if (live && Available(effect, card) &&
                    (effect.Window == OpponentWindow.Quick || effect.Window == OpponentWindow.Ignition))
                {
                    double confidence = effect.HiddenCost ? .35 : .75;
                    if (used.Unidentified > 0) confidence *= .5;
                    double value = effect.Utility * confidence * (effect.Window == OpponentWindow.Ignition && !ownerTurn ? .4 : 1);
                    double prior; future.TryGetValue(effect.Group, out prior);
                    future[effect.Group] = Math.Max(prior, value); // alternate modes share one use
                }
                for (int i = 0; i < chain.Count; i++)
                {
                    ChainInfo link = chain[i];
                    if (!ReferenceEquals(link.RelatedCard, card) || Identify(link) != effect ||
                        negatedZeroBased.Contains(i) || !live) continue;
                    // A committed activation establishes its costs/conditions. Removal
                    // stops it only for a reviewed field-presence dependency.
                    if (effect.RequiresFaceup) result.PreventedResolution += effect.Utility;
                    result.NegateNow += effect.Utility;
                }
            }
            result.Future = future.Values.Sum();
            return result;
        }

        public OpponentResponse VisibleResponses(IEnumerable<ClientCard> cards)
        {
            OpponentResponse result = OpponentResponse.None;
            foreach (ClientCard card in cards.Where(c => c.IsFaceup() && !c.IsDisabled()))
                foreach (OpponentEffect effect in _effects.Where(e => e.CardId == card.Id && (e.Location & card.Location) != 0))
                    if (Available(effect, card) && PublicCondition(effect, card) &&
                        (!effect.HiddenCost || effect.RespondsTo == OpponentResponse.ConditionalFieldMonster))
                        result |= effect.RespondsTo;
            return result;
        }

        private static OpponentEffect E(int id, int offset, int group, string name, CardLocation location,
            OpponentWindow window, OpponentOutcome outcome)
        {
            return new OpponentEffect { CardId = id, Offset = offset, Group = group, Name = name,
                Location = location, Window = window, Outcome = outcome };
        }

        public static IEnumerable<OpponentEffect> VerifiedEffects()
        {
            // Provenance: configured MDPro4 script.zip, reviewed 2026-11-08; hashes
            // and native receipts in README. No downloaded text is executed here.
            OpponentEffect box = E(2606417, 1, 1, "counter + coin + hidden Deck summon -> disable surviving target / ATK0", CardLocation.SpellZone,
                OpponentWindow.Quick, OpponentOutcome.Summon | OpponentOutcome.Negate);
            box.Limit = OpponentLimit.None; box.HiddenCost = true; box.RequiresFaceup = true;
            box.RespondsTo = OpponentResponse.ConditionalFieldMonster;
            yield return box;
            OpponentEffect counters = E(2606417, 0, 0, "Standby die -> public counters (client does not retain quantity)", CardLocation.SpellZone,
                OpponentWindow.Summoned, OpponentOutcome.None);
            counters.Limit = OpponentLimit.InstanceTurn; yield return counters;
            yield return E(28642461, 1, 0, "reveal two Level5 -> summon pair", CardLocation.Hand, OpponentWindow.Ignition, OpponentOutcome.Summon);
            yield return E(28642461, 2, 1, "non-Aqua K9 search", CardLocation.MonsterZone, OpponentWindow.Ignition, OpponentOutcome.Search);
            yield return E(92248362, 1, 0, "conditional hand summon", CardLocation.Hand, OpponentWindow.Quick, OpponentOutcome.Summon);
            yield return E(92248362, 2, 1, "on-summon K9 send", CardLocation.MonsterZone, OpponentWindow.Summoned, OpponentOutcome.Send);
            OpponentEffect caseSearch = E(80181649, 0, 0, "activation-only K9 search", CardLocation.SpellZone, OpponentWindow.Activation, OpponentOutcome.Search);
            caseSearch.RequiresFaceup = true; caseSearch.Limit = OpponentLimit.UnknownActivation;
            yield return caseSearch;
            yield return E(80181649, 2, 1, "destroyed in S/T zone -> set K9 Quick-Play", CardLocation.Grave | CardLocation.Removed,
                OpponentWindow.Destroyed, OpponentOutcome.Set);
            OpponentEffect hand = E(93156774, 0, 0, "reveal VS -> hand summon", CardLocation.Hand, OpponentWindow.Quick, OpponentOutcome.Summon);
            hand.OnePerChain = true; yield return hand;
            OpponentEffect control = E(93156774, 1, 1, "EARTH+DARK reveal -> lowest ATK control", CardLocation.MonsterZone, OpponentWindow.Quick, OpponentOutcome.Control);
            control.HiddenCost = true; control.OnePerChain = true; yield return control;
            OpponentEffect summon = E(93156774, 2, 1, "FIRE+DARK reveal -> non-Psychic VS summon", CardLocation.MonsterZone, OpponentWindow.Quick, OpponentOutcome.Summon);
            summon.HiddenCost = true; summon.OnePerChain = true; yield return summon;
            OpponentEffect c104 = E(49456901, 1, 1, "Number104 material -> negate field monster / optional random send", CardLocation.MonsterZone,
                OpponentWindow.Quick, OpponentOutcome.Negate | OpponentOutcome.Send);
            c104.RequiredOverlay = 2061963; c104.OverlayCost = true; c104.Limit = OpponentLimit.InstanceTurn;
            c104.RespondsTo = OpponentResponse.FieldMonster; yield return c104;
            OpponentEffect onSummon = E(49456901, 0, 0, "on-summon S/T destruction", CardLocation.MonsterZone, OpponentWindow.Summoned, OpponentOutcome.Destroy);
            onSummon.Limit = OpponentLimit.None; yield return onSummon;
            OpponentEffect jacks = E(67515699, 0, 0, "Xyz summon or hostile hand/GY monster -> destroy", CardLocation.MonsterZone,
                OpponentWindow.Quick, OpponentOutcome.Destroy);
            jacks.OverlayCost = true; jacks.RespondsTo = OpponentResponse.HandGraveMonster; yield return jacks;
        }
    }
}

#endregion
