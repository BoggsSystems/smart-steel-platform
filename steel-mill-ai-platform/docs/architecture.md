# Steel Mill AI Platform Architecture

## System Overview

The Steel Mill AI Platform is designed as a cloud-native, microservices-based system that integrates IoT sensor data collection, real-time processing, and machine learning predictions.

## Architecture Components

### 1. Data Ingestion Layer

**IoT Hub**
- Manages device connectivity and authentication
- Supports millions of simultaneous device connections
- Provides device-to-cloud and cloud-to-device messaging

**Event Hub**
- High-throughput data streaming service
- Processes millions of events per second
- Partitioned for parallel processing

### 2. Microservices Layer

Each microservice is built with:
- .NET 9 minimal APIs
- Event-driven architecture
- Cosmos DB for state management
- Health checks and observability

**Service Communication**
```
Sensors → IoT Hub → Event Hub → Microservices → Service Bus → ML Pipeline
                                       ↓
                                  Cosmos DB
```

### 3. Data Storage Layer

**Cosmos DB**
- Primary data store for telemetry
- Partitioned by deviceId for scalability
- Containers:
  - SensorData: Raw telemetry
  - DeviceStatus: Current device states
  - MLPredictions: Model outputs

**Azure Storage**
- ML model artifacts
- Training datasets
- Checkpoint storage for Event Hub

### 4. ML Pipeline

**Components**
- Data preprocessing pipeline
- Model training scripts
- Inference REST API
- Model versioning

**Model Architecture**
- Predictive Maintenance: Random Forest Classifier
- Anomaly Detection: Isolation Forest
- Energy Optimization: Gradient Boosting Regressor

### 5. Infrastructure as Code

**Terraform Modules**
- Resource group management
- Networking configuration
- Service provisioning
- Security setup

## Data Flow

1. **Sensor Data Collection**
   ```
   Physical Sensors → IoT Devices → IoT Hub
   ```

2. **Real-time Processing**
   ```
   IoT Hub → Event Hub → Microservices → Cosmos DB
   ```

3. **ML Predictions**
   ```
   Event Hub → ML Service → Predictions → Event Hub → Microservices
   ```

4. **Monitoring & Alerts**
   ```
   Application Insights ← All Services → Alert Rules → Action Groups
   ```

## Scalability Patterns

### Horizontal Scaling
- Microservices scale independently
- Event Hub partitions enable parallel processing
- Cosmos DB auto-scales based on RU consumption

### Vertical Scaling
- Container instances can be resized
- ML compute can be scaled up for training

## Security Architecture

### Network Security
- Virtual Network isolation
- Network Security Groups
- Private endpoints for data stores

### Identity & Access
- Managed Identities for services
- Azure AD integration
- Key Vault for secrets

### Data Security
- Encryption at rest (Cosmos DB, Storage)
- TLS for data in transit
- Data retention policies

## Reliability Patterns

### High Availability
- Multi-region Cosmos DB replication
- Event Hub geo-disaster recovery
- Service redundancy

### Fault Tolerance
- Circuit breaker pattern in services
- Retry policies with exponential backoff
- Dead letter queues for failed messages

### Monitoring
- Application Insights for APM
- Log Analytics for centralized logging
- Custom metrics and dashboards

## Performance Optimization

### Caching
- Redis Cache for frequently accessed data
- In-memory caching in services

### Data Partitioning
- Cosmos DB partitioned by deviceId
- Event Hub partitioned for throughput

### Async Processing
- Non-blocking I/O in services
- Message-based communication

## ML Pipeline Architecture

### Training Pipeline
```
Historical Data → Preprocessing → Feature Engineering → Model Training → Validation → Model Registry
```

### Inference Pipeline
```
Real-time Data → Feature Extraction → Model Inference → Post-processing → Result Publication
```

### Model Lifecycle
1. Development: Jupyter notebooks
2. Training: Automated scripts
3. Validation: Cross-validation, metrics
4. Deployment: Containerized service
5. Monitoring: Performance tracking

## Deployment Architecture

### Development Environment
- Docker Compose for local services
- Emulators for Azure services
- Local ML model development

### Staging Environment
- Kubernetes cluster
- Scaled-down Azure services
- Integration testing

### Production Environment
- Azure Kubernetes Service (AKS)
- Full-scale Azure services
- Blue-green deployments

## Future Enhancements

### Phase 2
- Deep learning models
- Real-time video analytics
- Advanced optimization algorithms

### Phase 3
- Edge computing integration
- Federated learning
- Autonomous control systems