namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.Assimp;
using Silk.NET.Core.Native;

using static KiwiCubed.Api.KLogger;

public static class ModelParser {
	private static Assimp assimp = Assimp.GetApi();

    public static unsafe GeneralMesh ParseModel(string modelFilepath) {
		Scene* scene = assimp.ImportFile(modelFilepath, (uint)(PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices));
		if (scene == null || scene->MFlags == (uint)SceneFlags.Incomplete) {
			KERR("Failed to parse OBJ model at path \"" + modelFilepath + "\". Assimp error: " + SilkMarshal.PtrToString((nint)Assimp.GetApi().GetErrorString()));
			KBREAK();
        }

		Mesh* mesh = scene->MMeshes[0];

		float[] vertices = new float[mesh->MNumVertices * 5];
		ushort[] indices = new ushort[mesh->MNumFaces * 3];
		for (int iterator = 0; iterator < vertices.Length / 5; iterator++) {
			vertices[(iterator * 5)] = mesh->MVertices[iterator].X;
            vertices[(iterator * 5) + 1] = mesh->MVertices[iterator].Y;
            vertices[(iterator * 5) + 2] = mesh->MVertices[iterator].Z;
            vertices[(iterator * 5) + 3] = mesh->MTextureCoords[0][iterator].X;
            vertices[(iterator * 5) + 4] = mesh->MTextureCoords[0][iterator].X;
        }
		for (int iterator = 0; iterator < indices.Length / 3; iterator++) {
			indices[(iterator * 3)] = (ushort)mesh->MFaces[iterator].MIndices[0];
			indices[(iterator * 3) + 1] = (ushort)mesh->MFaces[iterator].MIndices[1];
			indices[(iterator * 3) + 2] = (ushort)mesh->MFaces[iterator].MIndices[2];
        }

		assimp.ReleaseImport(scene);

        return new GeneralMesh(vertices.ToList(), indices.ToList(), true);
	}
}