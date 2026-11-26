using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

// Synthetic offered-action and callback checks. These do not execute Lua,
// establish core legality, or substitute for native combo/Arena results.
internal static class DestinyBoardChecks
{
    private const int Board = 94212438, Sanctuary = 16625614, Sentence = 40771118, Mastery = 14386013;
    private const int LetterI = 31893528, LetterN = 67287533, LetterA = 94772232, LetterL = 30170981;
    private const int Ruler = 100268007, Zorc = 73320394, Rhino = 81035362, Tour = 10802915, Doomdog = 58616392;
    private const int Necrofear = 31829185, Curse = 14509651, Malice = 87988305, Banishment = 51993760;
    private const int Cherubini = 58699500, Muckraker = 71607202, Dharc = 8264361, Phoenix = 2857636;
    private const int Unicorn = 38342335, Rage = 21419436, Abomination = 29479265, Goddess = 98127546;
    private const int Darius = 51497409, Machinex = 46593546, Dingirsu = 93854893, Hope = 63767246, LittleKnight = 29301450;
    private const int Pot = 55144522, Charity = 79571449, Ash = 14558127, Baronne = 84815190;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Type ExecutorClass = typeof(Executor).Assembly.GetType("WindBot.Game.AI.Decks.DestinyBoardExecutor");
    private static readonly List<int> MainDeck = new List<int>();
    private static readonly List<int> Extra = new List<int>();
    private static readonly List<Dictionary<string, object>> Results = new List<Dictionary<string, object>>();

    private sealed class Fixture
    {
        internal readonly Duel Duel = new Duel();
        internal readonly GameAI AI;
        internal readonly Executor Executor;
        internal ClientField Bot { get { return Duel.Fields[0]; } }

        internal Fixture()
        {
            Bot.Init(MainDeck.Count, 0, 0);
            Duel.Fields[1].Init(40, 0, 1);
            Bot.SetInitialDeck(MainDeck.Select(NamedCard.Get));
            foreach (int id in Extra) Bot.ExtraDeck.Add(Card(id, CardLocation.Extra));
            Duel.Turn = 1;
            Duel.Player = 0;
            Duel.Phase = DuelPhase.Main1;
            Duel.IsNewRule = true;
            Duel.IsNewRule2020 = true;
            AI = new GameAI(new GameClient(new WindBotInfo { Chat = false, Deck = "DestinyBoard", Dialog = "default" }), Duel);
            Executor = (Executor)Activator.CreateInstance(ExecutorClass, AI, Duel);
            AI.Executor = Executor;
            Executor.Deck = "AI_DestinyBoard";
            AI.OnNewTurn();
            AI.OnNewPhase();
        }

        internal ClientCard Add(int id, CardLocation location, int controller = 0, int slot = -1)
        {
            ClientCard card = Card(id, location);
            card.Controller = controller;
            var field = Duel.Fields[controller];
            if (location == CardLocation.Hand) field.Hand.Add(card);
            else if (location == CardLocation.Grave) field.Graveyard.Add(card);
            else if (location == CardLocation.Removed) field.Banished.Add(card);
            else if (location == CardLocation.MonsterZone)
            {
                if (slot < 0) slot = Array.FindIndex(field.MonsterZone, value => value == null);
                card.Sequence = slot;
                field.MonsterZone[slot] = card;
            }
            else if (location == CardLocation.SpellZone)
            {
                if (slot < 0) slot = id == Sanctuary ? 5 : Array.FindIndex(field.SpellZone, value => value == null);
                card.Sequence = slot;
                field.SpellZone[slot] = card;
            }
            if (controller == 0 && MainDeck.Contains(id))
            {
                typeof(ClientField).GetMethod("TrackRemoveFromDeck", Flags).Invoke(Bot, new object[] { id });
                if (Bot.Deck.Count > 0) Bot.Deck.RemoveAt(Bot.Deck.Count - 1);
            }
            return card;
        }

        internal void Chain(ClientCard card, int description, bool resolving = false)
        {
            Duel.CurrentChain.Clear();
            Duel.CurrentChainInfo.Clear();
            AppendChain(card, description);
            Duel.SolvingChainIndex = resolving ? 1 : 0;
        }

        internal void AppendChain(ClientCard card, int description)
        {
            Duel.CurrentChain.Add(card);
            Duel.CurrentChainInfo.Add(new ChainInfo(card, card.Controller, description));
            Duel.LastChainPlayer = card.Controller;
            Duel.LastChainLocation = card.Location;
        }

        internal bool Activate(ClientCard card, int offset)
        {
            bool accepted = AI.OnSelectEffectYn(card, Desc(card.Id, offset));
            if (accepted)
            {
                AppendChain(card, Desc(card.Id, offset));
                AI.OnChaining(card, card.Controller);
            }
            return accepted;
        }

        internal void EndChain(bool negated = false)
        {
            if (Duel.CurrentChainInfo.Count > 0)
            {
                Duel.SolvingChainIndex = Duel.CurrentChainInfo.Count;
                if (negated) Duel.NegatedChainIndexList.Add(Duel.SolvingChainIndex);
                AI.OnChainSolved(Duel.SolvingChainIndex);
            }
            AI.OnChainEnd();
            Duel.CurrentChain.Clear();
            Duel.CurrentChainInfo.Clear();
            Duel.NegatedChainIndexList.Clear();
            Duel.SolvingChainIndex = 0;
            Duel.LastChainPlayer = -1;
        }

        internal IList<ClientCard> Select(int hint, int min, int max, params ClientCard[] offered)
        {
            IList<ClientCard> selected = AI.OnSelectCard(offered, min, max, hint, false);
            Require(selected != null && selected.Count >= min && selected.Count <= max,
                "Selection count violated the offered bounds");
            Require(selected.Distinct().Count() == selected.Count && selected.All(offered.Contains),
                "Selection did not contain distinct offered objects");
            return selected;
        }

        internal ClientCard Select(int hint, params ClientCard[] offered)
        {
            return Select(hint, 1, 1, offered)[0];
        }

