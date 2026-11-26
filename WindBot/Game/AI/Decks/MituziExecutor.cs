using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

//剩余BUG：
//攻击对象不懂得选择
namespace WindBot.Game.AI.Decks
{
    [Deck("Mituzi", "AI_Mituzi")]
    public class MituziExecutor : RushExecutor
    {
        public class CardId
        {
            public const int 幻煌 = 120247063;
            public const int 起重龙 = 120199020;
            public const int 幻龙重骑 = 120155022;
            public const int 建造龙 = 120155024;
            public const int 幻刃破龙 = 120151029;
            public const int 铲子龙 = 120155002;
            public const int 搅拌龙 = 120192010;
            public const int 荒废蟠龙 = 120192011;
            public const int 风车鳞虫 = 120183034;
            public const int 丰沃蟠龙 = 120170037;
            public const int 高空工龙 = 120247046;
            public const int 镐头龙 = 120155025;
            public const int 贯通 = 120120037;
            public const int 阴阳封阵 = 120183053;
            public const int 死者苏生 = 120194004;
            public const int 透幻乡险峻 = 120155042;
            public const int 风 = 120155044;
            public const int 幻刃复归 = 120151033;
            public const int 幻刃急攻 = 120155053;
            public const int 幻刃封锁 = 120155054;
            public const int 幻坏奥义 = 120247058;
            public const int 暗黑释放 = 120105013;
        }


        private static readonly int[] priList = { CardId.起重龙, CardId.建造龙, CardId.幻龙重骑,  CardId.幻煌, CardId.幻刃破龙 };
        private static readonly int[] rePriList = { CardId.幻刃破龙, CardId.幻煌, CardId.幻龙重骑, CardId.建造龙, CardId.起重龙, };
        public MituziExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, RushCardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, RushCardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, CardId.幻坏奥义, 奥义Effect);
            AddExecutor(ExecutorType.Activate, CardId.阴阳封阵, 阴阳封阵Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.阴阳封阵);
            AddExecutor(ExecutorType.Activate, CardId.透幻乡险峻,DefaultField);
            AddExecutor(ExecutorType.Repos, 幻刃急攻Repos);
            AddExecutor(ExecutorType.Activate, CardId.风);
            AddExecutor(ExecutorType.Activate, CardId.丰沃蟠龙,丰沃蟠龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.荒废蟠龙, 荒废蟠龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.搅拌龙, 搅拌龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.风车鳞虫);
            AddExecutor(ExecutorType.Activate, CardId.起重龙);
            AddExecutor(ExecutorType.Activate, CardId.建造龙, 建造龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.镐头龙, 镐头龙Effect);
            AddExecutor(ExecutorType.Summon, CardId.幻煌, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, RushCardId.伟大魔兽, 伟大魔兽Summon);
            AddExecutor(ExecutorType.Summon, 幻刃急攻Summon);

