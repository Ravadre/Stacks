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

        private readonly List<IActorCompilerStrategy> actorCompilers; 

        public ActorTypeGenerator()
        {
            actorCompilers = new List<IActorCompilerStrategy>
            {
                new TaskMethodsCompiler(),
                new ObservablePropertiesCompiler(),
                new ObservableMethodCompiler()
            };

        }

        private bool TryGetCachedType(out Type wrapperType)
        {
            wrapperType = null;
#if DEBUG_CODEGEN
            return false;
#else
            var actorType = actorImplementation.GetType();
            var key = Tuple.Create(actorType, actorInterface);

            lock (cachedTypes)
            {
                return cachedTypes.TryGetValue(key, out wrapperType);
            }
#endif
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
            foreach (var method in actorInterface.FindValidMethods(onlyPublic: false))
            {
                var compiler = actorCompilers.FirstOrDefault(c => c.CanCompile(method));

                if (compiler == null)
                {
                    throw new Exception(
                        $"Could not find compiler for method {method.Info.FormatDeclaration()}. " +
                        "If method has non standard declaration, maybe appropriate compiler strategy was not registered? " +
                        "Additional strategies can be registered using ActorCompilerStrategy.");
                }

                compiler.Implement(method, actorInterface, wrapperBuilder);
            }
        }

        private void GeneratePropertiesImplementation()
        {
            foreach (var property in actorInterface.FindValidProperties(onlyPublic: false))
            {
                var compiler = actorCompilers.FirstOrDefault(p => p.CanCompile(property));

                if (compiler == null)
                {
                    throw new Exception(
                        $"Could not find compiler for method {property.Info.FormatDeclaration()}. " +
                        "If method has non standard declaration, maybe appropriate compiler strategy was not registered? " +
                        "Additional strategies can be registered using ActorCompilerStrategy.");
                }

                compiler.Implement(property, actorInterface, wrapperBuilder);
            }
        }

        private void ImplementWrapperConstructor()
        {
            var ctor = wrapperBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName, CallingConventions.HasThis,
                new[] {typeof (IActor)});

            var il = ctor.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(ActorWrapperBase).GetConstructor(new [] { typeof(IActor) }));
            il.Emit(OpCodes.Ret);
        }

        
        private void CreateWrapperType()
        {
            wrapperBuilder = moduleBuilder.DefineType(actorInterface.Name + "$Wrapper",
                TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public,
                typeof(ActorWrapperBase), new [] { typeof(IActor), actorInterface } );

            ImplementWrapperConstructor();
            GenerateMethodsImplementation();
            GeneratePropertiesImplementation();
        }

        [Conditional("DEBUG_CODEGEN")]
        private void SaveWrapperToFile()
        {
            asmBuilder.Save(asmName.Name + ".dll");
        }

        private AssemblyBuilder CreateAssemblyBuilder()
        {
#if DEBUG_CODEGEN
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
