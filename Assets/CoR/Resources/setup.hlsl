
#ifndef _SKINNINGSETUP_
#define _SKINNINGSETUP_

StructuredBuffer<float3> verticesOutBuffer;
StructuredBuffer<float3> normalsOutBuffer;
StructuredBuffer<float4> tangentsOutBuffer;

void GetPosition_float(float ID, out float3 Postion, out float3 Normal, out float3 Tangent)
{
    Postion = float3(0, 0, 0);
    Normal = float3(0, 0, 0);
    Tangent = float3(0, 0, 0);
#ifndef SHADERGRAPH_PREVIEW
    Postion = verticesOutBuffer[ID];
    Normal = normalsOutBuffer[ID];
    Tangent = tangentsOutBuffer[ID].xyz;
#endif
}
#endif