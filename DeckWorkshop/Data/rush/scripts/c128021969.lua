-- Light Leading the Valiant
-- Maintained MDPro4 custom-duel support; printed official-duel restriction is unchanged.
local s,id=GetID()
function s.initial_effect(c)
	RD.CannotSpecialSummon(c)
	-- Required three-Tribute procedure; native tribute values and race legality apply.
	local e1=Effect.CreateEffect(c)
	e1:SetProperty(EFFECT_FLAG_CANNOT_DISABLE+EFFECT_FLAG_UNCOPYABLE)
	e1:SetType(EFFECT_TYPE_SINGLE)
	e1:SetCode(EFFECT_LIMIT_SUMMON_PROC)
	e1:SetCondition(RD.SummonProcedureConditionThree(s.tribute_filter))
	e1:SetOperation(RD.SummonProcedureOperationThree(s.tribute_filter))
	e1:SetValue(SUMMON_TYPE_ADVANCE+SUMMON_VALUE_THREE)
	c:RegisterEffect(e1)
	-- Normal Summoning must place this card in Attack Position, never face-down.
	local e2=Effect.CreateEffect(c)
	e2:SetProperty(EFFECT_FLAG_CANNOT_DISABLE+EFFECT_FLAG_UNCOPYABLE)
	e2:SetType(EFFECT_TYPE_SINGLE)
	e2:SetCode(EFFECT_LIMIT_SET_PROC)
	e2:SetCondition(s.set_condition)
	c:RegisterEffect(e2)
	-- The native core checks direct lethal battle damage. This continuous effect
	-- remains negatable; it does not issue a separate or premature Duel.Win.
	local e3=Effect.CreateEffect(c)
	e3:SetType(EFFECT_TYPE_SINGLE)
	e3:SetCode(EFFECT_MATCH_KILL)
	c:RegisterEffect(e3)
end
function s.set_condition(e,c)
	-- Keep the limiting procedure discoverable during the core's nil probe,
	-- then reject every actual Normal Set.
	return c==nil
end
function s.tribute_filter(c)
	return c:IsRace(RACE_FAIRY)
end
