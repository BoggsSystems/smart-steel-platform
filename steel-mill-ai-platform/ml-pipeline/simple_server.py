"""
Simple Steel Mill AI Development Server
Minimal version for testing and development
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse
from typing import Dict, Any
import json
from datetime import datetime
import asyncio
import random

# Initialize FastAPI app
app = FastAPI(
    title="Steel Mill AI - Simple Development Server",
    description="Minimal ML API for local development",
    version="1.0.0"
)

# Configure CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Simple data store
synthetic_data = {
    "furnace_data": [],
    "quality_data": [],
    "maintenance_data": []
}

@app.get("/", response_class=HTMLResponse)
async def root():
    """Root endpoint with API documentation"""
    html_content = """
    <!DOCTYPE html>
    <html>
    <head>
        <title>Steel Mill AI - Development Server</title>
        <style>
            body { font-family: Arial, sans-serif; margin: 40px; }
            .header { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; }
            .endpoint { background-color: #f8f9fa; padding: 10px; margin: 10px 0; border-radius: 5px; }
            .method { color: #27ae60; font-weight: bold; }
        </style>
    </head>
    <body>
        <div class="header">
            <h1>🏭 Steel Mill AI - Development Server</h1>
            <p>Simple ML API for local development and testing</p>
        </div>
        
        <h2>🚀 Available Endpoints</h2>
        
        <div class="endpoint">
            <span class="method">GET</span> <strong>/health</strong> - Health check
        </div>
        
        <div class="endpoint">
            <span class="method">POST</span> <strong>/predict/quality</strong> - Quality prediction
        </div>
        
        <div class="endpoint">
            <span class="method">POST</span> <strong>/predict/defects</strong> - Defect detection
        </div>
        
        <div class="endpoint">
            <span class="method">POST</span> <strong>/predict/maintenance</strong> - Maintenance prediction
        </div>
        
        <div class="endpoint">
            <span class="method">GET</span> <strong>/data/synthetic</strong> - Synthetic data
        </div>
        
        <h2>📚 Documentation</h2>
        <p><a href="/docs">📖 Interactive API Documentation (Swagger)</a></p>
        
        <h2>🏭 Steel Mill Platform Status</h2>
        <p>✅ ML API Server Running</p>
        <p>⏸️ Infrastructure services (requires Docker)</p>
        <p>⏸️ Microservices (can be started individually)</p>
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
        "server": "simple",
        "version": "1.0.0"
    }

@app.post("/predict/quality")
async def predict_quality(data: Dict[str, Any]):
    """Mock quality prediction"""
    grade = data.get('grade', 'Grade60')
    carbon = data.get('carbon_content', 0.25)
    temp = data.get('billet_temperature', 1050)
    
    # Mock prediction based on inputs
    quality_score = 85 + random.uniform(0, 15)
    compliance = quality_score > 90
    
    # Mock mechanical properties based on grade
    grade_properties = {
        'Grade40': {'yield': 300, 'tensile': 450},
        'Grade60': {'yield': 450, 'tensile': 650},
        'Grade75': {'yield': 550, 'tensile': 730},
        'Grade80': {'yield': 580, 'tensile': 760}
    }
    
    props = grade_properties.get(grade, grade_properties['Grade60'])
    
    return {
        "status": "success",
        "prediction": {
            "astm_compliant": compliance,
            "quality_score": round(quality_score, 2),
            "predicted_yield_strength": props['yield'] + random.uniform(-20, 20),
            "predicted_tensile_strength": props['tensile'] + random.uniform(-30, 30),
            "predicted_elongation": 10 + random.uniform(0, 5),
            "confidence": random.uniform(0.8, 0.98)
        },
        "model": "mock_quality_prediction",
        "timestamp": datetime.now().isoformat()
    }

@app.post("/predict/defects")
async def predict_defects(data: Dict[str, Any]):
    """Mock defect detection"""
    surface_roughness = data.get('surface_roughness', 2.5)
    rib_height = data.get('rib_height', 0.7)
    
    # Mock defect prediction
    defects = [
        'no_defect', 'surface_crack', 'rib_height_low', 'rib_spacing_error',
        'dimensional_error', 'surface_scale'
    ]
    
    predicted_defect = random.choice(defects)
    severity = random.choice(['low', 'medium', 'high'])
    
    return {
        "status": "success",
        "prediction": {
            "primary_defect": predicted_defect,
            "severity": severity,
            "confidence": random.uniform(0.7, 0.95),
            "defect_probability": random.uniform(0.1, 0.9),
            "recommendations": [
                "Monitor surface finish quality",
                "Check ribbing roll alignment",
                "Verify temperature control"
            ]
        },
        "model": "mock_defect_detection",
        "timestamp": datetime.now().isoformat()
    }

@app.post("/predict/maintenance")
async def predict_maintenance(data: Dict[str, Any]):
    """Mock maintenance prediction"""
    vibration = data.get('vibration', 3.0)
    temperature = data.get('temperature', 75)
    
    # Mock maintenance prediction
    risk_score = random.uniform(0.1, 0.8)
    prediction = "failure_risk" if risk_score > 0.6 else "healthy"
    
    return {
        "status": "success",
        "prediction": {
            "prediction": prediction,
            "risk_score": round(risk_score, 3),
            "confidence": random.uniform(0.8, 0.95),
            "next_maintenance": f"{random.randint(5, 30)} days",
            "recommendations": [
                "Schedule routine inspection",
                "Monitor vibration levels",
                "Check lubrication system"
            ]
        },
        "model": "mock_maintenance_prediction",
        "timestamp": datetime.now().isoformat()
    }

@app.get("/data/synthetic")
async def get_synthetic_data():
    """Get current synthetic data"""
    # Generate some mock data
    furnace_data = {
        "temperature": 1500 + random.uniform(-100, 100),
        "power_consumption": 80 + random.uniform(-10, 10),
        "efficiency": 85 + random.uniform(0, 15),
        "status": random.choice(["operational", "maintenance", "startup"])
    }
    
    quality_data = {
        "sample_id": f"QC-{random.randint(1000, 9999)}",
        "grade": random.choice(["Grade40", "Grade60", "Grade75", "Grade80"]),
        "test_type": random.choice(["tensile", "bend", "dimensional"]),
        "passed": random.choice([True, False])
    }
    
    return {
        "status": "success",
        "data": {
            "furnace_data": furnace_data,
            "quality_data": quality_data,
            "timestamp": datetime.now().isoformat()
        }
    }

async def generate_data():
    """Background task to generate synthetic data"""
    while True:
        try:
            # Generate mock telemetry
            await asyncio.sleep(5)  # Generate every 5 seconds
        except Exception as e:
            print(f"Data generation error: {e}")

if __name__ == "__main__":
    import uvicorn
    print("🏭 Starting Steel Mill AI Simple Development Server...")
    print("📍 Server will be available at: http://localhost:8000")
    print("📖 API Documentation at: http://localhost:8000/docs")
    
    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")