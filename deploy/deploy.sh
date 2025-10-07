#!/bin/bash

# Exit on any error
set -e

# Parse command line arguments
CONTAINERS_ONLY=false
while [[ $# -gt 0 ]]; do
  case $1 in
    --containers-only)
      CONTAINERS_ONLY=true
      shift
      ;;
    -h|--help)
      echo "Usage: $0 [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --containers-only    Only update container images, skip infrastructure deployment"
      echo "  -h, --help          Show this help message"
      echo ""
      echo "Environment variables:"
      echo "  RESOURCE_GROUP_NAME  Resource group name (default: fileshare-rg)"
      echo "  LOCATION            Azure region (default: eastus)"
      echo "  RESOURCE_PREFIX     Prefix for resource names (default: fileshare)"
      exit 0
      ;;
    *)
      echo "Unknown option $1"
      echo "Use --help for usage information"
      exit 1
      ;;
  esac
done

# Configuration
RESOURCE_GROUP_NAME="${RESOURCE_GROUP_NAME:-fileshare-rg}"
LOCATION="${LOCATION:-eastus}"
RESOURCE_PREFIX="${RESOURCE_PREFIX:-fileshare}"

echo "🚀 Starting deployment to Azure..."
echo "Resource Group: $RESOURCE_GROUP_NAME"
echo "Location: $LOCATION"
echo "Resource Prefix: $RESOURCE_PREFIX"

if [ "$CONTAINERS_ONLY" != "true" ]; then
  # Create resource group if it doesn't exist
  echo "📦 Creating resource group..."
  az group create --name $RESOURCE_GROUP_NAME --location $LOCATION

  # Deploy Bicep template
  echo "🏗️  Deploying Azure resources..."
  DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP_NAME \
    --template-file deploy/main.bicep \
    --parameters resourcePrefix=$RESOURCE_PREFIX \
    --query 'properties.outputs' \
    --output json)
else
  echo "🔄 Containers-only mode: Using existing Azure resources..."
  # Get existing deployment outputs
  DEPLOYMENT_OUTPUT=$(az deployment group show \
    --resource-group $RESOURCE_GROUP_NAME \
    --name main \
    --query 'properties.outputs' \
    --output json)
fi

# Extract outputs
CONTAINER_REGISTRY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.containerRegistryName.value')
CONTAINER_REGISTRY_LOGIN_SERVER=$(echo $DEPLOYMENT_OUTPUT | jq -r '.containerRegistryLoginServer.value')
API_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.apiUrl.value')

echo "📋 Using resources:"
echo "  Container Registry: $CONTAINER_REGISTRY_NAME"
echo "  Registry Server: $CONTAINER_REGISTRY_LOGIN_SERVER"
echo "  API URL: $API_URL"

# Generate unique tag based on timestamp and short commit hash
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
if git rev-parse --git-dir > /dev/null 2>&1; then
  COMMIT_HASH=$(git rev-parse --short HEAD)
  IMAGE_TAG="${TIMESTAMP}-${COMMIT_HASH}"
else
  IMAGE_TAG="${TIMESTAMP}"
fi

echo "🏷️  Using image tag: $IMAGE_TAG"

# Login to Azure Container Registry
echo "🔐 Logging into Azure Container Registry..."
az acr login --name $CONTAINER_REGISTRY_NAME

# Build and push API container
echo "🐳 Building and pushing API container..."
cd api
docker build -t $CONTAINER_REGISTRY_LOGIN_SERVER/api:$IMAGE_TAG .
docker build -t $CONTAINER_REGISTRY_LOGIN_SERVER/api:latest .
docker push $CONTAINER_REGISTRY_LOGIN_SERVER/api:$IMAGE_TAG
docker push $CONTAINER_REGISTRY_LOGIN_SERVER/api:latest
cd ..

# Build and push UI container
echo "🐳 Building and pushing UI container..."
cd ui
docker build -t $CONTAINER_REGISTRY_LOGIN_SERVER/ui:$IMAGE_TAG \
  --build-arg REACT_APP_API_BASE=$API_URL .
docker build -t $CONTAINER_REGISTRY_LOGIN_SERVER/ui:latest \
  --build-arg REACT_APP_API_BASE=$API_URL .
docker push $CONTAINER_REGISTRY_LOGIN_SERVER/ui:$IMAGE_TAG
docker push $CONTAINER_REGISTRY_LOGIN_SERVER/ui:latest
cd ..

# Update container apps with new images using specific tags
echo "🔄 Updating Container Apps with tag: $IMAGE_TAG..."
az containerapp update \
  --name ${RESOURCE_PREFIX}-api \
  --resource-group $RESOURCE_GROUP_NAME \
  --image $CONTAINER_REGISTRY_LOGIN_SERVER/api:$IMAGE_TAG

az containerapp update \
  --name ${RESOURCE_PREFIX}-ui \
  --resource-group $RESOURCE_GROUP_NAME \
  --image $CONTAINER_REGISTRY_LOGIN_SERVER/ui:$IMAGE_TAG \
  --set-env-vars REACT_APP_API_BASE=$API_URL

UI_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.uiUrl.value')

echo "✅ Deployment completed successfully!"
echo ""
echo "🌐 Your applications are available at:"
echo "  UI:  $UI_URL"
echo "  API: $API_URL"
echo ""
echo "🔧 To redeploy just the containers (after code changes), run:"
echo "  ./deploy/deploy.sh --containers-only"