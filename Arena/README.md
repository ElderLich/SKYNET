# Skynet Arena

Compare duel bots, run a deck against a roster, or inspect a `.ydk` before
writing its executor. Arena serves a local browser dashboard and stores your
settings, match scores and experiment history in SQLite.

| I want to… | Start here | What I need |
| --- | --- | --- |
| Explore cards, Lua and related cards | [Deck Workshop](#deck-workshop) | Node.js 24 and a deck |
| Play a normal WindBot matchup | [Normal WindBot setup](#normal-windbot-setup) | Built WindBot, roster, matching card database and dedicated SRVPro |
| Compare a frozen SKYNET policy or expert | [Configure a SKYNET comparison](#configure-a-skynet-comparison) | Built SKYNET, deck profiles and dedicated SRVPro |
| Inspect decisions from a match | [Structured duel traces](#structured-duel-traces) | A local WindBot with trace support |
| Run Shaddoll against Lightsworn | [Shaddoll one-click challenge](#shaddoll-2025-one-click-challenge) | Windows, Release WindBot and a prepared local server |

## Start

On Windows, install Node.js 24 and double-click **[Skynet Arena.cmd](Skynet%20Arena.cmd)**.
On first use it installs dependencies, builds the dashboard and opens the local
console. Keep the console open and press **Ctrl+C** there to stop Arena. A
second double-click reopens the default instance. [Open Arena](http://127.0.0.1:3000)

For a manual launch on Windows or Linux, open a terminal **in `Arena/`**.
Install dependencies on first use and when the lockfile changes.

```sh
npm ci
npm run build
npm start
```

In Windows PowerShell, `.\Start-SkynetArena.ps1 -Build -Port 3137` starts the
dashboard on another port. After an update, restart the Node server and reload
the page. If Windows reports `EPERM` on `better_sqlite3.node`, stop Arena
consoles using this checkout, rerun `npm ci`, then start again.

## Deck Workshop

Open **Deck Workshop** and import or paste a `.ydk`. Select a card to read its
text and numbered Lua, follow its script links, or browse categorized
RelatedCards relationships. Export the combined text and Lua report. This
works without building a bot or preparing a duel server. [Open the local Workshop](http://127.0.0.1:3000/#workshop)

| Bundled format | What you can do |
| --- | --- |
| Standard | Analyze a deck, export a report, and generate a starter executor, deck and provenance manifest. |
| Rush | Analyze cards, scripts and deck relationships, and export a report. |

The Standard starter uses the RDA template, registers as `Test`, and ships with
its model disabled. Implement its card effects and test it before comparing it
in Arena. The snapshot excludes Edison. If a request returns HTML where the
Workshop expects JSON, restart the backend and use its own URL or the configured
Vite proxy. [Workshop sources, refresh commands and generation limits](../DeckWorkshop/README.md)

## Normal WindBot setup

1. Build a Release runtime. From the repository root in Developer PowerShell,
   use the commands below for the bundled Standard snapshot. Use a database
   that matches the scripts on your chosen duel server.

   ```powershell
   msbuild WindBot/WindBot.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU"
   Copy-Item -LiteralPath DeckWorkshop/Data/standard/cards.cdb -Destination WindBot/bin/Release/cards.cdb
   ```

2. In **Settings → Agents**, select the candidate and baseline runtimes. A fresh
   installation points both at `WindBot/bin/Release` and the English
   `WindBot/BotWrapper/bot.conf` on ports 2399 and 2398. To compare versions,
   point the baseline at a separately preserved build. Each runtime needs its
   own decks, dependencies and dialogs.
3. In **Settings → SRVPro**, select a dedicated server and enter its duel port,
   management port and administrative credentials. You can also
   [prepare a dedicated server on Windows](#dedicated-local-server-windows).
4. Create a small **Regression** with a deck available to both participants.
   **Match order & activity** shows accepted launches. Once the rankings come
   in, open the matchup for wins and disconnects.

Local WindBot mode launches the executable directly on Windows and uses Mono on
Linux. The normal bot's guide lists the compiler, Mono and SQLite prerequisites
and explains its roster and dialog sources.
[WindBot build and runtime guide](../WindBot/README.md#build-and-check-on-windows-or-linux)

### Structured duel traces

In **Settings → Agents**, enable **Structured duel traces** for a **Local WindBot**,
enter an absolute non-root directory and start an experiment. Each participant
writes client-visible JSONL under `<root>/<experiment>/<room>/<participant>/`.
The room and `matchOrder` identify the accepted match on the dashboard.
`duelNumber` orders duels within a match and `recordedAt` gives the event's UTC
time.

On Windows, **[Duel Log Arena.bat](Duel%20Log%20Arena.bat)** builds the
workspace Release, starts or verifies a prepared server, and enables traces for
agents already configured as Local WindBot. Select the local server reported by
the launcher when creating the experiment. The default output is the workspace's
`Artifacts/windbot-duel-traces/`. Example commands from `Arena/`:

```bat
"Duel Log Arena.bat" -TraceDirectory "D:\Arena-Traces"
"Duel Log Arena.bat" -SkipWindBotBuild -SkipArenaBuild
```

Run the analyzer from the repository root. It orders captures by start time and
exports a summary and decision table.

```powershell
python WindBot/Tools/analyze_traces.py --traces D:/Arena-Traces --output D:/Arena-Traces/analysis.json --decisions-csv D:/Arena-Traces/decisions.csv
```

### Shaddoll 2025 one-click challenge

On Windows, double-click **[Shaddoll 2025 Arena.cmd](Shaddoll%202025%20Arena.cmd)**.
It builds the workspace's normal WindBot Release, supplies the Standard database
if the runtime needs one, and runs Shaddoll's runtime and focused checks.
Select this Release runtime and its English catalog as Arena's candidate.
The launcher verifies the dedicated server and starts one best-of-three match
against Lightsworn. Keep the Arena console open until settlement. Results
appear in the dashboard and SQLite.

Both specialized launchers require Python on PATH and a prepared local
runtime in `Arena/Artifacts/arena-server-v2`. To use another prepared runtime,
pass `-ServerRuntime "D:\path\to\runtime"`. Its manifest supplies both ports.
The launchers reuse a matching server profile or add one. They preserve other
server profiles, agent modes and baseline paths. The hidden server supervisor
has its own [stop procedure](#dedicated-local-server-windows).

```bat
"Shaddoll 2025 Arena.cmd" -GamesPerOpponent 3
"Shaddoll 2025 Arena.cmd" -OpponentDeck Lightsworn -SkipBuild -SkipChecks
"Shaddoll 2025 Arena.cmd" -PreflightOnly -SkipBuild -SkipChecks
```

Preflight performs setup and checks the selected server and roster without
creating a run. To select several opponents, use PowerShell. The English MDPro4
roster marks **Normal** entries `AI_LV3`. The runner accepts Standard opponents
at that level.

```powershell
.\Start-Shaddoll2025Arena.ps1 -OpponentDeck @('Lightsworn', 'SkyStriker')
```

## Configure a SKYNET comparison

1. Build `SKYNET/Skynet.sln` in Release. In **Settings → Agents**, select
   **SKYNET** for the candidate and enter its executable and matching database.
2. Create a profile with a unique matchup ID, display name (up to 17
   characters), absolute `.ydk` path, policy and optional frozen model.
3. Set the same matchup ID on the baseline. For a first run, use
   `SKYNET/Decks/AI_ExpertOldSchool.ydk` on both sides, candidate `skynet`,
   and baseline `expert-oldschool`. Another frozen baseline pairs
   `expert-swordsoul` with `SKYNET/Decks/AI_ExpertSwordsoul.ydk`.
4. Configure a dedicated SRVPro and start a small **Regression**. To examine
   general-policy choices, enable **Decision explanations** before the next run
   and open **Candidate output** or **Baseline output**.

Evaluation disables training and chat. Expert policies run their own decision
lifecycle and take no model. Explanations are bounded recent output for the
general policy. Restarting Arena clears this buffer. For research on the compact
Swordsoul route, freeze both binaries and decklists before a comparison.
[SKYNET policies, training and evaluation](../SKYNET/README.md)

## Participant modes

| Mode | What Arena runs | Where to configure the deck |
| --- | --- | --- |
| Local WindBot | An owned HTTP server for that runtime | The runtime's local English `bot.conf` |
| Remote WindBot | Calls a manually managed WindBot HTTP endpoint | Paste its `bot.conf` or supply its catalog URL |
| SKYNET | One owned `SKYNET.exe` per match participant | A native profile with explicit deck and policy paths |

Give two WindBot endpoints distinct addresses or ports. WindBot's HTTP server
defaults to `127.0.0.1`. For **Remote WindBot** on another machine, launch it
there with `ServerMode=true` and `ServerBindAddress=<private-interface-IP>`,
then enter that private address and service port in Arena. Restrict access to
the trusted network because WindBot accepts unauthenticated launch requests.
Structured trace destinations belong to local agents.

Native profiles share capacity by executable path (2–32, default 4). The
stricter active limit wins and the total cap is 32. Arena reserves all seats for
a room together. Its per-process deadline (10–3600 seconds, default 300)
includes queueing and the entire match.

## Experiments and results

| Experiment | Matchup and scoring |
| --- | --- |
| Regression | Candidate versus baseline on a shared deck or profile ID. Win rate divides candidate wins by total wins. |
| Challenge | One challenger versus selected opponents. Bulk selection skips itself. |
| Ranking | Repeatedly samples candidate deck pairs until stopped. |
| Tag smoke test | WindBot-only tag duels. Counts track launches and per-deck appearances. |

Standard runs exclude `AI_RD*` Rush profiles. Each dedicated SRVPro accepts one
experiment at a time. Different instances can run in parallel. Regression,
Challenge and Ranking use best-of-three `M#` rooms. API fields such as
`gamesPerMatchup` count **matches** for these modes. The dashboard gets wins
from cumulative SRVPro rankings and reports disconnects separately. A generic
duel server or a standalone native smoke fixture lacks these management APIs.
[Read-only results API](API.md)

<details>
<summary>Recorded OldSchool comparisons</summary>

| Run | Outcome |
| --- | --- |
| Four-match integration pilot | 2–2 |
| Initial 20-match mirror on 2026-10-20 | General SKYNET 2–18 against the OldSchool expert |
| Updated shared card behaviors, 20 matches per opponent | 18–2 against previous SKYNET and 9–11 against the OldSchool expert |

The 20-match runs used shuffled decks without a fixed seed and recorded zero
disconnects. Weights stayed frozen. These observations apply to the recorded
decks, versions and match conditions. Use both seats, varied decks and larger
held-out comparisons for strength evaluation. [Evaluation inputs and receipts](../SKYNET/Catalog/expert-baselines.json)

</details>

## Dedicated local server (Windows)

Arena's explicit bootstrap uses pinned
[arena-srvpro](https://github.com/mercury233/arena-srvpro) commit
`af87d3a8c4269c105144a67221728aa25a747910` and its single locked `deepmerge`
dependency. The recorded source archive has this SHA-256 digest.

```text
c6779f6d7d226bae22fa27f4eb0b860d6d5a27a575b5d430baca158918e24d37
```

The staged runtime includes the upstream source and AGPL-3.0 license.
The bootstrap verifies the complete allowlisted runtime file set before patching
only listener addresses, the native host name and its parent-process guard.

Run the following from the `Arena/` directory, using Python 3.12 or newer,
Node.js 24 and the reviewed Visual Studio 2022 C++ toolchain. These examples
require explicitly supplied local inputs; they do not download dependencies or
search neighboring directories.

1. Export exact MDPro4 server/core commits and reuse dependency sources from an
   explicit previous Windows build candidate. The core must match the selected
   server's `mdpro4/build/windows-server.lock.json`. `build_arena_native.py`
   patches only the isolated copy's native listener from `INADDR_ANY` to
   `INADDR_LOOPBACK`, builds it and writes an input/build receipt.

   ```powershell
   python Tools/build_arena_native.py `
     --server-repository D:/YourSources/mdpro4-ygopro `
     --server-commit 9b65c3fc90a782742a92c3e9afa685aa0eb3feca `
     --core-repository D:/YourSources/mdpro4-ygopro-core `
     --core-commit 25eb27f6292ea407f615958fd5ff1d1db77066dc `
     --dependency-candidate D:/YourBuilds/windows-server-candidate `
     --work-dir Artifacts/arena-native `
     --msbuild 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe'
   ```

2. Build the owned `SKYNET_DUEL_HOST.exe` with explicit engine and data inputs.
   It accepts twelve native room arguments and lives for the room's lifetime.
   For a trusted installed MDPro4 `ygoserver.dll`, skip step 1 and pass that DLL
   to `-ServerLibrary`. The v3 host permits only IPv4 loopback through the DLL's
   Winsock bind import and redirects wildcard requests to `127.0.0.1`. Its
   receipt pairs the executable and DLL hashes.

   ```powershell
   ./Tools/Build-DuelHost.ps1 `
     -Destination Artifacts/arena-host `
     -ServerLibrary Artifacts/arena-native/source/bin/release/x64/ygoserver.dll `
     -Database D:/YourGame/Data/locales/en-US/cards.cdb `
     -ScriptsArchive D:/YourGame/Data/script.zip
   ```

3. Supply the exact upstream source checkout/archive, then stage a fresh ignored
   runtime. The example uses the source-build receipt. For an installed DLL,
   set `--native-library` to `Artifacts/arena-host/ygoserver.dll` and
   `--native-receipt` to `Artifacts/arena-host/host-receipt.json`. Rebuild an
   older v2 host to use its receipt for new staging.

   ```powershell
   python Tools/arena_server.py prepare `
     --source-root Artifacts/arena-srvpro-source --runtime-dir Artifacts/arena-server-v2 `
     --native-host Artifacts/arena-host/SKYNET_DUEL_HOST.exe `
     --native-library Artifacts/arena-native/source/bin/release/x64/ygoserver.dll `
     --native-receipt Artifacts/arena-native/arena-native-receipt.json `
     --database D:/YourGame/Data/locales/en-US/cards.cdb `
     --scripts D:/YourGame/Data/script.zip
   npm --prefix Artifacts/arena-server-v2 ci --ignore-scripts --no-audit --no-fund
   python Tools/arena_server.py serve `
     --runtime-dir Artifacts/arena-server-v2 --node D:/YourNode/node.exe --minutes 60
   ```

4. In Arena's SRVPro settings, enter host `127.0.0.1`, duel port `17911`,
   management port `17922`, username `skynet-local`, and the generated password
   from that runtime's `config/admin_user.json`. Set at most two concurrent rooms
   for the default four-bot process limit. Keep credentials and runtime files
   outside commits. The dashboard and duel supervisor are separate processes.

New examples use `Arena/Artifacts/`. Previously staged workspace-root
`Artifacts/` runtimes remain valid; pass their explicit paths when resuming them.
The bootstrap's output boundary allows both ignored locations within this
workspace. It never relocates or overwrites an existing staged runtime.

The supervisor accepts authenticated Arena reboots, bounds its duration/restarts
and log size, and uses a private Windows job to retire its process tree on exit
or abrupt supervisor termination. An exclusive lease prevents duplicate owners.
Create `stop.request` inside the selected runtime to stop it. Remove that exact
file explicitly before a later restart. A stale lease after forced termination
requires checking that its recorded supervisor has exited before removing it.
There is no global service, firewall change or installed-game replacement.

This bootstrap enables shuffling and disables deck legality checks for unrestricted
research comparisons. It does not expose the core RNG seed. Record these settings
with results because they affect comparisons. Native hosts reject
`YGOPRO_EXPANSIONS` and `YGOPRO_EXTRA_SCRIPT` overrides so staged input hashes
remain meaningful.

## Local data and hosting

| Variable | Default | Use |
| --- | --- | --- |
| `SKYNET_ARENA_HOST` | `127.0.0.1` | Dashboard and API bind address |
| `SKYNET_ARENA_PORT` | `3000` | HTTP port |
| `SKYNET_ARENA_DATABASE` | `data/arena.sqlite` | SQLite path relative to Arena, or absolute |

The former `WINDBOT_ARENA_*` variables remain fallback aliases. Back up
`data/` to preserve settings and history. Migrations run on startup. The local
console has no built-in login. For shared hosting, use an authenticated reverse
proxy and enable unbuffered SSE. The PM2 example binds to localhost.

## Development

Run these from `Arena/`.

```sh
npm run dev
npm test
npm run build
python Tools/test_arena_process_owner.py
```

The tests cover scheduling with mocked SRVPro responses and check process
ownership with controlled children. A completed match on a compatible dedicated
host provides separate protocol evidence.

Arena derives from [WindBot Arena](https://github.com/mercury233/windbot-arena)
at commit `371e62de0b0b29a8f097f84293e9f8f0f15a7509`. The upstream package
declares MIT. The imported snapshot records its original file hashes.
[Import provenance](UPSTREAM.json) · [Contributor instructions](AGENTS.md)
