#!/bin/bash

# Steel Mill AI Platform - Local Development Environment Startup Script
# This script starts the complete local development stack

set -e

echo "🏭 Starting Steel Mill AI Platform Local Development Environment..."

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

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    print_error "Docker is not running. Please start Docker Desktop and try again."
    exit 1
fi

# Check if docker-compose is available
if ! command -v docker-compose &> /dev/null; then
    print_error "docker-compose is not installed. Please install it and try again."
    exit 1
fi

# Create necessary directories
print_status "Creating necessary directories..."
mkdir -p config/grafana/dashboards/production
mkdir -p config/grafana/dashboards/quality
mkdir -p config/grafana/dashboards/maintenance
mkdir -p config/grafana/dashboards/infrastructure
mkdir -p config/grafana/dashboards/ml-models
mkdir -p data/mlflow
mkdir -p data/influxdb
mkdir -p logs

# Set permissions for Grafana
print_status "Setting permissions for Grafana..."
sudo chown -R 472:472 config/grafana/ 2>/dev/null || print_warning "Could not set Grafana permissions (this may cause issues)"

# Pull latest images
print_status "Pulling latest Docker images..."
docker-compose -f docker-compose.dev.yml pull

# Start infrastructure services first (databases, message brokers)
print_status "Starting infrastructure services..."
docker-compose -f docker-compose.dev.yml up -d \
    cosmosdb-emulator \
    redis \
    mongodb \
    influxdb \
    zookeeper \
    kafka \
    rabbitmq

# Wait for infrastructure services to be healthy
print_status "Waiting for infrastructure services to start..."
sleep 30

# Check service health
check_service_health() {
    local service=$1
    local port=$2
    local max_attempts=30
    local attempt=1

    while [ $attempt -le $max_attempts ]; do
        if nc -z localhost $port 2>/dev/null; then
            print_success "$service is ready on port $port"
            return 0
        fi
        print_status "Waiting for $service (attempt $attempt/$max_attempts)..."
        sleep 2
        ((attempt++))
    done
    
    print_warning "$service may not be ready yet on port $port"
    return 1
}

# Check critical services
print_status "Checking service health..."
check_service_health "Redis" 6379
check_service_health "MongoDB" 27017
check_service_health "Kafka" 9092
check_service_health "RabbitMQ" 5672

# Start monitoring services
print_status "Starting monitoring services..."
docker-compose -f docker-compose.dev.yml up -d \
    prometheus \
    grafana \
    jaeger

# Start ML and development tools
print_status "Starting ML and development tools..."
docker-compose -f docker-compose.dev.yml up -d \
    mlflow \
    jupyter

# Start management UIs
print_status "Starting management interfaces..."
docker-compose -f docker-compose.dev.yml up -d \
    kafka-ui \
    mongo-express \
    redis-commander \
    seq

# Wait for all services to be ready
print_status "Waiting for all services to be ready..."
sleep 45

# Display service status
print_success "Steel Mill AI Platform Local Environment Started!"
echo
echo "=================================================================="
echo "                    🏭 SERVICE ENDPOINTS                          "
echo "=================================================================="
echo
echo "📊 MONITORING & VISUALIZATION:"
echo "  • Grafana Dashboard:      http://localhost:3001 (admin/steelmill123)"
echo "  • Prometheus:             http://localhost:9090"
echo "  • Jaeger Tracing:         http://localhost:16686"
echo "  • Seq Logging:            http://localhost:5341 (admin/steelmill123)"
echo
echo "🛠️  DEVELOPMENT TOOLS:"
echo "  • Jupyter Notebooks:      http://localhost:8888 (token: steelmill123)"
echo "  • MLflow:                 http://localhost:5000"
echo
echo "🗄️  DATABASE MANAGEMENT:"
echo "  • Cosmos DB Emulator:     https://localhost:8081/_explorer/index.html"
echo "  • MongoDB Express:        http://localhost:8082 (admin/steelmill123)"
echo "  • Redis Commander:        http://localhost:8083 (admin/steelmill123)"
echo
echo "📨 MESSAGE BROKER MANAGEMENT:"
echo "  • Kafka UI:               http://localhost:8080"
echo "  • RabbitMQ Management:    http://localhost:15672 (steelmill/steelmill123)"
echo
echo "🔧 DATABASE CONNECTIONS:"
echo "  • MongoDB:     mongodb://steelmill:steelmill123@localhost:27017/steelmill"
echo "  • Redis:       redis://localhost:6379 (password: steelmill123)"
echo "  • InfluxDB:    http://localhost:8086 (steelmill/steelmill123)"
echo
echo "=================================================================="
echo "                    🚀 NEXT STEPS                                "
echo "=================================================================="
echo
echo "1. Start your .NET microservices:"
echo "   cd services/furnace-service && dotnet run"
echo "   cd services/quality-control-service && dotnet run"
echo "   cd services/rolling-mill-service && dotnet run"
echo
echo "2. Start the ML inference service:"
echo "   cd ml-pipeline && python -m uvicorn main:app --reload --port 8000"
echo
echo "3. Start the React dashboard:"
echo "   cd frontend && npm start"
echo
echo "4. Run tests:"
echo "   ./scripts/run-tests.sh"
echo
echo "=================================================================="
echo

# Create a simple health check script
cat > scripts/health-check.sh << 'EOF'
#!/bin/bash
echo "🏭 Steel Mill AI Platform - Health Check"
echo "========================================"

services=("Redis:6379" "MongoDB:27017" "Kafka:9092" "RabbitMQ:5672" "InfluxDB:8086" "Prometheus:9090" "Grafana:3001")

for service in "${services[@]}"; do
    IFS=':' read -r name port <<< "$service"
    if nc -z localhost $port 2>/dev/null; then
        echo "✅ $name (port $port) - OK"
    else
        echo "❌ $name (port $port) - NOT RESPONDING"
    fi
done
EOF

chmod +x scripts/health-check.sh

print_success "Local development environment is ready!"
print_status "Run './scripts/health-check.sh' to check service status"
print_status "Run './scripts/stop-local-dev.sh' to stop all services"

# Ask if user wants to open key interfaces
read -p "Would you like to open key interfaces in your browser? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_status "Opening key interfaces..."
    sleep 2
    open http://localhost:3001 2>/dev/null || print_status "Please open http://localhost:3001 for Grafana"
    open http://localhost:8888 2>/dev/null || print_status "Please open http://localhost:8888 for Jupyter"
    open http://localhost:8080 2>/dev/null || print_status "Please open http://localhost:8080 for Kafka UI"
fi

print_success "Steel Mill AI Platform local development environment started successfully! 🎉"