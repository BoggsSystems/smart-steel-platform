# Steel Mill AI Platform

An Azure-based IoT steel mill microservice platform with integrated ML models for predictive maintenance, anomaly detection, and energy optimization.

## 🏗️ Architecture Overview

The platform consists of:
- **Microservices** (.NET 9) for each production stage
- **ML Pipeline** (Python) with three Phase 1 models
- **Azure Services**: IoT Hub, Event Hub, Cosmos DB, Azure ML
- **Test Runner** for sensor simulation and data generation

## 🚀 Quick Start

### Prerequisites
- .NET 9 SDK
- Python 3.11+
- Docker & Docker Compose
- Azure CLI (for cloud deployment)
- Terraform (for infrastructure provisioning)

### Local Development

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd steel-mill-ai-platform
   ```

2. **Start local services**
   ```bash
   docker-compose up -d
   ```

3. **Generate sensor data**
   ```bash
   cd test-runner
   pip install -r requirements.txt
   python simulate_sensors.py --count 1000 --output csv
   ```

4. **Train ML models**
   ```bash
   cd ml-pipeline/training
   python train_predictive_maintenance.py
   python train_anomaly_detection.py
   python train_energy_optimization.py
   ```

5. **Start inference service**
   ```bash
   cd ml-pipeline/inference
   python inference_service.py
   ```

## 📊 ML Models

### Phase 1 Models

1. **Predictive Maintenance**
   - Predicts equipment failure risk
   - Input: vibration, torque, current, temperature
   - Output: binary classification (healthy/at-risk)

2. **Anomaly Detection**
   - Detects unusual patterns in sensor data
   - Input: temperature, vibration, oxygen mix, fuel flow
   - Output: anomaly score (0-1)

3. **Energy Optimization**
   - Predicts energy consumption peaks
   - Input: power draw, time features, load factors
   - Output: predicted energy peak (kWh) + recommendations

## 🔧 Microservices

Each service follows the production flow:
- `scrap-intake-service`: Manages incoming scrap metal
- `furnace-service`: Controls melting operations
- `casting-service`: Handles steel casting
- `rolling-mill-service`: Manages rolling operations
- `packaging-service`: Handles final packaging
- `shipping-service`: Manages shipping logistics

## 📡 Sensor Simulation

The test runner simulates realistic sensor data:

```bash
# Generate CSV data
python simulate_sensors.py --interval 5 --count 1000 --output csv

# Send to Event Hub (requires Azure connection)
export EVENTHUB_CONNECTION_STRING="your-connection-string"
python simulate_sensors.py --interval 1 --count 100 --output eventhub
```

## 🌐 API Endpoints

### ML Inference API

- `GET /` - Service info
- `POST /predict/maintenance` - Predictive maintenance
- `POST /predict/anomaly` - Anomaly detection
- `POST /predict/energy` - Energy optimization
- `POST /predict/batch` - Batch predictions
- `GET /health` - Service health check

Example request:
```json
{
  "deviceId": "rolling-mill-1",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    "vibration": 0.12,
    "motorTorque": 5200,
    "motorCurrent": 95,
    "temperature": 82
  }
}
```

## 🏃 Deployment

### Azure Infrastructure

1. **Configure Terraform variables**
   ```bash
   cd infrastructure/terraform
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with your values
   ```

2. **Deploy infrastructure**
   ```bash
   terraform init
   terraform plan
   terraform apply
   ```

3. **Deploy microservices**
   Use Azure Container Instances or AKS for production deployment.

## 📈 Monitoring

- **Application Insights** for service monitoring
- **CosmosDB metrics** for data throughput
- **Event Hub metrics** for message processing
- **ML model performance** tracking in Azure ML

## 🔐 Security

- All secrets stored in Azure Key Vault
- Service-to-service auth using Managed Identities
- TLS encryption for all communications
- Role-based access control (RBAC)

## 📋 Development Workflow

1. **Simulate sensors** → Generate training data
2. **Train models** → Create ML models
3. **Test locally** → Validate with Docker Compose
4. **Deploy to Azure** → Use Terraform
5. **Monitor & iterate** → Track performance

## 🧪 Testing

```bash
# Run ML tests
cd ml-pipeline
pytest tests/

# Run .NET tests
cd services/furnace-service
dotnet test
```

## 📚 Documentation

- [Architecture Details](docs/architecture.md)
- [ML Roadmap](docs/ml-roadmap.md)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📝 License

This project is licensed under the MIT License.