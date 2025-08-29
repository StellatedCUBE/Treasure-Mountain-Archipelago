using SuikaBilliards.GameFlow.UI;

namespace ClientPlugin;

class WeddingChecker : Ticker {
	public static bool isWeddingState = false;
	static GameMainLoopUI gmlui = null;

    public override void Tick() {
		if (gmlui) {
			bool @new = gmlui.IsWeddingState();
			if (@new && !isWeddingState)
				Checks.cgWeddings++;
			isWeddingState = @new;
		} else {
			gmlui = GameMainLoopUI.FindObjectOfType<GameMainLoopUI>();
		}
    }
}