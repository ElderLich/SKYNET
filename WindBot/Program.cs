using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Diagnostics;
using System.Text;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;

namespace WindBot
{
    public class Program
    {
        internal static Random Rand;
        internal static bool ServerMode;

        internal static void Main(string[] args)
        {
            Config.Load(args);
            Logger.SetLogFile(Config.GetString("LogPath"));
            Logger.WriteLine("WindBot starting...");

            string databasePath = Config.GetString("DbPath", "cards.cdb");

            InitDatas(databasePath);

            ServerMode = Config.GetBool("ServerMode", false);

            if (ServerMode)
            {
                // Run in server mode, provide a http interface to create bot.
                int serverPort = Config.GetInt("ServerPort", 2399);
                IPAddress bindAddress;
                if (!IPAddress.TryParse(Config.GetString("ServerBindAddress", "127.0.0.1"), out bindAddress))
                    throw new ArgumentException("ServerBindAddress must be a local IP address.");
                RunAsServer(bindAddress, serverPort);
            }
            else
            {
                // Join the host specified on the command line.
                if (args.Length == 0)
                {
                    Logger.WriteErrorLine("=== WARN ===", null);
                    Logger.WriteLine("No input found, trying to connect to localhost YGOPro host.");
                    Logger.WriteLine("If it fails, the program will quit silently.");
                }
                RunFromArgs();
            }
        }

        public static void InitDatas(string databasePath)
        {
            Rand = new Random();
            DecksManager.Init();
            string absolutePath = Path.GetFullPath(databasePath);
            if (!File.Exists(absolutePath))
                // In case windbot is placed in a folder under ygopro folder
                absolutePath = Path.GetFullPath("../" + databasePath);
            if (!File.Exists(absolutePath))
                // In case windbot is placed in a folder under ygopro2 folder
                absolutePath = Path.GetFullPath("../cdb/" + databasePath);
            if (!File.Exists(absolutePath))
            {
                Logger.WriteErrorLine("Can't find cards database file.", null);
                Logger.WriteErrorLine("Please place cards.cdb next to WindBot.exe or Bot.exe .", null);
                Logger.WriteLine("Press any key to quit...");
                Console.ReadKey();
                System.Environment.Exit(1);
            }
            NamedCardsManager.Init(absolutePath);
        }

        private static void RunFromArgs()
        {
            WindBotInfo Info = new WindBotInfo();
            Info.Name = Config.GetString("Name", Info.Name);
            Info.Deck = Config.GetString("Deck", Info.Deck);
            Info.DeckFile = Config.GetString("DeckFile", Info.DeckFile);
            Info.Dialog = Config.GetString("Dialog", Info.Dialog);
            Info.TraceData = Config.GetString("TraceData", Info.TraceData);
            if (!string.IsNullOrWhiteSpace(Info.TraceData))
                Info.TraceData = Game.AI.Trace.TraceSession.ResolveDirectory(Info.TraceData);
            Info.TraceRoom = Config.GetString("TraceRoom", Info.TraceRoom);
            int traceMatchOrder = Config.GetInt("TraceMatchOrder", 0);
            if (traceMatchOrder > 0)
                Info.TraceMatchOrder = traceMatchOrder;
            Info.Host = Config.GetString("Host", Info.Host);
            Info.Port = Config.GetInt("Port", Info.Port);
            Info.HostInfo = Config.GetString("HostInfo", Info.HostInfo);
            Info.Version = Config.GetInt("Version", Info.Version);
            Info.Hand = Config.GetInt("Hand", Info.Hand);
            Info.Debug = Config.GetBool("Debug", Info.Debug);
            Info.Chat = Config.GetBool("Chat", Info.Chat);
            Run(Info);
        }

