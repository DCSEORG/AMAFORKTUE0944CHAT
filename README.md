![Header image](https://github.com/DougChisholm/App-Mod-Assist/blob/main/repo-header.png)

# Expense Management System

A modern Azure-native web application for managing employee expenses, featuring a clean UI, REST APIs, and AI-powered chat assistance.

## Features

- **Dashboard** - Overview of expenses with statistics
- **Expense Management** - Create, view, submit, and manage expenses
- **Approval Workflow** - Managers can approve/reject submitted expenses
- **AI Chat Assistant** - Natural language interface for expense operations (optional)
- **REST APIs** - Full API with Swagger documentation

## Quick Start

### Prerequisites

1. Azure CLI installed and logged in (`az login`)
2. .NET 8 SDK (for local development)
3. Appropriate Azure subscription permissions

### Deployment

#### Option 1: Basic Deployment (Database + App Service)

```bash
./deploy.sh
```

This deploys:
- Resource Group
- User Assigned Managed Identity
- App Service (S1 Standard)
- Azure SQL Database (Basic tier, Entra ID auth)
- Web Application

#### Option 2: Full Deployment with AI Chat (Recommended)

```bash
./deploy-with-chat.sh
```

This deploys everything in Option 1, plus:
- Azure OpenAI (GPT-4o model, Sweden Central)
- Azure AI Search (Basic tier)
- Full AI-powered chat functionality

### Accessing the Application

After deployment, navigate to:
```
https://<app-service-url>/Index
```

> **Note**: Navigate to `/Index` not the root URL

API Documentation:
```
https://<app-service-url>/swagger
```

## Local Development

1. Update connection string in `src/ExpenseManagement/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=tcp:YOUR_SERVER.database.windows.net,1433;Database=Northwind;Authentication=Active Directory Default;..."
   }
   ```

2. Login to Azure:
   ```bash
   az login
   ```

3. Run the application:
   ```bash
   cd src/ExpenseManagement
   dotnet run
   ```

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed Azure services diagram.

## Project Structure

```
├── infrastructure/          # Bicep templates
│   ├── main.bicep           # Main orchestration
│   ├── app-service.bicep    # App Service (S1)
│   ├── azure-sql.bicep      # Azure SQL (Basic)
│   ├── managed-identity.bicep
│   └── genai.bicep          # Azure OpenAI & AI Search
├── src/ExpenseManagement/   # .NET 8 application
│   ├── Controllers/         # API controllers
│   ├── Models/              # Data models
│   ├── Pages/               # Razor pages
│   ├── Services/            # Business logic
│   └── wwwroot/             # Static files
├── Database-Schema/         # SQL schema
├── deploy.sh                # Basic deployment
├── deploy-with-chat.sh      # Full deployment with AI
├── build-app.sh             # Build & package app
└── app.zip                  # Deployment package
```

## Security

- **Entra ID Only Authentication** - No SQL passwords
- **Managed Identity** - Passwordless Azure service access
- **HTTPS Only** - Encrypted traffic
- **TLS 1.2** - Minimum version enforced

## Legacy Screenshots

This modern application was generated from legacy screenshots in the `Legacy-Screenshots` folder, demonstrating how GitHub Copilot coding agent can modernize applications.
