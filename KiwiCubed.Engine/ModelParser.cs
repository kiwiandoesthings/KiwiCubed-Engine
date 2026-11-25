namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.Assimp;
using Silk.NET.Core.Native;

public static class ModelParser {
    private static readonly KLogger logger = new KLogger("ModelParser");
	private static readonly Assimp assimp = Assimp.GetApi();

    public static unsafe GeneralMesh<Vertex3D> ParseModel(string modelFilepath) {
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

        Vertex3D[] vertices = new Vertex3D[totalVerticesCount];
        ushort[] indices = new ushort[totalIndicesCount];

        uint indexOffset = 0;
        uint baseVertexTracker = 0;

        bool hasTextureCoordinates = true;
        for (uint meshIterator = 0; meshIterator < scene->MNumMeshes; meshIterator++) {
            Mesh* mesh = scene->MMeshes[meshIterator];

            for (uint iterator = 0; iterator < mesh->MNumVertices; iterator++) {
                float x = mesh->MVertices[iterator].X;
                float y = mesh->MVertices[iterator].Y;
                float z = mesh->MVertices[iterator].Z;

                float u = 0.0f;
                float v = 0.0f;
                if (mesh->MTextureCoords[0] != null) {
                    u = mesh->MTextureCoords[0][iterator].X;
                    v = 1.0f - mesh->MTextureCoords[0][iterator].Y;
                } else {
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

        return new GeneralMesh<Vertex3D>(vertices, indices);
	}
}