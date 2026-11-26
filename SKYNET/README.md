# SKYNET

<img src="Branding/skynet-logo.png" width="160" align="right" alt="SKYNET project logo">

SKYNET is the experimental duel player in this workspace. Give it a deck list and it scores the legal actions offered by the duel server using shared card knowledge. The general policy can reuse a supported effect across decks. For an unfamiliar card it can still make basic plays, but it declines optional effects it does not understand.

[Workspace](../README.md) · [Policy and research notes](Research/PolicyNotes.md) · [Compare players in Arena](../Arena/README.md)

## Build and run

Run these commands from **`SKYNET/`** in a Visual Studio Developer PowerShell with the .NET Framework 4.8 targeting pack. The help command works before you set up a duel host. Python 3.11+ is needed for the development tools.

```powershell
msbuild Skynet.sln /p:Configuration=Release /p:Platform="Any CPU"
.\bin\Release\SKYNET.exe --help
```

Start a compatible duel host and open a room. This example uses the bundled Standard database and the Vanguard research deck. Supply the database, protocol version and room password used by your host.

```powershell
.\bin\Release\SKYNET.exe Agent=skynet "DeckFile=Decks/AI_Skynet.ydk" `
  "DbPath=../DeckWorkshop/Data/standard/cards.cdb" Host=127.0.0.1 Port=7911 `
  PolicyTraining=false Explain=true
```

