"""
Local Development Server for Steel Mill AI ML Pipeline
This server provides a complete local development environment with all ML models and synthetic data generation
"""

import asyncio
import logging
import sys
import uvicorn
from fastapi import FastAPI, HTTPException, BackgroundTasks, Depends
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse
from typing import Dict, Any, List, Optional
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import json
import os
from pathlib import Path

# Add the ml-pipeline directory to Python path
sys.path.append(str(Path(__file__).parent))

# Import our models and configuration
from config.local_config import get_config, MODEL_REGISTRY, KAFKA_TOPICS
from models.rebar_quality_prediction import RebarQualityPredictionModel
from models.rebar_defect_detection import RebarDefectDetectionModel
from models.rebar_predictive_maintenance import RebarPredictiveMaintenanceModel
from models.rebar_process_optimization import RebarProcessOptimizationModel

# Set up logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Initialize FastAPI app
app = FastAPI(
    title="Steel Mill AI - Local Development Server",
    description="Complete ML pipeline for local development and testing",
    version="1.0.0",
    docs_url="/docs",
    redoc_url="/redoc"
)

# Configure CORS for local development
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, specify exact origins
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Global variables for models and data
models = {}
synthetic_data_task = None
data_cache = {}
config = get_config()

class ModelManager:
    """Manages ML models for local development"""
    
    def __init__(self):
        self.models = {}
        self.model_metadata = {}
        
    def load_models(self):
        """Load all ML models"""
        try:
            logger.info("Loading ML models...")
            
            # Load quality prediction model
            self.models['quality_prediction'] = RebarQualityPredictionModel()
            try:
                self.models['quality_prediction'].load_model(config.ml.model_storage_path)
                logger.info("Loaded saved quality prediction model")
            except:
                logger.info("Training new quality prediction model...")
                data = self.models['quality_prediction'].generate_synthetic_training_data(5000)
                targets = {
                    'compliance': 'astm_compliant',
                    'yield_strength': 'actual_yield_strength',
                    'tensile_strength': 'actual_tensile_strength',
                    'elongation': 'actual_elongation'
                }
                self.models['quality_prediction'].train(data, targets)
                self.models['quality_prediction'].save_model(config.ml.model_storage_path)
                logger.info("Quality prediction model trained and saved")
            
            # Load defect detection model
            self.models['defect_detection'] = RebarDefectDetectionModel()
            try:
                self.models['defect_detection'].load_model(config.ml.model_storage_path)
                logger.info("Loaded saved defect detection model")
            except:
                logger.info("Training new defect detection model...")
                data = self.models['defect_detection'].generate_synthetic_defect_data(5000)
                self.models['defect_detection'].train(data, 'defect_type')
                self.models['defect_detection'].save_model(config.ml.model_storage_path)
                logger.info("Defect detection model trained and saved")
            
            # Load predictive maintenance model
            self.models['predictive_maintenance'] = RebarPredictiveMaintenanceModel()
            try:
                self.models['predictive_maintenance'].load_model(config.ml.model_storage_path)
                logger.info("Loaded saved predictive maintenance model")
            except:
                logger.info("Training new predictive maintenance model...")
                # Generate synthetic data for training
                synthetic_data = self._generate_maintenance_training_data(3000)
                X = synthetic_data[['vibration', 'vibrationX', 'vibrationY', 'vibrationZ', 
                                  'motorTorque', 'motorCurrent', 'temperature']].values
                y = self.models['predictive_maintenance'].generate_synthetic_labels(X)
                self.models['predictive_maintenance'].train(X, y)
                self.models['predictive_maintenance'].save_model(config.ml.model_storage_path)
                logger.info("Predictive maintenance model trained and saved")
            
            # Load process optimization model
            self.models['process_optimization'] = RebarProcessOptimizationModel()
            try:
                self.models['process_optimization'].load_model(config.ml.model_storage_path)
                logger.info("Loaded saved process optimization model")
            except:
                logger.info("Training new process optimization model...")
                data = self.models['process_optimization'].generate_synthetic_training_data(3000)
                targets = {
                    'rolling': ['billet_temperature', 'rolling_speed', 'total_reduction', 
                               'pass_schedule_factor', 'roll_gap_sequence', 'ribbing_pressure',
                               'finishing_temperature', 'rolling_force_distribution'],
                    'heat_treatment': ['austenitizing_temp', 'quench_rate', 'tempering_temp',
                                     'cooling_rate', 'atmosphere_composition', 'holding_time'],
                    'cutting': ['processing_speed', 'straightening_force', 'cutting_speed',
                               'length_compensation', 'quality_check_interval']
                }
                self.models['process_optimization'].train(data, targets)
                self.models['process_optimization'].save_model(config.ml.model_storage_path)
                logger.info("Process optimization model trained and saved")
                
            logger.info(f"Successfully loaded {len(self.models)} ML models")
            
        except Exception as e:
            logger.error(f"Error loading models: {e}")
            
    def _generate_maintenance_training_data(self, n_samples: int) -> pd.DataFrame:
        """Generate synthetic maintenance training data"""
        np.random.seed(42)
        
        data = {
            'vibration': np.random.lognormal(2, 0.5, n_samples),
            'vibrationX': np.random.lognormal(1.8, 0.4, n_samples),
            'vibrationY': np.random.lognormal(1.9, 0.4, n_samples),
            'vibrationZ': np.random.lognormal(1.7, 0.3, n_samples),
            'motorTorque': np.random.normal(150, 30, n_samples),
            'motorCurrent': np.random.normal(25, 8, n_samples),
            'temperature': np.random.normal(75, 15, n_samples)
        }
        
        return pd.DataFrame(data)

