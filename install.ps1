 $ErrorActionPreference = 'Stop'
 $ProgressPreference = 'SilentlyContinue'
try { [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072 } catch {}

 $BepInExUrl       = 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.4/BepInEx_win_x64_5.4.23.4.zip'
 $BepInExSha256    = 'f881201b79da03e513bf97cdf39607ffa7f9e0d31a519b1aeeca8eb60f8309e7'
 $ScriptUrl        = 'https://github.com/iimenu/manifest/raw/refs/heads/main/install.ps1'
 $ApiManifestUrl   = 'https://github.com/iimenu/manifest/raw/refs/heads/main/api.json'
 $VersionManifestUrl = 'https://github.com/iimenu/manifest/raw/refs/heads/main/menuversion.json'
 $SigningPublicKey = 'MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEohKBpx0zIokiQbQ9MvN5as5rYSXUS/Lp7tqDpwbondDFYWVLGz31J47YQno/hgtF/gXyZDvdkmmB15X86H4nrw=='

function Fail($msg) { Write-Host "`n$msg" -ForegroundColor Red; Read-Host 'Press Enter to exit'; exit 1 }

Write-Host ''
Write-Host '  ii Reborn - Installer' -ForegroundColor Yellow
Write-Host '  github.com/iimenu/manifest'
Write-Host ''

# spki for p-256, 26 byte asn.1 header, 0x04 (uncompressed), x[32], y[32]
 $spki = [Convert]::FromBase64String($SigningPublicKey)
if ($spki.Length -ne 91 -or $spki[26] -ne 4) { Fail 'Signing key constant is malformed.' }
 $pubX = New-Object byte[] 32
 $pubY = New-Object byte[] 32
[Array]::Copy($spki, 27, $pubX, 0, 32)
[Array]::Copy($spki, 59, $pubY, 0, 32)
 $ecPoint = New-Object System.Security.Cryptography.ECPoint
 $ecPoint.X = $pubX
 $ecPoint.Y = $pubY
 $ecParams = New-Object System.Security.Cryptography.ECParameters
 $ecParams.Curve = [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256
 $ecParams.Q = $ecPoint
 $VerifyKey = [System.Security.Cryptography.ECDsa]::Create()
 $VerifyKey.ImportParameters($ecParams)

function Test-Signature([string]$Canonical, [string]$SignatureB64) {
    if ([string]::IsNullOrEmpty($SignatureB64)) { return $false }
    try {
        $sigBytes = [Convert]::FromBase64String($SignatureB64)
        if ($sigBytes.Length -ne 64) { return $false }
        $dataBytes = [System.Text.Encoding]::ASCII.GetBytes($Canonical)
        return $VerifyKey.VerifyData($dataBytes, $sigBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    } catch { return $false }
}

function Test-DownloadUrl([string]$Url) {
    if ([string]::IsNullOrEmpty($Url)) { return $false }
    $c = $Url.Trim()
    if (-not $c.StartsWith('https://')) { return $false }
    if ($c.Contains('..')) { return $false }
    if (-not $c.EndsWith('.dll', [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    if ($c -match '[\s"''$`&|;\\<>^%]') { return $false }
    return $true
}

 $LF = [string][char]10

# -- locate game --
 $candidates = @(
    'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag',
    'D:\SteamLibrary\steamapps\common\Gorilla Tag',
    'C:\Program Files\Oculus\Software\Software\another-axiom-gorilla-tag',
    'D:\Steam\steamapps\common\Gorilla Tag'
)
 $found = @($candidates | Where-Object { Test-Path "$($_)\Gorilla Tag.exe" })
if ($found.Count -eq 0) {
    $gamePath = (Read-Host 'Gorilla Tag directory not found. Enter it manually').Trim('"')
    if (-not (Test-Path $gamePath)) { Fail 'Invalid directory.' }
} elseif ($found.Count -eq 1) {
    $gamePath = $found[0]
} else {
    Write-Host 'Multiple Gorilla Tag installations found:' -ForegroundColor Yellow
    for ($i = 0; $i -lt $found.Count; $i++) {
        Write-Host ('  [{0}] {1}' -f ($i + 1), $found[$i])
    }
    $pick = 0
    while ($true) {
        $answer = (Read-Host "Choose [1-$($found.Count)] (Enter = 1)").Trim()
        if ($answer -eq '') { $pick = 1; break }
        if ([int]::TryParse($answer, [ref]$pick) -and $pick -ge 1 -and $pick -le $found.Count) { break }
        Write-Host 'Invalid choice.' -ForegroundColor Red
    }
    $gamePath = $found[$pick - 1]
}
Write-Host "Game directory: $gamePath`n"

# -- fetch verified update info (priority: live API > api.json domains > static manifest) --
Write-Host 'Fetching verified update manifest...' -ForegroundColor Cyan
 $updateInfo = $null

try {
    $apiResp = Invoke-WebRequest -UseBasicParsing -Uri 'https://api-prod-iidk-de.corgi.st/v2/cfg?rev=0' -TimeoutSec 15
    $apiCfg = $apiResp.Content | ConvertFrom-Json
    if ($apiCfg.update) {
        $u = $apiCfg.update
        $canonical = "update$LF$($u.version)$LF$($u.sha256)$LF$($u.downloadUrl)$LF$($u.releaseUrl)$LF$($u.timestamp)"
        if (-not [string]::IsNullOrEmpty($u.version) -and
            -not [string]::IsNullOrEmpty($u.timestamp) -and
            (Test-Signature $canonical $u.signature)) {
            $updateInfo = $u
        }
    }
} catch { }

if (-not $updateInfo) {
    try {
        $manifestResp = Invoke-WebRequest -UseBasicParsing -Uri $ApiManifestUrl -TimeoutSec 15
        $manifestApi = $manifestResp.Content | ConvertFrom-Json
        $domainList = @($manifestApi.domains | Where-Object { $_ -match '^[A-Za-z0-9.-]{1,253}$' } | Select-Object -First 8)
        if ($domainList.Count -gt 0) {
            $apiCanonical = "api$LF$($domainList -join $LF)$LF$($manifestApi.timestamp)"
            if (Test-Signature $apiCanonical $manifestApi.signature) {
                foreach ($domain in $domainList) {
                    try {
                        $resp = Invoke-WebRequest -UseBasicParsing -Uri "https://$domain/v2/cfg?rev=0" -TimeoutSec 15
                        $cfg = $resp.Content | ConvertFrom-Json
                        if ($cfg.update) {
                            $u = $cfg.update
                            $canonical = "update$LF$($u.version)$LF$($u.sha256)$LF$($u.downloadUrl)$LF$($u.releaseUrl)$LF$($u.timestamp)"
                            if (-not [string]::IsNullOrEmpty($u.version) -and
                                -not [string]::IsNullOrEmpty($u.timestamp) -and
                                (Test-Signature $canonical $u.signature)) {
                                $updateInfo = $u
                                break
                            }
                        }
                    } catch { }
                }
            }
        }
    } catch { }
}

if (-not $updateInfo) {
    try {
        $verResp = Invoke-WebRequest -UseBasicParsing -Uri $VersionManifestUrl -TimeoutSec 15
        $verCfg = $verResp.Content | ConvertFrom-Json
        $canonical = "update$LF$($verCfg.version)$LF$($verCfg.sha256)$LF$($verCfg.downloadUrl)$LF$($verCfg.releaseUrl)$LF$($verCfg.timestamp)"
        if (-not [string]::IsNullOrEmpty($verCfg.version) -and
            -not [string]::IsNullOrEmpty($verCfg.timestamp) -and
            (Test-Signature $canonical $verCfg.signature)) {
            $updateInfo = $verCfg
        }
    } catch { }
}

if (-not $updateInfo) { Fail 'No verified update manifest could be fetched from any source.' }
if (-not (Test-DownloadUrl $updateInfo.downloadUrl)) { Fail 'Update manifest contains an unsafe download URL.' }
if ($updateInfo.sha256 -notmatch '^[0-9a-fA-F]{64}$') { Fail 'Update manifest contains an invalid SHA-256 hash.' }

 $pluginUrl = $updateInfo.downloadUrl
 $pluginSha256 = $updateInfo.sha256

# -- already up to date? --
 $menuDll = "$gamePath\BepInEx\plugins\ii.Reborn.dll"
if (Test-Path $menuDll) {
    $localHash = ''
    try { $localHash = (Get-FileHash -Path $menuDll -Algorithm SHA256).Hash } catch {}
    if ($localHash -eq $pluginSha256) {
        Write-Host ''
        Write-Host 'ii Reborn is already updated in this folder.' -ForegroundColor Yellow
        Write-Host 'Have you installed it but nothing showed up in game? Then this installer is probably'
        Write-Host 'using the wrong game folder!'
        Write-Host ''
        Write-Host 'To find the right one in Steam:'
        Write-Host '  1. Open Steam and right-click Gorilla Tag'
        Write-Host '  2. Manage -> Browse local files   (a folder window opens)'
        Write-Host '  3. Click the address bar and copy the full path'
        Write-Host ''
        $alt = (Read-Host 'Paste the full folder path to "Gorilla Tag" here (or press Enter to keep this folder)').Trim().Trim('"')
        if ($alt -ne '') {
            if (Test-Path $alt) {
                $gamePath = $alt
                Write-Host "Game directory: $gamePath`n"
            } else {
                Write-Host 'That path does not exist - keeping the folder found earlier.' -ForegroundColor Red
            }
        }
    }
}

# -- bepinex --
Write-Host 'Downloading BepInEx...' -ForegroundColor Cyan
 $zip = Join-Path $env:TEMP 'iimenu-bepinex.zip'
try {
    Invoke-WebRequest -UseBasicParsing -Uri $BepInExUrl -OutFile $zip

    $zipHash = (Get-FileHash -Path $zip -Algorithm SHA256).Hash
    if ($zipHash -ne $BepInExSha256) {
        Remove-Item $zip -Force -ErrorAction SilentlyContinue
        Fail "BepInEx archive hash mismatch (expected $BepInExSha256, got $zipHash)."
    }

    Write-Host 'Extracting BepInEx...' -ForegroundColor Cyan

    # Expand-Archive (PS 5.1) misjoins dot-leading entries (e.g. '.doorstop_version' -> 'Gorilla Tag.doorstop_version')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) { continue }
            $rel = $entry.FullName -replace '/', '\'
            $target = $gamePath + '\' + $rel
            $slash = $rel.LastIndexOf('\')
            if ($slash -ge 0) {
                $targetDir = $gamePath + '\' + $rel.Substring(0, $slash)
                if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force -Path $targetDir | Out-Null }
            }
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $archive.Dispose() }
} catch {
    if ($env:IIMENU_ELEVATED -eq '1') { Fail "Failed to download/extract BepInEx ($($_.Exception.Message))" }
    Write-Host "BepInEx step failed: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host 'Relaunching as administrator - accept the UAC prompt!!!' -ForegroundColor Yellow

    # Only requested if the game files cannot be written without Administrator
    $child = '-NoProfile -Command "$env:IIMENU_ELEVATED=''1''; irm ''' + $ScriptUrl + ''' | iex"'
    try {
        Start-Process powershell -Verb RunAs -ArgumentList $child
    } catch {
        Fail 'Administrator access was declined. Re-run the installer and accept the UAC prompt.'
    }
    exit
}
Remove-Item $zip -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path "$gamePath\BepInEx\config", "$gamePath\BepInEx\plugins" | Out-Null

Get-ChildItem -Path "$gamePath\BepInEx\plugins" -Filter 'ii*.dll' -Recurse -File -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path "$gamePath\BepInEx\plugins" -Filter 'ii*' -Directory -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# -- menu --
Write-Host 'Downloading ii Reborn...' -ForegroundColor Cyan
 $menuDllPath = "$gamePath\BepInEx\plugins\ii.Reborn.dll"
try {
    Invoke-WebRequest -UseBasicParsing -Uri $pluginUrl -OutFile $menuDllPath
    $dllHash = (Get-FileHash -Path $menuDllPath -Algorithm SHA256).Hash
    if ($dllHash -ne $pluginSha256) {
        Remove-Item $menuDllPath -Force -ErrorAction SilentlyContinue
        Fail "Menu DLL hash mismatch (expected $pluginSha256, got $dllHash)."
    }
} catch { Fail "Failed to download the menu ($($_.Exception.Message))" }

Write-Host ''
Write-Host 'Congratulations, you now have the menu!' -ForegroundColor Green
Write-Host 'Launch Gorilla Tag and the menu will load automatically.'
Write-Host ''
Read-Host 'All good, press Enter to exit or close this window'