        private static void RunAsServer(IPAddress bindAddress, int serverPort)
        {
            TcpListener mainServer = new TcpListener(bindAddress, serverPort);
            SemaphoreSlim requestSlots = new SemaphoreSlim(16);
            try
            {
                mainServer.Start();
                Logger.WriteLine("WindBot HTTP server listening on " + mainServer.LocalEndpoint + ".");
                while (true)
                {
                    TcpClient client = mainServer.AcceptTcpClient();
                    requestSlots.Wait();
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        using (client)
                        {
                            try
                            {
                                HandleServerRequest(client);
                            }
                            catch (IOException)
                            {
                                // A polling client can disconnect before receiving the response.
                            }
                            catch (Exception ex)
                            {
                                Logger.WriteErrorLine("WindBot HTTP request failed", ex);
                            }
                            finally
                            {
                                requestSlots.Release();
                            }
                        }
                    });
                }
            }
            finally
            {
                mainServer.Stop();
            }
        }

        private static void HandleServerRequest(TcpClient client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            NetworkStream stream = client.GetStream();
            int statusCode = 400;

            try
            {
                int remaining = 16384;
                Stopwatch elapsed = Stopwatch.StartNew();
                BufferedStream input = new BufferedStream(stream);
                string requestLine = ReadHttpLine(input, ref remaining, elapsed);
                string[] requestParts = requestLine.Split(' ');
                if (requestParts.Length != 3 || !requestParts[1].StartsWith("/", StringComparison.Ordinal)
                    || (requestParts[2] != "HTTP/1.1" && requestParts[2] != "HTTP/1.0"))
                {
                    WriteHttpResponse(stream, 400);
                    return;
                }
                if (requestParts[0] != "GET")
                {
                    WriteHttpResponse(stream, 405);
                    return;
                }

                while (ReadHttpLine(input, ref remaining, elapsed).Length != 0) { }

                Uri requestUri;
                if (!Uri.TryCreate("http://127.0.0.1" + requestParts[1], UriKind.Absolute, out requestUri))
                {
                    WriteHttpResponse(stream, 400);
                    return;
                }

                var queryParams = HttpUtility.ParseQueryString(requestUri.Query);
                WindBotInfo Info = new WindBotInfo();
                Info.Name = queryParams.Get("name");
                Info.Deck = queryParams.Get("deck");
                Info.Host = queryParams.Get("host");
                string port = queryParams.Get("port");
                if (port != null)
                    Info.Port = Int32.Parse(port);
                string deckfile = queryParams.Get("deckfile"); // Obsoleted
                string dialog = queryParams.Get("dialog");
                if (dialog != null)
                {
                    if (dialog.Length == 0 || dialog == "." || dialog == ".."
                        || dialog.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                        throw new ArgumentException("A dialog name is required.");
                    Info.Dialog = dialog;
                }
                string traceData = queryParams.Get("tracedata");
                if (traceData != null)
                {
                    if (!IPAddress.IsLoopback(((IPEndPoint)client.Client.RemoteEndPoint).Address))
                        throw new ArgumentException("Only local launchers may select a trace directory.");
                    Info.TraceData = Game.AI.Trace.TraceSession.ResolveDirectory(traceData);
                }
                string traceRoom = queryParams.Get("traceroom");
                if (traceRoom != null)
                {
                    if (traceRoom.Length > 64)
                        throw new ArgumentException("Trace room is too long.");
                    Info.TraceRoom = traceRoom;
                }
                string traceOrder = queryParams.Get("traceorder");
                if (traceOrder != null)
                {
                    int parsedTraceOrder;
                    if (!Int32.TryParse(traceOrder, out parsedTraceOrder) || parsedTraceOrder <= 0)
                        throw new ArgumentException("Trace order must be a positive integer.");
                    Info.TraceMatchOrder = parsedTraceOrder;
                }
                string version = queryParams.Get("version");
                if (version != null)
                    Info.Version = Int16.Parse(version);
                string password = queryParams.Get("password");
                if (password != null)
                    Info.HostInfo = password;
                string hand = queryParams.Get("hand");
                if (hand != null)
                    Info.Hand = Int32.Parse(hand);
                string debug = queryParams.Get("debug");
                if (debug != null)
                    Info.Debug = bool.Parse(debug);
                string chat = queryParams.Get("chat");
                if (chat != null)
                    Info.Chat = bool.Parse(chat);

                if (string.IsNullOrWhiteSpace(Info.Name) || string.IsNullOrWhiteSpace(Info.Host)
                    || port == null || Info.Port < 1 || Info.Port > 65535 || deckfile != null)
                {
                    statusCode = 400;
                }
                else if (Info.Deck != null && !DecksManager.HasDeck(Info.Deck))
                {
                    statusCode = 404;
                }
                else
                {
                    statusCode = 200;
                    Thread workThread = new Thread(new ParameterizedThreadStart(Run));
                    workThread.Start(Info);
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException
                || ex is NotSupportedException || ex is InvalidDataException || ex is IOException)
            {
                statusCode = 400;
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                    throw;
                Logger.WriteErrorLine("Parse Http Request Error", ex);
                statusCode = 500;
            }

            WriteHttpResponse(stream, statusCode);
        }

        private static string ReadHttpLine(Stream stream, ref int remaining, Stopwatch elapsed)
        {
            StringBuilder line = new StringBuilder();
            while (remaining > 0 && elapsed.ElapsedMilliseconds < 5000)
            {
                int value = stream.ReadByte();
                --remaining;
                if (value < 0)
                    throw new EndOfStreamException("HTTP request ended before the headers were complete.");
                if (value == '\n')
                {
                    if (line.Length > 0 && line[line.Length - 1] == '\r')
                        line.Length--;
                    return line.ToString();
                }
                line.Append((char)value);
            }
            throw new InvalidDataException("HTTP request exceeded the header size or time limit.");
        }

        private static void WriteHttpResponse(NetworkStream stream, int statusCode)
        {
            string reason = statusCode == 200 ? "OK"
                : statusCode == 400 ? "Bad Request"
                : statusCode == 404 ? "Not Found"
                : statusCode == 405 ? "Method Not Allowed"
                : "Internal Server Error";
            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 " + statusCode + " " + reason + "\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n" +
                "Cache-Control: no-store\r\n\r\n");
            stream.Write(response, 0, response.Length);
        }

        private static void Run(object o)
        {
            GameClient.RunSession((WindBotInfo)o);
        }

        public static FileStream ReadFile(string directory, string filename, string extension)
        {
            string tryfilename = filename + "." + extension;
            string fullpath = Path.Combine(directory, tryfilename);
            if (!File.Exists(fullpath))
                fullpath = filename;
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../", filename);
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../deck/", filename);
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../", tryfilename);
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../deck/", tryfilename);
            return new FileStream(fullpath, FileMode.Open, FileAccess.Read);
        }
    }
}
