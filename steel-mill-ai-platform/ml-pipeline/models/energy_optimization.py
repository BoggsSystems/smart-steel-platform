import numpy as np
import pandas as pd
from sklearn.ensemble import GradientBoostingRegressor
from sklearn.preprocessing import StandardScaler
from typing import Dict, List, Tuple, Any
import joblib
from datetime import datetime

class EnergyOptimizationModel:
    """
    Energy Optimization Model for steel mill operations.
    Predicts energy consumption peaks and provides optimization recommendations.
    """
    
    def __init__(self):
        self.model = GradientBoostingRegressor(
            n_estimators=100,
            max_depth=5,
            learning_rate=0.1,
            random_state=42
        )
        self.scaler = StandardScaler()
        self.feature_names = [
            'powerDraw', 'hour_of_day', 'day_of_week',
            'furnace_load', 'rolling_load', 'temperature',
            'production_rate', 'ambient_temp'
        ]
        self.is_trained = False
        self.energy_baselines = {}
    
    def extract_temporal_features(self, timestamps: List[str]) -> np.ndarray:
        """Extract time-based features from timestamps"""
        temporal_features = []
        
        for ts_str in timestamps:
            if isinstance(ts_str, str):
                ts = datetime.fromisoformat(ts_str.replace('Z', '+00:00'))
            else:
                ts = ts_str
            
            hour = ts.hour
            day_of_week = ts.weekday()
            
            hour_sin = np.sin(2 * np.pi * hour / 24)
            hour_cos = np.cos(2 * np.pi * hour / 24)
            
            temporal_features.append([hour, day_of_week, hour_sin, hour_cos])
        
        return np.array(temporal_features)
    
    def preprocess_features(self, data: pd.DataFrame) -> np.ndarray:
        """Extract and preprocess features from raw sensor data"""
        features = []
        
        base_features = ['powerDraw', 'temperature']
        for feature in base_features:
            if feature in data.columns:
                features.append(data[feature].values)
            else:
                features.append(np.zeros(len(data)))
        
        if 'timestamp' in data.columns:
            temporal_features = self.extract_temporal_features(data['timestamp'].tolist())
            features.extend([temporal_features[:, i] for i in range(temporal_features.shape[1])])
        else:
            for _ in range(4):
                features.append(np.zeros(len(data)))
        
        furnace_load = np.random.uniform(0.6, 0.95, len(data))
        rolling_load = np.random.uniform(0.5, 0.9, len(data))
        production_rate = np.random.uniform(80, 120, len(data))
        ambient_temp = np.random.uniform(15, 30, len(data))
        
        features.extend([furnace_load, rolling_load, production_rate, ambient_temp])
        
        return np.column_stack(features)
    
    def train(self, X: np.ndarray, y: np.ndarray) -> Dict[str, float]:
        """Train the energy optimization model"""
        X_scaled = self.scaler.fit_transform(X)
        self.model.fit(X_scaled, y)
        self.is_trained = True
        
        score = self.model.score(X_scaled, y)
        feature_importance = dict(zip(
            self.feature_names + ['hour_sin', 'hour_cos'],
            self.model.feature_importances_
        ))
        
        self.energy_baselines = {
            'daily_avg': float(np.mean(y)),
            'peak_threshold': float(np.percentile(y, 90)),
            'off_peak_avg': float(np.percentile(y, 25))
        }
        
        return {
            'r2_score': score,
            'feature_importance': feature_importance,
            'energy_baselines': self.energy_baselines
        }
    
    def predict(self, X: np.ndarray) -> np.ndarray:
        """Predict energy consumption"""
        if not self.is_trained:
            raise ValueError("Model must be trained before prediction")
        
        X_scaled = self.scaler.transform(X)
        predictions = self.model.predict(X_scaled)
        
        return predictions
    
    def predict_single(self, sensor_data: Dict[str, Any]) -> Dict[str, Any]:
        """Predict energy consumption for a single reading and provide recommendations"""
        df = pd.DataFrame([sensor_data])
        X = self.preprocess_features(df)
        
        predicted_energy = self.predict(X)[0]
        
        recommendations = []
        potential_savings = 0
        
        if predicted_energy > self.energy_baselines.get('peak_threshold', 3000):
            recommendations.append("Shift non-critical operations to off-peak hours")
            potential_savings += (predicted_energy - self.energy_baselines['daily_avg']) * 0.2
        
        if 'hour_of_day' in sensor_data and sensor_data['hour_of_day'] in [11, 12, 13, 18, 19]:
            recommendations.append("Consider reducing furnace temperature by 50°C during peak hours")
            potential_savings += predicted_energy * 0.1
        
        if sensor_data.get('furnace_load', 0) < 0.7:
            recommendations.append("Increase furnace utilization to improve efficiency")
            potential_savings += predicted_energy * 0.05
        
        return {
            'predicted_energy_peak': float(predicted_energy),
            'current_consumption': float(sensor_data.get('powerDraw', 0)),
            'recommended_actions': recommendations,
            'potential_savings': float(potential_savings),
            'peak_probability': float(
                1 if predicted_energy > self.energy_baselines.get('peak_threshold', 3000) else 0
            )
        }
    
    def save_model(self, path: str):
        """Save trained model to disk"""
        model_data = {
            'model': self.model,
            'scaler': self.scaler,
            'feature_names': self.feature_names,
            'is_trained': self.is_trained,
            'energy_baselines': self.energy_baselines
        }
        joblib.dump(model_data, f"{path}/energy_optimization_model.pkl")
    
    def load_model(self, path: str):
        """Load trained model from disk"""
        model_data = joblib.load(f"{path}/energy_optimization_model.pkl")
        self.model = model_data['model']
        self.scaler = model_data['scaler']
        self.feature_names = model_data['feature_names']
        self.is_trained = model_data['is_trained']
        self.energy_baselines = model_data.get('energy_baselines', {})
    
    def generate_synthetic_targets(self, X: np.ndarray) -> np.ndarray:
        """Generate synthetic energy consumption targets based on features"""
        base_consumption = 2500
        
        power_draw = X[:, 0]
        hour_of_day = X[:, 2]
        furnace_load = X[:, 6]
        rolling_load = X[:, 7]
        
        peak_hours = np.isin(hour_of_day, [11, 12, 13, 18, 19, 20])
        
        energy = (
            base_consumption +
            power_draw * 0.8 +
            furnace_load * 500 +
            rolling_load * 300 +
            peak_hours * 500 +
            np.random.normal(0, 100, len(X))
        )
        
        return energy