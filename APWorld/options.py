from dataclasses import dataclass

from Options import PerGameCommonOptions, StartInventoryPool, DeathLink, DefaultOnToggle, FreeText, Choice

class LiveChecks(DefaultOnToggle):
	"""Send checks as soon as the requirements are met, without waiting for the round to end.
	Does not work for all checks."""
	display_name = "Send Checks Immediately"

class Goal(FreeText):
	"""The goal of the run. This consists of any number of the following structure:

	- An action, one of:
	  - “Create” - Create this treasure at the largest size.
	  - “Unlock” - Receive this treasure from another player, or use a ticket to get it.
	  - “Check” - Complete the check at this treasure (The requirements to unlock it in the base game.)
	  - “Merge” - Merge two of this treasure at the largest size, destroying them.

	- A target, one of:
	  - One or more name(s) of treasure(s).
	  - A number, indicating how many different treasures this needs to be done for.
	  - “all”
	"""
	display_name = "Goal"
	default = "Check all"

class Visibility(Choice):
	"""How much information should be displayed about what each check unlocks before it is reached."""
	display_name = "Visibility Information"
	auto_display_name = False
	option_0 = 0
	option_1 = 1
	option_2 = 2
	option_3 = 3
	option_7 = 7

	@classmethod
	def get_option_name(cls, id_):
		match id_:
			case 0: return "None"
			case 1: return "Progression Class"
			case 2: return "Receiving Player"
			case 3: return "Progression Class + Receiving Player"
			case 7: return "Full Item"

@dataclass
class TMOptions(PerGameCommonOptions):
	goal: Goal
	death_link: DeathLink
	live_checks: LiveChecks
	visibility: Visibility
	start_inventory_from_pool: StartInventoryPool
