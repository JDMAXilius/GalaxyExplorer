<#
.SYNOPSIS
    Refresh the asset database, wait for the compile to finish, and report compiler errors.

.DESCRIPTION
    The editor compiles when it is told to refresh, not when a file changes on disk, so a script
    written from outside Unity is invisible until this runs. The wait matters as much as the
    refresh: while the editor reloads its domain the relay answers "Unity not detected", which
    reads like a broken connection and is nothing of the sort - it is the expected reply, and the
    only correct response is to ask again.

    Errors come out of the editor log rather than out of a RunCommand, because the command that
    started the compile is in the assembly the compile unloads: it cannot be alive to report the
    result. Only the part of the log written since the refresh is read, so yesterday's errors
    cannot be reported as today's.

.EXAMPLE
    pwsh tools/mcp/compile.ps1
    pwsh tools/mcp/compile.ps1 -TimeoutSeconds 600 -ShowWarnings

.NOTES
    Exit codes: 0 clean, 1 compiler errors, 2 timed out or the relay never answered,
    3 refused because the editor is in play mode (never edit scripts during play).
#>

[CmdletBinding()]
param(
    # The Unity project. Defaults to this repo, two levels up from tools/mcp.
    [string] $ProjectPath = '',

    # How long to wait for the compile and domain reload to finish.
    [int] $TimeoutSeconds = 300,

    # Skip AssetDatabase.Refresh and only wait and report. Use after a refresh you triggered
    # yourself, or to read the result of a compile already under way.
    [switch] $NoRefresh,

    # List warnings as well as errors. Off by default: the relay already reports NOT-OK on any
    # warning, and treating every warning as a failure is how a clean build gets called broken.
    [switch] $ShowWarnings,

    # Compile anyway while the editor is in play mode. Almost always the wrong thing.
    [switch] $Force,

    # Passed straight through to umcp.js.
    [string] $Relay = $env:UNITY_RELAY,
    [switch] $VerboseRelay
)

# Continue, not Stop, deliberately. umcp.js exits non-zero when a tool reports an error and writes
# its progress notes to stderr; under Stop, PowerShell 7.4 turns both of those into terminating
# errors and this script would die on the very replies it exists to read.
$ErrorActionPreference = 'Continue'
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

# $PSScriptRoot is unreliable here: it is empty when the script is
# invoked through `powershell -File` with a relative path - which is how it failed the first
# time it was ever run for real. Resolve the script's own folder once, here, and derive both
# the project path and umcp.js from it.
$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition }
if (-not $scriptRoot) {
    # Falling back to the working directory would compute a project path two levels above wherever
    # the caller happened to stand, and then refresh the wrong project. Fail loudly instead.
    Write-Error 'Cannot determine the location of compile.ps1; pass -ProjectPath explicitly.'
    exit 2
}
if (-not $ProjectPath) { $ProjectPath = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path }

$umcp = Join-Path $scriptRoot 'umcp.js'
if (-not (Test-Path $umcp)) { Write-Error "umcp.js is missing from $scriptRoot"; exit 2 }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Error 'node is not on PATH; umcp.js needs it.'
    exit 2
}

$commonArgs = @('--project', $ProjectPath)
if ($Relay) { $commonArgs += @('--relay', $Relay) }
if ($VerboseRelay) { $commonArgs += '--verbose' }

# ------------------------------------------------------------------ the two tiny RunCommand scripts
#
# Written to temp rather than checked in: they are one line each and belong to this script's
# behaviour. Single-quoted here-strings so PowerShell leaves the C# alone.

$stateSource = @'
// Reports what the editor is doing. Fully qualified and using-free so it compiles whatever
// preamble the runner wraps it in.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("STATE compiling=" + UnityEditor.EditorApplication.isCompiling +
                   " updating=" + UnityEditor.EditorApplication.isUpdating +
                   " playing=" + UnityEditor.EditorApplication.isPlaying +
                   " paused=" + UnityEditor.EditorApplication.isPaused +
                   " unity=" + UnityEngine.Application.unityVersion);
    }
}
'@

