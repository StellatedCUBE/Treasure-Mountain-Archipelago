namespace ClientPlugin;

public abstract class Ticker {
	public bool removalQueued = false;
	public void Remove() => removalQueued = true;

	public abstract void Tick();
}