        internal bool Summon(int id)
        {
            var main = new MainPhase { CanEndPhase = true };
            ClientCard candidate = Bot.ExtraDeck.First(card => card.Id == id);
            main.SpecialSummonableCards.Add(candidate);
            Duel.MainPhase = main;
            return AI.OnSelectIdleCmd(main).Action == MainPhaseAction.MainAction.SpSummon;
        }

        internal int Queued
        {
            get { return ((ICollection)typeof(GameAI).GetField("m_selector", Flags).GetValue(AI)).Count; }
        }
    }

    private static ClientCard Card(int id, CardLocation location)
    {
        var card = new ClientCard(id, location, 0, (int)CardPosition.FaceUpAttack) { Controller = 0 };
        Require(card.Data != null, "Missing card database entry " + id);
        foreach (string name in new[] { "Type", "Level", "Race", "Attribute", "Attack", "Defense" })
        {
            object value = card.Data.GetType().GetProperty(name, Flags).GetValue(card.Data, null);
            typeof(ClientCard).GetProperty(name, Flags).SetValue(card, value, null);
        }
        Set(card, "BaseAttack", card.Attack);
        Set(card, "BaseDefense", card.Defense);
        if (card.HasType(CardType.Xyz)) { Set(card, "Rank", card.Level); Set(card, "Level", 0); }
        if (card.HasType(CardType.Link))
        {
            Set(card, "LinkCount", card.Level);
            Set(card, "LinkMarker", card.Data.LinkMarker);
            Set(card, "Level", 0);
        }
        return card;
    }

    private static void Set(ClientCard card, string property, int value)
    {
        typeof(ClientCard).GetProperty(property, Flags).SetValue(card, value, null);
    }

    private static void MessageMonster(ClientCard card)
    {
        Set(card, "Type", (int)(CardType.Monster | CardType.Normal));
        Set(card, "Race", (int)CardRace.Fiend);
        Set(card, "Level", 1);
        Set(card, "Attack", 0);
        Set(card, "Defense", 0);
    }

    private static void UnknownBackrow(Fixture fixture)
    {
        fixture.Duel.Fields[1].SpellZone[0] = new ClientCard(0, CardLocation.SpellZone, 0,
            (int)CardPosition.FaceDownDefence) { Controller = 1 };
    }

