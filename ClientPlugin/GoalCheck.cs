using System;
using System.Collections.Generic;
using System.Linq;
using BX.Lib.Master;
using GetTreasureTicket;
using Save;

namespace ClientPlugin;

class GoalCheck : Ticker {
	static void Unlocked1() {
		var sd = SaveData.Instance;

		int ticketUnlocks = 11;
		foreach (var ttd in sd.GetTeasureTicketData.UsedList)
			if (!APSave.instance.unlocked[int.Parse(MstMemoryManager.CharactorTable.FindByID(ttd).achievementID)])
				ticketUnlocks++;

		if (APSave.instance.unlocked.Count(f => f) + ticketUnlocks < APSlot.instance.goal.unlockUnique)
			return;
		
		currentCheck = Unlocked2;
	}

	static void Unlocked2() {
		var gttm = GetTreasureTicketManager.Instance;
		foreach (var hid in APSlot.instance.goal.unlock) {
			var a = MstMemoryManager.CharactorTable.FindByID(hid.ToString("000")).achievementID;
			if (int.TryParse(a, out int aid) && APSave.instance.unlocked[aid])
				continue;
			if (!gttm.IsUsed(hid.ToString("000")))
				return;
		}

		currentCheck = Created;
	}

	static void Created() {
		var sd = SaveData.Instance;

		HashSet<int> created = [];
		foreach (var c in MstMemoryManager.CharactorTable.All.orderedData) {
			sd.ReadOnlyAchievementScoreData.SpecificTopTreasure.TryGetValue(c.ID, out int create);
			if (create > 0 || (APSlot.instance.live && Checks.cgWeddings > 0 && Checks.cgTreasures?[10] == c.ID))
				created.Add(int.Parse(c.ID));
		}

		if (created.Count < APSlot.instance.goal.createUnique || !APSlot.instance.goal.create.All(created.Contains))
			return;
		
		currentCheck = Checked;
	}

	static void Checked() {
		foreach (var check in APSlot.instance.goal.check)
			if (!APSave.instance.@checked.Contains(check))
				return;
		
		if (APSave.instance.@checked.Count < APSlot.instance.goal.checkUnique)
			return;
		
		currentCheck = Merged;
	}

	static void Merged() {
		foreach (var holo in APSlot.instance.goal.merge)
			if (APSave.instance.merges == null || APSave.instance.merges[holo] == 0)
				return;
		
		if (APSlot.instance.goal.mergeUnique > 0 && (APSave.instance.merges == null ||
			APSave.instance.merges.Count(m => m > 0) < APSlot.instance.goal.mergeUnique))
			return;
		
		currentCheck = null;
	}
	
	static Action currentCheck = Unlocked1;

	public override void Tick() {
		if (currentCheck == null) {
			Plugin.archipelagoSession.SetGoalAchieved();
			Remove();
		} else
			currentCheck();
	}
}