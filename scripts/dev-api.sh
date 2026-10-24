#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
set -a
source .env
set +a
export ConnectionStrings__Database="Host=localhost;Port=5432;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD"
export Jwt__Key="$JWT_KEY"
export ASPNETCORE_ENVIRONMENT=Development
if [[ "${1:-}" == "--migrate" ]]; then
  dotnet run --project src/MyTCG.Api -- --migrate
else
  dotnet run --project src/MyTCG.Api --urls http://0.0.0.0:5080
fi
