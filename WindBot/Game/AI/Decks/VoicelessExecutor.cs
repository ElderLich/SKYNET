using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;


namespace WindBot.Game.AI.Decks
{
    [Deck("Voiceless", "AI_Voiceless")]
    public class VoicelessExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int Saffira = 51296484;
            public const int Lo = 25801745;
            public const int VVSauravis = 88284599;
            public const int VVSkullGuard = 10774240;
            public const int VVPrayer = 52472775;
            public const int Token = 52340445;
            public const int VVBarrier = 98477480;
            public const int VVRadiance = 86310763;
            public const int Blessing = 39114494;
            public const int Linkuriboh = 41999284;
            public const int SecureGardna = 2220237;

            public const int AshBlossom = 14558127;
            public const int GhostMournerMoonlitChill = 52038441;
            public const int EffectVeiler = 97268402;
            public const int InfiniteImperm = 10045474;
            public const int Droll = 94145021;
            public const int PrePrep = 13048472;
            public const int Diviner = 92919429;
            public const int Trias = 26866984;
            public const int CalledBy = 24224830;
            public const int Sauravis = 4810828;
            public const int SolemnStrike = 40605147;

            public const int DynaMondo = 73898890;
            public const int SPLittleKnight = 29301450;
            public const int UnderworldGoddess = 98127546;
            public const int Ntss = 80532587;
            public const int ChaosAngel = 22850702;
            public const int Herald = 79606837;