$refreshSource = @'
// Imports whatever changed on disk, which is what starts a compile. The reply is likely to be the
// last thing this assembly ever says: a script change unloads it moments later.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("REFRESH: refused, the editor is in play mode.");
            return;
        }

        UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.Default);
        result.Log("REFRESH: requested, isCompiling=" + UnityEditor.EditorApplication.isCompiling);
    }
}
'@

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('umcp_' + [System.Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tempDir | Out-Null
$stateFile = Join-Path $tempDir 'state.cs'
$refreshFile = Join-Path $tempDir 'refresh.cs'
Set-Content -Path $stateFile -Value $stateSource -Encoding UTF8
Set-Content -Path $refreshFile -Value $refreshSource -Encoding UTF8

function Invoke-Umcp([string] $file) {
    # stderr is folded into the text on purpose: umcp's own notes ("retrying, domain reload") are
    # part of what the caller needs to see, and everything here is matched, not parsed.
    $text = (& node $umcp run $file @commonArgs 2>&1 | Out-String)
    return $text
}

function Get-EditorLogPath {
    $projectLog = Join-Path $ProjectPath 'Logs\Editor.log'
    if (Test-Path $projectLog) { return $projectLog }

    # The relay usually starts the editor with -logFile <project>\Logs\Editor.log. When it did not,
    # the editor is logging to its per-user default instead.
    if ($env:LOCALAPPDATA) {
        $userLog = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
        if (Test-Path $userLog) { return $userLog }
    }

    return $null
}

function Read-LogFrom([string] $path, [long] $offset) {
    # FileShare ReadWrite is not optional: the editor holds this file open for writing, and any
    # stricter share mode fails with "being used by another process".
    $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite)
    try {
        if ($offset -gt $fs.Length) { $offset = 0 }   # the log was rotated under us
        [void]$fs.Seek($offset, [System.IO.SeekOrigin]::Begin)
        $reader = New-Object System.IO.StreamReader($fs)
        return $reader.ReadToEnd()
    } finally {
        $fs.Dispose()
    }
}

try {
    # -------------------------------------------------------------- 1. what is the editor doing

    Write-Host 'Asking the editor what it is doing...'
    $state = Invoke-Umcp $stateFile
    if ($state -notmatch 'STATE\s+compiling=') {
        Write-Host $state
        Write-Error 'The relay never returned editor state. Is it running against this project, and is only one client connected?'
        exit 2
    }

    if ($state -match 'playing=True' -and -not $Force) {
        Write-Host $state.Trim()
        Write-Warning 'The editor is in play mode. Compiling now reloads the domain out from under the running app.'
        Write-Warning 'Leave play first: node tools/mcp/umcp.js run tools/mcp/leave_play_mode.cs   (-Force overrides.)'
        exit 3
    }

    # -------------------------------------------------------------- 2. mark the log, then refresh

    $log = Get-EditorLogPath
    $offset = 0L
    if ($log) {
        $offset = (Get-Item $log).Length
        Write-Host "Reading errors from $log (from byte $offset)."
    } else {
        Write-Warning 'No Editor.log found in the project or in %LOCALAPPDATA%\Unity\Editor. Errors cannot be reported from the log.'
    }

    if (-not $NoRefresh) {
        Write-Host 'Refreshing the asset database...'
        $refresh = Invoke-Umcp $refreshFile
        # A refresh that unloads the assembly mid-reply is normal, so its answer is informational.
        Write-Host ($refresh.Trim())
    }

    # -------------------------------------------------------------- 3. wait it out

    Write-Host -NoNewline 'Waiting for the compile'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $settled = $false
    $sawBusy = $false

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $state = Invoke-Umcp $stateFile

        if ($state -match 'STATE\s+compiling=(\w+)\s+updating=(\w+)') {
            $compiling = $Matches[1] -eq 'True'
            $updating = $Matches[2] -eq 'True'
            if ($compiling -or $updating) {
                $sawBusy = $true
                Write-Host -NoNewline '.'
                continue
            }

            # One quiet answer can arrive in the gap between the import finishing and the compile
            # starting, so a run that has not yet seen the editor busy waits a beat and asks again.
            if (-not $sawBusy -and -not $NoRefresh) {
                $sawBusy = $true
                Write-Host -NoNewline ':'
                continue
            }

            $settled = $true
            break
        }

        # "Unity not detected" - the domain is reloading. Expected, not an error.
        Write-Host -NoNewline '_'
    }

    Write-Host ''
    if (-not $settled) {
        Write-Error "The editor was still busy after $TimeoutSeconds s. Raise -TimeoutSeconds, or look at the editor."
        exit 2
    }

    # -------------------------------------------------------------- 4. report

    if (-not $log) { Write-Host 'Compile finished. No log to read.'; exit 0 }

    $text = Read-LogFrom $log $offset
    $lines = $text -split "`r?`n"

    # Roslyn writes "<file>(<line>,<col>): error CS####: <message>"; Unity adds its own
    # "Compilation failed" and "Assembly ... will not be loaded" lines around it.
    # @() so that one error line, or none at all, still has a .Count.
    $errors = @($lines | Where-Object { $_ -match 'error CS\d+' -or $_ -match '^\s*Compilation failed' } |
        Select-Object -Unique)
    $warnings = @($lines | Where-Object { $_ -match 'warning CS\d+' } | Select-Object -Unique)

    if ($errors.Count -gt 0) {
        Write-Host ''
        Write-Host "COMPILE FAILED - $($errors.Count) error line(s):" -ForegroundColor Red
        $errors | ForEach-Object { Write-Host "  $_" }
        if ($warnings.Count -gt 0) { Write-Host "  (plus $($warnings.Count) warning line(s))" }
        exit 1
    }

    Write-Host ''
    Write-Host 'COMPILE CLEAN - no compiler errors since the refresh.' -ForegroundColor Green
    if ($warnings.Count -gt 0) {
        Write-Host "$($warnings.Count) warning line(s). The relay would answer NOT-OK for these; they are not errors."
        if ($ShowWarnings) { $warnings | ForEach-Object { Write-Host "  $_" } }
    }
    exit 0
} finally {
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}
