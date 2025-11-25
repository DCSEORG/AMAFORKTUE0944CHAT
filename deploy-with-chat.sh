#!/bin/bash

# =============================================================================
# Expense Management System - Deployment Script (With GenAI/Chat UI)
# =============================================================================
# This script deploys everything including GenAI services:
# - Resource Group
# - App Service with Managed Identity
# - Azure SQL Database with Entra ID Authentication
# - Azure OpenAI (GPT-4o in Sweden)
# - Azure AI Search
# - Application Code with Chat UI
#
# Usage: ./deploy-with-chat.sh
# 
# Prerequisites:
# - Azure CLI installed and logged in (az login)
# - Appropriate permissions in Azure subscription
# =============================================================================

set -e

# =============================================================================
# CONFIGURATION - Update these values before running
# =============================================================================
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
BASE_NAME="expensemgmt"

# Get current user info for SQL Admin
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

echo "============================================="
echo "Expense Management System Deployment (With GenAI)"
echo "============================================="
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Admin Login: $ADMIN_LOGIN"
echo "Admin Object ID: $ADMIN_OBJECT_ID"
echo "============================================="

# =============================================================================
# Step 1: Create Resource Group
# =============================================================================
echo ""
echo "Step 1: Creating Resource Group..."
az group create --name $RESOURCE_GROUP --location $LOCATION

# =============================================================================
# Step 2: Deploy Infrastructure (Bicep) with GenAI
# =============================================================================
echo ""
echo "Step 2: Deploying Infrastructure with GenAI resources..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infrastructure/main.bicep \
  --parameters \
    baseName=$BASE_NAME \
    adminLogin="$ADMIN_LOGIN" \
    adminObjectId="$ADMIN_OBJECT_ID" \
    deployGenAI=true \
  --query "properties.outputs" -o json)

# Extract outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
APP_SERVICE_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceUrl.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.databaseName.value')
OPENAI_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.searchEndpoint.value')

echo ""
echo "Infrastructure deployed successfully!"
echo "App Service: $APP_SERVICE_NAME"
echo "SQL Server: $SQL_SERVER_NAME"
echo "Database: $DATABASE_NAME"
echo "Managed Identity: $MANAGED_IDENTITY_NAME"
echo "OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "OpenAI Model: $OPENAI_MODEL_NAME"
echo "Search Endpoint: $SEARCH_ENDPOINT"

# =============================================================================
# Step 3: Configure App Service Connection String and Settings
# =============================================================================
echo ""
echo "Step 3: Configuring App Service..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config connection-string set \
  --name $APP_SERVICE_NAME \
  --resource-group $RESOURCE_GROUP \
  --connection-string-type SQLAzure \
  --settings DefaultConnection="$CONNECTION_STRING"

# Configure OpenAI settings
az webapp config appsettings set \
  --name $APP_SERVICE_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
    "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
    "Search__Endpoint=$SEARCH_ENDPOINT" \
    "GenAI__Enabled=true"

# =============================================================================
# Step 4: Wait for SQL Server to be ready
# =============================================================================
echo ""
echo "Step 4: Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30

# =============================================================================
# Step 5: Add local IP to SQL Server firewall
# =============================================================================
echo ""
echo "Step 5: Adding local IP to SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name "LocalDev-$MY_IP" \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP || true

# =============================================================================
# Step 6: Install Python dependencies
# =============================================================================
echo ""
echo "Step 6: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

# =============================================================================
# Step 7: Update Python scripts with server/database values
# =============================================================================
echo ""
echo "Step 7: Configuring Python scripts..."

# Update run-sql.py
sed -i.bak "s/SERVER = \"example.database.windows.net\"/SERVER = \"${SQL_SERVER_FQDN}\"/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/DATABASE = \"database_name\"/DATABASE = \"${DATABASE_NAME}\"/g" run-sql.py && rm -f run-sql.py.bak

# Update run-sql-dbrole.py
sed -i.bak "s/SERVER = \"example.database.windows.net\"/SERVER = \"${SQL_SERVER_FQDN}\"/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/DATABASE = \"database_name\"/DATABASE = \"${DATABASE_NAME}\"/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak

# Update script.sql with managed identity name
sed -i.bak "s/MANAGED-IDENTITY/${MANAGED_IDENTITY_NAME}/g" script.sql && rm -f script.sql.bak

# Update run-sql-stored-procs.py
sed -i.bak "s/SERVER = \"example.database.windows.net\"/SERVER = \"${SQL_SERVER_FQDN}\"/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s/DATABASE = \"database_name\"/DATABASE = \"${DATABASE_NAME}\"/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

# =============================================================================
# Step 8: Import Database Schema
# =============================================================================
echo ""
echo "Step 8: Importing database schema..."
python3 run-sql.py

# =============================================================================
# Step 9: Configure Database Roles for Managed Identity
# =============================================================================
echo ""
echo "Step 9: Configuring database roles for managed identity..."
python3 run-sql-dbrole.py

# =============================================================================
# Step 10: Deploy Stored Procedures
# =============================================================================
echo ""
echo "Step 10: Deploying stored procedures..."
python3 run-sql-stored-procs.py

# =============================================================================
# Step 11: Deploy Application Code
# =============================================================================
echo ""
echo "Step 11: Deploying application code..."
az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_SERVICE_NAME \
  --src-path ./app.zip

# =============================================================================
# Deployment Complete
# =============================================================================
echo ""
echo "============================================="
echo "Deployment Complete (With GenAI)!"
echo "============================================="
echo ""
echo "App Service URL: ${APP_SERVICE_URL}/Index"
echo ""
echo "NOTE: Navigate to ${APP_SERVICE_URL}/Index to view the application"
echo "      (not the root URL)"
echo ""
echo "Chat UI is available with full GenAI capabilities!"
echo "- Azure OpenAI (GPT-4o) is configured"
echo "- AI Search is configured for RAG pattern"
echo ""
echo "To view API documentation: ${APP_SERVICE_URL}/swagger"
echo ""
echo "============================================="
