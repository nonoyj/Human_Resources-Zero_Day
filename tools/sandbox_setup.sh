#!/usr/bin/env bash
# sandbox_setup.sh -- install dotnet 8 + PowerShell 7 for this sandbox only.
# They go in ~/.cache/hr0d-tools, NOT /tmp: /tmp is a small tmpfs (992 MB) and the SDK does not fit.
# The sandbox wipes /tmp between some turns, so this is kept in the repo and re-run whenever
# `dotnet: command not found` appears. Nothing here is part of a bundle.
set -euo pipefail
ARCH=x64
ROOT="$HOME/.cache/hr0d-tools"
mkdir -p "$ROOT"
if [ ! -x $ROOT/dotnet/dotnet ]; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o $ROOT/di.sh
  bash $ROOT/di.sh --version 8.0.404 --install-dir $ROOT/dotnet >/dev/null
fi
if [ ! -x $ROOT/pwsh/pwsh ]; then
  V=7.4.6
  curl -sSL "https://github.com/PowerShell/PowerShell/releases/download/v$V/powershell-$V-linux-$ARCH.tar.gz" -o $ROOT/pwsh.tgz
  mkdir -p $ROOT/pwsh && tar -xzf $ROOT/pwsh.tgz -C $ROOT/pwsh && chmod +x $ROOT/pwsh/pwsh
fi
echo "dotnet $($ROOT/dotnet/dotnet --version 2>/dev/null) · pwsh $($ROOT/pwsh/pwsh -NoLogo -Command '$PSVersionTable.PSVersion.ToString()' 2>/dev/null)"
