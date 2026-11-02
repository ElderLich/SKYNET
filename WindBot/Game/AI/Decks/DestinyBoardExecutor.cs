using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    // RDA-first boundaries: Capture observes, FindMaterialPlan predicts a bounded
    // immediate transition, and handlers accept only the server's current offer.
    // Plans never modify Duel and are recomputed after every response/interrupt.
    [Deck("DestinyBoard", "AI_DestinyBoard")]
    public class DestinyBoardExecutor : DefaultExecutor
    {
        public static class CardId
        {
            public const int TourGuide = 10802915, Rhino = 81035362, Octhros = 58616392;
            public const int Ruler = 100268007, Zorc = 73320394, Necrofear = 31829185, Curse = 14509651;
            public const int Banishment = 51993760, Malice = 87988305;
            public const int Mastery = 14386013, Sanctuary = 16625614, Board = 94212438;
            public const int I = 31893528, N = 67287533, A = 94772232, L = 30170981;
            public const int Sentence = 40771118, Shield = 8324284;
            public const int Charity = 79571449, Pot = 55144522, Foolish = 81439173, Reborn = 83764718, Judgment = 41420027;
            public const int Cherubini = 58699500, Muckraker = 71607202, Dharc = 8264361;
            public const int Phoenix = 2857636, Unicorn = 38342335, Rage = 21419436, Abomination = 29479265;
            public const int Goddess = 98127546, Darius = 51497409, Machinex = 46593546;
            public const int Dingirsu = 93854893, Hope = 63767246, LittleKnight = 29301450;
        }

        private static readonly int[] Letters = { CardId.I, CardId.N, CardId.A, CardId.L };
        private static readonly int[] ExtraIds = { CardId.Cherubini, CardId.Darius, CardId.Machinex, CardId.Dingirsu,
            CardId.Hope, CardId.Goddess, CardId.Dharc, CardId.LittleKnight, CardId.Phoenix, CardId.Unicorn,
            CardId.Rage, CardId.Abomination, CardId.Muckraker };
        private readonly HashSet<long> _used = new HashSet<long>();
        private readonly Dictionary<ClientCard, int> _boardProgress = new Dictionary<ClientCard, int>();
        private readonly HashSet<ClientCard> _muckrakerSummoned = new HashSet<ClientCard>();
        private readonly HashSet<ClientCard> _temporaryControl = new HashSet<ClientCard>();
        private readonly List<ClientCard> _materials = new List<ClientCard>();
        private readonly HashSet<int> _failedExtraSummons = new HashSet<int>();
        private int _acceptedId;
        private int _acceptedDescription;
        private int _pendingSummon;
        private int _replacementId;
        private bool _fiendLock;
        private bool _normalUsed;
        private bool _intentionalRulerDestruction;
        private ClientCard _zorcSelected;

        public DestinyBoardExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, CardId.Judgment, JudgmentEffect);
            AddExecutor(ExecutorType.Activate, CardId.Sentence, SentenceEffect);
            AddExecutor(ExecutorType.Activate, CardId.Board, BoardEffect);
            AddExecutor(ExecutorType.Activate, CardId.Shield, ShieldEffect);
            AddExecutor(ExecutorType.Activate, CardId.Banishment, SpiritEffect);
            AddExecutor(ExecutorType.Activate, CardId.Malice, SpiritEffect);
            foreach (int id in ExtraIds) AddExecutor(ExecutorType.Activate, id, ExtraEffect);
            AddExecutor(ExecutorType.Activate, CardId.Pot, DrawEffect);
            AddExecutor(ExecutorType.Activate, CardId.Charity, DrawEffect);
            AddExecutor(ExecutorType.Activate, CardId.Sanctuary, SanctuaryEffect);
            AddExecutor(ExecutorType.Activate, CardId.Mastery, MasteryEffect);
            AddExecutor(ExecutorType.Activate, CardId.Octhros, AlwaysEffect);
            AddExecutor(ExecutorType.Activate, CardId.Rhino, RhinoEffect);
            AddExecutor(ExecutorType.Activate, CardId.TourGuide, AlwaysEffect);
            AddExecutor(ExecutorType.Activate, CardId.Zorc, ZorcEffect);
            AddExecutor(ExecutorType.Activate, CardId.Foolish, FoolishEffect);
            AddExecutor(ExecutorType.Activate, CardId.Ruler, RulerEffect);
            AddExecutor(ExecutorType.Activate, CardId.Curse, CurseEffect);
            AddExecutor(ExecutorType.Activate, CardId.Necrofear, AlwaysEffect);
            AddExecutor(ExecutorType.Summon, CardId.TourGuide, NormalSummon);
            AddExecutor(ExecutorType.Activate, CardId.Reborn, RebornEffect);
            AddExecutor(ExecutorType.Summon, CardId.Rhino, NormalSummon);
            AddExecutor(ExecutorType.Summon, CardId.Octhros, NormalSummon);
            AddExecutor(ExecutorType.Summon, CardId.Banishment, NormalSummon);
            AddExecutor(ExecutorType.Summon, CardId.Malice, NormalSummon);
            foreach (int id in ExtraIds) AddExecutor(ExecutorType.SpSummon, id, ExtraSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Necrofear, NecrofearSummon);
            AddExecutor(ExecutorType.SpellSet, SetBackrow);
            AddExecutor(ExecutorType.Repos, Reposition);
        }

        private static bool IsLetter(ClientCard c) { return c != null && Letters.Contains(c.Id); }
        private static bool IsBoardPiece(ClientCard c) { return c != null && (c.Id == CardId.Board || IsLetter(c)); }
        private static bool IsFiend(ClientCard c) { return c != null && (c.Race != 0 ? c.HasRace(CardRace.Fiend) : c.Data != null && c.Data.Race == (int)CardRace.Fiend); }
        private static bool IsEffect(ClientCard c) { return c != null && (c.Type != 0 ? c.HasType(CardType.Effect) : c.Data != null && (c.Data.Type & (int)CardType.Effect) != 0); }
        private static int Level(ClientCard c) { return c.Level != 0 ? c.Level : c.Data != null ? c.Data.Level : 0; }
        private bool HasHand(int id) { return Bot.Hand.Any(c => c.IsCode(id)); }
        private bool HasMonster(int id) { return Bot.MonsterZone.Any(c => c != null && c.IsCode(id) && c.IsFaceup()); }
        private bool HasBackrow(int id) { return Bot.SpellZone.Any(c => c != null && c.IsCode(id) && c.IsFaceup() && !c.IsDisabled()); }
        private bool HasBoard { get { return HasBackrow(CardId.Board); } }
        private int FreeMonsters { get { return Bot.MonsterZone.Take(5).Count(c => c == null); } }
        private int FreeBackrow { get { return Bot.SpellZone.Take(5).Count(c => c == null); } }
        private int GraveFiends { get { return Bot.Graveyard.Count(IsFiend); } }
        private int LetterCount { get { return Bot.GetMonsters().Concat(Bot.GetSpells()).Where(c => IsLetter(c) && c.IsFaceup()).Select(c => c.Id).Distinct().Count(); } }
        private bool OnFieldLetter(int id) { return Bot.GetMonsters().Concat(Bot.GetSpells()).Any(c => c.IsCode(id) && c.IsFaceup()); }
        private bool IsNaturallyDue(int id)
        {
            foreach (ClientCard board in Bot.GetSpells().Where(c => c.Id == CardId.Board && c.IsFaceup()))
            {
                int progress;
                _boardProgress.TryGetValue(board, out progress);
                if (progress < Letters.Length && Letters[progress] == id) return true;
            }
            return false;
        }
        private bool CanAccelerate()
        {
            return HasBoard && Letters.Any(id => !OnFieldLetter(id) && (LetterCount == 3 || !IsNaturallyDue(id))
                && (HasHand(id) || Bot.HasInDeck(id) || Bot.Graveyard.Any(c => c.Id == id)));
        }
        private bool Used(int id, int effect) { return _used.Contains(((long)id << 4) | (uint)effect); }
        private int EffectIndex(int id, int desc)
        {
            for (int i = 0; i < 8; i++) if (desc == Util.GetStringId(id, i)) return i;
            return -1;
        }
        private bool Accept(int effect)
        {
            _acceptedId = Card.Id;
            _acceptedDescription = ActivateDescription;
            if (effect >= 0) _used.Add(((long)Card.Id << 4) | (uint)effect);
            Logger.DebugWriteLine("[DestinyBoard] accept " + Card.Id + " effect=" + effect + " location=" + Card.Location);
            return true;
        }
        private int PromptSource()
        {
            if (_replacementId != 0) return _replacementId;
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null) return solving.ActivatePlayer == 0 ? solving.ActivateId : 0;
            if (_acceptedId != 0) return _acceptedId;
            ClientCard chain = Duel.GetCurrentChainCard();
            if (chain != null) return chain.Controller == 0 ? chain.Id : 0;
            return _pendingSummon;
        }
        private int PromptDescription()
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null) return solving.ActivateDescription;
            if (_acceptedId != 0) return _acceptedDescription;
            ChainInfo chain = Duel.CurrentChainInfo.LastOrDefault();
            return chain == null ? -1 : chain.ActivateDescription;
        }

        public override bool OnSelectHand() { return true; }
        private bool AlwaysEffect() { return Accept(EffectIndex(Card.Id, ActivateDescription)); }
        private bool JudgmentEffect()
        {
            if (Duel.CurrentChain.Count > 0) return Duel.LastChainPlayer == 1 && Accept(-1);
            return (Duel.SummoningCards.Any(c => c.Controller == 1) || Duel.Player == 1) && Accept(-1);
        }
        private bool DrawEffect()
        {
            int draw = Card.Id == CardId.Pot ? 2 : 3;
            return Bot.Deck.Count >= draw && DefaultCheckWhetherBotCanDraw() && !Card.IsDisabled() && Accept(-1);
        }
        private bool SanctuaryEffect()
        {
            if (Card.Location == CardLocation.SpellZone && Card.IsFaceup()) return Accept(-1);
            return !HasBackrow(CardId.Sanctuary) && Accept(-1);
        }
        private bool BoardEffect()
        {
            if (Card.IsFacedown() || Card.Location == CardLocation.Hand)
                return !HasBoard && Accept(-1);
            return Accept(-1);
        }
        private bool SentenceEffect()
        {
            if (Card.IsFacedown()) return HasBoard && Accept(-1);
            if (Used(CardId.Sentence, 0)) return false;
            int effect = EffectIndex(Card.Id, ActivateDescription);
            if (effect == 1)
                return CanAccelerate() && Accept(0);
            if (effect == 0)
            {
                // Placement is normally more valuable; recovery is for a spent or
                // currently unavailable letter package, and never with zero names.
                if (!HasBoard && LetterCount == 0) return false;
                bool canAdvance = CanAccelerate();
                return !canAdvance && Bot.Graveyard.Concat(Bot.Banished).Any(IsFiend) && Accept(0);
            }
            return false;
        }
        private bool ShieldEffect()
        {
            if (Card.IsFacedown()) return (HasBoard || Bot.UnderAttack || Duel.LastChainPlayer == 1) && Accept(-1);
            int effect = EffectIndex(Card.Id, ActivateDescription);
            if (effect == 2) return Accept(-1); // Mandatory End Phase maintenance.
            if (effect == 1) return Duel.LastChainPlayer == 1 && Accept(-1);
            if (effect == 0)
            {
                ClientCard attacker = Enemy.BattlingMonster;
                ClientCard defender = Bot.BattlingMonster;
                bool needed = defender == null || attacker == null || attacker.Attack >= (defender.IsDefense() ? defender.Defense : defender.Attack);
                return needed && Accept(-1);
            }
            return false;
        }
        private bool MasteryEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return DefaultCheckWhetherBotCanDraw() && !Used(CardId.Mastery, 1) && Bot.Graveyard.Concat(Bot.Hand).Any(c => IsBoardPiece(c)
                    && (c.Id != CardId.Board || HasBoard || Bot.Hand.Count(x => x.Id == CardId.Board) > 1)) && Accept(1);
            if (Used(CardId.Mastery, 0) || Bot.Hand.Count < 2) return false;
            if (!DefaultCheckWhetherBotCanSearch() && !Bot.Graveyard.Any(c => c.Id == CardId.Board || IsFiend(c) && Level(c) == 8)) return false;
            return Accept(0);
        }
        private bool RhinoEffect() { return !Used(CardId.Rhino, 0) && Accept(0); }
        private bool FoolishEffect() { return Bot.Deck.Count > 0 && Accept(-1); }
        private bool RulerEffect()
        {
            if (Card.Location == CardLocation.Hand)
                return FreeMonsters > 0 && !Used(CardId.Ruler, 0) && Accept(0);
            return !Used(CardId.Ruler, 1) && Accept(1);
        }
        private bool CurseEffect() { return Accept(Card.Location == CardLocation.Hand ? 0 : 1); }
        private bool SpiritEffect()
        {
            if (Card.Location == CardLocation.Grave) return Accept(1);
            // Sending a fielded Dark Spirit as the activation cost opens its own
            // Main Monster Zone before the target is revived.
            if (FreeMonsters == 0 && Card.Location != CardLocation.MonsterZone || Used(Card.Id, 0)) return false;
            return Bot.Graveyard.Any(c => Level(c) == 8 && IsFiend(c) && (c.Id != CardId.Necrofear || c.ProcCompleted != 0)) && Accept(0);
        }
        private bool ZorcEffect()
        {
            if (Card.Location == CardLocation.Hand)
            {
                if (Used(CardId.Zorc, 0) || !DefaultCheckWhetherBotCanSearch()) return false;
                return Bot.Hand.Any(c => c != Card && !IsBoardPiece(c) && DiscardScore(c) < 70) && Accept(0);
            }
            if (Card.Location == CardLocation.Grave) return FreeMonsters > 0 && Accept(1);
            if (Card.IsDisabled() || Used(CardId.Zorc, 2) || Enemy.GetMonsterCount() == 0) return false;
            return Bot.GetMonsters().Concat(Bot.GetSpells()).Any(c => !IsBoardPiece(c) || IsLetter(c) && c.Location == CardLocation.MonsterZone)
                && Accept(2);
        }
        private bool NormalSummon()
        {
            if (_normalUsed || FreeMonsters == 0) return false;
            if (Card.Id != CardId.TourGuide && HasHand(CardId.TourGuide)) return false;
            if ((Card.Id == CardId.Banishment || Card.Id == CardId.Malice) && Bot.GetMonsterCount() == 0 && !HasBoard) return false;
            _normalUsed = true;
            return true;
        }
        private bool RebornEffect()
        {
            if (FreeMonsters == 0) return false;
            return Bot.Graveyard.Concat(Enemy.Graveyard).Any(c => c.IsMonster() && !IsLetter(c)
                && (!_fiendLock || IsFiend(c)) && (c.Id != CardId.Necrofear || c.ProcCompleted != 0)) && Accept(-1);
        }
        private bool NecrofearSummon()
        {
            // Do not turn off a live Ruler hand summon or consume our revival target.
            if (FreeMonsters == 0 || GraveFiends < 3 || HasHand(CardId.Ruler) && GraveFiends < 6) return false;
            if (HasMonster(CardId.Necrofear) && HasBoard) return false;
            _pendingSummon = CardId.Necrofear;
            return true;
        }
        private bool SetBackrow()
        {
            if (IsLetter(Card)) return false;
            if (Card.Id == CardId.Board) return !HasBoard && !Bot.GetSpells().Any(c => c.Id == CardId.Board);
            if (Card.Id != CardId.Judgment && Card.Id != CardId.Shield && Card.Id != CardId.Sentence) return false;
            if (Bot.GetSpells().Any(c => c.Id == Card.Id)) return false;
            int reserve = HasBackrow(CardId.Sanctuary) ? 1 : Math.Max(1, 4 - LetterCount);
            return FreeBackrow > reserve || Card.Id == CardId.Sentence && FreeBackrow > 1;
        }
        private bool Reposition() { return !IsLetter(Card) && DefaultMonsterRepos(); }

        // PART 1/2: immutable scalar observation used only by the bounded one-step
        // Extra Deck model. No enemy hand/deck identities enter this snapshot.
        private sealed class Observation
        {
            public readonly int Enemies, EnemyBackrow, EnemyPower, Letters, Fuel, Free, Turn;
            public readonly bool Board, Ruler, FiendLock, Mill, HasDiscard, DarkGrave, Main2;
            public Observation(int enemies, int backrow, int power, int letters, int fuel, int free, int turn,
                bool board, bool ruler, bool fiendLock, bool mill, bool discard, bool darkGrave, bool main2)
            {
                Enemies = enemies; EnemyBackrow = backrow; EnemyPower = power; Letters = letters;
                Fuel = fuel; Free = free; Turn = turn; Board = board; Ruler = ruler;
                FiendLock = fiendLock; Mill = mill; HasDiscard = discard; DarkGrave = darkGrave; Main2 = main2;
            }
        }
        private Observation Capture()
        {
            return new Observation(Enemy.GetMonsterCount(), Enemy.GetSpellCountWithoutField(),
                Enemy.GetMonsters().Select(c => Math.Max(c.Attack, c.Defense)).DefaultIfEmpty(0).Max(),
                LetterCount, GraveFiends, FreeMonsters, Duel.Turn, HasBoard, HasMonster(CardId.Ruler), _fiendLock,
                !Used(CardId.Cherubini, 0) && Bot.HasInDeck(CardId.Rhino)
                    && (DefaultCheckWhetherBotCanSearch() || HasHand(CardId.Ruler)), Bot.Hand.Any(c => !IsBoardPiece(c)),
                Enemy.Graveyard.Any(c => c.HasAttribute(CardAttribute.Dark) && c.IsMonster()), Duel.Phase == DuelPhase.Main2);
        }
        private sealed class MaterialPlan
        {
            public readonly int Id;
            public readonly List<ClientCard> Cards;
            public readonly double Score;
            public MaterialPlan(int id, List<ClientCard> cards, double score) { Id = id; Cards = cards; Score = score; }
        }
        private static int LinkRating(int id)
        {
            if (id == CardId.Goddess) return 5;
            if (id == CardId.Abomination) return 4;
            if (id == CardId.Unicorn || id == CardId.Rage) return 3;
            if (id == CardId.Darius || id == CardId.Machinex || id == CardId.Dingirsu || id == CardId.Hope) return 0;
            return 2;
        }
        private static bool LinkSum(IList<ClientCard> cards, int index, int sum)
        {
            if (index == cards.Count) return sum == 0;
            if (sum <= 0) return false;
            ClientCard card = cards[index];
            return LinkSum(cards, index + 1, sum - 1)
                || card.HasType(CardType.Link) && card.LinkCount > 1 && LinkSum(cards, index + 1, sum - card.LinkCount);
        }
        private bool HasExtraDestination(int id, IList<ClientCard> materials)
        {
            var own = Bot.GetMonsters().Where(c => !materials.Contains(c)).ToList();
            var enemy = Enemy.GetMonsters().Where(c => !materials.Contains(c)).ToList();
            bool linkedPlacement = LinkRating(id) > 0 || Duel.IsNewRule && !Duel.IsNewRule2020;
            if (!linkedPlacement || !Duel.IsNewRule && !Duel.IsNewRule2020)
                return own.Count(c => c.Sequence < 5) < 5;
            if (!own.Any(c => c.Sequence >= 5)
                && (!enemy.Any(c => c.Sequence == 5) || !enemy.Any(c => c.Sequence == 6))) return true;
            int linked = 0;
            foreach (ClientCard c in own) linked |= c.GetLinkedZones() & 0x1f;
            foreach (ClientCard c in enemy) linked |= (c.GetLinkedZones() >> 16) & 0x1f;
            foreach (ClientCard c in own.Where(c => c.Sequence < 5)) linked &= ~(1 << c.Sequence);
            return linked != 0;
        }
        private bool SuitableMaterials(int id, List<ClientCard> cards)
        {
            if (cards.Any(IsLetter)) return false;
            if (id == CardId.Machinex) return cards.Count == 1 && cards[0].Id == CardId.Darius;
            if (id == CardId.Darius) return cards.Count == 2 && cards.All(c => Level(c) == 3 && IsFiend(c) && !c.HasType(CardType.Xyz) && !c.HasType(CardType.Link));
            if (id == CardId.Dingirsu || id == CardId.Hope) return cards.Count == 2 && cards.All(c => Level(c) == 8 && !c.HasType(CardType.Xyz) && !c.HasType(CardType.Link));
            if (cards.Any(c => _muckrakerSummoned.Contains(c))) return false;
            int min = id == CardId.Goddess ? 4 : 2;
            if (cards.Count < min || !LinkSum(cards, 0, LinkRating(id))) return false;
            if ((id == CardId.Cherubini || id == CardId.Muckraker || id == CardId.Dharc || id == CardId.Phoenix || id == CardId.LittleKnight) && cards.Count != 2) return false;
            if (id == CardId.Cherubini && !cards.All(c => Level(c) == 3 && !c.HasType(CardType.Xyz) && !c.HasType(CardType.Link))) return false;
            if ((id == CardId.Muckraker || id == CardId.LittleKnight || id == CardId.Goddess) && !cards.All(IsEffect)) return false;
            if (id == CardId.Dharc && !cards.Any(c => c.HasAttribute(CardAttribute.Dark))) return false;
            if ((id == CardId.Phoenix || id == CardId.Unicorn) && cards.Select(c => c.Id).Distinct().Count() != cards.Count) return false;
            if (id == CardId.Rage && !cards.Any(IsFiend)) return false;
            if (id == CardId.Abomination && !cards.Any(c => c.HasType(CardType.Link))) return false;
            return cards.Count(c => c.Controller == 1) <= (id == CardId.Goddess ? 1 : 0);
        }
        private double MaterialCost(ClientCard c)
        {
            if (c.Controller == 1) return -Math.Max(35, Threat(c));
            if (IsBoardPiece(c)) return 10000;
            if (_temporaryControl.Contains(c)) return -15;
            if (c.IsDisabled()) return 6;
            if (c.Id == CardId.Octhros && !Used(CardId.Octhros, 0)) return -15;
            if (c.Id == CardId.Rhino && !Used(CardId.Rhino, 0)) return -10;
            if (c.Id == CardId.TourGuide) return 8;
            if (c.Id == CardId.Cherubini) return !Used(CardId.Cherubini, 0) && Bot.HasInDeck(CardId.Rhino) ? 110 : 16;
            if (c.Id == CardId.Zorc && !Used(CardId.Zorc, 2) && Enemy.GetMonsterCount() > 0) return 95;
            if (c.Id == CardId.Muckraker && !Used(CardId.Muckraker, 0) && Bot.Hand.Count > 0) return 100;
            if (c.Id == CardId.Ruler) return 45;
            if (c.Id == CardId.Necrofear || c.Id == CardId.Curse) return 38;
            if (c.Id == CardId.Darius) return 12;
            if (c.Id == CardId.Hope || c.Id == CardId.Dingirsu || c.Id == CardId.Goddess || c.Id == CardId.Machinex) return 110;
            return Math.Max(12, c.Attack / 100.0);
        }
        private static double SummonValue(int id, Observation s, IList<ClientCard> materials)
        {
            if (s.Letters >= 3 && s.Board && s.Enemies == 0) return -100;
            if (id == CardId.Cherubini) return s.Mill ? 125 : -100;
            if (id == CardId.Darius) return 70;
            if (id == CardId.Machinex) return 100;
            if (id == CardId.Dingirsu) return s.Enemies + s.EnemyBackrow > 0 ? 160 : s.Board ? 110 : -100;
            if (id == CardId.Hope) return s.Enemies == 0 ? 120 : 90;
            if (id == CardId.Goddess) return materials.Any(c => c.Controller == 1) ? 130 : -100;
            if (id == CardId.Dharc) return s.DarkGrave ? 80 : -100;
            if (id == CardId.Phoenix) return s.EnemyBackrow > 0 && s.HasDiscard ? 95 : -100;
            if (id == CardId.Unicorn) return s.Enemies + s.EnemyBackrow > 0 && s.HasDiscard ? 120 : -100;
            if (id == CardId.LittleKnight) return s.Enemies > 0 && materials.Any(c => c.HasType(CardType.Link) || c.HasType(CardType.Xyz)) ? 110 : -100;
            if (id == CardId.Rage) return s.Ruler && !materials.Any(c => c.Id == CardId.Ruler) ? 125 : 60;
            if (id == CardId.Abomination) return s.Enemies + s.EnemyBackrow > 0 ? 135 : -100;
            if (id == CardId.Muckraker) return s.HasDiscard && s.Fuel > 0 ? 90 : -100;
            return -100;
        }
        private MaterialPlan FindMaterialPlan(int id, Observation observed, Stopwatch watch)
        {
            ClientCard extra = Bot.ExtraDeck.FirstOrDefault(c => c.Id == id);
            if (extra == null || _fiendLock && !IsFiend(extra)) return null;
            var pool = Bot.GetMonsters().Where(c => c.IsFaceup() && !IsLetter(c)).ToList();
            if (id == CardId.Goddess) pool.AddRange(Enemy.GetMonsters().Where(c => c.IsFaceup() && c.Id != 0));
            // At most 14 public field monsters, 16383 subsets. One deadline starts
            // before enumeration; no persistent worker/queue can outlive the offer.
            MaterialPlan best = null;
            int limit = Math.Min(1 << pool.Count, 16384);
            for (int mask = 1; mask < limit && watch.ElapsedMilliseconds < 25; mask++)
            {
                var chosen = new List<ClientCard>();
                for (int i = 0; i < pool.Count; i++) if ((mask & (1 << i)) != 0) chosen.Add(pool[i]);
                if (!SuitableMaterials(id, chosen)) continue;
                if (chosen.Count(c => c.Controller == 0) == 0) continue;
                if (!HasExtraDestination(id, chosen)) continue;
                if (id == CardId.Rage && !Bot.GetMonsters().Any(c => !chosen.Contains(c) && !IsLetter(c) && IsFiend(c))) continue;
                if (id == CardId.Muckraker && !Bot.Graveyard.Concat(chosen).Any(c => c.Id != CardId.Muckraker && IsFiend(c)
                    && (c.Id != CardId.Necrofear || c.ProcCompleted != 0))) continue;
                // A speculative backrow probe is not worth our live recovery boss.
                // Visible active backrow or an expendable pair still permits Phoenix.
                if (id == CardId.Phoenix && chosen.Any(c => c.Id == CardId.Ruler && !c.IsDisabled())
                    && !Used(CardId.Ruler, 1) && Enemy.GetMonsters().Any(c => c.IsFaceup() && c.Id != 0 && c.Attack > 0)
                    && !Enemy.GetSpells().Any(c => c.IsFaceup() && c.Id != 0 && !c.IsDisabled())) continue;
                double score = SummonValue(id, observed, chosen) - chosen.Sum(c => MaterialCost(c));
                // Preserve the imminent Board win and don't turn all our attackers
                // into a weaker body just because an Extra Deck summon is legal.
                if (observed.Board && observed.Letters >= 2) score -= 20;
                if (best == null || score > best.Score) best = new MaterialPlan(id, chosen, score);
            }
            return best != null && best.Score > 0 ? best : null;
        }
        private bool ExtraSummon()
        {
            if (Card.Location != CardLocation.Extra) return false;
            if (_failedExtraSummons.Contains(Card.Id)) return false;
            if (Card.Id == CardId.Machinex && Duel.Phase == DuelPhase.Main1 && Duel.Turn > 1
                && Bot.GetMonsters().Any(c => c.Id == CardId.Darius && !c.Attacked && Enemy.GetMonsters().Any(d => CanUseDariusBattle(c, d)))) return false;
            var watch = Stopwatch.StartNew();
            Observation observed = Capture();
            MaterialPlan candidate = FindMaterialPlan(Card.Id, observed, watch);
            if (candidate == null) return false;
            // Choosing amongst offered legal summons avoids assuming an engine
            // will offer every modelled material set or ignore a current lock.
            foreach (ClientCard offered in Duel.MainPhase.SpecialSummonableCards)
            {
                if (offered == Card || !ExtraIds.Contains(offered.Id) || _failedExtraSummons.Contains(offered.Id)) continue;
                MaterialPlan alternative = FindMaterialPlan(offered.Id, observed, watch);
                if (alternative != null && alternative.Score > candidate.Score + 1) return false;
                if (watch.ElapsedMilliseconds >= 25) break;
            }
            _pendingSummon = Card.Id;
            _materials.Clear();
            _materials.AddRange(candidate.Cards);
            Logger.DebugWriteLine("[DestinyBoard] summon " + Card.Id + " score=" + candidate.Score + " materials=" + string.Join(",", candidate.Cards.Select(c => c.Id)));
            return true;
        }

        private bool ExtraEffect()
        {
            int effect = EffectIndex(Card.Id, ActivateDescription);
            // Continuous replacement prompts have no activation chain of their
            // own. Capture their source only after agreeing to the actual prompt.
            if (AI.SelectingEffectYn && ActivateDescription == 96
                && (Card.Id == CardId.Cherubini || Card.Id == CardId.Muckraker || Card.Id == CardId.Dingirsu))
            {
                if (_intentionalRulerDestruction) return false;
                // DESTROY_CONFIRMED is not exposed by ClientCard. A prospective
                // ordinary cost may therefore disappear from the actual replacement
                // offer. Declining here guarantees mandatory later selection cannot
                // consume a message/Board; Dingirsu's overlay protection stays live.
                if (Card.Id == CardId.Muckraker && Bot.GetMonsters().Any(IsLetter)) return false;
                if (Card.Id == CardId.Cherubini && Bot.GetMonsters().Concat(Bot.GetSpells()).Any(IsBoardPiece)) return false;
                if (Card.Id == CardId.Muckraker && !Bot.GetMonsters().Any(c => c != Card && IsFiend(c)
                    && !IsBoardPiece(c) && MaterialCost(c) < 25)) return false;
                if (Card.Id == CardId.Cherubini && !Bot.GetMonsters().Concat(Bot.GetSpells())
                    .Any(c => c != Card && !IsBoardPiece(c) && MaterialCost(c) < 25)) return false;
                _replacementId = Card.Id;
                return true;
            }
            if (Card.IsDisabled() && Card.Location == CardLocation.MonsterZone) return false;
            switch (Card.Id)
            {
                case CardId.Cherubini: return !Used(Card.Id, 0) && Accept(0);
                case CardId.Muckraker:
                    return !Used(Card.Id, 0) && Bot.Hand.Any(c => !IsBoardPiece(c))
                        && !Duel.MainPhase.ActivableCards.Any(c => (c.Id == CardId.Pot || c.Id == CardId.Charity) && c.Location == CardLocation.Hand)
                        && FreeMonsters > 0 && Accept(0);
                case CardId.Dharc: return Card.Location == CardLocation.MonsterZone ? Accept(0) : DefaultCheckWhetherBotCanSearch() && Accept(1);
                case CardId.Darius:
                    ClientCard battling = Enemy.BattlingMonster;
                    return effect == 1 && battling != null
                        && (battling.GetDefensePower() >= Card.GetDefensePower() || battling.IsMonsterInvincible()) && Accept(1);
                case CardId.Machinex: return effect == 2 && Duel.LastChainPlayer == 1 && Accept(-1);
                case CardId.Dingirsu: return Accept(0);
                case CardId.Hope:
                    if (effect == 0) return Duel.LastChainPlayer == 1 && Accept(-1);
                    if (effect == 1) return Enemy.BattlingMonster != null && Enemy.BattlingMonster.Attack <= Card.Attack && Accept(-1);
                    return effect == 2 && Accept(-1);
                case CardId.Goddess: return effect == 0 || Duel.LastChainPlayer == 1 ? Accept(effect) : false;
                case CardId.Phoenix:
                case CardId.Unicorn: return Enemy.GetFieldCount() > 0 && Bot.Hand.Count > 0 && Accept(0);
                case CardId.Rage:
                    if (Card.Location == CardLocation.Grave) return Duel.LastChainPlayer == 1 && Accept(1);
                    return Bot.GetMonsters().Any(c => IsFiend(c) && !IsLetter(c)) && Accept(0);
                case CardId.Abomination: return (Enemy.GetFieldCount() > 0 || CanDestroyRuler()) && Accept(effect);
                case CardId.LittleKnight:
                    if (effect == 0) return !HasDirectLethal() && Enemy.GetFieldCount() + Enemy.Graveyard.Count > 0 && Accept(0);
                    return Duel.LastChainPlayer == 1 && Enemy.GetMonsters().Any(c => c.IsFaceup())
                        && Bot.GetMonsters().Any(c => !IsLetter(c) && c.IsFaceup()) && Accept(1);
            }
            return false;
        }
        private bool HasDirectLethal()
        {
            return Duel.Player == 0 && Duel.Turn > 1 && Duel.Phase == DuelPhase.Main1 && Enemy.GetMonsterCount() == 0
                && Bot.GetMonsters().Where(c => c.IsAttack() && !c.Attacked && !IsLetter(c)).Sum(c => Math.Max(0, c.Attack)) >= Enemy.LifePoints;
        }
        private bool CanUseDariusBattle(ClientCard attacker, ClientCard defender)
        {
            if (attacker.Id != CardId.Darius || attacker.IsDisabled() || attacker.Overlays.Count == 0 || !attacker.IsAttack()
                || defender == null || !defender.IsFaceup() || defender.IsMonsterHasPreventActivationEffectInBattle()) return false;
            attacker.RealPower = attacker.GetAttackPower();
            defender.RealPower = defender.GetDefensePower();
            if (!base.OnPreBattleBetween(attacker, defender)) return false;
            int damage = Math.Max(0, defender.RealPower - attacker.RealPower);
            // The Lua grants battle indestructibility, not damage prevention. It
            // destroys the battled monster afterward; do not die before that event.
            return defender.RealPower >= attacker.RealPower && damage < Bot.LifePoints;
        }
        private bool CanDestroyRuler()
        {
            if (!HasMonster(CardId.Ruler) || Used(CardId.Ruler, 1) || Enemy.GetMonsterCount() == 0) return false;
            if (HasMonster(CardId.Rhino)) return false;
            ClientCard ruler = Bot.GetMonsters().First(c => c.Id == CardId.Ruler);
            if (Bot.GetMonsters().Any(c => c.Id == CardId.Cherubini && c.IsFaceup() && !c.IsDisabled()
                && (c.GetLinkedZones() & (1 << ruler.Sequence)) != 0)) return false;
            return true;
        }
        private double Threat(ClientCard c)
        {
            if (c == null) return 0;
            if (c.Id == 0 || c.IsFacedown()) return 20;
            return 20 + Math.Max(c.Attack, c.Defense) / 100.0 + (c.HasType(CardType.Link) || c.HasType(CardType.Xyz) ? 15 : 0) + (c.IsDisabled() ? -15 : 0);
        }
        private double DiscardScore(ClientCard c)
        {
            if (IsLetter(c)) return IsNaturallyDue(c.Id) ? 250 : 200;
            if (c.Id == CardId.Board) return HasBoard || Bot.Hand.Count(x => x.Id == CardId.Board) > 1 ? 35 : 150;
            if (c.Id == CardId.Rhino && !Used(CardId.Rhino, 0)) return -30;
            if (c.Id == CardId.Zorc) return 8;
            if (c.Id == CardId.Banishment || c.Id == CardId.Malice) return 0;
            if (c.Id == CardId.Curse) return 10;
            if (c.Id == CardId.Necrofear) return 20;
            if (c.Id == CardId.Ruler) return HasHand(CardId.Reborn) || Bot.Graveyard.Any(x => x.Id == CardId.Malice || x.Id == CardId.Banishment) ? 10 : 45;
            if (c.Id == CardId.Sanctuary) return HasBackrow(CardId.Sanctuary) || Bot.Hand.Count(x => x.Id == c.Id) > 1 ? 25 : 90;
            if (c.Id == CardId.TourGuide) return _normalUsed ? 15 : 80;
            if (c.Id == CardId.Pot || c.Id == CardId.Charity) return 100;
            return 50;
        }
        private bool MasterySearchPending()
        {
            return PromptSource() == CardId.Mastery && EffectIndex(CardId.Mastery, PromptDescription()) == 0
                && !DefaultCheckWhetherBotWillBeBanished(CardType.Spell, CardLocation.SpellZone);
        }
        private bool RetainedMasterySearch(IList<ClientCard> discarded)
        {
            return MasterySearchPending() || !Used(CardId.Mastery, 0)
                && Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2)
                && Bot.Hand.Any(c => c.Id == CardId.Mastery && !discarded.Contains(c)
                    && !DefaultSpellWillBeNegated(c) && !DefaultCheckWhetherBotWillBeBanished(c))
                && (DefaultCheckWhetherBotCanSearch() && (Bot.HasInDeck(CardId.Board) || Bot.HasInDeck(CardId.Ruler)
                    || Bot.HasInDeck(CardId.Zorc) || Bot.HasInDeck(CardId.Curse) || Bot.HasInDeck(CardId.Necrofear))
                    || Bot.Graveyard.Any(c => c.Id == CardId.Board || IsFiend(c) && Level(c) == 8));
        }
        private bool BoardAccessibleAfterDiscard(IList<ClientCard> discarded)
        {
            return Bot.GetSpells().Any(c => c.Id == CardId.Board)
                || Bot.Hand.Any(c => c.Id == CardId.Board && !discarded.Contains(c))
                || RetainedMasterySearch(discarded) && (Bot.Graveyard.Any(c => c.Id == CardId.Board)
                    || DefaultCheckWhetherBotCanSearch() && Bot.HasInDeck(CardId.Board));
        }
        private bool LetterRecoveryRemains(IList<ClientCard> discarded)
        {
            if (Bot.GetSpells().Any(c => c.Id == CardId.Sentence && !c.IsDisabled() && !DefaultTrapWillBeNegated(c))
                || Bot.Hand.Any(c => c.Id == CardId.Sentence && !discarded.Contains(c) && !DefaultTrapWillBeNegated(c))) return true;
            // Retained Mastery can reach the GY through its real search effect.
            // Its recycle may need the following turn, hence only late, non-due
            // letters qualify below. Never count a Mastery being discarded now.
            return DefaultCheckWhetherBotCanDraw() && !Used(CardId.Mastery, 1)
                && (Bot.Graveyard.Any(c => c.Id == CardId.Mastery) || RetainedMasterySearch(discarded));
        }
        private double DiscardBatchScore(IList<ClientCard> discarded)
        {
            bool preserveProtection = Bot.Hand.Any(c => c.Id == CardId.Sentence || c.Id == CardId.Judgment);
            bool recoverLate = preserveProtection && LetterCount < 3 && BoardAccessibleAfterDiscard(discarded)
                && LetterRecoveryRemains(discarded);
            double result = 0;
            foreach (ClientCard card in discarded)
            {
                bool late = card.Id == CardId.A || card.Id == CardId.L;
                result += late && recoverLate && !IsNaturallyDue(card.Id) && !DefaultCheckWhetherBotWillBeBanished(card)
                    ? 25 : DiscardScore(card);
            }
            return result;
        }
        private IList<ClientCard> SelectDiscards(IList<ClientCard> cards, int min, int max, bool cancelable)
        {
            int count = Math.Max(1, min);
            if (count > 2 || cards.Count < count)
                return Pick(cards, cards.OrderBy(DiscardScore), min, max, cancelable, false);
            // Charity's two discards are one decision. Independently ranking each
            // card could incorrectly spend both a letter and its sole recovery.
            List<ClientCard> best = null;
            double bestScore = double.MaxValue;
            for (int i = 0; i < cards.Count; i++)
            {
                int start = count == 1 ? i : i + 1;
                int end = count == 1 ? i + 1 : cards.Count;
                for (int j = start; j < end; j++)
                {
                    var batch = count == 1 ? new List<ClientCard> { cards[i] } : new List<ClientCard> { cards[i], cards[j] };
                    double score = DiscardBatchScore(batch);
                    if (score < bestScore) { best = batch; bestScore = score; }
                }
            }
            return Pick(cards, best ?? cards.OrderBy(DiscardScore).Take(count).ToList(), min, max, cancelable, false);
        }
        private double SearchScore(ClientCard c)
        {
            if (c.Location == CardLocation.Deck && !DefaultCheckWhetherBotCanSearch()) return -10000;
            if (c.Id == CardId.Board) return !HasBoard && !HasHand(CardId.Board) ? 200 : 5;
            if (c.Id == CardId.Ruler) return !HasHand(CardId.Ruler) && !HasMonster(CardId.Ruler) ? 140 : 40;
            if (c.Id == CardId.Zorc) return !HasHand(CardId.Zorc) ? 85 : 20;
            if (c.Id == CardId.Curse) return Bot.Banished.Count(IsFiend) >= 3 ? 160 : 55;
            if (c.Id == CardId.Necrofear) return GraveFiends >= 6 ? 100 : 45;
            if (c.Id == CardId.TourGuide) return !_normalUsed ? 130 : 70;
            if (c.Id == CardId.Rhino) return 90;
            if (c.Id == CardId.Malice || c.Id == CardId.Banishment) return 70;
            return 30;
        }
        private double MillScore(ClientCard c)
        {
            if (c.Id == CardId.Rhino && !Used(CardId.Rhino, 0)) return 200;
            if (c.Id == CardId.Ruler && !Bot.Graveyard.Any(x => x.Id == CardId.Ruler)
                && (HasHand(CardId.Reborn) || HasHand(CardId.Malice) || HasHand(CardId.Banishment))) return 170;
            if (c.Id == CardId.Malice && !Bot.Graveyard.Any(x => x.Id == CardId.Malice)) return 130;
            if (c.Id == CardId.Banishment && !Bot.Graveyard.Any(x => x.Id == CardId.Banishment)) return 120;
            if (c.Id == CardId.Zorc && !Bot.Graveyard.Any(x => x.Id == CardId.Zorc)) return 110;
            if (c.Id == CardId.Curse) return 80;
            return IsFiend(c) ? 40 : 0;
        }
        private double RevivalScore(ClientCard c)
        {
            if (IsLetter(c) || _fiendLock && !IsFiend(c)) return -10000;
            if (c.Id == CardId.Necrofear && c.ProcCompleted == 0) return -10000;
            if (c.Id == CardId.Ruler) return 160;
            if (c.Id == CardId.Zorc) return Enemy.GetMonsterCount() > 0 && !Used(CardId.Zorc, 2) ? 180 : 115;
            if (c.Id == CardId.Curse) return 125;
            if (c.Id == CardId.Rhino && Bot.GetMonsterCount() > 0) return 85;
            if (c.Id == CardId.Octhros) return 70;
            return c.Attack / 50.0 + (c.HasType(CardType.Link) ? 40 : 0);
        }
        private double BanishCost(ClientCard c)
        {
            if (c.Id == CardId.TourGuide || c.Id == CardId.Octhros) return 0;
            if (c.Id == CardId.Rhino) return 5;
            if (c.Id == CardId.Necrofear && c.ProcCompleted == 0) return 10;
            if (c.Id == CardId.Banishment || c.Id == CardId.Malice) return 85;
            if (c.Id == CardId.Ruler || c.Id == CardId.Curse || c.Id == CardId.Zorc) return 95;
            return 30;
        }
        private IList<ClientCard> Pick(IList<ClientCard> offered, IEnumerable<ClientCard> ordered, int min, int max, bool cancelable, bool many)
        {
            var result = ordered.Where(c => c != null && offered.Contains(c)).Distinct().Take(many ? max : Math.Max(1, min)).ToList();
            if (result.Count < min)
            {
                if (cancelable) return new List<ClientCard>();
                // Mandatory prompts still need a legal answer. This fallback is
                // offered-only and preserves messages until no legal choice remains.
                result.AddRange(offered.Where(c => !result.Contains(c)).OrderBy(c => IsBoardPiece(c) ? 1 : 0).Take(min - result.Count));
            }
            return result;
        }
        private IList<ClientCard> SelectMaterials(IList<ClientCard> cards, int min, int max, bool cancelable)
        {
            var preferred = _materials.Where(cards.Contains).ToList();
            if (preferred.Count < min && cancelable)
            {
                Logger.DebugWriteLine("[DestinyBoard] retire material plan " + _pendingSummon + " offered="
                    + string.Join(",", cards.Select(c => c.Id)));
                _failedExtraSummons.Add(_pendingSummon);
                _pendingSummon = 0;
                _materials.Clear();
                return new List<ClientCard>();
            }
            return Pick(cards, preferred, min, max, cancelable, true);
        }
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            int source = PromptSource();
            if (hint == HintMsg.Discard && cards.All(c => c.Controller == 0 && c.Location == CardLocation.Hand))
                return SelectDiscards(cards, min, max, cancelable);
            if (hint == HintMsg.LinkMaterial || hint == HintMsg.XyzMaterial)
            {
                if (_pendingSummon == 0) return base.OnSelectCard(cards, min, max, hint, cancelable);
                return SelectMaterials(cards, min, max, cancelable);
            }
            if (source == 0) return base.OnSelectCard(cards, min, max, hint, cancelable);
            if (_replacementId != 0)
            {
                int replacement = _replacementId;
                _replacementId = 0;
                return Pick(cards, cards.Where(c => !IsBoardPiece(c)).OrderBy(c => replacement == CardId.Dingirsu ? BanishCost(c) : MaterialCost(c)), min, max, cancelable, false);
            }
            if (hint == HintMsg.Discard)
                return SelectDiscards(cards, min, max, cancelable);
            if (hint == HintMsg.RemoveXyz)
                return Pick(cards, cards.OrderBy(BanishCost), min, max, cancelable, false);
            if (source == CardId.Board || source == CardId.Sentence && cards.All(IsLetter))
            {
                // Sentence never advances the Board's own counter. Place the
                // latest missing letter so I/N remain available to natural ticks.
                return Pick(cards, cards.OrderBy(c => OnFieldLetter(c.Id) ? 100 : 0)
                    .ThenBy(c => source == CardId.Sentence && LetterCount < 3 && IsNaturallyDue(c.Id) ? 1 : 0)
                    .ThenByDescending(c => Array.IndexOf(Letters, c.Id)), min, max, cancelable, false);
            }
            if (source == CardId.Charity) return SelectDiscards(cards, min, max, cancelable);
            if (source == CardId.Mastery)
            {
                if (hint == HintMsg.ToDeck || cards.All(IsBoardPiece))
                {
                    if (hint == HintMsg.AddToHand) return Pick(cards, cards.OrderByDescending(SearchScore), min, max, cancelable, false);
                    var distinct = cards.Where(c => c.Id != CardId.Board || HasBoard || Bot.Hand.Count(x => x.Id == CardId.Board) > 1)
                        .GroupBy(c => c.Id).Select(g => g.OrderBy(c => c.Location == CardLocation.Grave ? 0 : 1).First());
                    return Pick(cards, distinct, min, max, cancelable, true);
                }
                return Pick(cards, cards.OrderByDescending(SearchScore), min, max, cancelable, false);
            }
            if (source == CardId.TourGuide)
            {
                return Pick(cards, cards.OrderByDescending(c => c.Id == CardId.Octhros && !HasHand(CardId.Ruler) ? 200 : c.Id == CardId.Rhino ? 160 : c.Id == CardId.Octhros ? 150 : 20), min, max, cancelable, false);
            }
            if (source == CardId.Rhino || source == CardId.Foolish || source == CardId.Cherubini && hint == HintMsg.ToGrave)
                return Pick(cards, cards.OrderByDescending(MillScore), min, max, cancelable, false);
            if (source == CardId.Cherubini)
                return Pick(cards, cards.OrderBy(c => c.Controller == 0 && c.Id == CardId.Cherubini ? 0 : 1), min, max, cancelable, false);
            if (source == CardId.Octhros || source == CardId.Sentence || source == CardId.Dharc && hint == HintMsg.AddToHand)
                return Pick(cards, cards.OrderByDescending(SearchScore), min, max, cancelable, source == CardId.Sentence);
            if (source == CardId.Zorc)
            {
                if (hint == HintMsg.AddToHand) return Pick(cards, cards.OrderByDescending(SearchScore), min, max, cancelable, false);
                if (cards.Any(c => c.Controller == 1))
                {
                    var selected = Pick(cards, cards.OrderByDescending(Threat), min, max, cancelable, false);
                    _zorcSelected = selected.FirstOrDefault();
                    return selected;
                }
                var own = cards.OrderBy(c => c.Id == CardId.Ruler && CanDestroyRuler() ? -100
                    : IsLetter(c) && c.Location == CardLocation.MonsterZone ? -50 : IsBoardPiece(c) ? 10000 : MaterialCost(c));
                var pick = Pick(cards, own, min, max, cancelable, false);
                _intentionalRulerDestruction = pick.Any(c => c.Id == CardId.Ruler);
                return pick;
            }
            if (source == CardId.Ruler) return Pick(cards, cards.OrderByDescending(Threat), min, max, cancelable, false);
            if (source == CardId.Necrofear || source == CardId.Shield)
                return Pick(cards, hint == HintMsg.Control ? cards.OrderByDescending(Threat) : cards.OrderBy(BanishCost), min, max, cancelable, false);
            if (source == CardId.Curse)
                return Pick(cards, hint == HintMsg.Destroy ? cards.OrderByDescending(Threat) : cards.OrderByDescending(SearchScore), min, max, cancelable, hint == HintMsg.Destroy);
            if (source == CardId.Banishment || source == CardId.Malice || source == CardId.Reborn || source == CardId.Muckraker || source == CardId.Dharc)
                return Pick(cards, cards.OrderByDescending(RevivalScore), min, max, cancelable, false);
            if (source == CardId.Rage && hint == HintMsg.Destroy)
            {
                var result = Pick(cards, cards.Where(c => !IsLetter(c)).OrderBy(c => c.Id == CardId.Ruler && CanDestroyRuler() ? -100 : MaterialCost(c)), min, max, cancelable, false);
                _intentionalRulerDestruction = result.Any(c => c.Id == CardId.Ruler);
                return result;
            }
            if (source == CardId.LittleKnight)
            {
                var enemies = cards.Where(c => c.Controller == 1).OrderByDescending(Threat).ToList();
                if (enemies.Count > 0) return Pick(cards, enemies, min, max, cancelable, false);
                return Pick(cards, cards.Where(c => !IsLetter(c)).OrderBy(c => c.Id == CardId.LittleKnight ? -20 : MaterialCost(c)), min, max, cancelable, false);
            }
            if (source == CardId.Hope) return Pick(cards, cards.OrderByDescending(c => c.Attack), min, max, cancelable, false);
            if (ExtraIds.Contains(source))
                return Pick(cards, cards.Where(c => c.Controller == 1).OrderByDescending(Threat)
                    .Concat(cards.Where(c => c.Controller == 0 && !IsBoardPiece(c)).OrderBy(MaterialCost)), min, max, cancelable, false);
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }
        public override IList<ClientCard> OnSelectLinkMaterial(IList<ClientCard> cards, int min, int max)
        {
            return _pendingSummon != 0 ? SelectMaterials(cards, min, max, false) : base.OnSelectLinkMaterial(cards, min, max);
        }
        public override IList<ClientCard> OnSelectXyzMaterial(IList<ClientCard> cards, int min, int max)
        {
            return _pendingSummon != 0 ? SelectMaterials(cards, min, max, false) : base.OnSelectXyzMaterial(cards, min, max);
        }
        public override int OnSelectOption(IList<int> options)
        {
            int source = PromptSource();
            int wanted = -1;
            if (source == CardId.Dingirsu) wanted = Util.GetStringId(CardId.Dingirsu, 1);
            if (source == CardId.Machinex) wanted = Util.GetStringId(CardId.Machinex, 5);
            if (source == CardId.Mastery) return 0;
            int index = options.IndexOf(wanted);
            return index >= 0 ? index : base.OnSelectOption(options);
        }
        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.Sanctuary, 0)) return FreeMonsters > 0;
            if (desc == Util.GetStringId(CardId.Zorc, 3)) return FreeMonsters > 0 && _zorcSelected != null;
            if (desc == Util.GetStringId(CardId.Ruler, 2) || desc == Util.GetStringId(CardId.Curse, 2)) return true;
            return base.OnSelectYesNo(desc);
        }
        public override IList<ClientCard> OnCardSorting(IList<ClientCard> cards)
        {
            if (PromptSource() == CardId.Mastery) return cards.OrderBy(c => c.Id == CardId.Board ? -1 : Array.IndexOf(Letters, c.Id)).ToList();
            return base.OnCardSorting(cards);
        }
        public override int OnSelectPlace(int cardId, int player, CardLocation location, int available)
        {
            if (player != 0) return base.OnSelectPlace(cardId, player, location, available);
            if (location == CardLocation.MonsterZone)
            {
                // Keep the central lane free for control/revival; Ruler avoids
                // Cherubini's lower diagonals in both possible Extra Monster Zones.
                int[] zones = Letters.Contains(cardId) ? new[] { 0, 4, 1, 3, 2, 5, 6 }
                    : cardId == CardId.Ruler ? new[] { 0, 4, 2, 1, 3, 5, 6 }
                    : ExtraIds.Contains(cardId) && LinkRating(cardId) == 0 ? new[] { 2, 1, 3, 0, 4, 5, 6 }
                    : new[] { 5, 6, 2, 1, 3, 0, 4 };
                int linked = 0;
                if (cardId == CardId.Ruler)
                    foreach (ClientCard cherubini in Bot.GetMonsters().Where(c => c.Id == CardId.Cherubini && !c.IsDisabled())) linked |= cherubini.GetLinkedZones();
                foreach (int zone in zones) if ((available & (1 << zone)) != 0 && (linked & (1 << zone)) == 0) return 1 << zone;
                foreach (int zone in zones) if ((available & (1 << zone)) != 0) return 1 << zone;
            }
            if (location == CardLocation.SpellZone)
                foreach (int zone in new[] { 2, 0, 4, 1, 3, 5 }) if ((available & (1 << zone)) != 0) return 1 << zone;
            return base.OnSelectPlace(cardId, player, location, available);
        }
        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if ((Letters.Contains(cardId) || cardId == CardId.Rhino || cardId == CardId.Octhros || cardId == CardId.TourGuide)
                && positions.Contains(CardPosition.FaceUpDefence)) return CardPosition.FaceUpDefence;
            return positions.Contains(CardPosition.FaceUpAttack) ? CardPosition.FaceUpAttack : base.OnSelectPosition(cardId, positions);
        }
        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (attacker.Controller == 0 && IsLetter(attacker)) return false;
            if (attacker.Controller == 0 && CanUseDariusBattle(attacker, defender))
            {
                attacker.RealPower = defender.RealPower + 1;
                return true;
            }
            return base.OnPreBattleBetween(attacker, defender);
        }
        public override BattlePhaseAction OnBattle(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            var useful = attackers.Where(c => !IsLetter(c)).OrderByDescending(c => c.Attack).ToList();
            if (useful.Count == 0)
            {
                if (Duel.BattlePhase.CanMainPhaseTwo) return AI.ToMainPhase2();
                if (Duel.BattlePhase.CanEndPhase) return AI.ToEndPhase();
                return null;
            }
            if (defenders.Count == 0) return AI.Attack(useful[0], null);
            return base.OnBattle(attackers, defenders);
        }
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            return IsLetter(attacker) ? null : base.OnSelectAttackTarget(attacker, defenders);
        }
        public override void OnChaining(int player, ClientCard card)
        {
            if (player == 0 && card != null)
            {
                ChainInfo chain = Duel.CurrentChainInfo.LastOrDefault();
                if (chain != null)
                {
                    int effect = EffectIndex(card.Id, chain.ActivateDescription);
                    if (effect >= 0) _used.Add(((long)card.Id << 4) | (uint)(card.Id == CardId.Sentence ? 0 : effect));
                }
            }
            // An accepted action owns only its pre-CHAINING cost/target window.
            // Once any chain link is announced, immutable chain context takes over.
            _acceptedId = 0;
            base.OnChaining(player, card);
        }
        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo chain = chainIndex > 0 && chainIndex <= Duel.CurrentChainInfo.Count ? Duel.CurrentChainInfo[chainIndex - 1] : null;
            if (chain != null && chain.ActivatePlayer == 0 && chain.ActivateId == CardId.Muckraker
                && !Duel.NegatedChainIndexList.Contains(chainIndex)) _fiendLock = true;
            base.OnChainSolved(chainIndex);
        }
        public override void OnMove(ClientCard card, int previousControler, int previousLocation, int currentControler, int currentLocation)
        {
            _failedExtraSummons.Clear();
            if (card != null)
            {
                ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
                if (IsLetter(card) && currentControler == 0 && ((currentLocation & ((int)CardLocation.MonsterZone | (int)CardLocation.SpellZone)) != 0)
                    && solving != null && solving.ActivatePlayer == 0 && solving.ActivateId == CardId.Board && solving.RelatedCard != null)
                {
                    int progress;
                    _boardProgress.TryGetValue(solving.RelatedCard, out progress);
                    _boardProgress[solving.RelatedCard] = Math.Min(4, progress + 1);
                    Logger.DebugWriteLine("[DestinyBoard] natural letter=" + card.Id + " progress=" + (progress + 1));
                }
                if (card.Id == CardId.Board && (currentControler != 0 || currentLocation != (int)CardLocation.SpellZone)) _boardProgress.Remove(card);
                if (card.Id == CardId.Muckraker && currentControler == 0 && currentLocation == (int)CardLocation.MonsterZone
                    && previousLocation == (int)CardLocation.Extra) _muckrakerSummoned.Add(card);
                if (solving != null && solving.ActivatePlayer == 0 && solving.ActivateId == CardId.Ruler && previousControler == 1 && currentControler == 0) _temporaryControl.Add(card);
                if (currentControler != 0 || currentLocation != (int)CardLocation.MonsterZone) _temporaryControl.Remove(card);
            }
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
        }
        public override void OnSpSummoning()
        {
            _pendingSummon = 0;
            _materials.Clear();
            base.OnSpSummoning();
        }
        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            _failedExtraSummons.Clear();
            if (card != null && card.Id == CardId.Board && (currentPosition & (int)CardPosition.FaceDown) != 0) _boardProgress.Remove(card);
            base.OnPosChange(card, previousPosition, currentPosition);
        }
        public override void OnChainEnd()
        {
            _failedExtraSummons.Clear();
            _acceptedId = 0;
            _replacementId = 0;
            _intentionalRulerDestruction = false;
            _zorcSelected = null;
            base.OnChainEnd();
        }
        public override void OnNewPhase()
        {
            _failedExtraSummons.Clear();
            _pendingSummon = 0;
            _materials.Clear();
            _acceptedId = 0;
            _replacementId = 0;
            base.OnNewPhase();
        }
        public override void OnNewTurn()
        {
            _failedExtraSummons.Clear();
            Logger.DebugWriteLine("DestinyBoard turn=" + Duel.Turn + " player=" + Duel.Player + " first=" + Duel.IsFirst);
            _used.Clear();
            _muckrakerSummoned.Clear();
            _temporaryControl.Clear();
            _materials.Clear();
            _normalUsed = false;
            _fiendLock = false;
            _acceptedId = 0;
            _pendingSummon = 0;
            _replacementId = 0;
            _intentionalRulerDestruction = false;
            base.OnNewTurn();
        }
    }
}
