using System.Collections.Generic;

namespace ClientPlugin;

class APSave {
	public static APSave instance;
	public static bool loaded = false;
	public static bool dirty = false;

	public string @base = "{}";
	public List<long> @checked = [];
	public uint[] merges;
	public bool[] unlocked;
	public long maxTicket;
}