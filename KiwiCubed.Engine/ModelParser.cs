namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.KLogger;

public class ModelParser {
	public static GeneralMesh ParseModel(string modelFilepath) {
		FileStream modelFile = new FileStream(modelFilepath, FileMode.Open, FileAccess.Read);
		StreamReader reader = new StreamReader(modelFile);

		try {
			string currentLine = reader.ReadLine();
			while (currentLine != "o mesh") {
				currentLine = reader.ReadLine();
			}

			// Positions section
			List<Vector3> vertices = new();
			currentLine = reader.ReadLine();
			while (currentLine.Substring(0, 2) == "v ") {
				string[] parts = currentLine.Substring(2).Split(" ");
				Vector3 position = new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
				vertices.Add(position);

				currentLine = reader.ReadLine();
			}

			// Texture coordinates section
			List<Vector2> textureCoordinates = new();
			currentLine = reader.ReadLine();
			while (currentLine.Substring(0, 2) == "vt") {
				string[] parts = currentLine.Substring(3).Split(" ");
				Vector2 textureCoordinate = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
				textureCoordinates.Add(textureCoordinate);

				currentLine = reader.ReadLine();
			}

			// Skip normals
			currentLine = reader.ReadLine();
			while (currentLine.Substring(0, 2) == "vn") {
				currentLine = reader.ReadLine();
			}

			// Faces section
			currentLine = reader.ReadLine();
			while (currentLine.Substring(0, 2) == "f ") {
				if (!reader.
			}
		} catch (IOException exception) {
			KERR("Failed to parse model file at \"" + modelFilepath + "\" with error \"" + exception.Message + "\"");
			return new GeneralMesh();
		}
	}
}