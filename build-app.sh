#!/bin/bash

# =============================================================================
# Build and Package Application for Azure Deployment
# =============================================================================
# This script builds the .NET application and creates app.zip for deployment
#
# Usage: ./build-app.sh
# =============================================================================

set -e

echo "============================================="
echo "Building Expense Management Application"
echo "============================================="

# Navigate to source directory
cd src/ExpenseManagement

# Restore packages
echo ""
echo "Step 1: Restoring NuGet packages..."
dotnet restore

# Build application
echo ""
echo "Step 2: Building application..."
dotnet build --configuration Release --no-restore

# Publish application
echo ""
echo "Step 3: Publishing application..."
dotnet publish --configuration Release --no-build --output ./publish

# Navigate back to root
cd ../..

# Create zip file with correct structure
# IMPORTANT: Files must be at the root of the zip, not in a subdirectory
echo ""
echo "Step 4: Creating app.zip..."
cd src/ExpenseManagement/publish
zip -r ../../../app.zip ./*
cd ../../..

echo ""
echo "============================================="
echo "Build Complete!"
echo "============================================="
echo ""
echo "app.zip has been created in the repository root."
echo ""
echo "To deploy to Azure App Service:"
echo "  az webapp deploy --resource-group <RG> --name <APP> --src-path ./app.zip"
echo ""
echo "NOTE: Navigate to <app-url>/Index to view the application"
echo ""