Each process joins one room. `HostInfo` carries the password, `Version` overrides the protocol version, and `Name` sets the display name. `Explain=true` prints ranked choices and reasons for supported general-policy decisions. Chat and training default to off. [Runtime options](Program.cs) · [Bundled data](../DeckWorkshop/README.md#included-data-and-provenance)

## Choose a player

| Player | Deck to try | What to expect |
| --- | --- | --- |
| `Agent=skynet` | [Vanguard](Decks/AI_Skynet.ydk), [Swordsoul](Decks/AI_SkynetSwordsoul.ydk), [Chimera](Decks/AI_SkynetChimera.ydk) | One shared policy with reviewed staples, selected Swordsoul Synchro routes and Chimera Fusion routes. It reassesses available resources after interruptions. |
| `Agent=expert-oldschool` | [OldSchool](Decks/AI_ExpertOldSchool.ydk) | A small, frozen deck executor for integration comparisons. |
| `Agent=expert-swordsoul` | [Swordsoul–Tenyi](Decks/AI_ExpertSwordsoul.ydk) | A reviewed full-deck executor with its own action, selection and lifecycle logic. |

Set `DeckFile` to the matching list when switching players. Each expert owns the entire duel and rejects training and model overrides. To write a normal deck-specific executor, use WindBot. [Effect coverage and known gaps](Research/PolicyNotes.md#bootstrap-card-coverage) · [Expert review receipts](Catalog/expert-baselines.json) · [WindBot authoring](../WindBot/Guides/ExecutorAuthoring.md)

## Can it teach itself?

The learner adjusts **nine weights** that influence the general policy's choices. Card rules, material selection and prompt handling come from reviewed code. A new weight file can change the ranking of supported actions. Adding support for a new effect requires implementing and checking its decisions.

1. **Collect completed duels.** Start the bot once per room against an explicitly configured opponent. Give each stable deck-and-opponent group its own `PolicyGroup`. Use the same source model across groups and collect enough distinct groups for the training and holdout partitions.

   ```powershell
   .\bin\Release\SKYNET.exe "DbPath=../DeckWorkshop/Data/standard/cards.cdb" `
     "DeckFile=Decks/AI_Skynet.ydk" Host=127.0.0.1 Port=7911 `
     PolicyTraining=true PolicyData=Training PolicySeed=42 PolicyGroup=vanguard-vs-dragon
   ```

2. **Train a candidate offline.** Run the trainer on the collected episodes. It checks the policy revision and source model and excludes held-out groups from the update.

   ```powershell
   python Tools/Skynet/train.py --episodes Training --output Models/local/candidate-001.json
   ```

3. **Play fresh, frozen matches.** Use the original deck and opponents with `PolicyTraining=false`. Run the candidate with `PolicyModel=Models/local/candidate-001.json` and compare with the original model across both seats. Policy sampling and the duel core have separate random-number generators. Leave the CSV's `seed` empty and supply a unique `matchId` when the core seed is unknown.

   ```powershell
   python Tools/Skynet/evaluate.py Artifacts/evaluation/results.csv
   ```

The evaluator reads a CSV with `model,opponent,seed,seat,result,matchId`, where seats are `0` and `1` and results are `win`, `loss` or `draw`. It summarizes each opponent and model. Promoting a candidate is a separate decision. Interrupted duels supply no terminal training label. [Trainer](Tools/Skynet/train.py) · [Evaluator](Tools/Skynet/evaluate.py) · [Recorded comparisons](Research/PolicyNotes.md#recorded-arena-comparisons)

## Install into MDPro4

On Windows, build Release and select the game directory explicitly. The installer copies SKYNET's executable, libraries, decks, dialogs, logo and license into `Data/Skynet/` and writes a hash manifest there.

```powershell
.\Tools\Skynet\Install-MDPro4.ps1 -GameRoot 'D:/YourGame' -Configuration Release
```

The game build must already include the SKYNET Solo host adapter and its call sites. In a compatible build, select **ocgcore → SKYNET** in Solo, then choose the opponent's deck. The adapter uses the game's locale database and starts the bot with training disabled. [Installer](Tools/Skynet/Install-MDPro4.ps1) · [Adapter source](Tools/Skynet/SoloSelector.Skynet.cs)

## Verify a change

Run the Debug build as well as Release, then check the offline tools.

```powershell
msbuild Skynet.sln /p:Configuration=Debug /p:Platform="Any CPU"
python Tools/Skynet/test_learning.py
python Tools/Skynet/test_ygo_agent_inspection.py
python Tools/Skynet/test_ygo_agent_shadow.py
python Tools/Skynet/test_demonstrations.py
```

With a compatible Windows server DLL and matching data, run a fixed-order native smoke test into a fresh output directory. Its results establish that the chosen clients can complete those duels. Use fresh shuffled matches to compare playing strength.

```powershell
python Tools/Skynet/native_smoke.py `
  --server 'D:/YourGame/Assets/Plugins/Windows/ygoserver.dll' `
  --data-root 'D:/YourGame/Data' --bot-exe bin/Release/SKYNET.exe `
  --deck Decks/AI_Skynet.ydk --opponent-deck Decks/AI_Skynet.ydk `
  --games 2 --output-dir Artifacts/qa/my-run
```

## Source and research

| Area | Where to look |
| --- | --- |
| Legal offers, policy scores and prompt ownership | [Decision adapter](Game/SkynetDecisionAdapter.cs) · [Strategy](Game/AI/Strategy/) · [Experts](Game/AI/Experts/) |
| Demonstrations and observation encoding | [Shadow capture](Game/AI/Shadow/) · [Learning tools](Tools/Skynet/) · [Pinned YGO Agent research](Research/ygo-agent/README.md) |
| Measured results, unsupported effects and future work | [Policy notes and experiment records](Research/PolicyNotes.md) · [Executor source catalog](Catalog/executor-sources.json) |
| Contribution rules and attribution | [Contributor guide](AGENTS.md) · [WindBot/YGOSharp license](LICENSE) · [YGO Agent upstream receipt](Research/ygo-agent/UPSTREAM.json) · [Arena upstream receipt](../Arena/UPSTREAM.json) |

SKYNET retains the WindBot/YGOSharp transport attribution. Third-party imports retain their own notices. The owner supplied the SKYNET branding. The bundled card databases and Lua snapshots have separate provenance. [Data origins](../DeckWorkshop/README.md#included-data-and-provenance)