# Initialize model manager
model_manager = ModelManager()

class DataGenerator:
    """Generates synthetic data for local development"""
    
    def __init__(self):
        self.furnace_data = []
        self.quality_data = []
        self.maintenance_data = []
        self.production_data = []
        
    def generate_furnace_telemetry(self) -> Dict[str, Any]:
        """Generate synthetic furnace telemetry data"""
        base_temp = 1500 + np.random.normal(0, 100)
        base_power = 80 + np.random.normal(0, 10)
        
        return {
            'furnaceId': f'EAF-{np.random.randint(1, 4):03d}',
            'timestamp': datetime.now().isoformat(),
            'temperature': max(1000, min(2000, base_temp)),
            'powerConsumption': max(0, base_power),
            'efficiency': max(60, min(100, 85 + np.random.normal(0, 8))),
            'electrodePosition': 150 + np.random.normal(0, 20),
            'arcVoltage': 480 + np.random.normal(0, 30),
            'oxygenFlow': 1000 + np.random.normal(0, 100),
            'status': np.random.choice(['operational', 'maintenance', 'startup'], p=[0.8, 0.15, 0.05])
        }
    
    def generate_quality_sample(self) -> Dict[str, Any]:
        """Generate synthetic quality test data"""
        grade = np.random.choice(['Grade40', 'Grade60', 'Grade75', 'Grade80'], p=[0.1, 0.6, 0.2, 0.1])
        
        grade_specs = {
            'Grade40': {'yield': 275, 'tensile': 420, 'elongation': 14},
            'Grade60': {'yield': 420, 'tensile': 620, 'elongation': 11},
            'Grade75': {'yield': 520, 'tensile': 690, 'elongation': 8.5},
            'Grade80': {'yield': 550, 'tensile': 720, 'elongation': 7.5}
        }
        
        spec = grade_specs[grade]
        
        return {
            'sampleId': f'QC-{datetime.now().strftime("%Y%m%d")}-{np.random.randint(1000, 9999)}',
            'timestamp': datetime.now().isoformat(),
            'grade': grade,
            'testType': np.random.choice(['tensile', 'bend', 'dimensional', 'chemical']),
            'yieldStrength': spec['yield'] + np.random.normal(0, spec['yield'] * 0.05),
            'tensileStrength': spec['tensile'] + np.random.normal(0, spec['tensile'] * 0.05),
            'elongation': max(5, spec['elongation'] + np.random.normal(0, 1.5)),
            'carbonContent': np.random.normal(0.25, 0.03),
            'manganeseContent': np.random.normal(1.2, 0.1),
            'passed': np.random.choice([True, False], p=[0.92, 0.08])
        }
    
    def generate_maintenance_alert(self) -> Dict[str, Any]:
        """Generate synthetic maintenance alert"""
        equipment_types = ['EAF', 'ROLLING_MILL', 'CASTING_MACHINE', 'HEAT_TREATMENT']
        alert_types = ['vibration_high', 'temperature_high', 'efficiency_low', 'scheduled_maintenance']
        
        return {
            'equipmentId': f'{np.random.choice(equipment_types)}-{np.random.randint(1, 5):03d}',
            'timestamp': datetime.now().isoformat(),
            'alertType': np.random.choice(alert_types),
            'severity': np.random.choice(['low', 'medium', 'high', 'critical'], p=[0.4, 0.3, 0.2, 0.1]),
            'vibrationLevel': np.random.exponential(3),
            'temperature': 75 + np.random.exponential(15),
            'efficiency': max(60, 95 - np.random.exponential(10)),
            'predictedFailureRisk': np.random.beta(2, 8) * 100
        }
    
    async def generate_continuous_data(self):
        """Continuously generate synthetic data"""
        while True:
            try:
                # Generate furnace telemetry every 5 seconds
                furnace_data = self.generate_furnace_telemetry()
                self.furnace_data.append(furnace_data)
                
                # Generate quality sample every 30 seconds
                if len(self.furnace_data) % 6 == 0:
                    quality_data = self.generate_quality_sample()
                    self.quality_data.append(quality_data)
                
                # Generate maintenance alert every 60 seconds
                if len(self.furnace_data) % 12 == 0:
                    maintenance_data = self.generate_maintenance_alert()
                    self.maintenance_data.append(maintenance_data)
                
                # Keep only recent data (last 1000 records)
                if len(self.furnace_data) > 1000:
                    self.furnace_data = self.furnace_data[-1000:]
                if len(self.quality_data) > 100:
                    self.quality_data = self.quality_data[-100:]
                if len(self.maintenance_data) > 50:
                    self.maintenance_data = self.maintenance_data[-50:]
                    
                # Update data cache
                data_cache.update({
                    'furnace_data': self.furnace_data[-50:],  # Last 50 records
                    'quality_data': self.quality_data[-20:],   # Last 20 records
                    'maintenance_data': self.maintenance_data[-10:]  # Last 10 records
                })
                
                await asyncio.sleep(5)  # Generate data every 5 seconds
                
            except Exception as e:
                logger.error(f"Error generating synthetic data: {e}")
                await asyncio.sleep(5)

