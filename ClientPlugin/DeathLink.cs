using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using BX.Lib.Master;
using BX.Lib.Master.Class;
using SuikaBilliards.Ball;
using UnityEngine;

namespace ClientPlugin;

class DeathLink : Ticker {
	public static bool kill = false;
	public static BallController dropped;
	static DeathLinkService dls;

	public DeathLink() {
		dls = Plugin.archipelagoSession.CreateDeathLinkService();
		dls.OnDeathLinkReceived += _ => kill = true;
	}

    public override void Tick() {
		if (!Plugin.isGameInProgress)
			kill = false;
		else if (kill && !WeddingChecker.isWeddingState && Application.isFocused && !GameObject.Find("GamePauseCanvas/Root")) {
			var g = -Physics.gravity.y * Time.deltaTime;
			foreach (var bc in Component.FindObjectsOfType<BallController>()) {
				if (bc.gameObject.activeInHierarchy && bc.IsAfterShot) {
					var rb = bc.GetComponent<Rigidbody>();
					if (rb.velocity.sqrMagnitude > Physics.gravity.y * Physics.gravity.y)
						continue;
					Vector2 fdir = new(rb.worldCenterOfMass.x, rb.worldCenterOfMass.z);
					if (fdir.sqrMagnitude > 0.4225f)
						continue;
					fdir.Normalize();
					if (fdir.sqrMagnitude <= float.Epsilon)
						fdir = Vector2.up;
					rb.AddForce(fdir.x * g, g * 0.4f, fdir.y * g, ForceMode.VelocityChange);
				}
			}
		}
	}

	public static void MaybeSend() {
		if (!kill && Plugin.isGameInProgress && dls != null)
			dls.SendDeathLink(new(
				Plugin.archipelagoSession.Players.ActivePlayer.Name,
				$"{Plugin.archipelagoSession.Players.ActivePlayer.Name} dropped their {ShortName(Holo(dropped))}"
			));
		
		dropped = null;
	}

	static MstCharactor Holo(BallController ball) {
		if (!ball)
			return null;

		var t = ball.transform;
		while (true) {
			t = t.parent;
			if (!t)
				return null;
			if (t.gameObject.name.EndsWith(" Pool")) {
				var s = t.gameObject.name.Split('_');
				if (s.Length == 3 && int.TryParse(s[1], out int id) && MstMemoryManager.CharactorTable.TryFindByID(id.ToString("000"), out var holo))
					return holo;
				return null;
			}
		}
	}

	static string ShortName(MstCharactor holo) {
		if (holo == null)
			return "treasure";

		switch (int.Parse(holo.ID)) {
			case 2: return "Roboco";
			case 7: return "Haachama";
			case 30: return "La+";
			case 42: return "Iofi";
			case 51: return "Ina";
			case 60: return "Biboo";

			case 6:
			case 41:
			case 44:
			case 47:
			case 48:
			case 49:
				return holo.englishName.Split(' ')[0];
			
			default:
				if ((holo.group & 12288) != 0)
					goto case 6;
				return holo.englishName.Split(' ')[^1];
		}
	}
}