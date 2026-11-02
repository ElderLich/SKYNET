using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;

// Explicitly compiled by bot_tool.py; not included in WindBot.exe.
internal static class RuntimeChecks
{
    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static int Main(string[] args)
    {
        if (args.Length != 4) return 2;
        var report = new Dictionary<string, object>();
        bool passed = false;
        try
        {
            report["evidence"] = "Offline runtime/database/deck identity check. No connection, Lua execution or duel result.";
            report["executableSha256"] = Hash(typeof(Executor).Assembly.Location);
            report["databaseSha256"] = Hash(args[0]);
            report["deckSha256"] = Hash(args[1]);
            WindBot.Config.Load(new string[0]);
            NamedCardsManager.Init(Path.GetFullPath(args[0]));
            DecksManager.Init();
            var cards = NamedCardsManager.GetAllCards();
            if (cards.Count == 0) throw new InvalidDataException("Database loaded no cards.");
            var known = new HashSet<int>(cards.Select(card => card.Id));
            var ids = new List<int>();
            int lineNumber = 0;
            foreach (string raw in File.ReadAllLines(args[1]))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line == "!side") continue;
                int id;
                if (!int.TryParse(line, out id) || id <= 0)
                    throw new InvalidDataException("Invalid deck ID on line " + lineNumber);
                ids.Add(id);
            }
            if (ids.Count == 0) throw new InvalidDataException("Deck contains no card IDs.");
            int[] missing = ids.Where(id => !known.Contains(id)).Distinct().ToArray();
            report["cardsLoaded"] = cards.Count;
            report["deckCopies"] = ids.Count;
            report["missingCardIds"] = missing;
            if (missing.Length != 0) throw new InvalidDataException("Deck IDs missing from this database: " + string.Join(", ", missing));
            if (!string.IsNullOrEmpty(args[3]) && !DecksManager.HasDeck(args[3]))
                throw new InvalidDataException("Compiled Deck registration not found: " + args[3]);
            report["registration"] = args[3];
            passed = true;
            Console.WriteLine("PASS native SQLite loading, database identities and unique executor registrations.");
            Console.WriteLine("PASS " + ids.Count + " deck copies found in " + cards.Count + " database cards.");
            if (!string.IsNullOrEmpty(args[3])) Console.WriteLine("PASS compiled Deck=" + args[3]);
        }
        catch (Exception error)
        {
            report["error"] = error.ToString();
            Console.Error.WriteLine(error);
        }
        report["format"] = "windbot-runtime-check-v1";
        report["passed"] = passed;
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(report));
        return passed ? 0 : 1;
    }
}
