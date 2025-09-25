# Steel Mill AI Platform - Local Development Guide

## 🏭 Overview

This guide will help you set up and run the complete Steel Mill AI Platform locally on your development machine without any Azure dependencies. The platform includes microservices, ML pipeline, databases, monitoring, and development tools - everything needed for full-stack development and testing.

## 📋 Prerequisites

### Required Software
- **Docker Desktop** (4.20+ with Kubernetes enabled)
- **.NET 9 SDK** (9.0.0+)
- **Python 3.11+**
- **Git**
- **Node.js 18+** (for frontend development)

### Recommended Tools
- **Visual Studio Code** with recommended extensions
- **Postman** or **Insomnia** for API testing
- **DBeaver** or similar for database management

### System Requirements
- **RAM**: 16GB minimum, 32GB recommended
- **Storage**: 20GB free space
- **CPU**: 4+ cores recommended
- **Network**: Internet connection for initial setup

## 🚀 Quick Start

### 1. Automated Setup (Recommended)

```bash
# Clone the repository (if not already done)
git clone <repository-url>
cd steel-mill-ai-platform

# Run the automated setup script
./scripts/setup-local-environment.sh

# Start the local development environment
./scripts/start-local-dev.sh
```

### 2. Manual Setup

If you prefer manual setup or encounter issues with the automated script:

```bash
# 1. Create Python virtual environment
cd ml-pipeline
python3 -m venv .venv
source .venv/bin/activate  # On Windows: .venv\Scripts\activate
pip install -r requirements-local.txt
cd ..

# 2. Restore .NET dependencies
dotnet restore

# 3. Start infrastructure services
docker-compose -f docker-compose.dev.yml up -d

# 4. Start individual services (in separate terminals)
cd services/furnace-service && dotnet run
cd services/quality-control-service && dotnet run
cd services/rolling-mill-service && dotnet run
cd ml-pipeline && python local_development_server.py
```

## 🛠️ Development Environment

### Local Infrastructure Stack

The development environment includes the following services:

| Service | URL | Credentials | Purpose |
|---------|-----|------------|---------|
| **Grafana** | http://localhost:3001 | admin/steelmill123 | Monitoring dashboards |
| **Jupyter** | http://localhost:8888 | token: steelmill123 | ML development |
| **MLflow** | http://localhost:5000 | - | Experiment tracking |
| **Kafka UI** | http://localhost:8080 | - | Message broker management |
| **MongoDB Express** | http://localhost:8082 | admin/steelmill123 | Database management |
| **Redis Commander** | http://localhost:8083 | admin/steelmill123 | Cache management |
| **RabbitMQ** | http://localhost:15672 | steelmill/steelmill123 | Message broker UI |
| **Prometheus** | http://localhost:9090 | - | Metrics collection |
| **Jaeger** | http://localhost:16686 | - | Distributed tracing |
| **Seq** | http://localhost:5341 | admin/steelmill123 | Structured logging |

### Database Connections

```bash
# MongoDB
mongodb://steelmill:steelmill123@localhost:27017/steelmill

# Redis
redis://:steelmill123@localhost:6379/0

# InfluxDB
http://localhost:8086 (steelmill/steelmill123)

# Cosmos DB Emulator
https://localhost:8081
```

### Service Endpoints

| Service | Port | Swagger UI |
|---------|------|------------|
| Furnace Service | 5001 | http://localhost:5001/swagger |
| Quality Control Service | 5002 | http://localhost:5002/swagger |
| Rolling Mill Service | 5003 | http://localhost:5003/swagger |
| Casting Service | 5004 | http://localhost:5004/swagger |
| ML Inference Service | 8000 | http://localhost:8000/docs |

## 🔧 Development Workflow

### Using VS Code (Recommended)

1. **Open the project**: `code .`

2. **Start infrastructure**: 
   - Open Command Palette (Cmd/Ctrl+Shift+P)
   - Run task: "Start Local Infrastructure"

3. **Launch services**:
   - Use F5 to launch "All Services" compound configuration
   - Or launch individual services from the Run and Debug panel

4. **Development**:
   - Edit code with full IntelliSense support
   - Automatic formatting and linting
   - Integrated debugging
   - Built-in terminal access

### Using Command Line

