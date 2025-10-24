#!/bin/bash

# Setup script for Agent Group Chat application

echo "🤖 Agent Group Chat - Setup Script"
echo "===================================="
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK is not installed. Please install .NET 9.0 or later."
    exit 1
fi

echo "✅ .NET SDK found: $(dotnet --version)"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "⚠️  Azure CLI is not installed. You'll need to configure authentication manually."
    echo "   Visit: https://docs.microsoft.com/cli/azure/install-azure-cli"
else
    echo "✅ Azure CLI found"
    
    # Check if user is logged in
    if az account show &> /dev/null; then
        echo "✅ Azure CLI is logged in"
    else
        echo "⚠️  Azure CLI is not logged in. Please run: az login"
    fi
fi

echo ""
echo "📝 Configuration Steps:"
echo ""
echo "1. Configure Azure OpenAI settings:"
echo "   Edit appsettings.json and set:"
echo "   - AzureOpenAI:Endpoint"
echo "   - AzureOpenAI:DeploymentName"
echo ""
echo "   OR set environment variables:"
echo "   export AZURE_OPENAI_ENDPOINT='https://your-resource.openai.azure.com/'"
echo "   export AZURE_OPENAI_DEPLOYMENT_NAME='gpt-4o-mini'"
echo ""
echo "2. Ensure Azure authentication is configured:"
echo "   - Run 'az login' if using Azure CLI"
echo "   - Or configure service principal credentials"
echo ""
echo "3. Run the application:"
echo "   dotnet run"
echo ""
echo "🚀 Ready to start? Run: dotnet run"
echo ""
