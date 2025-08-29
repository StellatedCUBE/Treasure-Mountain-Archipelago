import random
from enum import Enum

from worlds.AutoWorld import WebWorld, World
from BaseClasses import Region, Tutorial, ItemClassification, Item, Location, LocationProgressType
from Options import OptionError

from .options import TMOptions
from .data import data
from .goal_data import name_to_gid, gid_to_iid, holo_count

GAME = "Hololive Treasure Mountain"

TICKET_NAME = "Treasure Ticket"
TICKET_ID = 99

class TMWebWorld(WebWorld):
	setup_en = Tutorial(
		tutorial_name = "Multiworld Setup Guide",
		description = "A guide to playing Hololive Treasure Mountain with Archipelago.",
		language = "English",
		file_name = "setup_en.md",
		link = "setup/en",
		authors = ["StellatedCUBE"]
	)

	tutorials = [setup_en]
	game_info_languages = ["en"]
	rich_text_options_doc = True
	theme = "ocean"

class TMItem(Item):
	game = GAME

class TMLocation(Location):
	game = GAME

location_name_to_id = {
	loc: id_
	for id_, _, loc, _ in data
}

item_name_to_id = {
	name: id_
	for id_, name, _, _ in data
}

item_name_to_id[TICKET_NAME] = TICKET_ID

def require(reqs, player):
	names = [
		next(name for id2, name, _, _ in data if id_ == id2)
		for id_ in reqs
	]
	return lambda state: all(state.has(name, player) for name in names)

class TMWorld(World):
	"""Stack the Love of Captain Marine! A Simple yet Exciting 3D Physics Puzzle Game!"""

	game = GAME
	web = TMWebWorld()
	options: TMOptions
	options_dataclass = TMOptions
	location_name_to_id = location_name_to_id
	item_name_to_id = item_name_to_id
	
	def create_item(self, name):
		id_ = item_name_to_id[name]
		return TMItem(name, ItemClassification.useful if name == TICKET_NAME else (ItemClassification.progression if id_ in self.needed else ItemClassification.filler), id_, self.player)
	
	def get_filler_item_name(self):
		return TICKET_NAME
	
	def generate_early(self):
		self.g_create = [set(), 0, 'create']
		self.g_unlock = [set(), 0, 'unlock']
		self.g_check = [set(), 0, 'check']
		self.g_merge = [set(), 0, 'merge']

		goal = self.options.goal.value.lower().replace('.', '').replace("'", '').replace('+', ' ').replace(',', ' ').replace(';', ' ').split()

		target = None
		try:
			for token in goal:
				if token == 'create':
					target = self.g_create
				elif token == 'unlock':
					target = self.g_unlock
				elif token == 'check':
					target = self.g_check
				elif token == 'merge':
					target = self.g_merge
				elif token == 'any':
					target[1] = max(target[1], 1)
				elif token == 'all':
					target[1] = len(data) if target is self.g_check else holo_count
				elif token in name_to_gid:
					gid = name_to_gid[token]
					if target is self.g_check:
						if gid in gid_to_iid:
							target[0].add(gid_to_iid[gid])
						else:
							raise OptionError(token[0].upper() + token[1:] + ' is a default treasure; they have no check')
					else:
						target[0].add(gid)
				else:
					target[1] = max(target[1], int(token))
					if target[1] > (len(data) if target is self.g_check else holo_count):
						raise OptionError(f"There aren't even {token} treasures to {target[2]}")
		except OptionError:
			raise
		except ValueError:
			if target is None:
				raise OptionError('Goal must start with one of: "Create", "Unlock", "Check", or "Merge"')
			raise OptionError('Unknown treasure ' + token[0].upper() + token[1:])
		except TypeError:
			raise OptionError('Goal must start with one of: "Create", "Unlock", "Check", or "Merge"')

		self.needed = {
			gid_to_iid[gid]
			for gid in self.g_create[0] | self.g_merge[0]
			if gid in gid_to_iid
		}

		all_ = []
		for id_, _, _, reqs in data:
			self.needed.update(reqs)
			all_.append(id_)

		while len(self.needed) < max(self.g_create[1], self.g_merge[1], self.g_unlock[1]) - (holo_count - len(data)):
			self.needed.add(all_.pop(self.random.randrange(len(all_))))

	def create_regions(self):
		self.region = Region("Menu", self.player, self.multiworld)
		self.multiworld.regions.append(self.region)
		
		for id_, _, name, reqs in data:
			loc = TMLocation(self.player, name, id_, self.region)
			loc.access_rule = require(reqs, self.player)
			self.region.locations.append(loc)
	
	def create_items(self):
		for _, name, _, _ in data:
			self.multiworld.itempool.append(self.create_item(name))
	
	def set_rules(self):
		player = self.player
		locations = [
			location
			for id_, _, location, _ in data
			if id_ in self.needed
		]
		self.multiworld.completion_condition[player] = lambda state: all(state.can_reach_location(l, player) for l in locations)
	
	def fill_slot_data(self):
		game_id = ''
		for _ in range(12):
			game_id += random.choice('1234567890qwertyuiopasdfghjklzxcvbnm')

		return dict(
			gameId = game_id,
			deathLink = self.options.death_link.value,
			live = self.options.live_checks.value,
			visibility = self.options.visibility.value,
			goal = dict(
				create = self.g_create[0],
				createUnique = self.g_create[1],
				unlock = self.g_unlock[0],
				unlockUnique = self.g_unlock[1],
				check = self.g_check[0],
				checkUnique = self.g_check[1],
				merge = self.g_merge[0],
				mergeUnique = self.g_merge[1]
			)
		)
