using System.Collections.Generic;
using BX.Lib.Master.Class;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace ClientPlugin;

static class IconCharacters {
	static Dictionary<string, MstCharactor> map = null;
	public static readonly Dictionary<string, Sprite> icons = [];

	public static Dictionary<string, MstCharactor> Get() {
		if (map == null) {
			map = [];
			Make("AP:base", Plugin.archipelagoIcon);
			Make("AP:filler", Plugin.archipelagoIconGrey);
			Make("AP:prog", Plugin.archipelagoIconGreen);
			Make("AP:trap", Plugin.archipelagoIconRed);
			Make("AP:useful", Plugin.archipelagoIconBlue);
			Make("AP:ticket", Items.ticket);

			/*foreach (var a in Checks.checks) {
				if (a != null) {
					LocalizationSettings.StringDatabase.GetTable(TreasureUI.k_localizeAchievementTable).AddEntry(
						">" + a.Data.ID,
						LocalizationSettings.StringDatabase.GetLocalizedString(TreasureUI.k_localizeAchievementTable, a.Data.ID)
					);
				}
			}*/
		}

		return map;	
	}

	static void Make(string id, Sprite sprite) {
		map.Add(id, new(id, true, 0, id, id, 1, "", "", "", "", ""));
		TreasureAssetsManager.Instance.AssetDic.Add(id, new(sprite, null));
		icons.Add(id, sprite);
	}
}