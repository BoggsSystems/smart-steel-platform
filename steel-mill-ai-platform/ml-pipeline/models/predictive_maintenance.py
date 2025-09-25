import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.preprocessing import StandardScaler
from typing import Dict, List, Tuple, Any
import joblib

class PredictiveMaintenanceModel:
    """
    Predictive Maintenance Model for steel mill equipment.
    Predicts binary outcome: healthy (0) or failure risk (1).
    """
    
    def __init__(self):
        self.model = RandomForestClassifier(
            n_estimators=100,
            max_depth=10,
            random_state=42
        )
        self.scaler = StandardScaler()
        self.feature_names = [
            'vibration', 'vibrationX', 'vibrationY', 'vibrationZ',
            'motorTorque', 'motorCurrent', 'temperature'
        ]
        self.is_trained = False
    
    def preprocess_features(self, data: pd.DataFrame) -> np.ndarray:
        """Extract and preprocess features from raw sensor data"""
        features = []
        
        for feature in self.feature_names:
            if feature in data.columns:
                features.append(data[feature].values)
            else:
                features.append(np.zeros(len(data)))
        
        return np.column_stack(features)
    
    def train(self, X: np.ndarray, y: np.ndarray) -> Dict[str, float]:
        """Train the predictive maintenance model"""
        X_scaled = self.scaler.fit_transform(X)
        self.model.fit(X_scaled, y)
        self.is_trained = True
        
        score = self.model.score(X_scaled, y)
        feature_importance = dict(zip(self.feature_names, self.model.feature_importances_))
        
        return {
            'accuracy': score,
            'feature_importance': feature_importance
        }
    
    def predict(self, X: np.ndarray) -> Tuple[np.ndarray, np.ndarray]:
        """Predict maintenance requirements"""
        if not self.is_trained:
            raise ValueError("Model must be trained before prediction")
        
        X_scaled = self.scaler.transform(X)
        predictions = self.model.predict(X_scaled)
        probabilities = self.model.predict_proba(X_scaled)[:, 1]
        
        return predictions, probabilities
    
    def predict_single(self, sensor_data: Dict[str, float]) -> Dict[str, Any]:
        """Predict for a single sensor reading"""
        df = pd.DataFrame([sensor_data])
        X = self.preprocess_features(df)
        
        prediction, probability = self.predict(X)
        
        return {
            'prediction': 'failure_risk' if prediction[0] == 1 else 'healthy',
            'confidence': float(probability[0]),
            'risk_score': float(probability[0])
        }
    
    def save_model(self, path: str):
        """Save trained model to disk"""
        model_data = {
            'model': self.model,
            'scaler': self.scaler,
            'feature_names': self.feature_names,
            'is_trained': self.is_trained
        }
        joblib.dump(model_data, f"{path}/predictive_maintenance_model.pkl")
    
    def load_model(self, path: str):
        """Load trained model from disk"""
        model_data = joblib.load(f"{path}/predictive_maintenance_model.pkl")
        self.model = model_data['model']
        self.scaler = model_data['scaler']
        self.feature_names = model_data['feature_names']
        self.is_trained = model_data['is_trained']
    
    def generate_synthetic_labels(self, X: np.ndarray) -> np.ndarray:
        """Generate synthetic labels for training based on feature patterns"""
        vibration_mean = X[:, 0:4].mean(axis=1)
        torque = X[:, 4]
        current = X[:, 5]
        temp = X[:, 6]
        
        risk_score = (
            (vibration_mean > np.percentile(vibration_mean, 80)) * 0.3 +
            (torque > np.percentile(torque, 85)) * 0.3 +
            (current > np.percentile(current, 90)) * 0.2 +
            (temp > np.percentile(temp, 85)) * 0.2
        )
        
        labels = (risk_score > 0.5).astype(int)
        
        noise_indices = np.random.choice(len(labels), size=int(0.1 * len(labels)), replace=False)
        labels[noise_indices] = 1 - labels[noise_indices]
        
        return labels