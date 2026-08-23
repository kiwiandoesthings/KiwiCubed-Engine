namespace KiwiCubed.Api;

using Silk.NET.Windowing;
using System.Numerics;

public interface IVirtualWindow {
	public bool GetFocused();
	public bool SetFocused(bool focus);
	public Vector2 GetSize();
	public uint GetWidth();
	public uint GetHeight();
	public IWindow GetWindow();
}