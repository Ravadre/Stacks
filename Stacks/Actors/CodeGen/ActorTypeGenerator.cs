using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;

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

        private List<IActorCompilerStrategy> actorCompilers; 

        public ActorTypeGenerator()
        {
            actorCompilers = new List<IActorCompilerStrategy>
            {
                new TaskMethodsCompiler()  
            };

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

        private void GenerateMethodsImplementation()
        {
            foreach (var method in actorInterface.GetMethods())
            {
                var compiler = actorCompilers.FirstOrDefault(c => c.CanCompile(method));

                if (compiler == null)
                {
                    throw new Exception(
                        string.Format("Could not find compiler for method {0}. " +
                                      "If method has non standard declaration, maybe appropriate compiler strategy was not registered? " +
                                      "Additional strategies can be registered using ActorCompilerStrategry.",
                            method.FormatDeclaration()));
                }

                compiler.Implement(method, wrapperBuilder);
            }
        }

        private void CreateWrapperType()
        {
            wrapperBuilder = moduleBuilder.DefineType(actorInterface.Name + "$Wrapper",
                TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public,
                null, new [] { typeof(IActor), actorInterface } );

            
        }

        [Conditional("DEBUG")]
        private void SaveWrapperToFile()
        {
            asmBuilder.Save(asmName.Name + ".dll");
        }

        private AssemblyBuilder CreateAssemblyBuilder()
        {
#if DEBUG
            return AppDomain.CurrentDomain
                            .DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
#else
            return AppDomain.CurrentDomain
                            .DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
#endif
        }


        private Type FinishWrapperCreation()
        {
            var wrapperType = wrapperBuilder.CreateType();
            SaveWrapperToCache(wrapperType);
            SaveWrapperToFile();

            return wrapperType;
        }

        private void CreateBuilders()
        {
            asmName =
                new AssemblyName("Stacks_LocalActorWrapper_" + actorImplementation.GetType().FullName + "_" +
                                 actorInterface.FullName);

            asmBuilder = CreateAssemblyBuilder();
            moduleBuilder = asmBuilder.DefineDynamicModule(asmName + ".dll");
            
        }
    }
}
