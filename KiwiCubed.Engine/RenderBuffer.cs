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

	public unsafe void SetAttributeDivisor(uint layout, uint divisor) {
		vertexArrayObject.SetAttributeDivisor(layout, divisor);
    }

    public unsafe void UpdateVertexBufferData(int size, void* data) {
        vertexBufferObject.SetBufferData((nuint)size, data);
    }

	public unsafe void UpdateIndexBufferData(int size, void* data) {
        indexBufferObject.SetBufferData((nuint)size, data);
    }

    public unsafe void UpdateVertexBufferSubData(int offset, int size, void* data) {
        vertexBufferObject.SetBufferSubData(offset, (nuint)size, data);
    }

	public unsafe void UpdateIndexBufferSubData(int offset, int size, void* data) {
        indexBufferObject.SetBufferSubData(offset, (nuint)size, data);
    }

    public void Dispose() {
		vertexArrayObject.Dispose();
		vertexBufferObject.Dispose();
		indexBufferObject.Dispose();

		GC.SuppressFinalize(this);
	}
}