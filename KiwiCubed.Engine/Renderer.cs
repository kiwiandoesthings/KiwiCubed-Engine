namespace KiwiCubed.Engine;

using System.Collections.Concurrent;
using System.Numerics;
using KiwiCubed.Api;
using Silk.NET.OpenGL;

public class RendererWrapper : IRenderer {
	public void UpdateBuffers(IRenderBuffers renderBuffers, float[] vertices, ushort[] indices) => Renderer.UpdateBuffers((RenderBuffers)renderBuffers, vertices, indices);
	public void DrawElements(IRenderBuffers renderBuffers, int indicesCount) => Renderer.DrawElements((RenderBuffers)renderBuffers, indicesCount);
	public void EnqueueRenderTask(Action renderTask) => Renderer.EnqueueRenderTask(renderTask);
    public IRenderBuffers CreateRenderBuffers() => (IRenderBuffers)Renderer.CreateRenderBuffer();
	public ICamera CreateCamera() => (ICamera)Renderer.CreateCamera();
}

public static class Renderer {
	private static GL gl = MetaHandler.Get<GL>();
	private static ConcurrentQueue<Action> renderTasks;

	public unsafe static void DrawElements(VertexArrayObject vertexArrayObject, int indicesCount) {
		vertexArrayObject.Bind();
		gl.DrawElements(PrimitiveType.Triangles, (uint)indicesCount, DrawElementsType.UnsignedShort, (void*)0);
	}

	public unsafe static void UpdateBuffers(VertexArrayObject vertexArrayObject, VertexBufferObject vertexBufferObject, IndexBufferObject indexBufferObject, float[] vertices, ushort[] indices) {
		vertexArrayObject.Bind();
		vertexBufferObject.Bind();
		fixed (void* data = vertices) {
			vertexBufferObject.SetBufferData((nuint)vertices.Length * sizeof(float), data);
		}
		indexBufferObject.Bind();
		fixed (void* data = indices) {
			indexBufferObject.SetBufferData((nuint)indices.Length * sizeof(ushort), data);
		}
	}

	public unsafe static void DrawElements(RenderBuffers renderBuffers, int indicesCount) {
		renderBuffers.BindArrayObject();
        gl.DrawElements(PrimitiveType.Triangles, (uint)indicesCount, DrawElementsType.UnsignedShort, (void*)0);
	}

	public unsafe static void UpdateBuffers(RenderBuffers renderBuffers, float[] vertices, ushort[] indices) {
		renderBuffers.BindArrayObject();
		renderBuffers.BindVertexBuffer();
		fixed (void* data = vertices) {
			renderBuffers.UpdateVertexBufferData(vertices.Length * sizeof(float), data);
		}
		renderBuffers.BindIndexBuffer();
		fixed (void* data = indices) {
			renderBuffers.UpdateIndexBufferData(indices.Length * sizeof(ushort), data);
        }
	}

	public static void EnqueueRenderTask(Action renderTask) {
        renderTasks.Enqueue(renderTask);
    }

    public static RenderBuffers CreateRenderBuffer() {
		return new RenderBuffers();
	}

	public static Camera CreateCamera() {
		return new Camera();
	}

	public static Vector2 PixelsToNDC(Vector2 pixelPosition) {
		VirtualWindow globalWindow = (VirtualWindow)MetaHandler.Get<IVirtualWindow>();
		return new Vector2((pixelPosition.X / globalWindow.GetWidth()) * 2 - 1, (pixelPosition.Y / globalWindow.GetHeight()) * 2 - 1);
	}
}

public class VertexArrayObject : IDisposable {
	private static GL gl = MetaHandler.Get<GL>();
	private uint id;

	public VertexArrayObject() {
		id = gl.GenVertexArray();
	}

	public unsafe void LinkAttribute(VertexBufferObject vertexBufferObject, uint layout, int componentCount, VertexAttribPointerType type, bool isNormalized, uint stride, void* offset) {
        Bind();
		vertexBufferObject.Bind();
        gl.VertexAttribPointer(layout, componentCount, type, isNormalized, stride, offset);
		gl.EnableVertexAttribArray(layout);
	}

	public unsafe void LinkIntAttribute(VertexBufferObject vertexBufferObject, uint layout, int componentCount, GLEnum type, uint stride, void* offset) {
        Bind();
        vertexBufferObject.Bind();
        gl.VertexAttribIPointer(layout, componentCount, type, stride, offset);
        gl.EnableVertexAttribArray(layout);
    }

	public void SetAttributeDivisor(uint layout, uint divisor) {
        Bind();
        gl.VertexAttribDivisor(layout, divisor);
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
	private static GL gl = MetaHandler.Get<GL>();
	private uint id;

	public VertexBufferObject() {
		id = gl.GenBuffer();
	}

	public unsafe void SetBufferData(nuint size, void* data) {
		Bind();
		gl.BufferData(GLEnum.ArrayBuffer, size, data, GLEnum.StaticDraw);
	}

	public unsafe void SetBufferSubData(nint offset, nuint size, void* data) {
        Bind();
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
	private static GL gl = MetaHandler.Get<GL>();
	private uint id;

	public IndexBufferObject() {
		id = gl.GenBuffer();
	}

	public unsafe void SetBufferData(nuint size, void* data) {
        Bind();
        gl.BufferData(GLEnum.ElementArrayBuffer, size, data, GLEnum.StaticDraw);
    }

	public unsafe void SetBufferSubData(nint offset, nuint size, void* data) {
        Bind();
        gl.BufferSubData(GLEnum.ElementArrayBuffer, offset, size, data);
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