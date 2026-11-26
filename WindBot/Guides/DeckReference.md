# WindBot deck reference

[WindBot quick start](../README.md) · [Write an executor](ExecutorAuthoring.md) · [Registered sources](../Game/AI/Decks/) · [Deck-specific tools](../Tools/Executors/)

Look up a deck by its exact registration. Commands on this page run from the **workspace root**. Use a fresh output directory for each check. Each example selects its card database explicitly. In native examples, replace `D:/YourGame` with the installation whose database, scripts and server you intend to test.

| Deck or topic | What you can check |
| --- | --- |
| [The Crimson King](#the-crimson-king) | RDA planner scheduling, fixed hands and frozen memory settings |
| [Rush Duel](#rush-duel) | Eight registrations, card effects, prompt allocations and rematch cleanup |
| [Metalmorph and Odion](#personal-executor-checks) | Effect selection, prompt ownership and the rejected Odion candidate |
| [Witchcraft 2026](#witchcraft-2026-executor) | Planner, graveyard effects and fixed-opening guide |
| [Voiceless](#voiceless-voice-routes-and-recovery) | Ritual setup and recovery after interruption |
| [Sky Striker 2026](#sky-striker-2026-executor) | Planner, battle, recovery and mirror fixtures |
| [Destiny Board](#bakura--destiny-board) | Bakura's prerelease database overlay and native scenarios |
| [Blue-Eyes 2025](#blue-eyes-2025) | Spirit revival targets, Tyrant Dragon trap selection and callback checks |
| [Shaddoll 2025](#shaddoll-2025-executor) | Lev/Void routes, quick Fusions, suite and match records |
| [Import provenance](#executor-origins) · [Recorded comparisons](#recorded-validation-and-arena-results) | Revisions, tested binaries and opponent-specific results |

## Rush Duel

The MDPro4 roster has eight Rush registrations. Pass the exact name to `--deck`. Each loads its matching `WindBot/Decks/AI_<registration>.ydk` file.

| MDPro4 roster group | WindBot registrations |
| --- | --- |
| `AI_RD1` | `RushDragonDragears` |
| `AI_RD2` | `BusterBlader`, `DinocarriageDynarmix`, `Mituzi`, `SaikyoBattleFlag`, `TheLegend` |
| `AI_RD3` | `Excutie`, `ObsidianMagical` |

Build WindBot and run the offline checks for all eight decks together. The suite loads every listed card, exercises the effect callbacks and records allocated bytes over 12,000 prompts per deck. It also checks that completed duel objects can be collected and that a failed launch releases the client.

```powershell
python WindBot/Tools/bot_tool.py build --configuration Release
python WindBot/Tools/bot_tool.py check --suite rush --configuration Release --database DeckWorkshop/Data/rush/cards.cdb --deck-dir WindBot/Decks --output Artifacts/rush-check
```

Open `Artifacts/rush-check/report.json` for the per-deck results and the hashes of the executable and card database. To compare builds, repeat the check with `--bot-exe` pointing at a frozen executable and a fresh output directory. The allocation measurement covers the WindBot thread answering the prompts.

To play, open a room on a compatible host configured with **Rush rules, cards and scripts**. Substitute its address and port in this example.

```powershell
python WindBot/Tools/bot_tool.py run --configuration Release --database DeckWorkshop/Data/rush/cards.cdb --deck SaikyoBattleFlag --host 127.0.0.1 --port 7911
```

**November 2026 memory check.** Four failed deck launches left four room connections open in the original build. The fix rejected all four before connecting. Across 12,000 synthetic prompts per deck, Release allocations fell from 866 bytes per prompt for Mituzi and 1,004 for the other decks to 134 for all eight. Both builds released all tracked executors, duels and AI instances after 32 simulated duel lifetimes per deck.

The repaired Debug build finished 24 duels on a native MDPro4 Rush host, eight with fixed deck order and 16 shuffled. Every deck played first and second. Both clients recorded all 24 endings, and each batch finished with zero established room connections. During the 16 shuffled games the persistent WindBot worker used 39.44–39.93 MB of private memory after games.

MDPro4's Solo launcher can run its own embedded WindBot on a thread inside the game. That copy needs the fixes integrated into the game build. The measurements above cover this repository's standalone WindBot. For a report from Solo play, record the MDPro4 version, deck, process memory before and after each rematch, and whether a launch failed.

## Executor origins

These additional executors are included in the normal bot. They are
listed in `BotWrapper/bot.conf` with `Dialog=Universal.EN`; their original executor
registrations are preserved. Difficulty flags are left unassigned until measured.

| Roster name | `Deck=` | Source |
| --- | --- | --- |
| Sirokami Dark Magician | `SirokamiDarkMagician` | [PR #296](https://github.com/IceYGO/windbot/pull/296), pinned `12d5270` |
| Raise Moon | `RaiseMoon` | [PR #297](https://github.com/IceYGO/windbot/pull/297), pinned `12a1d1b` |
| Metalmorph | `Metalmorph` | Supplied personal source and deck |
| Odion | `Odion` | Supplied personal source and deck |
| Bakura - Destiny Board | `DestinyBoard` | Workspace implementation; unrestricted, selected prerelease Necrofear |

The pinned upstream revisions were unreviewed at import. The
[upstream receipt](../executor-provenance.json) records exact revisions, script
evidence, local compatibility changes and focused checks. The
[personal-executor receipt](../personal-executor-provenance.json) records the supplied
files and fixes. Metalmorph's obsolete `100256007` and Odion's `100257013` were
mapped to released cards `76636978` and `27118421`, respectively, using the selected
database and matching Lua scripts. Both `.ydk` files and their handlers use those IDs.

The personal executors have a follow-up script and callback audit described below.
Coverage is bounded; large untested branches remain. Sirokami's upstream combo gate is disabled in its submitted code.
RaiseMoon's optional lexical script helper accepts `SHENGYUE_SCRIPT_DIR` or a
runtime-local `script/` directory, with bounded reads; it does not inspect an
engine's private state or extract `script.zip`. Its guesses are not rule simulation.

## The Crimson King

The live RDA planner now shares one monotonic deadline across background queueing,
search-slot wait, route search and post-processing. Cancellation retires waiting
work, and concurrency/node/candidate caps bound resource use. Three fixed model
hands (Soul, Bone, and Power Vice + Darkness) retain the baseline's exact final
boards, scores and step counts. This establishes those model regressions, not
full rules correctness or a higher win rate.

The import receipts retain their original compatibility checks and native pilots.
Those pilots establish completion for the recorded binaries, not current
competitive strength. Run the checks and compare frozen builds after each
behavior change.

Run the maintained checks from the workspace root with explicit inputs:

```powershell
.\WindBot\Tools\Executors\TheCrimsonKing\Invoke-RdaPlannerChecks.ps1 `
  -BotExecutable WindBot/bin/Release/WindBot.exe `
  -Database 'DeckWorkshop/Data/standard/cards.cdb' `
  -Deck WindBot/Decks/AI_TheCrimsonKing.ydk `
  -OutputDirectory Artifacts/rda-checks-my-change
```

Use a new output directory for each receipt. Combo fixtures exercise specific
hands, expected actions, interruptions and resets. Native fixtures then verify
actual script prompts. [Arena](../../Arena/README.md) compares preserved baseline and
candidate binaries across opponents; its current UI does not inject hands or
assert combo milestones. Keep match results separate from fixed-hand model tests.
RDA has its own local opponent memory. For a fixed-opponent benchmark, create an
empty `WindBot_knowledge/TheCrimsonKing_knowledge.json.off` file beside **each
participating executable** before launching any clients. This existing marker
disables both collection and memory-based decisions. Verify the trace reports
`memory: disabled, deciding without it`; a frozen executable alone does not freeze
RDA's policy when its memory file keeps changing. Keep normal play's memory
settings separate from these isolated benchmark runtimes.

Use a **Debug** normal WindBot build with the existing `native_smoke.py` verifier:
the original client's start/result markers are compiled out in Release, so a
Release duel cannot satisfy that tool's completion assertions. Arena uses the
server's ranking API and can compare Release binaries.

## Personal executor checks

Metalmorph retains its supplied deck list, registration and strategic priorities.
Its maintained checks exercise real WindBot dispatch and selection callbacks
against the selected card database. **Odion retains its original executor.** Its
attempted callback/effect fixes passed 16 focused checks but scored worse in Arena,
so the broad candidate was rejected. Its source, patch, harness and receipts remain
in local `Artifacts/executor-review-20261102/rejected-odion/` for investigation.

Metal Illusionist distinguishes its summon tutor from its race
change, Flame uses the actual replacement prompt, and Time Engine answers its
resolution benefits without re-running activation logic. Fullmetal chooses burn
damage using original ATK. Max/Flame, Phoenix and Accesscode selections follow
the scripts' actual cost/target/resolution prompts without duplicate queues.
Callback identity comes from accepted actions or immutable chain snapshots.

```powershell
.\WindBot\Tools\Executors\Metalmorph\Invoke-MetalmorphChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -Database 'DeckWorkshop/Data/standard/cards.cdb' `
  -Deck WindBot/Decks/AI_Metalmorph.ydk `
  -OutputDirectory Artifacts/metalmorph-checks-my-change
```

Use fresh output directories. These synthetic checks establish dispatch, prompts
and state behavior; real Lua-backed duels and Arena results are separate evidence.
The [personal-executor receipt](../personal-executor-provenance.json) retains source
origins, exact script hashes and before/after validation.

## Witchcraft 2026 executor

`Deck=Witchcraft2026` adds a separate **Test** registration and
`Decks/AI_Witchcraft2026.ydk` (40 Main / 15 Extra / 7 Side). The original
`Witchcraft` executor/deck remain unchanged. Imported from the selected deck and
implementation at `DaruKani/SKYNET` revision `94a977b2`; no external game files
or databases are bundled by this addition.

The RDA-style planner separates observations, resource-checked predictions and
actual engine offers. It compares guide routes, alternatives and stopping, then
commits one action and replans. Limits are 180 ms, depth 30, beam 64, 5,000 nodes
and 256 candidates per expansion. The guide uses Walpurgis, the low-Witch ladder,
Marka/Dragoon, Terracottan/Pupils/Rilliona, Vice and Edel revival. Fieldless
Lacrima/Maid access and interrupted-route recovery are modeled separately.

The executor's `OpponentAwareness` helper records reviewed public effects and observed usage;
unknown identities, costs and outcomes remain uncertain. Chain forecasts separate
removal from negation, preserve paid costs and reselect actual resolution targets.
They are bounded predictions, not a full opponent simulator or win probabilities.

GY handling covers the 15 applicable identities in this selected list: Seed,
Pittore, Potterie, Schmietta, Genni, Marka, Distortion, Madame Rilliona,
Despian Luluwalilith, Creation, Unveiling, Bystreet, Confusion, Celebration and
Side-deck Draping. Seed returns an exact Spell-containing hand subset and stops
prediction at the unknown draw; Pittore chooses from the actual post-draw hand.
Potterie checks the empty hand; Distortion tracks GY age and discards at resolution.
Genni supports copied Fusion/destruction, recruit/send, Unveiling and bounce,
sharing its copy limit and paying banishes before selecting materials.
Marka/Rilliona/Luluwalilith use real engine-offered triggers. Luluwalilith has
**no qualifying recruit in the current Main Deck**; its native fixture uses
Effect Veiler only as a test input. Lacrima has no own GY activation.

Known limits: heuristic draw valuation, finite action coverage, partial public
effect profiles, aggregated same-name model resources, no automatic side decking,
and no guarantee of a globally optimal line. Pittore deliberately passes with a
nonempty all-non-Witch hand. Draping copy evaluates one useful public S/T target.
Unmodeled Link/Synchro routes and hidden opponent responses are not invented.

Run these commands from the workspace root. The first joins a compatible room
already listening on the specified host and port.

```powershell
WindBot\Tools\Executors\Witchcraft2026\Run-Witchcraft2026.bat 127.0.0.1 7911 "DeckWorkshop\Data\standard\cards.cdb"
WindBot\Tools\Executors\Witchcraft2026\Run-Witchcraft2026.bat --build-only
./WindBot/Tools/Executors/Witchcraft2026/Invoke-Witchcraft2026PlannerChecks.ps1 -BotExecutable WindBot/bin/Debug/WindBot.exe -Database DeckWorkshop/Data/standard/cards.cdb -OutputDirectory Artifacts/witch-planner
./WindBot/Tools/Executors/Witchcraft2026/Invoke-WitchcraftOpponentChecks.ps1 -BotExecutable WindBot/bin/Debug/WindBot.exe -Database DeckWorkshop/Data/standard/cards.cdb -OutputDirectory Artifacts/witch-opponent
./WindBot/Tools/Executors/Witchcraft2026/Invoke-Witchcraft2026NativeChecks.ps1 -BotExecutable WindBot/bin/Debug/WindBot.exe -Python python -ServerLibrary D:/YourGame/MDPro4_Data/Plugins/x86_64/ygoserver.dll -DataRoot D:/YourGame/Data -OutputDirectory Artifacts/witch-guide -AllowVerifiedLethal
python WindBot/Tools/Executors/Witchcraft2026/witchcraft_grave_native.py --scenario seed --core D:/YourGame/MDPro4_Data/Plugins/x86_64/ocgcore.dll --database D:/YourGame/Data/locales/en-US/cards.cdb --scripts D:/YourGame/Data/script.zip --output Artifacts/witch-seed.json
```

Use fresh receipt paths, Python 3.11+ and the installed scripts and CDB selected
for the native host. Both 2026 Windows launchers build Debug, accept a CDB as
their third argument and a room password as their fourth. With the CDB omitted
they use the bundled `DeckWorkshop/Data/standard/cards.cdb`. Each launch writes a
timestamped decision log under `Artifacts/executor-logs/`.

Upstream-based branch validation (2026-11-15): Debug/Release builds; 90/90 planner,
119/119 opponent/GY and 14/14 RDA regression assertions. Full-bot guide: two
completed wins against passive Default, **39/44 strict milestones**, with a
separately verified early lethal in duel 1. Strict misses are retained, not
renamed passes. The earlier source-runtime run reached 44/44; bounded scheduling
can select a shorter win. Native GY fixture drivers pass 43/43 assertions across
21 scenarios, verifying mechanics separately from executor strategy. No Arena
strength comparison is claimed.

## Voiceless Voice routes and recovery

`Deck=Voiceless` loads `Decks/AI_Voiceless.ydk`. Its 40-card main deck includes
Blessing; the nine-card Extra Deck includes Linkuriboh and Secure Gardna. The executor uses the RDA structure of
observed state, isolated card transitions, bounded search and actual-offer
execution. It does not run Lua inside its planner or share SKYNET training state.

The five supplied routes are implemented with these minimum starters (assuming
the required remaining deck/Extra Deck cards and no initial restrictions):

| Starter | Own-turn route | Opponent-turn follow-up |
| --- | --- | --- |
| Lo | Barrier, Saffira, Skull Guardian, then Dyna Mondo | Revive Skull, revive Lo and place Radiance. |
| Diviner | Send Trias, Tribute Diviner, recruit Lo, then the Dyna route | Revive Skull/Lo and place Radiance. |
| Lo plus an extra Spell | Establish Skull, Link Lo into Linkuriboh, summon Dragon Sage | Respond with Sage to summon Sauravis, revive Lo and place Radiance. |
| Pre-Preparation plus a suitable Level 7+ LIGHT tribute | Prayers summons Skull; find Lo, Barrier and Dragon Sage | Sage supplies the second Ritual, Lo and Radiance. |
| Lo plus Skull Guardian | Barrier finds Blessing; Linkuriboh sends Lo to GY; Blessing recovers Lo; Secure Gardna triggers the Ritual Summon | Blessing responds to a non-Ritual summon with Sauravis; Lo returns and places Radiance. |

Blessing does not perform Link Summons itself. Opponent-turn effects are
conditional opportunities, not disruption already available at the end of our
turn. The planner compares current protection with those opportunities and
replans from actual card locations, negation, effect usage and observed search
locks. It supports alternatives after interruptions instead of advancing an
unchecked fixed combo counter. Search is synchronous, capped at 100 ms, 2,048
expanded states, beam width 128 and depth 24, with cancellation and a bounded
per-duel cache. This is a model of the supported routes, not a complete duel
simulator or an arbitrary ritual-material solver; the server still owns legality.

Location/effect-specific handlers and script-stage selection callbacks cover Lo,
Barrier, Saffira, Skull Guardian, Prayers, Diviner/Trias, Blessing, Dragon Sage,
Sauravis, Radiance and the three Link routes. Single optional triggers with an
unknown description use observed summon context. Selection and effect-use state
reset at their actual chain/phase/turn boundaries. Existing names remain. Voiceless now uses the default `Normal` registration and
can enter the normal random-deck pool. The measured scenarios do not imply
exhaustive card, matchup or tag-duel coverage.

Run focused dispatcher/prompt checks from the workspace root:

```powershell
.\WindBot\Tools\Executors\Voiceless\Invoke-VoicelessChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -Database 'DeckWorkshop/Data/standard/cards.cdb' `
  -Deck WindBot/Decks/AI_Voiceless.ydk `
  -OutputDirectory Artifacts/voiceless-focused-my-change
```

Run the five real-engine routes, Ash/Droll recovery, Veiler protection and a
resolved-Veiler recovery on the next own turn (nine scenarios):

```powershell
python WindBot/Tools/Executors/Voiceless/voiceless_native_checks.py `
  --bot-exe WindBot/bin/Debug/WindBot.exe `
  --opponent-exe D:/YourFrozenBot/Debug/WindBot.exe `
  --data-root D:/YourGame/Data `
  --server D:/YourGame/MDPro4_Data/Plugins/x86_64/ygoserver.dll `
  --deck WindBot/Decks/AI_Voiceless.ydk `
  --output-dir Artifacts/voiceless-native-my-change
```

Both commands require fresh output directories. The native runner takes explicit
host/opponent inputs, forces the fixture play order through rock-paper-scissors,
verifies the observed opening hand, and asserts actual moves and board milestones
through the opponent's next turn. The resolved-Veiler case instead checks the
next own turn: its opening has no surviving extender, so recovery uses the
fixture's known Saffira draw rather than inventing a first-turn continuation.
Interruption assertions require an ordered, incomplete-board-to-recovery sequence
and check the stopped search/placement; the separate protection case checks
Skull Guardian answering Veiler. Its no-shuffle/no-deck-check fixtures and
scripted opponent deck lists are correctness tests, not legal-format strength
comparisons. Core RNG is not seeded or claimed reproducible. The supplied native
DLL and frozen baseline are local ignored artifacts; select your own equivalent
paths on another machine. Use a Debug client for its authoritative result logs.

Exact source/script/deck hashes and separate focused, native and Arena evidence
are recorded in [voiceless-provenance.json](../voiceless-provenance.json).

## Sky Striker 2026 executor

`Deck=SkyStriker2026` is a separate normal registration using
`Decks/AI_SkyStriker2026.ydk` (40 Main / 15 Extra / 15 Side). The original
`SkyStriker` executor/deck remain unchanged. Imported from the tested
implementation at `DaruKani/SKYNET` revision `94a977b2`, with a distinct roster
entry. No dependency on the separate Witchcraft executor is required.

The bounded RDA-style route planner compares executable continuations and retained
resources. Each actual offer, chain outcome and interruption can invalidate the
forecast. Raye/Roze access, Zero setup, Engage/Kagari recovery, Linkage battle
extension, Lemnisgate field/GY conversion, modern Ace links and the
Fallen/Dogma/Ecclesia package have explicit decisions and offered-card callbacks.
Draw/search restrictions and the Main Monster Zone condition constrain planning;
a plan cannot reserve an unavailable search and suppress a real setup action.

Battle planning uses observed attackable targets, spent attackers and current
damage modifiers, with bounded search and replanning after each event. It does
not assume unknown backrow or hand responses are absent. Removal is not treated
as negation; known GY follow-ups and paid costs are accounted for. Lemnisgate
compares conversion/timing value against a free normal Link or immediate battle.
This remains a partial heuristic model, not universal effect coverage or an
optimal-play guarantee. Side-card handlers do not imply automatic side decking.

Run these commands from the workspace root. Only the first needs an open room.

```powershell
WindBot\Tools\Executors\SkyStriker2026\Run-SkyStriker2026.bat 127.0.0.1 7911 "DeckWorkshop\Data\standard\cards.cdb"
WindBot\Tools\Executors\SkyStriker2026\Run-SkyStriker2026.bat --build-only
./WindBot/Tools/Executors/SkyStriker2026/Invoke-SkyStrikerPlannerChecks.ps1 -BotExecutable WindBot/bin/Debug/WindBot.exe -CardDatabase DeckWorkshop/Data/standard/cards.cdb -OutputDirectory Artifacts/sky-planner
./WindBot/Tools/Executors/SkyStriker2026/Invoke-SkyStrikerRecoveryChecks.ps1 -BotExecutable WindBot/bin/Debug/WindBot.exe -CardDatabase DeckWorkshop/Data/standard/cards.cdb -OutputDirectory Artifacts/sky-recovery
./WindBot/Tools/Executors/SkyStriker2026/Test-SkyStriker2026.ps1 -Python python -DataRoot D:/YourGame/Data -Server D:/YourGame/MDPro4_Data/Plugins/x86_64/ygoserver.dll -OutputDirectory Artifacts/sky-native -MirrorGames 2
```

Use fresh receipt paths, Python 3.11+ and explicitly selected compatible CDB,
scripts and duel server. The test wrapper builds Debug/Release, runs focused
checks, recovery fixtures, fixed-opening native scenarios and the requested even
number of mirror games. Its seed controls fixture deck permutations, **not** the
unknown core RNG. Runtime inputs, logs and result receipts stay under ignored
`Artifacts/`; no external scripts or game settings are edited.

The Windows launcher writes its Debug decision log under `Artifacts/executor-logs/`.

Upstream-based branch validation (2026-11-15): Debug/Release builds and focused
92/92 planner/policy plus 18/18 recovery assertions. Native scripted recovery
passes 6/6 cases; five fixed-opening scenarios and two paired mirror games pass
their behavior/completion audits (designated client: one win, one loss).
These checks are not a frozen Arena comparison or a competitive-strength claim.
Normal registration is a selection category, not a certification of playing skill.

## Recorded validation and Arena results

The loss-analysis update preserves all three deck lists. Voiceless now targets
live effects beneath graveyard chain links, coordinates Ash with an available
Droll against opening Gaia, and preserves the Skull/Lo core and direct attacks
when considering S:P. The previous three-match RDA/Swordsoul results lacked
usable decision traces; new diagnostic duels supplied the concrete findings.

The accepted combined build passes Debug/Release and C# 6 compilation, **70 focused
checks** (39 Voiceless, 31 Metalmorph), the 22 preserved import checks,
and all **nine native Voiceless combo/recovery scenarios**. Two paired interaction
fixtures also demonstrated the new Chixiao and Gaia responses; the RDA fixture
still lost after the corrected response. These establish behavior, not guaranteed
wins through every interruption.

The Arena comparison uses preserved revision `363f7eb` against candidate Debug
`8ff56a0c`, identical deck lists, the same database/scripts and frozen opponents.
The accepted build `7516a6ab` restores original Odion and promotes Voiceless to
`Normal`. Voiceless decision code differs from the measured candidate only in its
registration attribute; Metalmorph source is identical. Builds and all checks above
were rerun. The Arena runs retain their original binary identity.
RDA memory collection and decisions are disabled and verified in the traces.
Scores below are authoritative server **best-of-three match** wins-losses, not
individual duel counts.

| Executor | Opponent | Original W-L | Candidate W-L | Decision |
| --- | --- | --- | --- | --- |
| Voiceless | RDA | 12-8 | 15-5 | Accept fixes |
| Voiceless | Swordsoul | 9-11 | 12-8 | Accept fixes |
| Metalmorph | RDA | 0-6 | 1-5 | Accept fixes |
| Metalmorph | Swordsoul | 0-6 | 4-2 | Accept fixes |
| Odion | RDA | 0-6 | 0-6 | Retain original |
| Odion | Swordsoul | 9-11 | 2-18 | Retain original |

All **156 scored matches / 312 clients** completed with zero recorded flees, client
errors or forced client cleanup. Thirty-four diagnostic matches with evolving RDA
memory or a misnamed memory marker are excluded from this comparison.

Voiceless and Metalmorph improved in these samples. Odion's candidate fell from
9-11 to 2-18 against Swordsoul despite passing its prompt checks. Three controlled
opening pairs did not isolate a causal regression, and two pairs diverged in
unseeded Swordsoul choices before changed Odion actions. That uncertainty does not
justify promotion: the accepted source retains original Odion. Its existing
TY-PHON material-reservation policy and Metalmorph's willingness to tribute a
fresh Synchro interaction remain known tactical limits for separate testing.

Shuffled draws were not paired, the core RNG seed is unknown, and first-turn
choices were not forced. This no-deck-check pool and the sample sizes do not
establish a universal or legal-format win rate. RDA's broad removal boards and
repeated interruptions still beat supported Voiceless routes. The local comparison
uses task-owned loopback HTTP-to-CLI adapters because Windows rejected the normal
server's wildcard URL reservation; real frozen clients and SRVPro rankings are
used without changing Windows ACLs.

Exact inputs, per-run results, confidence intervals, trace findings and separate
validation receipts are retained in [Voiceless provenance](../voiceless-provenance.json),
[personal-executor provenance](../personal-executor-provenance.json) and local
`Artifacts/executor-review-20261102/`.

## Bakura / Destiny Board

`Deck=DestinyBoard` loads `Decks/AI_DestinyBoard.ydk`: the selected Bakura-focused
40-card Main / 15-card Extra Deck, using unrestricted/custom rules. It includes
three Pot of Greed and Graceful Charity, two Evil Lord - Zorc (`73320394`), and
two prerelease Dark Necrofear, the Ruler of Demise (`100268007`). This list is not
legal under ordinary tournament banlists. The original Necrofear, Curse, both
Dark Spirits, Spirit Shield and full Board package retain the Bakura theme.

Prepare the selected prerelease data without changing your installed game:

```sh
python DeckWorkshop/prepare_runtime.py --expansion DeckWorkshop/Expansions/bakura-prerelease --output Artifacts/bakura-data
python WindBot/Tools/bot_tool.py build
python WindBot/Tools/bot_tool.py check --suite runtime --database Artifacts/bakura-data/locales/en-US/cards.cdb --deck WindBot/Decks/AI_DestinyBoard.ydk --registration DestinyBoard --output Artifacts/bakura-runtime-check
python WindBot/Tools/bot_tool.py check --suite destiny-board --database Artifacts/bakura-data/locales/en-US/cards.cdb --deck WindBot/Decks/AI_DestinyBoard.ydk --output Artifacts/bakura-decision-check
```

Use `python3` on Linux and follow the Mono prerequisites above. Output directories
must be new. The bot and duel host must load the same prepared CDB; the host must
also load the prepared `script.zip`. The base Workshop catalog remains released
Standard; the optional prerelease snapshot and its provenance are under
`DeckWorkshop/Expansions/bakura-prerelease/`. The exporter refuses future ID
collisions rather than silently replacing a released card.

For an explicitly configured compatible duel server, replace host/port/version
with its actual settings:

```sh
python WindBot/Tools/bot_tool.py run --database Artifacts/bakura-data/locales/en-US/cards.cdb --deck DestinyBoard --host 127.0.0.1 --port 7911 --debug
```

The deck uses a small synchronous RDA-style boundary between observations,
material/board evaluation, and the server's legal offers. It does not execute Lua
inside the client or predict a favorable die roll. Extra Deck evaluations share
a bounded deadline; each decision is reconsidered after actual moves and chains.

Opening and recovery routes include:

- Tour Guide recruits Doomdog Octhros, then Cherubini sends Rhino. Octhros finds
  Ruler and Rhino sends another Fiend, establishing the three-Fiend GY threshold.
- Zorc discards itself and Rhino, searches Ruler, then Rhino supplies the third
  Fiend. Ruler's own summon counts these cards without banishing them.
- Dark Spirits and Muckraker revive legal Fiends. Original Necrofear cannot be
  revived before its proper summon; the new Ruler has a different restriction.
- Sentence advances missing later letters without consuming the naturally due
  I/N. Board's progress belongs to each Board instance, survives turn changes,
  and resets when that card leaves or is reset.
- Zorc's adverse die roll can destroy Ruler for its return/control/burn sequence,
  or select an immune Sanctuary message. Ruler needs an eligible opposing monster;
  Rhino and Cherubini protection can prevent intentional destruction.

The Extra Deck has purposes beyond simply summoning the largest available card:

| Card | Intended timing / role |
| --- | --- |
| Cherubini (2) | Early graveyard setup; use its mill before spending it as material. |
| Muckraker (2) | Revival with expendable discard; account for its Fiend-only lock and prohibition on Link material during its Link Summon turn. |
| Darius → Deus Machinex | Alternate two-Level-3 Fiend route; preserve Darius's battle opportunity when useful, then overlay. Machinex absorbs a monster; it does not negate that activation. |
| Dingirsu | Rank-8 non-targeting removal and destruction protection for Board. |
| Hope Harbinger | Rank-8 Spell interaction when holding bodies is less useful. |
| Dharc | Exploit an eligible opposing DARK monster in the GY. |
| Phoenix / Unicorn | Backrow destruction / broader shuffling with a worthwhile discard. |
| S:P Little Knight | Removal and temporary banishing; preserve useful direct attacks when its summon effect would forbid them. |
| Unchained Soul Rage Abominator | Opposing-turn disruption, potentially destroying Ruler for its follow-up trigger. |
| Unchained Abomination | Repeat destruction when the field presents useful targets. |
| Underworld Goddess | Remove an opposing monster through its legal material procedure when sufficient expendable effect monsters exist. |

Spirit Messages are protected from ordinary material, tribute, discard and
replacement preferences. Backrow/monster capacity, imminent Board wins, graveyard
fuel and retained interaction affect whether another summon is worthwhile.
A late A/L may be discarded to retain protection when Board remains accessible
and a retained Sentence or Mastery supplies a recovery route. The naturally due
letter and the fourth missing letter of an imminent win remain protected; the
choice also accounts for known negation and banishment effects. Phoenix keeps a
live Ruler recovery plan when its only payoff is probing unknown backrow.
Because the client does not observe the engine's pending-destruction flags,
Muckraker deliberately declines optional destruction replacement while message
monsters exist; Cherubini declines it while any Board/message exists. This avoids
accepting a replacement whose later mandatory payment might sacrifice the win
condition. Dingirsu's overlay-based destruction protection remains available.
Unrelated archetype branches with no supporting cards (such as Darius's Dark
Contract effect or Machinex's Pendulum setup) are intentionally declined.

Run reproducible native scenarios against the explicitly selected native host
using Debug builds (movement assertions need debug traces):

```sh
python WindBot/Tools/Executors/DestinyBoard/destiny_board_native_checks.py --bot-exe WindBot/bin/Debug/WindBot.exe --opponent-exe /path/to/frozen/WindBot.exe --data-root Artifacts/bakura-data --server /path/to/ygoserver.dll --deck WindBot/Decks/AI_DestinyBoard.ydk --output-dir Artifacts/bakura-native
```

On Linux, supply a compatible Linux native duel library in `--server` and Mono
on PATH (or pass `--mono /path/to/mono`). A Windows `ygoserver.dll` cannot run on
Linux. The recorded native scenarios were executed on Windows; the Mono launch
path has not been exercised in this workspace.

The native tool uses no-shuffle/no-deck-check fixture decks and records actual
opening hands and intermediate moves. Its core RNG seed is unknown. Focused
checks establish specified decisions, native scenarios establish those observed
routes, and Arena results measure only the recorded matchups. Exact validation
and source identities are recorded in `destiny-board-provenance.json`.

Validated the final executor with Debug/Release and C# 6 builds, 63 focused
checks, and 10 real-script native scenarios containing 66 assertions. These
include actual natural/accelerated Board wins, Muckraker revival and Fiend lock,
Darius into Machinex, four interruption cases, and Ruler control plus 1000 burn.
Windows runtime checks loaded the database, all 55 deck copies and registration.

The frozen Arena comparison completed 20 best-of-three matches per opponent
for each version. Scores below are match wins-losses:

| Frozen opponent | Initial v7 | Final v8 |
| --- | --- | --- |
| TheCrimsonKing | 0-20 | 0-20 |
| Swordsoul | 0-20 | 0-20 |
| Odion | 3-17 | 2-18 |
| Metalmorph | 0-20 | 2-18 |

Both 80-match runs completed with zero client errors, nonzero exits, forced
cleanup or flees; all frozen runtime hashes remained unchanged. The final v8
policy retains protection when a late letter is recoverable and preserves a
live Ruler against speculative Phoenix removal; the 40/15 list is unchanged.
These results still show a weak matchup profile for this thematic deck. Unknown
core RNG, unpaired draws and only 20 matches per opponent do not establish a
reliable improvement or tournament strength. Pilot runs are excluded. Exact
inputs, observed first-player counts and separate evidence are in
[Destiny Board provenance](../destiny-board-provenance.json); detailed local logs
remain under `Artifacts/destiny-board-implementation-20261102/`.

## Blue-Eyes 2025

`Deck=BE2025` selects the 40 Main / 15 Extra / 14 Side list. PR #4 moved Blue-Eyes Tyrant Dragon into the starting Extra Deck and one copy of Spirit with Eyes of Blue to the Side Deck. The reviewed configuration registers in the launcher as `AI_LV4`. [Deck](../Decks/AI_BE2025.ydk) · [Executor](../Game/AI/Decks/BE2025Executor.cs)

Spirit tributes itself for cost, so a full row at activation can still produce a legal summon. At resolution it chooses among the offered Blue-Eyes monsters. It can revive a Normal Blue-Eyes from the Graveyard to save a hand copy. For an Effect Monster such as Jet Dragon, summoning from the hand preserves its effects and ability to attack. Tyrant Dragon ranks the offered settable Traps and can prefer interruption when True Light is already on the field. [Spirit script](../../DeckWorkshop/Data/standard/scripts/c42097666.lua) · [Tyrant Dragon script](../../DeckWorkshop/Data/standard/scripts/c11443677.lua)

```powershell
python WindBot/Tools/bot_tool.py check --suite be-2025 --configuration Release --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_BE2025.ydk --output Artifacts/be-2025-check
```

The merged binary passed 6 of 11 focused cases. The repaired Debug and Release executors passed all 11. [BE2025 harness](../Tools/Executors/BE2025/BE2025Checks.cs)

## Shaddoll 2025 executor

`Deck=Shaddoll2025` selects a 40 Main / 15 Extra / 9 Side list. Its game-one Extra Deck contains El Shaddoll Anoyatyllis. El Shaddoll Grysta and the Link Monster Shaddoll Construct are in the Side Deck. Tohushaddoll Grysta is a different card with a different role in the Lev route. [List](../Decks/AI_Shaddoll2025.ydk) · [Executor](../Game/AI/Decks/Shaddoll2025Executor.cs) · [Focused harness](../Tools/Executors/Shaddoll2025/Shaddoll2025Checks.cs)

### Run the checks

```powershell
python WindBot/Tools/bot_tool.py build --configuration Release
python WindBot/Tools/bot_tool.py check --suite runtime --configuration Release --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_Shaddoll2025.ydk --registration Shaddoll2025 --output Artifacts/shaddoll-runtime
python WindBot/Tools/bot_tool.py check --suite shaddoll-2025 --configuration Release --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_Shaddoll2025.ydk --output Artifacts/shaddoll-focused
```

The first check verifies SQLite, the card IDs and the compiled registration. The second exercises the executor through synthetic choices and callbacks. Read its `report.json` for individual decisions, failures and the state reached by the Lev fixture. A shuffled match can end before the bot sees the opening that a fixed-hand fixture tests.

### Routes covered by the review

| Decision | What to look for in a replay |
| --- | --- |
| Lev starter | Which monster Lev sends from the Extra Deck, whether Meshachrer gets its search before being tributed, and whether Construct and Hedgehog leave a legal reveal for Void. |
| Fusion setup | The offered Fusion target, the actual materials selected, and whether the bot keeps a defensive Fusion or Schism for the opponent's turn. |
| Opponent interaction | The timing of Winda, which face-up card Apkallone can still negate after resolution, and whether Squamata and Wendikuruhu retain a usable flip effect. |
| Recovery | Which continuation remains after a search is negated, a material leaves play, or a Fusion target is unavailable. |

Tournament provenance for this exact list is unconfirmed. The deck database entry and strategy profile document related Shaddoll builds, and the Belen result used a different shell. The deck's scripts and the host's actual offers determine legal play. [Konami deck database](https://www.db.yugioh-card.com/yugiohdb/member_deck.action?cgid=417886d4321be901de816ca475c25137&dno=6&request_locale=en) · [TW03 profile](https://ygorganization.com/cdp_shaddolltw03/) · [Belen result](https://ygoprodeck.com/tournament/belen-wcq-regional-5175) · [Lev combo](https://www.masterduelmeta.com/combo/6a6d325cb3d5c9618637712e) · [Shaddoll guide](https://www.masterduelmeta.com/articles/guides/shaddoll-pancakes)

### Checks after the PR #4 review

Using the same deck, Standard database and 85 original checks, the earlier PR revision `8282a5e6` passed 84 and the merged version (`31f3c5ee`) passed 58. The repaired executor and fixtures pass all 85 plus two new negative cases. The changes cover Schism's prompt, Granguignol's requirement for Cartesia plus a LIGHT or DARK monster, legal material setups and chain-end cleanup. The executor retains the newer material, battle and flip-control decisions.

In a two-seat, fixed-order native comparison against the same frozen BE2025, the merged and repaired Shaddoll builds each went 2–0. A separate two-seat compatibility check with repaired BE2025 against frozen Shaddoll went 0–2. All six duels completed. The core RNG seed was unavailable.

With **shuffling enabled**, both Shaddoll versions played 20 individual duels against the same frozen BE2025, ten in each room seat. The engine, scripts, database and both deck files had identical hashes across versions. All 40 duels completed without a retry.

| Shaddoll version | Room seat 0 | Room seat 1 | Total wins–losses |
| --- | ---: | ---: | ---: |
| Merged (`31f3c5ee`) | 1–9 | 5–5 | 6–14 |
| Repaired | 2–8 | 5–5 | 7–13 |

The one-duel difference is too small to establish a strength change. Both versions lost more often than they won against this opponent. These figures count native duels. The Arena reports below count best-of-three matches where indicated.

### Historical match reports

The contributor recorded these runs across several deck lists and executor revisions. Counts labeled “Arena” are match results. Counts from client traces are individual duels. Compare each figure only with its recorded deck, opponents and version. Statements about the “current” or “next” policy below describe the revision under discussion.

<details>
<summary>Read the original run IDs, results and replay observations</summary>

Broad challenge run `77fa0384-9f9f-46b7-9172-9548647d0fe3` completed 183
single-match Arena entries across 61 Standard opponents with no recorded
disconnects. Shaddoll2025 went 66-117 (36.1%); excluding the three mirror
entries it went 64-116 (35.6%). Its trace tree contains 874 captures, 873 of
them complete. After normalizing the older writer's mislabeled card callbacks,
the analyzer recovers 22,939 selection rows instead of reporting zero. The same
evidence found 20 accepted Faimena activations, all on the bot's own turn; 15
ended in losses, and several selected Granguignol without creating immediate
control. That correlation motivated preserving Faimena for a verified
opponent-turn Winda or Apkallone line. It does not prove the policy will improve
the next randomized sample, so this update is committed before any new Arena
run.

Arena run `517f9b9a-61be-43c8-ba7f-30055724bbb7` used the current 40/15/9 list
and completed three Normal-level matches against `Lightsworn`: Shaddoll2025 won
2–1, all three results were observed and the Shaddoll client recorded no
disconnects. This is a functional regression sample, not an estimate of matchup
win rate or competitive strength.

Scheduler-fix run `da97e47f-5c46-447e-b3c8-578b3024fd51` expanded the smoke
matrix to 30 Normal-level games before the temporary Grysta/Anoyatyllis Extra Deck swap.
Shaddoll2025 went 1–4 against Blue-Eyes, 0–5 against Chaos-MAX, 2–3 against Dark
Magician, 2–3 against Mathmech, 5–0 against Salamangreat and 4–1 against Sky
Striker, with no Shaddoll disconnects. The later run
`2e3b4c81-d8d1-4d16-a3ba-7be06f03d567` completed another 18 observed games:
0–3 Blue-Eyes, 0–3 Chaos-MAX, 0–3 Dark Magician, 3–0 Mathmech, 0–3
Salamangreat and 1–2 Sky Striker, again with no Shaddoll disconnects, but used
the mistaken El Shaddoll Grysta game-one swap and is superseded by the restored
list. These
results show that the launch/runtime path is stable and that the combo scheduler
regression is covered, but they also show unresolved matchup weakness and high
small-sample variance; they are not evidence of competitive strength.

Combo-Sheet correction run `1e840c39-f0bb-4f82-8836-20e200ae7c44` used the
restored game-one Extra Deck and the executor that records Wendikuruhu, Ariel,
Squamata, El Shaddoll Fusion and Schism correctly. It completed one Normal-level
game each against Blue-Eyes, Dark Magician and Sky Striker: Shaddoll2025 went
0–3 with zero disconnects. This older run predated structured per-action traces,
so it proves runtime completion only and does not show that Lev was drawn or that
the deterministic combo occurred.

Quick-Fusion chain-guard run `7c0cdc0b-b92d-4eb5-b6ea-d12f3f9ac91b`
completed one Normal-level game against Lightsworn: Shaddoll2025 won 1–0 with
zero disconnects. This confirms the corrected build completed a real Lua-backed
duel; the exact logged Void opening remains covered by the deterministic
chain-lifecycle regression.

Final trace-backed validation run `7875ea0c-30ed-4f08-82f4-40d398a38cae`
completed three Normal-level Arena match entries against Blue-Eyes with no
Shaddoll disconnects. Arena recorded three match wins, representing eight
underlying duels (6–2). The shuffled sample contained one opening Lev route and
that trace selected Apkallone, Squamata, Tohushaddoll Grysta, Meshachrer, Void,
Construct, Hedgehog, Wendi and Schism in order. Across the run, the analyzer also
observed the complete prepared control shell (Wendikuruhu, Ariel, revived
Squamata, Schism and recovered El Shaddoll Fusion) in a real Lua-backed duel.
This small successful sample verifies execution and traceability, not an expected
Blue-Eyes win rate.

Completed run `f07d557f-7b67-4927-98c7-366eb2005067` recorded 486 underlying
duels across 200 Arena entries. Shaddoll2025 went 325–161 by duel and 151–49 by
Arena result. The two weak Arena matchups were Blue-Eyes at 3–17 and Orcust at
8–12. The prepared control shell was observed in 46 duels and went 40–6; with El
Shaddoll Fusion also observed it went 37–4. This supports preserving the core Lev
route while correcting its response policy rather than replacing the route.

Against the bundled legacy Blue-Eyes bot, Shaddoll2025 went 12–34 by individual
duel. Twenty-three of the 34 losses showed Number 38: Hope Harbinger Dragon
Titanic Galaxy and twenty-one showed Blue-Eyes Spirit Dragon. The list also uses
cards outside current tournament legality because Arena deck checks are disabled.
The traces exposed three correctable policies: set Schism fired before the first
opposing Special Summon, Schism kept selecting Winda while a LIGHT spell negate
remained face-up, and Ariel valued generic grave bodies above live Orcust/Dragon
recursion. Timing, legal material counting, adaptive Schism targets and grave
priorities now have focused regressions. This run predates those corrections and
is not a post-fix win-rate result.

Completed post-timing run `3d471151-ffb4-4bad-89e7-446ea571169a` recorded 488
underlying duels across 200 Arena entries. Shaddoll2025 went 313–175 by duel and
140–60 by Arena result. Blue-Eyes improved from the preceding run's 3–17 Arena
result to 12–8 and Orcust finished 10–10, but Dark Magician fell from 15–5 to
5–15; Rank V finished 14–6 and Salamangreat 17–3. The Dark Magician losses
exposed Dark Magical Circle in 30 of 33 duels, Dark Magician in 29, Eternal Soul
in 28 and Magician Navigation in 24. Rank V losses exposed Tiras in 11 of 22
duels and Cyber Dragon Infinity in nine. Those public observations now drive a
capability-based correction instead of a matchup-name switch: visible repeatable
control can open the delayed Schism window; Apkallone is preferred against live
negate, removal, recursion or protection engines; and Dragon/Ariel score field
and grave capabilities before card-specific fallbacks. These changes have
compiled callback regressions. The completed run remains diagnosis, not proof of
improvement.

Capability-policy run `800a8951-99d7-4ec8-8fbf-c078d7f7f88a` is the first
post-change Arena evidence. It recorded 477 completed underlying duels across 200
Arena entries: 292–185 by duel and 130–70 by Arena result, down from 70% to 65%
by Arena result. Blue-Eyes regressed from 12–8 to 2–18, while Dark Magician
improved from 5–15 to 11–9; Rank V was 12–8, Orcust 10–10 and Salamangreat 15–5.
The prepared control shell remained strong when reached (33–9, or 28–7 when El
Shaddoll Fusion was also observed), but it appeared in 42 duels instead of 49,
and opening Lev fell from 175 to 141 observations. That draw mix explains part
of the aggregate movement, not the Blue-Eyes collapse. Source and trace review
found that the first capability parser treated repeatable removal on a face-up
monster as a one-shot effect; Blue-Eyes Alternative White Dragon therefore fell
below the live-engine threshold after the older name-specific score was removed.
It also allowed Schism to consume the floodgate branch before a ready El Shaddoll
Fusion could establish Apkallone. The parser now recognizes once-per-turn
monster interaction as a repeatable permanent capability, and the prepared
control shell gives El Shaddoll Fusion the first answer to such a threat while
retaining Schism for Winda. The focused compiled regression covers that exact
decision. No Arena run has been made after this correction, so the change is not
yet a win-rate claim.

Completed run `6751fceb-69b3-43d3-80fe-b5e11ec335e6` recorded 464 completed
underlying duel traces across 200 single-match Arena entries: 306–158 by duel
trace and 140–60 by Arena result. The Arena matchup results were Blue-Eyes 2–18,
Dark Magician 6–14, Orcust 12–8, Salamangreat 14–6, Lightsworn 15–5, Rank V
16–4, Toadally Awesome 16–4, Dragunity 19–1, MathMech 20–0 and Mokey Mokey King
20–0. The prepared control shell was still positive globally at 39–10 in 49
observations, but it went only 1–7 against Blue-Eyes. Opening Lev went 4–17
against Blue-Eyes, and a first-turn Winda went 0–7 there; against Dark Magician,
first-turn Winda went 2–10.

Trace order exposed the timing defect rather than a missing combo route. El
Shaddoll Fusion was firing on resolving one-shot setup Spells such as Trade-In,
The Melody of Awakening Dragon, Cards of Consonance, Dragon Shrine, Pot of
Desires and Upstart Goblin. Apkallone's summon trigger begins only after that
chain resolves, when those Normal Spells are already in the GY, so the bot spent
El Fusion and its materials without negating the effect and before Blue-Eyes
Alternative White Dragon, Hope Harbinger, Dark Magical Circle or Eternal Soul
became the real target. Opponent-turn Fusion timing now distinguishes transient
chain cards from persistent face-up threats, preserves Schism for Winda, and
retains a no-Schism Winda fallback. The compiled suite covers Trade-In versus
Dark Magical Circle, the prepared Alternative branch and the fallback after an
opposing Special Summon. No Arena run has been made after this timing correction.

Completed run `551ef82e-ec51-4d57-a15d-9c6b3808d932` recorded 476 completed
underlying duel traces across 200 single-match Arena entries: 316–160 by duel
trace and 142–58 by Arena result (71%). The Arena matchup results were Blue-Eyes
6–14, Orcust 10–10, Rank V 12–8, Dark Magician 13–7, Salamangreat 14–6,
Toadally Awesome 15–5, Lightsworn 16–4, MathMech 17–3, Dragunity 19–1 and Mokey
Mokey King 20–0. The prepared control shell went 40–6, or 39–5 when El Shaddoll
Fusion was also observed, and opening Lev went 117–43. This confirms that the
core route remained productive after the one-shot Spell timing fix.

The next run, `81781204-441c-473d-82f4-cf14da400c61`, exposed that the first
summon-denial correction was itself incomplete. It recorded 475 completed
Shaddoll duel traces and fell from 316–160 to 295–180. Blue-Eyes fell from 20–30
to 13–35 by duel trace and Dark Magician from 29–18 to 16–32, while Orcust
improved from 24–26 to 34–17. Schism activations increased from 417 to 457 and
El Shaddoll Fusion activations fell from 71 to 57, confirming that the new rule
was consuming too many response windows rather than improving the core combo.

The trace order supplies the rules-level cause. In a Blue-Eyes loss, Alternative
White Dragon was Special Summoned, Schism then made Winda, and Blue-Eyes White
Dragon was still Special Summoned afterward. Winda only limits summons made
while it is face-up; making it after the first summon does not retroactively
consume the opponent's permitted summon. The corrected policy now establishes
Winda before the first summon whenever Schism has a legal Shaddoll plus DARK
material pair. The deterministic Lev route also leaves revived Squamata
face-down so a live Wendikuruhu keeps its position-change interaction. If the
first summon has already happened, Schism removes the visible material with a
same-Attribute Fusion instead: Construct for a LIGHT
Alternative or Rank V extender, and Winda for a DARK material that Schism can
also send. Dark Magical Circle still routes to El Fusion/Apkallone. The compiled
regressions cover all of these branches and prove that the recorded Lev board's
Wendikuruhu effect is legally activatable.

The later targeted Blue-Eyes run
`7b1fc6be-ab7a-49f7-a85e-6a673554d165` recorded 245 completed underlying duels
across 100 Arena entries and went 85–160. Blue-Eyes Spirit Dragon appeared in
147 duels; Shaddoll won 28 of those (19.0%) versus 57 of 98 without Spirit
(58.2%). Number 38: Hope Harbinger appeared in 149 duels; Shaddoll won 20
(13.4%) versus 65 of 96 without it (67.7%). The Spirit chain-order regression
had no practical opportunity in this sample: no captured Shaddoll chain choice
contained multiple simultaneous own Graveyard triggers while a live Spirit was
already face-up. First-end observations instead showed Schism set in 185 duels,
but the prior policy required the complete Wendikuruhu/Flip shell before making
Winda ahead of the first opposing summon. The executor now uses any legal
Schism-to-Winda material pair for that pre-summon lock and preserves the last
face-down Flip body for Wendikuruhu. This correction has focused compiled
coverage but has not yet been rerun in Arena.

</details>
