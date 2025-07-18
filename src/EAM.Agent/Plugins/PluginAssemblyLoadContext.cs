using System.Reflection;
using System.Runtime.Loader;

namespace EAM.Agent.Plugins;

public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _pluginName;
    private readonly string _pluginPath;
    private readonly AssemblyDependencyResolver _resolver;

    public PluginAssemblyLoadContext(string pluginName, string pluginPath) : base(pluginName, isCollectible: true)
    {
        _pluginName = pluginName;
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Primeiro, tentar resolver usando o resolver de dependências
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Se não encontrar, permitir que o contexto padrão carregue
        // Isso é importante para assemblies do sistema e do .NET
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    public string PluginName => _pluginName;
    public string PluginPath => _pluginPath;
}