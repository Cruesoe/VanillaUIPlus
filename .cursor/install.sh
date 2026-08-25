#!/usr/bin/env bash
#
# Cloud Agent install script for Vanilla UI+ (RimWorld 1.6 mod).
#
# Prepares a Linux machine to build Source/VanillaUIPlus.csproj without a local
# RimWorld installation by:
#   1. Installing the .NET SDK (provides Roslyn / MSBuild).
#   2. Fetching stripped, publicized reference assemblies from NuGet
#      (RimWorld, Unity, Harmony) plus the .NET Framework 4.7.2 reference
#      assemblies, and laying them out under .cursor/refs/.
#
# The paths produced here are consumed by Directory.Build.props, which only
# engages on non-Windows hosts, so the normal Windows workflow is untouched.
#
# Idempotent and non-interactive: safe to run repeatedly.
set -euo pipefail

# Resolve the repository root (this file lives in <repo>/.cursor/).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REFS_DIR="$SCRIPT_DIR/refs"

# Pinned package versions (RimWorld 1.6).
KRAFS_VERSION="1.6.4871"
HARMONY_VERSION="2.4.2"
NETFX_REF_VERSION="1.0.3"

MANAGED_DIR="$REFS_DIR/rimworld/RimWorldWin64_Data/Managed"
HARMONY_DIR="$REFS_DIR/harmony"
FRAMEWORK_DIR="$REFS_DIR/framework/net472"

log() { printf '\n=== %s ===\n' "$*"; }

install_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    log "dotnet already installed: $(dotnet --version)"
    return
  fi
  log "Installing .NET SDK 8.0"
  sudo apt-get update -qq
  sudo apt-get install -y --no-install-recommends dotnet-sdk-8.0 unzip curl
  dotnet --version
}

# Download a .nupkg (which is a zip) and extract it to a scratch directory.
fetch_nupkg() {
  local id="$1" version="$2" dest="$3"
  local lower_id="${id,,}"
  local url="https://api.nuget.org/v3-flatcontainer/${lower_id}/${version}/${lower_id}.${version}.nupkg"
  local tmp
  tmp="$(mktemp -d)"
  curl -fsSL -o "$tmp/pkg.nupkg" "$url"
  rm -rf "$dest"
  mkdir -p "$dest"
  unzip -q "$tmp/pkg.nupkg" -d "$dest"
  rm -rf "$tmp"
}

lay_out_refs() {
  # Skip the (slow) download when every reference assembly is already in place.
  if [[ -f "$MANAGED_DIR/Assembly-CSharp.dll" \
     && -f "$MANAGED_DIR/netstandard.dll" \
     && -f "$HARMONY_DIR/0Harmony.dll" \
     && -f "$FRAMEWORK_DIR/mscorlib.dll" ]]; then
    log "Reference assemblies already present; skipping download"
    return
  fi

  local scratch
  scratch="$(mktemp -d)"

  log "Fetching Krafs.Rimworld.Ref $KRAFS_VERSION"
  fetch_nupkg "Krafs.Rimworld.Ref" "$KRAFS_VERSION" "$scratch/krafs"

  log "Fetching Lib.Harmony $HARMONY_VERSION"
  fetch_nupkg "Lib.Harmony" "$HARMONY_VERSION" "$scratch/harmony"

  log "Fetching Microsoft.NETFramework.ReferenceAssemblies.net472 $NETFX_REF_VERSION"
  fetch_nupkg "Microsoft.NETFramework.ReferenceAssemblies.net472" "$NETFX_REF_VERSION" "$scratch/netfx"

  log "Laying out reference assemblies under .cursor/refs"
  mkdir -p "$MANAGED_DIR" "$HARMONY_DIR" "$FRAMEWORK_DIR"

  # RimWorld / Unity assemblies referenced by the csproj (plus the netstandard
  # 2.1 facade the publicized Unity assemblies forward core types to).
  for dll in Assembly-CSharp UnityEngine.CoreModule UnityEngine.IMGUIModule \
             UnityEngine.TextRenderingModule netstandard; do
    cp "$scratch/krafs/ref/net472/${dll}.dll" "$MANAGED_DIR/"
  done

  cp "$scratch/harmony/lib/net472/0Harmony.dll" "$HARMONY_DIR/"
  cp "$scratch/netfx/build/.NETFramework/v4.7.2/"*.dll "$FRAMEWORK_DIR/"

  rm -rf "$scratch"
}

install_dotnet
lay_out_refs

log "Environment ready"
echo "Build with: dotnet build Source/VanillaUIPlus.csproj -c Debug"