            AddExecutor(ExecutorType.Summon, CardId.镐头龙, 镐头龙Summon);
            AddExecutor(ExecutorType.Summon, CardId.搅拌龙, 搅拌龙Summon);
            AddExecutor(ExecutorType.MonsterSet, 幻刃急攻Set);          
            AddExecutor(ExecutorType.Activate, CardId.死者苏生, 死苏Effect);
            AddExecutor(ExecutorType.Activate, CardId.贯通, 贯通Effect);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.幻刃复归, 幻刃复归Effect);
            AddExecutor(ExecutorType.Activate, CardId.幻刃封锁, 幻刃封锁Effect);
            AddExecutor(ExecutorType.Activate, CardId.幻刃急攻, 幻刃急攻Effect);
            AddExecutor(ExecutorType.Activate, CardId.透幻乡险峻);
            AddExecutor(ExecutorType.Activate, RushCardId.破灭之龙魔导士, 破灭之龙魔导士Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.星战骑佩流安, 星战骑佩流安Effect);
            AddExecutor(ExecutorType.Activate, RushCardId.加百列热茶, 加百列热茶Effect);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.幻煌,反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.起重龙,反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.建造龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, 圣防Repos);
            AddExecutor(ExecutorType.SpellSet, TrapCardSet);
            AddExecutor(ExecutorType.SpellSet);
        }

         private bool TrapCardSet()
         {
             if (Card.HasType(CardType.Trap)) return true;
             return false;
         }
         private bool MonsRepos()
         {
             if (Card.Id == CardId.镐头龙)
             {
                 if (Card.IsAttack()) return true;
                 if (Card.IsFacedown())
                     return Discord1Draw1Eff(CardRace.Wyrm, false);

                 return false;
             }
             if (Card.Id == CardId.风车鳞虫)
             {
                 if (Card.IsFacedown())
                 {
                     ClientCard card = WeakestFaceupEnemy();
                     if (card == null) return true;
                     if (Bot.HasInSpellZone(CardId.透幻乡险峻))
                     {
                         if (Card.Attack + 400 >= (card.IsAttack() ? card.Attack : card.Defense))
                             return true;
                         else return false;
                     }
                     else
                     {
                         if (Card.Attack >= (card.IsAttack() ? card.Attack : card.Defense))
                             return true;
                         else return false;
                     }
                 }
             }
             if (Card.Id == CardId.丰沃蟠龙)
             {
                 if (Card.IsFacedown())
                     return Bot.GetMonsterCount()>1&&
                         Bot.Hand.GetMatchingCardsCount(card => card.Level > 4) > 0;
             }
             ClientCard emMon = WeakestFaceupEnemy();
             if (emMon == null) return Card.IsDefense();
             else
             {
                 if (Card.Attack >= emMon.GetDefensePower()) return Card.IsDefense();
                 else return Card.IsAttack();
             }

         }

         private bool 镐头龙Summon()
         {
             if (Bot.HasInMonstersZone(CardId.幻刃破龙) || (Bot.HasInMonstersZone(CardId.幻煌)) && Bot.HasInHand(CardId.建造龙) && Bot.HasInHand(CardId.起重龙))
                 return Discord1Draw1Eff(CardRace.Wyrm, false, true);
             return Discord1Draw1Eff(CardRace.Wyrm, false, false);
         }
         private bool 风车鳞虫Summon()
         {
             ClientCard card = WeakestFaceupEnemy();
             if (card == null) return true;
             if (Bot.HasInSpellZone(CardId.透幻乡险峻))
             {
                 if (Card.Attack + 400 >= (card.IsAttack() ? card.Attack : card.Defense))
                     return true;
                 else return false;
             }
             else
             {
                 if (Card.Attack >= (card.IsAttack() ? card.Attack : card.Defense))
                     return true;
                 else return false;
             }

         }
         private bool 搅拌龙Summon()
         {
             return HasFaceupFieldSpell();
         }

    
       // protected override IList<ClientCard> OnSelectTribute(IList<ClientCard> cards, int min, int max)

            protected bool 伟大魔兽Summon()
        {
            int[] materials = new[] { CardId.幻刃破龙, CardId.建造龙, CardId.幻龙重骑, CardId.起重龙 };
            if (Bot.HasInMonstersZone(materials))
            {
                AI.SelectCard(materials);
                return true;
            }
            return false;
        }
        private bool 幻刃急攻Summon()
        {
            if (Card.Level < 5 && (Card.Race & (int)CardRace.Wyrm) > 0 &&
                Bot.HasInSpellZone(CardId.幻刃急攻) && !IsUpperMonSummon())
            {
                IList<ClientCard> lowerList =
                   Bot.Hand.GetMatchingCards(card => (card.Level < 5 && ((card.Race & (int)CardRace.Wyrm) > 0)));
                if (Card.Id == lowerList.OrderByDescending(card=>card.Attack).FirstOrDefault().Id &&
                    Bot.Hand.GetMatchingCardsCount(card => card.Id == Card.Id) < 2)
                    return false;
            }
            return MonsSummon();
        }

        private bool 幻刃急攻Repos()
        {
            if (Bot.HasInSpellZone(CardId.幻刃急攻)&&
                Bot.Hand.GetMatchingCardsCount(card => card.Level < 7 && (card.Race & (int)CardRace.Wyrm) > 0)>0)
            {
                ClientCard bestMon = Bot.MonsterZone.GetMonsters().OrderByDescending(card => card.Attack).FirstOrDefault();
                if (bestMon == null) return false;
                if(Card.Id == bestMon.Id)
                {
                    return !Card.IsAttack();
                }
            }
            return MonsRepos();
        }
        private bool 幻刃急攻Set()
        {
            if (Card.Level < 5 && (Card.Race & (int)CardRace.Wyrm) > 0 &&
                Bot.HasInSpellZone(CardId.幻刃急攻) && !IsUpperMonSummon())
            {
                IList<ClientCard> lowerList =
                    Bot.Hand.GetMatchingCards(card => (card.Level < 5 && ((card.Race & (int)CardRace.Wyrm) > 0)));
                if (Card.Id == lowerList.OrderByDescending(card => card.Attack).FirstOrDefault().Id &&
                    Bot.Hand.GetMatchingCardsCount(card => card.Id == Card.Id) < 2)
                    return false;
            }
            return MonsSet();
        }
        private bool MonsSet()
        {
            ClientCard EmWorstMon = WeakestFaceupEnemy();
            if (Card.Level < 5)
            {
                if (Card.Id == CardId.镐头龙)
                {
                    return !Discord1Draw1Eff(CardRace.Wyrm, false);
                }

                if (Duel.Turn == 1)
                { if (Bot.HasInHandOrInSpellZone(CardId.幻刃封锁) &&
                    (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsFaceup() && card.Level < 5) <= 0))
                        return false;
                    else
                        return true;
                }
                if (EmWorstMon == null) return false;

                if (Card.Id == CardId.风车鳞虫)
                {
                    return !风车鳞虫Summon();
                }
                if (Bot.HasInSpellZone(CardId.透幻乡险峻))
                {
                    return Card.Attack + 400 < (EmWorstMon.GetDefensePower());
                }
                else 
                return Card.Attack < (EmWorstMon.GetDefensePower());
            }
            else
                return false;
        }

        private bool UpperMonSummon(ClientCard _card)       //检查某怪兽是否能够上级召唤
        {
            if (_card.Level < 5) return true;
            int tributeAllow = Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5);
            if (HasFaceupFieldSpell())
            {
                if (_card.Id == CardId.建造龙)
                    tributeAllow += Bot.HasInMonstersZone(CardId.幻刃破龙) ? 1 : 0;
                tributeAllow += Bot.HasInMonstersZone(CardId.幻煌) ? 1 : 0;
                if (_card.Id == CardId.起重龙 || Bot.HasInMonstersZone(CardId.起重龙))
                    tributeAllow += Bot.HasInMonstersZone(CardId.建造龙) ? 1 : 0;
            }
            if ((_card.Id != CardId.幻刃破龙 && _card.Id != CardId.幻煌) && Bot.Hand.GetMatchingCardsCount(card => card.Level > 6) > 2 &&
                Bot.Hand.Count(card => card.IsMonster() && card.Level < 5) + Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) < 2)
                tributeAllow = Bot.GetMonsterCount();
            return tributeAllow >= (_card.Level > 6 ? 2 : 1);
        }

        private bool IsUpperMonSummon()
        {
            int tributeCandidates = Bot.MonsterZone.Count(c => c != null && c.Level < 5)
                + Bot.Hand.Count(c => c.IsMonster() && c.Level < 5);
            bool hasSpace = Bot.MonsterZone.Count(c => c != null && c.Level > 4) <= 1;
            return Bot.Hand.Any(c => c.Level > 4 && (UpperMonSummon(c)
                || (hasSpace && tributeCandidates >= (c.Level > 6 ? 2 : 1))));
        }
        private bool MonsSummon()
        {
            if (Card.Id == CardId.镐头龙 )
            {
                return 镐头龙Summon();
            }
            if(Card.Id == CardId.风车鳞虫)
            {
                return 风车鳞虫Summon();
            }
            if(Card.Level>6)
            {
                return UpperMonSummon(Card);
            }
            return RushMonsterSummon();
        }
        private bool 镐头龙Effect()
        {
            if (Bot.HasInMonstersZone(CardId.幻刃破龙) || (Bot.HasInMonstersZone(CardId.幻煌)) && Bot.HasInHand(CardId.建造龙) && Bot.HasInHand(CardId.起重龙))
                return Discord1Draw1Eff(CardRace.Wyrm, true, true);
            return Discord1Draw1Eff(CardRace.Wyrm, true, false);
        }
        private bool Discord1Draw1Eff(CardRace race, bool isAISelect = true, bool isReplaceUpperMon = false)
        {
            if (Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0) > 0)
            {
                int triCost = Bot.Hand.GetMatchingCardsCount(card => card.Level > 4 && card.Level < 7) +
                    Bot.Hand.GetMatchingCardsCount(card => card.Level > 6) * 2;
                int tributeNum = (isReplaceUpperMon ?
                    Bot.GetMonsterCount() : Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5)) +
                    Bot.Hand.Count(card => card.IsMonster() && card.Level < 5);

                if (triCost > tributeNum &&
                    Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0 && card.Level > 4) > 0)
                {
                    if (isAISelect && triCost > 2) AI.SelectCard(rePriList);
                }
                else if (triCost < tributeNum -1 && triCost >0 && Bot.GetSpellCountWithoutField()<3 &&
                     Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0 && card.Level < 5) > 0)
                {
                    if (isAISelect) AI.SelectCard(Bot.Hand.GetFirstMatchingCard(card => (card.Race & (int)race) > 0 && card.Level < 5));
                }
                else return false;
                return true;
            }
            return false;
        }
        private bool sealResolved;

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            sealResolved = false;
        }

        public override void OnChainSolved(int chainIndex)
        {
            base.OnChainSolved(chainIndex);
            if (chainIndex <= 0 || chainIndex > Duel.CurrentChainInfo.Count || Duel.NegatedChainIndexList.Contains(chainIndex))
                return;
            ChainInfo link = Duel.CurrentChainInfo[chainIndex - 1];
            if (link.ActivatePlayer == 0 && link.IsActivateCode(CardId.阴阳封阵))
                sealResolved = true;
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (sealResolved && Bot.GetMonsterCount() <= Enemy.GetMonsterCount())
            {
                for (int i = 0; i < defenders.Count; i++)
                {
                    ClientCard defender = defenders[i];
                    if (!defender.IsFaceup() || !defender.IsAttack()) continue;
                    attacker.RealPower = attacker.Attack;
                    defender.RealPower = defender.GetDefensePower();
                    if (!OnPreBattleBetween(attacker, defender))
                        continue;
                    if (attacker.RealPower > defender.RealPower && defender.Level > 6 && defender.IsAttack() && defender.Attack < 1800)
                    {
                        return AI.Attack(attacker, defender);
                    }
                }
            }
            return base.OnSelectAttackTarget(attacker, defenders);
        }
        private ClientCard WeakestFaceupEnemy()
        {
            return Enemy.MonsterZone.Where(c => c != null && c.IsFaceup()).OrderBy(c => c.GetDefensePower()).FirstOrDefault();
        }

        private bool HasFaceupFieldSpell()
        {
            return Bot.GetFieldSpellCard()?.IsFaceup() == true || Enemy.GetFieldSpellCard()?.IsFaceup() == true;
        }

        private bool 建造龙Effect()
        {
            if (!HasFaceupFieldSpell() || Bot.Hand.Any(c => c.Level > 6 && UpperMonSummon(c)))
                return false;
            ClientCard monster = Bot.Graveyard.Where(c => c.HasRace(CardRace.Wyrm) && c.HasType(CardType.Normal))
                .OrderByDescending(c => c.Level).FirstOrDefault();
            if (monster == null)
                return false;
            ClientCard enemy = WeakestFaceupEnemy();
            int power = monster.Attack + (Bot.HasInSpellZone(CardId.透幻乡险峻) ? 400 : 0);
            AI.SelectCard(monster);
            AI.SelectPosition(enemy == null || power >= enemy.GetDefensePower() ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence);
            return true;
        }

        private bool 搅拌龙Effect()
        {
            return HasFaceupFieldSpell();
        }

        private bool 丰沃蟠龙Effect()
        {
            return Bot.Graveyard.Any(c => c.HasType(CardType.Field));
        }

        private bool 荒废蟠龙Effect()
        {
            ClientCard field = Bot.GetFieldSpellCard();
            if (field == null || !field.IsFaceup() || (Enemy.LifePoints > 600 && !Bot.HasInHand(CardId.透幻乡险峻)))
                return false;
            AI.SelectCard(field);
            return true;
        }

        private bool 奥义Effect()
        {
            int enemyAttack = Util.GetTotalAttackingMonsterAttack(1);
            if (!(enemyAttack >= 1900 && Enemy.MonsterZone.Where(c => c != null && c.IsFaceup()).GetImmuneTrapMonster() == null)
                && !(enemyAttack - Util.GetTotalAttackingMonsterAttack(0) > Bot.LifePoints && !Bot.HasDefendingMonster()))
                return false;
            var targets = new List<ClientCard>(6);
            int levels = 0;
            foreach (ClientCard monster in Bot.Graveyard.Where(c => c.HasRace(CardRace.Wyrm)).OrderByDescending(c => c.Level).Take(6))
            {
                targets.Add(monster);
                levels += monster.Level;
                if (levels >= 16)
                {
                    AI.SelectCard(targets);
                    return true;
                }
            }
            return false;
        }

        private bool 贯通Effect()
        {
            if (!Enemy.HasDefendingMonster() || !Bot.HasAttackingMonster())
                return false;
            ClientCard defender = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup() && c.IsDefense())
                .OrderByDescending(c => c.Defense).FirstOrDefault();
            var attackers = Bot.MonsterZone.Where(c => c != null && c.IsFaceup() && c.IsAttack());
            ClientCard target;
            if (defender == null)
                target = attackers.OrderByDescending(c => c.Attack).FirstOrDefault();
            else
            {
                var tributeMonsters = attackers.Where(c => c.Level >= 7);
                if (tributeMonsters.Count() <= Enemy.MonsterZone.Count(c => c != null && c.IsFaceup() && c.IsAttack() && c.Level >= 7))
                    return false;
                target = tributeMonsters.Where(c => c.Attack > defender.Defense).OrderBy(c => c.Attack).FirstOrDefault();
            }
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool 幻刃复归Effect()
        {
            bool canReviveNormal = Bot.HasInMonstersZone(CardId.建造龙) && HasFaceupFieldSpell()
                && Bot.Graveyard.Any(c => c.HasRace(CardRace.Wyrm) && c.HasType(CardType.Normal));
            var revivals = Bot.Graveyard.Where(c => c.Level >= 7 && c.HasRace(CardRace.Wyrm));
            ClientCard monster = revivals.OrderBy(c =>
            {
                if (canReviveNormal && c.IsCode(CardId.建造龙, CardId.幻刃破龙))
                    return c.IsCode(CardId.建造龙) ? -2 : -1;
                int index = Array.IndexOf(priList, c.Id);
                return index < 0 ? int.MaxValue : index;
            }).FirstOrDefault();
            if (monster == null)
                return false;
            int attack = monster.Attack + (Bot.HasInSpellZone(CardId.透幻乡险峻) ? 400 : 0);
            bool defend = Enemy.MonsterZone.Any(c => c != null && c.IsAttack() && !c.Attacked && c.Attack > attack);
            AI.SelectCard(monster);
            AI.SelectPosition(defend ? CardPosition.FaceUpDefence : CardPosition.FaceUpAttack);
            return true;
        }

        private bool 幻刃急攻Effect()
        {
            ClientCard own = Bot.BattlingMonster, enemy = Enemy.BattlingMonster;
            if (own == null || !own.IsFaceup() || !own.HasRace(CardRace.Wyrm) || enemy == null)
                return false;
            ClientCard cost = Bot.Hand.Where(c => c.Level < 7 && c.HasRace(CardRace.Wyrm))
                .OrderByDescending(c => c.Attack).FirstOrDefault();
            if (cost == null || own.GetDefensePower() + cost.Attack < enemy.Attack)
                return false;
            AI.SelectCard(cost);
            return true;
        }

        private bool 幻刃封锁Effect()
        {
            var costs = Bot.MonsterZone.Where(c => c != null && c.IsFaceup() && c.HasRace(CardRace.Wyrm));
            ClientCard cost;
            if (!costs.Any(c => c.Level >= 7))
                cost = costs.OrderBy(c => c.GetDefensePower()).FirstOrDefault();
            else
            {
                cost = Bot.HasInMonstersZone(CardId.建造龙) && HasFaceupFieldSpell()
                    ? costs.Where(c => c.HasType(CardType.Normal)).OrderBy(c => c.Level).FirstOrDefault() : null;
                if (cost == null)
                    cost = costs.Where(c => c.Level < 5).OrderBy(c => c.Attack).FirstOrDefault();
                if (cost == null)
                {
                    int power = costs.Max(c => c.GetDefensePower());
                    int threats = Enemy.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Attack > power);
                    bool hasBackup = (Bot.HasInSpellZone(CardId.幻刃急攻) && Bot.Hand.Any(c => c.Level < 7 && c.HasRace(CardRace.Wyrm)))
                        || (Bot.HasInSpellZone(CardId.幻刃复归) && Bot.Graveyard.Any(c => c.Level >= 7 && c.HasRace(CardRace.Wyrm)));
                    if (threats <= (hasBackup ? 1 : 0))
                        return false;
                    cost = new CardSelector(rePriList).Select(costs.ToList(), 1, 1).First();
                }
            }
            if (cost == null)
                return false;
            AI.SelectCard(cost);
            return true;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.幻刃封锁) && hint == Util.GetStringId(CardId.幻刃封锁, 1))
                    return cards.OrderByDescending(c => Duel.LastSummonedCards.Contains(c)).ThenByDescending(c => c.Attack).Take(max).ToList();
                if (link.IsActivateCode(CardId.阴阳封阵) && hint == Util.GetStringId(CardId.阴阳封阵, 1))
                    return cards.OrderByDescending(c => c.IsAttack() && c.Level >= 7).ThenByDescending(c => c.Attack).Take(max).ToList();
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private bool 阴阳封阵Effect()
        {
            if (!Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsAttack() && c.Level >= 7))
                return false;
            // Keep one heavy Wyrm for revival while other monsters can pay the four-card cost.
            ClientCard reserve = Bot.Graveyard.FirstOrDefault(c => c.IsCode(CardId.幻龙重骑));
            var cost = Bot.Graveyard.Where(c => c.IsMonster()).OrderBy(c =>
            {
                if (c == reserve) return int.MaxValue;
                int rank = Array.IndexOf(priList, c.Id);
                return rank < 0 ? priList.Length : rank;
            }).Take(4).ToList();
            if (cost.Count < 4)
                return false;
            AI.SelectCard(cost);
            return true;
        }

    }
}
