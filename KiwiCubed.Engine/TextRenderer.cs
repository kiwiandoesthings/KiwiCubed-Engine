namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using FreeTypeSharp;
using Silk.NET.OpenGL;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static FreeTypeSharp.FT;

public class TextRendererWrapper : ITextRenderer {
	public void RenderText(string text, Vector2 position, Vector2 size, Color color) => TextRenderer.RenderText(text, position, size, color);
	public Vector2 MeasureText(string text) => TextRenderer.MeasureText(text);
}

public static unsafe class TextRenderer {
	struct Character {
		public Vector2 size;
		public Vector2 bearing;
		public Vector2 u;
		public Vector2 v;
		public uint advance;

		public Character(Vector2 size, Vector2 bearing, Vector2 u, Vector2 v, int advance) {
			this.size = size;
			this.bearing = bearing;
			this.u = u;
			this.v = v;
			this.advance = (uint)advance;
		}
	};

	private static KLogger logger;
	private static AssetManager assetManager;
	private static GL gl;
	private static VirtualWindow globalWindow;
	private static Shader textShader;
	private static Texture fontTexture;
	private static VertexArrayObject vertexArrayObject;
	private static VertexBufferObject vertexBufferObject;
	private static IndexBufferObject indexBufferObject;
	private static FT_LibraryRec_* freeType;
	private static FT_FaceRec_* fontFace;
	private static Dictionary<char, Character> characters;
	private static Vector2 pos = Vector2.Zero;
	private static Vector2 big = Vector2.One;

	static TextRenderer() {
		logger = new KLogger("TextRenderer");
        assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
		gl = MetaHandler.Get<GL>();
		globalWindow = (VirtualWindow)MetaHandler.Get<IVirtualWindow>();
		textShader = (Shader)assetManager.GetShader(new AssetStringID("kiwicubed", "shader/text"));
		vertexArrayObject = new VertexArrayObject();
		vertexBufferObject = new VertexBufferObject();
		indexBufferObject = new IndexBufferObject();
		vertexArrayObject.Bind();
		vertexBufferObject.Bind();
		vertexArrayObject.LinkAttribute(vertexBufferObject, 0, 3, VertexAttribPointerType.Float, false, sizeof(float) * 5, (void*)0);
		vertexArrayObject.LinkAttribute(vertexBufferObject, 1, 2, VertexAttribPointerType.Float, false, sizeof(float) * 5, (void*)(sizeof(float) * 3));
		characters = new();

		fixed (FT_LibraryRec_** ptr = &freeType) {
			FT_Error error = FT_Init_FreeType(ptr);
			if (error != FT_Error.FT_Err_Ok) {
				logger.ERR("Failed to initialize FreeType libraray with error of \"" + error + "\"");
				return;
			}
		}
	}

	public static void AddFont(string filePath) {
		fixed (FT_FaceRec_** fontFacePtr = &fontFace) {
			byte[] pathData = System.Text.Encoding.UTF8.GetBytes(filePath + "\0");
			fixed (byte* pathDataPtr = pathData) {
				FT_Error error = FT_New_Face(freeType, pathDataPtr, 0, fontFacePtr);
				if (error != FT_Error.FT_Err_Ok) {
					logger.ERR("Encountered an error \"" + error + "\" while loading font from \"" + filePath + "\", returning");
					logger.BREAK();
				}
			}
		}
		FT_Set_Pixel_Sizes(fontFace, 0, 32);

		fontTexture = new Texture(null, 512, 512, TextureTarget.Texture2D, TextureUnit.Texture0, PixelFormat.Red, PixelType.UnsignedByte, true);
		fontTexture.Bind();
		gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

		int glyphX = 0;
		int glyphY = 0;
		int rowHeight = 0;
		for (int characterIndex = 0; characterIndex < 128; characterIndex++) {
			if (FT_Load_Char(fontFace, (nuint)characterIndex, FT_LOAD.FT_LOAD_RENDER) != FT_Error.FT_Err_Ok) {
				logger.ERR("Failed to load character glyph with numerical id {" + characterIndex + "}");
				continue;
			}

			FT_GlyphSlotRec_* slot = fontFace->glyph;
			FT_Bitmap_ bitmap = slot->bitmap;

			if (glyphX + (int)bitmap.width > 512) {
				glyphX = 0;
				glyphY += rowHeight + 1;
				rowHeight = 0;
			}

			if (bitmap.buffer != null) {
				int length = (int)(bitmap.width * bitmap.rows);
				byte[] textureData = new byte[length];
				Marshal.Copy((IntPtr)bitmap.buffer, textureData, 0, length);
				fontTexture.SetTextureData(glyphX, glyphY, (int)bitmap.width, (int)bitmap.rows, textureData);
			}

			Character character = new Character(new Vector2(bitmap.width, bitmap.rows), new Vector2(slot->bitmap_left, slot->bitmap_top), new Vector2(glyphX / 512.0f, glyphY / 512.0f), new Vector2((glyphX + bitmap.width) / 512.0f, (glyphY + bitmap.rows) / 512.0f), (int)slot->advance.x);
			characters.Add((char)characterIndex, character);

			glyphX += (int)bitmap.width + 1;
			rowHeight = Math.Max(rowHeight, (int)bitmap.rows);
		}
		gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
	}

