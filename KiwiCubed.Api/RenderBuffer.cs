using Silk.NET.OpenGL;

namespace KiwiCubed.Api;

public interface IRenderBuffer {
	public void BindArrayObject();
	public void BindVertexBuffer();
	public void BindIndexBuffer();

	public void LinkAttribute(uint layout, int componentCount, VertexAttribPointerType type, uint stride, int offset);
}