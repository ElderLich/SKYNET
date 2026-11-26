using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

internal static class BE2025Checks
{
    private const int Spirit = 42097666, BlueEyes = 89631139, JetDragon = 30576089;
    private const int Tyrant = 11443677, TrueLight = 62089826, Impermanence = 10045474;
    private static readonly Dictionary<string, bool> Results = new Dictionary<string, bool>();

    private static ClientCard Card(int id, CardLocation location)
    {
        var card = new ClientCard(id, location, 0, (int)CardPosition.FaceUpAttack) { Controller = 0 };
        // In a duel these two properties come from MSG_UPDATE_DATA.
        typeof(ClientCard).GetProperty("Type").SetValue(card, card.Data.Type, null);
        typeof(ClientCard).GetProperty("Attack").SetValue(card, card.Data.Attack, null);
        return card;
    }

    private static void Check(string name, bool passed)
    {
        Results.Add(name, passed);
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
    }

    private static GameAI CreateBot(Deck deck, out Duel duel)
    {
        duel = new Duel { Turn = 3, Player = 0, Phase = DuelPhase.Main1 };
        duel.Fields[0].Init(deck.Cards.Count, 0, 0);
        duel.Fields[0].SetInitialDeck(deck.Cards);
        duel.Fields[1].Init(40, 0, 1);
        var ai = new GameAI(new GameClient(new WindBotInfo { Deck = "BE2025", Dialog = "default" }), duel);
        ai.Executor = DecksManager.Instantiate(ai, duel);
        return ai;
    }

    private static ClientCard Choose(GameAI ai, int hint, params ClientCard[] offered)
    {
        return ai.OnSelectCard(offered, 1, 1, hint, false).Single();
    }

    private static void SpiritChecks(Deck deck)
    {
        Duel duel;
        GameAI ai = CreateBot(deck, out duel);
        ClientField bot = duel.Fields[0];
        ClientCard spirit = Card(Spirit, CardLocation.MonsterZone);
        bot.MonsterZone[0] = spirit;
        for (int slot = 1; slot < 5; slot++)
            bot.MonsterZone[slot] = Card(14558127, CardLocation.MonsterZone);
        int summon = ai.Executor.Util.GetStringId(Spirit, 2);
        Check("Spirit accepts its on-Link Summon Mausoleum search", ai.OnSelectEffectYn(spirit, ai.Executor.Util.GetStringId(Spirit, 0)));
        Check("Spirit holds with no Blue-Eyes monster in hand or graveyard", !ai.OnSelectEffectYn(spirit, summon));

        ClientCard jetHand = Card(JetDragon, CardLocation.Hand);
        ClientCard jetGrave = Card(JetDragon, CardLocation.Grave);
        bot.Hand.Add(jetHand);
        Check("Spirit accepts a Jet Dragon with all five main zones occupied", ai.OnSelectEffectYn(spirit, summon));
        bot.Graveyard.Add(jetGrave);
        duel.CurrentChain.Add(spirit);
        duel.CurrentChainInfo.Add(new ChainInfo(spirit, 0, summon));
        duel.SolvingChainIndex = 1;
        Check("Spirit keeps Jet Dragon's effects and attack by summoning from hand",
            Choose(ai, HintMsg.SpSummon, jetGrave, jetHand) == jetHand);

        ClientCard hand = Card(BlueEyes, CardLocation.Hand);
        ClientCard grave = Card(BlueEyes, CardLocation.Grave);
        bot.Hand.Add(hand);
        bot.Graveyard.Add(grave);
        Check("Spirit saves the hand copy when graveyard Blue-Eyes is offered",
            Choose(ai, HintMsg.SpSummon, hand, grave) == grave);
        Check("Spirit accepts the remaining hand copy when the graveyard copy is unavailable",
            Choose(ai, HintMsg.SpSummon, hand) == hand);
    }

    private static void TyrantChecks(Deck deck)
    {
        Duel duel;
        GameAI ai = CreateBot(deck, out duel);
        ClientField bot = duel.Fields[0];
        ClientCard tyrant = Card(Tyrant, CardLocation.MonsterZone);
        ClientCard light = Card(TrueLight, CardLocation.Grave);
        ClientCard interruption = Card(Impermanence, CardLocation.Grave);
        bot.MonsterZone[0] = tyrant;
        bot.Graveyard.Add(light);
        bot.Graveyard.Add(interruption);
        Check("Tyrant accepts the trap-setting trigger with an open zone", ai.OnSelectEffectYn(tyrant, -1));
        Check("Tyrant selects an offered True Light", Choose(ai, HintMsg.Set, interruption, light) == light);
        bot.SpellZone[0] = Card(TrueLight, CardLocation.SpellZone);
        Check("Tyrant prioritizes interruption over a redundant True Light",
            Choose(ai, HintMsg.Set, light, interruption) == interruption);
        Check("Tyrant uses the only offered settable trap", Choose(ai, HintMsg.Set, interruption) == interruption);
        for (int slot = 1; slot < 5; slot++)
            bot.SpellZone[slot] = Card(Impermanence, CardLocation.SpellZone);
        Check("Tyrant holds when every spell and trap zone is occupied", !ai.OnSelectEffectYn(tyrant, -1));
    }

    private static int Main(string[] args)
    {
        if (args.Length != 3) return 2;
        string error = null;
        try
        {
            Config.Load(new string[0]);
            NamedCardsManager.Init(args[0]);
            DecksManager.Init();
            Deck deck = Deck.Load(args[1]);
            if (!DecksManager.HasDeck("BE2025") || deck == null || !deck.ExtraCards.Any(c => c.Id == Tyrant) ||
                !deck.ExtraCards.Any(c => c.Id == Spirit))
                throw new InvalidDataException("Supply the registered BE2025 deck with Spirit and Tyrant in the Extra Deck.");
            SpiritChecks(deck);
            TyrantChecks(deck);
        }
        catch (Exception exception)
        {
            error = exception.ToString();
            Console.Error.WriteLine(error);
        }
        bool passed = error == null && Results.Values.All(result => result);
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(new
        {
            format = "windbot-be-2025-check-v1", passed, results = Results, error
        }));
        Console.WriteLine("{0} {1} BE2025 focused checks.", passed ? "PASS" : "FAIL", Results.Count);
        return passed ? 0 : 1;
    }
}
