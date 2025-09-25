import numpy as np
import pandas as pd
from sklearn.ensemble import IsolationForest
from sklearn.preprocessing import StandardScaler
from typing import Dict, List, Tuple, Any
import joblib

class AnomalyDetectionModel:
    """
    Anomaly Detection Model for steel mill sensor data.
    Uses Isolation Forest to detect unusual patterns in sensor readings.
    """
    
    def __init__(self):
        self.model = IsolationForest(
            contamination=0.05,
            random_state=42,
            n_estimators=100
        )
        self.scaler = StandardScaler()
        self.feature_names = [
            'temperature', 'vibration', 'oxygenMix', 'fuelFlow',
            'powerDraw', 'speed', 'pressure'
        ]
        self.is_trained = False
        self.baseline_stats = {}
    
    def preprocess_features(self, data: pd.DataFrame) -> np.ndarray:
        """Extract and preprocess features from raw sensor data"""
        features = []
        
        for feature in self.feature_names:
            if feature in data.columns:
                features.append(data[feature].values)
            else:
                features.append(np.zeros(len(data)))
        
        X = np.column_stack(features)
        
        if len(data) > 1:
            rolling_mean = pd.DataFrame(X).rolling(window=5, min_periods=1).mean().values
            rolling_std = pd.DataFrame(X).rolling(window=5, min_periods=1).std().fillna(0).values
            X = np.hstack([X, rolling_mean, rolling_std])
        
        return X
    
    def train(self, X: np.ndarray) -> Dict[str, Any]:
        """Train the anomaly detection model"""
        X_scaled = self.scaler.fit_transform(X)
        self.model.fit(X_scaled)
        self.is_trained = True
        
        self.baseline_stats = {
            'mean': np.mean(X, axis=0),
            'std': np.std(X, axis=0),
            'percentiles': {
                '25': np.percentile(X, 25, axis=0),
                '50': np.percentile(X, 50, axis=0),
                '75': np.percentile(X, 75, axis=0),
                '95': np.percentile(X, 95, axis=0)
            }
        }
        
        anomaly_scores = self.model.score_samples(X_scaled)
        
        return {
            'contamination_rate': self.model.contamination,
            'avg_anomaly_score': float(np.mean(anomaly_scores)),
            'score_threshold': float(np.percentile(anomaly_scores, 5))
        }
    
    def detect_anomalies(self, X: np.ndarray) -> Tuple[np.ndarray, np.ndarray]:
        """Detect anomalies in sensor data"""
        if not self.is_trained:
            raise ValueError("Model must be trained before detection")
        
        X_scaled = self.scaler.transform(X)
        predictions = self.model.predict(X_scaled)
        anomaly_scores = self.model.score_samples(X_scaled)
        
        anomaly_scores_normalized = 1 / (1 + np.exp(-anomaly_scores))
        
        return predictions, anomaly_scores_normalized
    
    def detect_single(self, sensor_data: Dict[str, float]) -> Dict[str, Any]:
        """Detect anomaly for a single sensor reading"""
        df = pd.DataFrame([sensor_data])
        X = self.preprocess_features(df)
        
        prediction, score = self.detect_anomalies(X)
        
        feature_contributions = {}
        if self.baseline_stats:
            for i, feature in enumerate(self.feature_names):
                if feature in sensor_data:
                    value = sensor_data[feature]
                    mean = self.baseline_stats['mean'][i]
                    std = self.baseline_stats['std'][i]
                    if std > 0:
                        z_score = abs(value - mean) / std
                        feature_contributions[feature] = float(z_score)
        
        return {
            'is_anomaly': bool(prediction[0] == -1),
            'anomaly_score': float(score[0]),
            'feature_contributions': feature_contributions
        }
    
    def save_model(self, path: str):
        """Save trained model to disk"""
        model_data = {
            'model': self.model,
            'scaler': self.scaler,
            'feature_names': self.feature_names,
            'is_trained': self.is_trained,
            'baseline_stats': self.baseline_stats
        }
        joblib.dump(model_data, f"{path}/anomaly_detection_model.pkl")
    
    def load_model(self, path: str):
        """Load trained model from disk"""
        model_data = joblib.load(f"{path}/anomaly_detection_model.pkl")
        self.model = model_data['model']
        self.scaler = model_data['scaler']
        self.feature_names = model_data['feature_names']
        self.is_trained = model_data['is_trained']
        self.baseline_stats = model_data.get('baseline_stats', {})
    
    def inject_synthetic_anomalies(self, X: np.ndarray, anomaly_rate: float = 0.05) -> np.ndarray:
        """Inject synthetic anomalies into training data"""
        n_samples = len(X)
        n_anomalies = int(n_samples * anomaly_rate)
        anomaly_indices = np.random.choice(n_samples, n_anomalies, replace=False)
        
        X_with_anomalies = X.copy()
        
        for idx in anomaly_indices:
            anomaly_type = np.random.choice(['spike', 'drift', 'noise'])
            
            if anomaly_type == 'spike':
                feature_idx = np.random.randint(0, X.shape[1])
                X_with_anomalies[idx, feature_idx] *= np.random.uniform(2, 5)
            elif anomaly_type == 'drift':
                X_with_anomalies[idx] *= np.random.uniform(1.5, 2.0)
            else:
                X_with_anomalies[idx] += np.random.normal(0, 2, X.shape[1])
        
        return X_with_anomalies