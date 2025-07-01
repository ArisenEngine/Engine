Shader "Arisen/TestShader"
{
    HLSLINCLUDE

    #pragma vertex Vert
    
    struct Attributes
    {
        uint vertexID : SV_VertexID;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
    };

    Varyings Vert(Attributes input)
    {
        // 测试注释1
        Varyings output;
        output.positionCS
        // 测试注释2 
        = float4(input.vertexID>>1, 
        input.vertexID<<2,input.
        vertexID &2,1.0);
        return output;
    }


    struct RenderOutput
    {
        float4 colorBuffer : SV_Target;
    };

    RenderOutput FragRender(Varyings input)
    {
       
        RenderOutput output;
        /**
        测试注释3
        */

        output.colorBuffer = float4(input.positionCS.x,1
            ,1,1);
        /////
        ///// 测试注释4
        ////
        return output;
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend One SrcAlpha, Zero One // Premultiplied alpha
            Cull Off

            HLSLPROGRAM
                //#pragma enable_d3d11_debug_symbols
                #pragma multi_compile_local_fragment _ RENDER_SUN_DISK
                #pragma multi_compile_local_fragment _ USE_SUN_TEXTURE
                #pragma multi_compile_local_fragment _ USE_SKY_BACKGROUND_TEXTURE
                #pragma fragment FragRender

                
            ENDHLSL
        }

    }
    Fallback Off
}