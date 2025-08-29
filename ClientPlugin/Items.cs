using System.Collections.Generic;
using System.Linq;
using Achievements;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using BX.Lib.Master;
using BX.Lib.Master.Class;
using Cysharp.Threading.Tasks;
using GameFlowControlKit.StateManagement;
using GetTreasureTicket;
using HarmonyLib;
using Il2CppInterop.Runtime;
using SuikaBilliards;
using SuikaBilliards.CommonExtension;
using SuikaBilliards.GameFlow;
using SuikaBilliards.GameFlow.States;
using SuikaBilliards.GameFlow.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace ClientPlugin;

class Items : Ticker {
	public const long TICKET = 99;

	public static readonly Dictionary<string, MstCharactor> achievementToHolo = [];
	public static Dictionary<long, ScoutedItemInfo> scout = null;
	public static readonly Queue<long> incomingHolos = [];
	public static Sprite ticket;
	static long ticketId = TICKET;

	public static void Init() {
		APSave.instance.unlocked ??= new bool[Checks.maxId + 1];

		foreach (var c in MstMemoryManager.CharactorTable.All.orderedData)
			if (!string.IsNullOrEmpty(c.achievementID))
				achievementToHolo[c.achievementID] = c;
	}

	public static void Get(ItemInfo item) {
		Plugin.L("Received " + item.ItemDisplayName);

		var req = "Received from " + item.Player;
		if (scout != null && item.Player.Slot == Plugin.archipelagoSession.Players.ActivePlayer.Slot) {
			try {
				req = LocalizationSettings.StringDatabase.GetLocalizedString(
					TreasureUI.k_localizeAchievementTable,
					scout.Single(
						p => p.Value.Player.Slot == Plugin.archipelagoSession.Players.ActivePlayer.Slot && p.Value.ItemId == item.ItemId
					).Key.ToString("000")
				);
			} catch {}
		}

		if (item.ItemId == TICKET) {
			ticketId++;
			if (ticketId <= APSave.instance.maxTicket)
				return;
			APSave.instance.maxTicket = ticketId;
			GetTreasureTicketManager.Instance.AddTicket(1);
			incomingHolos.Enqueue(ticketId);
			LocalizationSettings.StringDatabase.GetTable(TreasureUI.k_localizeAchievementTable).AddEntry($"<{ticketId}", req);
		}
		
		else if (!APSave.instance.unlocked[item.ItemId]) {
			APSave.instance.unlocked[item.ItemId] = true;
			APSave.dirty = true;
			incomingHolos.Enqueue(item.ItemId);

			LocalizationSettings.StringDatabase.GetTable(TreasureUI.k_localizeAchievementTable).AddEntry($"<{item.ItemId:000}", req);
		}
	}

	static bool isShowingIncomingItems = false;
	static bool CanDisplayIncoming() => Plugin.titleState.IsCurrentState && (isShowingIncomingItems || (int)Plugin.stateMachine.m_stateStatus != 4);

	public Items() {
		Plugin.archipelagoSession.Locations.ScoutLocationsAsync(
			HintCreationPolicy.None,
			[.. Plugin.archipelagoSession.Locations.AllLocations]
		).ContinueWith(scoutData => scout = scoutData.Result);
	}

    public override void Tick() {
		if (scout == null)
			return;

        while (Plugin.archipelagoSession.Items.Any())
			Get(Plugin.archipelagoSession.Items.DequeueItem());
		
		if (CanDisplayIncoming()) {
			if (incomingHolos.Count > 0) {
				if (GetTreasureState<GameTitleState>.s_setDeckIndex > 10)
					GetTreasureState<GameTitleState>.ResetDeckIndex();
					
				isShowingIncomingItems = true;
				AchievementManager.Instance.ComplatedIDList.Add($"<{incomingHolos.Dequeue():000}");

				Plugin.stateMachine.m_playStateIndex = 0;
				while (!Plugin.stateMachine.m_states[Plugin.stateMachine.m_playStateIndex + 1].GetIl2CppType().Name.Contains("GetTreasure"))
					Plugin.stateMachine.m_playStateIndex++;
				if ((int)Plugin.stateMachine.m_stateStatus == 4)
					Plugin.stateMachine.m_playStateIndex++;
				Plugin.stateMachine.JumpToNext();
			} else {
				GetTreasureState<GameTitleState>.ResetDeckIndex();
				isShowingIncomingItems = false;
			}
		}
    }
}