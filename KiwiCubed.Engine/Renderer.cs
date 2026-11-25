namespace KiwiCubed.Engine;

using System.Numerics;
using KiwiCubed.Api;
using Silk.NET.OpenGL;

public class RendererWrapper : IRenderer {
	public void UpdateBuffers<T1, T2>(IRenderBuffers renderBuffers, ReadOnlySpan<T1> vertices, ReadOnlySpan<T2> indices) where T1: unmanaged where T2: unmanaged => Renderer.UpdateBuffers((RenderBuffers)renderBuffers, vertices, indices);
	public void DrawElements(IRenderBuffers renderBuffers, int indicesCount) => Renderer.DrawElements((RenderBuffers)renderBuffers, indicesCount);
    public IRenderBuffers CreateRenderBuffers() => Renderer.CreateRenderBuffer();
	public ICamera CreateCamera() => Renderer.CreateCamera();
}

public static class Renderer {
	private readonly static GL gl = MetaHandler.Get<GL>();

	public unsafe static void DrawElements(VertexArrayObject vertexArrayObject, int indicesCount) {
		vertexArrayObject.Bind();
		gl.DrawElements(PrimitiveType.Triangles, (uint)indicesCount, DrawElementsType.UnsignedShort, (void*)0);
	}

	public unsafe static void DrawElements(RenderBuffers renderBuffers, int indicesCount) {
		renderBuffers.BindArrayObject();
        gl.DrawElements(PrimitiveType.Triangles, (uint)indicesCount, DrawElementsType.UnsignedShort, (void*)0);
	}

	public static void UpdateBuffers<T1, T2>(VertexArrayObject vertexArrayObject, VertexBufferObject vertexBufferObject, IndexBufferObject indexBufferObject, ReadOnlySpan<T1> vertices, ReadOnlySpan<T2> indices) where T1: unmanaged where T2 : unmanaged {
		vertexArrayObject.Bind();
		vertexBufferObject.Bind();
        vertexBufferObject.SetBufferData(vertices);

        indexBufferObject.Bind();
        indexBufferObject.SetBufferData(indices);
    }

	public static void UpdateBuffers<T1, T2>(RenderBuffers renderBuffers, ReadOnlySpan<T1> vertices, ReadOnlySpan<T2> indices) where T1 : unmanaged where T2 : unmanaged {
		renderBuffers.BindArrayObject();
		renderBuffers.BindVertexBuffer();
        renderBuffers.UpdateVertexBufferData(vertices);

		renderBuffers.BindIndexBuffer();
        renderBuffers.UpdateIndexBufferData(indices);
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

public abstract class RenderBuffer : IDisposable {
    protected readonly static GL gl = MetaHandler.Get<GL>();
    protected readonly uint id;
	private readonly GLEnum bufferType;
	private readonly BufferTargetARB bufferTarget;

    public RenderBuffer(GLEnum bufferType, BufferTargetARB bufferTarget) {
		id = gl.CreateBuffer();
		this.bufferType = bufferType;
		this.bufferTarget = bufferTarget;
	}

    public unsafe void SetBufferData<T>(ReadOnlySpan<T> data) where T: unmanaged {
        Bind();
        gl.BufferData(bufferType, (nuint)(sizeof(T) * data.Length), data, GLEnum.StaticDraw);
    }

    public unsafe void SetBufferSubData<T>(nint offset, ReadOnlySpan<T> data) where T : unmanaged {
        Bind();
        gl.BufferSubData(bufferType, offset, (nuint)(sizeof(T) * data.Length), data);
    }

    public void Bind() {
        gl.BindBuffer(bufferTarget, id);
    }

    public void Unbind() {
        gl.BindBuffer(bufferTarget, 0);
    }

    public void Dispose() {
        gl.DeleteBuffer(id);
    }
}

public class VertexArrayObject : IDisposable {
	private readonly static GL gl = MetaHandler.Get<GL>();
	private readonly uint id;

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
	
	public static void Unbind() {
		gl.BindVertexArray(0);
	}
	
	public void Dispose() {
		gl.DeleteVertexArray(id);
	}
}

public class VertexBufferObject : RenderBuffer {
	public VertexBufferObject() : base(GLEnum.ArrayBuffer, BufferTargetARB.ArrayBuffer) { }
}

public class IndexBufferObject : RenderBuffer {
	public IndexBufferObject() : base(GLEnum.ElementArrayBuffer, BufferTargetARB.ElementArrayBuffer) { }
}

public class ShaderStorageObject : RenderBuffer {
    public ShaderStorageObject(int layout) : base(GLEnum.ShaderStorageBuffer, BufferTargetARB.ShaderStorageBuffer) {
		gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, (uint)layout, id);
	}
}