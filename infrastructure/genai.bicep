// Azure OpenAI and AI Search Bicep template for Expense Management System
// Uses GPT-4o model in Sweden region, S0 SKUs for cost optimization

@description('Location for the resources (App Service location)')
param location string

@description('Base name for the resources')
param baseName string

@description('Managed Identity Principal ID for role assignments')
param managedIdentityPrincipalId string

// Azure OpenAI must be in Sweden for GPT-4o
var openAILocation = 'swedencentral'
var openAIName = toLower('oai-${baseName}')
var searchName = toLower('search-${baseName}')
var modelDeploymentName = 'gpt-4o'

// Cognitive Services OpenAI User role
var cognitiveServicesOpenAIUserRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')

// Search Index Data Contributor role
var searchIndexDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')

// Search Service Contributor role
var searchServiceContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')

// Azure OpenAI resource
resource openAI 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: openAIName
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// GPT-4o Model Deployment
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAI
  name: modelDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
    raiPolicyName: 'Microsoft.Default'
  }
}

// Azure AI Search resource
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

// Role assignment: Cognitive Services OpenAI User for Managed Identity on OpenAI
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRole)
  scope: openAI
  properties: {
    roleDefinitionId: cognitiveServicesOpenAIUserRole
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Search Index Data Contributor for Managed Identity on Search
resource searchIndexRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, managedIdentityPrincipalId, searchIndexDataContributorRole)
  scope: search
  properties: {
    roleDefinitionId: searchIndexDataContributorRole
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Search Service Contributor for Managed Identity on Search
resource searchContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, managedIdentityPrincipalId, searchServiceContributorRole)
  scope: search
  properties: {
    roleDefinitionId: searchServiceContributorRole
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAI.properties.endpoint
output openAIModelName string = gpt4oDeployment.name
output openAIName string = openAI.name
output searchEndpoint string = 'https://${search.name}.search.windows.net'
output searchName string = search.name
