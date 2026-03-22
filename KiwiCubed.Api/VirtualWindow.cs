namespace KiwiCubed.Api;

using Silk.NET.Windowing;

public interface IVirtualWindow {
	public bool GetFocused();
	public bool SetFocused(bool focus);
	public uint GetWidth();
	public uint GetHeight();
	public IWindow GetWindow();
}