using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

internal static partial class RushChecks
{
    private static readonly string[] Registrations =
    {
        "RushDragonDragears", "BusterBlader", "DinocarriageDynarmix", "SaikyoBattleFlag",
        "Excutie", "ObsidianMagical", "TheLegend", "Mituzi"
    };
    private static readonly Dictionary<string, Deck> Decks = new Dictionary<string, Deck>();
    private static readonly Dictionary<string, bool> Checks = new Dictionary<string, bool>();
    private static readonly Dictionary<string, object> Profiles = new Dictionary<string, object>();
    private static readonly Duel CardQueryPerspective = new Duel { IsFirst = true };

    private static GameAI CreateBot(string registration)
    {
        Deck deck = Decks[registration];
        var duel = new Duel { Turn = 3, Player = 0, Phase = DuelPhase.Main1 };
        duel.Fields[0].Init(deck.Cards.Count, deck.ExtraCards.Count, 0);
        duel.Fields[0].SetInitialDeck(deck.Cards);
        duel.Fields[1].Init(40, 0, 1);
        var ai = new GameAI(new GameClient(new WindBotInfo { Deck = registration, Dialog = "default" }), duel);
        ai.Executor = DecksManager.Instantiate(ai, duel);
        ai.OnNewPhase();
        return ai;
    }

    private static ClientCard Card(int id, CardLocation location, int controller = 0,
        CardPosition position = CardPosition.FaceUpAttack)
    {
        var card = new ClientCard(id, location, 0, (int)position) { Controller = controller };
        if (id != 0 && card.Data == null)
            throw new InvalidDataException("Card missing from Rush database: " + id);
        using (var packet = new MemoryStream())
        using (var writer = new BinaryWriter(packet))
        using (var reader = new BinaryReader(packet))
        {
            Query flags = Query.Owner;
            if (card.Data != null)
                flags |= Query.Type | Query.Level | Query.Attribute | Query.Race | Query.Attack | Query.Defence;
            writer.Write((int)flags);
            if (card.Data != null)
            {
                writer.Write(card.Data.Type);
                writer.Write(card.Data.Level);
                writer.Write(card.Data.Attribute);
                writer.Write(card.Data.Race);
                writer.Write(card.Data.Attack);
                writer.Write(card.Data.Defense);
            }
            writer.Write(controller);
            packet.Position = 0;
            card.Update(reader, CardQueryPerspective);
        }
        return card;
    }

    private static void Check(string name, bool passed)
    {
        Checks.Add(name, passed);
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
    }

    private static readonly int[] Hints = { HintMsg.Destroy, HintMsg.Release, HintMsg.AddToHand, HintMsg.Confirm };

    private static void PromptBatch(GameAI ai, ClientCard[] offered, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            int hint = Hints[i % Hints.Length];
            int max = hint == HintMsg.Confirm ? 3 : 1;
            IList<ClientCard> selected = ai.OnSelectCard(offered, 1, max, hint, false);
            if (selected == null || selected.Count == 0 || selected.Count > max)
                throw new InvalidDataException("Invalid response to hint " + hint);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateShortDuel(string registration, List<WeakReference> references, ClientCard[] offered)
    {
        GameAI ai = CreateBot(registration);
        PromptBatch(ai, offered, 4);
        references.Add(new WeakReference(ai));
        references.Add(new WeakReference(ai.Duel));
        references.Add(new WeakReference(ai.Executor));
    }

    private static void Profile(string registration)
    {
        GameAI ai = CreateBot(registration);
        ClientCard[] offered =
        {
            Card(120109014, CardLocation.MonsterZone, 1), Card(120151039, CardLocation.MonsterZone, 1),
            Card(120170035, CardLocation.MonsterZone), Card(120130016, CardLocation.Hand),
            Card(120181001, CardLocation.Deck), Card(120183031, CardLocation.Deck),
            Card(120145014, CardLocation.Grave), Card(0, CardLocation.MonsterZone, 1, CardPosition.FaceDownDefence)
        };
        PromptBatch(ai, offered, 128);
        const int prompts = 12000, duels = 32;
        long before = GC.GetAllocatedBytesForCurrentThread();
        PromptBatch(ai, offered, prompts);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        var references = new List<WeakReference>(duels * 3);
        for (int i = 0; i < duels; ++i) CreateShortDuel(registration, references, offered);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        int survivors = references.Count(reference => reference.IsAlive);
        Profiles.Add(registration, new { prompts, allocatedBytes = bytes, shortDuels = duels, survivingObjects = survivors });
        Console.WriteLine("{0}: {1:F1} bytes/prompt, {2}/{3} short-duel objects survived",
            registration, (double)bytes / prompts, survivors, references.Count);
        Check(registration + " releases short-duel objects", survivors == 0);
        Check(registration + " uses its registered executor", ai.Executor.Deck == "AI_" + registration);

        ai.SelectCard(offered[3]);
        Check(registration + " respects a queued card",
            ai.OnSelectCard(offered, 1, 1, HintMsg.Destroy, false).Single() == offered[3]);
        ai.SelectPosition(CardPosition.FaceUpAttack);
        Check(registration + " respects a queued position", ai.OnSelectPosition(120145014,
            new[] { CardPosition.FaceUpDefence, CardPosition.FaceUpAttack }) == CardPosition.FaceUpAttack);
        var destruction = ai.OnSelectCard(new[] { offered[0], offered[2] }, 1, 2, HintMsg.Destroy, false);
        Check(registration + " avoids unnecessary friendly destruction", destruction.Count == 1 && destruction[0] == offered[0]);
        var materials = ai.OnSelectCard(new[] { offered[2], offered[3] }, 1, 1, HintMsg.Tribute, false);
        Check(registration + " offers the weaker tribute", materials.Count == 1 && materials[0].Attack ==
            Math.Min(offered[2].Attack, offered[3].Attack));
    }

    private static string HashFile(string filename)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream input = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }

    private static int Main(string[] args)
    {
        if (args.Length != 3) return 2;
        string error = null;
        string executableSha256 = null, databaseSha256 = null;
        try
        {
            executableSha256 = HashFile(typeof(Executor).Assembly.Location);
            databaseSha256 = HashFile(args[0]);
            Config.Load(new string[0]);
            WindBot.Program.InitDatas(args[0]);
            foreach (string name in Registrations)
            {
                string path = Path.Combine(args[1], "AI_" + name + ".ydk");
                Deck deck = Deck.Load(path);
                int listed = File.ReadLines(path).Count(line => { int id; return int.TryParse(line, out id); });
                if (!DecksManager.HasDeck(name) || deck == null || deck.Cards.Count == 0 ||
                    deck.Cards.Count + deck.ExtraCards.Count + deck.SideCards.Count != listed)
                    throw new InvalidDataException("Check Rush database, deck and registration for " + name);
                Decks.Add(name, deck);
                Profile(name);
            }
            LifecycleChecks();
            CombatChecks();
            SpecialistChecks();
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            Console.Error.WriteLine(error);
        }
        bool passed = error == null && Checks.Values.All(result => result);
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(new
        {
            format = "windbot-rush-check-v1", passed, executableSha256, databaseSha256,
            checks = Checks, profiles = Profiles, error
        }));
        return passed ? 0 : 1;
    }
}
