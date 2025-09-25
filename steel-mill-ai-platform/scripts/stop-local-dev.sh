#!/bin/bash

# Steel Mill AI Platform - Stop Local Development Environment Script
# This script stops all local development services

set -e

echo "🏭 Stopping Steel Mill AI Platform Local Development Environment..."

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
    print_error "Docker is not running."
    exit 1
fi

# Parse command line arguments
REMOVE_VOLUMES=false
REMOVE_IMAGES=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --volumes)
            REMOVE_VOLUMES=true
            shift
            ;;
        --images)
            REMOVE_IMAGES=true
            shift
            ;;
        --clean)
            REMOVE_VOLUMES=true
            REMOVE_IMAGES=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  --volumes    Remove all volumes (deletes all data)"
            echo "  --images     Remove all images"
            echo "  --clean      Remove volumes and images (complete cleanup)"
            echo "  -h, --help   Show this help message"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Stop all services
print_status "Stopping all Steel Mill AI Platform services..."
docker-compose -f docker-compose.dev.yml down

if [ "$REMOVE_VOLUMES" = true ]; then
    print_warning "Removing all volumes (this will delete all data)..."
    read -p "Are you sure you want to remove all data volumes? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        print_status "Removing volumes..."
        docker volume rm steel-mill-cosmosdb-data 2>/dev/null || true
        docker volume rm steel-mill-redis-data 2>/dev/null || true
        docker volume rm steel-mill-mongodb-data 2>/dev/null || true
        docker volume rm steel-mill-influxdb-data 2>/dev/null || true
        docker volume rm steel-mill-influxdb-config 2>/dev/null || true
        docker volume rm steel-mill-zookeeper-data 2>/dev/null || true
        docker volume rm steel-mill-zookeeper-logs 2>/dev/null || true
        docker volume rm steel-mill-kafka-data 2>/dev/null || true
        docker volume rm steel-mill-rabbitmq-data 2>/dev/null || true
        docker volume rm steel-mill-prometheus-data 2>/dev/null || true
        docker volume rm steel-mill-grafana-data 2>/dev/null || true
        docker volume rm steel-mill-mlflow-data 2>/dev/null || true
        docker volume rm steel-mill-jupyter-data 2>/dev/null || true
        docker volume rm steel-mill-seq-data 2>/dev/null || true
        print_success "All volumes removed"
    else
        print_status "Volumes preserved"
    fi
fi

if [ "$REMOVE_IMAGES" = true ]; then
    print_warning "Removing all Steel Mill AI Platform images..."
    read -p "Are you sure you want to remove all images? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        print_status "Removing images..."
        # Remove images used in docker-compose
        docker rmi mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest 2>/dev/null || true
        docker rmi redis:7-alpine 2>/dev/null || true
        docker rmi mongo:7 2>/dev/null || true
        docker rmi influxdb:2.7-alpine 2>/dev/null || true
        docker rmi confluentinc/cp-zookeeper:7.4.0 2>/dev/null || true
        docker rmi confluentinc/cp-kafka:7.4.0 2>/dev/null || true
        docker rmi rabbitmq:3.12-management-alpine 2>/dev/null || true
        docker rmi prom/prometheus:v2.47.0 2>/dev/null || true
        docker rmi grafana/grafana:10.1.0 2>/dev/null || true
        docker rmi jaegertracing/all-in-one:1.50 2>/dev/null || true
        docker rmi ghcr.io/mlflow/mlflow:v2.7.1 2>/dev/null || true
        docker rmi jupyter/scipy-notebook:python-3.11 2>/dev/null || true
        docker rmi provectuslabs/kafka-ui:latest 2>/dev/null || true
        docker rmi mongo-express:1.0.0-alpha 2>/dev/null || true
        docker rmi rediscommander/redis-commander:latest 2>/dev/null || true
        docker rmi datalust/seq:latest 2>/dev/null || true
        print_success "All images removed"
    else
        print_status "Images preserved"
    fi
fi

# Remove network
print_status "Removing network..."
docker network rm steel-mill-network 2>/dev/null || true

# Clean up any orphaned containers
print_status "Cleaning up orphaned containers..."
docker container prune -f 2>/dev/null || true

# Clean up dangling images
print_status "Cleaning up dangling images..."
docker image prune -f 2>/dev/null || true

print_success "Steel Mill AI Platform local development environment stopped successfully!"

# Show status
echo
echo "=================================================================="
echo "                    📊 CLEANUP SUMMARY                           "
echo "=================================================================="
echo

if [ "$REMOVE_VOLUMES" = true ]; then
    echo "✅ All data volumes removed"
else
    echo "💾 Data volumes preserved (use --volumes to remove)"
fi

if [ "$REMOVE_IMAGES" = true ]; then
    echo "✅ All images removed"
else
    echo "🐳 Images preserved (use --images to remove)"
fi

echo
echo "🔧 To completely clean up everything:"
echo "   ./scripts/stop-local-dev.sh --clean"
echo
echo "🚀 To restart the environment:"
echo "   ./scripts/start-local-dev.sh"
echo
echo "=================================================================="