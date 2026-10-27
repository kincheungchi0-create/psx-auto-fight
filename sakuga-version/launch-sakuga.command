#!/bin/bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")" && pwd)"
runtime_dir="$HOME/Library/Application Support/SakugaAutoFight"
godot="$runtime_dir/Godot_mono.app/Contents/MacOS/Godot"
dotnet="$runtime_dir/dotnet"
if [[ ! -x "$godot" || ! -x "$dotnet/dotnet" ]]; then
  printf 'Godot 4.7 .NET or .NET 8 is missing. See README-SAKUGA.md.\n'
  exit 1
fi
export DOTNET_ROOT="$dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$dotnet:$PATH"
# Ignore macOS AppleDouble metadata that can be mistaken for a solution file.
find "$project_dir" -maxdepth 1 -name '._*.sln' -delete
"$dotnet/dotnet" build "$project_dir/Sakuga Engine.csproj" --nologo
"$godot" --headless --editor --path "$project_dir" --import
exec "$godot" --path "$project_dir"
