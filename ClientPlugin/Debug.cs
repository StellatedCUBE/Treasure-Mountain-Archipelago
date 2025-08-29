#if DEBUG
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Achievements;
using Achievements.Data;
using Achievements.Data.Achievement;
using Archipelago.MultiClient.Net.Json;
using BX.Lib.Master;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Linq;
using SuikaBilliards.Ball;
using SuikaBilliards.GameFlow;
using SuikaBilliards.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace ClientPlugin;

class Debug : Ticker {
	int i;
	Harmony h = new("DBG");
	//Queue<Sprite> iconTest;

    public override void Tick() {
		if ((i = i + 1 & 15) > 0)
			return;

        if (Cmd("dumptree")) {
			Plugin.L("TREE DUMP START");
			foreach (var t in Component.FindObjectsOfType<Transform>())
				if (!t.parent)
					PGO(t.gameObject);
		}

		if (Cmd("dumplist")) {
			Plugin.L("LIST DUMP START");
			foreach (var t in Component.FindObjectsOfType<Transform>())
				if (!t.parent)
					DL(t.gameObject);
		}

		var acm = AchievementManager.Instance;
		if (Cmd("result")) {
			/*AchievementManager.ResultInfo ri = new(
				acm.MadeSpecificTopTreasureInfo,
				0,
				acm.Combine,
				acm.MaxChain,
				acm.TotalChain,
				Save.SaveData.Instance.BallFacialIndexList,
				AchievementManager.ResultInfo.GameMode.Endless
			);
			Plugin.L(ri._DeckList_k__BackingField == null);
			List<string> l2 = [];
			foreach (var s in ri._DeckList_k__BackingField)
				l2.Add(s);
				Plugin.L($"[{string.Join(", ", l2)}]");
			acm.OnResult.Invoke(ri);*/
			int i = 0;
			while (true) {
				IAchievement a;
				try {
					a = acm.Achievements[i++];
				} catch {
					break;
				}

				var d = a.Cast<AchievementBase>().Data;
				List<string> x = [];
				foreach (var f in a.Cast<AchievementBase>().GetIl2CppType().GetFields(Il2CppSystem.Reflection.BindingFlags.Instance | Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic)) {
					if (!f.FieldType.IsGenericType && f.FieldType.Name.EndsWith("String")) {
						x.Add(new Il2CppSystem.String(f.GetValue(a.Cast<AchievementBase>()).Pointer));
					}
				}
				if (TryCast<AchievementOfDeckSpecificTopTreasure>(a, out var n1))
					foreach (var s in n1.m_treasureIds)
						x.Add(s);
				else if (TryCast<AchievementOfDeckTotalCombine>(a, out var n2))
					foreach (var s in n2.m_treasureIds)
						x.Add(s);
				else if (TryCast<AchievementOfMadeSpecificTop38Treasure>(a, out var n3))
					foreach (var s in n3.m_treasureIds)
						x.Add(s);
				else if (TryCast<AchievementOfDeckTotalScore>(a, out var n4))
					foreach (var s in n4.m_treasureIds)
						x.Add(s);
				else if (TryCast<AchievementOfDeckTotalChain>(a, out var n5))
					foreach (var s in n5.m_treasureIds)
						x.Add(s);
				else if (TryCast<AchievementOfDeckBestScore>(a, out var n6))
					foreach (var s in n6.m_treasureIds)
						x.Add(s);
				else if (TryCast<AchievementOfDeckBestChain>(a, out var n7))
					foreach (var s in n7.m_treasureIds)
						x.Add(s);
				else if (TryCast<AchievementOfConsecutiveBestChain>(a, out var n8))
					foreach (var s in n8.m_treasureIds)
						x.Add(s);
				
				Plugin.L($"A) Type: {a.Cast<Il2CppSystem.Object>().GetIl2CppType().FullName} ID: {d.ID} Got: {d.IsCompleted} N1: {d.RequestValue} N2: {d.RequestValue2} P1: {d.ProgressingValue?.Value} P2: {d.ProgressingValue2?.Value} X: [{string.Join(", ", x)}]");
			}

			foreach (var c in MstMemoryManager.CharactorTable.All.orderedData) {
				Plugin.L($"C) ID: {c.ID} Name: {c.Name} ({c.englishName}) Sprite: {c.spriteKey} ({c.holoNatsuSpriteKey}) Voice: {c.voiceID} Pf: {c.prefabKey} Achievement: {c.achievementID} Group: {c.group}");
			}
		}

		/*if (iconTest != null && iconTest.Count > 0 && !Component.FindObjectOfType<AchievementHandlerUI>().busyShowing) {
			var icon = iconTest.Dequeue();
			PopupHandler.Popup("Icon Test", "icon test", icon);
		}

		List<string> l = [];
		foreach (var s in AchievementManager.Instance.ComplatedIDList)
			l.Add(s);
		Plugin.L($"[{string.Join(", ", l)}]");*/

		if (Cmd("ezgame"))
			h.Patch(typeof(BallSpawner).GetMethods().First(m => m.Name == "GetSpawnIndex" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(int)), prefix: new(typeof(Debug).GetMethod("EZGame")));
		
		if (Cmd("hardgame"))
			h.UnpatchSelf();
		
		if (Cmd("death")) {
			DeathLink.kill = true;
			Plugin.AddTickerUniqueType(new DeathLink());
		}

		if (Cmd("apdata")) {
			StringBuilder data = new();
			/*data.AppendLine("data: tuple[int, str, str, list[int]] = [");

			foreach (var c in MstMemoryManager.CharactorTable.All.orderedData) {
				if (int.TryParse(c.achievementID ?? "", out int id)) {
					Plugin.L(c.achievementID);
					var a = AchievementManager.Instance.GetInGameAchievement(c.achievementID).Cast<AchievementBase>();
					List<string> x = [];
					foreach (var f in a.GetIl2CppType().GetFields(Il2CppSystem.Reflection.BindingFlags.Instance | Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic)) {
						if (!f.FieldType.IsGenericType && f.FieldType.Name.EndsWith("String")) {
							x.Add(new Il2CppSystem.String(f.GetValue(a).Pointer));
						}
					}
					if (TryCast<AchievementOfDeckSpecificTopTreasure>(a, out var n1))
						foreach (var s in n1.m_treasureIds)
							x.Add(s);
					else if (TryCast<AchievementOfDeckTotalCombine>(a, out var n2))
						foreach (var s in n2.m_treasureIds)
							x.Add(s);
					else if (TryCast<AchievementOfMadeSpecificTop38Treasure>(a, out var n3))
						foreach (var s in n3.m_treasureIds)
							x.Add(s);
					else if (TryCast<AchievementOfDeckTotalScore>(a, out var n4))
						foreach (var s in n4.m_treasureIds)
							x.Add(s);
					else if (TryCast<AchievementOfDeckTotalChain>(a, out var n5))
						foreach (var s in n5.m_treasureIds)
							x.Add(s);
					else if (TryCast<AchievementOfDeckBestScore>(a, out var n6))
						foreach (var s in n6.m_treasureIds)
							x.Add(s);
					else if (TryCast<AchievementOfDeckBestChain>(a, out var n7))
						foreach (var s in n7.m_treasureIds)
							x.Add(s);
					else if (TryCast<AchievementOfConsecutiveBestChain>(a, out var n8))
						foreach (var s in n8.m_treasureIds)
							x.Add(s);

					var d = a.Data;
					data.AppendLine($"\t({id}, \"{(c.englishName == "Robocosan" ? "Roboco" : c.englishName)}\", {JObject.FromObject(LocalizationSettings.StringDatabase.GetLocalizedString(TreasureUI.k_localizeAchievementTable, d.m_id)).ToJSON()}, [{string.Join(", ", x.Where(c => !string.IsNullOrEmpty(c)).Select(c => MstMemoryManager.CharactorTable.FindByID(c).achievementID).Where(a => !string.IsNullOrEmpty(a)).Select(a => a.TrimStart('0')))}]),");
				}
			}*/

			data.AppendLine("data: tuple[int, int, list[str]] = [");

			foreach (var c in MstMemoryManager.CharactorTable.All.orderedData) {
				int.TryParse(c.achievementID ?? "", out int aid);
				var names = c.englishName.ToLowerInvariant().Split(' ').ToList();
				if (c.ID == "002")
					names.Add("roboco");
				else if (c.ID == "007")
					names.Add("haachama");
				data.AppendLine($"\t({c.ID.TrimStart('0')}, {aid}, [\"{string.Join("\", \"", names)}\"]),");
			}

