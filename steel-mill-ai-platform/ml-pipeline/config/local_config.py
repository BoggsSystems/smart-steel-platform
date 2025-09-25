"""
Local development configuration for Steel Mill AI ML Pipeline
"""

import os
from typing import Dict, Any
from dataclasses import dataclass
from enum import Enum

class Environment(Enum):
    LOCAL = "local"
    DEVELOPMENT = "development"
    STAGING = "staging"
    PRODUCTION = "production"

@dataclass
class DatabaseConfig:
    """Database configuration for local development"""
    
    # MongoDB
    mongodb_url: str = "mongodb://steelmill:steelmill123@localhost:27017/steelmill"
    mongodb_database: str = "steelmill"
    
    # Redis
    redis_url: str = "redis://:steelmill123@localhost:6379/0"
    redis_host: str = "localhost"
    redis_port: int = 6379
    redis_password: str = "steelmill123"
    
    # InfluxDB
    influxdb_url: str = "http://localhost:8086"
    influxdb_token: str = "steelmill-super-secret-token"
    influxdb_org: str = "steelmill-org"
    influxdb_bucket: str = "sensor-data"
    
    # Cosmos DB Emulator
    cosmosdb_endpoint: str = "https://localhost:8081"
    cosmosdb_key: str = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
    cosmosdb_database: str = "steelmill"

@dataclass
class MessageBrokerConfig:
    """Message broker configuration for local development"""
    
    # Kafka
    kafka_bootstrap_servers: str = "localhost:9092"
    kafka_topics: Dict[str, str] = None
    
    # RabbitMQ
    rabbitmq_url: str = "amqp://steelmill:steelmill123@localhost:5672/steelmill"
    rabbitmq_host: str = "localhost"
    rabbitmq_port: int = 5672
    rabbitmq_username: str = "steelmill"
    rabbitmq_password: str = "steelmill123"
    rabbitmq_vhost: str = "steelmill"
    
    def __post_init__(self):
        if self.kafka_topics is None:
            self.kafka_topics = {
                "furnace_telemetry": "furnace-telemetry",
                "quality_events": "quality-events",
                "maintenance_alerts": "maintenance-alerts",
                "production_events": "production-events",
                "ml_predictions": "ml-predictions",
                "equipment_status": "equipment-status"
            }

@dataclass
class MLConfig:
    """Machine Learning configuration for local development"""
    
    # MLflow
    mlflow_tracking_uri: str = "http://localhost:5000"
    mlflow_experiment_name: str = "steel-mill-local"
    
    # Model storage
    model_storage_path: str = "./models/saved"
    model_artifacts_path: str = "./models/artifacts"
    
    # Data paths
    training_data_path: str = "./data/training"
    validation_data_path: str = "./data/validation"
    test_data_path: str = "./data/test"
    
    # Model serving
    inference_host: str = "localhost"
    inference_port: int = 8000
    
    # Training parameters
    default_random_state: int = 42
    default_test_size: float = 0.2
    default_validation_size: float = 0.2
    
    # Model update intervals (in seconds for local development)
    model_update_interval: int = 300  # 5 minutes
    data_refresh_interval: int = 60   # 1 minute
    
    # Feature store configuration
    feature_store_path: str = "./data/feature_store"
    feature_cache_ttl: int = 3600  # 1 hour

@dataclass
class MonitoringConfig:
    """Monitoring configuration for local development"""
    
    # Prometheus
    prometheus_host: str = "localhost"
    prometheus_port: int = 9090
    metrics_port: int = 8001
    
    # Grafana
    grafana_host: str = "localhost"
    grafana_port: int = 3001
    grafana_username: str = "admin"
    grafana_password: str = "steelmill123"
    
    # Jaeger
    jaeger_host: str = "localhost"
    jaeger_port: int = 16686
    
    # Logging
    log_level: str = "INFO"
    log_format: str = "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    log_file: str = "./logs/ml-pipeline.log"

