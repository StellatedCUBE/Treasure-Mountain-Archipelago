using System;

namespace ClientPlugin;

[Flags]
enum ItemVisibility {
	None = 0,
	Class = 1,
	Slot = 2,
	Item = 4,
	Full = 7
}