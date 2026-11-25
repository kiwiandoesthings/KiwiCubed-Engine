namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.OpenGL;

public class RenderBuffers : IRenderBuffers, IDisposable {
	private VertexArrayObject vertexArrayObject;
	private VertexBufferObject vertexBufferObject;
	private IndexBufferObject indexBufferObject;

	public RenderBuffers() {
		vertexArrayObject = new VertexArrayObject();
		vertexBufferObject = new VertexBufferObject();
		indexBufferObject = new IndexBufferObject();
	}

	public RenderBuffers(VertexArrayObject vertexArrayObject, VertexBufferObject vertexBufferObject, IndexBufferObject indexBufferObject) {
		this.vertexArrayObject = vertexArrayObject;
		this.vertexBufferObject = vertexBufferObject;
		this.indexBufferObject = indexBufferObject;
	}

	public void BindArrayObject() {
		vertexArrayObject.Bind();
	}

	public void BindVertexBuffer() {
		vertexBufferObject.Bind();
	}

	public void BindIndexBuffer() {
		indexBufferObject.Bind();
	}

	public unsafe void LinkAttribute(uint layout, int componentCount, VertexAttribPointerType type, uint stride, int offset) {
		vertexArrayObject.LinkAttribute(vertexBufferObject, layout, componentCount, type, false, stride, (void*)offset);
	}

    public unsafe void LinkIntAttribute(uint layout, int componentCount, GLEnum type, uint stride, int offset) {
        vertexArrayObject.LinkIntAttribute(vertexBufferObject, layout, componentCount, type, stride, (void*)offset);
    }

	public void SetAttributeDivisor(uint layout, uint divisor) {
		vertexArrayObject.SetAttributeDivisor(layout, divisor);
    }

    public void UpdateVertexBufferData<T>(ReadOnlySpan<T> data) where T: unmanaged {
        vertexBufferObject.SetBufferData(data);
    }

	public void UpdateIndexBufferData<T>(ReadOnlySpan<T> data) where T : unmanaged {
        indexBufferObject.SetBufferData(data);
    }

    public void UpdateVertexBufferSubData<T>(int offset, ReadOnlySpan<T> data) where T : unmanaged {
        vertexBufferObject.SetBufferSubData(offset, data);
    }

	public void UpdateIndexBufferSubData<T>(int offset, ReadOnlySpan<T> data) where T : unmanaged {
        indexBufferObject.SetBufferSubData(offset, data);
    }

    public void Dispose() {
		vertexArrayObject.Dispose();
		vertexBufferObject.Dispose();
		indexBufferObject.Dispose();

		
	}
}