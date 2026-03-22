#version 330 core

layout (location = 0) in vec2 position;
layout (location = 1) in vec2 textureCoordinates;

out vec2 textureCoordinatesOut;

uniform mat4 modelMatrix;
uniform mat4 projectionMatrix;

void main()
{
    vec4 worldPosition = modelMatrix * vec4(position, 0.0, 1.0);

    worldPosition.xy = floor(worldPosition.xy + 0.5);
    gl_Position = projectionMatrix * worldPosition;

    textureCoordinatesOut = textureCoordinates;
}