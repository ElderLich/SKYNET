using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("SaikyoBattleFlag", "AI_SaikyoBattleFlag")]
    public class SaikyoBattleFlagExecutor : RushExecutor
    {
        public class CardId
        {
            public const int 最强战旗 = 120181001;
            public const int 钢机神 = 120155015;
            public const int 连击龙 = 120110001;
            public const int 恶德 = 120183024;
            public const int 旗兽 = 120193002;
            public const int 社员 = 120183002;

            public const int 成金恐龙王 = 120151010;
            public const int 捕鸟蛛 = 120224010;       
            public const int 突击坦克 = 120183031;
            public const int 工匠无人机 = 120183030;
            public const int 双角兽 = 120181011;
            public const int 瞄准鹰 = 120193003;
            public const int 死苏 = 120194004;            
            public const int 世纪末兽机界 = 120130039;
            public const int 铁之重击 = 120183054;
            public const int 对死者的供奉 = 120151023;
            public const int 七抽 = 120199052;

            public const int 兽之拳 = 120155060;
            public const int 减俸 = 120183062;
            public const int 左迁 = 120183063;
            public const int 冻结 = 120183064;
            public const int 暗黑释放 = 120105013;
        }

        public SaikyoBattleFlagExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, CardId.铁之重击);           
            AddExecutor(ExecutorType.Activate, CardId.成金恐龙王);
            AddExecutor(ExecutorType.Activate, CardId.捕鸟蛛);
            AddExecutor(ExecutorType.Activate, CardId.恶德, 恶德Effect);            
            AddExecutor(ExecutorType.Activate, CardId.最强战旗, 最强战旗Eff);           
            AddExecutor(ExecutorType.Activate, CardId.突击坦克);
            AddExecutor(ExecutorType.Activate, CardId.瞄准鹰, 瞄准鹰Effect);
            AddExecutor(ExecutorType.Activate, CardId.双角兽, 双角兽Effect);
            AddExecutor(ExecutorType.Activate, _CardId.手枪龙, 手枪龙Effect);
            AddExecutor(ExecutorType.Repos, CardId.双角兽, 双角兽summon);
            AddExecutor(ExecutorType.Repos, CardId.瞄准鹰, 瞄准鹰summon);
            AddExecutor(ExecutorType.Repos, CardId.最强战旗, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.钢机神, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, _CardId.手枪龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.恶德, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Summon, CardId.最强战旗, DefaultDoubleSummon);
            AddExecutor(ExecutorType.Summon, CardId.钢机神, DefaultDoubleSummon);
            AddExecutor(ExecutorType.Summon, CardId.恶德, DefaultDoubleSummon);

            AddExecutor(ExecutorType.Repos, CardId.成金恐龙王, 瞄准鹰成金恐龙王summon);
            AddExecutor(ExecutorType.Repos, CardId.突击坦克, 瞄准鹰坦克summon);
            AddExecutor(ExecutorType.Repos, CardId.双角兽, 瞄准鹰双角兽summon);
            AddExecutor(ExecutorType.Summon, CardId.双角兽, 瞄准鹰双角兽summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 瞄准鹰成金恐龙王summon);
            AddExecutor(ExecutorType.Summon, CardId.突击坦克, 瞄准鹰坦克summon);
            AddExecutor(ExecutorType.Summon, CardId.瞄准鹰, 瞄准鹰summon);
            AddExecutor(ExecutorType.Summon, CardId.瞄准鹰, 瞄准鹰summon2);
            AddExecutor(ExecutorType.Summon, CardId.瞄准鹰, 瞄准鹰summon3);

            AddExecutor(ExecutorType.Summon, _CardId.手枪龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.最强战旗, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.钢机神, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.恶德, RushMonsterSummon);

            AddExecutor(ExecutorType.Summon, CardId.双角兽, 双角兽summon);
            AddExecutor(ExecutorType.Summon, CardId.捕鸟蛛, 捕鸟蛛summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 成金恐龙王Summon);

            AddExecutor(ExecutorType.Summon, CardId.最强战旗, DefaultOverSummon);
            AddExecutor(ExecutorType.Summon, CardId.钢机神, DefaultOverSummon);
            AddExecutor(ExecutorType.Activate, CardId.死苏, 死苏effect);
            AddExecutor(ExecutorType.Activate, CardId.最强战旗, 最强战旗Effect);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, 死供Effect);
            AddExecutor(ExecutorType.Activate, CardId.钢机神, 钢机神1Effect);
            AddExecutor(ExecutorType.Activate, CardId.钢机神, 钢机神3Effect);
            AddExecutor(ExecutorType.MonsterSet, CardId.成金恐龙王);            
            AddExecutor(ExecutorType.Activate, CardId.工匠无人机,无人机Effect);
                
            AddExecutor(ExecutorType.MonsterSet, CardId.工匠无人机);
            AddExecutor(ExecutorType.MonsterSet, CardId.突击坦克);
            AddExecutor(ExecutorType.MonsterSet, CardId.捕鸟蛛);
            AddExecutor(ExecutorType.Summon, CardId.工匠无人机, 无人机summon);
            AddExecutor(ExecutorType.Summon, CardId.突击坦克, 突击坦克summon);
            AddExecutor(ExecutorType.MonsterSet, CardId.旗兽);
            AddExecutor(ExecutorType.MonsterSet, CardId.社员);
            AddExecutor(ExecutorType.MonsterSet, CardId.瞄准鹰);
            AddExecutor(ExecutorType.MonsterSet, CardId.双角兽);
            AddExecutor(ExecutorType.Activate, CardId.世纪末兽机界, DefaultField);
            AddExecutor(ExecutorType.Activate, CardId.兽之拳, 兽之拳Effect);
            AddExecutor(ExecutorType.Activate, CardId.左迁, 左迁1Effect); 
            AddExecutor(ExecutorType.Activate, CardId.减俸);
            AddExecutor(ExecutorType.Activate, CardId.左迁);
            AddExecutor(ExecutorType.Activate, CardId.兽之拳);
            AddExecutor(ExecutorType.Activate, CardId.冻结,冻结Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, _CardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, _CardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, _CardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);

            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.七抽, 七抽Effect);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);           
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Activate, CardId.世纪末兽机界, DefaultField2);
            AddExecutor(ExecutorType.Repos, CardId.钢机神, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.最强战旗, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.恶德, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.最强战旗, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.钢机神, 圣防Repos);
        }

        private readonly HashSet<ClientCard> _doubleTributes = new HashSet<ClientCard>();
        private ClientCard _resolvingBicorn;
        private bool _payingEagleCost;

        public override void OnChaining(int player, ClientCard card)
        {
            base.OnChaining(player, card);
            _payingEagleCost = false;
            if (player == 0 && card.IsCode(CardId.双角兽))
                _resolvingBicorn = card;
        }

        public override void OnChainSolved(int chainIndex)
        {
            base.OnChainSolved(chainIndex);
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (_resolvingBicorn != null && link != null && link.RelatedCard == _resolvingBicorn)
            {
                if (!Duel.IsCurrentSolvingChainNegated() && _resolvingBicorn.IsFaceup() &&
                    Bot.MonsterZone.Contains(_resolvingBicorn))
                    _doubleTributes.Add(_resolvingBicorn);
                _resolvingBicorn = null;
            }
        }

        public override void OnChainEnd()
        {
            base.OnChainEnd();
            _payingEagleCost = false;
            _resolvingBicorn = null;
        }

        public override void OnMove(ClientCard card, int previousControler, int previousLocation, int currentControler, int currentLocation)
        {
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
            if (previousControler == 0 && previousLocation == (int)CardLocation.MonsterZone &&
                (currentControler != 0 || currentLocation != (int)CardLocation.MonsterZone))
                ForgetBicorn(card);
        }

        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            base.OnPosChange(card, previousPosition, currentPosition);
            if ((currentPosition & (int)CardPosition.FaceDown) != 0)
                ForgetBicorn(card);
        }

        private void ForgetBicorn(ClientCard card)
        {
            if (_resolvingBicorn == card)
                _resolvingBicorn = null;
            _doubleTributes.Remove(card);
        }

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            _payingEagleCost = false;
            _resolvingBicorn = null;
            _doubleTributes.Clear();
        }

        public override void OnNewPhase()
        {
            base.OnNewPhase();
            _payingEagleCost = false;
        }

        private bool DefaultDoubleSummon()
        {
            // The core supplies each offered tribute's actual weight to GameAI.OnSelectTribute.
            return _doubleTributes.Count > 0;
        }

        private static readonly int[] EagleSummonPriority =
        {
            _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德, CardId.旗兽, CardId.社员
        };

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards())
                return null;
            if (_payingEagleCost && hint == HintMsg.ToGrave && min >= 1 && min <= 2 &&
                cards.All(c => c.Controller == 0 && c.Location == CardLocation.MonsterZone &&
                    c.IsFaceup() && c.HasAttribute(CardAttribute.Light)))
            {
                var materials = cards.OrderBy(c => c.Level > 4).ThenBy(c => c.Attack).Take(min).ToList();
                if (materials.Count == min)
                    return materials;
            }
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0)
            {
                if (link.IsActivateCode(CardId.瞄准鹰) && hint == HintMsg.SpSummon && min == 1 && max == 1)
                {
                    foreach (int id in EagleSummonPriority)
                    {
                        var machine = cards.FirstOrDefault(c => c.Controller == 0 && c.Location == CardLocation.Hand && c.IsCode(id));
                        if (machine != null)
                            return new[] { machine };
                    }
                }
                if (link.IsActivateCode(CardId.左迁) && hint == HintMsg.PosChange && min == 1 && max == 1)
                {
                    ClientCard attacker = Enemy.BattlingMonster;
                    if (attacker != null && attacker.Attack > 2000 && cards.Contains(attacker))
                        return new[] { attacker };
                    var target = cards.Where(c => c.Controller == 1 && c.IsFaceup() && c.IsAttack())
                        .OrderByDescending(c => c.Attack).FirstOrDefault();
                    if (target != null)
                        return new[] { target };
                }
                if (link.IsActivateCode(CardId.恶德) && hint == HintMsg.Set && max > 0)
                {
                    var traps = new List<ClientCard>(2);
                    foreach (int id in TrapPriority)
                    {
                        var trap = cards.FirstOrDefault(c => c.Controller == 0 && c.Location == CardLocation.Grave && c.IsCode(id));
                        if (trap != null && traps.Count < max)
                            traps.Add(trap);
                    }
                    if (traps.Count >= min)
                        return traps;
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private static readonly int[] TrapPriority = { CardId.左迁, CardId.减俸 };

        private bool DefaultOverSummon()
        {
            if (Bot.Hand.Count >= 6 && Bot.HasInMonstersZone(CardId.恶德)) return true;
            else return false;
        }
        private bool 左迁1Effect()
        {
            return Enemy.BattlingMonster != null && Enemy.BattlingMonster.Attack > 2000;
        }

        private bool 冻结Effect()
        {
            foreach (ClientCard monster in Bot.GetMonsters())
            {
                if (monster.IsFacedown())
                {
                    AI.SelectCard(monster);
                    return true;
                }
                else if (monster.IsDefense())
                {
                    AI.SelectCard(monster);
                    return true;
                }
            }
            AI.SelectCard(Bot.MonsterZone.GetLowestAttackMonster());
            return true;
        }
        private bool 钢机神3Effect()
        {
            return Duel.Turn != 1;
        }
        private bool 恶德Effect()
        {
            AI.SelectCard(CardLocation.Hand);
            return true;
        }
        private bool 兽之拳Effect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && attacker.HasRace(CardRace.Dragon | CardRace.Fairy | CardRace.SpellCaster);
        }

        private bool 死苏effect()
        {
            return Bot.Graveyard.Any(IsRevivalTarget) || Enemy.Graveyard.Any(IsRevivalTarget);
        }

        private static bool IsRevivalTarget(ClientCard card)
        {
            return card.IsMonster() && card.Attack >= 2000 && card.IsCanRevive();
        }
        private bool 成金恐龙王Summon()
        {
            if (Enemy.GetHandCount() < 1) return true;
            return false;
        }
        private bool 突击坦克summon()
        {
            if (Bot.MonsterZone.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) == 2 || Util.GetOneEnemyBetterThanMyBest(true, true) == null) return true;
            return false;
        }
        private bool 捕鸟蛛summon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.旗兽, CardId.社员 }) && Enemy.GetSpellCount()  >= 2)
                return true;
            return false;
        }
        private bool 双角兽summon()
        {
            if (Bot.Graveyard.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) >= 3 && Bot.HasInHand(new[] { CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && !Bot.HasInMonstersZone(new[] { CardId.最强战旗, CardId.钢机神 })) 
                return true;
            return false;
        }
        private bool 双角兽Effect()
        {            
            if (Bot.HasInHand(new[] { CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && !Bot.HasInMonstersZone(new[] { CardId.最强战旗, CardId.钢机神 }))
            {
                AI.SelectCard(CardId.瞄准鹰, CardId.捕鸟蛛, CardId.双角兽, CardId.工匠无人机, CardId.突击坦克, CardId.社员, CardId.旗兽, CardId.恶德, CardId.钢机神, CardId.最强战旗);
                return true;
            }
            return false;
        }
        private bool 瞄准鹰双角兽summon()
        {
            if (Bot.HasInMonstersZone(CardId.瞄准鹰) && Bot.HasInHand(new[] { CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                return true;
            return false;
        }
        private bool 瞄准鹰坦克summon()
        {
            if (Bot.HasInMonstersZone(CardId.瞄准鹰) && Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                return true;
            return false;
        }
        private bool 瞄准鹰成金恐龙王summon()
        {
            if (Bot.HasInMonstersZone(CardId.瞄准鹰) && Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                return true;
            return false;
        }
        private bool 瞄准鹰summon()
        {           
          foreach (ClientCard m in Bot.GetMonsters())
               if (m.HasAttribute(CardAttribute.Light) && m.Level <= 4 && m.IsFaceup())
              if (Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }))
             if (Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
              return true;           
            return false;
        }
        private bool 瞄准鹰summon2()
        {
            if (Bot.Hand.GetCardCount(CardId.瞄准鹰) >= 2 )
                if (Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }))
                  if (Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                             return true;           
            return false;
        }
        private bool 瞄准鹰summon3()
        {
                     if (Bot.HasInHand(CardId.双角兽) || Bot.HasInHand(CardId.突击坦克) || Bot.HasInHand(CardId.成金恐龙王))
                if (Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }))
                            if (Enemy.MonsterZone.Any(card => card != null && card.IsFaceup() && card.Level >= 7))
                                return true;
            return false;
        }
        private bool 瞄准鹰Effect()
        {
            _payingEagleCost = Bot.MonsterZone.GetMatchingCardsCount(c => c.IsFaceup() &&
                c.Level <= 4 && c.HasAttribute(CardAttribute.Light)) >= 2 && Bot.HasInHand(EagleSummonPriority);
            return _payingEagleCost;
        }
        private bool 七抽Effect()
        {
            if (!Bot.HasInHand(SevenTreasuresPriority))
                return false;
            AI.SelectCard(SevenTreasuresPriority);
            return true;
        }

        private static readonly int[] SevenTreasuresPriority = { CardId.恶德, CardId.钢机神, CardId.最强战旗, _CardId.手枪龙 };
        private bool 无人机Effect()
        {
            int fodder = Bot.MonsterZone.GetMatchingCardsCount(c => c.Level < 5)
                + Bot.Hand.GetMatchingCardsCount(c => c.IsMonster() && c.Level < 5);
            if (fodder <= 2)
            {
                int bosses = Bot.Hand.GetMatchingCardsCount(c => c.Level > 5);
                if (bosses > 2)
                    AI.SelectCard(CardId.最强战旗, CardId.钢机神, CardId.恶德, _CardId.手枪龙);
                else if (bosses == 1 && fodder == 1)
                    AI.SelectCard(CardId.最强战旗, CardId.钢机神, _CardId.手枪龙, CardId.恶德);
                else
                    return false;
            }
            else
                AI.SelectCard(CardId.瞄准鹰, CardId.突击坦克, CardId.工匠无人机, CardId.社员, CardId.旗兽);
            return true;
        }
        private bool 无人机summon()
        {
            if (Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) >=1 || Util.GetOneEnemyBetterThanMyBest(true, true) == null)
                return true;
            return false;
        }
        
        private bool DefaultField2()
        {
            if (Bot.GetHandCount() >= 2)
            {
                return true;
            }
            return false;
        }

    }
}
