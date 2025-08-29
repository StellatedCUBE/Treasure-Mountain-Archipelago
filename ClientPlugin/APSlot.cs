using System.Collections.Generic;

namespace ClientPlugin;

#pragma warning disable CS0649
class APSlot {
	public static APSlot instance;

	public string gameId;
	public bool deathLink;
	public bool live;
	public ItemVisibility visibility;
	public Goal goal;

	public class Goal {
		public List<int> create;
		public List<int> unlock;
		public List<long> check;
		public List<int> merge;
		public int createUnique;
		public int unlockUnique;
		public int checkUnique;
		public int mergeUnique;
	}
}