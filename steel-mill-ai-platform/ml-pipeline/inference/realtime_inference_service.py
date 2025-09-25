#!/usr/bin/env python3
"""
Real-time ML inference service with <100ms response times
Optimized for production deployment with caching, batching, and monitoring
"""
import asyncio
import time
import json
from typing import Dict, List, Any, Optional
from collections import defaultdict, deque
from datetime import datetime, timedelta
import numpy as np
import pandas as pd
from contextlib import asynccontextmanager

import uvicorn
from fastapi import FastAPI, HTTPException, BackgroundTasks, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.middleware.gzip import GZipMiddleware
from pydantic import BaseModel, Field
import redis.asyncio as redis
from prometheus_client import Counter, Histogram, Gauge, generate_latest
from starlette.responses import Response

# Import our ML models
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from models.predictive_maintenance import PredictiveMaintenanceModel
from models.anomaly_detection import AnomalyDetectionModel
from models.energy_optimization import EnergyOptimizationModel
from utils.performance_monitor import get_monitor

# Prometheus metrics
REQUEST_COUNT = Counter('inference_requests_total', 'Total inference requests', ['model_type', 'status'])
REQUEST_DURATION = Histogram('inference_duration_seconds', 'Request duration', ['model_type'])
ACTIVE_REQUESTS = Gauge('inference_active_requests', 'Active requests', ['model_type'])
BATCH_SIZE = Histogram('inference_batch_size', 'Batch size for inference')
CACHE_HITS = Counter('inference_cache_hits_total', 'Cache hits', ['model_type'])
CACHE_MISSES = Counter('inference_cache_misses_total', 'Cache misses', ['model_type'])

class OptimizedInferenceRequest(BaseModel):
    deviceId: str = Field(..., min_length=1, max_length=50)
    timestamp: str
    features: Dict[str, float] = Field(..., min_items=1)
    model_types: List[str] = Field(default=["all"], description="Models to run: predictive_maintenance, anomaly_detection, energy_optimization, or all")
    priority: str = Field(default="normal", description="Priority: high, normal, low")
    cache_ttl: int = Field(default=300, ge=0, le=3600, description="Cache TTL in seconds")

class BatchInferenceRequest(BaseModel):
    requests: List[OptimizedInferenceRequest] = Field(..., max_items=100)
    batch_id: Optional[str] = None

class InferenceResponse(BaseModel):
    deviceId: str
    timestamp: str
    predictions: Dict[str, Any]
    inference_time_ms: float
    cached: bool = False
    model_versions: Dict[str, str] = {}