	public static GeneralMesh GetTextMesh(string text) {
		float[] vertices = new float[text.Length * 20];
		ushort[] indices = new ushort[text.Length * 6];

		float currentX = 0.0f;
		float currentY = 0.0f;
		ushort lastIndex = 0;
		for (int iterator = 0; iterator < text.Length; iterator++) {
			char rawCharacter = text[iterator];
			if (!characters.TryGetValue(rawCharacter, out Character character)) {
				logger.ERR("Failed to lookup character \"" + rawCharacter + "\" from character dictionary");
			}

			float characterX = currentX + character.bearing.X;
			float characterY = currentY - character.bearing.Y;
			float width = character.size.X;
			float height = character.size.Y;

			int vertexIndex = iterator * 20;
			int indexIndex = iterator * 6;

			vertices[vertexIndex] = characterX;
			vertices[vertexIndex + 1] = characterY;
            vertices[vertexIndex + 2] = 0.0f;
            vertices[vertexIndex + 3] = character.u.X;
			vertices[vertexIndex + 4] = character.u.Y;

			vertices[vertexIndex + 5] = characterX + width;
			vertices[vertexIndex + 6] = characterY;
            vertices[vertexIndex + 7] = 0.0f;
            vertices[vertexIndex + 8] = character.v.X;
			vertices[vertexIndex + 9] = character.u.Y;

			vertices[vertexIndex + 10] = characterX + width;
			vertices[vertexIndex + 11] = characterY + height;
            vertices[vertexIndex + 12] = 0.0f;
            vertices[vertexIndex + 13] = character.v.X;
			vertices[vertexIndex + 14] = character.v.Y;

			vertices[vertexIndex + 15] = characterX;
			vertices[vertexIndex + 16] = characterY + height;
            vertices[vertexIndex + 17] = 0.0f;
            vertices[vertexIndex + 18] = character.u.X;
			vertices[vertexIndex + 19] = character.v.Y;

			indices[indexIndex] = (ushort)(lastIndex + 0);
			indices[indexIndex + 1] = (ushort)(lastIndex + 1);
			indices[indexIndex + 2] = (ushort)(lastIndex + 2);

			indices[indexIndex + 3] = (ushort)(lastIndex + 2);
			indices[indexIndex + 4] = (ushort)(lastIndex + 3);
			indices[indexIndex + 5] = (ushort)(lastIndex + 0);

			lastIndex += 4;

			currentX += (character.advance >> 6);
		}

		GeneralMesh textMesh = new GeneralMesh(vertices, indices);

		return textMesh;
	}

	public static Vector2 MeasureText(string text) {
		float totalWidth = 0.0f;
		float maxHeight = 0.0f;

		foreach (char rawCharacter in text) {
			if (!characters.TryGetValue(rawCharacter, out Character character)) {
				logger.ERR("Failed to lookup character \"" + rawCharacter + "\" from character dictionary");
			} else {
				totalWidth += (character.advance >> 6);

				if (character.size.Y > maxHeight) {
					maxHeight = character.size.Y;
				}
			}
		}

		totalWidth -= 2;
		return new Vector2(totalWidth, maxHeight);
	}

	public static void RenderText(string text, Vector2 position) {
		RenderText(text, position, Vector2.One, Color.Black);
	}

	public static void RenderText(string text, Vector2 position, Vector2 scale, Color color) {
		fontTexture.Bind();

		GeneralMesh textMesh = GetTextMesh(text);
		Renderer.UpdateBuffers(vertexArrayObject, vertexBufferObject, indexBufferObject, textMesh.vertices, textMesh.indices);

		Matrix4x4 modelMatrix = Matrix4x4.CreateScale(new Vector3(scale.X, scale.Y, 1.0f)) * Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0.1f));
		Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(0, globalWindow.GetWidth(), globalWindow.GetHeight(), 0, -1.0f, 1.0f);
		textShader.SetMatrix4("modelMatrix", modelMatrix);
		textShader.SetMatrix4("projectionMatrix", projection);
		textShader.SetVector3("textColor", new Vector3(color.R, color.G, color.B));

		Renderer.DrawElements(vertexArrayObject, textMesh.indices.Length);
	}
}