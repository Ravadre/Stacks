using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.Remote.CodeGen
{
    static class MethodInfoMappingExtensions
    {
        public static void EnsureNamesAreUnique(this IEnumerable<MethodInfoMapping> methods)
        {
            var hs = new HashSet<string>();

            foreach (var m in methods)
            {
                if (!hs.Add(m.PublicName))
                    throw new InvalidOperationException("Method names must be unique when using " +
                        "an interface as an actor proxy");
            }
        }
    }

    static class PropertyInfoMappingExtensions
    {
        public static void EnsureNamesAreUnique(this IEnumerable<PropertyInfoMapping> properties)
        {
            //This might be unnecessary?
            var hs = new HashSet<string>();

            foreach (var p in properties)
            {
                if (!hs.Add(p.PublicName))
                    throw new InvalidOperationException("Property names must be unique when using " +
                        "an interface as an actor proxy");
            }
        }
    }

    public struct MethodInfoMapping
    {
        public MethodInfo Info { get; private set; }
        public MethodInfo InterfaceInfo { get; private set; }
        public string PublicName { get; private set; }
        public string MappedName { get; private set; }

        public MethodInfoMapping(MethodInfo info, MethodInfo interfaceInfo, string publicName, string mappedName) 
            : this()
        {
            Info = info;
            InterfaceInfo = interfaceInfo;
            PublicName = publicName;
            MappedName = mappedName;
        }
    }

    public struct PropertyInfoMapping
    {
        public PropertyInfo Info { get; private set; }
        public PropertyInfo InterfaceInfo { get; private set; }
        public string PublicName { get; private set; }
        public string MappedName { get; private set; }

        public PropertyInfoMapping(PropertyInfo propInfo, PropertyInfo interfacePropInfo, string publicName, string mappedName)
            : this()
        {
            Info = propInfo;
            InterfaceInfo = interfacePropInfo;
            PublicName = publicName;
            MappedName = mappedName;
        }
    }

    static class TypeExtensions
    {
        public static MethodInfoMapping[] FindValidProxyMethods(this Type type, bool onlyPublic)
        {
            var t = type;
            var publicMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(m => typeof (Task).IsAssignableFrom(m.ReturnType))
                                 .Select(m => new MethodInfoMapping(m, m, m.Name, m.Name));

            if (onlyPublic)
            {
                return publicMethods
                    .OrderBy(m => m.PublicName)
                    .ToArray();
            }
            else
            {
                var mappings = new Dictionary<string, MethodInfo>();
                
                // Role of this method is to take not only public methods, but also
                // private ones, as long as those methods are explicit interface implementations.
                // To check for this case, interface maps are checked.
                // This makes standard interface implementation pattern for F# viable
                // for server side proxies.
                foreach (var mapping in type.GetInterfaces().Select(iFace => t.GetInterfaceMap(iFace)))
                {
                    for (var i = 0; i < Math.Min(mapping.InterfaceMethods.Length, mapping.TargetMethods.Length); ++i)
                    {
                        mappings[mapping.TargetMethods[i].Name] = mapping.InterfaceMethods[i];
                    }
                }

                var overridenMethods = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                        .Where(m => typeof (Task).IsAssignableFrom(m.ReturnType))
                                        .Where(m => mappings.ContainsKey(m.Name))
                                        .Select(m => new MethodInfoMapping(m, mappings[m.Name], mappings[m.Name].Name, m.Name));

                return publicMethods
                    .Concat(overridenMethods)
                    .OrderBy(m => m.PublicName)
                    .ToArray();
            }
        }
        
        public static PropertyInfoMapping[] FindValidObservableProperties(this Type type, bool onlyPublic)
        {
            var t = type;
            var publicProperties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => p.PropertyType.IsGenericType &&
                                                typeof (IObservable<>) == p.PropertyType.GetGenericTypeDefinition())
                                    .Select(m => new PropertyInfoMapping(m, m, m.Name, m.Name));

            if (onlyPublic)
            {
                return publicProperties.OrderBy(p => p.PublicName).ToArray();
            }
            else
            {
                  var mappings = new Dictionary<string, MethodInfo>();
                
                // Role of this method is to take not only public methods, but also
                // private ones, as long as those methods are explicit interface implementations.
                // To check for this case, interface maps are checked.
                // This makes standard interface implementation pattern for F# viable
                // for server side proxies.
                foreach (var mapping in type.GetInterfaces().Select(iFace => t.GetInterfaceMap(iFace)))
                {
                    for (var i = 0; i < Math.Min(mapping.InterfaceMethods.Length, mapping.TargetMethods.Length); ++i)
                    {
                        mappings[mapping.TargetMethods[i].Name] = mapping.InterfaceMethods[i];
                    }
                }

                Func<MethodInfo, PropertyInfo> findPropByGetMethod = m =>
                    m.DeclaringType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(p => p.GetGetMethod(true).Name == m.Name)
                     .First();

                var overridenProperties = t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
                                        .Where(p => p.PropertyType.IsGenericType &&
                                            typeof(IObservable<>) == p.PropertyType.GetGenericTypeDefinition())
                                        .Where(m => mappings.ContainsKey(m.GetMethod.Name))
                                        .Select(p => new PropertyInfoMapping(
                                                        p, 
                                                        findPropByGetMethod(mappings[p.GetGetMethod(true).Name]),
                                                        findPropByGetMethod(mappings[p.GetGetMethod(true).Name]).Name, 
                                                        p.Name));

                return publicProperties
                    .Concat(overridenProperties)
                    .OrderBy(m => m.PublicName)
                    .ToArray();
            }
        }

        private static string GetObservableMethodName(MethodInfo mi)
        {
            // This should handle special F# case.
            // Interface property is implemented as get_PROPERTYNAME method
            if (mi.IsSpecialName)
            {
                if (mi.Name.StartsWith("get_"))
                    return mi.Name.Remove(0, 4);

                return mi.Name;
            }
            else
            {
                return mi.Name;
            }
        }

        public static MethodInfoMapping[] FindValidObservableMethods(this Type type, bool onlyPublic)
        {
            var propertyMethods =
                new HashSet<string>(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .SelectMany(p => new[]
                        {
                            p.GetMethod != null ? p.GetMethod.Name : null,
                            p.SetMethod != null ? p.SetMethod.Name : null
                        })
                        .Where(n => n != null));

            var t = type;
            var publicMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => p.ReturnType.IsGenericType &&
                                                typeof(IObservable<>) == p.ReturnType.GetGenericTypeDefinition())
                                    .Where(m => m.GetParameters().Length == 0)
                                    .Where(m => !propertyMethods.Contains(m.Name))
                                    .Select(m => new MethodInfoMapping(m, m, GetObservableMethodName(m), m.Name));

            if (onlyPublic)
            {
                return publicMethods.OrderBy(p => p.PublicName).ToArray();
            }
            else
            {
                var mappings = new Dictionary<string, MethodInfo>();

                // Role of this method is to take not only public methods, but also
                // private ones, as long as those methods are explicit interface implementations.
                // To check for this case, interface maps are checked.
                // This makes standard interface implementation pattern for F# viable
                // for server side proxies.
                foreach (var mapping in type.GetInterfaces().Select(iFace => t.GetInterfaceMap(iFace)))
                {
                    for (var i = 0; i < Math.Min(mapping.InterfaceMethods.Length, mapping.TargetMethods.Length); ++i)
                    {
                        mappings[mapping.TargetMethods[i].Name] = mapping.InterfaceMethods[i];
                    }
                }

                var overridenMethods = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                        .Where(p => p.ReturnType.IsGenericType &&
                                                typeof(IObservable<>) == p.ReturnType.GetGenericTypeDefinition())
                                        .Where(m => m.GetParameters().Length == 0)
                                        .Where(m => mappings.ContainsKey(m.Name))
                                        .Where(m => !propertyMethods.Contains(m.Name))
                                        .Select(m => new MethodInfoMapping(m, mappings[m.Name], GetObservableMethodName(mappings[m.Name]), m.Name));

                return publicMethods
                    .Concat(overridenMethods)
                    .OrderBy(m => m.PublicName)
                    .ToArray();
            }
        }
    }
}