# Initialize data generator
data_generator = DataGenerator()

# API Endpoints

@app.get("/", response_class=HTMLResponse)
async def root():
    """Root endpoint with API documentation"""
    html_content = """
    <!DOCTYPE html>
    <html>
    <head>
        <title>Steel Mill AI - Local Development Server</title>
        <style>
            body { font-family: Arial, sans-serif; margin: 40px; }
            .header { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; }
            .section { margin: 20px 0; }
            .endpoint { background-color: #f8f9fa; padding: 10px; margin: 10px 0; border-radius: 5px; }
            .method { color: #27ae60; font-weight: bold; }
            ul { list-style-type: none; padding: 0; }
            li { margin: 5px 0; }
        </style>
    </head>
    <body>
        <div class="header">
            <h1>🏭 Steel Mill AI - Local Development Server</h1>
            <p>Complete ML pipeline for local development and testing</p>
        </div>
        
        <div class="section">
            <h2>🚀 Available Endpoints</h2>
            
            <div class="endpoint">
                <span class="method">GET</span> <strong>/health</strong> - Health check
            </div>
            
            <div class="endpoint">
                <span class="method">GET</span> <strong>/models/status</strong> - Model status and metadata
            </div>
            
            <div class="endpoint">
                <span class="method">POST</span> <strong>/predict/quality</strong> - Quality prediction
            </div>
            
            <div class="endpoint">
                <span class="method">POST</span> <strong>/predict/defects</strong> - Defect detection
            </div>
            
            <div class="endpoint">
                <span class="method">POST</span> <strong>/predict/maintenance</strong> - Predictive maintenance
            </div>
            
            <div class="endpoint">
                <span class="method">POST</span> <strong>/optimize/process</strong> - Process optimization
            </div>
            
            <div class="endpoint">
                <span class="method">GET</span> <strong>/data/synthetic</strong> - Current synthetic data
            </div>
            
            <div class="endpoint">
                <span class="method">GET</span> <strong>/data/stream</strong> - Real-time data stream
            </div>
        </div>
        
        <div class="section">
            <h2>📚 Documentation</h2>
            <ul>
                <li><a href="/docs">📖 Interactive API Documentation (Swagger)</a></li>
                <li><a href="/redoc">📋 Alternative Documentation (ReDoc)</a></li>
            </ul>
        </div>
        
        <div class="section">
            <h2>🔧 Development Tools</h2>
            <ul>
                <li><a href="http://localhost:3001" target="_blank">📊 Grafana Dashboard</a></li>
                <li><a href="http://localhost:8888" target="_blank">📓 Jupyter Notebooks</a></li>
                <li><a href="http://localhost:5000" target="_blank">🧪 MLflow Tracking</a></li>
                <li><a href="http://localhost:8080" target="_blank">📨 Kafka UI</a></li>
            </ul>
        </div>
    </body>
    </html>
    """
    return HTMLResponse(content=html_content)

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "timestamp": datetime.now().isoformat(),
        "models_loaded": len(model_manager.models),
        "environment": config.environment.value,
        "version": "1.0.0"
    }

