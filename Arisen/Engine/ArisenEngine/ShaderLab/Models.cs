namespace ArisenEngine.ShaderLab;

using System.Collections.Generic;

// ShaderLab相关模型
public enum PassStage
{
    Vertex,
    Fragment,
    Geometry,
    Hull,
    Domain,
}


public class RenderStateValue
{
    public string stateName; // "Blend", "ZTest" ...
    public bool isReference;
    public string referenceName; // 如果是引用
    
    public enum ValueKind
    {
        None,
        String,
        Int,
        Float
    }

    public ValueKind kind;
    public string stringValue;
    public int intValue;
    public float floatValue;

    public override string ToString()
    {
        return isReference
            ? $"[{referenceName}]"
            : kind switch
            {
                ValueKind.String => stringValue,
                ValueKind.Int => intValue.ToString(),
                ValueKind.Float => floatValue.ToString("0.###"),
                _ => "(null)"
            };
    }
}

public class BlendState
{
    public RenderStateValue SrcColor;
    public RenderStateValue DstColor;
    public RenderStateValue? SrcAlpha;
    public RenderStateValue? DstAlpha;
}

public class ShaderLabShader
{
    public string name;
    public List<Property> properties = new ();
    public List<SubShader> subShaders = new ();
    public List<IncludedHLSL> includedHLSLs = new ();
}

public class Property
{
    public string name;
    public string displayName;
    public string type;
    public string defaultValue;
}

public class SubShader
{
    public List<Pass> passes = new ();
    public List<string> tags = new ();
}

public class IncludedHLSL
{
    public string hlslCode;
    public int passIndex;
    public int subShaderIndex;
}

public class Pass
{
    public string name;
    public string tagsRaw;
    public string hlslCode;
    public List<HlslStruct> hlslStructs = new ();
    public List<HlslVariable> variables = new ();
    public Dictionary<PassStage, string> passStages = new ();
}

public class HlslStruct
{
    public string name;
    public List<HlslStructMember> members = new List<HlslStructMember>();
}

public class HlslStructMember
{
    public string type;
    public string name;
}

public class HlslVariable
{
    public string type;
    public string name;
    public string register; // 如 : register(t0)
}

