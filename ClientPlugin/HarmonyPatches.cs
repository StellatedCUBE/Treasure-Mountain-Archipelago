using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Achievements;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Json;
using BX.Lib.Master;
using BX.Lib.Master.Class;
using BX.Lib.Master.Tables;
using HarmonyLib;
using Il2CppInterop.Runtime.Runtime;
using LitMotion;
using Save;
using Save.Achievement;
using Save.Handler;
using Save.Utility;
using SuikaBilliards.Ball;
using SuikaBilliards.Ball.UI;
using SuikaBilliards.Entities;
using SuikaBilliards.GameFlow.States;
using SuikaBilliards.Sound;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClientPlugin;

static class HarmonyPatches {
	[HarmonyPatch(typeof(SaveData), "Load")]
	[HarmonyPrefix]
	static void OnSaveLoad(SaveData __instance) {
		if (__instance.m_handler.TryCast<SteamSaveHandler>() == null) {
			Plugin.L("Not using Steam save; transitioning");
			__instance.m_handler = new SteamSaveHandler().Cast<ISaveHandler>();
		}
	}

	[HarmonyPatch(typeof(SteamFileUtility), "GetString")]
	[HarmonyPrefix]
	static bool SteamGet(string key, ref string __result) {
		if (key == "UserData") {
			if (!APSave.loaded) {
				try {
					APSave.instance = JObject.FromJSON(File.ReadAllText(Path.Combine(
						Application.persistentDataPath, $"archipelago-{APSlot.instance.gameId}.json"))).ToObject<APSave>();
				} catch {
					APSave.instance = new();
				}
				APSave.loaded = true;
				Plugin.AddTicker(new SaveManager());
				Checks.SendInitial();
			}

			__result = APSave.instance.@base;
			return false;
		}

		return true;
	}

	[HarmonyPatch(typeof(SteamFileUtility), "SetString")]
	[HarmonyPrefix]
	static bool SteamSet(string key, string text) {
		if (key == "UserData") {
			APSave.instance.@base = text;
			SaveManager.SaveNow();
		}

		return false;
	}

	[HarmonyPatch(typeof(GetTreasureTicket.TicketDistribute), "IsWithinValidityPeriod")]
	[HarmonyPrefix]
	static bool DisableTickets(ref bool __result) => __result = false;

	[HarmonyPatch(typeof(BallDropChecker), "OnTriggerEnter")]
	[HarmonyPrefix]
	static void OnBallDrop(Collider other) {
		if (Plugin.isGameInProgress)
			DeathLink.dropped ??= other.GetComponentInParent<BallController>();
	}

	[HarmonyPatch(typeof(EndlessGameOverState), "ResultAchievement")]
	[HarmonyPostfix]
	static void OnGameEnd() {
		DeathLink.MaybeSend();
		Plugin.isGameInProgress = false;
		AchievementManager.Instance.ComplatedIDList.Clear();
		foreach (var check in Checks.nonLocalChecksThisRun)
			AchievementManager.Instance.ComplatedIDList.Add($">{check:000}");
		Checks.nonLocalChecksThisRun.Clear();
		while (Items.incomingHolos.Count > 0)
			AchievementManager.Instance.ComplatedIDList.Add($"<{Items.incomingHolos.Dequeue():000}");
	}

	static TreasureUI isInTreasureMenu = null;
	[HarmonyPatch(typeof(AchievementData), "get_IsCompleted")]
	[HarmonyPrefix]
	static bool IsHoloUnlocked(AchievementData __instance, ref bool __result) {
		if (isInTreasureMenu && int.TryParse(__instance.ID, out int i)) {
			__result = APSave.instance?.unlocked?[i] ?? false;
			return false;
		} else {
			return true;
		}
	}

	[HarmonyPatch(typeof(TreasureUI), "Awake")]
	[HarmonyPrefix]
	static void InTreasureMenu(TreasureUI __instance) => isInTreasureMenu = __instance;

