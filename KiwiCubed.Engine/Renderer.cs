namespace KiwiCubed.Engine;

using System.Numerics;
using System.Runtime.InteropServices;
using KiwiCubed.Api;
using Silk.NET.OpenGL;

public class RendererWrapper : IRenderer {
	public void UpdateBuffers(IRenderBuffers renderBuffers, List<float> vertices, List<ushort> indices) => Renderer.UpdateBuffers((RenderBuffers)renderBuffers, vertices, indices);
	public void DrawElements(IRenderBuffers renderBuffers, int indicesCount) => Renderer.DrawElements((RenderBuffers)renderBuffers, indicesCount);
	public IRenderBuffers CreateRenderBuffers() => (IRenderBuffers)Renderer.CreateRenderBuffer();
	public ICamera CreateCamera() => (ICamera)Renderer.CreateCamera();
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

	public unsafe static void DrawElements(RenderBuffers renderBuffers, int indicesCount) {
		renderBuffers.BindArrayObject();
		gl.DrawElements(PrimitiveType.Triangles, (uint)indicesCount, DrawElementsType.UnsignedShort, (void*)0);
	}

	public unsafe static void UpdateBuffers(RenderBuffers renderBuffers, List<float> vertices, List<ushort> indices) {
		renderBuffers.BindArrayObject();
		renderBuffers.BindVertexBuffer();
		fixed (void* data = CollectionsMarshal.AsSpan(vertices)) {
			gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(float)), data, BufferUsageARB.StaticDraw);
		}
		renderBuffers.BindIndexBuffer();
		fixed (void* data = CollectionsMarshal.AsSpan(indices)) {
			gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(ushort)), data, BufferUsageARB.StaticDraw);
		}
	}

	public static RenderBuffers CreateRenderBuffer() {
		return new RenderBuffers();
	}

	public static Camera CreateCamera() {
		return new Camera();
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