    private static int Desc(int id, int offset) { return id * 16 + offset; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Is(ClientCard actual, ClientCard expected, string detail)
    {
        Require(actual == expected, detail + "; expected " + expected.Id + ", got " + actual.Id);
    }

    private static void Run(string name, Action test)
    {
        bool passed = false;
        string detail = "passed";
        try { test(); passed = true; }
        catch (Exception error) { detail = (error.InnerException ?? error).Message; }
        Results.Add(new Dictionary<string, object> { { "name", name }, { "passed", passed }, { "detail", detail } });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " + detail);
    }

    private static void Effects()
    {
        Run("Zorc discards Rhino while preserving the next Spirit Message", () =>
        {
            var f = new Fixture();
            var zorc = f.Add(Zorc, CardLocation.Hand);
            var letter = f.Add(LetterI, CardLocation.Hand);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            Require(f.Activate(zorc, 0), "Zorc search rejected");
            Is(f.Select(HintMsg.Discard, letter, rhino), rhino, "Zorc lost its graveyard setup");
        });
        Run("Mandatory Doomdog resolution searches summonable Ruler without an activation flag", () =>
        {
            var f = new Fixture();
            var dog = f.Add(Doomdog, CardLocation.Grave);
            f.Add(Tour, CardLocation.Grave);
            f.Add(Rhino, CardLocation.Grave);
            f.Chain(dog, Desc(Doomdog, 0), true);
            var ruler = Card(Ruler, CardLocation.Deck);
            Is(f.Select(HintMsg.AddToHand, Card(Necrofear, CardLocation.Deck), ruler), ruler,
                "Mandatory search ignored the available Ruler summon");
        });
        Run("Sentence acceleration preserves I and N and places L first", () =>
        {
            var f = new Fixture();
            var sentence = f.Add(Sentence, CardLocation.Grave);
            f.Add(Board, CardLocation.SpellZone);
            f.Add(Sanctuary, CardLocation.SpellZone);
            f.Chain(sentence, Desc(Sentence, 1), true);
            var last = Card(LetterL, CardLocation.Deck);
            Is(f.Select(HintMsg.ToField, Card(LetterI, CardLocation.Deck), Card(LetterN, CardLocation.Deck),
                Card(LetterA, CardLocation.Deck), last), last, "Sentence blocked natural letter progression");
        });
        Run("Sentence next acceleration chooses missing A instead of duplicate L", () =>
        {
            var f = new Fixture();
            var sentence = f.Add(Sentence, CardLocation.Grave);
            f.Add(Board, CardLocation.SpellZone);
            f.Add(LetterL, CardLocation.SpellZone);
            f.Chain(sentence, Desc(Sentence, 1), true);
            var a = Card(LetterA, CardLocation.Deck);
            Is(f.Select(HintMsg.ToField, Card(LetterI, CardLocation.Deck), a, Card(LetterN, CardLocation.Deck)), a,
                "Sentence failed to choose the last missing late letter");
        });
        Run("Ruler control selection belongs to resolving link rather than newest chain", () =>
        {
            var f = new Fixture();
            var ruler = f.Add(Ruler, CardLocation.Hand);
            var low = f.Add(Ash, CardLocation.MonsterZone, 1);
            var high = f.Add(Baronne, CardLocation.MonsterZone, 1);
            f.Chain(ruler, Desc(Ruler, 1));
            f.AppendChain(low, Desc(Ash, 0));
            f.Duel.SolvingChainIndex = 1;
            f.Executor.SetCard(ExecutorType.Activate, low, Desc(Ash, 0));
            Is(f.Select(HintMsg.Control, low, high), high, "Ruler used stale candidate/latest-chain context");
        });
        Run("Zorc adverse die result preserves Board while Ruler offers a payoff", () =>
        {
            var f = new Fixture();
            var zorc = f.Add(Zorc, CardLocation.MonsterZone);
            var board = f.Add(Board, CardLocation.SpellZone);
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            f.Add(Baronne, CardLocation.MonsterZone, 1);
            f.Chain(zorc, Desc(Zorc, 2), true);
            Is(f.Select(HintMsg.Destroy, board, zorc, ruler), ruler, "Adverse roll broke the Board instead of triggering Ruler");
        });
        Run("Muckraker target prompt precedes its resolution discard", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone);
            var ruler = f.Add(Ruler, CardLocation.Grave);
            var dog = f.Add(Doomdog, CardLocation.Grave);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            var letter = f.Add(LetterI, CardLocation.Hand);
            Require(f.Activate(muck, 0), "Useful Muckraker revival rejected");
            Is(f.Select(HintMsg.SpSummon, dog, ruler), ruler, "Revival target was confused with discard");
            f.Duel.SolvingChainIndex = 1;
            Is(f.Select(HintMsg.Discard, letter, rhino), rhino, "Resolution discard consumed a required letter");
            Require(f.Queued == 0, "Muckraker left a duplicated target/discard selector");
        });
        Run("Cherubini mill and boost are separate prompts", () =>
        {
            var f = new Fixture();
            var cherubini = f.Add(Cherubini, CardLocation.MonsterZone, 0, 5);
            f.Add(Ruler, CardLocation.Hand);
            Require(f.Activate(cherubini, 0), "Cherubini setup rejected");
            var rhino = Card(Rhino, CardLocation.Deck);
            Is(f.Select(HintMsg.ToGrave, Card(Tour, CardLocation.Deck), rhino), rhino, "Cherubini failed to mill Rhino");
            Is(f.Select(HintMsg.Faceup, cherubini), cherubini, "Cherubini lost its boost target");
            Require(f.Queued == 0, "Cherubini left a duplicate selector");
        });
        Run("S:P quick effect preserves Ruler across two separate REMOVE prompts", () =>
        {
            var f = new Fixture();
            var sp = f.Add(LittleKnight, CardLocation.MonsterZone);
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            var enemy = f.Add(Baronne, CardLocation.MonsterZone, 1);
            f.Chain(enemy, Desc(Baronne, 0));
            Require(f.Activate(sp, 1), "S:P response rejected");
            Is(f.Select(HintMsg.Remove, ruler, sp), sp, "S:P failed to preserve the active Ruler");
            Is(f.Select(HintMsg.Remove, ruler, enemy), enemy, "S:P second prompt consumed its own Ruler");
            Require(f.Queued == 0, "S:P left a duplicated target selector");
        });
        Run("Hope Harbinger never negates our own draw spell", () =>
        {
            var f = new Fixture();
            var hope = f.Add(Hope, CardLocation.MonsterZone);
            var pot = f.Add(Pot, CardLocation.SpellZone);
            f.Chain(pot, Desc(Pot, 0));
            Require(!f.Activate(hope, 0), "Hope Harbinger chained to its own Pot of Greed");
            Require(f.Queued == 0, "Rejected negate left a queued response");
        });
        Run("Hope Harbinger accepts an opponent field spell effect", () =>
        {
            var f = new Fixture();
            var hope = f.Add(Hope, CardLocation.MonsterZone);
            var pot = f.Add(Pot, CardLocation.SpellZone, 1);
            f.Chain(pot, Desc(Pot, 0));
            Require(f.Activate(hope, 0), "Opponent draw spell was left unopposed");
        });
        Run("Machinex keeps its monster interaction instead of entering an unusable scale", () =>
        {
            var f = new Fixture();
            f.Duel.Phase = DuelPhase.Standby;
            var machinex = f.Add(Machinex, CardLocation.MonsterZone);
            machinex.Overlays.AddRange(new[] { Darius, Tour, Doomdog });
            Require(!f.Activate(machinex, 3), "Machinex moved itself to an unusable Pendulum Zone");
        });
        Run("Machinex accepts its actual absorption description on an enemy activation", () =>
        {
            var f = new Fixture();
            var machinex = f.Add(Machinex, CardLocation.MonsterZone);
            machinex.Overlays.AddRange(new[] { Darius, Tour, Doomdog });
            var enemy = f.Add(Baronne, CardLocation.MonsterZone, 1);
            f.Chain(enemy, Desc(Baronne, 0));
            Require(f.Activate(machinex, 2), "Machinex absorption rejected");
            f.Duel.SolvingChainIndex = 2;
            var selected = f.Select(HintMsg.RemoveXyz, 2, 2, Card(Darius, CardLocation.Overlay),
                Card(Tour, CardLocation.Overlay), Card(Doomdog, CardLocation.Overlay));
            Require(selected.Count == 2, "Machinex did not pay its two-material resolution requirement");
        });
        Run("Dingirsu chooses send-to-grave by offered option identity", () =>
        {
            var f = new Fixture();
            var dingirsu = f.Add(Dingirsu, CardLocation.MonsterZone);
            f.Add(Baronne, CardLocation.MonsterZone, 1);
            f.Chain(dingirsu, -1);
            int option = f.AI.OnSelectOption(new[] { Desc(Dingirsu, 2), Desc(Dingirsu, 1) });
            Require(option == 1, "Dingirsu assumed option index zero meant removal");
        });
        Run("Dingirsu mandatory-style resolution removes an opposing card without activation flags", () =>
        {
            var f = new Fixture();
            var dingirsu = f.Add(Dingirsu, CardLocation.MonsterZone);
            var low = f.Add(Ash, CardLocation.MonsterZone, 1);
            var high = f.Add(Baronne, CardLocation.MonsterZone, 1);
            f.Chain(dingirsu, -1, true);
            Is(f.Select(HintMsg.ToGrave, low, high), high, "Dingirsu lacked a resolution-owned removal choice");
        });
        Run("Rage Abominator cannot destroy our activating monster through its grave effect", () =>
        {
            var f = new Fixture();
            var rage = f.Add(Rage, CardLocation.Grave);
            f.Add(Abomination, CardLocation.MonsterZone);
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            f.Chain(ruler, Desc(Ruler, 0));
            Require(!f.Activate(rage, 1), "Rage grave effect destroyed our own activating monster");
        });
        Run("Rage destruction selection does not sacrifice an immune message instead of Ruler", () =>
        {
            var f = new Fixture();
            var rage = f.Add(Rage, CardLocation.MonsterZone);
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            var letter = f.Add(LetterL, CardLocation.MonsterZone);
            MessageMonster(letter);
            f.Add(Sanctuary, CardLocation.SpellZone);
            f.Add(Baronne, CardLocation.MonsterZone, 1);
            f.Chain(rage, Desc(Rage, 0), true);
            Is(f.Select(HintMsg.Destroy, letter, rage, ruler), ruler, "Rage chose a message that cannot pay its destruction requirement");
        });
        Run("Link material selection preserves the active message when expendable bodies suffice", () =>
        {
            var f = new Fixture();
            var letter = f.Add(LetterI, CardLocation.MonsterZone);
            MessageMonster(letter);
            f.Add(Board, CardLocation.SpellZone);
            var tour = f.Add(Tour, CardLocation.MonsterZone);
            var dog = f.Add(Doomdog, CardLocation.MonsterZone);
            f.Add(Pot, CardLocation.SpellZone, 1);
            f.Add(Rhino, CardLocation.Hand);
            Require(f.Summon(Phoenix), "Useful Phoenix summon rejected");
            var chosen = f.Select(HintMsg.LinkMaterial, 2, 2, letter, tour, dog);
            Require(!chosen.Contains(letter), "Link material fallback consumed a Spirit Message");
        });
        Run("Unicorn refuses a removal line whose only target is our Board package", () =>
        {
            var f = new Fixture();
            var unicorn = f.Add(Unicorn, CardLocation.MonsterZone);
            f.Add(Board, CardLocation.SpellZone);
            f.Add(LetterI, CardLocation.SpellZone);
            f.Add(Rhino, CardLocation.Hand);
            Require(!f.Activate(unicorn, 0), "Unicorn activated with no worthwhile enemy target");
            Require(f.Queued == 0, "Rejected Unicorn effect left selectors");
        });
        Run("Abomination declines an optional pop that only breaks our Board", () =>
        {
            var f = new Fixture();
            var abomination = f.Add(Abomination, CardLocation.MonsterZone);
            f.Add(Board, CardLocation.SpellZone);
            f.Add(LetterL, CardLocation.SpellZone);
            f.Duel.Phase = DuelPhase.End;
            Require(!f.Activate(abomination, 2), "Abomination destroyed its own winning engine without a payoff");
        });
        Run("Cherubini is a useful opening summon from Tour Guide and Doomdog", () =>
        {
            var f = new Fixture();
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Doomdog, CardLocation.MonsterZone);
            Require(f.Summon(Cherubini), "Opening Cherubini route rejected");
        });
        Run("Phoenix is held when no opposing backrow needs removal", () =>
        {
            var f = new Fixture();
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Rhino, CardLocation.MonsterZone);
            f.Add(Malice, CardLocation.Hand);
            Require(!f.Summon(Phoenix), "Phoenix consumed two bodies without a target or route");
        });
        Run("Dharc is held when there is no opposing DARK graveyard target", () =>
        {
            var f = new Fixture();
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Rhino, CardLocation.MonsterZone);
            Require(!f.Summon(Dharc), "Dharc consumed two bodies without a useful revival");
        });
        Run("S:P holds its optional first effect when direct attacks already win", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Duel.Fields[1].LifePoints = 2000;
            var sp = f.Add(LittleKnight, CardLocation.MonsterZone);
            f.Add(Ruler, CardLocation.MonsterZone);
            f.Add(Ash, CardLocation.Grave, 1);
            Require(!f.Activate(sp, 0), "S:P blocked an available direct lethal just to remove a graveyard card");
        });
        Run("S:P can use its first effect after the battle opportunity has passed", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Duel.Phase = DuelPhase.Main2;
            f.Duel.Fields[1].LifePoints = 2000;
            var sp = f.Add(LittleKnight, CardLocation.MonsterZone);
            f.Add(Ruler, CardLocation.MonsterZone);
            f.Add(Ash, CardLocation.Grave, 1);
            Require(f.Activate(sp, 0), "S:P unnecessarily rejected useful post-battle removal");
        });
        Run("Darius remains a fallback when Cherubini is no longer in the Extra Deck", () =>
        {
            var f = new Fixture();
            foreach (var card in f.Bot.ExtraDeck.Where(c => c.Id == Cherubini).ToArray()) f.Bot.ExtraDeck.Remove(card);
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Doomdog, CardLocation.MonsterZone);
            Require(f.Summon(Darius), "Unavailable Cherubini permanently suppressed Darius/Machinex fallback");
        });
        Run("Darius bridges to Machinex with both Rhino copies already drawn", () =>
        {
            var f = new Fixture();
            f.Add(Rhino, CardLocation.Hand);
            f.Add(Rhino, CardLocation.Hand);
            var tour = f.Add(Tour, CardLocation.MonsterZone);
            var dog = f.Add(Doomdog, CardLocation.MonsterZone);
            Require(f.Summon(Darius), "No-mill Darius route rejected");
            var materials = f.Select(HintMsg.XyzMaterial, 2, 2, dog, tour);
            Require(materials.Contains(tour) && materials.Contains(dog), "Darius lost its level-three materials");
            f.Bot.MonsterZone[tour.Sequence] = null;
            f.Bot.MonsterZone[dog.Sequence] = null;
            f.AI.OnSpSummoning();
            var darius = f.Add(Darius, CardLocation.MonsterZone);
            darius.Overlays.AddRange(new[] { Tour, Doomdog });
            Require(f.Summon(Machinex), "Darius failed to become Machinex");
            Is(f.Select(HintMsg.XyzMaterial, darius), darius, "Machinex material was not its D/D/D bridge");
        });
        Run("Muckraker Fiend lock survives a lost revival target and resets next turn", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone, 0, 5);
            var ruler = f.Add(Ruler, CardLocation.Grave);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            Require(f.Activate(muck, 0), "Muckraker activation rejected");
            Is(f.Select(HintMsg.SpSummon, ruler), ruler, "Muckraker failed to target Ruler");
            // An opponent removes the target, but the non-negated resolution still
            // discards and establishes the script's Fiend-only restriction.
            f.Bot.Graveyard.Remove(ruler);
            ruler.Location = CardLocation.Removed;
            f.Bot.Banished.Add(ruler);
            f.Duel.SolvingChainIndex = 1;
            Is(f.Select(HintMsg.Discard, rhino), rhino, "Muckraker lost the resolution discard");
            f.EndChain();
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Doomdog, CardLocation.MonsterZone);
            Require(!f.Summon(Cherubini), "Muckraker lost its lock when revival failed");
            f.Duel.Turn++;
            f.AI.OnNewTurn();
            Require(f.Summon(Cherubini), "Fiend lock incorrectly persisted into the next turn");
        });
        Run("Effect-negated Muckraker does not invent a Fiend lock", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone, 0, 5);
            f.Add(Ruler, CardLocation.Grave);
            f.Add(Rhino, CardLocation.Hand);
            Require(f.Activate(muck, 0), "Muckraker activation rejected");
            f.EndChain(true);
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Doomdog, CardLocation.MonsterZone);
            Require(f.Summon(Cherubini), "Negated effect incorrectly established a Fiend lock");
        });
        Run("Muckraker cannot be used as Link material in its Link Summon turn", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone, 0, 5);
            f.AI.OnMove(muck, 0, (int)CardLocation.Extra, 0, (int)CardLocation.MonsterZone);
            f.Add(Ruler, CardLocation.MonsterZone);
            f.Add(Malice, CardLocation.Hand);
            f.Add(Baronne, CardLocation.MonsterZone, 1);
            Require(!f.Summon(Unicorn), "Planner consumed a newly Link Summoned Muckraker");
        });
        Run("Sentence placement and recovery share their once-per-turn use", () =>
        {
            var f = new Fixture();
            var first = f.Add(Sentence, CardLocation.SpellZone);
            var second = f.Add(Sentence, CardLocation.SpellZone);
            f.Add(Board, CardLocation.SpellZone);
            f.Add(Ruler, CardLocation.Grave);
            Require(f.Activate(first, 1), "Sentence acceleration rejected");
            f.EndChain();
            Require(!f.Activate(second, 0), "Sentence allowed recovery after already placing a letter");
            Require(!f.Activate(second, 1), "Second Sentence bypassed the shared once-per-turn placement limit");
            f.Duel.Turn++;
            f.AI.OnNewTurn();
            Require(f.Activate(second, 1), "Sentence's shared use did not reset next turn");
        });
        Run("Ruler placement avoids Cherubini's effect-destruction protection when possible", () =>
        {
            var f = new Fixture();
            f.Add(Cherubini, CardLocation.MonsterZone, 0, 5);
            int available = (1 << 0) | (1 << 2) | (1 << 4);
            int place = f.Executor.OnSelectPlace(Ruler, 0, CardLocation.MonsterZone, available);
            Require(place == (1 << 4), "Ruler was placed in a protected lane despite an unlinked lane being available");
        });
        Run("Rejected Muckraker activation does not discard the only Spirit Message", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone);
            f.Add(Ruler, CardLocation.Grave);
            f.Add(LetterI, CardLocation.Hand);
            Require(!f.Activate(muck, 0), "Muckraker spent the only natural next letter");
            Require(f.Queued == 0, "Rejected revival left selectors");
        });
        Run("Dharc material plan frees an occupied Extra Monster Zone", () =>
        {
            var f = new Fixture();
            var dingirsu = f.Add(Dingirsu, CardLocation.MonsterZone, 0, 5);
            var stolen = f.Add(70088809, CardLocation.MonsterZone, 0, 0);
            var guide = f.Add(Tour, CardLocation.MonsterZone, 0, 1);
            f.Add(Zorc, CardLocation.Grave, 1);
            // Native pilot v2 repeatedly planned stolen Harmonia + Tour Guide,
            // although Dingirsu occupied the only usable Extra Monster Zone.
            // The host could offer Dharc using Dingirsu instead. Either preserve
            // the more valuable Dingirsu or explicitly free its occupied zone.
            if (f.Summon(Dharc))
            {
                var chosen = f.Select(HintMsg.LinkMaterial, 2, 2, stolen, guide, dingirsu);
                Require(chosen.Contains(dingirsu), "Dharc retained the blocking Dingirsu and had no legal destination");
            }
        });
        Run("Direct battle skips zero-ATK messages before a real attacker", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Duel.Phase = DuelPhase.Battle;
            var letter = f.Add(LetterI, CardLocation.MonsterZone);
            MessageMonster(letter);
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            letter.ActionIndex[(int)BattlePhaseAction.BattleAction.Attack] = 0;
            ruler.ActionIndex[(int)BattlePhaseAction.BattleAction.Attack] = 1;
            var battle = new BattlePhase { CanMainPhaseTwo = true, CanEndPhase = true };
            battle.AttackableCards.Add(letter);
            battle.AttackableCards.Add(ruler);
            f.Duel.BattlePhase = battle;
            var action = f.AI.OnSelectBattleCmd(battle);
            Require(action.Action == BattlePhaseAction.BattleAction.Attack && action.Index == 1,
                "A Spirit Message attacked first and exposed a pointless battle-response window");
        });
        Run("Only-message battle board advances phase instead of declaring a zero-damage attack", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Duel.Phase = DuelPhase.Battle;
            var letter = f.Add(LetterI, CardLocation.MonsterZone);
            MessageMonster(letter);
            var battle = new BattlePhase { CanMainPhaseTwo = true, CanEndPhase = true };
            battle.AttackableCards.Add(letter);
            f.Duel.BattlePhase = battle;
            var action = f.AI.OnSelectBattleCmd(battle);
            Require(action.Action != BattlePhaseAction.BattleAction.Attack, "The executor attacked with its only Spirit Message");
        });
        Run("Message battle callback also rejects direct attacks through existing defenders", () =>
        {
            var f = new Fixture();
            var letter = f.Add(LetterI, CardLocation.MonsterZone);
            MessageMonster(letter);
            letter.CanDirectAttack = true;
            var enemy = f.Add(Baronne, CardLocation.MonsterZone, 1);
            Require(f.Executor.OnSelectAttackTarget(letter, new[] { enemy }) == null,
                "The base direct-attack fallback bypassed message protection");
        });
        Run("Resolved Droll prevents spending two cards on Zorc's search", () =>
        {
            var f = new Fixture();
            var bird = f.Add(94145021, CardLocation.Grave, 1);
            f.Chain(bird, Desc(94145021, 0), true);
            f.EndChain();
            var zorc = f.Add(Zorc, CardLocation.Hand);
            f.Add(Rhino, CardLocation.Hand);
            Require(!f.Activate(zorc, 0), "Zorc spent two cards on a known blocked search");
            Require(f.Queued == 0, "Rejected search left selectors");
        });
        Run("Droll does not prevent Mastery recovering an existing Board from the graveyard", () =>
        {
            var f = new Fixture();
            var bird = f.Add(94145021, CardLocation.Grave, 1);
            f.Chain(bird, Desc(94145021, 0), true);
            f.EndChain();
            var mastery = f.Add(Mastery, CardLocation.Hand);
            f.Add(Rhino, CardLocation.Hand);
            var board = f.Add(Board, CardLocation.Grave);
            Require(f.Activate(mastery, 0), "Mastery lost its legal graveyard recovery under Droll");
            f.Duel.SolvingChainIndex = 1;
            Is(f.Select(HintMsg.AddToHand, board), board, "Mastery did not recover Board");
        });
        Run("A rejected Zorc offer does not consume its later valid search", () =>
        {
            var f = new Fixture();
            var zorc = f.Add(Zorc, CardLocation.Hand);
            f.Add(LetterI, CardLocation.Hand);
            Require(!f.Activate(zorc, 0), "Zorc discarded a required letter without a good payoff");
            f.Add(Rhino, CardLocation.Hand);
            Require(f.Activate(zorc, 0), "Earlier rejected offer incorrectly marked Zorc as used");
        });
        Run("Unusable material plan cancels once and does not repeat the unchanged summon", () =>
        {
            var f = new Fixture();
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Doomdog, CardLocation.MonsterZone);
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            var zorc = f.Add(Zorc, CardLocation.MonsterZone);
            f.Add(Rhino, CardLocation.Hand);
            f.Add(Pot, CardLocation.SpellZone, 1);
            Require(f.Summon(Phoenix), "Useful Phoenix summon rejected");
            // The engine can apply material restrictions absent from our shallow
            // prediction. It offers a cancelable set excluding the cheap pair.
            var response = f.AI.OnSelectCard(new[] { ruler, zorc }, 2, 2, HintMsg.LinkMaterial, true);
            Require(response != null && response.Count == 0, "Executor spent unplanned bosses instead of canceling");
            Require(!f.Summon(Phoenix), "Canceled material plan repeated immediately without a state change");
            var changed = f.Add(Malice, CardLocation.Grave);
            f.AI.OnMove(changed, 0, (int)CardLocation.Hand, 0, (int)CardLocation.Grave);
            Require(f.Summon(Phoenix), "A later observed movement never retired the failed-summon suppression");
        });
        Run("A field Dark Spirit can pay its cost to open a full monster zone", () =>
        {
            foreach (int id in new[] { Malice, Banishment })
            {
                var f = new Fixture();
                var spirit = f.Add(id, CardLocation.MonsterZone);
                f.Add(Tour, CardLocation.MonsterZone);
                f.Add(Rhino, CardLocation.MonsterZone);
                f.Add(Doomdog, CardLocation.MonsterZone);
                f.Add(Zorc, CardLocation.MonsterZone);
                f.Add(Ruler, CardLocation.Grave);
                var enemy = f.Add(Baronne, CardLocation.MonsterZone, 1);
                f.Chain(enemy, Desc(Baronne, 0));
                Require(f.Activate(spirit, 0), "Field Spirit " + id + " ignored the monster zone freed by its own cost");
            }
        });
        Run("A hand Dark Spirit does not claim that its cost frees a full monster zone", () =>
        {
            foreach (int id in new[] { Malice, Banishment })
            {
                var f = new Fixture();
                var spirit = f.Add(id, CardLocation.Hand);
                f.Add(Tour, CardLocation.MonsterZone);
                f.Add(Rhino, CardLocation.MonsterZone);
                f.Add(Doomdog, CardLocation.MonsterZone);
                f.Add(Zorc, CardLocation.MonsterZone);
                f.Add(Curse, CardLocation.MonsterZone);
                f.Add(Ruler, CardLocation.Grave);
                var enemy = f.Add(Baronne, CardLocation.MonsterZone, 1);
                f.Chain(enemy, Desc(Baronne, 0));
                Require(!f.Activate(spirit, 0), "Hand Spirit " + id + " incorrectly assumed its cost opened a monster zone");
            }
        });
        Run("Muckraker replacement does not mistake cheap backrow for a safe Fiend tribute", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone);
            var letter = f.Add(LetterI, CardLocation.MonsterZone);
            MessageMonster(letter);
            f.Add(Board, CardLocation.SpellZone);
            f.Add(8324284, CardLocation.SpellZone);
            Require(!f.AI.OnSelectEffectYn(muck, 96),
                "Muckraker accepted a replacement using its backrow as proof of a non-message Fiend tribute");
        });
        Run("Muckraker replacement accepts a real expendable Fiend and selects it", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone);
            var dog = f.Add(Doomdog, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(muck, 96), "Muckraker rejected an available expendable Fiend tribute");
            Is(f.Select(HintMsg.DestroyReplace, dog), dog, "Muckraker did not use its expendable Fiend");
        });
        Run("Muckraker declines replacement while a message could become the only legal tribute", () =>
        {
            var f = new Fixture();
            var muck = f.Add(Muckraker, CardLocation.MonsterZone);
            var letter = f.Add(LetterI, CardLocation.MonsterZone);
            MessageMonster(letter);
            f.Add(Doomdog, CardLocation.MonsterZone);
            // Public client state cannot prove that Doomdog is not simultaneously
            // marked for destruction and absent from the later tribute offer.
            Require(!f.AI.OnSelectEffectYn(muck, 96), "Muckraker risked a mandatory later message tribute");
        });
        Run("Cherubini declines replacement while a Board card could become the only legal payment", () =>
        {
            var f = new Fixture();
            var cherubini = f.Add(Cherubini, CardLocation.MonsterZone);
            f.Add(Board, CardLocation.SpellZone);
            f.Add(Doomdog, CardLocation.MonsterZone);
            Require(!f.AI.OnSelectEffectYn(cherubini, 96), "Cherubini risked sending the Board as a mandatory replacement");
        });
        Run("Darius can battle a stronger vanilla monster when its pop is useful and LP survive", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Duel.Phase = DuelPhase.Battle;
            f.Bot.LifePoints = 8000;
            var darius = f.Add(Darius, CardLocation.MonsterZone);
            darius.Overlays.AddRange(new[] { Tour, Doomdog });
            f.Add(89631139, CardLocation.MonsterZone, 1);
            var battle = new BattlePhase { CanMainPhaseTwo = true, CanEndPhase = true };
            battle.AttackableCards.Add(darius);
            f.Duel.BattlePhase = battle;
            Require(f.AI.OnSelectBattleCmd(battle).Action == BattlePhaseAction.BattleAction.Attack,
                "Darius failed to use its battle destruction line against a stronger vanilla monster");
        });
        Run("Darius does not take lethal battle damage merely because it prevents its own destruction", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Duel.Phase = DuelPhase.Battle;
            f.Bot.LifePoints = 500;
            var darius = f.Add(Darius, CardLocation.MonsterZone);
            darius.Overlays.AddRange(new[] { Tour, Doomdog });
            f.Add(89631139, CardLocation.MonsterZone, 1);
            var battle = new BattlePhase { CanMainPhaseTwo = true, CanEndPhase = true };
            battle.AttackableCards.Add(darius);
            f.Duel.BattlePhase = battle;
            Require(f.AI.OnSelectBattleCmd(battle).Action != BattlePhaseAction.BattleAction.Attack,
                "Darius confused battle indestructibility with damage prevention");
        });
        Run("Machinex is not delayed for a Darius battle line that would lose on damage", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Bot.LifePoints = 500;
            var darius = f.Add(Darius, CardLocation.MonsterZone);
            darius.Overlays.AddRange(new[] { Tour, Doomdog });
            f.Add(89631139, CardLocation.MonsterZone, 1);
            Require(f.Summon(Machinex), "Machinex waited for an unusable lethal-damage Darius battle");
        });
        Run("Charity preserves sole Sentence and Judgment over a recoverable late letter", () =>
        {
            foreach (int id in new[] { LetterA, LetterL })
            {
                var f = new Fixture();
                f.Add(Board, CardLocation.SpellZone);
                var charity = f.Add(Charity, CardLocation.SpellZone);
                var letter = f.Add(id, CardLocation.Hand);
                var sentence = f.Add(Sentence, CardLocation.Hand);
                var judgment = f.Add(41420027, CardLocation.Hand);
                var rhino = f.Add(Rhino, CardLocation.Hand);
                f.Chain(charity, Desc(Charity, 0), true);
                var chosen = f.Select(HintMsg.Discard, 2, 2, sentence, judgment, letter, rhino);
                Require(chosen.Contains(rhino) && chosen.Contains(letter),
                    "Charity discarded essential protection/acceleration instead of recoverable late letter " + id);
            }
        });
        Run("Charity still preserves the naturally due I despite available recovery", () =>
        {
            var f = new Fixture();
            f.Add(Board, CardLocation.SpellZone);
            var charity = f.Add(Charity, CardLocation.SpellZone);
            var letter = f.Add(LetterI, CardLocation.Hand);
            var sentence = f.Add(Sentence, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            f.Chain(charity, Desc(Charity, 0), true);
            var chosen = f.Select(HintMsg.Discard, 2, 2, sentence, judgment, letter, rhino);
            Require(!chosen.Contains(letter), "Charity stranded the next natural Board placement");
        });
        Run("Charity preserves late letters when no recovery card is available", () =>
        {
            var f = new Fixture();
            f.Add(Board, CardLocation.SpellZone);
            var charity = f.Add(Charity, CardLocation.SpellZone);
            var letter = f.Add(LetterL, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            var pot = f.Add(Pot, CardLocation.Hand);
            f.Chain(charity, Desc(Charity, 0), true);
            var chosen = f.Select(HintMsg.Discard, 2, 2, judgment, letter, rhino, pot);
            Require(!chosen.Contains(letter), "Late-letter discard assumed unavailable recovery");
        });
        Run("Charity preserves the fourth missing letter of an imminent Board win", () =>
        {
            var f = new Fixture();
            f.Add(Board, CardLocation.SpellZone);
            f.Add(LetterI, CardLocation.SpellZone);
            f.Add(LetterN, CardLocation.SpellZone);
            f.Add(LetterA, CardLocation.SpellZone);
            var charity = f.Add(Charity, CardLocation.SpellZone);
            var letter = f.Add(LetterL, CardLocation.Hand);
            var sentence = f.Add(Sentence, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            f.Chain(charity, Desc(Charity, 0), true);
            var chosen = f.Select(HintMsg.Discard, 2, 2, sentence, judgment, letter, rhino);
            Require(!chosen.Contains(letter), "Charity sacrificed an imminent fourth letter");
        });
        Run("Charity does not discard a late letter together with its only Sentence recovery", () =>
        {
            var f = new Fixture();
            f.Add(Board, CardLocation.SpellZone);
            var charity = f.Add(Charity, CardLocation.SpellZone);
            var letter = f.Add(LetterL, CardLocation.Hand);
            var sentence = f.Add(Sentence, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            f.Chain(charity, Desc(Charity, 0), true);
            var chosen = f.Select(HintMsg.Discard, 2, 2, sentence, letter, judgment);
            Require(!(chosen.Contains(letter) && chosen.Contains(sentence)),
                "Independent discard scores removed both the letter and its only recovery route");
        });
        Run("An existing Mastery graveyard recycle makes a late letter recoverable", () =>
        {
            var f = new Fixture();
            f.Duel.Turn = 3;
            f.Add(Board, CardLocation.SpellZone);
            f.Add(Mastery, CardLocation.Grave);
            var charity = f.Add(Charity, CardLocation.SpellZone);
            var letter = f.Add(LetterL, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            var pot = f.Add(Pot, CardLocation.Hand);
            f.Chain(charity, Desc(Charity, 0), true);
            var chosen = f.Select(HintMsg.Discard, 2, 2, judgment, letter, rhino, pot);
            Require(chosen.Contains(rhino) && chosen.Contains(letter),
                "Charity failed to preserve protection despite existing Mastery recovery");
        });
        Run("Phoenix preserves an active Ruler recovery plan against unknown backrow", () =>
        {
            var f = new Fixture();
            f.Add(Ruler, CardLocation.MonsterZone);
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Malice, CardLocation.Grave);
            f.Add(Rhino, CardLocation.Hand);
            f.Add(89631139, CardLocation.MonsterZone, 1);
            UnknownBackrow(f);
            Require(!f.Summon(Phoenix), "Phoenix consumed the live Ruler plan just to probe unknown backrow");
        });
        Run("Phoenix can spend Ruler to remove a visible relevant floodgate", () =>
        {
            var f = new Fixture();
            f.Add(Ruler, CardLocation.MonsterZone);
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Malice, CardLocation.Grave);
            f.Add(Rhino, CardLocation.Hand);
            f.Add(89631139, CardLocation.MonsterZone, 1);
            f.Add(30241314, CardLocation.SpellZone, 1);
            Require(f.Summon(Phoenix), "Ruler protection incorrectly blocked removal of visible Macro Cosmos");
        });
        Run("Phoenix may use a disabled Ruler against unknown backrow", () =>
        {
            var f = new Fixture();
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            Set(ruler, "Disabled", 1);
            f.Add(Tour, CardLocation.MonsterZone);
            f.Add(Malice, CardLocation.Grave);
            f.Add(Rhino, CardLocation.Hand);
            f.Add(89631139, CardLocation.MonsterZone, 1);
            UnknownBackrow(f);
            Require(f.Summon(Phoenix), "Planner preserved a disabled Ruler at the expense of a useful removal line");
        });
        Run("Phoenix can use spare bodies while preserving the live Ruler", () =>
        {
            var f = new Fixture();
            var ruler = f.Add(Ruler, CardLocation.MonsterZone);
            var guide = f.Add(Tour, CardLocation.MonsterZone);
            var dog = f.Add(Doomdog, CardLocation.MonsterZone);
            f.Add(Malice, CardLocation.Grave);
            f.Add(Rhino, CardLocation.Hand);
            f.Add(89631139, CardLocation.MonsterZone, 1);
            UnknownBackrow(f);
            Require(f.Summon(Phoenix), "Ruler preservation incorrectly suppressed the spare-body Phoenix route");
            var chosen = f.Select(HintMsg.LinkMaterial, 2, 2, ruler, guide, dog);
            Require(!chosen.Contains(ruler), "Phoenix consumed Ruler despite spare materials");
        });
        Run("Royal Decree prevents treating a retained Sentence as usable letter recovery", () =>
        {
            var f = new Fixture();
            f.Add(Board, CardLocation.SpellZone);
            f.Add(51452091, CardLocation.SpellZone, 1);
            var charity = f.Add(Charity, CardLocation.SpellZone);
            var letter = f.Add(LetterL, CardLocation.Hand);
            var sentence = f.Add(Sentence, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            f.Chain(charity, Desc(Charity, 0), true);
            var chosen = f.Select(HintMsg.Discard, 2, 2, letter, sentence, judgment, rhino);
            Require(!chosen.Contains(letter), "Letter recovery relied on a Sentence negated by Royal Decree");
        });
        Run("A Zorc discard does not rely on hand Mastery while Imperial Order prevents its activation", () =>
        {
            var f = new Fixture();
            f.Add(Board, CardLocation.SpellZone);
            f.Add(61740673, CardLocation.SpellZone, 1);
            var zorc = f.Add(Zorc, CardLocation.Hand);
            var letter = f.Add(LetterL, CardLocation.Hand);
            var mastery = f.Add(Mastery, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            Require(f.Activate(zorc, 0), "Zorc's monster effect was incorrectly prevented by Imperial Order");
            var chosen = f.Select(HintMsg.Discard, 1, 1, letter, mastery, judgment);
            Require(!chosen.Contains(letter), "Letter recovery relied on a hand Mastery the bot cannot activate");
        });
        Run("Macro Cosmos does not turn a discarded letter into recoverable graveyard fuel", () =>
        {
            var f = new Fixture();
            f.Add(Board, CardLocation.SpellZone);
            f.Add(30241314, CardLocation.SpellZone, 1);
            var charity = f.Add(Charity, CardLocation.SpellZone);
            var letter = f.Add(LetterL, CardLocation.Hand);
            var sentence = f.Add(Sentence, CardLocation.Hand);
            var judgment = f.Add(41420027, CardLocation.Hand);
            var rhino = f.Add(Rhino, CardLocation.Hand);
            f.Chain(charity, Desc(Charity, 0), true);
            var chosen = f.Select(HintMsg.Discard, 2, 2, letter, sentence, judgment, rhino);
            Require(!chosen.Contains(letter), "Letter discard ignored its known banish replacement");
        });
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public static int Main(string[] args)
    {
        if (args.Length != 3 || ExecutorClass == null) return 2;
        NamedCardsManager.Init(Path.GetFullPath(args[0]));
        typeof(Executor).Assembly.GetType("WindBot.Program").GetField("Rand", Flags).SetValue(null, new Random(20261102));
        bool extra = false;
        foreach (string raw in File.ReadAllLines(args[1]))
        {
            string line = raw.Trim();
            if (line == "#extra") { extra = true; continue; }
            if (line == "!side") break;
            int id;
            if (int.TryParse(line, out id)) (extra ? Extra : MainDeck).Add(id);
        }
        Effects();
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(new Dictionary<string, object>
        {
            { "format", "destiny-board-focused-checks-v1" },
            { "evidence", "Synthetic dispatcher and callback cases; no Lua execution or duel strength claim." },
            { "executableSha256", Hash(typeof(Executor).Assembly.Location) },
            { "databaseSha256", Hash(args[0]) },
            { "deckSha256", Hash(args[1]) },
            { "harnessSha256", Hash(Assembly.GetExecutingAssembly().Location) },
            { "passed", Results.All(item => (bool)item["passed"]) },
            { "checks", Results }
        }));
        return Results.All(item => (bool)item["passed"]) ? 0 : 1;
    }
}
