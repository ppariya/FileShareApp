@description('The location for all resources')
param location string = resourceGroup().location

@description('The prefix for all resource names')
param resourcePrefix string = 'fileshare-${uniqueString(resourceGroup().id)}'

@description('Container image for the API')
param apiImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Container image for the UI')
param uiImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

// Azure Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: '${replace(resourcePrefix, '-', '')}${uniqueString(resourceGroup().id, 'acr')}'
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${resourcePrefix}-env'
  location: location
  properties: {}
}

// API Container App
resource apiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${resourcePrefix}-api'
  location: location
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
        }
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImageName
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// UI Container App
resource uiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${resourcePrefix}-ui'
  location: location
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
      ingress: {
        external: true
        targetPort: 80
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
    }
    template: {
      containers: [
        {
          name: 'ui'
          image: uiImageName
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'REACT_APP_API_BASE'
              value: 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// Outputs
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output apiUrl string = 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'
output uiUrl string = 'https://${uiContainerApp.properties.configuration.ingress.fqdn}'
