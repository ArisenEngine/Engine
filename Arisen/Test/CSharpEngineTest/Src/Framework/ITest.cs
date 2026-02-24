namespace CSharpEngineTest.Framework
{
    public enum TestCategory
    {
        Unit,
        Rendering,
        Performance,
        Misc
    }

    public interface ITest
    {
        string GetName();
        TestCategory GetCategory();
        bool Setup();
        bool Run();
        void Teardown();
    }
}
