# Deck Workshop

Deck Workshop lives inside Arena. Import a `.ydk`, inspect its card text and Lua,
follow script and card relationships, and export an RDA-first WindBot starter.
It uses versioned local snapshots; the original game and Deck Compiler folders
are not needed after checkout. Standard and Rush catalogs stay separate.

## Open the workshop

From the workspace root, on Windows or Linux with Node.js 24:

```sh
cd Arena
npm ci
npm run build
npm start
```

Open **http://127.0.0.1:3000/#workshop**. Windows users can also use
`Arena/Skynet Arena.cmd`. No duel server is required for analysis or generation.

1. Drop, select or paste a `.ydk`, choose Standard or Rush, and analyze it.
   **Explore the RDA deck** loads the existing TheCrimsonKing list as an example.
2. Search the library and select a card. **Effect map** links effect declarations
   to callbacks; selecting a node opens its source line. **Lua references** shows
   literal card-ID links. **Related cards** shows categorized static relations.
3. Inspect the printed text, database prompt strings, script hash and numbered
   Lua. Download **Text report** for the Deck Compiler-style combined text/Lua file.
4. For Standard decks, enter a unique executor name and **Generate starter**.
   Preview/download its two C# files, `.ydk`, analysis, instructions and manifest.
   Copy the files to their displayed destinations when ready to integrate them.

Both C# files are required. The per-deck planner base comes from
`WindBot/Templates/RdaPlannerExecutor.cs.template`; the concrete executor contains
card IDs, observation/model/action seams and prompt callbacks. Registration starts
as `Test`, outside the normal random pool. Its model deliberately starts disabled.
The generator never installs source, changes the roster, compiles or launches a bot.

Use [WindBot's portable workflow](../WindBot/README.md#build-and-check-on-windows-or-linux)
for prerequisites and compilation. From the workspace root:

```sh
python WindBot/Tools/bot_tool.py doctor --database DeckWorkshop/Data/standard/cards.cdb
python WindBot/Tools/bot_tool.py build
python WindBot/Tools/bot_tool.py check --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_TheCrimsonKing.ydk --registration TheCrimsonKing --output Artifacts/workshop-runtime-check
```

Use `python3` on Linux. For a generated executor, substitute its actual `.ydk`
and registration name in the last command. Choose a new output directory for
each check. The runtime probe checks SQLite, card IDs and registration; it does
not validate decisions or play a duel. Implement and test the deck using the
[executor authoring guide](../WindBot/Guides/ExecutorAuthoring.md).

## Included data and provenance

| Snapshot | Cards | Lua files, including helpers | Original inputs |
| --- | ---: | ---: | --- |
| Standard | 15,019 | 13,575 | `locales/en-US/cards.cdb`, `script.zip` |
| Rush | 4,224 | 3,920 | `locales/en-US/rush_cards.cdb`, `rush_scripts.zip` |

The inputs came from the explicitly selected
`D:/Programs/GitHub/MDPro4_Data/Data`. **Both CDBs and all extracted scripts are
tracked in Git.** `Data/standard/cards.cdb` and `Data/rush/cards.cdb` are the local
database paths. Edison is excluded. Archive licenses and metadata are retained;
`script/` archive members appear under each snapshot's `scripts/` directory.
The data remains subject to its original attribution and licenses.

`LegacyCompiler/` preserves the original `Pull.py`, `Deck.txt` and `Deck - MF.txt`
from `D:/Modding/Private Workshop/Deck Compiler`. They are reference inputs, not
runtime dependencies; the old machine-specific translation paths, generated
report and duplicate/pre-release scripts are not used. The new report reads the
selected CDB and matching script snapshot directly.

`data-provenance.json` records source archive/database hashes and every imported
file's hash. `.gitattributes` preserves their original bytes and line endings.
Verify a checkout with:

```sh
python DeckWorkshop/import_data.py verify
```

To review a future refresh, import into a new directory; the importer refuses
to overwrite an existing snapshot:

```powershell
python DeckWorkshop/import_data.py import --data-root 'D:/Programs/GitHub/MDPro4_Data/Data' --compiler-root 'D:/Modding/Private Workshop/Deck Compiler' --output Artifacts/workshop-refresh
```

Compare the files and provenance before replacing the tracked snapshots. Restart
Arena after refreshing data and rebuild the related indexes using the command below.
Runtime lookup stays inside these workspace snapshots, never external game paths.

## RelatedCards integration

The supplied `MDPro_3/Tools/RelatedCards` parser is copied into `RelatedCards/`.
Its source revision and hashes are in `RelatedCards/provenance.json`; the parser
is unchanged and the imported tests only adapt their Python import path.
It recognizes named IDs, aliases, static archetype/type/race/level predicates,
and supported Rush Maximum/Fusion/material/filter/alternate helpers.

`Relations/standard.json.gz` and `Relations/rush.json.gz` are versioned indexes
generated from the imported snapshots. They contain 13,852 and 3,943 indexed
cards respectively, with 4,094,243 categorized edges in total. Edges can be
inverse links or overlapping categories, not unique combos. The UI prioritizes
deck members, then named-ID and archetype relationships, and shows at most 24
related cards per selected card. These are research leads, not ranked deck edits.

```sh
python DeckWorkshop/build_relations.py
python -m unittest discover -s DeckWorkshop/RelatedCards/tests -v
```

The wrapper passes explicit snapshots and shared standard constants to the Rush
parser, then produces deterministic compressed indexes. Use this wrapper rather
than the imported builder's game-specific default CLI. No Lua is executed.

## Limits and validation

This is static source analysis, not a Lua interpreter or a duel simulator. Dynamic
helpers, cloned effects, scopes, branches and prompt ordering can be incomplete.
Prompt-string offsets are not printed effect-paragraph numbers. Missing cards,
ambiguous scripts, alias fallback and source limits are surfaced. A `.ydk` is
preserved without claiming banlist or format legality. Standard analysis also
does not automatically establish a deck's strategy or competitive strength.

Inputs are limited to 32 KiB and 200 total card copies. Individual scripts are
limited to 256 KiB and combined Lua to 4 MiB per analysis. Function/effect/reference
inventories are bounded. Rush supports analysis, relationships and reports;
executor generation is disabled because this WindBot runtime has not been
validated for Rush rules.

Validated on Windows on 2026-11-02: byte verification of all 17,503 imported
files; ten RelatedCards fixtures; Arena backend tests and production build;
WindBot Debug/Release; two generated starters compiled together as C# 6;
runtime SQLite/deck/registry checks; RDA 14/14, Voiceless 39/39 and Metalmorph
31/31 offline checks. Browser checks cover the analysis, source graph and export
flows. These are separate from real-duel outcomes: no duels or strength comparison
were performed for this update. Linux commands and Mono launch paths are supplied,
but local WSL could not start, so Linux execution remains unverified.
