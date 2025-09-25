#!/usr/bin/env python3
import sys
import os
import time
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Dict, Any, List
import uvicorn
import joblib
from models.predictive_maintenance import PredictiveMaintenanceModel
from models.anomaly_detection import AnomalyDetectionModel
from models.energy_optimization import EnergyOptimizationModel
from utils.model_versioning import ModelVersionManager
from utils.performance_monitor import get_monitor

app = FastAPI(title="Steel Mill ML Inference API", version="1.0.0")

models = {
    'predictive_maintenance': None,
    'anomaly_detection': None,
    'energy_optimization': None
}

# Initialize version manager and performance monitors
version_manager = ModelVersionManager()
monitors = {
    'predictive_maintenance': get_monitor('predictive_maintenance'),
    'anomaly_detection': get_monitor('anomaly_detection'),
    'energy_optimization': get_monitor('energy_optimization')
}

class SensorData(BaseModel):
    deviceId: str
    timestamp: str
    data: Dict[str, float]

class PredictionResponse(BaseModel):
    model_type: str
    deviceId: str
    prediction: Dict[str, Any]
    timestamp: str

@app.on_event("startup")
async def load_models():
    """Load pre-trained models on startup"""
    model_dir = "../models"
    
    try:
        pm_model = PredictiveMaintenanceModel()
        pm_model.load_model(model_dir)
        models['predictive_maintenance'] = pm_model
        print("Loaded predictive maintenance model")
    except Exception as e:
        print(f"Warning: Could not load predictive maintenance model: {e}")
        models['predictive_maintenance'] = PredictiveMaintenanceModel()
    
    try:
        ad_model = AnomalyDetectionModel()
        ad_model.load_model(model_dir)
        models['anomaly_detection'] = ad_model
        print("Loaded anomaly detection model")
    except Exception as e:
        print(f"Warning: Could not load anomaly detection model: {e}")
        models['anomaly_detection'] = AnomalyDetectionModel()
    
    try:
        eo_model = EnergyOptimizationModel()
        eo_model.load_model(model_dir)
        models['energy_optimization'] = eo_model
        print("Loaded energy optimization model")
    except Exception as e:
        print(f"Warning: Could not load energy optimization model: {e}")
        models['energy_optimization'] = EnergyOptimizationModel()

@app.get("/")
async def root():
    return {
        "message": "Steel Mill ML Inference Service",
        "endpoints": {
            "predictive_maintenance": "/predict/maintenance",
            "anomaly_detection": "/predict/anomaly",
            "energy_optimization": "/predict/energy",
            "batch_predictions": "/predict/batch"
        }
    }

