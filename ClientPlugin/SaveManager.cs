using System.IO;
using Archipelago.MultiClient.Net.Json;
using UnityEngine;

namespace ClientPlugin;

class SaveManager : Ticker {
	static uint dirtyTime = 0;

    public override void Tick() {
		if (APSave.dirty && ++dirtyTime > 64)
			SaveNow();
	}

	public static void SaveNow() {
		if (APSave.loaded) {
			File.WriteAllText(
				Path.Combine(Application.persistentDataPath, $"archipelago-{APSlot.instance.gameId}.json"),
				JObject.FromObject(APSave.instance).ToJSON()
			);
			APSave.dirty = false;
			dirtyTime = 0;
		}
	}
}