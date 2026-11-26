# Contributing a WindBot executor

An executor tells WindBot which legal move to make and how to answer the prompts that follow. If you found a bad move or a crash while playing, you can report it without writing code. [Choose an issue form](https://github.com/ElderLich/SKYNET/issues/new/choose)

## Prepare the deck

1. Create a branch from the latest `main`. If you use a fork, open your pull request against `ElderLich/SKYNET:main`.
2. For a new executor, start from the RDA template. For a fix, edit the existing executor and preserve its `Deck=` registration. Use the card script and a concrete duel position to decide what the bot should do. [Executor template](WindBot/Templates/RdaPlannerExecutor.cs.template) · [Authoring guide](WindBot/Guides/ExecutorAuthoring.md)
3. Keep the registered class and its deck-specific decision code together in `WindBot/Game/AI/Decks/<Name>Executor.cs`. Put the playable deck in `WindBot/Decks/`. For example, `[Deck("MyDeck", "AI_MyDeck")]` pairs `Deck=MyDeck` with `AI_MyDeck.ydk`. Put any deck-specific checks or fixtures in `WindBot/Tools/Executors/MyDeck/` and generated results in `Artifacts/`.
4. When the bot is ready for Solo, add it to the English roster and all ten translated rosters. Match the registration, difficulty or specialty flags, and menu order. Reference an installed dialog by its exact name in each locale. Put Standard entries before `# Rush Support Start` and Rush entries between that marker and `# Rush Support End`. [English roster](WindBot/BotWrapper/bot.conf) · [Translations](WindBot/BotWrapper/locales/) · [Solo publishing guide](WindBot/README.md#sync-the-mdpro4-solo-bots)

## Check your change locally

Run these commands from the repository root. On Linux, use `python3`. The build command compiles Debug and Release. [Build prerequisites](WindBot/README.md#build-and-check-on-windows-or-linux)

```sh
python WindBot/Tools/bot_tool.py build
python WindBot/Tools/test_bot_tool.py
python -m unittest discover -s WindBot/Tools/Sync
```

Then check that the compiled registration and every card in your deck load. This example uses The Crimson King. Substitute your registration and deck file, use `DeckWorkshop/Data/rush/cards.cdb` for Rush, and choose a fresh output directory for each run.

```sh
python WindBot/Tools/bot_tool.py check --configuration Release --suite runtime --registration TheCrimsonKing --deck WindBot/Decks/AI_TheCrimsonKing.ydk --database DeckWorkshop/Data/standard/cards.cdb --output Artifacts/contributor-deck-check
```

For a gameplay change, also exercise the move or prompt you changed. A short account of the starting position, the offered choices and the bot's actual response gives reviewers something they can reproduce. [Deck-specific checks and examples](WindBot/Guides/DeckReference.md)

## Open the pull request and follow its checks

Push your branch and open a PR to `main`. In the PR template, name the executor, explain the change players will see, and list the checks you ran. Link the issue if you are fixing a report. For an imported executor or deck list, credit the author and identify the source revision.

Open the PR's **Checks** tab for live progress. The workflow **Summary** links to the Windows builds and offline tests, Arena tests, legacy Mono builds, and macOS Solo dialog check. If one fails, open its log, fix the first failing step, and push to the same branch to start a fresh run. With GitHub CLI, run `gh pr checks --watch` from your PR branch to follow it in the terminal. [Validation runs](https://github.com/ElderLich/SKYNET/actions/workflows/test-build.yml)
