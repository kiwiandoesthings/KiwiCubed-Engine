#version 330 core

out vec4 FragColor;

in vec2 textureCoordinatesOut;

uniform sampler2D tex0;


void main()
{
	vec4 textureSample = texture(tex0, textureCoordinatesOut);
	if (textureSample.a < 0.1) {
		discard;
	}

    FragColor = textureSample;
}