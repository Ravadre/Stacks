using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public class ReactiveClientModelBuilder<T>
    {
        private Dictionary<string, int> propertyMappings;

        public bool HasMapping(string property)
        {
            return propertyMappings.ContainsKey(property);
        }
        
        public int? TryGetMapping(string property)
        {
            int v;
            if (propertyMappings.TryGetValue(property, out v))
                return v;
            return null;
        }

        public ReactiveClientModelBuilder()
        {
            propertyMappings = new Dictionary<string, int>();
        }

        public Mapping Packet<R>(Expression<Func<T, R>> expr)
        {
            LambdaExpression e = (LambdaExpression)expr;
            var e2 = (MemberExpression)e.Body;
            
            return new Mapping(propertyMappings, e2.Member.Name);
        }

        public class Mapping
        {
            private Dictionary<string, int> propertyMappings;
            private string propName;

            public Mapping(Dictionary<string, int> propertyMappings, string propName)
            {
                this.propertyMappings = propertyMappings;
                this.propName = propName;
            }

            public void HasId(int id)
            {
                propertyMappings[propName] = id;
            }
        }
    }
}
