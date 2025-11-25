// Azure SQL Database Bicep template for Expense Management System
// Uses Entra ID (Azure AD) Only Authentication as required by MCAPS governance

@description('Location for the SQL Server')
param location string

@description('Base name for the resource')
param baseName string

@description('Admin login (Entra ID user principal name)')
param adminLogin string

@description('Admin Object ID (Entra ID object ID)')
param adminObjectId string

@description('Managed Identity Principal ID for database access')
param managedIdentityPrincipalId string

var sqlServerName = toLower('sql-${baseName}')
var databaseName = 'Northwind'

// SQL Server with Entra ID Only Authentication
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

// Allow Azure Services firewall rule
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Northwind Database with Basic tier
resource database 'Microsoft.Sql/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output managedIdentityPrincipalId string = managedIdentityPrincipalId