	[HarmonyPatch(typeof(TreasureUI), "set_CharaID")]
	[HarmonyPostfix]
	static void SetCompleteStatus(TreasureUI __instance) {
		GameObject newText = null;
		RectTransform rt;

		if (long.TryParse(__instance.m_info?.achievementID ?? "", out var location)) {
			bool index = false;
			var t = __instance.transform;
			while (true) {
				t = t.parent;
				if (!t)
					break;
				
				if (t.gameObject.name == "TreasureBooksCanvas(Clone)") {
					index = true;
					break;
				}
			}

			if (__instance.m_proggressText && __instance.m_completeText) {
				if (Checks.Checked(location)) {
					__instance.m_proggressText.enabled = false;
					__instance.m_completeText.enabled = true;
				} else {
					__instance.m_completeText.enabled = false;
					if (__instance.m_proggressText.enabled = SaveData.Instance.TryGetAchievementData(__instance.m_info.achievementID, out var ad))
						__instance.m_proggressText.text = $"{ad.ProgressingValue.Value}/{ad.RequestValue}";
				}

				if (index) {
					newText = GameObject.Find("ArchipelagoText");
					if (newText)
						newText.GetComponent<TextMeshProUGUI>().text = "";
				}

				if (index && APSlot.instance.visibility != ItemVisibility.None && !newText) {
					newText = GameObject.Instantiate(__instance.m_conditionsText.gameObject);
					newText.name = "ArchipelagoText";
					rt = newText.GetComponent<RectTransform>();
					rt.SetParent(__instance.m_completeText.transform.parent);
					rt.localScale = __instance.m_completeText.transform.localScale;
					rt.localPosition = __instance.m_completeText.transform.localPosition + new Vector3(0f, rt.rect.height * 0.2f, 0f);

					Vector3 offset = new(0f, rt.rect.height * 1.3f, 0f);
					__instance.m_completeText.transform.localPosition += offset;
					__instance.m_proggressText.transform.localPosition += offset;
					__instance.m_conditionsText.transform.localPosition += offset;
					__instance.m_acquisitionConditionsText.transform.localPosition += offset;

					rt.sizeDelta = new(rt.sizeDelta.x, rt.sizeDelta.y + rt.rect.height);
				}

				if (newText && Items.scout.TryGetValue(location, out var item)) {
					var visibility = !Checks.hints.TryGetValue(location, out var hint) ? ItemVisibility.Full : APSlot.instance.visibility;

					newText.GetComponent<TextMeshProUGUI>().text =
						visibility.HasFlag(ItemVisibility.Item) && item.Player.Slot == Plugin.archipelagoSession.Players.ActivePlayer.Slot ?
						(item.ItemId == Items.TICKET ? "Grants a Treasure Unlock Ticket" : "Unlocks " + item.ItemDisplayName) :
						$@"Sen{(__instance.m_completeText.enabled ? "t" : "ds")} {(
						visibility.HasFlag(ItemVisibility.Item) ? item.ItemDisplayName :
						visibility.HasFlag(ItemVisibility.Class) ? (
							item.Flags.HasFlag(ItemFlags.Advancement) ? (
								item.Flags.HasFlag(ItemFlags.NeverExclude) ? "a useful progression item" : "a progression item"
							) : item.Flags.HasFlag(ItemFlags.NeverExclude) ? "a useful item" :
							item.Flags.HasFlag(ItemFlags.Trap) ? "a trap" : "a filler item"
						) : "an item"
					)} to {(visibility.HasFlag(ItemVisibility.Slot) ? item.Player.Name : "someone")}{(
						hint != null && !__instance.m_completeText.enabled && (
							hint.Status == HintStatus.Priority ||
							hint.Status == HintStatus.NoPriority ||
							(hint.Status == HintStatus.Avoid && item.Flags != ItemFlags.Trap)
						) ? ".\nThey have " + (hint.Status) switch {
							HintStatus.Priority => "marked it as a priority.",
							HintStatus.NoPriority => "marked it as not a priority.",
							HintStatus.Avoid => "requested it be avoided.",
							_ => null
						} : visibility.HasFlag(ItemVisibility.Item) ? ", which is a " + (
							item.Flags.HasFlag(ItemFlags.Advancement) ? (
								item.Flags.HasFlag(ItemFlags.NeverExclude) ? "useful progression item" : "progression item"
							) : item.Flags.HasFlag(ItemFlags.NeverExclude) ? "useful item" :
							item.Flags.HasFlag(ItemFlags.Trap) ? "trap" : "filler item"
						) : ""
					)}";
				}

				return;
			}

			if (!index || Items.scout == null || !Items.scout.TryGetValue(location, out var scout))
				return;

			GameObject iconGo = new();
			iconGo.transform.SetParent(__instance.transform.Find("FocusableButton/Root"), false);
			IconCharacters.Get();
			iconGo.AddComponent<Image>().sprite = APSlot.instance.visibility.HasFlag(ItemVisibility.Class) ?
				IconCharacters.icons[Checks.Icon(scout.Flags)] : Plugin.archipelagoIcon;
			rt = iconGo.GetComponent<RectTransform>();
			rt.anchorMax = rt.anchorMin = new(0.75f, 0.25f);
			rt.sizeDelta = rt.parent.GetComponent<RectTransform>().rect.size * 0.3f;

