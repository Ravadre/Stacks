using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public static class FormattingExtensions
    {
        public static string FormatDeclaration(this MethodInfo method)
        {
            return method.Name;
        }

        public static string FormatDeclaration(this PropertyInfo property)
        {
            return property.Name;
        }
    }
}
