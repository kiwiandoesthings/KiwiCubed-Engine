namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using FreeTypeSharp;
using Silk.NET.OpenGL;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.KLogger;
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
		OVERRIDE_LOG_NAME("Text Renderer");

		assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
		gl = SystemsManager.Get<GL>();
		globalWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();
		textShader = (Shader)assetManager.GetShader(new AssetStringID("kiwicubed", "shader/text"));
		vertexArrayObject = new VertexArrayObject();
		vertexBufferObject = new VertexBufferObject();
		indexBufferObject = new IndexBufferObject();
		vertexArrayObject.LinkAttribute(vertexBufferObject, 0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, (void*)0);
		vertexArrayObject.LinkAttribute(vertexBufferObject, 1, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, (void*)(sizeof(float) * 2));
		characters = new();

		fixed (FT_LibraryRec_** ptr = &freeType) {
			FT_Error error = FT_Init_FreeType(ptr);
			if (error != FT_Error.FT_Err_Ok) {
				KERR("Failed to initialize FreeType libraray with error of \"" + error + "\"");
				return;
			}
		}
	}

	public static void AddFont(string filePath) {
		OVERRIDE_LOG_NAME("Text Renderer");

		fixed (FT_FaceRec_** fontFacePtr = &fontFace) {
			byte[] pathData = System.Text.Encoding.UTF8.GetBytes(filePath + "\0");
			fixed (byte* pathDataPtr = pathData) {
				FT_Error error = FT_New_Face(freeType, pathDataPtr, 0, fontFacePtr);
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
				KERR("Failed to load character glyph with numerical id {" + characterIndex + "}");
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
		OVERRIDE_LOG_NAME("Text Renderer");

		List<float> vertices = new();
		List<ushort> indices = new();

		float currentX = 0.0f;
		float currentY = 0.0f;
		ushort lastIndex = 0;
		foreach (char rawCharacter in text) {
			if (!characters.TryGetValue(rawCharacter, out Character character)) {
				KERR("Failed to lookup character \"" + rawCharacter + "\" from character dictionary");
			}

			float characterX = currentX + character.bearing.X;
			float characterY = currentY - character.bearing.Y;
			float width = character.size.X;
			float height = character.size.Y;

			vertices.Add(characterX);
			vertices.Add(characterY);
			vertices.Add(character.u.X);
			vertices.Add(character.u.Y);

			vertices.Add(characterX + width);
			vertices.Add(characterY);
			vertices.Add(character.v.X);
			vertices.Add(character.u.Y);

			vertices.Add(characterX + width);
			vertices.Add(characterY + height);
			vertices.Add(character.v.X);
			vertices.Add(character.v.Y);

			vertices.Add(characterX);
			vertices.Add(characterY + height);
			vertices.Add(character.u.X);
			vertices.Add(character.v.Y);

			indices.Add((ushort)(lastIndex + 0));
			indices.Add((ushort)(lastIndex + 1));
			indices.Add((ushort)(lastIndex + 2));

			indices.Add((ushort)(lastIndex + 2));
			indices.Add((ushort)(lastIndex + 3));
			indices.Add((ushort)(lastIndex + 0));

			lastIndex += 4;

			currentX += (character.advance >> 6);
		}

		return new GeneralMesh(vertices, indices);
	}

	public static Vector2 MeasureText(string text) {
		float totalWidth = 0.0f;
		float maxHeight = 0.0f;
		int lastWidth = 0;

		foreach (char rawCharacter in text) {
			if (!characters.TryGetValue(rawCharacter, out Character character)) {
				KERR("Failed to lookup character \"" + rawCharacter + "\" from character dictionary");
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

		Renderer.DrawElements(vertexArrayObject, textMesh.indices.Count);
	}
}