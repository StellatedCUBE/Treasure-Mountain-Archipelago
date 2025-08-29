using Archipelago.MultiClient.Net;
using TMPro;
using UnityEngine;

namespace ClientPlugin;

class Console : Ticker {
	static TextMeshProUGUI text;
	static string msg = "";
	static float targetWidth = -1f;

	public static void Listen(ArchipelagoSession session) {
		session.MessageLog.OnMessageReceived += m => msg = string.IsNullOrWhiteSpace(m.ToString()) ? msg : $"{m}\n_";
	}

    public override void Tick() {
		var ver = GameObject.Find("VersionText");
		if (ver)
			GameObject.Destroy(ver);
		if (text)
			text.text = Plugin.archipelagoSession.Socket.Connected ? msg : "Disconnected from Archipelago room\n_";
		else if (text = GameObject.Find("Copyright/Root/Text")?.GetComponent<TextMeshProUGUI>()) {
			text.alignment = TextAlignmentOptions.BottomRight;
			var rt = text.GetComponent<RectTransform>();
			if (targetWidth < 0)
				targetWidth = rt.sizeDelta.x - rt.rect.width * 0.05f;
			rt.sizeDelta = new(targetWidth, rt.sizeDelta.y);
		}
	}
}