using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.Network;
using YGOSharp.OCGWrapper.Enums;

internal static partial class RushChecks
{
    private sealed class SelectionProbe : Executor
    {
        public IList<ClientCard> Response;

        public SelectionProbe(GameAI ai) : base(ai, ai.Duel)
        {
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            return Response;
        }
    }

    private static void LifecycleChecks()
    {
        string absent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ydk");
        var client = new GameClient(new WindBotInfo { Deck = Registrations[0], DeckFile = absent });
        bool rejected = false;
        try
        {
            new GameBehavior(client);
        }
        catch (InvalidDataException ex)
        {
            rejected = ex.Message.Contains(absent);
        }
        Check("A missing deck is reported during setup before joining the room", rejected);

        var transport = new NetworkClient();
        var packets = new BinaryClient(transport);
        var reason = new IOException("Test room closed");
        Exception reported = null;
        int disconnections = 0;
        transport.Disconnected += error => Interlocked.Increment(ref disconnections);
        packets.Disconnected += error => reported = error;
        Parallel.For(0, 32, index => transport.Close(reason));
        packets.Update();
        Check("Concurrent cleanup delivers one disconnect and its reason to the game thread",
            disconnections == 1 && !packets.IsConnected && reported == reason);
        using (var unused = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            bool retired = false;
            try
            {
                transport.Initialize(unused);
            }
            catch (ObjectDisposedException)
            {
                retired = true;
            }
            Check("A retired client refuses a new socket", retired && !transport.IsConnected);
        }

        GameAI ai = CreateBot(Registrations[0]);
        var probe = new SelectionProbe(ai);
        ai.Executor = probe;
        ClientCard[] offered = { Card(120109014, CardLocation.Hand), Card(120109014, CardLocation.Hand) };
        probe.Response = new ClientCard[0];
        Check("A cancelable prompt accepts an empty response", ai.OnSelectCard(offered, 1, 2, 0, true) == probe.Response);
        Check("A required prompt falls back from an empty response", ai.OnSelectCard(offered, 1, 2, 0, false).Single() == offered[0]);
        probe.Response = new[] { offered[1] };
        Check("A single offered instance passes validation", ai.OnSelectCard(offered, 1, 1, 0, false) == probe.Response);
        probe.Response = new[] { Card(120109014, CardLocation.Hand) };
        Check("A different instance of the same card is rejected", ai.OnSelectCard(offered, 1, 1, 0, false).Single() == offered[0]);
        probe.Response = new[] { offered[1], offered[0] };
        Check("Separate offered copies are valid together", ai.OnSelectCard(offered, 2, 2, 0, false) == probe.Response);
        probe.Response = new[] { offered[1], offered[1] };
        Check("A duplicate instance is rejected", ai.OnSelectCard(offered, 2, 2, 0, false).SequenceEqual(offered));
    }
}
