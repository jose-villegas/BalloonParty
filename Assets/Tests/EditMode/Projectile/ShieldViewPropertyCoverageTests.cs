using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    /// Static-analysis regression test for the MaterialPropertyBlock split-push bug.
    ///
    /// The bug: two code paths (Update and PushProperties) wrote to the same MaterialPropertyBlock.
    /// Tween callbacks calling PushProperties only wrote dissolve/reveal properties, overwriting
    /// deform/squash values that Update had set. New features appeared broken because their shader
    /// values were erased every tween frame.
    ///
    /// The fix: a single WriteAllProperties method writes ALL shader properties, and only
    /// Update() and Reset() call SetPropertyBlock — tween callbacks only mutate backing arrays.
    ///
    /// This test ensures every shader property ID declared in the class is referenced inside
    /// WriteAllProperties, so a future developer adding a new shader property cannot forget to
    /// include it in the unified push path.
    /// </summary>
    [TestFixture]
    public class ShieldViewPropertyCoverageTests
    {
        private const string SourceRelativePath =
            "Assets/Source/Projectile/View/ProjectileShieldView.cs";

        // Matches: private static readonly int FooId = Shader.PropertyToID("_Foo");
        private static readonly Regex PropertyIdDeclaration =
            new(@"static\s+readonly\s+int\s+(\w+Id)\s*=\s*Shader\.PropertyToID", RegexOptions.Compiled);

        // Matches: _block.SetFloat(FooId, ...) or _block.SetVector(FooId, ...) etc.
        private static readonly Regex BlockSetCall =
            new(@"_block\.Set\w+\(\s*(\w+Id)", RegexOptions.Compiled);

        private string _sourceText;
        private string _pushPropertiesBody;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Find the project root (walk up from Assembly location to the repo root)
            var projectRoot = FindProjectRoot();
            var fullPath = Path.Combine(projectRoot, SourceRelativePath);

            Assert.IsTrue(File.Exists(fullPath),
                $"Source file not found at {fullPath}. Ensure the test runs from the project root.");

            _sourceText = File.ReadAllText(fullPath);
            _pushPropertiesBody = ExtractMethodBody(_sourceText, "WriteAllProperties");

            Assert.IsNotNull(_pushPropertiesBody,
                "Could not extract WriteAllProperties method body from source.");
        }

        [Test]
        public void WriteAllProperties_ReferencesAllDeclaredShaderPropertyIds()
        {
            // Collect all declared shader property IDs
            var declaredIds = PropertyIdDeclaration
                .Matches(_sourceText)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.IsNotEmpty(declaredIds,
                "No shader property IDs found — regex may be stale or file changed.");

            // Collect IDs referenced in WriteAllProperties via _block.Set*() calls
            var pushedIds = new HashSet<string>(
                BlockSetCall
                    .Matches(_pushPropertiesBody)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value));

            var missing = declaredIds.Where(id => !pushedIds.Contains(id)).ToList();

            Assert.IsEmpty(missing,
                "WriteAllProperties does not reference these shader property IDs, " +
                "risking the split-push bug where tween callbacks erase values set by Update():\n  " +
                string.Join("\n  ", missing));
        }

        [Test]
        public void AllDeclaredPropertyIds_AreSetSomewhere()
        {
            // Every declared ID should appear in at least one _block.Set*() call in the
            // entire file (Update or PushProperties). A declared-but-never-set ID is dead code
            // or a forgotten integration.
            var declaredIds = PropertyIdDeclaration
                .Matches(_sourceText)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            var allSetIds = new HashSet<string>(
                BlockSetCall
                    .Matches(_sourceText)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value));

            var neverSet = declaredIds.Where(id => !allSetIds.Contains(id)).ToList();

            Assert.IsEmpty(neverSet,
                "These shader property IDs are declared but never pushed to the MaterialPropertyBlock:\n  " +
                string.Join("\n  ", neverSet));
        }

        /// <summary>
        /// Extracts the body of a method by name using brace-counting.
        /// </summary>
        private static string ExtractMethodBody(string source, string methodName)
        {
            var methodPattern = new Regex(
                @"(private|protected|public|internal)?\s*(static\s+)?(void|[\w<>\[\]]+)\s+" +
                Regex.Escape(methodName) + @"\s*\(");

            var match = methodPattern.Match(source);

            if (!match.Success)
            {
                return null;
            }

            var startIndex = source.IndexOf('{', match.Index);

            if (startIndex < 0)
            {
                return null;
            }

            var depth = 0;

            for (var i = startIndex; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        return source.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Walk up from the current directory to find the project root containing Assets/.
        /// </summary>
        private static string FindProjectRoot()
        {
            // In Unity EditMode tests, the working directory is the project root.
            var cwd = Directory.GetCurrentDirectory();

            if (Directory.Exists(Path.Combine(cwd, "Assets")))
            {
                return cwd;
            }

            // Fallback: walk up from the assembly location
            var dir = Path.GetDirectoryName(typeof(ShieldViewPropertyCoverageTests).Assembly.Location);

            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, "Assets")))
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir);
            }

            return cwd; // Last resort
        }
    }
}
