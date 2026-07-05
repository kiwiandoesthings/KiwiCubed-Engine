namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.Assimp;
using Silk.NET.Core.Native;

public static class ModelParser {
	private static Assimp assimp = Assimp.GetApi();
    private static KLogger logger = new KLogger("ModelParser");

    public static unsafe GeneralMesh ParseModel(string modelFilepath) {
		Scene* scene = assimp.ImportFile(modelFilepath, (uint)(PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices));
		if (scene == null || scene->MFlags == (uint)SceneFlags.Incomplete) {
			logger.ERR("Failed to parse OBJ model at path \"" + modelFilepath + "\". Assimp error: " + SilkMarshal.PtrToString((nint)Assimp.GetApi().GetErrorString()));
			logger.BREAK();
        }

        uint totalVerticesCount = 0;
        uint totalIndicesCount = 0;

        for (uint iterator = 0; iterator < scene->MNumMeshes; iterator++) {
            totalVerticesCount += scene->MMeshes[iterator]->MNumVertices;
            totalIndicesCount += scene->MMeshes[iterator]->MNumFaces * 3;
        }

        float[] vertices = new float[totalVerticesCount * 5];
        ushort[] indices = new ushort[totalIndicesCount];

        uint vertexOffset = 0;
        uint indexOffset = 0;
        uint baseVertexTracker = 0;

        bool hasTextureCoordinates = true;
        for (uint meshIterator = 0; meshIterator < scene->MNumMeshes; meshIterator++) {
            Mesh* mesh = scene->MMeshes[meshIterator];

            for (uint iterator = 0; iterator < mesh->MNumVertices; iterator++) {
                vertices[vertexOffset++] = mesh->MVertices[iterator].X;
                vertices[vertexOffset++] = mesh->MVertices[iterator].Y;
                vertices[vertexOffset++] = mesh->MVertices[iterator].Z;

                if (mesh->MTextureCoords[0] != null) {
                    vertices[vertexOffset++] = mesh->MTextureCoords[0][iterator].X;
                    vertices[vertexOffset++] = 1.0f - mesh->MTextureCoords[0][iterator].Y;
                } else {
                    vertices[vertexOffset++] = 0.0f;
                    vertices[vertexOffset++] = 0.0f;
                    hasTextureCoordinates = false;
                }
            }

            for (uint iterator = 0; iterator < mesh->MNumFaces; iterator++) {
                indices[indexOffset++] = (ushort)(mesh->MFaces[iterator].MIndices[0] + baseVertexTracker);
                indices[indexOffset++] = (ushort)(mesh->MFaces[iterator].MIndices[1] + baseVertexTracker);
                indices[indexOffset++] = (ushort)(mesh->MFaces[iterator].MIndices[2] + baseVertexTracker);
            }

            baseVertexTracker += mesh->MNumVertices;
        }

        if (!hasTextureCoordinates) {
            logger.WARN("Model contains partial or no texture coordinate data");
        }

        assimp.ReleaseImport(scene);

        return new GeneralMesh(vertices, indices);
	}
}