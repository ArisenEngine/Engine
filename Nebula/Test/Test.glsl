#version 450

// Binding 0, Set 0: Uniform Buffer for transformation matrices (not used but bound)
layout(set = 0, binding = 0) uniform TransformUBO {
    mat4 model;
    mat4 view;
    mat4 projection;
};

// Binding 1, Set 0: Sampler for texture
layout(set = 0, binding = 1) uniform sampler2D textureSampler;

// Binding 2, Set 0: Uniform Buffer for light data
layout(set = 0, binding = 2) uniform LightUBO {
    vec4 lightPosition;
    vec4 lightColor;
};

// Binding 0, Set 1: Storage Image for output processing
layout(set = 1, binding = 0, rgba32f) uniform image2D storageImage;

// Binding 1, Set 1: Storage Buffer for additional data
layout(set = 1, binding = 1) buffer StorageBuffer {
    vec4 someData[];
} storageBuffer;

layout(location = 0) in vec2 fragTexCoord;
layout(location = 0) out vec4 outColor;

void main() {
    // Sample the texture
    vec4 sampledColor = texture(textureSampler, fragTexCoord);

    // Mix with light color
    vec4 finalColor = sampledColor * light.lightColor;

    // Write to the storage image (if needed)
    imageStore(storageImage, ivec2(gl_FragCoord.xy), finalColor);

    // Optional: Process storage buffer data (example)
    vec4 bufferData = storageBuffer.someData[0];
    finalColor += bufferData;

    // Output final color
    outColor = finalColor;
}
