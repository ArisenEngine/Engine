using System;
using System.ComponentModel;

namespace EditorTest;

[Flags]
public enum TestFlags
{
    None = 0,
    Visible = 1 << 0,
    Active = 1 << 1,
    Solid = 1 << 2,
    Transparent = 1 << 3,
    Everything = Visible | Active | Solid | Transparent
}

public class InspectorTestObject
{
    [Category("General")]
    [DisplayName("Object Name")]
    [Description("The name of this test object")]
    public string Name { get; set; } = "Test Object";

    [Category("General")]
    public bool IsEnabled { get; set; } = true;

    [Category("Numbers")]
    public int IntegerVal { get; set; } = 42;

    [Category("Numbers")]
    public float FloatVal { get; set; } = 3.14f;

    [Category("Enumerations")]
    public DayOfWeek NormalEnum { get; set; } = DayOfWeek.Monday;

    [Category("Enumerations")]
    public TestFlags BitmaskFlags { get; set; } = TestFlags.Visible | TestFlags.Active;

    [Category("References")]
    [Description("An object reference field that supports Drag and Drop from the Hierarchy")]
    public object? ReferenceField { get; set; }

    public override string ToString() => Name;
}
