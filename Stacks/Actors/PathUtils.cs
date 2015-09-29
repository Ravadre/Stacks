using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    static class PathUtils
    {
        public static string FixQueryPath(string path)
        {
            if (path == null)
                return null;
            if (path.Length == 0)
                return path;

            var pb = new StringBuilder(path);

            if (pb[0] != '/')
                pb.Insert(0, '/');
            if (!pb.ToString().StartsWith("/root"))
                pb.Insert(0, "/root");
            if (pb[pb.Length - 1] != '/')
                pb.Append('/');
            return pb.ToString();
        }


        public static void AssertNameForInvalidCharacters(string name)
        {
            if (name == null)
                return;

            if (name.Length == 0)
                throw new Exception("Name can't be empty whitespace");

            var invalidChars = new[] { '$', ' ', '\t', '/', '\\' };

            foreach (var ch in invalidChars.Where(ch => name.IndexOf(ch) != -1))
            {
                throw new Exception("Actor name cannot contain symbol '" + ch + "'");
            }
        }
    }
}
