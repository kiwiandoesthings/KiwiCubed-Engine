namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StbImageSharp;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;

using static KiwiCubed.Api.KLogger;

public class Texture : ITexture {
	public uint id { get; private set; }
	private GL gl;
	private TextureTarget type;
	private TextureUnit slot;
	private PixelFormat format;
	private PixelType dataType;
	private uint width;
	private uint height;

	//public Texture(string filepath, TextureTarget textureType, TextureUnit slot, PixelFormat format, PixelType pixelType, string usage) {
	//	ImageResult image;
	//
	//	try {
	//		image = ImageResult.FromStream(File.OpenRead(filepath), ColorComponents.RedGreenBlueAlpha);
	//	} catch (Exception exception) {
	//		KCRITICAL("Failed to load image from file path \"" + filepath + "\" with error \"" + exception.Message + "\"");
	//		return;
	//	}
	//
	//	CreateTexture(image, textureType, slot, format, pixelType, usage);
	//}
	//
	//public Texture(ImageResult image, TextureTarget textureType, TextureUnit slot, PixelFormat format, PixelType pixelType, string usage) {
	//	CreateTexture(image, textureType, slot, format, pixelType, usage);
	//}

	public Texture(byte[] pixelData, int width, int height, TextureTarget textureType, TextureUnit slot, PixelFormat format, PixelType pixelType, bool mipmapped) {
		CreateTexture(pixelData, width, height, textureType, slot, format, pixelType, mipmapped);
	}

	public Texture(Image<Rgba32> image, TextureTarget textureType, TextureUnit slot, PixelFormat format, PixelType pixelType, bool mipmapped) {
		byte[] pixelData = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgba32>()];
		image.CopyPixelDataTo(pixelData);

		CreateTexture(pixelData, image.Width, image.Height, textureType, slot, format, pixelType, mipmapped);
	}

	public void TextureUnit(Shader shader, string uniform) {
		shader.SetInt(uniform, (int)(slot - Silk.NET.OpenGL.TextureUnit.Texture0));
	}

	public void SetTextureData(int xPosition, int yPosition, int width, int height, byte[] data) {
		gl.TexSubImage2D(type, 0, xPosition, yPosition, (uint)width, (uint)height, format, dataType, data);
	}

	public void Bind() {
		gl.BindTexture(type, id);
	}

	public void Unbind() {
		gl.BindTexture(type, 0);
	}

	public void SetActive() {
		gl.ActiveTexture(slot);
	}

	public static ImageResult GetRawTexture(string filepath) {
		ImageResult image;

		try {
			image = ImageResult.FromStream(File.OpenRead(filepath), ColorComponents.RedGreenBlueAlpha);
			return image;
		} catch (Exception exception) {
			KCRITICAL("Failed to load image from file path \"" + filepath + "\" with error \"" + exception.Message + "\"");
			return null;
		}
	}

	public Vector2 GetSize() {
		return new Vector2(width, height);
	}

	public void Delete(GL gl) {
		gl.DeleteTexture(id);
	}

	private void CreateTexture(byte[] pixelData, int width, int height, TextureTarget textureType, TextureUnit slot, PixelFormat format, PixelType pixelType, bool mipmapped) {
		OVERRIDE_LOG_NAME("Texture Creation");

		gl = MetaHandler.Get<GL>();
		type = textureType;
		this.slot = slot;
		this.format = format;
		this.dataType = pixelType;

		this.width = (uint)width;
		this.height = (uint)height;

		gl.ActiveTexture(slot);
		id = gl.GenTexture();
		gl.BindTexture(type, id);

		gl.TexParameter(type, TextureParameterName.TextureBaseLevel, 0);
		gl.TexParameter(type, TextureParameterName.TextureMaxLevel, 2);

		gl.TexParameter(type, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapNearest);
		gl.TexParameter(type, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

		gl.TexParameter(type, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
		gl.TexParameter(type, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

		InternalFormat internalFormat;
		switch (format) {
			case PixelFormat.Red:
				internalFormat = InternalFormat.R8;
				break;
			case PixelFormat.Rgb:
				internalFormat = InternalFormat.Rgb;
				break;
			default:
				internalFormat = InternalFormat.Rgba;
				break;
		}

		unsafe {
			fixed (byte* ptr = pixelData) {
				gl.TexImage2D(type, 0, internalFormat, (uint)width, (uint)height, 0, format, pixelType, ptr);
			}
		}

		if (mipmapped) {
			gl.GenerateMipmap(type);
		} else {
			gl.TexParameter(type, TextureParameterName.TextureBaseLevel, 0);
			gl.TexParameter(type, TextureParameterName.TextureMaxLevel, 0);
		}

		gl.TexParameter(type, TextureParameterName.TextureLodBias, 0.0f);

		gl.BindTexture(type, 0);
		KINFO("Successfully created texture with ID of {" + id.ToString() + "} that " + (mipmapped ? "is" : "is not") + " mipmapped");
	}
}