@app.get("/models/status")
async def get_models_status():
    """Get status of all loaded models"""
    status = {}
    for model_name, model in model_manager.models.items():
        status[model_name] = {
            "loaded": hasattr(model, 'models_trained') and model.models_trained,
            "type": type(model).__name__,
            "last_updated": datetime.now().isoformat()
        }
    return status

@app.post("/predict/quality")
async def predict_quality(data: Dict[str, Any]):
    """Predict rebar quality"""
    try:
        if 'quality_prediction' not in model_manager.models:
            raise HTTPException(status_code=503, detail="Quality prediction model not loaded")
        
        model = model_manager.models['quality_prediction']
        prediction = model.predict_quality(data)
        
        return {
            "status": "success",
            "prediction": prediction,
            "model": "rebar_quality_prediction",
            "timestamp": datetime.now().isoformat()
        }
    except Exception as e:
        logger.error(f"Quality prediction error: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/predict/defects")
async def predict_defects(data: Dict[str, Any]):
    """Detect defects in rebar"""
    try:
        if 'defect_detection' not in model_manager.models:
            raise HTTPException(status_code=503, detail="Defect detection model not loaded")
        
        model = model_manager.models['defect_detection']
        prediction = model.detect_defects(data)
        
        return {
            "status": "success",
            "prediction": prediction,
            "model": "rebar_defect_detection",
            "timestamp": datetime.now().isoformat()
        }
    except Exception as e:
        logger.error(f"Defect detection error: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/predict/maintenance")
async def predict_maintenance(data: Dict[str, Any]):
    """Predict maintenance requirements"""
    try:
        if 'predictive_maintenance' not in model_manager.models:
            raise HTTPException(status_code=503, detail="Predictive maintenance model not loaded")
        
        model = model_manager.models['predictive_maintenance']
        prediction = model.predict_single(data)
        
        return {
            "status": "success",
            "prediction": prediction,
            "model": "rebar_predictive_maintenance",
            "timestamp": datetime.now().isoformat()
        }
    except Exception as e:
        logger.error(f"Maintenance prediction error: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/optimize/process")
async def optimize_process(specifications: Dict[str, Any]):
    """Optimize process parameters"""
    try:
        if 'process_optimization' not in model_manager.models:
            raise HTTPException(status_code=503, detail="Process optimization model not loaded")
        
        model = model_manager.models['process_optimization']
        optimization = model.optimize_process_parameters(specifications)
        
        return {
            "status": "success",
            "optimization": optimization,
            "model": "rebar_process_optimization",
            "timestamp": datetime.now().isoformat()
        }
    except Exception as e:
        logger.error(f"Process optimization error: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/data/synthetic")
async def get_synthetic_data():
    """Get current synthetic data"""
    return {
        "status": "success",
        "data": data_cache,
        "timestamp": datetime.now().isoformat()
    }

@app.get("/data/stream")
async def get_data_stream():
    """Get real-time data stream info"""
    return {
        "status": "active" if synthetic_data_task and not synthetic_data_task.done() else "inactive",
        "data_points": {
            "furnace_data": len(data_generator.furnace_data),
            "quality_data": len(data_generator.quality_data),
            "maintenance_data": len(data_generator.maintenance_data)
        },
        "timestamp": datetime.now().isoformat()
    }

# Startup and shutdown events

@app.on_event("startup")
async def startup_event():
    """Initialize the application"""
    logger.info("Starting Steel Mill AI Local Development Server...")
    
    # Create necessary directories
    os.makedirs(config.ml.model_storage_path, exist_ok=True)
    os.makedirs(config.ml.model_artifacts_path, exist_ok=True)
    os.makedirs("./logs", exist_ok=True)
    
    # Load ML models
    model_manager.load_models()
    
    # Start synthetic data generation
    global synthetic_data_task
    synthetic_data_task = asyncio.create_task(data_generator.generate_continuous_data())
    
    logger.info("Steel Mill AI Local Development Server started successfully!")

@app.on_event("shutdown")
async def shutdown_event():
    """Cleanup on shutdown"""
    logger.info("Shutting down Steel Mill AI Local Development Server...")
    
    if synthetic_data_task and not synthetic_data_task.done():
        synthetic_data_task.cancel()
        try:
            await synthetic_data_task
        except asyncio.CancelledError:
            pass
    
    logger.info("Shutdown complete")

if __name__ == "__main__":
    # Run the development server
    uvicorn.run(
        "local_development_server:app",
        host="0.0.0.0",
        port=config.ml.inference_port,
        reload=config.debug,
        log_level=config.monitoring.log_level.lower(),
        access_log=True
    )