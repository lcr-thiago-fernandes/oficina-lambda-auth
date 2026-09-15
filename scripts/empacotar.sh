#!/usr/bin/env bash
# Publica as duas funcoes para linux-x64 (framework-dependent, ReadyToRun) e gera os
# zips que o Terraform referencia (var.api_zip_path / var.authorizer_zip_path).
# Uso: ./scripts/empacotar.sh   -> artefatos/auth-api.zip, artefatos/auth-authorizer.zip
set -euo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
SAIDA="$RAIZ/artefatos"
rm -rf "$SAIDA" "$RAIZ/publish"
mkdir -p "$SAIDA"

publicar() {
  local projeto="$1" nome="$2"
  # --self-contained false: o runtime dotnet8 da Lambda ja traz o .NET.
  # PublishReadyToRun: pre-compila IL -> nativo, elimina JIT no cold start (design, secao 4).
  dotnet publish "$RAIZ/src/$projeto/$projeto.csproj" \
    -c Release -r linux-x64 --self-contained false \
    -p:PublishReadyToRun=true \
    -o "$RAIZ/publish/$nome"
  # O zip precisa ter os arquivos na RAIZ (sem pasta intermediaria).
  (cd "$RAIZ/publish/$nome" && zip -qr "$SAIDA/$nome.zip" .)
  echo "OK: $SAIDA/$nome.zip ($(du -h "$SAIDA/$nome.zip" | cut -f1))"
}

publicar OficinaAuth.Api        auth-api
publicar OficinaAuth.Authorizer auth-authorizer
