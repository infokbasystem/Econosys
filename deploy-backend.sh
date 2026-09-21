#!/usr/bin/env bash
set -euo pipefail

resource_group="Econosys"
app_name="econosysapi"
project="Econosys.Api/Econosys.Api.csproj"
deploy_dir="deploy-backend"
publish_dir="$deploy_dir/publish"
zip_file="$deploy_dir/deploy.zip"
health_url="https://econosysapi-cycbg7adfphndecj.northeurope-01.azurewebsites.net/health"

echo "Publishing ${app_name}..."

rm -rf "$publish_dir" "$zip_file"
mkdir -p "$publish_dir"

echo "Building and publishing .NET API..."
dotnet publish "$project" \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --output "$publish_dir"

echo "Creating deployment archive..."
(
  cd "$publish_dir"
  zip -r -q "../deploy.zip" .
)

echo "Deploying to Azure App Service..."
az webapp deploy \
  --resource-group "$resource_group" \
  --name "$app_name" \
  --src-path "$zip_file" \
  --type zip

echo "Checking API health..."
curl --fail --silent --show-error \
  --retry 6 \
  --retry-delay 5 \
  "$health_url"

echo
echo "Deployment complete."