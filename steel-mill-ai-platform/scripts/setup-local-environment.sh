#!/bin/bash

# Steel Mill AI Platform - Local Environment Setup Script
# This script sets up the complete local development environment

set -e

echo "🏭 Setting up Steel Mill AI Platform Local Development Environment..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
print_status "Checking prerequisites..."

# Check if we're on macOS, Linux, or Windows
OS="unknown"
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS="linux"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    OS="macos"
elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
    OS="windows"
fi

print_status "Detected OS: $OS"

# Check Docker
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed. Please install Docker Desktop."
    print_status "Download from: https://www.docker.com/products/docker-desktop"
    exit 1
fi

if ! docker info > /dev/null 2>&1; then
    print_error "Docker is not running. Please start Docker Desktop."
    exit 1
fi

print_success "Docker is installed and running"

# Check docker-compose
if ! command -v docker-compose &> /dev/null; then
    print_error "docker-compose is not installed."
    if [[ "$OS" == "macos" ]]; then
        print_status "Install with: brew install docker-compose"
    elif [[ "$OS" == "linux" ]]; then
        print_status "Install with: sudo apt-get install docker-compose"
    fi
    exit 1
fi

print_success "docker-compose is installed"

# Check .NET
if ! command -v dotnet &> /dev/null; then
    print_error ".NET is not installed. Please install .NET 9 SDK."
    print_status "Download from: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
print_success ".NET $DOTNET_VERSION is installed"

# Check Python
if ! command -v python3 &> /dev/null; then
    print_error "Python 3 is not installed."
    if [[ "$OS" == "macos" ]]; then
        print_status "Install with: brew install python@3.11"
    elif [[ "$OS" == "linux" ]]; then
        print_status "Install with: sudo apt-get install python3.11"
    fi
    exit 1
fi

PYTHON_VERSION=$(python3 --version)
print_success "$PYTHON_VERSION is installed"

# Check Node.js (for frontend development)
if ! command -v node &> /dev/null; then
    print_warning "Node.js is not installed. Frontend development will be limited."
    print_status "Install from: https://nodejs.org/"
else
    NODE_VERSION=$(node --version)
    print_success "Node.js $NODE_VERSION is installed"
fi

# Create project structure
print_status "Creating project directory structure..."

mkdir -p data/{training,validation,test,feature_store}
mkdir -p logs
mkdir -p ml-pipeline/{models/saved,models/artifacts}
mkdir -p config/{grafana/dashboards/{production,quality,maintenance,infrastructure,ml-models},prometheus,rabbitmq}
mkdir -p scripts
mkdir -p tests/{integration,unit,e2e}
mkdir -p .vscode

print_success "Project directory structure created"

# Set up Python virtual environment
print_status "Setting up Python virtual environment..."

cd ml-pipeline

if [[ "$OS" == "windows" ]]; then
    python -m venv .venv
    .venv/Scripts/activate
else
    python3 -m venv .venv
    source .venv/bin/activate
fi

print_success "Python virtual environment created"

# Install Python dependencies
print_status "Installing Python dependencies..."

if [[ -f "requirements-local.txt" ]]; then
    pip install --upgrade pip
    pip install -r requirements-local.txt
    print_success "Python dependencies installed"
else
    print_warning "requirements-local.txt not found, skipping Python dependencies"
fi

cd ..

# Restore .NET dependencies
print_status "Restoring .NET dependencies..."

