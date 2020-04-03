using Microsoft.Extensions.DependencyInjection;
using Sandbox.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sandbox.Implement.Runner
{
    public class DependencyInjectionRunner : IRunner
    {
        public void RunExample()
        {
            var services = new ServiceCollection();
            services.AddSingleton<A>();
            services.AddTransient<B>();
            services.AddScoped<C>();
            var serviceProvider = services.BuildServiceProvider();

            Console.WriteLine("Step 1: creating transient B...");
            Console.WriteLine("Create A first because A is dependency of B, then create B and auto inject A into B...");
            var b = serviceProvider.GetService<B>();
            Console.WriteLine("Step 1: done");
            Console.WriteLine();

            Console.WriteLine("Step 2: creating transient B...");
            Console.WriteLine("No need to create A because A is singleton, but create B because B is transient...");
            b = serviceProvider.GetService<B>();
            Console.WriteLine("Step 2: done");
            Console.WriteLine();

            Console.WriteLine("Step 3: creating singleton A...");
            Console.WriteLine("Cannot create A again because A is singleton...");
            var a = serviceProvider.GetService<A>();
            Console.WriteLine("Step 3: done");
            Console.WriteLine();

            Console.WriteLine("Step 4: creating scoped C...");
            Console.WriteLine("First time create C in this scope...");
            var c = serviceProvider.GetService<C>();
            Console.WriteLine("Step 4: done");
            Console.WriteLine();

            Console.WriteLine("Step 5: creating scoped C...");
            Console.WriteLine("Cannot create C again in this scope...");
            c = serviceProvider.GetService<C>();
            Console.WriteLine("Step 5: done");
            Console.WriteLine();

            Console.WriteLine("--New scope--");
            using (var scope = serviceProvider.CreateScope())
            {
                Console.WriteLine("Step 1: creating singleton A...");
                Console.WriteLine("Cannot create A again because A is singleton...");
                a = scope.ServiceProvider.GetService<A>();
                Console.WriteLine("Step 1: done");
                Console.WriteLine();

                Console.WriteLine("Step 2: creating scoped C...");
                Console.WriteLine("First time create C in this scope...");
                c = scope.ServiceProvider.GetService<C>();
                Console.WriteLine("Step 2: done");
            }
        }
    }

    abstract class ABase
    {
        public void ShowInfo() => Console.WriteLine($"{GetType().Name} - {GetHashCode()}");
        public void NotifyCreate() => Console.WriteLine($"{GetType().Name} created");
    }

    class A : ABase
    {
        public A() => NotifyCreate();
    }

    class B : ABase
    {
        A _dependency;
        public B(A dependency)
        {
            dependency = _dependency;
            NotifyCreate();
        }
    }

    class C : ABase
    {
        public C() => NotifyCreate();
    }
}
