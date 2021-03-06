local manual = ...
if not Common_Skill_Check(attacker, _Skill, manual) then return end

local list = {}
local choose_list = All_target_list()

if not next(choose_list) then return end

local multiple_choose = false
local focus = false
-- [[
if attacker 
and attacker.game 
and attacker.game.attacker_player_count 
and attacker.side == 2 then
	multiple_choose = true
	if _Skill.skill_consume_ep > 0 and attacker.focus_pid > 0 then
		focus = true
	end
end
--]]

table.insert(list, {target = "enemy", button = Check_Button_All(_Skill.skill_type), random = 1, multiple_choose = multiple_choose, focus = focus})

return list;
