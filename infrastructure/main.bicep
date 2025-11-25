// Main Bicep template for Expense Management System
// Deploys all Azure resources in the correct order

@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for all resources')
param baseName string = 'expensemgmt'

@description('Admin login for SQL Server (Entra ID user principal name)')
param adminLogin string

@description('Admin Object ID for SQL Server (Entra ID object ID)')
param adminObjectId string

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// Generate unique suffix using resource group ID
var uniqueSuffix = toLower(uniqueString(resourceGroup().id))
var resourceBaseName = toLower('${baseName}-${uniqueSuffix}')

// Deploy Managed Identity first
module managedIdentity 'managed-identity.bicep' = {
  name: 'managedIdentityDeployment'
  params: {
    location: location
    baseName: resourceBaseName
  }
}

// Deploy App Service
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: resourceBaseName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
  }
  dependsOn: [
    managedIdentity
  ]
}

// Deploy Azure SQL
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    baseName: resourceBaseName
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
  dependsOn: [
    managedIdentity
  ]
}

// Deploy GenAI resources conditionally
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: location
    baseName: resourceBaseName
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
  dependsOn: [
    managedIdentity
  ]
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName

// GenAI outputs (conditional)
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genai.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genai.outputs.searchName : ''
