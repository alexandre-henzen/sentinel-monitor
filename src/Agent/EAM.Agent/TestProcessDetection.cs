using System.Diagnostics;
using System.Text;
using EAM.Agent.Core.Windows;

namespace EAM.Agent;

public class TestProcessDetection
{
    public static void TestCurrentProcess()
    {
        Console.WriteLine("=== TESTE DETECÇÃO DE PROCESSOS ===");
        
        // Teste 1: Listar todos os processos msedge
        Console.WriteLine("\n1. Processos Edge encontrados:");
        var edgeProcesses = Process.GetProcessesByName("msedge");
        foreach (var edgeProcess in edgeProcesses)
        {
            try
            {
                Console.WriteLine($"   PID: {edgeProcess.Id} | Nome: {edgeProcess.ProcessName} | Título: {edgeProcess.MainWindowTitle}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   PID: {edgeProcess.Id} | Nome: {edgeProcess.ProcessName} | Erro: {ex.Message}");
            }
        }
        
        // Teste 2: GetForegroundWindow atual
        Console.WriteLine("\n2. Janela em primeiro plano:");
        var foregroundWindow = WindowsApi.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            Console.WriteLine("   ❌ Nenhuma janela em primeiro plano");
            return;
        }
        
        var processId = WindowsApi.GetWindowThreadProcessId(foregroundWindow, out _);
        Console.WriteLine($"   Foreground Window Handle: {foregroundWindow}");
        Console.WriteLine($"   Process ID: {processId}");
        
        if (processId == 0)
        {
            Console.WriteLine("   ❌ ProcessId = 0");
            return;
        }
        
        // Teste 3: Verificar se processo existe
        Process? process = null;
        try
        {
            process = Process.GetProcessById((int)processId);
            Console.WriteLine($"   ✅ Processo encontrado: {process.ProcessName}");
            
            var titleBuffer = new StringBuilder(256);
            var windowTitleLength = WindowsApi.GetWindowText(foregroundWindow, titleBuffer, 256);
            var windowTitle = titleBuffer.ToString();
            Console.WriteLine($"   Título da janela: '{windowTitle}'");
            
            // Teste 4: Verificar se é Edge
            if (process.ProcessName.ToLowerInvariant() == "msedge")
            {
                Console.WriteLine("   🎉 EDGE DETECTADO!");
            }
            else
            {
                Console.WriteLine($"   ℹ️  Não é Edge, é: {process.ProcessName}");
            }
            
        }
        catch (ArgumentException)
        {
            Console.WriteLine($"   ❌ Processo {processId} NÃO EXISTE");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erro: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
        
        Console.WriteLine("\n=== FIM DO TESTE ===");
    }
}