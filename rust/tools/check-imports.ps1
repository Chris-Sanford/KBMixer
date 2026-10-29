# Fails if a PE32+ exe imports any DLL that isn't part of Windows (keeps the release dependency-free).
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$Path = (Resolve-Path $Path).Path   # .NET resolves relative paths against the process directory, not $PWD

$allowed = @('kernel32', 'user32', 'gdi32', 'shell32', 'ole32', 'advapi32', 'winhttp', 'ntdll', 'msvcrt',
             'bcryptprimitives', 'userenv', 'ws2_32', 'dbghelp', 'oleaut32', 'uxtheme', 'dwmapi', 'version')

$b = [IO.File]::ReadAllBytes($Path)
$pe = [BitConverter]::ToInt32($b, 0x3C)
$nsec = [BitConverter]::ToUInt16($b, $pe + 6)
$opt = $pe + 24
$secs = $opt + [BitConverter]::ToUInt16($b, $pe + 20)
$importRva = [BitConverter]::ToUInt32($b, $opt + 120)

function RvaToOff([uint32]$rva) {
    for ($i = 0; $i -lt $nsec; $i++) {
        $s = $secs + 40 * $i
        $va = [BitConverter]::ToUInt32($b, $s + 12); $size = [Math]::Max([BitConverter]::ToUInt32($b, $s + 8), [BitConverter]::ToUInt32($b, $s + 16))
        if ($rva -ge $va -and $rva -lt $va + $size) { return $rva - $va + [BitConverter]::ToUInt32($b, $s + 20) }
    }
    throw "RVA 0x{0:X} is not in any section" -f $rva
}

$dlls = @()
for ($o = RvaToOff $importRva; ($nameRva = [BitConverter]::ToUInt32($b, $o + 12)) -ne 0; $o += 20) {
    $n = RvaToOff $nameRva; $e = $n
    while ($b[$e] -ne 0) { $e++ }
    $dlls += [Text.Encoding]::ASCII.GetString($b, $n, $e - $n)
}

$bad = $dlls | Where-Object {
    $base = [IO.Path]::GetFileNameWithoutExtension($_).ToLowerInvariant()
    -not ($allowed -contains $base -or $base.StartsWith('api-ms-win-'))
}
"Imports: $($dlls -join ', ')"
if ($bad) { throw "Non-system DLL imports: $($bad -join ', ')" }
