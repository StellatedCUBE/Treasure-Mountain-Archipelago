using SuikaBilliards.UI;
using UnityEngine;

namespace ClientPlugin;

class NoVS : Ticker {
	FocusableButtonUI versus;

    public override void Tick() {
		if (versus) {
			versus.Disable(Color.clear);
			versus.m_submitEvent.Clear();
			versus.m_weakSubmitEvent.Clear();
			versus.m_submitUnityEvent.RemoveAllListeners();
			versus.m_button.m_OnClick.RemoveAllListeners();
		}
		
		else
			versus = GameObject.Find("Versus")?.GetComponent<FocusableButtonUI>();
    }
}