class RealTimeInferenceService:
    def __init__(self):
        self.models = {
            'predictive_maintenance': None,
            'anomaly_detection': None,
            'energy_optimization': None
        }
        self.model_versions = {}
        self.redis_client = None
        self.request_queue = defaultdict(lambda: deque(maxlen=1000))
        self.batch_processor_running = False
        
        # Performance optimization settings
        self.max_batch_size = 32
        self.batch_timeout_ms = 50  # Max 50ms batching delay
        self.cache_enabled = True
        
        # Circuit breaker for model failures
        self.circuit_breaker = {
            'predictive_maintenance': {'failures': 0, 'last_failure': None, 'open': False},
            'anomaly_detection': {'failures': 0, 'last_failure': None, 'open': False},
            'energy_optimization': {'failures': 0, 'last_failure': None, 'open': False}
        }
    
    async def initialize(self):
        """Initialize models and Redis connection"""
        # Initialize Redis for caching
        try:
            self.redis_client = redis.Redis(
                host=os.getenv('REDIS_HOST', 'redis'),
                port=int(os.getenv('REDIS_PORT', 6379)),
                decode_responses=True,
                socket_connect_timeout=5,
                socket_timeout=5,
                retry_on_timeout=True,
                health_check_interval=30
            )
            await self.redis_client.ping()
            print("✅ Connected to Redis cache")
        except Exception as e:
            print(f"⚠️ Redis connection failed: {e}")
            self.cache_enabled = False
        
        # Load ML models
        model_dir = "../models"
        for model_name in self.models.keys():
            try:
                if model_name == 'predictive_maintenance':
                    model = PredictiveMaintenanceModel()
                elif model_name == 'anomaly_detection':
                    model = AnomalyDetectionModel()
                elif model_name == 'energy_optimization':
                    model = EnergyOptimizationModel()
                
                try:
                    model.load_model(model_dir)
                    self.models[model_name] = model
                    self.model_versions[model_name] = "2.0.0"
                    print(f"✅ Loaded {model_name} model")
                except:
                    # Use untrained model as fallback
                    self.models[model_name] = model
                    self.model_versions[model_name] = "2.0.0-untrained"
                    print(f"⚠️ Using untrained {model_name} model")
                    
            except Exception as e:
                print(f"❌ Failed to load {model_name}: {e}")
        
        # Start background batch processor
        if not self.batch_processor_running:
            asyncio.create_task(self._batch_processor())
            self.batch_processor_running = True
    
    async def _batch_processor(self):
        """Background task to process batched requests"""
        while True:
            try:
                # Process high priority requests first
                for priority in ['high', 'normal', 'low']:
                    if len(self.request_queue[priority]) > 0:
                        batch = []
                        while len(batch) < self.max_batch_size and len(self.request_queue[priority]) > 0:
                            batch.append(self.request_queue[priority].popleft())
                        
                        if batch:
                            await self._process_batch_internal(batch)
                
                await asyncio.sleep(0.01)  # 10ms processing interval
            except Exception as e:
                print(f"Error in batch processor: {e}")
                await asyncio.sleep(0.1)
    
    async def _get_cache_key(self, device_id: str, features: Dict[str, float], model_type: str) -> str:
        """Generate cache key for request"""
        # Create deterministic hash of features
        feature_hash = hash(json.dumps(features, sort_keys=True))
        return f"inference:{model_type}:{device_id}:{feature_hash}"
    
    async def _get_cached_prediction(self, cache_key: str) -> Optional[Dict[str, Any]]:
        """Get prediction from cache if available"""
        if not self.cache_enabled or not self.redis_client:
            return None
        
        try:
            cached_result = await self.redis_client.get(cache_key)
            if cached_result:
                return json.loads(cached_result)
        except Exception as e:
            print(f"Cache read error: {e}")
        
        return None
    
    async def _cache_prediction(self, cache_key: str, prediction: Dict[str, Any], ttl: int):
        """Cache prediction result"""
        if not self.cache_enabled or not self.redis_client:
            return
        
        try:
            await self.redis_client.setex(
                cache_key,
                ttl,
                json.dumps(prediction, default=str)
            )
        except Exception as e:
            print(f"Cache write error: {e}")
    
    def _is_circuit_breaker_open(self, model_type: str) -> bool:
        """Check if circuit breaker is open for model"""
        cb = self.circuit_breaker[model_type]
        if cb['open']:
            # Reset after 60 seconds
            if cb['last_failure'] and datetime.now() - cb['last_failure'] > timedelta(seconds=60):
                cb['open'] = False
                cb['failures'] = 0
                return False
            return True
        return False
    
    def _record_model_failure(self, model_type: str):
        """Record model failure for circuit breaker"""
        cb = self.circuit_breaker[model_type]
        cb['failures'] += 1
        cb['last_failure'] = datetime.now()
        
        # Open circuit breaker after 5 failures
        if cb['failures'] >= 5:
            cb['open'] = True
            print(f"⚠️ Circuit breaker opened for {model_type}")
    
    def _record_model_success(self, model_type: str):
        """Record model success"""
        cb = self.circuit_breaker[model_type]
        cb['failures'] = max(0, cb['failures'] - 1)
    
    async def _run_single_model(self, model_type: str, features: Dict[str, float]) -> Dict[str, Any]:
        """Run inference on a single model with error handling"""
        start_time = time.time()
        
        # Check circuit breaker
        if self._is_circuit_breaker_open(model_type):
            return {
                "error": f"{model_type} temporarily unavailable",
                "fallback": True
            }
        
        try:
            model = self.models[model_type]
            if not model:
                raise Exception(f"Model {model_type} not loaded")
            
            # Run prediction based on model type
            if model_type == 'predictive_maintenance':
                if hasattr(model, 'predict_single') and model.is_trained:
                    result = model.predict_single(features)
                else:
                    result = {
                        "prediction": "healthy",
                        "confidence": 0.95,
                        "risk_score": 0.05,
                        "fallback": True
                    }
            
            elif model_type == 'anomaly_detection':
                if hasattr(model, 'detect_single') and model.is_trained:
                    result = model.detect_single(features)
                else:
                    result = {
                        "is_anomaly": False,
                        "anomaly_score": 0.1,
                        "feature_contributions": {},
                        "fallback": True
                    }
            
            elif model_type == 'energy_optimization':
                # Add timestamp for energy model
                features_with_time = features.copy()
                features_with_time['timestamp'] = datetime.now().isoformat()
                
                if hasattr(model, 'predict_single') and model.is_trained:
                    result = model.predict_single(features_with_time)
                else:
                    result = {
                        "predicted_energy_peak": 2800.0,
                        "current_consumption": features.get('powerDraw', 2500.0),
                        "recommended_actions": ["Monitor consumption patterns"],
                        "potential_savings": 150.0,
                        "fallback": True
                    }
            
            # Record success and timing
            self._record_model_success(model_type)
            REQUEST_DURATION.labels(model_type=model_type).observe(time.time() - start_time)
            
            return result
            
        except Exception as e:
            print(f"Model {model_type} error: {e}")
            self._record_model_failure(model_type)
            
            # Return fallback prediction
            return {
                "error": str(e),
                "fallback": True,
                **self._get_fallback_prediction(model_type, features)
            }
    
    def _get_fallback_prediction(self, model_type: str, features: Dict[str, float]) -> Dict[str, Any]:
        """Get fallback prediction when model fails"""
        if model_type == 'predictive_maintenance':
            return {
                "prediction": "healthy",
                "confidence": 0.5,
                "risk_score": 0.1
            }
        elif model_type == 'anomaly_detection':
            return {
                "is_anomaly": False,
                "anomaly_score": 0.2,
                "feature_contributions": {}
            }
        elif model_type == 'energy_optimization':
            return {
                "predicted_energy_peak": features.get('powerDraw', 2500.0) * 1.1,
                "current_consumption": features.get('powerDraw', 2500.0),
                "recommended_actions": ["System unavailable - monitor manually"],
                "potential_savings": 0.0
            }
        return {}
    
    async def predict_single(self, request: OptimizedInferenceRequest) -> InferenceResponse:
        """Process single prediction request"""
        start_time = time.time()
        
        # Determine which models to run
        models_to_run = []
        if "all" in request.model_types:
            models_to_run = list(self.models.keys())
        else:
            models_to_run = [m for m in request.model_types if m in self.models]
        
        predictions = {}
        cached_results = {}
        
        # Check cache for each model
        for model_type in models_to_run:
            cache_key = await self._get_cache_key(request.deviceId, request.features, model_type)
            cached_pred = await self._get_cached_prediction(cache_key)
            
            if cached_pred:
                predictions[model_type] = cached_pred
                cached_results[model_type] = True
                CACHE_HITS.labels(model_type=model_type).inc()
            else:
                CACHE_MISSES.labels(model_type=model_type).inc()
        
        # Run inference for non-cached models
        tasks = []
        for model_type in models_to_run:
            if model_type not in predictions:
                ACTIVE_REQUESTS.labels(model_type=model_type).inc()
                task = self._run_single_model(model_type, request.features)
                tasks.append((model_type, task))
        
        # Execute remaining predictions concurrently
        if tasks:
            results = await asyncio.gather(*[task for _, task in tasks], return_exceptions=True)
            
            for i, (model_type, _) in enumerate(tasks):
                result = results[i]
                if isinstance(result, Exception):
                    predictions[model_type] = self._get_fallback_prediction(model_type, request.features)
                    predictions[model_type]["error"] = str(result)
                else:
                    predictions[model_type] = result
                
                ACTIVE_REQUESTS.labels(model_type=model_type).dec()
                
                # Cache successful predictions
                if not result.get("error") and request.cache_ttl > 0:
                    cache_key = await self._get_cache_key(request.deviceId, request.features, model_type)
                    await self._cache_prediction(cache_key, predictions[model_type], request.cache_ttl)
        
        # Record metrics
        inference_time_ms = (time.time() - start_time) * 1000
        
        for model_type in models_to_run:
            REQUEST_COUNT.labels(model_type=model_type, status='success').inc()
        
        return InferenceResponse(
            deviceId=request.deviceId,
            timestamp=request.timestamp,
            predictions=predictions,
            inference_time_ms=round(inference_time_ms, 2),
            cached=any(cached_results.values()),
            model_versions=self.model_versions
        )
    
    async def predict_batch(self, batch_request: BatchInferenceRequest) -> List[InferenceResponse]:
        """Process batch prediction requests"""
        start_time = time.time()
        BATCH_SIZE.observe(len(batch_request.requests))
        
        # Process all requests concurrently
        tasks = [self.predict_single(req) for req in batch_request.requests]
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Convert exceptions to error responses
        responses = []
        for i, result in enumerate(results):
            if isinstance(result, Exception):
                responses.append(InferenceResponse(
                    deviceId=batch_request.requests[i].deviceId,
                    timestamp=batch_request.requests[i].timestamp,
                    predictions={"error": str(result)},
                    inference_time_ms=(time.time() - start_time) * 1000,
                    model_versions=self.model_versions
                ))
            else:
                responses.append(result)
        
        return responses

