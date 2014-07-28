using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.Remote.CodeGen
{
    public static class MethodInfoExtensions
    {
        public static void EnsureNamesAreUnique(this IEnumerable<MethodInfo> methods)
        {
            var hs = new HashSet<string>();

            foreach (var m in methods)
            {
                if (!hs.Add(m.Name))
                    throw new InvalidOperationException("Method names must be unique when using " +
                        "an interface as a actor client proxy");
            }
        }
    }

    public static class TypeExtensions
    {
        public static MethodInfo[] FindValidProxyMethods(this Type type)
        {
            var t = type;
            return t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
                    .OrderBy(m => m.Name)
                    .ToArray();
        }
    }
}
