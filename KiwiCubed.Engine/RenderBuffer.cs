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

	public void Dispose() {
		vertexArrayObject.Dispose();
		vertexBufferObject.Dispose();
		indexBufferObject.Dispose();

		GC.SuppressFinalize(this);
	}
}