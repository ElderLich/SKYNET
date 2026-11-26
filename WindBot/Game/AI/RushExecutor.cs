using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI
{
    public abstract class RushExecutor : DefaultExecutor
    {
        protected static class RushCardId
        {
            public const int 海龙骑士 = 120199032;
            public const int 斗牛士 = 120170035;
            public const int 结界像 = 120226013;
            public const int 终焰魔神 = 120231008;
            public const int 最强战旗 = 120181001;
            public const int 钢机神 = 120155015;
            public const int 破灭之龙魔导士 = 120231049;
            public const int 星战骑佩流安 = 120109014;
            public const int 加百列热茶 = 120151039;
            public const int 武枪 = 120231052;

            public const int 疾风弹 = 120203028;
            public const int HMD = 120130041;
            public const int 死苏 = 120194004;
            public const int 死苏2 = 120195004;
            public const int 死供 = 120151023;
            public const int 强欲之壶 = 120181003;
            public const int 傲慢之壶 = 120238006;
            public const int 黑洞 = 120231069;
            public const int 魔法筒 = 120232005;
            public const int 圣防 = 120198003;
            public const int 激流葬 = 120223105;
            public const int 大灾害 = 120238028;
            public const int 炸甲 = 120194005;
            public const int 万能地雷 = 120234005;
            public const int 万能地雷2 = 120237001;
            public const int 落穴 = 120150019;
            public const int 神鸟攻击 = 120247065;
            public const int 攻击无力化 = 120249065;
            public const int 沙尘防护罩 = 120263038;
            public const int 狡猾 = 120272063;
            public const int 第六感 = 120272064;
            public const int 波纹防护罩 = 120274084;
            public const int 强脱 = 120254032;
            public const int 暗黑释放 = 120105013;
            public const int 亚龙地狱 = 120231068;
            public const int 捕获 = 120231041;

            public const int 人造人 = 120155000;
            public const int 狼人 = 120214001;
            public const int 幻影之龙 = 120223102;
            public const int 手枪龙 = 120208000;
            public const int 伟大魔兽 = 120238019;
            public const int 黑魔术师 = 120130000;
            public const int 千年盾 = 120194001;


            public const int SevensRoadMage = 120130016;
            public const int SparkheartsGirl = 120145014;
            public const int AssaultCannon = 120183031;
        }

        private static int _seed = Environment.TickCount;
        private readonly Random _random = new Random(Interlocked.Increment(ref _seed));

        protected RushExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
        }

        // Dinocarriage uses the normal WindBot option and position callbacks.
        protected virtual bool UseGenericRushChoices { get { return true; } }

        public override int GetBattleTargetPower(ClientCard defender)
        {
            // A set monster has the same unknown power whether the client cached its identity or not.
            return defender.IsFacedown() ? 0 : defender.GetDefensePower();
        }

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            return defender.IsFacedown() || base.OnPreBattleBetween(attacker, defender);
        }

        protected override bool AllRepositionThreatsStronger()
        {
            int ownPower = Util.GetBestPower(Bot);
            bool hasOpponent = false;
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster == null || !monster.IsFaceup()) continue;
                if (monster.GetDefensePower() <= ownPower) return false;
                hasOpponent = true;
            }
            return hasOpponent;
        }

        protected override int StrongestRepositionThreat()
        {
            int strongest = -1;
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster != null && monster.IsFaceup())
                    strongest = Math.Max(strongest, monster.GetDefensePower());
            }
            return strongest;
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override int OnSelectOption(IList<int> options)
        {
            if (!UseGenericRushChoices || options.Count == 0)
                return base.OnSelectOption(options);
            return _random.Next(options.Count);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (!UseGenericRushChoices)
                return base.OnSelectPosition(cardId, positions);
            if (AI.HaveSelectedPosition())
                return 0;
            NamedCard data = NamedCard.Get(cardId);
            if (data == null || data.Attack > 1000)
                return 0;
            CardPosition preferred = data.Attack < 0 ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence;
            return positions.Contains(preferred) ? preferred : 0;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards())
                return null;

            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            int owner = link != null && link.ActivatePlayer == 0 ? SharedRushEffect(link.ActivateId) : 0;
            IList<ClientCard> effectChoice = SelectEffectCards(owner, hint, cards, min, max);
            if (effectChoice != null || Duel.Phase == DuelPhase.BattleStart)
                return effectChoice;

            int limit = Math.Min(max, cards.Count);
            if (limit <= 0)
                return Array.Empty<ClientCard>();
            var selected = limit > 1 ? new List<ClientCard>(limit) : null;
            var used = limit > 1 ? new HashSet<ClientCard>() : null;
            bool material = IsMaterial(hint);
            bool enemyFirst = material || hint == HintMsg.Destroy || hint == HintMsg.Remove ||
                hint == HintMsg.ToGrave || hint == HintMsg.ReturnToHand || hint == HintMsg.ToDeck || hint == HintMsg.Disable;
            bool deckFirst = hint == HintMsg.SpSummon || hint == HintMsg.ToGrave || hint == HintMsg.Remove ||
                hint == HintMsg.AddToHand || hint == HintMsg.FusionMaterial;
            bool fill = hint == HintMsg.SpSummon || hint == HintMsg.ToGrave || hint == HintMsg.AddToHand ||
                hint == HintMsg.FusionMaterial || hint == HintMsg.Equip || hint == HintMsg.Confirm;

            // Most prompts ask for a single card. Scan the offered instances directly.
            for (int pick = 0; pick < limit; ++pick)
            {
                ClientCard best = null;
                int bestPriority = -1;
                int ties = 0;
                for (int i = 0; i < cards.Count; ++i)
                {
                    ClientCard card = cards[i];
                    if (card == null || (pick > 0 && used.Contains(card)))
                        continue;
                    int priority = 0;
                    if (enemyFirst && card.Controller == 1)
                        priority = 4;
                    else if (deckFirst && card.Location == CardLocation.Deck)
                        priority = 3;
                    else if (deckFirst && card.Location == CardLocation.Extra)
                        priority = 2;
                    else if (hint == HintMsg.Equip && card.Controller == 0)
                        priority = 1;
                    if (priority < bestPriority)
                        continue;
                    // Keep the weakest own material. Opponent face-down stats are private.
                    if (priority == bestPriority && material && priority == 0 && best != null)
                    {
                        if (card.Attack >= best.Attack)
                            continue;
                    }
                    else if (priority == bestPriority && _random.Next(++ties) != 0)
                        continue;
                    if (priority != bestPriority)
                        ties = 1;
                    best = card;
                    bestPriority = priority;
                }
                if (best == null || (pick >= min && !fill && bestPriority == 0))
                    break;
                if (limit == 1)
                    return new[] { best };
                selected.Add(best);
                used.Add(best);
            }
            return selected ?? (IList<ClientCard>)Array.Empty<ClientCard>();
        }

        private static bool IsMaterial(int hint)
        {
            return hint == HintMsg.Release || hint == HintMsg.Tribute || hint == HintMsg.FusionMaterial ||
                hint == HintMsg.SynchroMaterial || hint == HintMsg.XyzMaterial || hint == HintMsg.LinkMaterial;
        }


        private static int SharedRushEffect(int printedId)
        {
            // Only equivalent printings verified in the bundled Rush scripts.
            switch (printedId)
            {
                case 120195004:
                case 129022671:
                    return RushCardId.死苏;
                case 120197003:
                    return RushCardId.SevensRoadMage;
                case 120239059:
                case 129018890:
                    return RushCardId.钢机神;
                case 129018567:
                    return RushCardId.破灭之龙魔导士;
                case 129022509:
                    return RushCardId.最强战旗;
                default:
                    return printedId;
            }
        }

        private IList<ClientCard> SelectEffectCards(int owner, int hint, IList<ClientCard> cards, int min, int max)
        {
            if (owner == 0 || cards.Count == 0 || max < 1)
                return null;
            if (owner == RushCardId.星战骑佩流安 && hint == Util.GetStringId(owner, 1))
            {
                // The script's subgroup must contain one face-up monster from each field.
                // The core can offer the two picks together or as select/unselect steps.
                ClientCard own = null, enemy = null;
                foreach (ClientCard card in cards)
                {
                    if (card == null || !card.IsFaceup()) continue;
                    if (card.Controller == 0 && (own == null || card.Attack > own.Attack))
                        own = card;
                    if (card.Controller == 1 && (enemy == null || card.Attack < enemy.Attack))
                        enemy = card;
                }
                if (min == 2 && max >= 2 && own != null && enemy != null)
                    return new[] { own, enemy };
                if (max == 1 && (own != null || enemy != null))
                    return new[] { own ?? enemy };
                return null;
            }

            if (owner == RushCardId.钢机神 && hint == HintMsg.ToDeck)
            {
                // Returning one monster grants piercing. Otherwise each returned level adds 100 ATK.
                int count = 钢机神1Effect() ? Math.Max(1, min) : Math.Min(3, max);
                return cards.OrderByDescending(c => c.Level).Take(count).ToList();
            }
            bool destroy = (owner == RushCardId.死供 || owner == RushCardId.最强战旗 || owner == RushCardId.手枪龙)
                && hint == HintMsg.Destroy;
            bool statTarget = (owner == RushCardId.SevensRoadMage || owner == RushCardId.SparkheartsGirl || owner == RushCardId.AssaultCannon ||
                owner == RushCardId.加百列热茶 || owner == RushCardId.破灭之龙魔导士) && hint == Util.GetStringId(owner, 1);
            if (min > 1 || !(destroy || statTarget || (owner == RushCardId.斗牛士 && hint == HintMsg.Set)
                || (owner == RushCardId.死苏 && hint == HintMsg.SpSummon)))
                return null;

            ClientCard best = null;
            int bestScore = int.MinValue;
            foreach (ClientCard card in cards)
            {
                if (card == null) continue;
                if (destroy && card.Controller == 0) continue;
                int score = 0;
                if (card.Controller == 0 || card.IsFaceup() || card.Location == CardLocation.Grave)
                {
                    if (destroy || owner == RushCardId.AssaultCannon)
                        score = card.GetDefensePower();
                    else if (owner == RushCardId.SevensRoadMage || owner == RushCardId.SparkheartsGirl)
                        score = card.IsAttack() ? card.Attack : -1;
                    else
                        score = card.Attack;
                }
                if (best == null || score > bestScore)
                {
                    best = card;
                    bestScore = score;
                }
            }
            if (best != null)
                return new[] { best };
            return null;
        }

        private static readonly int[] LegendTrapIds =
        {
            RushCardId.圣防, RushCardId.落穴, RushCardId.炸甲,
            RushCardId.魔法筒, RushCardId.万能地雷, RushCardId.万能地雷2,
            RushCardId.大灾害, RushCardId.神鸟攻击, RushCardId.攻击无力化,
            RushCardId.强脱, RushCardId.沙尘防护罩, RushCardId.狡猾,
            RushCardId.第六感, RushCardId.波纹防护罩, RushCardId.激流葬
        };

        private static readonly int[] BattleTrapSuppressors =
        {
            RushCardId.人造人, RushCardId.狼人, RushCardId.武枪,
            RushCardId.幻影之龙
        };

        protected bool 手枪龙Effect()
        {
            // The host asks for the target before tossing the three coins.
            return Enemy.GetMonsterCount() > 0;
        }

        protected bool 星战骑佩流安Effect()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsFaceup()) &&
                Enemy.MonsterZone.Any(c => c != null && c.IsFaceup());
        }

        protected bool 加百列热茶Effect()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsFaceup());
        }

        protected bool 破灭之龙魔导士Effect()
        {
            // The host offers four eligible graveyard cards together for the cost.
            return Bot.MonsterZone.Any(c => c != null && c.IsFaceup());
        }

        protected bool 斗牛士Effect()
        {
            return Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsAttack() && c.Level <= 8);
        }

        protected bool 海龙骑士Effect()
        {
            return Enemy.SpellZone.Any(c => c != null && c.IsFacedown());
        }

        protected bool 耳语妖精Effect()
        {
            // The discard is paid before the opposing graveyard cards are offered.
            return Enemy.Graveyard.Any(c => c.IsMonster());
        }

        protected bool 钢机神1Effect()
        {
            return Enemy.MonsterZone.Any(c => c != null && c.IsDefense()) && !HasFaceupRushThreat(2500);
        }

        protected bool 最强战旗Effect()
        {
            return HasFaceupRushThreat(1900);
        }

        protected bool 最强战旗Eff()
        {
            return HasFaceupRushThreat(2500);
        }

        protected bool 反转Repos()
        {
            if (Card.IsFacedown())
                return true;
            return false;
        }
        protected bool 圣防Repos()
        {

            if (Enemy.GetSpellCountWithoutField() >= 1 && !Bot.HasInMonstersZone(BattleTrapSuppressors) && !Bot.HasDefendingMonster())
            {

                if (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsAttack() && card.Level > 5) >= 2
                    && Enemy.Graveyard.GetMatchingCardsCount(card => (card.Race & (int)CardRace.SpellCaster) > 0) >= 4
                    && Enemy.Graveyard.GetMatchingCardsCount(card => card.IsCode(RushCardId.暗黑释放)) < 3)
                    return true;
                if (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsAttack()
                    && card.Attack >= Enemy.LifePoints) >= 2
                    && Enemy.GetMonsterCount() == 0
                    && Enemy.Graveyard.GetMatchingCardsCount(card => card.IsCode(LegendTrapIds)) == 0)
                    return true;
            }
            return false;
        }
        protected bool 死苏Effect()
        {
            return Enemy.Graveyard.Any(c => c.Level > 6) || Bot.Graveyard.Any(c => c.Level > 6);
        }

        protected bool HMDEffect()
        {
            return Enemy.GetSpellCount()  >= 2;
        }
        protected bool 黑洞Effect()
        {
            return (HasStrongerFaceupEnemy() && Util.GetTotalAttackingMonsterAttack(1) >= 3000 && !Enemy.HasInMonstersZone(RushCardId.终焰魔神, faceUp: true)) || Enemy.GetMonsterCount() == 3;
        }
        protected bool 疾风弹Effect()
        {
            return (Util.GetTotalAttackingMonsterAttack(1) >= 3000 && !Enemy.HasInMonstersZone(RushCardId.终焰魔神, faceUp: true)) || Enemy.GetMonsterCount() == 3 ;
        }
        protected bool 死供Effect()
        {
            return TributeToTheDoomed(1900) && !Enemy.HasInMonstersZone(RushCardId.终焰魔神, faceUp: true);
        }

        protected bool TributeToTheDoomed(int minimumThreat)
        {
            // The script collects one discard, then asks for a monster only at resolution.
            return HasFaceupRushThreat(minimumThreat);
        }

        protected bool HasStrongerFaceupEnemy()
        {
            return HasFaceupRushThreat(Util.GetBestPower(Bot));
        }

        private bool HasFaceupRushThreat(int power)
        {
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster != null && monster.IsFaceup() && monster.GetDefensePower() > power)
                    return true;
            }
            return false;
        }

        protected bool 魔法筒Effect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && (Bot.LifePoints <= attacker.Attack || Enemy.LifePoints <= attacker.Attack ||
                attacker.Attack > 1800) && DefaultUniqueTrap();
        }

        protected bool 圣防Effect()
        {
            if (Util.GetTotalAttackingMonsterAttack(1) >= 1900 && (Enemy.GetMonsters().GetImmuneTrapMonster() == null)) return true;
            if (Util.GetTotalAttackingMonsterAttack(1) - Util.GetTotalAttackingMonsterAttack(0) > Bot.LifePoints && !Bot.HasDefendingMonster()) return true;
            return false;
        }
        protected bool 亚龙地狱Effect()
        {
            int enemySixes = 0;
            foreach (ClientCard monster in Enemy.MonsterZone)
            {
                if (monster == null || !monster.IsFaceup()) continue;
                if (monster.Level >= 7) return true;
                if (monster.Level == 6) enemySixes++;
            }
            return enemySixes > 0 && (enemySixes == 3
                || enemySixes > Bot.MonsterZone.Count(c => c != null && c.IsFaceup() && c.Level == 6));
        }

        protected bool 落穴Effect()
        {
            foreach (ClientCard n in Duel.LastSummonedCards)
            {
                if (n.Attack >= 1900 && !n.IsCode(RushCardId.武枪)) return true;
                if (Util.GetTotalAttackingMonsterAttack(1) - Util.GetTotalAttackingMonsterAttack(0) > Bot.LifePoints && !Bot.HasDefendingMonster() && !n.IsCode(RushCardId.武枪)) return true;
            }
            return false;
        }
        protected bool 激流葬Effect()
            {
                return !Util.HasChainedTrap(0) && Util.IsAllEnemyBetter(true) && (Enemy.GetMonsters().GetImmuneTrapMonster() == null) || Enemy.GetMonsterCount() == 3;
            }

        protected bool 炸甲Effect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && ((attacker.Attack > 2000 && Enemy.GetMonsters().GetImmuneTrapMonster() == null)
                || Bot.LifePoints <= attacker.Attack || (!Util.IsOneEnemyBetterThanValue(1500, true) && Bot.GetSpellCountWithoutField() == 3));
        }

        protected bool RushMonsterSummon()
        {
            if (Card.Level <= 4)
                return true;
            int needed = Card.Level <= 6 ? 1 : 2;
            foreach (ClientCard tribute in Bot.MonsterZone)
            {
                if (tribute != null && tribute.Level <= 4 && tribute.GetDefensePower() < Card.Attack && --needed == 0)
                    return true;
            }
            return false;
        }

        protected bool 伟大魔兽Sum()
        {
            int tributecount = 1;
            int maxAttack = 0;
            for (int k = 0; k < 7; ++k)
            {
                ClientCard tributeCard2 = Bot.MonsterZone[k];
                if (tributeCard2 == null) continue;
                if (tributeCard2.Attack > maxAttack && tributeCard2.Attack > 1600)
                {
                    maxAttack = tributeCard2.Attack;
                }
            }
            for (int k = 0; k < 7; ++k)
            {
                ClientCard tributeCard2 = Bot.MonsterZone[k];
                if (tributeCard2 == null) continue;
                if (tributeCard2.Attack == maxAttack)
                {
                    tributecount--;
                    AI.SelectMaterials(tributeCard2);
                }
            }
            return tributecount <= 0;
        }
    }
}
