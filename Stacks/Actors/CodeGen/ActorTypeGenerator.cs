using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class ActorTypeGenerator
    {
        private static readonly Dictionary<Tuple<Type, Type>, Type> cachedTypes = new Dictionary<Tuple<Type, Type>, Type>();

        private AssemblyName asmName;
        private AssemblyBuilder asmBuilder;
        private ModuleBuilder moduleBuilder;
        private TypeBuilder wrapperBuilder;
        private object actorImplementation;
        private Type actorInterface;

        public ActorTypeGenerator()
        {
            
        }

        private bool TryGetCachedType(out Type wrapperType)
        {
            var actorType = actorImplementation.GetType();
            var key = Tuple.Create(actorType, actorInterface);

            lock (cachedTypes)
            {
                return cachedTypes.TryGetValue(key, out wrapperType);
            }
        }

        private void SaveWrapperToCache(Type wrapperType)
        {
            var actorType = actorImplementation.GetType();
            var key = Tuple.Create(actorType, actorInterface);

            lock (cachedTypes)
            {
                cachedTypes[key] = wrapperType;
            }
        }

        public Type GenerateType(object actorImplementation, Type actorInterface)
        {
            this.actorInterface = actorInterface;
            this.actorImplementation = actorImplementation;

            Type wrapperType;
            if (TryGetCachedType(out wrapperType))
                return wrapperType;

            CreateBuilders();
            CreateWrapperType();
            return FinishWrapperCreation();
        }

        private void CreateWrapperType()
        {
            wrapperBuilder = moduleBuilder.DefineType(actorInterface.Name + "$Wrapper",
                TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public,
                null, new [] { typeof(IActor), actorInterface } );


        }

        private Type FinishWrapperCreation()
        {
            var wrapperType = wrapperBuilder.CreateType();
            SaveWrapperToCache(wrapperType);
            asmBuilder.Save(asmName.Name + ".dll");

            return wrapperType;
        }

        private void CreateBuilders()
        {
            asmName =
                new AssemblyName("Stacks_LocalActorWrapper_" + actorImplementation.GetType().FullName + "_" +
                                 actorInterface.FullName);

            asmBuilder = AppDomain.CurrentDomain
                                  .DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
            moduleBuilder = asmBuilder.DefineDynamicModule(asmName + ".dll");
            
        }
    }
}
