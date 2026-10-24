#!/usr/bin/env bash
set -euo pipefail
cd /workspace/MyTCG
export DOTNET_CLI_HOME=/workspace/.cache/dotnet
export NUGET_PACKAGES=/workspace/.cache/nuget
export DOTNET_ROOT=/workspace/.tools/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$DOTNET_ROOT"
if [[ ! -x "$DOTNET_ROOT/dotnet" ]]; then
python3 - <<'PY'
import hashlib, json, pathlib, tarfile, urllib.request
root = pathlib.Path('/workspace/MyTCG')
version = json.loads((root/'global.json').read_text())['sdk']['version']
with urllib.request.urlopen('https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json') as response:
    releases = json.load(response)
artifact = next(f for r in releases['releases'] for sdk in r.get('sdks', []) if sdk['version'] == version for f in sdk['files'] if f['rid'] == 'linux-x64' and f['name'].endswith('.tar.gz'))
archive = pathlib.Path('/workspace/.cache/dotnet-sdk.tar.gz')
urllib.request.urlretrieve(artifact['url'], archive)
with archive.open('rb') as stream:
    if hashlib.file_digest(stream, 'sha512').hexdigest().lower() != artifact['hash'].lower():
        raise RuntimeError('SHA512 du SDK invalide')
with tarfile.open(archive) as tar:
    tar.extractall('/workspace/.tools/dotnet', filter='data')
archive.unlink()
PY
fi
export PATH="$DOTNET_ROOT:$PATH"
dotnet --version
dotnet tool restore
dotnet restore MyTCG.Server.slnf --locked-mode
dotnet build MyTCG.Server.slnf --no-restore