            // Problem cards 
            public const int MaxxC = 23434538;
            public const int NaturalExterio = 99916754;
            public const int NaturalBeast = 33198837;
            public const int ImperialOrder = 61740673;
            public const int SwordsmanLV7 = 37267041;
            public const int RoyalDecree = 51452091;
            public const int Number41BagooskatheTerriblyTiredTapir = 90590303;
            public const int InspectorBoarder = 15397015;
            public const int DimensionShifter = 91800273;
        }

        
        

        public VoicelessExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // A grave trigger can shield an on-field search from Ash (for
            // example Mo Ye above Chixiao). Target the live lower chain link
            // with an actually offered Veiler/Impermanence first.
            AddExecutor(ExecutorType.Activate, CardId.EffectVeiler, VoicelessNegateShieldedMonster);
            AddExecutor(ExecutorType.Activate, CardId.InfiniteImperm, VoicelessNegateShieldedMonster);
            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, VoicelessAshBlossom);
            AddExecutor(ExecutorType.Activate, CardId.EffectVeiler, DefaultEffectVeiler);
            AddExecutor(ExecutorType.Activate, CardId.InfiniteImperm, DefaultInfiniteImpermanence);
            AddExecutor(ExecutorType.Activate, CardId.SolemnStrike, DefaultSolemnStrike);
            AddExecutor(ExecutorType.Activate, CardId.Droll);
            AddExecutor(ExecutorType.Activate, CardId.CalledBy, DefaultCalledByTheGrave);
            AddExecutor(ExecutorType.Activate, CardId.Sauravis, SauravisEffect);
            AddExecutor(ExecutorType.Activate, CardId.VVSkullGuard, SkullCounter);
            AddExecutor(ExecutorType.Activate, CardId.VVSauravis, DragonSageEffect);
            AddExecutor(ExecutorType.Activate, CardId.DynaMondo, DynaEffect);
            AddExecutor(ExecutorType.Activate, CardId.Linkuriboh, LinkuribohEffect);
            AddExecutor(ExecutorType.Activate, CardId.SPLittleKnight, SPLittleKnightActivate);
            AddExecutor(ExecutorType.Activate, CardId.ChaosAngel, ChaosAngelActivate);
            AddExecutor(ExecutorType.Activate, CardId.Ntss, NtssActivate);

            // Select Skull Guardian's search before Lo's revival when both are
            // offered, preserving the intended CL1 search / CL2 revival ordering.
            AddExecutor(ExecutorType.Activate, CardId.VVSkullGuard, SkullSearch);
            AddExecutor(ExecutorType.Activate, CardId.Lo, LoEffect);
            AddExecutor(ExecutorType.Activate, CardId.Diviner, DivinerEffect);
            AddExecutor(ExecutorType.Activate, CardId.Herald, HeraldSearch);
            AddExecutor(ExecutorType.Activate, CardId.Blessing, BlessingEffect);
            AddExecutor(ExecutorType.Activate, CardId.PrePrep, PrePrepSearch);
            AddExecutor(ExecutorType.Activate, CardId.VVBarrier, BarrierFirst);
            AddExecutor(ExecutorType.Activate, CardId.Saffira, SaffEffect);
            AddExecutor(ExecutorType.Activate, CardId.Saffira, SaffiraRitual);
            AddExecutor(ExecutorType.Activate, CardId.VVPrayer, PrayerEffect);
            AddExecutor(ExecutorType.Activate, CardId.Trias, TriasEffect);
            AddExecutor(ExecutorType.Activate, CardId.VVRadiance, RadianceEffect);
            AddExecutor(ExecutorType.Summon, CardId.Diviner, DivinerSummon);
            AddExecutor(ExecutorType.Summon, CardId.Lo, LoSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Linkuriboh, LinkuribohSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.SecureGardna, SecureGardnaSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.VVSauravis, DragonSageSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.DynaMondo, DynaLinkSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ChaosAngel, ChaosAngelSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.SPLittleKnight, SPLittleKnightSpSummon);
            AddExecutor(ExecutorType.Activate, CardId.UnderworldGoddess);
            AddExecutor(ExecutorType.SpellSet, VoicelessSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }


        private List<int> notToNegateIdList = new List<int>{
                58699500, 20343502
            };
        private List<int> notToBeTrapTargetList = new List<int>{
                72144675, 86188410, 41589166, 11443677, 72566043, 1688285, 59071624, 6511113, 48183890, 952523, 22423493, 73639099
            };
        private List<int> targetNegateIdList = new List<int> {
                _CardId.EffectVeiler, _CardId.InfiniteImpermanence, CardId.GhostMournerMoonlitChill, _CardId.BreakthroughSkill, 74003290, 67037924,
                9753964, 66192538, 23204029, 73445448, 35103106, 30286474, 45002991, 5795980, 38511382, 53742162, 30430448
            };
        private List<int> notToDestroySpellTrap = new List<int> { 50005218, 6767771 };

        private List<int> activatedCardIdList = new List<int>();
        private List<ClientCard> currentNegateMonsterList = new List<ClientCard>();
        private List<ClientCard> currentDestroyCardList = new List<ClientCard>();
        private List<ClientCard> setTrapThisTurn = new List<ClientCard>();
        private List<ClientCard> summonThisTurn = new List<ClientCard>();
        private List<ClientCard> enemySetThisTurn = new List<ClientCard>();
        private List<ClientCard> escapeTargetList = new List<ClientCard>();
        private List<ClientCard> summonInChainList = new List<ClientCard>();
        private List<int> chainSummoningIdList = new List<int>(3);
        private Dictionary<int, int> CalledbytheGraveCount = new Dictionary<int, int>();
        private List<int> infiniteImpermanenceList = new List<int>();
        private int CrossoutDesignatorTarget = 0;

        public bool NtssActivate()
        {
            List<ClientCard> problemCardList = GetProblematicEnemyCardList(true, selfType: CardType.Monster);
            problemCardList.AddRange(GetNormalEnemyTargetList(true, true, CardType.Monster));
            if (GetProblematicEnemyCardList(true, selfType: CardType.Monster).Count() == 0) //If there's no cards on opponent's field
                return false;
            else
            {
                AI.SelectCard(problemCardList);
                activatedCardIdList.Add(Card.Id);
                return true;
            }
        }
        public bool ChaosAngelActivate()
        {
            List<ClientCard> targetList = GetNormalEnemyTargetList(true, true, CardType.Monster);
            if (targetList.Count() > 0)
            {
                AI.SelectCard(targetList);
                currentDestroyCardList.Add(targetList[0]);
                return true;
            }
            return false;
        }
        public List<ClientCard> GetNormalEnemyTargetList(bool canBeTarget = true, bool ignoreCurrentDestroy = false, CardType selfType = 0)
        {
            List<ClientCard> targetList = GetProblematicEnemyCardList(canBeTarget, selfType: selfType);
            List<ClientCard> enemyMonster = Enemy.GetMonsters().Where(card => card.IsFaceup() && !targetList.Contains(card)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card))).ToList();
            enemyMonster.Sort(CardContainer.CompareCardAttack);
            enemyMonster.Reverse();
            targetList.AddRange(enemyMonster);
            targetList.AddRange(Util.ShuffleList(Enemy.GetSpells().Where(card =>
                (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card)) && enemySetThisTurn.Contains(card)).ToList()));
            targetList.AddRange(Util.ShuffleList(Enemy.GetSpells().Where(card =>
                (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card)) && !enemySetThisTurn.Contains(card)).ToList()));
            targetList.AddRange(Util.ShuffleList(Enemy.GetMonsters().Where(card => card.IsFacedown() && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card))).ToList()));
            return targetList;
        }

        public ClientCard GetProblematicEnemyMonster(int attack = 0, bool canBeTarget = false, bool ignoreCurrentDestroy = false, CardType selfType = 0)
        {
            List<ClientCard> dangerList = Enemy.MonsterZone.Where(c => c?.Data != null &&
                c.IsMonsterDangerous() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).OrderByDescending(card => card.Attack).ToList();
            if (dangerList.Count() > 0) return dangerList[0];

            List<ClientCard> invincibleList = Enemy.MonsterZone.Where(c => c?.Data != null &&
                c.IsMonsterInvincible() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).OrderByDescending(card => card.Attack).ToList();
            if (invincibleList.Count() > 0) return invincibleList[0];

            List<ClientCard> equippedList = Enemy.MonsterZone.Where(c => c?.Data != null &&
                c.EquipCards.Count() > 0 && CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).OrderByDescending(card => card.Attack).ToList();
            if (equippedList.Count() > 0) return equippedList[0];

            List<ClientCard> enemyMonsters = Enemy.GetMonsters().OrderByDescending(card => card.Attack).ToList();
            if (enemyMonsters.Count() > 0)
            {
                foreach (ClientCard target in enemyMonsters)
                {
                    if ((target.HasType(CardType.Fusion | CardType.Ritual | CardType.Synchro | CardType.Xyz)
                            || (target.HasType(CardType.Link) && target.LinkCount >= 2))
                        && CheckCanBeTargeted(target, canBeTarget, selfType) && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(target))
                    ) return target;
                }
            }

            if (attack >= 0)
            {
                if (attack == 0)
                    attack = Util.GetBestAttack(Bot);
                List<ClientCard> betterList = Enemy.MonsterZone.GetMonsters()
                    .Where(card => card.GetDefensePower() >= attack && card.GetDefensePower() > 0 && card.IsAttack() && CheckCanBeTargeted(card, canBeTarget, selfType)
                    && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card))).OrderByDescending(card => card.Attack).ToList();
                if (betterList.Count() > 0) return betterList[0];
            }
            return null;
        }

        public List<ClientCard> GetProblematicEnemyCardList(bool canBeTarget = false, bool ignoreSpells = false, CardType selfType = 0)
        {
            List<ClientCard> resultList = new List<ClientCard>();
            
            List<ClientCard> problemEnemySpellList = Enemy.SpellZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentDestroyCardList.Contains(c)
                && c.IsFloodgate() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)).ToList();
            if (problemEnemySpellList.Count() > 0) resultList.AddRange(Util.ShuffleList(problemEnemySpellList));

            List<ClientCard> dangerList = Enemy.MonsterZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentDestroyCardList.Contains(c)
                && c.IsMonsterDangerous() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)).OrderByDescending(card => card.Attack).ToList();
            if (dangerList.Count() > 0
                && (Duel.Player == 0 || (Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2))) resultList.AddRange(dangerList);

            List<ClientCard> invincibleList = Enemy.MonsterZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentDestroyCardList.Contains(c)
                && c.IsMonsterInvincible() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)).OrderByDescending(card => card.Attack).ToList();
            if (invincibleList.Count() > 0) resultList.AddRange(invincibleList);

            List<ClientCard> enemyMonsters = Enemy.GetMonsters().Where(c => !currentDestroyCardList.Contains(c)).OrderByDescending(card => card.Attack).ToList();
            if (enemyMonsters.Count() > 0)
            {
                foreach(ClientCard target in enemyMonsters)
                {
                    if ( (target.HasType(CardType.Fusion | CardType.Ritual | CardType.Synchro | CardType.Xyz)
                            || (target.HasType(CardType.Link) && target.LinkCount >= 2))
                        && !resultList.Contains(target) && CheckCanBeTargeted(target, canBeTarget, selfType)
                        )
                    {
                        resultList.Add(target);
                    }
                }
            }

            List<ClientCard> spells = Enemy.GetSpells().Where(c => c.IsFaceup() && !currentDestroyCardList.Contains(c)
                && c.HasType(CardType.Equip | CardType.Pendulum | CardType.Field | CardType.Continuous) && CheckCanBeTargeted(c, canBeTarget, selfType)
                && !notToDestroySpellTrap.Contains(c.Id)).ToList();
            if (spells.Count() > 0 && !ignoreSpells) resultList.AddRange(Util.ShuffleList(spells));

            return resultList;
        }

        public bool CheckCanBeTargeted(ClientCard card, bool canBeTarget, CardType selfType)
        {
            if (card == null) return true;
            if (canBeTarget)
            {
                if (card.IsShouldNotBeTarget()) return false;
                if (((int)selfType & (int)CardType.Monster) > 0 && card.IsShouldNotBeMonsterTarget()) return false;
                if (((int)selfType & (int)CardType.Spell) > 0 && card.IsShouldNotBeSpellTrapTarget()) return false;
                if (((int)selfType & (int)CardType.Trap) > 0
                    && card.IsShouldNotBeSpellTrapTarget()) return false;
            }
            return true;
        }
        public bool CheckCanDirectAttack()
        {
            return Enemy.GetMonsterCount() == 0 && !activatedCardIdList.Contains(CardId.SPLittleKnight) && Duel.Turn > 1 && Duel.Player == 0 && Duel.Phase < DuelPhase.Main2;
        }

        public bool SPLittleKnightSpSummon()
        {
            if (CheckCanDirectAttack())
            {
                // for attack
                List<ClientCard> materialList = SPLittleKnightSelectMaterial();
                if (materialList.Count() >= 2 && GetMaterialAttack(materialList) < 1600)
                {
                    AI.SelectMaterials(materialList);
                    return true;
                }
            } else if (!NegatedCheck(true) && GetProblematicEnemyCardList(true, selfType: CardType.Monster).Count() > 0)
            {
                // for remove 
                List<ClientCard> materialList = SPLittleKnightSelectMaterial(true);
                if (materialList.Count() >= 2 && materialList.Any(card => card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link)))
                {
                    AI.SelectMaterials(materialList);
                    return true;
                }
            }
            return false;
        }
        public int GetMaterialAttack(List<ClientCard> materials)
        {
            if (Util.IsTurn1OrMain2()) return 0;
            int result = 0;
            foreach (ClientCard material in materials)
            {
                if (material.IsAttack() || !summonThisTurn.Contains(material)) result += material.Attack;
            }
            return result;
        }
        public List<ClientCard> GetCanBeUsedForLinkMaterial(bool useAdvancedMonster = false, Func<ClientCard, bool> exceptRule = null)
        {
            List<ClientCard> materialList = Bot.GetMonsters().Where(card => {
                if (card.IsFacedown() || (exceptRule != null && exceptRule(card))) return false;
                if (card.IsCode(CardId.ChaosAngel)
                    && !useAdvancedMonster && (card.IsAttack() || !summonThisTurn.Contains(card))) return false;
                return true;
            }).ToList();
            materialList.Sort(CompareUsableAttack);
            return materialList;
        }
        public List<ClientCard> SPLittleKnightSelectMaterial(bool needToUseEffect = false)
        {
            // Keeping Lo beside an active Skull preserves the omni-negate and
            // Barrier's protection. An optional removal Link must use spare
            // bodies, rather than turn that established board off.
            int liveSkulls = Bot.GetMonsters().Count(card => card.IsCode(CardId.VVSkullGuard)
                && card.IsFaceup() && !card.IsDisabled());
            int faceupLos = Bot.GetMonsters().Count(card => card.IsCode(CardId.Lo) && card.IsFaceup());
            List<ClientCard> candidates = GetCanBeUsedForLinkMaterial(true)
                .Where(card => card.HasType(CardType.Effect)
                    && (!card.HasType(CardType.Link) || card.LinkCount <= 2)
                    && !(liveSkulls > 0 && faceupLos > 0
                        && (card.IsCode(CardId.Lo) && faceupLos == 1
                            || card.IsCode(CardId.VVSkullGuard) && !card.IsDisabled() && liveSkulls == 1))).ToList();
            return Util.GetLinkMaterials(candidates, 2, 2, 2).FirstOrDefault(materials =>
                (!needToUseEffect || materials.Any(card => card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link)))
                && (liveSkulls == 0 || faceupLos == 0
                    || materials.Count(card => card.IsCode(CardId.Lo)) < faceupLos
                        && materials.Count(card => card.IsCode(CardId.VVSkullGuard) && !card.IsDisabled()) < liveSkulls)
                && Util.GetBotAvailZonesFromExtraDeck(materials) > 0) ?? new List<ClientCard>();
        }
        public List<ClientCard> GetDangerousCardinEnemyGrave(bool onlyMonster = false)
        {
            List<ClientCard> result = Enemy.Graveyard.GetMatchingCards(card => 
                (!onlyMonster || card.IsMonster())).ToList(); //some specific matchup stuff removed
            List<int> dangerMonsterIdList = new List<int>{
                99937011, 63542003, 9411399, 28954097, 30680659
            };
            result.AddRange(Enemy.Graveyard.GetMatchingCards(card => dangerMonsterIdList.Contains(card.Id)));
            return result;
        }

        public ClientCard GetBestEnemyMonster(bool onlyFaceup = false, bool canBeTarget = false, bool ignoreCurrentDestroy = false, CardType selfType = 0)
        {
            ClientCard card = GetProblematicEnemyMonster(0, canBeTarget, ignoreCurrentDestroy, selfType);
            if (card != null)
                return card;

            card = Enemy.MonsterZone.Where(c => c?.Data != null && c.HasType(CardType.Monster) && c.IsFaceup()
                && CheckCanBeTargeted(c, canBeTarget, selfType) && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c)))
                .OrderByDescending(c => c.Attack).FirstOrDefault();
            if (card != null)
                return card;

            List<ClientCard> monsters = Enemy.GetMonsters().Where(c => CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).ToList();

            if (monsters.Count() > 0 && !onlyFaceup)
                return Util.ShuffleList(monsters)[0];

            return null;
        }
        public bool SPLittleKnightActivate()
        {
            if (ActivateDescription == -1 || ActivateDescription == Util.GetStringId(CardId.SPLittleKnight, 0))
            {
                // This effect forbids every friendly direct attack for the rest
                // of the turn, even when it only banishes a spent grave card.
                // Preserve the attack gained by making S:P on an open field.
                if (CheckCanDirectAttack() && Bot.GetMonsters().Any(card => card.IsAttack()
                    && !card.Attacked && card.Attack > 0)) return false;
                // banish card
                List<ClientCard> problemCardList = GetProblematicEnemyCardList(true, selfType: CardType.Monster);
                problemCardList.AddRange(GetDangerousCardinEnemyGrave(false));
                problemCardList.AddRange(GetNormalEnemyTargetList(true, true, CardType.Monster));
                problemCardList.AddRange(Enemy.Graveyard.Where(card => card.HasType(CardType.Monster)).OrderByDescending(card => card.Attack));
                problemCardList.AddRange(Enemy.Graveyard.Where(card => !card.HasType(CardType.Monster)));
                if (problemCardList.Count() > 0)
                {
                    AI.SelectCard(problemCardList);
                    activatedCardIdList.Add(Card.Id);
                    return true;
                }
            } else if (ActivateDescription == Util.GetStringId(CardId.SPLittleKnight, 1))
            {
                ClientCard selfMonster = null;
                foreach (ClientCard target in Bot.GetMonsters())
                {
                    if (Duel.ChainTargets.Contains(target) && !escapeTargetList.Contains(target))
                    {
                        selfMonster = target;
                        break;
                    }
                }
                if (selfMonster == null)
                {
                    if (Duel.Player == 1)
                    {
                        selfMonster = Bot.GetMonsters().Where(card => card.IsAttack()).OrderBy(card => card.Attack).FirstOrDefault();
                        if (selfMonster != null && !Util.IsOneEnemyBetterThanValue(selfMonster.Attack, true)) selfMonster = null;
                    }
                }
                if (selfMonster != null)
                {
                    ClientCard nextMonster = null;
                    List<ClientCard> selfTargetList = Bot.GetMonsters().Where(card => card != selfMonster).ToList();
                    if (Enemy.GetMonsterCount() == 0 && selfTargetList.Count() > 0)
                    {
                        selfTargetList.Sort(CompareUsableAttack);
                        nextMonster = selfTargetList[0];
                        escapeTargetList.Add(nextMonster);
                    }
                    if (Enemy.GetMonsterCount() > 0)
                    {
                        nextMonster = GetBestEnemyMonster(false, true, true);
                        currentDestroyCardList.Add(nextMonster);
                    }
                    if (nextMonster != null)
                    {
                        AI.SelectCard(selfMonster);
                        AI.SelectNextCard(nextMonster);
                        escapeTargetList.Add(selfMonster);
                        activatedCardIdList.Add(Card.Id + 1);
                        return true;
                    }
                }
            }

            return false;
        }

        public void SelectSTPlace(ClientCard card = null, bool avoid_Impermanence = false, List<int> avoid_list = null)
        {
            List<int> list = new List<int> { 0, 1, 2, 3, 4 };
            Util.ShuffleListInPlace(list);
            foreach (int seq in list)
            {
                int zone = (int)System.Math.Pow(2, seq);
                if (Bot.SpellZone[seq] == null)
                {
                    if (card != null && card.Location == CardLocation.Hand && avoid_Impermanence && infiniteImpermanenceList.Contains(seq)) continue;
                    if (avoid_list != null && avoid_list.Contains(seq)) continue;
                    AI.SelectPlace(zone);
                    return;
                };
            }
            AI.SelectPlace(0);
        }

        public bool NegatedCheck(bool disablecheck = true)
        {
            if (Card.IsSpell() || Card.IsTrap())
            {
                if (SpellNegatable()) return true;
            }
            if (CheckCalledbytheGrave(Card.Id) > 0 || Card.Id == CrossoutDesignatorTarget)
            {
                return true;
            }
            if (Card.IsMonster() && Card.Location == CardLocation.MonsterZone && Card.IsDefense())
            {
                if (DefaultCheckWhetherNumber41IsActive())
                {
                    return true;
                }
            }
            if (disablecheck)
            {
                return Card.IsDisabled();
            }
            return false;
        }

        public int CheckCalledbytheGrave(int id)
        {
            if (!CalledbytheGraveCount.ContainsKey(id))
            {
                return 0;
            }
            return CalledbytheGraveCount[id];
        }
        // activate of CalledbytheGrave
        public bool CalledByEffect()
        {
            if (NegatedCheck(true)) return false;
            if (Duel.LastChainPlayer == 1)
            {
                // negate
                if (Util.GetLastChainCard().IsMonster())
                {
                    int code = Util.GetLastChainCard().Id;
                    if (code == 0) return false;
                    if (CheckCalledbytheGrave(code) > 0 || CrossoutDesignatorTarget == code) return false;
                    if (Enemy.Graveyard.GetFirstMatchingCard(card => card.IsMonster() && card.IsOriginalCode(code)) != null)
                    {
                        if (!(Card.Location == CardLocation.SpellZone))
                        {
                            SelectSTPlace(null, true);
                        }
                        AI.SelectCard(code);
                        CalledbytheGraveCount[code] = 2;
                        return true;
                    }
                }

                // banish target
                foreach (ClientCard cards in Enemy.Graveyard)
                {
                    if (Duel.ChainTargets.Contains(cards))
                    {
                        int code = cards.Id;
                        AI.SelectCard(cards);
                        CalledbytheGraveCount[code] = 2;
                        return true;
                    }
                }

                // become targets
                if (Duel.ChainTargets.Contains(Card))
                {
                    List<ClientCard> enemy_monsters = Enemy.Graveyard.GetMatchingCards(card => card.IsMonster()).ToList();
                    if (enemy_monsters.Count > 0)
                    {
                        enemy_monsters.Sort(CardContainer.CompareCardAttack);
                        enemy_monsters.Reverse();
                        int code = enemy_monsters[0].Id;
                        AI.SelectCard(code);
                        CalledbytheGraveCount[code] = 2;
                        return true;
                    }
                }
            }

            // avoid danger monster in grave
            if (Duel.LastChainPlayer == 1) return false;
            List<ClientCard> targets = CheckDangerousCardinEnemyGrave(true);
            if (targets.Count() > 0)
            {
                int code = targets[0].Id;
                if (!(Card.Location == CardLocation.SpellZone))
                {
                    SelectSTPlace(null, true);
                }
                AI.SelectCard(code);
                CalledbytheGraveCount[code] = 2;
                return true;
            }

            return false;
        }

        public virtual List<ClientCard> CheckDangerousCardinEnemyGrave(bool onlyMonster = false)
        {
            return new List<ClientCard>();
        }

        // Accepted uses are not predictions of successful resolution. Card locations,
        // resources and prompt candidates always come from the observed duel state.
        private readonly HashSet<long> _voicelessUsed = new HashSet<long>();
        private bool _normalSummonUsed;
        private int _pendingVoicelessSummon;
        private int _acceptedActivationId;
        private int _radianceSelectedId;
        private readonly HashSet<ClientCard> _selectedLinkMaterials = new HashSet<ClientCard>();

        private bool VoicelessNormalUsed { get { return _normalSummonUsed; } }

        private bool VoicelessEffectUsed(int id, int effect)
        {
            return _voicelessUsed.Contains(((long)id << 4) | (uint)effect);
        }

        private bool AcceptVoicelessEffect(int effect)
        {
            _voicelessUsed.Add(((long)Card.Id << 4) | (uint)effect);
            _acceptedActivationId = Card.Id;
            return true;
        }

        private int VoicelessPromptSource()
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.ActivatePlayer == 0 ? solving.ActivateId : 0;
            // Costs may precede MSG_CHAINING. This is a captured accepted action,
            // retired when the server confirms it, never a rejected candidate.
            if (_acceptedActivationId != 0) return _acceptedActivationId;
            ClientCard chain = Duel.GetCurrentChainCard();
            if (chain != null) return chain.Controller == 0 ? chain.Id : 0;
            return _pendingVoicelessSummon;
        }

        private bool HasFaceupLo()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsCode(CardId.Lo) && c.IsFaceup());
        }

        private bool HasFaceupSkull()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsCode(CardId.VVSkullGuard) && c.IsFaceup());
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        private bool LoSummon()
        {
            if (HasFaceupLo() || _normalSummonUsed) return false;
            _normalSummonUsed = true;
            return true;
        }

        private bool DivinerSummon()
        {
            if (_normalSummonUsed || HasFaceupLo()) return false;
            _normalSummonUsed = true;
            return true;
        }

        private bool LoEffect()
        {
            if (Card.Location == CardLocation.Grave) return AcceptVoicelessEffect(1);
            if (Card.Location != CardLocation.MonsterZone || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool BarrierFirst()
        {
            if (Card.Location == CardLocation.Hand || Card.IsFacedown())
                return !Bot.SpellZone.Any(c => c != null && c != Card && c.IsCode(CardId.VVBarrier) && c.IsFaceup());
            if (Card.Location != CardLocation.SpellZone || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool SaffEffect()
        {
            // The matching Lua identifies the ritual effect by LOCATION_GRAVE;
            // the helper gives that effect no per-card StringId description.
            return Card.Location == CardLocation.Hand && AcceptVoicelessEffect(0);
        }

        private bool SaffiraRitual()
        {
            if (Card.Location != CardLocation.Grave) return false;
            return AcceptVoicelessEffect(1);
        }

        private bool SkullSearch()
        {
            bool summonTrigger = AI.SelectingEffectYn && ActivateDescription == -1
                && Duel.CurrentChain.Count == 0 && Duel.LastSummonedCards.Contains(Card);
            if (ActivateDescription != Util.GetStringId(CardId.VVSkullGuard, 0) && !summonTrigger) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool SkullCounter()
        {
            if (ActivateDescription != Util.GetStringId(CardId.VVSkullGuard, 1)
                || Duel.LastChainPlayer != 1 || !HasFaceupLo() || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(1);
        }

        private bool VoicelessNegateShieldedMonster()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) || Duel.LastChainPlayer != 1
                || Duel.CurrentChainInfo.Count < 2 || Duel.CurrentChainInfo.Any(chain => chain.ActivatePlayer == 0)) return false;
            ChainInfo last = Duel.CurrentChainInfo.Last();
            if (last.ActivateLocation != CardLocation.Grave) return false;
            for (int index = Duel.CurrentChainInfo.Count - 2; index >= 0; index--)
            {
                ChainInfo chain = Duel.CurrentChainInfo[index];
                ClientCard target = chain.RelatedCard;
                if (chain.ActivatePlayer != 1 || chain.ActivateLocation != CardLocation.MonsterZone
                    || Duel.NegatedChainIndexList.Contains(index + 1) || target == null
                    || target.Controller != 1 || target.Location != CardLocation.MonsterZone
                    || !target.IsFaceup() || target.IsDisabled() || target.IsShouldNotBeTarget()
                    || target.IsShouldNotBeSpellTrapTarget()) continue;
                AI.SelectCard(target);
                return true;
            }
            return false;
        }

        private bool VoicelessAshBlossom()
        {
            ChainInfo chain = Duel.CurrentChainInfo.LastOrDefault();
            ClientCard droll = Bot.Hand.FirstOrDefault(card => card.IsCode(CardId.Droll));
            // With an empty public GY, Gaia's opening search must add from deck.
            // Let a usable Droll cover subsequent searches and retain Ash for
            // Crimson Resonator's deck summons. A recovery from GY, an already
            // established board, or an unavailable Droll keeps normal Ash logic.
            if (Duel.Player == 1 && Duel.LastChainPlayer == 1 && chain != null
                && chain.IsActivateCode(98173209) && chain.ActivateDescription == Util.GetStringId(98173209, 0)
                && chain.ActivateLocation == CardLocation.SpellZone && Enemy.GetMonsterCount() == 0
                && Enemy.Graveyard.Count == 0 && droll != null
                && !DefaultCheckWhetherCardIsNegated(droll) && !DefaultCheckWhetherBotWillBeBanished(droll)) return false;
            return DefaultAshBlossomAndJoyousSpring();
        }

        private bool SauravisEffect()
        {
            // The engine offers the hand effect only for an actual targeting chain,
            // and the field effect only in a summon-negation window.
            if (Card.Location == CardLocation.Hand) return Duel.LastChainPlayer == 1;
            return Card.Location == CardLocation.MonsterZone && !Card.IsDisabled();
        }

        private bool PrePrepSearch()
        {
            return AcceptVoicelessEffect(0);
        }

        private bool PrayerEffect()
        {
            return AcceptVoicelessEffect(Card.Location == CardLocation.Grave ? 1 : 0);
        }

        private bool DivinerEffect()
        {
            return AcceptVoicelessEffect(ActivateDescription == Util.GetStringId(CardId.Diviner, 1)
                || Card.Location == CardLocation.Grave || Card.Location == CardLocation.Removed ? 1 : 0);
        }

        private bool TriasEffect()
        {
            ClientCard diviner = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsCode(CardId.Diviner));
            if (diviner == null || VoicelessEffectUsed(CardId.Diviner, 1)) return false;
            // Finish the mill first, unless Tributing Diviner dodges an opponent's
            // targeted negation. Do not Tribute Lo merely because the engine allows it.
            if (Duel.LastChainPlayer == 0) return false;
            if (Duel.LastChainPlayer == 1 && !Duel.ChainTargets.Contains(diviner)) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool HeraldSearch()
        {
            return AcceptVoicelessEffect(0);
        }

        private bool BlessingEffect()
        {
            if (Card.Location == CardLocation.Hand || Card.IsFacedown())
                return !Bot.SpellZone.Any(c => c != null && c != Card && c.IsCode(CardId.Blessing) && c.IsFaceup());
            if (ActivateDescription == Util.GetStringId(CardId.Blessing, 1))
                return AcceptVoicelessEffect(0);
            bool summonTrigger = AI.SelectingEffectYn && ActivateDescription == -1
                && Duel.CurrentChain.Count == 0
                && Duel.LastSummonedCards.Any(c => c != null && c.IsFaceup() && !c.HasType(CardType.Ritual));
            if (ActivateDescription == Util.GetStringId(CardId.Blessing, 2) || summonTrigger)
                return AcceptVoicelessEffect(1);
            return false;
        }

        private bool DragonSageSummon()
        {
            // Prefer sending Lo to the GY first when the selected route calls for
            // next-turn revival; otherwise an already-present Sage remains useful.
            if (HasFaceupLo() && ShouldUseVoicelessLink(CardId.Linkuriboh)) return false;
            _pendingVoicelessSummon = CardId.VVSauravis;
            _voicelessUsed.Add(((long)CardId.VVSauravis << 4));
            return true;
        }

        private bool DragonSageEffect()
        {
            if (Card.Location != CardLocation.MonsterZone || Duel.LastChainPlayer != 1 || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(1);
        }

        private bool DynaLinkSummon()
        {
            if (!ShouldUseVoicelessLink(CardId.DynaMondo)) return false;
            _pendingVoicelessSummon = CardId.DynaMondo;
            _selectedLinkMaterials.Clear();
            return true;
        }

        private bool DynaEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.DynaMondo, 1))
                return Duel.Player == 1 && AcceptVoicelessEffect(1);
            bool summonTrigger = AI.SelectingEffectYn && ActivateDescription == -1
                && Duel.CurrentChain.Count == 0 && Duel.LastSummonedCards.Contains(Card);
            if (ActivateDescription != Util.GetStringId(CardId.DynaMondo, 0) && !summonTrigger) return false;
            // The initial shuffle would consume the Ritual intended for next turn.
            // Use removal only when a real enemy threat makes it worthwhile.
            if (GetProblematicEnemyCardList(true, selfType: CardType.Monster).Count == 0) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool LinkuribohSummon()
        {
            if (!ShouldUseVoicelessLink(CardId.Linkuriboh)) return false;
            _pendingVoicelessSummon = CardId.Linkuriboh;
            _selectedLinkMaterials.Clear();
            return true;
        }

        private bool SecureGardnaSummon()
        {
            if (!ShouldUseVoicelessLink(CardId.SecureGardna)) return false;
            _pendingVoicelessSummon = CardId.SecureGardna;
            _selectedLinkMaterials.Clear();
            return true;
        }

        private bool LinkuribohEffect()
        {
            return Card.Location == CardLocation.MonsterZone && Duel.Player == 1;
        }

        private bool RadianceEffect()
        {
            if (Card.IsFacedown()) return true;
            if (ActivateDescription == Util.GetStringId(CardId.VVRadiance, 1))
            {
                if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 0) return false;
                if (GetNormalEnemyTargetList(true, false, CardType.Trap).Count == 0) return false;
                // Preserve removal for an opponent's actual commitment; do not
                // destroy a lone harmless set card just because a window opened.
                if (Duel.Player == 1 && Duel.LastChainPlayer == -1
                    && Enemy.GetMonsterCount() == 0 && GetProblematicEnemyCardList(true).Count == 0) return false;
                return AcceptVoicelessEffect(0);
            }
            if (ActivateDescription == Util.GetStringId(CardId.VVRadiance, 0))
            {
                if (Duel.Player == 1 && HasFaceupSkull() && HasFaceupLo()) return false;
                if (Duel.Player == 0 && HasFaceupSkull() && HasFaceupLo()) return false;
                return AcceptVoicelessEffect(0);
            }
            return false;
        }

        private bool ChaosAngelSummon()
        {
            if (GetProblematicEnemyMonster() == null || HasFaceupSkull()) return false;
            AI.SelectMaterials(new[] { CardId.Trias, CardId.Lo });
            return true;
        }

        private bool VoicelessSpellSet()
        {
            return Card.IsTrap() || Card.IsCode(CardId.CalledBy);
        }

        private IList<ClientCard> SelectVoicelessCards(IList<ClientCard> cards, int min, int max,
            IEnumerable<ClientCard> preferred, int desired = 1)
        {
            List<ClientCard> ranked = preferred.Where(c => c != null && cards.Contains(c)).Distinct().ToList();
            ranked.AddRange(cards.Where(c => !ranked.Contains(c)));
            return ranked.Take(Math.Min(max, Math.Max(min, desired))).ToList();
        }

        private IList<ClientCard> SelectVoicelessIds(IList<ClientCard> cards, int min, int max, params int[] ids)
        {
            List<ClientCard> ranked = new List<ClientCard>();
            foreach (int id in ids)
                ranked.AddRange(cards.Where(c => c.IsCode(id) && !ranked.Contains(c)));
            return SelectVoicelessCards(cards, min, max, ranked);
        }

        private IEnumerable<ClientCard> RitualMaterials(IList<ClientCard> cards)
        {
            // Prefer Lo's alternate Ritual level while revival is still available.
            // Once it is spent, keep a live Lo beside Skull Guardian if another
            // offered tribute can finish the summon.
            return cards.OrderBy(c => c.IsCode(CardId.Lo)
                    ? (c.Location == CardLocation.Hand || !c.IsDisabled()
                        && (!VoicelessEffectUsed(CardId.Lo, 1) || !HasFaceupSkull()) ? 0 : 5)
                    : c.IsCode(CardId.Trias) ? 1 : c.IsCode(CardId.Diviner) ? 2
                    : c.IsCode(CardId.VVSkullGuard) ? 9 : 3)
                .ThenByDescending(c => c.Level);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            int source = VoicelessPromptSource();
            if (cards.Count == 0) return base.OnSelectCard(cards, min, max, hint, cancelable);
            if (hint == HintMsg.LinkMaterial && _pendingVoicelessSummon != 0)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                int[] ids = _pendingVoicelessSummon == CardId.SecureGardna ? new[] { CardId.Linkuriboh }
                    : _pendingVoicelessSummon == CardId.Linkuriboh ? new[] { CardId.Lo }
                    : new[] { CardId.Lo, CardId.VVSkullGuard, CardId.Sauravis };
                IList<ClientCard> chosen = SelectVoicelessIds(cards, min, max, ids);
                foreach (ClientCard material in chosen) _selectedLinkMaterials.Add(material);
                return chosen;
            }
            if (source == CardId.VVSauravis && hint == HintMsg.ToDeck)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                // Select the Ritual Spell first; subgroup offers then enforce that
                // exactly two Spells including a Ritual Spell are returned.
                return SelectVoicelessCards(cards, min, max, cards.OrderBy(c =>
                    c.IsSpell() && c.HasType(CardType.Ritual) ? 0 : c.Location == CardLocation.Grave ? 1 : 2));
            }
            if (source == CardId.Trias && hint == HintMsg.Release)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                return SelectVoicelessIds(cards, min, max, CardId.Diviner);
            }
            if ((source == CardId.Saffira || source == CardId.VVPrayer || source == CardId.Blessing)
                && hint == HintMsg.Release)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                return SelectVoicelessCards(cards, min, max, RitualMaterials(cards));
            }
            if (source == CardId.Lo && hint == HintMsg.ToField)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.Lo),
                    CardId.VVBarrier, CardId.VVRadiance, CardId.Blessing);
            if (source == CardId.VVBarrier && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.VVBarrier),
                    CardId.Saffira, CardId.Lo, CardId.Blessing, CardId.VVRadiance);
            if (source == CardId.Saffira && hint == HintMsg.ToGrave)
                return SelectVoicelessIds(cards, min, max, CardId.VVPrayer);
            if (source == CardId.Saffira && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.Saffira),
                    HasFaceupSkull() || Bot.HasInHand(CardId.VVSkullGuard) ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            if (source == CardId.VVSkullGuard && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.VVSkullGuard),
                    HasFaceupLo() || Bot.HasInGraveyard(CardId.Lo) ? CardId.Sauravis : CardId.Lo,
                    CardId.VVSauravis, CardId.Saffira);
            if (source == CardId.PrePrep && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, CardId.VVPrayer,
                    HasFaceupSkull() || Bot.HasInHand(CardId.VVSkullGuard) ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            if (source == CardId.Herald && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.Herald),
                    !Bot.HasInHand(CardId.VVPrayer) && !Bot.HasInGraveyard(CardId.Saffira) ? CardId.VVPrayer : CardId.VVSkullGuard,
                    CardId.Sauravis);
            if (source == CardId.Diviner && hint == HintMsg.ToGrave)
            {
                int mill = !HasFaceupLo() && !Bot.HasInGraveyard(CardId.Trias) && !Bot.HasInHand(CardId.Trias)
                    && Bot.HasInDeck(CardId.Trias) ? CardId.Trias
                    : GetProblematicEnemyCardList(true).Count > 0 ? CardId.Ntss : CardId.Herald;
                return SelectVoicelessIds(cards, min, max, mill, CardId.Herald, CardId.Trias);
            }
            if (source == CardId.Diviner && hint == HintMsg.SpSummon)
                return SelectVoicelessIds(cards, min, max, CardId.Lo);
            if (source == CardId.Blessing && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max,
                    !HasFaceupLo() && !Bot.HasInHand(CardId.Lo) ? CardId.Lo : CardId.Saffira,
                    CardId.Saffira, CardId.VVPrayer, CardId.Lo, CardId.VVRadiance);
            if ((source == CardId.Saffira || source == CardId.VVPrayer || source == CardId.Blessing
                || source == CardId.VVSauravis) && hint == HintMsg.SpSummon)
                return SelectVoicelessIds(cards, min, max, HasFaceupSkull() ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            if (source == CardId.DynaMondo)
            {
                if (cards.Any(c => c.Controller == 1))
                    return SelectVoicelessCards(cards, min, max, GetNormalEnemyTargetList(true));
                return SelectVoicelessIds(cards, min, max, HasFaceupSkull() ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            }
            if (source == CardId.VVRadiance)
            {
                if (hint == HintMsg.Destroy)
                    return SelectVoicelessCards(cards, min, max, GetNormalEnemyTargetList(true, false, CardType.Trap), max);
                if (hint == HintMsg.ToDeck)
                    return SelectVoicelessCards(cards, min, max,
                        cards.OrderBy(c => c.Location == CardLocation.Grave ? 0 : 1).ThenBy(c => c.IsSpell() ? 0 : 1));
                if (hint == HintMsg.OperateCard || hint == HintMsg.SpSummon || hint == HintMsg.AddToHand)
                {
                    IList<ClientCard> chosen = SelectVoicelessIds(cards, min, max,
                        !HasFaceupLo() ? CardId.Lo : CardId.VVSauravis, CardId.Saffira, CardId.Lo);
                    _radianceSelectedId = chosen.Count > 0 ? chosen[0].Id : 0;
                    return chosen;
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectRitualTribute(IList<ClientCard> cards,
            IList<ClientCard> mandatoryCards, int sum, int min, int max, bool exactEqual)
        {
            int source = VoicelessPromptSource();
            if (source != CardId.Saffira && source != CardId.VVPrayer && source != CardId.Blessing)
                return base.OnSelectRitualTribute(cards, mandatoryCards, sum, min, max, exactEqual);
            // GameAI validates the server-supplied alternate ritual levels and
            // fills a valid sum if this preference alone is not sufficient.
            return RitualMaterials(cards).Take(Math.Max(1, min)).ToList();
        }

        public override int OnSelectOption(IList<int> options)
        {
            int source = VoicelessPromptSource();
            if (source == CardId.DynaMondo || source == CardId.VVRadiance)
            {
                int wanted = source == CardId.VVRadiance && _radianceSelectedId == CardId.Saffira ? 1190 : 1152;
                int index = options.IndexOf(wanted);
                if (index >= 0) return index;
            }
            return base.OnSelectOption(options);
        }

        public override bool OnSelectYesNo(int desc)
        {
            if (VoicelessPromptSource() == CardId.Saffira && desc == Util.GetStringId(CardId.Saffira, 1)) return true;
            return base.OnSelectYesNo(desc);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if ((cardId == CardId.Lo || cardId == CardId.Diviner) && positions.Contains(CardPosition.FaceUpDefence))
                return CardPosition.FaceUpDefence;
            if (positions.Contains(CardPosition.FaceUpAttack)) return CardPosition.FaceUpAttack;
            return base.OnSelectPosition(cardId, positions);
        }

        public override void OnChaining(int player, ClientCard card)
        {
            if (player == 0 && card != null && card.Id == _acceptedActivationId) _acceptedActivationId = 0;
            base.OnChaining(player, card);
        }

        public override void OnSpSummoning()
        {
            _pendingVoicelessSummon = 0;
            _selectedLinkMaterials.Clear();
            base.OnSpSummoning();
        }

        public override void OnChainEnd()
        {
            _acceptedActivationId = 0;
            _radianceSelectedId = 0;
            currentNegateMonsterList.Clear();
            currentDestroyCardList.Clear();
            escapeTargetList.Clear();
            summonInChainList.Clear();
            chainSummoningIdList.Clear();
            OnVoicelessPlannerReset();
            base.OnChainEnd();
        }

        public override void OnNewTurn()
        {
            _voicelessUsed.Clear();
            _normalSummonUsed = false;
            _pendingVoicelessSummon = 0;
            _acceptedActivationId = 0;
            _radianceSelectedId = 0;
            _selectedLinkMaterials.Clear();
            activatedCardIdList.Clear();
            currentNegateMonsterList.Clear();
            currentDestroyCardList.Clear();
            setTrapThisTurn.Clear();
            summonThisTurn.Clear();
            enemySetThisTurn.Clear();
            escapeTargetList.Clear();
            summonInChainList.Clear();
            chainSummoningIdList.Clear();
            infiniteImpermanenceList.Clear();
            CrossoutDesignatorTarget = 0;
            foreach (int id in CalledbytheGraveCount.Keys.ToList())
            {
                CalledbytheGraveCount[id]--;
                if (CalledbytheGraveCount[id] <= 0) CalledbytheGraveCount.Remove(id);
            }
            OnVoicelessPlannerReset();
            base.OnNewTurn();
        }

        public override void OnNewPhase()
        {
            _pendingVoicelessSummon = 0;
            _acceptedActivationId = 0;
            _selectedLinkMaterials.Clear();
            OnVoicelessPlannerReset();
            base.OnNewPhase();
        }

        private bool DefaultNoExecutor()
        {
            foreach (CardExecutor exec in Executors)
            {
                if (exec.Type == Type && Card.IsOriginalCode(exec.CardId))
                    return false;
            }
            return true;
        }

        private int CompareUsableAttack(ClientCard a, ClientCard b) 
        {
            return a.Attack.CompareTo(b.Attack);
        }

        private bool EmptyMainMonsterZone()
        {
            return Bot.MonsterZone.Any(c => c !=  null && c.Data != null);
        }

        public bool SpellNegatable(bool isCounter = false, ClientCard target = null)
        {
            // target default set
            if (target == null) target = Card;
            // won't negate if not on field
            if (target.Location != CardLocation.SpellZone && target.Location != CardLocation.Hand) return false;

            // negate judge
            if (Enemy.HasInMonstersZone(CardId.NaturalExterio, true) && !isCounter) return true;
            if (target.IsSpell())
            {
                if (Enemy.HasInMonstersZone(CardId.NaturalBeast, true)) return true;
                if (Enemy.HasInSpellZone(CardId.ImperialOrder, true) || Bot.HasInSpellZone(CardId.ImperialOrder, true)) return true;
                if (Enemy.HasInMonstersZone(CardId.SwordsmanLV7, true) || Bot.HasInMonstersZone(CardId.SwordsmanLV7, true)) return true;
            }
            if (target.IsTrap())
            {
                if (Enemy.HasInSpellZone(CardId.RoyalDecree, true) || Bot.HasInSpellZone(CardId.RoyalDecree, true)) return true;
            }
            if (target.Location == CardLocation.SpellZone && (target.IsSpell() || target.IsTrap()))
            {
                int selfSeq = -1;
                for (int i = 0; i < 5; ++i)
                {
                    if (Bot.SpellZone[i] == Card) selfSeq = i;
                }
                if (infiniteImpermanenceList.Contains(selfSeq))
                {
                    return true;
                }
            }
            // how to get here?
            return false;
        }

        private CancellationTokenSource _voicelessPlanningCancellation;
        private readonly object _voicelessPlanSync = new object();
        // c26866984.lua installs a persistent leave-field redirect on the actual
        // successfully summoned card. This survives new turns, unlike its use flag.
        private readonly HashSet<ClientCard> _voicelessRedirectedTrias = new HashSet<ClientCard>();
        private readonly Dictionary<string, VoicelessPlanner.Result> _voicelessPlanningCache =
            new Dictionary<string, VoicelessPlanner.Result>();
        private bool _voicelessDisconnectBound;

        private VoicelessPlanner.State CaptureVoicelessState()
        {
            var state = new VoicelessPlanner.State { NormalAvailable = !VoicelessNormalUsed,
                OurTurn = Duel.Player == 0, SearchAllowed = DefaultCheckWhetherBotCanSearch(),
                EnemyThreat = Enemy.GetMonsters().Count > 0 || Enemy.GetSpells().Count > 0 };
            foreach (int id in VoicelessPlanner.Cards)
            {
                state.Add(VoicelessPlanner.Zone.Hand, id, Bot.Hand.Count(c => c.Id == id));
                state.Add(VoicelessPlanner.Zone.Grave, id, Bot.Graveyard.Count(c => c.Id == id));
                state.Add(VoicelessPlanner.Zone.Banished, id, Bot.Banished.Count(c => c.Id == id && c.IsFaceup()));
                state.Add(VoicelessPlanner.Zone.Extra, id, Bot.ExtraDeck.Count(c => c.Id == id));
                state.Add(VoicelessPlanner.Zone.Deck, id, Bot.GetCardCountInDeck(id));
                var field = Bot.GetMonsters().Concat(Bot.GetSpells()).Where(c => c.Id == id && c.IsFaceup()).ToList();
                state.Add(VoicelessPlanner.Zone.Field, id, field.Count);
                if (field.Count > 0 && field.All(c => c.IsDisabled())) state.DisabledMask |= 1 << Array.IndexOf(VoicelessPlanner.Cards, id);
            }
            var known = new HashSet<int>(VoicelessPlanner.Cards);
            state.OtherMonsters = Bot.GetMonsters().Count(c => !known.Contains(c.Id) || c.IsFacedown());
            state.OtherSpells = Bot.GetSpells().Count(c => !known.Contains(c.Id) || c.IsFacedown());
            state.ExtraSpells = Bot.Hand.Concat(Bot.Graveyard).Count(c => !known.Contains(c.Id) && c.IsSpell());
            state.OtherLightTributes = Bot.Hand.Concat(Bot.GetMonsters()).Count(c => !known.Contains(c.Id)
                && c.IsMonster() && c.Level >= 7 && c.HasAttribute(CardAttribute.Light));
            state.ProperSkull = Bot.GetMonsters().Concat(Bot.Graveyard).Any(c => c.Id == VoicelessPlanner.Skull && c.ProcCompleted != 0);
            state.RedirectedTrias = Bot.GetMonsters().Count(c => c.Id == VoicelessPlanner.Trias && _voicelessRedirectedTrias.Contains(c));
            int[] ids = { VoicelessPlanner.Lo, VoicelessPlanner.Lo, VoicelessPlanner.Barrier,
                VoicelessPlanner.Saffira, VoicelessPlanner.Saffira, VoicelessPlanner.Skull,
                VoicelessPlanner.Blessing, VoicelessPlanner.Blessing, VoicelessPlanner.Diviner,
                VoicelessPlanner.Trias, VoicelessPlanner.PrePrep, VoicelessPlanner.Sage };
            int[] effects = { 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0 };
            for (int index = 0; index < ids.Length; index++) state.Used[index] = VoicelessEffectUsed(ids[index], effects[index]);
            return state;
        }

        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo chain = Duel.GetCurrentSolvingChainInfo();
            if (chain != null && chain.ActivatePlayer == 0 && chain.IsActivateCode(VoicelessPlanner.Trias)
                && !Duel.IsCurrentSolvingChainNegated() && chain.RelatedCard != null
                && chain.RelatedCard.Location == CardLocation.MonsterZone && chain.RelatedCard.IsFaceup()
                && (chain.ActivateLocation == CardLocation.Hand || chain.ActivateLocation == CardLocation.Grave))
                _voicelessRedirectedTrias.Add(chain.RelatedCard);
            base.OnChainSolved(chainIndex);
        }

        public override void OnMove(ClientCard card, int previousControler, int previousLocation, int currentControler, int currentLocation)
        {
            if (card != null && previousLocation == (int)CardLocation.MonsterZone && currentLocation != (int)CardLocation.MonsterZone)
                _voicelessRedirectedTrias.Remove(card);
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
        }

        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            // RESETS_REDIRECT in the selected script includes RESET_TURN_SET.
            if (card != null && (currentPosition & (int)CardPosition.FaceDown) != 0)
                _voicelessRedirectedTrias.Remove(card);
            base.OnPosChange(card, previousPosition, currentPosition);
        }

        private VoicelessPlanner.Result PlanVoiceless(int source)
        {
            VoicelessPlanner.State state = CaptureVoicelessState();
            string key = source + ":" + state.Key();
            VoicelessPlanner.Result result;
            CancellationToken cancellation;
            lock (_voicelessPlanSync)
            {
                if (!_voicelessDisconnectBound && AI.Game != null && AI.Game.Connection != null)
                {
                    AI.Game.Connection.Disconnected += delegate(Exception error) { OnVoicelessPlannerReset(); };
                    _voicelessDisconnectBound = true;
                }
                if (_voicelessPlanningCancellation == null) _voicelessPlanningCancellation = new CancellationTokenSource();
                cancellation = _voicelessPlanningCancellation.Token;
                if (_voicelessPlanningCache.TryGetValue(key, out result)) return result;
            }
            result = source == 0 ? VoicelessPlanner.Plan(state, cancellation)
                : VoicelessPlanner.ChooseSearch(state, source, cancellation);
            // Bounded per-instance cache. Observed counts, usage, disabled status and
            // public threat presence are all in the key; predictions never enter it.
            lock (_voicelessPlanSync)
            {
                if (cancellation.IsCancellationRequested) return null;
                if (_voicelessPlanningCache.Count >= 24) _voicelessPlanningCache.Clear();
                if (result != null) _voicelessPlanningCache[key] = result;
            }
            return result;
        }

        private int GetVoicelessSearch(int sourceId)
        {
            VoicelessPlanner.Result result = PlanVoiceless(sourceId);
            return result == null ? 0 : result.TargetId;
        }

        private bool ShouldUseVoicelessLink(int id)
        {
            VoicelessPlanner.Result result = PlanVoiceless(0);
            if (result == null) return false;
            return id == VoicelessPlanner.Dyna && result.FirstAction == VoicelessPlanner.ActionKind.DynaMondo
                || id == VoicelessPlanner.Linkuriboh && result.FirstAction == VoicelessPlanner.ActionKind.Linkuriboh
                || id == VoicelessPlanner.Gardna && result.FirstAction == VoicelessPlanner.ActionKind.SecureGardna;
        }

        private string GetVoicelessRoute()
        {
            VoicelessPlanner.Result result = PlanVoiceless(0);
            return result == null ? "Retired" : string.Join(" > ", result.Steps.Select(step => step.ToString()));
        }

        private void OnVoicelessPlannerReset()
        {
            lock (_voicelessPlanSync)
            {
                if (_voicelessPlanningCancellation != null)
                {
                    _voicelessPlanningCancellation.Cancel();
                    _voicelessPlanningCancellation.Dispose();
                    _voicelessPlanningCancellation = null;
                }
                _voicelessPlanningCache.Clear();
            }
        }
    }

    // RDA-style observed state -> isolated transitions -> bounded search. These are
    // predictions used to rank a current legal offer, never a replacement duel state.
    // The model covers the five documented Voiceless routes, not arbitrary card text.
    internal static class VoicelessPlanner
    {
        internal const int Lo = 25801745, Barrier = 98477480, Saffira = 51296484;
        internal const int Skull = 10774240, Prayer = 52472775, Radiance = 86310763;
        internal const int Blessing = 39114494, Sage = 88284599, Sauravis = 4810828;
        internal const int Diviner = 92919429, Trias = 26866984, PrePrep = 13048472;
        internal const int Linkuriboh = 41999284, Gardna = 2220237, Dyna = 73898890;
        internal const int Herald = 79606837;
        internal static readonly int[] Cards = { Lo, Barrier, Saffira, Skull, Prayer,
            Radiance, Blessing, Sage, Sauravis, Diviner, Trias, PrePrep, Linkuriboh, Gardna, Dyna };

        internal enum Zone { Hand, Field, Grave, Deck, Extra, Banished }
        internal enum Effect { LoPlace, LoRevive, BarrierSearch, SaffiraSearch,
            SaffiraRitual, SkullSearch, BlessingRecover, BlessingRitual,
            DivinerMill, TriasSummon, PrePreparation, SageSummon }
        internal enum ActionKind { None, NormalLo, NormalDiviner, DivinerMill,
            TriasSummon, PlaceLo, ActivateBarrier, SearchBarrier, SaffiraSearch,
            SaffiraRitual, PrayerRitual, PrePreparation, SkullSearch, ReturnLo,
            ActivateBlessing, BlessingRecover, BlessingRitual, Linkuriboh,
            SecureGardna, DynaMondo, SummonSage, DeclineTrigger }

        internal sealed class State
        {
            private int[,] _counts = new int[6, Cards.Length];
            public bool[] Used = new bool[12];
            public bool NormalAvailable = true, OurTurn = true, SearchAllowed = true, ProperSkull;
            public bool PendingLo, PendingSkull, PendingDiviner, PendingRevive, PendingBlessing;
            public int ExtraSpells, OtherLightTributes, OtherMonsters, OtherSpells;
            public int RedirectedTrias;
            public int DisabledMask;
            public bool EnemyThreat;

            public int Count(Zone zone, int id)
            {
                int index = Array.IndexOf(Cards, id);
                return index < 0 ? 0 : _counts[(int)zone, index];
            }

            public void Add(Zone zone, int id, int count)
            {
                int index = Array.IndexOf(Cards, id);
                if (index < 0) throw new ArgumentException("Unmodeled card", "id");
                int after = _counts[(int)zone, index] + count;
                if (after < 0 || after > 60) throw new ArgumentOutOfRangeException("count");
                _counts[(int)zone, index] = after;
            }

            public State Copy()
            {
                State copy = (State)MemberwiseClone();
                copy._counts = (int[,])_counts.Clone();
                copy.Used = (bool[])Used.Clone();
                return copy;
            }

            public bool Available(Effect effect)
            {
                return !Used[(int)effect];
            }
            public bool Active(int id)
            {
                int index = Array.IndexOf(Cards, id);
                return Count(Zone.Field, id) > 0 && (DisabledMask & (1 << index)) == 0;
            }
            public string Key()
            {
                var values = new List<int>(110);
                foreach (int count in _counts) values.Add(count);
                foreach (bool used in Used) values.Add(used ? 1 : 0);
                values.AddRange(new[] { NormalAvailable ? 1 : 0, OurTurn ? 1 : 0,
                    ProperSkull ? 1 : 0, PendingLo ? 1 : 0, PendingSkull ? 1 : 0,
                    PendingDiviner ? 1 : 0, PendingRevive ? 1 : 0, PendingBlessing ? 1 : 0,
                    ExtraSpells, OtherLightTributes, OtherMonsters, OtherSpells,
                    DisabledMask, EnemyThreat ? 1 : 0, SearchAllowed ? 1 : 0, RedirectedTrias });
                return string.Join(",", values);
            }
        }

        internal sealed class Step
        {
            public readonly ActionKind Kind;
            public readonly int TargetId;
            public Step(ActionKind kind, int targetId)
            {
                Kind = kind;
                TargetId = targetId;
            }
            public override string ToString()
            {
                return Kind + ":" + TargetId;
            }
        }

        internal sealed class Result
        {
            public int TargetId, Expanded;
            public ActionKind FirstAction;
            public State FinalState;
            public IList<Step> Steps;
            public double Score;
            public bool TimedOut;
            public long ElapsedMilliseconds;
        }

        private sealed class Node
        {
            public State State;
            public List<Step> Steps;
            public double Score;
            public int Target;
        }

        private sealed class Move
        {
            public State State;
            public Step Step;
            public Move(State state, ActionKind kind, int target)
            {
                State = state;
                Step = new Step(kind, target);
            }
        }

        public static Result Plan(State observed, CancellationToken cancellation)
        {
            return Search(observed, 0, cancellation);
        }

        public static Result ChooseSearch(State observed, int sourceId, CancellationToken cancellation)
        {
            return Search(observed, sourceId, cancellation);
        }

        private static Result Search(State observed, int sourceId, CancellationToken cancellation)
        {
            // Synchronous, no worker queue. One deadline covers forced choices,
            // deduplication, expansion and sorting; node/frontier/depth are bounded.
            const int milliseconds = 100, maxExpanded = 2048, width = 128, depthLimit = 24;
            Stopwatch clock = Stopwatch.StartNew();
            State root = observed.Copy();
            var frontier = new List<Node>();
            if (sourceId == 0)
                frontier.Add(new Node { State = root, Steps = new List<Step>(), Score = Evaluate(root) });
            else
            {
                foreach (Move move in SearchChoices(root, sourceId))
                    frontier.Add(new Node { State = move.State, Steps = new List<Step> { move.Step },
                        Score = Evaluate(move.State), Target = move.Step.TargetId });
            }
            Node best = frontier.Where(n => IsSettled(n.State)).OrderByDescending(n => n.Score).FirstOrDefault();
            var seen = new HashSet<string>();
            int expanded = 0;
            for (int depth = 0; depth < depthLimit && frontier.Count > 0; depth++)
            {
                if (cancellation.IsCancellationRequested || clock.ElapsedMilliseconds >= milliseconds) break;
                var next = new List<Node>();
                foreach (Node node in frontier)
                {
                    if (cancellation.IsCancellationRequested || clock.ElapsedMilliseconds >= milliseconds || expanded >= maxExpanded) break;
                    if (!seen.Add(node.Target + ":" + node.State.Key())) continue;
                    expanded++;
                    foreach (Move move in Expand(node.State))
                    {
                        if (cancellation.IsCancellationRequested || clock.ElapsedMilliseconds >= milliseconds || next.Count >= width * 16) break;
                        var steps = new List<Step>(node.Steps) { move.Step };
                        var child = new Node { State = move.State, Steps = steps,
                            Score = Evaluate(move.State) - steps.Count * 0.05, Target = node.Target };
                        next.Add(child);
                        if (IsSettled(child.State) && (best == null || child.Score > best.Score)) best = child;
                    }
                }
                if (expanded >= maxExpanded) break;
                frontier = next.OrderByDescending(n => n.Score).Take(width).ToList();
            }
            if (cancellation.IsCancellationRequested) return null;
            return new Result { TargetId = best == null ? 0 : best.Target,
                FirstAction = best == null || best.Steps.Count == 0 ? ActionKind.None : best.Steps[0].Kind,
                FinalState = best == null ? root : best.State.Copy(),
                Steps = best == null ? new List<Step>().AsReadOnly() : best.Steps.AsReadOnly(),
                Score = best == null ? Evaluate(root) : best.Score, Expanded = expanded,
                TimedOut = clock.ElapsedMilliseconds >= milliseconds, ElapsedMilliseconds = clock.ElapsedMilliseconds };
        }

        private static void MoveCard(State state, Zone from, Zone to, int id)
        {
            state.Add(from, id, -1);
            if (from == Zone.Field && state.Count(Zone.Field, id) == 0)
                state.DisabledMask &= ~(1 << Array.IndexOf(Cards, id));
            if (id == Trias && from == Zone.Field && state.RedirectedTrias > 0)
            {
                state.RedirectedTrias--;
                if (to == Zone.Grave) to = Zone.Banished;
            }
            state.Add(to, id, 1);
        }

        private static bool IsSettled(State state)
        {
            return !state.PendingLo && !state.PendingSkull && !state.PendingDiviner
                && !state.PendingRevive && !state.PendingBlessing;
        }

        private static Move Change(State source, ActionKind kind, int id, Zone from, Zone to)
        {
            State state = source.Copy();
            MoveCard(state, from, to, id);
            return new Move(state, kind, id);
        }

        private static IEnumerable<Move> SearchChoices(State state, int source)
        {
            if (source == Lo && SpellCount(state) >= 5) yield break;
            if (!state.SearchAllowed && source != Lo && source != Saffira) yield break;
            int[] targets;
            ActionKind kind;
            if (source == Lo) { targets = new[] { Barrier, Radiance, Blessing }; kind = ActionKind.PlaceLo; }
            else if (source == Barrier) { targets = new[] { Saffira, Blessing, Sage, Lo, Skull, Prayer, Radiance }; kind = ActionKind.SearchBarrier; }
            else if (source == Skull) { targets = new[] { Sauravis, Sage, Lo, Saffira, Skull }; kind = ActionKind.SkullSearch; }
            else if (source == Saffira) { targets = new[] { Skull, Sauravis }; kind = ActionKind.SaffiraSearch; }
            else if (source == Herald) { targets = new[] { Skull, Prayer, Sauravis }; kind = ActionKind.SkullSearch; }
            else if (source == PrePrep) { targets = new[] { Prayer }; kind = ActionKind.PrePreparation; }
            else yield break;
            foreach (int target in targets)
            {
                Zone from = Zone.Deck;
                if ((state.Count(from, target) == 0 || !state.SearchAllowed) && source == Saffira) from = Zone.Grave;
                if (state.Count(from, target) == 0) continue;
                if (source == Lo && state.Count(Zone.Field, target) > 0) continue;
                Move move = Change(state, kind, target, from, source == Lo ? Zone.Field : Zone.Hand);
                if (source == Lo) { move.State.PendingLo = false; move.State.Used[(int)Effect.LoPlace] = true; }
                if (source == Barrier) move.State.Used[(int)Effect.BarrierSearch] = true;
                if (source == Skull) { move.State.PendingSkull = false; move.State.Used[(int)Effect.SkullSearch] = true; }
                yield return move;
            }
        }

        private static IEnumerable<Move> Expand(State state)
        {
            if (state.PendingRevive)
            {
                State after = state.Copy(); after.PendingRevive = false;
                if (after.Available(Effect.LoRevive) && after.Count(Zone.Grave, Lo) > 0 && MonsterCount(after) < 5)
                {
                    MoveCard(after, Zone.Grave, Zone.Field, Lo);
                    after.Used[(int)Effect.LoRevive] = true;
                    after.PendingLo = after.Available(Effect.LoPlace);
                    yield return new Move(after, ActionKind.ReturnLo, Lo);
                }
                else yield return new Move(after, ActionKind.DeclineTrigger, Lo);
                yield break;
            }
            if (state.PendingLo)
            {
                foreach (Move move in SearchChoices(state, Lo)) yield return move;
                State after = state.Copy(); after.PendingLo = false;
                yield return new Move(after, ActionKind.DeclineTrigger, Lo);
                yield break;
            }
            if (state.PendingSkull)
            {
                foreach (Move move in SearchChoices(state, Skull)) yield return move;
                State after = state.Copy(); after.PendingSkull = false;
                yield return new Move(after, ActionKind.DeclineTrigger, Skull);
                yield break;
            }
            if (state.PendingDiviner)
            {
                State after = state.Copy(); after.PendingDiviner = false;
                after.Used[(int)Effect.DivinerMill] = true;
                if (after.Count(Zone.Deck, Trias) > 0)
                {
                    MoveCard(after, Zone.Deck, Zone.Grave, Trias);
                    yield return new Move(after, ActionKind.DivinerMill, Trias);
                }
                else yield return new Move(after, ActionKind.DeclineTrigger, Diviner);
                yield break;
            }
            if (state.PendingBlessing)
            {
                State after = state.Copy(); after.PendingBlessing = false;
                if (after.Active(Blessing) && after.Available(Effect.BlessingRitual))
                    foreach (Move move in Rituals(after, ActionKind.BlessingRitual)) yield return move;
                yield return new Move(after, ActionKind.DeclineTrigger, Blessing);
                yield break;
            }
            if (!state.OurTurn) yield break;

            if (state.NormalAvailable && MonsterCount(state) < 5)
            {
                if (state.Count(Zone.Hand, Lo) > 0)
                {
                    Move move = Change(state, ActionKind.NormalLo, Lo, Zone.Hand, Zone.Field);
                    move.State.NormalAvailable = false; move.State.PendingLo = state.Available(Effect.LoPlace);
                    move.State.PendingBlessing = state.Active(Blessing) && state.Available(Effect.BlessingRitual);
                    yield return move;
                }
                if (state.Count(Zone.Hand, Diviner) > 0)
                {
                    Move move = Change(state, ActionKind.NormalDiviner, Diviner, Zone.Hand, Zone.Field);
                    move.State.NormalAvailable = false; move.State.PendingDiviner = state.Available(Effect.DivinerMill);
                    yield return move;
                }
            }
            if (state.Available(Effect.TriasSummon) && state.Count(Zone.Field, Diviner) > 0)
            {
                Zone from = state.Count(Zone.Grave, Trias) > 0 ? Zone.Grave : Zone.Hand;
                if (state.Count(from, Trias) > 0)
                {
                    Move move = Change(state, ActionKind.TriasSummon, Trias, from, Zone.Field);
                    MoveCard(move.State, Zone.Field, Zone.Grave, Diviner);
                    move.State.Used[(int)Effect.TriasSummon] = true;
                    move.State.RedirectedTrias++;
                    Zone loFrom = state.Count(Zone.Deck, Lo) > 0 ? Zone.Deck : Zone.Hand;
                    if (state.Count(loFrom, Lo) > 0 && MonsterCount(move.State) < 5)
                    {
                        MoveCard(move.State, loFrom, Zone.Field, Lo);
                        move.State.PendingLo = state.Available(Effect.LoPlace);
                    }
                    yield return move;
                }
            }
            if (SpellCount(state) < 5)
            {
                foreach (int id in new[] { Barrier, Blessing })
                    if (state.Count(Zone.Hand, id) > 0 && state.Count(Zone.Field, id) == 0)
                        yield return Change(state, id == Barrier ? ActionKind.ActivateBarrier : ActionKind.ActivateBlessing,
                            id, Zone.Hand, Zone.Field);
            }
            if (state.Active(Barrier) && state.Available(Effect.BarrierSearch))
                foreach (Move move in SearchChoices(state, Barrier)) yield return move;
            if (state.SearchAllowed && state.Count(Zone.Hand, PrePrep) > 0 && state.Available(Effect.PrePreparation)
                && state.Count(Zone.Deck, Prayer) > 0 && (state.Count(Zone.Deck, Skull) + state.Count(Zone.Grave, Skull)) > 0)
            {
                Move move = Change(state, ActionKind.PrePreparation, PrePrep, Zone.Hand, Zone.Grave);
                MoveCard(move.State, Zone.Deck, Zone.Hand, Prayer);
                MoveCard(move.State, state.Count(Zone.Deck, Skull) > 0 ? Zone.Deck : Zone.Grave, Zone.Hand, Skull);
                move.State.Used[(int)Effect.PrePreparation] = true;
                yield return move;
            }
            if (state.Count(Zone.Hand, Saffira) > 0 && state.Available(Effect.SaffiraSearch) && state.Count(Zone.Deck, Prayer) > 0)
            {
                State after = state.Copy();
                MoveCard(after, Zone.Hand, Zone.Grave, Saffira); MoveCard(after, Zone.Deck, Zone.Grave, Prayer);
                after.Used[(int)Effect.SaffiraSearch] = true;
                foreach (Move move in SearchChoices(after, Saffira)) yield return move;
                yield return new Move(after, ActionKind.SaffiraSearch, 0);
            }
            if (state.Count(Zone.Grave, Saffira) > 0 && state.Available(Effect.SaffiraRitual))
                foreach (Move move in Rituals(state, ActionKind.SaffiraRitual)) yield return move;
            if (state.Count(Zone.Hand, Prayer) > 0)
                foreach (Move move in Rituals(state, ActionKind.PrayerRitual)) yield return move;
            if (state.Active(Blessing) && state.Available(Effect.BlessingRecover))
            {
                foreach (int id in new[] { Lo, Saffira, Prayer, Skull, Sage, Barrier, Radiance })
                {
                    Zone from = state.Count(Zone.Grave, id) > 0 ? Zone.Grave : Zone.Banished;
                    if (state.Count(from, id) == 0) continue;
                    Move move = Change(state, ActionKind.BlessingRecover, id, from, Zone.Hand);
                    move.State.Used[(int)Effect.BlessingRecover] = true;
                    yield return move;
                }
            }
            if (state.Count(Zone.Hand, Sage) > 0 && state.Available(Effect.SageSummon) && MonsterCount(state) < 5)
            {
                State after = state.Copy();
                if (PaySage(after))
                {
                    MoveCard(after, Zone.Hand, Zone.Field, Sage); after.Used[(int)Effect.SageSummon] = true;
                    yield return new Move(after, ActionKind.SummonSage, Sage);
                }
            }
            if (state.Count(Zone.Field, Lo) > 0 && state.Count(Zone.Extra, Linkuriboh) > 0)
            {
                Move move = Change(state, ActionKind.Linkuriboh, Linkuriboh, Zone.Extra, Zone.Field);
                MoveCard(move.State, Zone.Field, Zone.Grave, Lo);
                move.State.PendingBlessing = state.Active(Blessing) && state.Available(Effect.BlessingRitual);
                yield return move;
            }
            if (state.Count(Zone.Field, Linkuriboh) > 0 && state.Count(Zone.Extra, Gardna) > 0)
            {
                Move move = Change(state, ActionKind.SecureGardna, Gardna, Zone.Extra, Zone.Field);
                MoveCard(move.State, Zone.Field, Zone.Grave, Linkuriboh);
                move.State.PendingBlessing = state.Active(Blessing) && state.Available(Effect.BlessingRitual);
                yield return move;
            }
            if (state.Count(Zone.Field, Lo) > 0 && state.Count(Zone.Field, Skull) > 0 && state.ProperSkull && state.Count(Zone.Extra, Dyna) > 0)
            {
                Move move = Change(state, ActionKind.DynaMondo, Dyna, Zone.Extra, Zone.Field);
                MoveCard(move.State, Zone.Field, Zone.Grave, Lo); MoveCard(move.State, Zone.Field, Zone.Grave, Skull);
                yield return move;
            }
        }

        private static IEnumerable<Move> Rituals(State state, ActionKind kind)
        {
            foreach (int ritual in new[] { Skull, Sauravis })
            {
                if (state.Count(Zone.Hand, ritual) == 0) continue;
                foreach (int material in new[] { Lo, Trias, Sage, Sauravis, Skull, 0 })
                {
                    if (material == 0 && state.OtherLightTributes == 0) continue;
                    Zone from = state.Count(Zone.Field, material) > 0 ? Zone.Field : Zone.Hand;
                    if (material != 0 && state.Count(from, material) <= (from == Zone.Hand && material == ritual ? 1 : 0)) continue;
                    if (material == Lo && from == Zone.Field && !state.Active(Lo)) continue;
                    State after = state.Copy();
                    if (material == 0) after.OtherLightTributes--;
                    else MoveCard(after, from, Zone.Grave, material);
                    if (MonsterCount(after) >= 5) continue;
                    if (kind == ActionKind.SaffiraRitual)
                    {
                        MoveCard(after, Zone.Grave, Zone.Banished, Saffira);
                        after.Used[(int)Effect.SaffiraRitual] = true;
                    }
                    else if (kind == ActionKind.PrayerRitual) MoveCard(after, Zone.Hand, Zone.Grave, Prayer);
                    else after.Used[(int)Effect.BlessingRitual] = true;
                    MoveCard(after, Zone.Hand, Zone.Field, ritual);
                    if (ritual == Skull)
                    {
                        after.ProperSkull = true;
                        after.PendingSkull = after.Available(Effect.SkullSearch);
                    }
                    after.PendingRevive = after.Count(Zone.Grave, Lo) > 0 && after.Available(Effect.LoRevive);
                    yield return new Move(after, kind, ritual);
                }
            }
        }

        private static bool PaySage(State state)
        {
            Zone prayerFrom = state.Count(Zone.Grave, Prayer) > 0 ? Zone.Grave : Zone.Hand;
            if (state.Count(prayerFrom, Prayer) == 0) return false;
            MoveCard(state, prayerFrom, Zone.Deck, Prayer);
            foreach (Zone zone in new[] { Zone.Grave, Zone.Hand })
                foreach (int id in new[] { PrePrep, Prayer, Barrier, Blessing })
                    if (state.Count(zone, id) > 0) { MoveCard(state, zone, Zone.Deck, id); return true; }
            if (state.ExtraSpells > 0) { state.ExtraSpells--; return true; }
            return false;
        }

        private static int MonsterCount(State state)
        {
            return state.OtherMonsters + new[] { Lo, Saffira, Skull, Sage, Sauravis, Diviner, Trias, Linkuriboh, Gardna, Dyna }
                .Sum(id => state.Count(Zone.Field, id));
        }

        private static int SpellCount(State state)
        {
            return state.OtherSpells + new[] { Barrier, Radiance, Blessing }.Sum(id => state.Count(Zone.Field, id));
        }

        public static double Evaluate(State state)
        {
            bool lo = state.Count(Zone.Field, Lo) > 0, skull = state.Active(Skull), sage = state.Active(Sage);
            int rituals = (skull ? 1 : 0) + (state.Active(Sauravis) ? 1 : 0);
            double score = skull ? (lo ? 140 : 38) : 0;
            if (state.Active(Sauravis)) score += 38;
            if (state.Active(Barrier)) score += lo && rituals > 0 ? 25 : 9;
            if (state.Active(Radiance)) score += rituals * 36;
            if (state.Active(Blessing)) score += 12;
            if (state.Active(Trias)) score += 8;
            if (state.Active(Linkuriboh)) score += 8;
            if (state.Active(Gardna)) score += 6;
            score += Math.Min(1, state.Count(Zone.Hand, Sauravis)) * 20;
            score += state.Count(Zone.Hand, Lo) * 2 + state.Count(Zone.Hand, Saffira) * 2;
            score += state.ExtraSpells * 0.4 + state.OtherLightTributes * 0.4;

            // Discounted opponent-turn opportunities. No promise is counted as a
            // currently live negate; the actual executor rechecks offers and zones.
            bool nextPlacement = state.Count(Zone.Deck, Radiance) > 0 && SpellCount(state) < 5;
            bool loGrave = state.Count(Zone.Grave, Lo) > 0;
            if (state.OurTurn && state.Active(Dyna) && state.Count(Zone.Grave, Skull) > 0 && state.ProperSkull && loGrave)
                score += (state.EnemyThreat ? 92 : 142) + (nextPlacement ? 39 : 0);
            if (state.OurTurn && sage && skull && loGrave && state.Count(Zone.Deck, Sauravis) + state.Count(Zone.Hand, Sauravis) > 0)
                score += (state.EnemyThreat ? 112 : 151) + (nextPlacement ? 48 : 0);
            if (state.OurTurn && state.Active(Blessing) && skull && lo && state.Count(Zone.Hand, Sauravis) > 0)
                score += (state.EnemyThreat ? 35 : 76) + (nextPlacement ? 25 : 0);
            if (state.EnemyThreat && skull && lo) score += 45;
            return score;
        }
    }
}
