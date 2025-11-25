# Azure Services Architecture

This diagram shows the Azure services deployed by this repository and how they connect to each other.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              AZURE SUBSCRIPTION                              │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐ │
│  │                        Resource Group (rg-expensemgmt-demo)            │ │
│  │                                                                         │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐   │ │
│  │  │                    User Assigned Managed Identity               │   │ │
│  │  │                        (mid-expensemgmt-xxx)                    │   │ │
│  │  │                                                                  │   │ │
│  │  │   Used by App Service to authenticate to:                       │   │ │
│  │  │   - Azure SQL Database                                          │   │ │
│  │  │   - Azure OpenAI (when deployed with GenAI)                     │   │ │
│  │  │   - Azure AI Search (when deployed with GenAI)                  │   │ │
│  │  └─────────────────────────────────────────────────────────────────┘   │ │
│  │                              │                                          │ │
│  │              ┌───────────────┼───────────────┐                          │ │
│  │              │               │               │                          │ │
│  │              ▼               ▼               ▼                          │ │
│  │  ┌───────────────────┐  ┌───────────────────┐  ┌────────────────────┐  │ │
│  │  │   App Service     │  │   Azure SQL       │  │  Azure OpenAI      │  │ │
│  │  │  (app-xxx)        │  │  (sql-xxx)        │  │  (oai-xxx)         │  │ │
│  │  │                   │  │                   │  │  Sweden Central    │  │ │
│  │  │  ASP.NET 8.0      │──│  Northwind DB     │  │                    │  │ │
│  │  │  Razor Pages      │  │  Basic Tier       │  │  GPT-4o Model      │  │ │
│  │  │  REST APIs        │  │  Entra ID Auth    │  │  S0 SKU            │  │ │
│  │  │  Chat UI          │  │                   │  │                    │  │ │
│  │  │                   │  │                   │  │                    │  │ │
│  │  │  S1 Standard      │  │                   │  │                    │  │ │
│  │  └───────────────────┘  └───────────────────┘  └────────────────────┘  │ │
│  │           │                      ▲                      │               │ │
│  │           │                      │                      │               │ │
│  │           │     Stored Procedures│                      │               │ │
│  │           └──────────────────────┘                      │               │ │
│  │                                                         │               │ │
│  │           │                                             │               │ │
│  │           │     Function Calling / RAG                  │               │ │
│  │           └─────────────────────────────────────────────┘               │ │
│  │                                                                         │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐   │ │
│  │  │                    Azure AI Search (Optional)                   │   │ │
│  │  │                      (search-xxx)                               │   │ │
│  │  │                                                                  │   │ │
│  │  │  Basic SKU - Used for RAG pattern                               │   │ │
│  │  │  Indexes expense data for semantic search                       │   │ │
│  │  └─────────────────────────────────────────────────────────────────┘   │ │
│  │                                                                         │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

                                    │
                                    │ HTTPS
                                    │
                                    ▼
                            ┌───────────────┐
                            │     Users     │
                            │   (Browser)   │
                            └───────────────┘
```

## Service Connections

| Source | Destination | Purpose | Authentication |
|--------|-------------|---------|----------------|
| App Service | Azure SQL | Expense data CRUD | Managed Identity |
| App Service | Azure OpenAI | Chat AI responses | Managed Identity |
| App Service | AI Search | Semantic search | Managed Identity |
| Users | App Service | Web interface | HTTPS |

## Deployment Options

### Basic Deployment (deploy.sh)
- Resource Group
- Managed Identity
- App Service (S1)
- Azure SQL (Basic)
- Web Application

### Full Deployment with GenAI (deploy-with-chat.sh)
- All Basic Deployment resources
- Azure OpenAI (S0, Sweden Central)
- GPT-4o Model Deployment
- Azure AI Search (Basic)
- Function Calling enabled

## Security Features

1. **Entra ID Only Authentication** - SQL Server uses Azure AD authentication only (no SQL auth)
2. **Managed Identity** - No secrets or connection strings with passwords
3. **HTTPS Only** - All traffic encrypted
4. **TLS 1.2** - Minimum TLS version enforced
5. **Role Assignments** - Principle of least privilege for identity access
