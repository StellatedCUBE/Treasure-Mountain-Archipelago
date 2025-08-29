using System;
using System.Collections.Generic;
using System.Linq;
using Achievements;
using Achievements.Data;
using Achievements.Data.Achievement;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using Il2CppInterop.Runtime.InteropTypes;
using Save;
using SuikaBilliards.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace ClientPlugin;

class Checks : Ticker {
	public static int maxId;
	public static Sprite mark, collected;
	public static readonly List<long> nonLocalChecksThisRun = [];
	public static AchievementBase[] checks;
	public static readonly Dictionary<long, Hint> hints = [];
	static bool hinted = false;
	static int checkI = 0;

	public static void Init() {
		var raw = AchievementManager.Instance.Achievements;
		List<AchievementBase> data = [];
		int i = 0;
		while (true) {
			IAchievement a;
			try {
				a = raw[i++];
			} catch {
				break;
			}
			data.Add(a.Cast<AchievementBase>());
		}

		maxId = data.Where(a => a.Data.ID.Length == 3).Select(a => int.Parse(a.Data.ID)).Max();
		checks = new AchievementBase[maxId + 1];

		foreach (var a in data)
			if (int.TryParse(a.Data.ID, out int id))
				checks[id] = a;
		
		int me = Plugin.archipelagoSession.Players.ActivePlayer.Slot;
		Plugin.archipelagoSession.DataStorage.TrackHints(hh => {
			foreach (var h in hh)
				if (h.FindingPlayer == me)
					hints[h.LocationId] = h;
		});
		
		Items.Init();
	}

	public static void SendInitial() =>
		Plugin.archipelagoSession.Locations.CompleteLocationChecks([.. APSave.instance.@checked]);
	
	public static void SendHints() {
		if (!hinted && APSlot.instance.visibility.HasFlag(ItemVisibility.Item)) {
			hinted = true;
			Plugin.archipelagoSession.Locations.ScoutLocationsAsync(
				HintCreationPolicy.CreateAndAnnounceOnce,
				[.. Plugin.archipelagoSession.Locations.AllMissingLocations]
			);
		}
	}

	public static bool LiveCheck() {
		var acm = AchievementManager.Instance;
		int score = Component.FindObjectOfType<GameFlowController>().EndlessGameMode.ResultScore.InternalDecrypt();
		cgTreasures ??= [.. new AchievementManager.ResultInfo(acm.MadeSpecificTopTreasureInfo, 0, 0, 0, 0, SaveData.Instance.BallFacialIndexList, AchievementManager.ResultInfo.GameMode.Endless).DeckList];
		var largestTreasure = cgTreasures[10];
		
		checkI = (checkI + 1) % checks.Length;
		var a = checks[checkI];

		if (a == null)
			return false;
		
		if (Is<AchievementOfTotalScore>(a)) {
			if (score + a.Data.ProgressingValue.Value >= a.Data.RequestValue)
				return true;
		}
		
		else if (Is<AchievementOfChain>(a)) {
			if (acm.MaxChain >= a.Data.RequestValue)
				return true;
		}

		else if (Is<AchievementOfMadeWatermelon>(a)) {
			if (cgWeddings + a.Data.ProgressingValue.Value >= a.Data.RequestValue)
				return true;
		}

		else if (Is<AchievementOfBestScore>(a)) {
			if (score >= a.Data.RequestValue)
				return true;
		}

		else if (TryCast<AchievementOfMadeSpecificTopTreasure>(a, out var aomstt)) {
			if (aomstt.m_treasureId == largestTreasure && cgWeddings + a.Data.ProgressingValue.Value >= a.Data.RequestValue)
				return true;
		}

		else if (Is<AchievementOfTotalCombine>(a)) {
			if (cgCombines + a.Data.ProgressingValue.Value >= a.Data.RequestValue)
				return true;
		}

		else if (TryCast<AchievementOfDeckBestChain>(a, out var aodbc)) {
			if (acm.MaxChain >= a.Data.RequestValue) {
				bool flag = true;
				foreach (var t in aodbc.m_treasureIds) {
					if (!cgTreasures.Contains(t)) {
						flag = false;
						break;
					}
				}
				if (flag)
					return true;
			}
		}

		else if (TryCast<AchievementOfDeckTotalCombine>(a, out var aodtc)) {
			if (cgCombines + a.Data.ProgressingValue.Value >= a.Data.RequestValue) {
				bool flag = true;
				foreach (var t in aodtc.m_treasureIds) {
					if (!cgTreasures.Contains(t)) {
						flag = false;
						break;
					}
				}
				if (flag)
					return true;
			}
		}

		else if (TryCast<AchievementOfDeckTotalScore>(a, out var aodts)) {
			if (score + a.Data.ProgressingValue.Value >= a.Data.RequestValue) {
				bool flag = true;
				foreach (var t in aodts.m_treasureIds) {
					if (!cgTreasures.Contains(t)) {
						flag = false;
						break;
					}
				}
				if (flag)
					return true;
			}
		}

		else if (TryCast<AchievementOfDeckSpecificTopTreasure>(a, out var aodstt)) {
			if (cgWeddings + a.Data.ProgressingValue.Value >= a.Data.RequestValue) {
				bool flag = true;
				foreach (var t in aodstt.m_treasureIds) {
					if (!cgTreasures.Contains(t)) {
						flag = false;
						break;
					}
				}
				if (flag)
					return true;
			}
		}

		else if (TryCast<AchievementOfDeckBestScore>(a, out var aodbs)) {
			if (score >= a.Data.RequestValue) {
				bool flag = true;
				foreach (var t in aodbs.m_treasureIds) {
					if (!cgTreasures.Contains(t)) {
						flag = false;
						break;
					}
				}
				if (flag)
					return true;
			}
		}
		
		return false;
	}

