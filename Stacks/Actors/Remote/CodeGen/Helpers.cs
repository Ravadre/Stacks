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
                        "an interface as an actor proxy");
            }
        }
    }

    public static class PropertyInfoExtensions
    {
        public static void EnsureNamesAreUnique(this IEnumerable<PropertyInfo> properties)
        {
            //This might be unnecessary?
            var hs = new HashSet<string>();

            foreach (var p in properties)
            {
                if (!hs.Add(p.Name))
                    throw new InvalidOperationException("Property names must be unique when using " +
                        "an interface as an actor proxy");
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

        public static PropertyInfo[] FindValidObservableProperties(this Type type)
        {
            var t = type;
            return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType.IsGenericType && 
                                typeof(IObservable<>) == p.PropertyType.GetGenericTypeDefinition())
                    .ToArray();
        }
    }
}
