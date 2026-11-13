/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Infrastructure;

public class DependencyInjectionTests
{
    private sealed class Service;

    private sealed class DisposableProvider : IServiceProvider, IDisposable
    {
        public bool Disposed { get; private set; }

        public object? GetService(Type serviceType) => null;

        public void Dispose() => Disposed = true;
    }

    [Test]
    public void TypeResolver_NullProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TypeResolver(null!));
    }

    [Test]
    public void TypeResolver_Resolve_NullTypeYieldsNull_UnknownTypeYieldsNull_KnownTypeYieldsInstance()
    {
        var services = new ServiceCollection();
        var instance = new Service();
        services.AddSingleton(instance);
        var resolver = new TypeResolver(services.BuildServiceProvider());

        Assert.That(resolver.Resolve(null), Is.Null);
        Assert.That(resolver.Resolve(typeof(string)), Is.Null);
        Assert.That(resolver.Resolve(typeof(Service)), Is.SameAs(instance));
    }

    [Test]
    public void TypeResolver_Dispose_DisposesDisposableProviderOnly()
    {
        var provider = new DisposableProvider();

        new TypeResolver(provider).Dispose();

        Assert.That(provider.Disposed, Is.True);
        Assert.DoesNotThrow(() => new TypeResolver(new ServiceCollection().BuildServiceProvider(validateScopes: false)).Dispose());
    }

    [Test]
    public void TypeRegistrar_RegisterLazy_RejectsANullFactoryNamingTheParameter()
    {
        var registrar = new TypeRegistrar(new ServiceCollection());

        var ex = Assert.Throws<ArgumentNullException>(() => registrar.RegisterLazy(typeof(string), null!));

        Assert.That(ex!.ParamName, Is.EqualTo("func"));
    }

    [Test]
    public void TypeRegistrar_RegistersTypesInstancesAndLazyFactories()
    {
        var registrar = new TypeRegistrar(new ServiceCollection());
        var instance = new Service();
        registrar.RegisterInstance(typeof(Service), instance);
        registrar.Register(typeof(IDisposable), typeof(MemoryStream));
        registrar.RegisterLazy(typeof(string), () => "lazy");

        var resolver = registrar.Build();

        Assert.That(resolver.Resolve(typeof(Service)), Is.SameAs(instance));
        Assert.That(resolver.Resolve(typeof(IDisposable)), Is.TypeOf<MemoryStream>());
        Assert.That(resolver.Resolve(typeof(string)), Is.EqualTo("lazy"));
    }
}
