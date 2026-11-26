# WindBot

[Workspace](../README.md) / **WindBot** &nbsp;·&nbsp; [Deck reference](Guides/DeckReference.md) &nbsp;·&nbsp; [Executor authoring](Guides/ExecutorAuthoring.md) &nbsp;·&nbsp; [Arena](../Arena/README.md)

WindBot plays a chosen deck using its registered C# executor. Start with a local build and a registration check. To watch it play, connect the same build to a compatible MDPro4 or YGOPro duel host.

## Build and check on Windows or Linux

Run these commands from the **workspace root**, the directory containing `WindBot/`. On Linux use `python3` in place of `python`.

| Platform | Build prerequisites |
| --- | --- |
| Windows | Python 3.8+, Visual Studio or Build Tools with .NET desktop build tools and the .NET Framework 4.8 targeting pack. The launcher discovers MSBuild. |
| Linux | Python 3.8+, Mono development tools with a recent C# compiler, MSBuild or xbuild, 4.8 reference assemblies and `libsqlite3.so.0`. The project uses Mono's SQLite mapping in `WindBot.exe.config`. [Mono reference profiles](https://www.mono-project.com/docs/about-mono/compatibility/) · [dllmap](https://www.mono-project.com/docs/advanced/pinvoke/dllmap/) |

```powershell
python WindBot/Tools/bot_tool.py doctor --database DeckWorkshop/Data/standard/cards.cdb
python WindBot/Tools/bot_tool.py build --configuration Release
python WindBot/Tools/bot_tool.py check --configuration Release --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_TheCrimsonKing.ydk --registration TheCrimsonKing --output Artifacts/first-rda-check
```

A successful check loads native SQLite, finds the deck's cards in the selected database, and confirms the exact compiled registration. Open `Artifacts/first-rda-check/report.json` for the receipt. Use a new output directory on each run.

The solution produces `WindBot/bin/Release/WindBot.exe` and `WindBot/BotWrapper/bin/Release/Bot.exe`. Omit `--configuration` on **build** to compile Debug and Release. **Check** and **run** default to Debug, so pass the configuration you built. An explicitly selected frozen executable can be supplied with `--bot-exe`. [Portable launcher](Tools/bot_tool.py) · [Visual Studio solution](WindBot.sln)

## Join a duel

Start a compatible duel host and open a room. In this example it listens on port 7911 and uses the same Standard database and scripts. Replace the database, protocol version or room password if your host uses different settings.

```powershell
python WindBot/Tools/bot_tool.py run --configuration Release --database DeckWorkshop/Data/standard/cards.cdb --deck TheCrimsonKing --host 127.0.0.1 --port 7911
```

`--deck` takes the exact registration name, such as `TheCrimsonKing`. `--deck` on **check** takes the path to a `.ydk` file. The native client can fall back to a random Normal executor for an unknown name, so confirm the registration when trying a new deck. The launcher sets the working directory to the selected runtime. [Bot roster](BotWrapper/bot.conf) · [Deck lists](Decks/) · [Executors](Game/AI/Decks/)

## Find a deck or write your own