			data.Append(']');
			File.WriteAllText("z:\\tmp\\goal_data.py", data.ToString());
		}
    }

	public static bool EZGame(ref int __result) {
		__result = 8;
		return false;
	}

	static bool Cmd(string cmd) {
		if (File.Exists("z:\\tmp\\" + cmd)) {
			File.Delete("z:\\tmp\\" + cmd);
			return true;
		}
		return false;
	}

	static void PGO(GameObject go, int depth = 0) {
		string pf = new(' ', depth * 2);
		Plugin.L($"{pf}{go.name} ({go.activeSelf})");
		foreach (var c in go.GetComponents<TMP_Text>())
			try { Plugin.L($"{pf}\"{c.text.Replace("\n", "\\n")}\""); }
			catch {}
		foreach (var c in go.GetComponents<Component>())
			try {
				string enabled = "Unknown";
				var f = c.GetIl2CppType().GetProperty("enabled");
				if (f?.PropertyType == Il2CppType.Of<bool>())
					enabled = f.GetValue(c).ToString();
				Plugin.L($"{pf}.{c.GetIl2CppType().Name} ({enabled})");
			}
			catch {}
		for (int i = 0; i < go.transform.GetChildCount(); i++)
			PGO(go.transform.GetChild(i).gameObject, depth + 1);
	}

	static void DL(GameObject go, string pf = "/") {
		Plugin.L(pf + go.name);
		for (int i = 0; i < go.transform.GetChildCount(); i++)
		DL(go.transform.GetChild(i).gameObject, pf + go.name + "/");

	}

	static bool TryCast<T>(Il2CppObjectBase obj, out T cast) where T: Il2CppObjectBase => (cast = obj.TryCast<T>()) != null;
}

#endif