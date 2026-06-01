# Rewritten from STS2-Agent's build-mod.ps1 (AGPL).
# Builds the C# mod, packs the .pck via Godot headless, and installs into <game>/mods/.
#
# Godot binary resolution: -GodotExe arg, else the GODOT_BIN environment variable.
param(
    [string]$Configuration = "Debug",
    [string]$GameRoot = "C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2",
    [string]$GodotExe = ""
)

$ErrorActionPreference = "Stop"

# Repo root is the parent of this build/ directory.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$modName        = "Sts2AiAdvisor"
$projectDir     = Join-Path $repoRoot "src/Sts2AiAdvisor"
$modProject     = Join-Path $projectDir "$modName.csproj"
$buildOutputDir = Join-Path $projectDir "bin/$Configuration/net9.0"
$builderDir     = Join-Path $repoRoot "tools/pck_builder"
$builderScript  = Join-Path $builderDir "build_pck.gd"
$manifestSource = Join-Path $projectDir "mod_manifest.json"
$modIdSource    = Join-Path $projectDir "mod_id.json"
$configExample  = Join-Path $projectDir "config.example.json"
$stagingDir     = Join-Path $repoRoot "build/mods/$modName"
$pckOutput      = Join-Path $stagingDir "$modName.pck"
$dllSource      = Join-Path $buildOutputDir "$modName.dll"
$modsDir        = Join-Path $GameRoot "mods"

# --- Resolve Godot ---
if ([string]::IsNullOrWhiteSpace($GodotExe)) { $GodotExe = $env:GODOT_BIN }
if ([string]::IsNullOrWhiteSpace($GodotExe)) {
    throw "Godot executable not found. Pass -GodotExe or set the GODOT_BIN environment variable."
}
if (-not (Test-Path $GodotExe)) { throw "Godot executable not found: $GodotExe" }

# --- 1. dotnet build ---
Write-Host "[build-mod] Building C# mod project..."
dotnet build $modProject -c $Configuration | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $dllSource)) { throw "Built DLL not found: $dllSource" }

# --- 2. Pack the PCK ---
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
Write-Host "[build-mod] Packing mod_manifest.json into PCK..."
& $GodotExe --headless --path $builderDir --script $builderScript -- $manifestSource $pckOutput | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Godot PCK build failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $pckOutput)) { throw "PCK output not found: $pckOutput" }

# The game runs Godot 4.5.1 and REJECTS any PCK stamped with a newer engine version
# ("Pack created with a newer version of the engine"). Our packer is Godot 4.6.3, but the
# pack format (v3) is identical, so we rewrite the header's engine-version field to 4.5.1.
# PCK header layout: magic(0-3)="GDPC", pack_format(4-7), ver_major(8-11), ver_minor(12-15), ver_patch(16-19), little-endian.
# NOTE: if the game updates its Godot version, bump the values below to match (or downgrade the packer).
Write-Host "[build-mod] Stamping PCK engine version -> 4.5.1 for game compatibility..."
$pckBytes = [System.IO.File]::ReadAllBytes($pckOutput)
$pckBytes[12] = 5; $pckBytes[13] = 0; $pckBytes[14] = 0; $pckBytes[15] = 0   # ver_minor = 5
$pckBytes[16] = 1; $pckBytes[17] = 0; $pckBytes[18] = 0; $pckBytes[19] = 0   # ver_patch = 1
[System.IO.File]::WriteAllBytes($pckOutput, $pckBytes)

# --- 3. Install into <game>/mods/ ---
Write-Host "[build-mod] Installing into game mods directory..."
New-Item -ItemType Directory -Force -Path $modsDir | Out-Null
# Loose manifest must be named "<ModName>.json" (matches the proven RouteSuggest/Booba convention),
# not "mod_id.json" — a generic name would collide in the shared flat mods/ dir.
Copy-Item -Force $dllSource    (Join-Path $modsDir "$modName.dll")
Copy-Item -Force $pckOutput    (Join-Path $modsDir "$modName.pck")
Copy-Item -Force $modIdSource  (Join-Path $modsDir "$modName.json")
Copy-Item -Force $configExample (Join-Path $modsDir "config.example.json")

Write-Host "[build-mod] Done. Using Godot: $GodotExe"
Write-Host "[build-mod] Installed:"
Write-Host "  $(Join-Path $modsDir "$modName.dll")"
Write-Host "  $(Join-Path $modsDir "$modName.pck")"
Write-Host "  $(Join-Path $modsDir "$modName.json")"
Write-Host "  $(Join-Path $modsDir "config.example.json")"
Write-Host "[build-mod] Rename config.example.json -> config.json and set your apiKey to enable advice."
