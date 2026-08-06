using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace TiezhuToolbox.Modules.GearScan;

/// <summary>使用 Windows 内置 pktmon 完成一次无需额外驱动的抓包。</summary>
public sealed class GearScanCapture : IAsyncDisposable
{
    private readonly string _sessionDirectory;
    private readonly string _etlPath;
    private readonly string _pcapngPath;
    private readonly string _stopPath;
    private readonly string _abortPath;
    private readonly string _startedPath;
    private readonly string _completedPath;
    private readonly string _errorPath;
    private Process? _helperProcess;
    private bool _stopped;

    public GearScanCapture()
    {
        _sessionDirectory = Path.Combine(AppPaths.UserRoot, "gear-scan", Guid.NewGuid().ToString("N"));
        _etlPath = Path.Combine(_sessionDirectory, "capture.etl");
        _pcapngPath = Path.Combine(_sessionDirectory, "capture.pcapng");
        _stopPath = Path.Combine(_sessionDirectory, "stop.signal");
        _abortPath = Path.Combine(_sessionDirectory, "abort.signal");
        _startedPath = Path.Combine(_sessionDirectory, "started.signal");
        _completedPath = Path.Combine(_sessionDirectory, "completed.signal");
        _errorPath = Path.Combine(_sessionDirectory, "error.txt");
    }