			bool @checked = Checks.Checked(location);
			if (@checked || Plugin.archipelagoSession.Locations.AllLocationsChecked.Contains(location)) {
				GameObject checkGo = new();
				checkGo.transform.SetParent(iconGo.transform, false);
				checkGo.AddComponent<Image>().sprite = @checked ? Checks.mark : Checks.collected;
				rt = checkGo.GetComponent<RectTransform>();
				rt.anchorMin = new(0f, 0.1f);
				rt.anchorMax = new(1f, 1.1f);
				rt.sizeDelta = new();
			}

			return;
		}

		else if (__instance.m_proggressText && __instance.m_completeText && (newText = GameObject.Find("ArchipelagoText")))
			newText.GetComponent<TextMeshProUGUI>().text = "";
	}

	[HarmonyPatch(typeof(SoundIdHelper), "get_SePuzzleCombine")]
	[HarmonyPrefix]
	static void MergeSmall() => Checks.cgCombines++;

	[HarmonyPatch(typeof(SoundIdHelper), "get_SePuzzleDoubleVanish")]
	[HarmonyPrefix]
	static void MergeBig() {
		APSave.instance.merges ??= new uint[MstMemoryManager.CharactorTable.All.orderedData.Max(c => int.Parse(c.ID)) + 1];
		APSave.instance.merges[
			int.Parse(
				new AchievementManager.ResultInfo(
					AchievementManager.Instance.MadeSpecificTopTreasureInfo,
					0, 0, 0, 0,
					SaveData.Instance.BallFacialIndexList,
					AchievementManager.ResultInfo.GameMode.Endless
				).DeckList[10]
			)
		]++;
		MergeSmall();
	}

	[HarmonyPatch(typeof(AchievementManager), "OnCompleted")]
	[HarmonyPostfix]
	static void OnAchievement(AchievementManager __instance) {
		var last = __instance.ComplatedIDList[^1];
		if (long.TryParse(last, out var id))
			Checks.Check(id);
	}

	[HarmonyPatch(typeof(InGameCharacterUI), "PlayStart")]
	[HarmonyPrefix]
	static void OnGameStart() {
		Plugin.isGameInProgress = true;
		DeathLink.kill = false;
	}

	[HarmonyPatch(typeof(MstAchievementTable), "FindByID")]
	[HarmonyPrefix]
	static bool InterceptAchievement(ref MstAchievement __result, string key) {
		if (string.IsNullOrEmpty(key))
			return true;
		
		if (key[0] == '<') {
			__result = new(key, key[1] == '0' ? Items.achievementToHolo[key[1..]].ID : "AP:ticket", null, null, 0, null, 0, null);
			return false;
		}

		if (key[0] == '>') {
			ItemFlags flags = default;
			if (Items.scout != null && Items.scout.TryGetValue(long.Parse(key[1..]), out var scout))
				flags = scout.Flags;
			__result = new(key, Checks.Icon(flags), null, null, 0, null, 0, null);
			return false;
		}

		return true;
	}

	[HarmonyPatch(typeof(MstCharactorTable), "FindByID")]
	[HarmonyPrefix]
	static bool InterceptHolo(ref MstCharactor __result, string key) => !key.StartsWith("AP:") || !IconCharacters.Get().TryGetValue(key, out __result);

	[HarmonyPatch(typeof(SaveData), "get_BallFacialIndexList")]
	[HarmonyPostfix]
	static void MaybeIncludeN1(ref Il2CppSystem.Collections.Generic.IReadOnlyList<int> __result) {
		if (Checks.InTreasureGetUI) {
			Il2CppSystem.Collections.Generic.List<int> @new = new(__result.Cast<Il2CppSystem.Collections.Generic.IEnumerable<int>>());
			@new.Add(-1);
			__result = @new.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<int>>();
		}
	}

	[HarmonyPatch(typeof(TreasureGetProgressUI), "CalcProggress")]
	[HarmonyPatch(typeof(TreasureGetProgressUI), "Increment")]
	[HarmonyPostfix]
	static void Progress(TreasureGetProgressUI __instance) {
		var t = __instance.transform;
		while (true) {
			t = t.parent;
			if (!t)
				return;
			if (t.gameObject.name == "TreasureBooksCanvas(Clone)")
				break;
		}

		Checks.SendHints();

		var rt = __instance.GetComponent<RectTransform>();
		rt.sizeDelta = new(rt.parent.GetComponent<RectTransform>().rect.width, rt.sizeDelta.y);
		__instance.m_proggressText.text =
			$"Holos: {__instance.m_proggressText.text}     Checks: {APSave.instance.@checked.Count}/{Plugin.archipelagoSession.Locations.AllLocations.Count}";
	}
}