# Global service instance
service = RealTimeInferenceService()

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup
    await service.initialize()
    yield
    # Shutdown
    if service.redis_client:
        await service.redis_client.close()

# Create FastAPI app with optimized settings
app = FastAPI(
    title="Real-Time Steel Mill ML Inference API",
    description="High-performance ML inference service with <100ms response times",
    version="2.0.0",
    lifespan=lifespan
)

# Add middleware for performance
app.add_middleware(GZipMiddleware, minimum_size=1000)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.middleware("http")
async def add_response_headers(request: Request, call_next):
    response = await call_next(request)
    response.headers["X-Response-Time"] = str(time.time())
    return response

# API Endpoints
@app.get("/")
async def root():
    return {
        "service": "Real-Time Steel Mill ML Inference API",
        "version": "2.0.0",
        "features": [
            "Sub-100ms inference times",
            "Batch processing",
            "Intelligent caching",
            "Circuit breaker pattern",
            "Prometheus metrics"
        ]
    }

@app.post("/predict", response_model=InferenceResponse)
async def predict_single_endpoint(request: OptimizedInferenceRequest):
    """Single prediction endpoint optimized for low latency"""
    return await service.predict_single(request)

@app.post("/predict/batch", response_model=List[InferenceResponse])
async def predict_batch_endpoint(batch_request: BatchInferenceRequest):
    """Batch prediction endpoint for high throughput"""
    return await service.predict_batch(batch_request)

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    health_status = {
        "status": "healthy",
        "timestamp": datetime.now().isoformat(),
        "models": {},
        "cache": "enabled" if service.cache_enabled else "disabled",
        "circuit_breakers": {}
    }
    
    for model_name, model in service.models.items():
        health_status["models"][model_name] = {
            "loaded": model is not None,
            "trained": hasattr(model, 'is_trained') and model.is_trained if model else False,
            "version": service.model_versions.get(model_name, "unknown")
        }
        
        cb = service.circuit_breaker[model_name]
        health_status["circuit_breakers"][model_name] = {
            "open": cb['open'],
            "failures": cb['failures']
        }
    
    return health_status

@app.get("/metrics")
async def metrics():
    """Prometheus metrics endpoint"""
    return Response(generate_latest(), media_type="text/plain")

if __name__ == "__main__":
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=8000,
        workers=1,  # Use single worker for shared state
        loop="uvloop",  # Use uvloop for better performance
        access_log=False,  # Disable access logs for performance
        server_header=False
    )