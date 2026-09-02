#!/usr/bin/env bash
# Smoke test pós-deploy — Frente 14, P10. Rodar manualmente depois de cada `git push` que
# dispara deploy no Render (sem CI, ver decisão 4 da Frente 14): checa que o essencial está
# de pé antes de considerar o deploy bom, sem precisar abrir o navegador ou logar de verdade.
set -euo pipefail

API_URL="${1:-https://hub-pessoal.onrender.com}"

check() {
  local description="$1"
  local expected="$2"
  local actual="$3"

  echo "→ ${description}"
  if [[ "$actual" == "$expected" ]]; then
    echo "  OK (${actual})"
  else
    echo "  FALHOU: esperado ${expected}, recebido ${actual}"
    exit 1
  fi
}

health_code=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/health")
check "GET /health" "200" "$health_code"

swagger_code=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/swagger")
check "GET /swagger (deve ser 404 em produção)" "404" "$swagger_code"

login_code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"invalid","password":"invalid"}')
check "POST /auth/login com credenciais inválidas (deve ser 401, não 500)" "401" "$login_code"

echo "Smoke test OK"