```bash
# Development workflow commands
./scripts/dev-workflow.sh start    # Start everything
./scripts/dev-workflow.sh stop     # Stop everything  
./scripts/dev-workflow.sh test     # Run all tests
./scripts/dev-workflow.sh build    # Build all services
./scripts/dev-workflow.sh clean    # Clean artifacts
./scripts/dev-workflow.sh format   # Format code
./scripts/dev-workflow.sh logs     # View logs

# Health check
./scripts/health-check.sh

# Individual service commands
cd services/furnace-service
dotnet run                          # Start service
dotnet test                         # Run tests
dotnet watch run                    # Hot reload development

# ML Pipeline
cd ml-pipeline
python local_development_server.py  # Start ML API
jupyter lab                         # Start Jupyter
mlflow ui                          # Start MLflow UI
```

## 🧪 Testing

### Running Tests

```bash
# Run all tests
./scripts/run-tests.sh

# Run specific test suites
dotnet test                                    # .NET tests
python -m pytest ml-pipeline/tests/ -v       # Python tests

# Run with coverage
dotnet test /p:CollectCoverage=true
pytest --cov=ml-pipeline --cov-report=html

# Load testing
k6 run tests/load/api_load_test.js
```

### Test Data

The environment automatically generates synthetic data for testing:

- **Furnace telemetry**: Every 5 seconds
- **Quality samples**: Every 30 seconds  
- **Maintenance alerts**: Every 60 seconds

Access synthetic data via the ML API:
```bash
curl http://localhost:8000/data/synthetic
```

## 🤖 ML Pipeline Development

### Model Development Workflow

1. **Open Jupyter**: http://localhost:8888
   - Token: `steelmill123`
   - Navigate to `/work` directory

2. **Experiment with models**:
   ```python
   from models.rebar_quality_prediction import RebarQualityPredictionModel
   
   # Load model
   model = RebarQualityPredictionModel()
   
   # Generate training data
   data = model.generate_synthetic_training_data(10000)
   
   # Train model
   results = model.train(data, target_columns)
   ```

3. **Track experiments**: http://localhost:5000
   - All model training is automatically logged to MLflow

4. **Test inference**:
   ```bash
   curl -X POST http://localhost:8000/predict/quality \
     -H "Content-Type: application/json" \
     -d '{"grade": "Grade60", "carbon_content": 0.25}'
   ```

### Available ML Models

- **Rebar Quality Prediction**: ASTM compliance and mechanical properties
- **Rebar Defect Detection**: 15 defect types with severity assessment
- **Rebar Predictive Maintenance**: Equipment-specific failure prediction
- **Rebar Process Optimization**: Multi-objective parameter optimization

## 📊 Monitoring and Observability

### Grafana Dashboards

Access at http://localhost:3001 (admin/steelmill123):

- **Production Overview**: Real-time production metrics
- **Quality Control**: Defect rates, compliance scores
- **Equipment Health**: Predictive maintenance alerts
- **Energy Management**: Consumption and optimization
- **ML Model Performance**: Accuracy, drift detection

### Logging

- **Structured Logs**: View at http://localhost:5341
- **Application Logs**: `docker-compose logs -f [service-name]`
- **ML Pipeline Logs**: `tail -f ml-pipeline/logs/ml-pipeline.log`

### Tracing

- **Distributed Tracing**: http://localhost:16686
- Automatic tracing across all microservices
- ML inference request tracing

## 🔍 Troubleshooting

### Common Issues

#### Docker Issues
```bash
# Docker Desktop not running
# Solution: Start Docker Desktop and wait for it to be ready

# Port conflicts
# Solution: Stop conflicting services or change ports in docker-compose.dev.yml

# Insufficient memory
# Solution: Increase Docker Desktop memory allocation to 8GB+
```

#### Service Connection Issues
```bash
# Check service health
./scripts/health-check.sh

# Check service logs
docker-compose -f docker-compose.dev.yml logs [service-name]

# Restart specific service
docker-compose -f docker-compose.dev.yml restart [service-name]
```

#### ML Pipeline Issues
```bash
# Python environment issues
cd ml-pipeline
rm -rf .venv
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements-local.txt

# Model loading issues
rm -rf models/saved/*  # Clear cached models
python local_development_server.py  # Retrain models
```