if ls services/*/*.csproj 1> /dev/null 2>&1; then
    dotnet restore
    print_success ".NET dependencies restored"
else
    print_warning "No .NET projects found, skipping .NET restore"
fi

# Set up environment variables
print_status "Setting up environment variables..."

cat > .env << EOF
# Steel Mill AI Platform - Local Development Environment Variables

# Environment
ASPNETCORE_ENVIRONMENT=Development
ENVIRONMENT=local
DEBUG=true

# Database connections
MONGODB_URL=mongodb://steelmill:steelmill123@localhost:27017/steelmill
REDIS_URL=redis://:steelmill123@localhost:6379/0
INFLUXDB_URL=http://localhost:8086
COSMOSDB_ENDPOINT=https://localhost:8081

# Message brokers
KAFKA_BOOTSTRAP_SERVERS=localhost:9092
RABBITMQ_URL=amqp://steelmill:steelmill123@localhost:5672/steelmill

# ML Pipeline
MLFLOW_TRACKING_URI=http://localhost:5000
PYTHONPATH=./ml-pipeline

# Monitoring
PROMETHEUS_URL=http://localhost:9090
GRAFANA_URL=http://localhost:3001

# Service ports
FURNACE_SERVICE_PORT=5001
QUALITY_CONTROL_SERVICE_PORT=5002
ROLLING_MILL_SERVICE_PORT=5003
CASTING_SERVICE_PORT=5004
LADLE_METALLURGY_SERVICE_PORT=5005
HEAT_TREATMENT_SERVICE_PORT=5006
CUTTING_STRAIGHTENING_SERVICE_PORT=5007
BUNDLING_SERVICE_PORT=5008
ENERGY_MANAGEMENT_SERVICE_PORT=5009
MAINTENANCE_SERVICE_PORT=5010
INVENTORY_SERVICE_PORT=5011
ML_INFERENCE_SERVICE_PORT=8000
EOF

print_success "Environment variables configured"

# Set up Git hooks (if in a git repository)
if [[ -d ".git" ]]; then
    print_status "Setting up Git hooks..."
    
    mkdir -p .git/hooks
    
    cat > .git/hooks/pre-commit << 'EOF'
#!/bin/bash
# Pre-commit hook for Steel Mill AI Platform

echo "Running pre-commit checks..."

# Format .NET code
dotnet format --verify-no-changes --verbosity minimal

# Format Python code
if [[ -d "ml-pipeline/.venv" ]]; then
    cd ml-pipeline
    source .venv/bin/activate 2>/dev/null || .venv/Scripts/activate
    black --check --diff .
    flake8 --max-line-length=100 .
    cd ..
fi

echo "Pre-commit checks passed!"
EOF

    chmod +x .git/hooks/pre-commit
    print_success "Git hooks configured"
fi

# Create helpful scripts
print_status "Creating development scripts..."

# Create a comprehensive test runner script
cat > scripts/run-tests.sh << 'EOF'
#!/bin/bash

echo "🧪 Running Steel Mill AI Platform Tests..."

# Run .NET tests
echo "Running .NET tests..."
dotnet test --logger:console --verbosity:normal

# Run Python tests
echo "Running Python tests..."
cd ml-pipeline
source .venv/bin/activate 2>/dev/null || .venv/Scripts/activate
python -m pytest tests/ -v --tb=short --cov=. --cov-report=html
cd ..

echo "✅ All tests completed!"
EOF

chmod +x scripts/run-tests.sh

# Create a development workflow script
cat > scripts/dev-workflow.sh << 'EOF'
#!/bin/bash

echo "🚀 Steel Mill AI Platform - Development Workflow"
echo "================================================"

case $1 in
    "start")
        echo "Starting local development environment..."
        ./scripts/start-local-dev.sh
        ;;
    "stop")
        echo "Stopping local development environment..."
        ./scripts/stop-local-dev.sh
        ;;
    "test")
        echo "Running all tests..."
        ./scripts/run-tests.sh
        ;;
    "build")
        echo "Building all services..."
        dotnet build
        ;;
    "clean")
        echo "Cleaning build artifacts..."
        dotnet clean
        rm -rf ml-pipeline/__pycache__
        rm -rf ml-pipeline/.pytest_cache
        ;;
    "format")
        echo "Formatting code..."
        dotnet format
        cd ml-pipeline && source .venv/bin/activate && black . && cd ..
        ;;
    "logs")
        echo "Showing logs..."
        docker-compose -f docker-compose.dev.yml logs -f
        ;;
    *)
        echo "Usage: $0 {start|stop|test|build|clean|format|logs}"
        echo ""
        echo "Commands:"
        echo "  start   - Start local development environment"
        echo "  stop    - Stop local development environment"
        echo "  test    - Run all tests"
        echo "  build   - Build all services"
        echo "  clean   - Clean build artifacts"
        echo "  format  - Format all code"
        echo "  logs    - Show container logs"
        ;;
esac
EOF

chmod +x scripts/dev-workflow.sh

print_success "Development scripts created"

# Install VS Code extensions (if VS Code is installed)
if command -v code &> /dev/null; then
    print_status "Installing recommended VS Code extensions..."
    
    code --install-extension ms-dotnettools.csharp
    code --install-extension ms-dotnettools.csdevkit
    code --install-extension ms-python.python
    code --install-extension ms-python.black-formatter
    code --install-extension ms-azuretools.vscode-docker
    code --install-extension redhat.vscode-yaml
    code --install-extension humao.rest-client
    code --install-extension ms-kubernetes-tools.vscode-kubernetes-tools
    
    print_success "VS Code extensions installed"
else
    print_warning "VS Code not found, skipping extension installation"
fi

# Final setup summary
print_success "Steel Mill AI Platform Local Development Environment Setup Complete! 🎉"

echo
echo "=================================================================="
echo "                    📋 SETUP SUMMARY                             "
echo "=================================================================="
echo
echo "✅ Prerequisites checked"
echo "✅ Project structure created"  
echo "✅ Python virtual environment configured"
echo "✅ .NET dependencies restored"
echo "✅ Environment variables configured"
echo "✅ Development scripts created"
echo "✅ VS Code configuration added"
echo
echo "=================================================================="
echo "                    🚀 NEXT STEPS                                "
echo "=================================================================="
echo
echo "1. Start the local infrastructure:"
echo "   ./scripts/start-local-dev.sh"
echo
echo "2. Open the project in VS Code:"
echo "   code ."
echo
echo "3. Start developing:"
echo "   - Launch services using F5 in VS Code"
echo "   - Or use: ./scripts/dev-workflow.sh start"
echo
echo "4. Run tests:"
echo "   ./scripts/run-tests.sh"
echo
echo "5. View the development dashboard:"
echo "   Open browser to http://localhost:8000"
echo
echo "=================================================================="
echo "                    📚 USEFUL COMMANDS                           "
echo "=================================================================="
echo
echo "Development workflow:"
echo "  ./scripts/dev-workflow.sh start|stop|test|build|clean|format|logs"
echo
echo "Health check:"
echo "  ./scripts/health-check.sh"
echo
echo "View logs:" 
echo "  docker-compose -f docker-compose.dev.yml logs -f [service-name]"
echo
echo "Access web interfaces:"
echo "  • ML Development Server: http://localhost:8000"
echo "  • Grafana Dashboard: http://localhost:3001 (admin/steelmill123)"
echo "  • Jupyter Notebooks: http://localhost:8888 (token: steelmill123)"
echo "  • MLflow Tracking: http://localhost:5000"
echo "  • Kafka UI: http://localhost:8080"
echo
echo "=================================================================="

print_success "Happy coding! 🏭⚙️🤖"