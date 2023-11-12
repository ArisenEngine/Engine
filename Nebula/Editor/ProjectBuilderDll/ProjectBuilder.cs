using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.GameBuilder
{
    public enum TargetPlatform
    {
        Windows,
    }

    public static class ProjectBuilder
    {

        public static string GAME_PROJECT_REFERENCE_REPLACE_KEY = "<!--###-GAME_PROJECT_REFERENCE-###-->";


        public static void BuildProject(TargetPlatform target)
        {
            Console.WriteLine($"Start building project for target platform : {target} ");
        }
    }
}
