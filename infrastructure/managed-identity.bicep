// User Assigned Managed Identity for Expense Management System
// This identity will be used by App Service to connect to Azure SQL and Azure OpenAI

@description('Location for the managed identity')
param location string

@description('Base name for the resource')
param baseName string

// Create timestamp-like suffix from current date/time components
var identityName = toLower('mid-${baseName}')

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

output managedIdentityId string = managedIdentity.id
output managedIdentityName string = managedIdentity.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
