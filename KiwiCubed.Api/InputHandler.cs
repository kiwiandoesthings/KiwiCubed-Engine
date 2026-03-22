namespace KiwiCubed.Api;

using Silk.NET.Input;
using System.Numerics;

public interface IInputHandler {
	public uint RegisterKeyCallback(Key key, Action<Key> callback, bool downOrUp);
	public uint RegisterMouseButtonCallback(MouseButton button, Action<MouseButton> callback, bool downOrUp);
	public uint RegisterScrollCallback(bool directionY, Action<float> callback);
	public void DeregisterCallback(uint id, string instanceID);
	public bool GetKeyState(Key key);
	public bool GetMouseButtonState(MouseButton button);
	public Vector2 GetMousePosition();
	public bool SetMousePosition(Vector2 newMousePosition);
}