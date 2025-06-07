namespace ArisenEngine.ShaderLab;

using System.Collections.Generic;

// ShaderLab相关模型

public class ShaderLabShader
{
    public string name;
    public List<Property> properties = new List<Property>();
    public List<SubShader> subShaders = new List<SubShader>();
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
    public List<Pass> passes = new List<Pass>();
    public List<string> tags = new List<string>();
}

public class Pass
{
    public string name;
    public string tagsRaw;
    public string hlslCode;
    public List<HlslStruct> hlslStructs = new List<HlslStruct>();
    public List<HlslVariable> variables = new List<HlslVariable>();
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