| Goal | Open |
| --- | --- |
| Work on Shaddoll 2025, Blue-Eyes 2025, Witchcraft, Sky Striker, Voiceless, Metalmorph, Destiny Board or the RDA planner | [Deck reference and exact check commands](Guides/DeckReference.md) |
| Run one of the eight MDPro4 Rush bots or investigate memory growth across rematches | [Rush registrations, checks and launch command](Guides/DeckReference.md#rush-duel) |
| Create an executor from a deck list | [Deck Workshop](../DeckWorkshop/README.md) · [RDA C# scaffold](Templates/RdaPlannerExecutor.cs.template) |
| Implement a decision, prompt or recovery route | [Authoring guide](Guides/ExecutorAuthoring.md) · [Copyable starter prompt](Guides/ExecutorAuthoring.md#copyable-starter-prompt) |
| Check imported decks and previously measured matchups | [Source revisions and results](Guides/DeckReference.md#executor-origins) |

Keep each deck's implementation in one registered `Game/AI/Decks/*Executor.cs` file, its `.ydk` in `Decks/`, and its fixtures and launchers in `Tools/Executors/<deck>/`. The folder name matches the registration exactly. A generated starter registers as `Test` and needs implemented card effects and prompt handling before it can play its intended strategy. [Executor tools](Tools/Executors/) · [Contributor instructions](AGENTS.md)

## Duel traces

To inspect a decision, start a compatible room. From the workspace root, set an absolute trace directory and database path **before** entering the runtime directory. The native client loads its deck and dialogs from the working directory.

```powershell
$traceDir = [IO.Path]::GetFullPath((Join-Path $PWD 'Artifacts/shaddoll-traces'))
$cardDb = (Resolve-Path 'DeckWorkshop/Data/standard/cards.cdb').Path
Push-Location WindBot/bin/Release
try {
    .\WindBot.exe Deck=Shaddoll2025 Host=127.0.0.1 Port=7911 "DbPath=$cardDb" "TraceData=$traceDir"
} finally { Pop-Location }

python WindBot/Tools/analyze_traces.py --traces "$traceDir" --output Artifacts/shaddoll-trace-summary.json --decisions-csv Artifacts/shaddoll-decisions.csv
```

The optional JSONL capture records public observations and the executor's responses to supported idle-command, card, select/unselect and chain prompts. The analyzer summarizes choices, fallbacks, capture completion and duel results. Battle, tribute, sum and other uncovered prompt types have limited or no decision detail. Arena can tag traces by room, participant and match order. [Trace analyzer](Tools/analyze_traces.py) · [Arena](../Arena/README.md)

## Command-line options

When launching `WindBot.exe` directly, pass `Key=Value` arguments. Quote the entire argument when a path or value contains spaces.

| Option | Purpose |
| --- | --- |
| `Deck`, `DeckFile`, `Name`, `Dialog` | Select a registered executor, optionally override its card list, and set its nickname and dialog stem. `DeckFile` changes cards while retaining the selected executor's decisions. |
| `Host`, `Port`, `HostInfo`, `Version` | Set the duel server, port, room password and protocol version. |
| `DbPath`, `Config` | Set the card database or load additional options from a file. Command-line values take precedence. |
| `Debug`, `LogPath`, `Chat` | Enable detailed diagnostics, append a log to an explicit path, or control bot chat. Use a Debug build for movement and start/result messages. |
| `Hand` | Force the rock-paper-scissors choice for a fixture. Use 1 for scissors, 2 for rock or 3 for paper. |
| `TraceData`, `TraceRoom`, `TraceMatchOrder` | Enable structured capture in an absolute, non-root directory and optionally identify the room and match order. Capture is off by default. |
| `ServerMode`, `ServerPort`, `ServerBindAddress` | Start the HTTP launcher, choose its port and select the local address it listens on. |

## Server mode

An Arena participant can be a locally managed WindBot server or one you start yourself. From the Release output directory, start the launcher with a database matching the duel host. The port here serves HTTP launch requests. The `port` in a launch request belongs to the duel room.

```powershell
.\WindBot.exe ServerMode=True ServerPort=2399 "DbPath=D:/YourGame/Data/locales/en-US/cards.cdb"
```

The default listener is `127.0.0.1`. To reach a remote WindBot from Arena, set `ServerBindAddress` to the machine's local network IP and configure that address in Arena. Limit access to the intended clients. The launcher uses TCP directly and needs no Windows HTTP URL reservation.

With the launcher and room running, open this URL to start one client.

```text
http://127.0.0.1:2399/?name=WindBot&deck=TheCrimsonKing&host=127.0.0.1&port=7911
```

Required query parameters are `name`, `host` and `port`. Optional parameters include `deck`, `dialog`, `version`, `password`, `hand`, `chat` and `debug`. Local trace launches also accept `tracedata`, `traceroom` and `traceorder`. The duel host must already be running. [Arena participant setup](../Arena/README.md#participant-modes)

## Sync the MDPro4 Solo bots

Run from the workspace root with Python 3.8 or later. By default the script uses sibling checkouts named `MDPro_3` and `MDPro4_Data`.

```powershell
python WindBot/Tools/sync_mdpro4.py --dry-run
python WindBot/Tools/sync_mdpro4.py
python WindBot/Tools/sync_mdpro4.py --check
```

| Source in WindBot | Destination |
| --- | --- |
| Compiled `Game/` sources and the session settings they use | `MDPro_3/Assets/Scripts/Windbot/`. The embedded launcher retains its Unity database and expansion loading. Existing Unity asset GUIDs are preserved. |
| `Decks/*.ydk` and `Dialogs/*.json` | `Data/Windbot/Decks/` and `Data/Windbot/Dialogs/` in **both** checkouts. |
| `BotWrapper/bot.conf` (English) and `BotWrapper/locales/<locale>/bot.conf` (other languages) | `Data/locales/<locale>/bot.conf` in **both** checkouts. Destination-only menu entries and comments are retained. |

For checkouts elsewhere, pass `--game-root "D:/path/to/MDPro_3" --data-root "D:/path/to/MDPro4_Data"`. Both arguments take the repository root. Preview lists pending files. Apply updates them in place. Check exits with `0` when both destinations match, `1` for pending updates and `2` for an error. The sync leaves destination-only assets in place and treats equivalent UTF-8 BOMs and line endings as the same text.

**When adding a playable bot**, add its English entry and a translated entry in every maintained locale. Keep the exact executor name, gameplay options and mode flags in each entry. Use an existing dialog filename with its exact capitalization. Validation reports the missing locale or resource before publishing. To check your changes without either companion checkout, run `python -m unittest discover -s WindBot/Tools/Sync`. [English roster](BotWrapper/bot.conf) · [Localized rosters](BotWrapper/locales/) · [Sync script](Tools/sync_mdpro4.py)

Bakura's Destiny Board list uses unrestricted rules and prerelease card `100268007`. The game checkout's `Expansions/ygopro-super-pre.ypk` supplies its card data and script. A standalone host needs compatible data as well. [Bakura setup](Guides/DeckReference.md#bakura--destiny-board)

## BotWrapper

`Bot.exe` integrates with the game's normal bot menu. Deploy `bot.conf` beside it, with `WindBot.exe`, its DLLs, configuration, decks and dialogs in a sibling `WindBot/` subdirectory. A solution build leaves the two executable outputs separate. [BotWrapper source](BotWrapper/) · [English roster](BotWrapper/bot.conf)

The repository includes the selected MDPro4 dialogs and roster. `Dialogs/default.json` is a byte-identical copy of `Universal.EN.json`. The resource receipt records the upstream revision, import paths and hashes. [Resource provenance](resource-provenance.json)

## Validation and credits

| Evidence | What it checks |
| --- | --- |
| Build and runtime check | The bot compiles, loads the database, finds the supplied cards and registration. |
| Focused executor checks | Particular decisions, target selections and lifecycle resets on specified inputs. |
| Native scripted fixtures | Actual host offers, prompt sequence and observed outcomes for the selected opening. |
| Frozen Arena matches | Results against the recorded opponents with the recorded settings. Keep match counts and individual duels separate. |

Multiple effects can queue selections in one chain. When debugging a wrong target, verify which effect owns the prompt and whether a queued selection was left behind. [Selection contract](Guides/ExecutorAuthoring.md#route-each-prompt-at-its-actual-stage)

WindBot derives from the YGOPro/WindBot ecosystem and retains its original attribution. [License](LICENSE) · [YGOPro](https://github.com/Fluorohydride/ygopro) · [YGOSharp](https://github.com/IceYGO/ygosharp) · [SRVPro](https://github.com/mycard/srvpro) · [Upstream WindBot Arena](https://github.com/mercury233/windbot-arena) · [Original template generator](https://mercury233.me/windbot/gen.html)
