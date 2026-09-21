#!/usr/bin/env bash
set -euo pipefail

project_dir="Econosys.Web"
environment_file=".env.deploy.local"

if [[ -f "$environment_file" ]]; then
  set -a
  source "$environment_file"
  set +a
fi

: "${SWA_CLI_DEPLOYMENT_TOKEN:?Set SWA_CLI_DEPLOYMENT_TOKEN in the environment or .env.deploy.local.}"

echo "Building frontend for production..."
(
  cd "$project_dir"
  npm run build:production
)

echo "Deploying frontend to Azure Static Web Apps..."
npx --yes @azure/static-web-apps-cli deploy "$project_dir/dist" \
  --env production \
  --deployment-token "$SWA_CLI_DEPLOYMENT_TOKEN"

echo "Frontend deployment complete."