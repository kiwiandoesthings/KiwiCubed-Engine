using System.Numerics;
using Silk.NET.OpenGL;

namespace KiwiCubed.Api;

public interface IShader {
	public int GetUniformLocation(string name);
	public void SetInt(string name, int value);
	public void SetUInt(string name, uint value);
	public void SetFloat(string name, float value);
	public void SetVector2(string name, Vector2 value);
	public void SetVector3(string name, Vector3 value);
	public void SetVector4(string name, Vector4 value);
	public void SetMatrix4(string name, Matrix4x4 value);
	public void Bind();
	public void Unbind();
}