	public static void Check(long location) {
		if (!APSave.instance.@checked.Contains(location)) {
			try { Plugin.archipelagoSession.Locations.CompleteLocationChecks(location); }
			catch (ArchipelagoSocketClosedException) {}

			APSave.instance.@checked.Add(location);
			APSave.dirty = true;

			Plugin.L($"Sent check {location}");

			if (Items.scout?[location].Player?.Slot == Plugin.archipelagoSession.Players.ActivePlayer.Slot)
				Items.Get(Items.scout[location]);
			else
				nonLocalChecksThisRun.Add(location);
		}
	}

	public static bool Checked(long location) => APSave.instance.@checked.Contains(location);

	public static string Icon(ItemFlags flags) {
		if (flags.HasFlag(ItemFlags.Advancement))
			return "AP:prog";
		if (flags.HasFlag(ItemFlags.Trap))
			return "AP:trap";
		if (flags.HasFlag(ItemFlags.NeverExclude))
			return "AP:useful";
		return "AP:filler";
	}

	static bool TryCast<T>(Il2CppObjectBase obj, out T cast) where T: Il2CppObjectBase => (cast = obj.TryCast<T>()) != null;
	static bool Is<T>(Il2CppObjectBase obj) where T: Il2CppObjectBase => obj.TryCast<T>() != null;

	public static int cgWeddings, cgCombines;
	public static string[] cgTreasures;
	static InGameGetTreasureUI iggtui;

	public static bool InTreasureGetUI => iggtui && iggtui.gameObject.activeInHierarchy;

    public override void Tick() {
		if (!APSave.loaded)
			return;
		
		if (checks == null)
			Init();

        if (Plugin.isGameInProgress && APSlot.instance.live) {
			if (LiveCheck())
				Check(checkI);
		} else {
			cgWeddings = 0;
			cgCombines = 0;
			cgTreasures = null;

			if (iggtui) {
				if (iggtui.m_treasureName && iggtui.m_treasureName.text.Contains("AP:")) {
					var achievementID = iggtui.m_treasureUnlockDescription.text;
					int i = achievementID.IndexOf('>');
					bool ticket = i < 0;
					if (!ticket) {
						achievementID = achievementID.Substring(i + 1, 3);
						iggtui.m_treasureUnlockDescription.text = LocalizationSettings.StringDatabase.GetLocalizedString(
							TreasureUI.k_localizeAchievementTable, achievementID);
					}
					if (ticket)
						iggtui.m_group.text = iggtui.m_treasureName.text = "Treasure Unlock Ticket";
					else if (Items.scout != null && long.TryParse(achievementID, out var id) && Items.scout.TryGetValue(id, out var scout)) {
						iggtui.m_treasureName.text = $"Sent {scout.ItemDisplayName} to {scout.Player}";
						iggtui.m_group.text = scout.ItemGame == "Generic" ? "Archipelago" : scout.ItemGame;
					} else
						iggtui.m_group.text = iggtui.m_treasureName.text = "Archipelago";
				}
			} else {
				iggtui = Component.FindObjectOfType<InGameGetTreasureUI>();
			}
		}
    }
}