@dataclass
class LocalConfig:
    """Complete local development configuration"""
    
    environment: Environment = Environment.LOCAL
    debug: bool = True
    
    # Component configurations
    database: DatabaseConfig = DatabaseConfig()
    message_broker: MessageBrokerConfig = MessageBrokerConfig()
    ml: MLConfig = MLConfig()
    monitoring: MonitoringConfig = MonitoringConfig()
    
    # Security (disabled for local development)
    enable_authentication: bool = False
    enable_ssl: bool = False
    
    # Performance
    worker_processes: int = 1
    max_concurrent_requests: int = 100
    request_timeout: int = 30
    
    # Data generation for local development
    enable_synthetic_data: bool = True
    synthetic_data_interval: int = 10  # seconds
    synthetic_data_batch_size: int = 100
    
    @classmethod
    def from_env(cls) -> 'LocalConfig':
        """Create configuration from environment variables"""
        config = cls()
        
        # Override with environment variables if present
        if os.getenv('MONGODB_URL'):
            config.database.mongodb_url = os.getenv('MONGODB_URL')
        
        if os.getenv('REDIS_URL'):
            config.database.redis_url = os.getenv('REDIS_URL')
            
        if os.getenv('KAFKA_BOOTSTRAP_SERVERS'):
            config.message_broker.kafka_bootstrap_servers = os.getenv('KAFKA_BOOTSTRAP_SERVERS')
            
        if os.getenv('MLFLOW_TRACKING_URI'):
            config.ml.mlflow_tracking_uri = os.getenv('MLFLOW_TRACKING_URI')
            
        if os.getenv('LOG_LEVEL'):
            config.monitoring.log_level = os.getenv('LOG_LEVEL')
            
        if os.getenv('DEBUG'):
            config.debug = os.getenv('DEBUG').lower() == 'true'
            
        return config
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert configuration to dictionary"""
        return {
            'environment': self.environment.value,
            'debug': self.debug,
            'database': {
                'mongodb_url': self.database.mongodb_url,
                'redis_url': self.database.redis_url,
                'influxdb_url': self.database.influxdb_url,
                'cosmosdb_endpoint': self.database.cosmosdb_endpoint
            },
            'message_broker': {
                'kafka_bootstrap_servers': self.message_broker.kafka_bootstrap_servers,
                'rabbitmq_url': self.message_broker.rabbitmq_url
            },
            'ml': {
                'mlflow_tracking_uri': self.ml.mlflow_tracking_uri,
                'model_storage_path': self.ml.model_storage_path,
                'inference_port': self.ml.inference_port
            },
            'monitoring': {
                'prometheus_host': self.monitoring.prometheus_host,
                'log_level': self.monitoring.log_level
            }
        }

# Global configuration instance
config = LocalConfig.from_env()

# Service URLs for local development
SERVICE_URLS = {
    'furnace-service': 'http://localhost:5001',
    'quality-control-service': 'http://localhost:5002',
    'rolling-mill-service': 'http://localhost:5003',
    'casting-service': 'http://localhost:5004',
    'ladle-metallurgy-service': 'http://localhost:5005',
    'heat-treatment-service': 'http://localhost:5006',
    'cutting-straightening-service': 'http://localhost:5007',
    'bundling-service': 'http://localhost:5008',
    'energy-management-service': 'http://localhost:5009',
    'maintenance-service': 'http://localhost:5010',
    'inventory-service': 'http://localhost:5011'
}

# Kafka topic configuration
KAFKA_TOPICS = {
    'FURNACE_TELEMETRY': 'furnace-telemetry',
    'QUALITY_EVENTS': 'quality-events',
    'MAINTENANCE_ALERTS': 'maintenance-alerts',
    'PRODUCTION_EVENTS': 'production-events',
    'ML_PREDICTIONS': 'ml-predictions',
    'EQUIPMENT_STATUS': 'equipment-status',
    'ENERGY_METRICS': 'energy-metrics',
    'INVENTORY_UPDATES': 'inventory-updates'
}

# Database collections
MONGODB_COLLECTIONS = {
    'furnace_data': 'furnace_data',
    'quality_tests': 'quality_tests',
    'maintenance_records': 'maintenance_records',
    'production_batches': 'production_batches',
    'ml_models': 'ml_models',
    'predictions': 'predictions',
    'feature_store': 'feature_store'
}

# Model registry
MODEL_REGISTRY = {
    'quality_prediction': 'rebar_quality_prediction',
    'defect_detection': 'rebar_defect_detection',
    'predictive_maintenance': 'rebar_predictive_maintenance',
    'process_optimization': 'rebar_process_optimization',
    'anomaly_detection': 'anomaly_detection',
    'energy_optimization': 'energy_optimization',
    'demand_forecasting': 'demand_forecasting'
}

# Feature groups for feature store
FEATURE_GROUPS = {
    'furnace_features': [
        'temperature', 'power_consumption', 'efficiency', 'electrode_position',
        'arc_voltage', 'tap_time', 'oxygen_flow', 'carbon_injection'
    ],
    'quality_features': [
        'carbon_content', 'manganese_content', 'phosphorus_content', 
        'sulfur_content', 'yield_strength', 'tensile_strength', 'elongation'
    ],
    'production_features': [
        'batch_size', 'processing_time', 'throughput', 'yield_rate',
        'energy_per_ton', 'material_utilization', 'equipment_efficiency'
    ],
    'maintenance_features': [
        'equipment_age', 'operating_hours', 'failure_count', 'maintenance_cost',
        'vibration_level', 'temperature_deviation', 'wear_indicator'
    ]
}

def get_config() -> LocalConfig:
    """Get the global configuration instance"""
    return config

def get_database_url(db_type: str) -> str:
    """Get database URL by type"""
    urls = {
        'mongodb': config.database.mongodb_url,
        'redis': config.database.redis_url,
        'influxdb': config.database.influxdb_url,
        'cosmosdb': config.database.cosmosdb_endpoint
    }
    return urls.get(db_type, '')

def get_service_url(service_name: str) -> str:
    """Get service URL by name"""
    return SERVICE_URLS.get(service_name, f'http://localhost:5000')

def is_local_development() -> bool:
    """Check if running in local development mode"""
    return config.environment == Environment.LOCAL