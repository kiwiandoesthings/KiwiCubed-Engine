namespace KiwiCubed;

using System;
using System.IO;
using System.Numerics;
using Silk.NET.OpenGL;
using StbImageSharp;

using static KiwiCubed.Api.KLogger;

public class Texture {
	public uint ID;
	GL gl = null;
	public TextureTarget Type;
	public TextureUnit Slot;
	public Vector2 AtlasSize;

	public Texture(string filepath, TextureTarget textureType, TextureUnit slot, PixelFormat format, PixelType pixelType, string usage) {
		gl = SystemsManager.Get<GL>();
		Type = textureType;
		Slot = slot;
		AtlasSize = Vector2.Zero;

		StbImage.stbi_set_flip_vertically_on_load(1);
		ImageResult image;

		try {
			using (var stream = File.OpenRead(filepath)) {
				image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
			}
		} catch (Exception) {
			image = null;
		}

		if (image == null) {
			KCRITICAL("Failed to load image from file path: " + filepath);
			System.Diagnostics.Debugger.Break();
		}

		gl.ActiveTexture(Slot);
		ID = gl.GenTexture();
		gl.BindTexture(Type, ID);

		gl.TexParameter(Type, TextureParameterName.TextureBaseLevel, 0);
		gl.TexParameter(Type, TextureParameterName.TextureMaxLevel, 4);

		gl.TexParameter(Type, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapNearest);
		gl.TexParameter(Type, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

		gl.TexParameter(Type, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
		gl.TexParameter(Type, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

		if (usage == "texture_gui") {
			gl.TexParameter(Type, TextureParameterName.TextureBaseLevel, 0);
			gl.TexParameter(Type, TextureParameterName.TextureMaxLevel, 0);
		}

		unsafe {
			fixed (byte* ptr = image.Data) {
				gl.TexImage2D(Type, 0, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height, 0, format, pixelType, ptr);
			}
		}

		if (usage != "texture_gui") {
			gl.GenerateMipmap(Type);
		}
		gl.TexParameter(Type, TextureParameterName.TextureLodBias, 0.0f);

		gl.BindTexture(Type, 0);
		KINFO("Successfully created texture with ID of {" + ID.ToString() + "} of type \"" + usage + "\"");
	}

	public void TextureUnit(Shader shader, string uniform) {
		shader.SetInt(uniform, (int)(Slot - Silk.NET.OpenGL.TextureUnit.Texture0));
	}

	public void SetAtlasSize(Shader shader, Vector2 newAtlasSize) {
		AtlasSize = newAtlasSize;
		shader.Bind();
		shader.SetVector2("atlasSize", AtlasSize);
	}

	public void Bind() {
		gl.BindTexture(Type, ID);
	}

	public void Unbind() {
		gl.BindTexture(Type, 0);
	}

	public void SetActive() {
		gl.ActiveTexture(Slot);
	}

	public void Delete(GL gl) {
		gl.DeleteTexture(ID);
	}
}