#### Database Connection Issues
```bash
# Reset databases
./scripts/stop-local-dev.sh --volumes  # Warning: deletes all data
./scripts/start-local-dev.sh

# Check database connectivity
docker exec -it steel-mill-mongodb mongo --eval "db.adminCommand('ping')"
docker exec -it steel-mill-redis redis-cli ping
```

### Performance Optimization

```bash
# Monitor resource usage
docker stats

# Clean up Docker resources
docker system prune -f
docker volume prune -f

# Optimize .NET builds
dotnet clean
dotnet restore --no-cache
dotnet build --no-restore
```

## 📖 API Documentation

### Interactive Documentation

- **ML API**: http://localhost:8000/docs (Swagger UI)
- **Service APIs**: http://localhost:{port}/swagger

### Sample API Calls

```bash
# Quality prediction
curl -X POST http://localhost:8000/predict/quality \
  -H "Content-Type: application/json" \
  -d '{
    "grade": "Grade60",
    "carbon_content": 0.25,
    "billet_temperature": 1050,
    "rolling_speed": 8.0
  }'

# Defect detection  
curl -X POST http://localhost:8000/predict/defects \
  -H "Content-Type: application/json" \
  -d '{
    "surface_roughness": 2.5,
    "rib_height": 0.7,
    "diameter_variance": 0.1
  }'

# Process optimization
curl -X POST http://localhost:8000/optimize/process \
  -H "Content-Type: application/json" \
  -d '{
    "grade": "Grade60",
    "target_quality": 95,
    "energy_budget": 500,
    "production_rate": 100
  }'
```

## 🔐 Security Notes

### Local Development Security

⚠️ **Warning**: This local development setup is configured for ease of use, not security:

- Default passwords are used for all services
- SSL/TLS is disabled
- Authentication is disabled for most services
- All services run with elevated privileges

**Never use these configurations in production!**

### Secure Development Practices

```bash
# Use environment variables for secrets (already configured)
source .env

# Rotate default passwords regularly
# Update docker-compose.dev.yml with new passwords

# Use HTTPS for external connections
# Configure SSL certificates in nginx or service configurations
```

## 📈 Performance Considerations

### Resource Requirements by Component

| Component | CPU | Memory | Storage |
|-----------|-----|---------|---------|
| Infrastructure Services | 2 cores | 4GB | 5GB |
| .NET Microservices | 1 core | 2GB | 1GB |
| ML Pipeline | 2 cores | 4GB | 2GB |
| Monitoring Stack | 1 core | 2GB | 3GB |
| **Total Recommended** | **6+ cores** | **12+ GB** | **11+ GB** |

### Optimization Tips

```bash
# Reduce resource usage for development
# Edit docker-compose.dev.yml and add:
deploy:
  resources:
    limits:
      cpus: '0.5'
      memory: 512M

# Use fewer replicas for load testing
# Disable unnecessary services during development
# Use --scale flag to limit service instances
```

## 🚀 Next Steps

### Ready for Cloud Deployment?

When you're ready to move beyond local development:

1. **Review Azure Integration Guide**
2. **Set up CI/CD Pipeline**
3. **Configure Production Secrets**
4. **Set up Monitoring and Alerting**
5. **Plan Blue-Green Deployment Strategy**

### Contributing

```bash
# Set up pre-commit hooks (already done by setup script)
git add .
git commit -m "Your changes"  # Automatic formatting and linting

# Run full test suite before pushing
./scripts/run-tests.sh

# Follow conventional commit messages
git commit -m "feat: add new quality prediction model"
```

## 🆘 Getting Help

### Resources

- **API Documentation**: Available at service `/swagger` endpoints
- **Architecture Documentation**: `docs/ARCHITECTURE.md`
- **ML Pipeline Documentation**: `ml-pipeline/README.md`

### Support

For development issues:
1. Check this guide first
2. Run health check: `./scripts/health-check.sh`
3. Check logs: `docker-compose logs -f`
4. Review troubleshooting section above

---

**Happy Developing! 🏭⚙️🤖**

The Steel Mill AI Platform local development environment provides everything you need to build, test, and iterate on a world-class industrial IoT and ML platform.