@app.post("/predict/maintenance", response_model=PredictionResponse)
async def predict_maintenance(sensor_data: SensorData):
    """Predict equipment maintenance requirements"""
    start_time = time.time()
    model = models['predictive_maintenance']
    monitor = monitors['predictive_maintenance']
    
    if not model or not model.is_trained:
        dummy_prediction = {
            "prediction": "healthy",
            "confidence": 0.95,
            "risk_score": 0.05,
            "message": "Using dummy model - not trained"
        }
        
        # Log prediction for monitoring
        inference_time = time.time() - start_time
        monitor.log_prediction(
            prediction=dummy_prediction,
            features=sensor_data.data,
            inference_time=inference_time
        )
        
        return PredictionResponse(
            model_type="predictive_maintenance",
            deviceId=sensor_data.deviceId,
            prediction=dummy_prediction,
            timestamp=sensor_data.timestamp
        )
    
    try:
        prediction = model.predict_single(sensor_data.data)
        inference_time = time.time() - start_time
        
        # Log prediction for monitoring
        monitor.log_prediction(
            prediction=prediction,
            features=sensor_data.data,
            inference_time=inference_time
        )
        
        return PredictionResponse(
            model_type="predictive_maintenance",
            deviceId=sensor_data.deviceId,
            prediction=prediction,
            timestamp=sensor_data.timestamp
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/predict/anomaly", response_model=PredictionResponse)
async def predict_anomaly(sensor_data: SensorData):
    """Detect anomalies in sensor data"""
    model = models['anomaly_detection']
    
    if not model or not model.is_trained:
        return PredictionResponse(
            model_type="anomaly_detection",
            deviceId=sensor_data.deviceId,
            prediction={
                "is_anomaly": False,
                "anomaly_score": 0.1,
                "feature_contributions": {},
                "message": "Using dummy model - not trained"
            },
            timestamp=sensor_data.timestamp
        )
    
    try:
        prediction = model.detect_single(sensor_data.data)
        return PredictionResponse(
            model_type="anomaly_detection",
            deviceId=sensor_data.deviceId,
            prediction=prediction,
            timestamp=sensor_data.timestamp
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/predict/energy", response_model=PredictionResponse)
async def predict_energy(sensor_data: SensorData):
    """Predict energy consumption and provide optimization recommendations"""
    model = models['energy_optimization']
    
    if not model or not model.is_trained:
        return PredictionResponse(
            model_type="energy_optimization",
            deviceId=sensor_data.deviceId,
            prediction={
                "predicted_energy_peak": 2800.0,
                "current_consumption": sensor_data.data.get('powerDraw', 2500.0),
                "recommended_actions": ["Monitor energy consumption patterns"],
                "potential_savings": 150.0,
                "peak_probability": 0.3,
                "message": "Using dummy model - not trained"
            },
            timestamp=sensor_data.timestamp
        )
    
    try:
        sensor_data.data['timestamp'] = sensor_data.timestamp
        prediction = model.predict_single(sensor_data.data)
        return PredictionResponse(
            model_type="energy_optimization",
            deviceId=sensor_data.deviceId,
            prediction=prediction,
            timestamp=sensor_data.timestamp
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/predict/batch")
async def batch_predictions(sensor_data_list: List[SensorData]):
    """Get predictions from all models for multiple sensor readings"""
    results = []
    
    for sensor_data in sensor_data_list:
        device_predictions = {
            "deviceId": sensor_data.deviceId,
            "timestamp": sensor_data.timestamp,
            "predictions": {}
        }
        
        try:
            maintenance_result = await predict_maintenance(sensor_data)
            device_predictions["predictions"]["maintenance"] = maintenance_result.prediction
        except:
            device_predictions["predictions"]["maintenance"] = {"error": "Failed to get prediction"}
        
        try:
            anomaly_result = await predict_anomaly(sensor_data)
            device_predictions["predictions"]["anomaly"] = anomaly_result.prediction
        except:
            device_predictions["predictions"]["anomaly"] = {"error": "Failed to get prediction"}
        
        try:
            energy_result = await predict_energy(sensor_data)
            device_predictions["predictions"]["energy"] = energy_result.prediction
        except:
            device_predictions["predictions"]["energy"] = {"error": "Failed to get prediction"}
        
        results.append(device_predictions)
    
    return {"batch_results": results}

@app.get("/health")
async def health_check():
    """Check service health and model status"""
    return {
        "status": "healthy",
        "models": {
            "predictive_maintenance": {
                "loaded": models['predictive_maintenance'] is not None,
                "trained": models['predictive_maintenance'].is_trained if models['predictive_maintenance'] else False
            },
            "anomaly_detection": {
                "loaded": models['anomaly_detection'] is not None,
                "trained": models['anomaly_detection'].is_trained if models['anomaly_detection'] else False
            },
            "energy_optimization": {
                "loaded": models['energy_optimization'] is not None,
                "trained": models['energy_optimization'].is_trained if models['energy_optimization'] else False
            }
        }
    }

@app.get("/monitoring/{model_name}")
async def get_model_monitoring(model_name: str):
    """Get performance monitoring data for a specific model"""
    if model_name not in monitors:
        raise HTTPException(status_code=404, detail="Model not found")
    
    return monitors[model_name].get_performance_summary()

@app.get("/models/{model_name}/versions")
async def get_model_versions(model_name: str):
    """Get all versions of a specific model"""
    try:
        versions = version_manager.get_model_versions(model_name)
        return {
            "model_name": model_name,
            "versions": versions,
            "current_version": version_manager.get_current_version(model_name)
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/models/{model_name}/promote/{version_id}")
async def promote_model_version(model_name: str, version_id: str):
    """Promote a model version to production"""
    try:
        version_manager.promote_version(model_name, version_id)
        
        # Reload the model
        await load_models()
        
        return {"message": f"Successfully promoted {version_id} to production for {model_name}"}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/drift-detection/{model_name}")
async def check_data_drift(model_name: str):
    """Check for data drift in model features"""
    if model_name not in monitors:
        raise HTTPException(status_code=404, detail="Model not found")
    
    monitor = monitors[model_name]
    drift_results = {}
    
    for feature_name in monitor.feature_distributions.keys():
        drift_results[feature_name] = monitor.detect_data_drift(feature_name)
    
    return {
        "model_name": model_name,
        "drift_results": drift_results,
        "timestamp": time.time()
    }

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)