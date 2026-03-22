namespace KiwiCubed.Engine;

using System.Numerics;
using System.Runtime.InteropServices;
using KiwiCubed.Api;
using Silk.NET.OpenGL;

public class RendererWrapper : IRenderer {
	public void UpdateBuffers(IRenderBuffer renderBuffer, List<float> vertices, List<ushort> indices) => Renderer.UpdateBuffers((RenderBuffer)renderBuffer, vertices, indices);
	public void DrawElements(IRenderBuffer renderBuffer, int indicesCount) => Renderer.DrawElements((RenderBuffer)renderBuffer, indicesCount);
}

public static class Renderer {
	private static GL gl = SystemsManager.Get<GL>();

	public unsafe static void DrawElements(VertexArrayObject vertexArrayObject, int indicesCount) {
		vertexArrayObject.Bind();
		gl.DrawElements(PrimitiveType.Triangles, (uint)indicesCount, DrawElementsType.UnsignedShort, (void*)0);
	}

	public unsafe static void UpdateBuffers(VertexArrayObject vertexArrayObject, VertexBufferObject vertexBufferObject, IndexBufferObject indexBufferObject, List<float> vertices, List<ushort> indices) {
		vertexArrayObject.Bind();
		vertexBufferObject.Bind();
		fixed (void* data = CollectionsMarshal.AsSpan(vertices)) {
			gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(float)), data, BufferUsageARB.StaticDraw);
		}
		indexBufferObject.Bind();
		fixed (void* data = CollectionsMarshal.AsSpan(indices)) {
			gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(ushort)), data, BufferUsageARB.StaticDraw);
		}
	}

	public unsafe static void DrawElements(RenderBuffer renderBuffer, int indicesCount) {
		renderBuffer.BindArrayObject();
		gl.DrawElements(PrimitiveType.Triangles, (uint)indicesCount, DrawElementsType.UnsignedShort, (void*)0);
	}

	public unsafe static void UpdateBuffers(RenderBuffer renderBuffer, List<float> vertices, List<ushort> indices) {
		renderBuffer.BindArrayObject();
		renderBuffer.BindVertexBuffer();
		fixed (void* data = CollectionsMarshal.AsSpan(vertices)) {
			gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(float)), data, BufferUsageARB.StaticDraw);
		}
		renderBuffer.BindIndexBuffer();
		fixed (void* data = CollectionsMarshal.AsSpan(indices)) {
			gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(ushort)), data, BufferUsageARB.StaticDraw);
		}
	}

	public static Vector2 PixelsToNDC(Vector2 pixelPosition) {
		VirtualWindow globalWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();
		return new Vector2((pixelPosition.X / globalWindow.GetWidth()) * 2 - 1, (pixelPosition.Y / globalWindow.GetHeight()) * 2 - 1);
	}
}

public class VertexArrayObject : IDisposable {
	private static GL gl = SystemsManager.Get<GL>();
	private uint id;

	public VertexArrayObject() {
		id = gl.GenVertexArray();
	}

	public unsafe void LinkAttribute(VertexBufferObject vertexBufferObject, uint layout, int componentCount, VertexAttribPointerType type, bool isNormalized, uint stride, void* offset) {
		Bind();
		vertexBufferObject.Bind();
		gl.VertexAttribPointer(layout, componentCount, type, isNormalized, stride, offset);
		gl.EnableVertexAttribArray(layout);
		vertexBufferObject.Unbind();
	}

	public void Bind() {
		gl.BindVertexArray(id);
	}
	
	public void Unbind() {
		gl.BindVertexArray(0);
	}
	
	public void Dispose() {
		gl.DeleteVertexArray(id);
	}
}

public class VertexBufferObject : IDisposable {
	private static GL gl = SystemsManager.Get<GL>();
	private uint id;

	public VertexBufferObject() {
		id = gl.GenBuffer();
	}

	public unsafe void SetBufferData(nuint size, void* data) {
		gl.BufferData(GLEnum.ArrayBuffer, size, data, GLEnum.StaticDraw);
	}

	public unsafe void SetBufferSubData(nint offset, nuint size, void* data) {
		gl.BufferSubData(GLEnum.ArrayBuffer, offset, size, data);
	}

	public void Bind() {
		gl.BindBuffer(BufferTargetARB.ArrayBuffer, id);
	}

	public void Unbind() {
		gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
	}

	public void Dispose() {
		gl.DeleteBuffer(id);
	}
}

public class IndexBufferObject : IDisposable {
	private static GL gl = SystemsManager.Get<GL>();
	private uint id;

	public IndexBufferObject() {
		id = gl.GenBuffer();
	}

	public void Bind() {
		gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, id);
	}

	public void Unbind() {
		gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
	}

	public void Dispose() {
		gl.DeleteBuffer(id);
	}
}