    public string DiagnosticDirectory => _sessionDirectory;
    public string PcapngPath => _pcapngPath;

    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)
                                      && File.Exists(Path.Combine(Environment.SystemDirectory, "pktmon.exe"));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException("装备扫描需要 Windows 10 1809 或更高版本自带的 pktmon");

        Directory.CreateDirectory(_sessionDirectory);
        var scriptPath = Path.Combine(_sessionDirectory, "capture.ps1");
        var script = BuildCaptureScript(Environment.ProcessId);
        await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = true,
            Verb = IsAdministrator() ? string.Empty : "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            _helperProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动 Windows Packet Monitor");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("已取消管理员授权，未开始扫描", ex, cancellationToken);
        }

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(_startedPath))
                return;
            if (File.Exists(_errorPath))
                throw new InvalidOperationException("Packet Monitor 启动失败：" + await File.ReadAllTextAsync(_errorPath, cancellationToken));
            if (_helperProcess.HasExited)
                throw new InvalidOperationException("Packet Monitor 在开始抓包前意外退出");
            await Task.Delay(200, cancellationToken);
        }

        await SignalStopAsync();
        throw new TimeoutException("等待 Packet Monitor 启动超时");
    }

    public async Task<IReadOnlyList<string>> StopAndExtractAsync(CancellationToken cancellationToken)
    {
        if (_helperProcess == null)
            throw new InvalidOperationException("扫描尚未开始");
        if (_stopped)
            throw new InvalidOperationException("扫描已经停止");

        _stopped = true;
        await SignalStopAsync();

        var exitTask = _helperProcess.WaitForExitAsync(cancellationToken);
        var completed = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
        if (completed != exitTask)
            throw new TimeoutException("停止 Packet Monitor 超时；它会在本程序退出后自动停止");
        await exitTask;

        if (File.Exists(_errorPath))
            throw new InvalidOperationException("Packet Monitor 抓包失败：" + await File.ReadAllTextAsync(_errorPath, cancellationToken));
        if (!File.Exists(_completedPath) || !File.Exists(_etlPath))
            throw new InvalidOperationException("Packet Monitor 未生成完整抓包文件");

        await ConvertToPcapngAsync(cancellationToken);
        return await Task.Run(() => PcapngPayloadExtractor.ExtractHexStreams(_pcapngPath), cancellationToken);
    }

    /// <summary>主窗体退出时同步写入停止信号；提权辅助进程也会监控主进程 PID 作为兜底。</summary>
    public void RequestStop()
    {
        try
        {
            Directory.CreateDirectory(_sessionDirectory);
            File.WriteAllText(_stopPath, string.Empty);
        }
        catch
        {
            // 主进程退出后辅助进程仍会通过 PID 消失停止抓包。
        }
    }

    /// <summary>主程序退出时停止扫描并删除可能包含隐私的抓包。</summary>
    public void RequestAbort()
    {
        try
        {
            Directory.CreateDirectory(_sessionDirectory);
            File.WriteAllText(_abortPath, string.Empty);
            if (_helperProcess is null or { HasExited: true })
                Cleanup(keepDiagnostics: false);
        }
        catch
        {
            // 辅助进程仍会通过主进程 PID 消失停止，并尽力删除 ETL。
        }
    }

    public void Cleanup(bool keepDiagnostics)
    {
        if (keepDiagnostics || !Directory.Exists(_sessionDirectory))
            return;
        try
        {
            Directory.Delete(_sessionDirectory, recursive: true);
        }
        catch
        {
            // 清理失败不影响扫描结果，下次启动仍可由用户手动删除诊断目录。
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_helperProcess is { HasExited: false })
        {
            await SignalStopAsync();
            try
            {
                await _helperProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
                // 提权辅助进程还会监控主进程 PID；主程序退出后会自行停止 pktmon。
            }
        }
        _helperProcess?.Dispose();
    }

    private async Task ConvertToPcapngAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "pktmon.exe"),
            Arguments = $"etl2pcap \"{_etlPath}\" --out \"{_pcapngPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 pktmon ETL 转换");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0 || !File.Exists(_pcapngPath))
            throw new InvalidOperationException($"抓包格式转换失败：{(string.IsNullOrWhiteSpace(error) ? output : error).Trim()}");
    }

    private Task SignalStopAsync()
    {
        Directory.CreateDirectory(_sessionDirectory);
        return File.WriteAllTextAsync(_stopPath, string.Empty);
    }

    private string BuildCaptureScript(int ownerProcessId)
    {
        static string Quote(string path) => path.Replace("'", "''", StringComparison.Ordinal);
        return $$"""
            $ErrorActionPreference = 'Stop'
            $etlPath = '{{Quote(_etlPath)}}'
            $stopPath = '{{Quote(_stopPath)}}'
            $abortPath = '{{Quote(_abortPath)}}'
            $startedPath = '{{Quote(_startedPath)}}'
            $completedPath = '{{Quote(_completedPath)}}'
            $errorPath = '{{Quote(_errorPath)}}'
            $captureStarted = $false
            try {
                & "$env:SystemRoot\System32\pktmon.exe" start --capture --comp nics --pkt-size 0 --file-name $etlPath --file-size 256 --log-mode circular | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "pktmon start 返回错误代码 $LASTEXITCODE；请确认没有其他抓包任务正在运行" }
                $captureStarted = $true
                [IO.File]::WriteAllText($startedPath, '')
                while (-not [IO.File]::Exists($stopPath) -and -not [IO.File]::Exists($abortPath) -and (Get-Process -Id {{ownerProcessId}} -ErrorAction SilentlyContinue)) {
                    Start-Sleep -Milliseconds 300
                }
                & "$env:SystemRoot\System32\pktmon.exe" stop | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "pktmon stop 返回错误代码 $LASTEXITCODE" }
                $captureStarted = $false
                if ([IO.File]::Exists($abortPath) -or -not (Get-Process -Id {{ownerProcessId}} -ErrorAction SilentlyContinue)) {
                    Remove-Item -LiteralPath $etlPath -Force -ErrorAction SilentlyContinue
                    exit 0
                }
                [IO.File]::WriteAllText($completedPath, '')
            }
            catch {
                if ($captureStarted) { try { & "$env:SystemRoot\System32\pktmon.exe" stop | Out-Null } catch {} }
                [IO.File]::WriteAllText($errorPath, $_.Exception.Message)
                exit 1
            }
            """;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
