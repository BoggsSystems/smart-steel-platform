#!/usr/bin/env python3
import json
import time
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Any
import numpy as np
from collections import deque
import threading
import os

class ModelPerformanceMonitor:
    """Real-time monitoring of ML model performance and drift detection"""
    
    def __init__(
        self,
        model_name: str,
        window_size: int = 1000,
        drift_threshold: float = 0.1,
        performance_threshold: float = 0.05
    ):
        self.model_name = model_name
        self.window_size = window_size
        self.drift_threshold = drift_threshold
        self.performance_threshold = performance_threshold
        
        # Rolling windows for metrics
        self.predictions = deque(maxlen=window_size)
        self.actuals = deque(maxlen=window_size)
        self.inference_times = deque(maxlen=window_size)
        self.feature_distributions = {}
        
        # Performance metrics history
        self.metrics_history = []
        self.alerts = []
        
        # Thread safety
        self.lock = threading.Lock()
        
        # Load historical data if exists
        self.data_file = f"./monitoring/{model_name}_performance.json"
        os.makedirs("./monitoring", exist_ok=True)
        self._load_historical_data()
    
    def _load_historical_data(self):
        """Load historical performance data"""
        if os.path.exists(self.data_file):
            try:
                with open(self.data_file, 'r') as f:
                    data = json.load(f)
                    self.metrics_history = data.get('metrics_history', [])
                    self.alerts = data.get('alerts', [])
            except Exception as e:
                print(f"Error loading historical data: {e}")
    
    def _save_data(self):
        """Save performance data to disk"""
        try:
            data = {
                'metrics_history': self.metrics_history[-100:],  # Keep last 100 records
                'alerts': self.alerts[-50:],  # Keep last 50 alerts
                'last_updated': datetime.utcnow().isoformat()
            }
            with open(self.data_file, 'w') as f:
                json.dump(data, f, indent=2, default=str)
        except Exception as e:
            print(f"Error saving performance data: {e}")
    
    def log_prediction(
        self,
        prediction: Any,
        features: Dict[str, float],
        inference_time: float,
        actual: Any = None
    ):
        """Log a model prediction for monitoring"""
        with self.lock:
            timestamp = datetime.utcnow()
            
            self.predictions.append({
                'value': prediction,
                'timestamp': timestamp,
                'features': features
            })
            
            if actual is not None:
                self.actuals.append({
                    'value': actual,
                    'timestamp': timestamp
                })
            
            self.inference_times.append({
                'time': inference_time,
                'timestamp': timestamp
            })
            
            # Update feature distributions for drift detection
            self._update_feature_distributions(features)
            
            # Check for alerts
            self._check_performance_alerts(inference_time)
    
    def _update_feature_distributions(self, features: Dict[str, float]):
        """Update feature distributions for drift detection"""
        for feature_name, value in features.items():
            if feature_name not in self.feature_distributions:
                self.feature_distributions[feature_name] = deque(maxlen=self.window_size)
            
            self.feature_distributions[feature_name].append({
                'value': value,
                'timestamp': datetime.utcnow()
            })
    
    def _check_performance_alerts(self, inference_time: float):
        """Check for performance degradation alerts"""
        # Check inference time
        if len(self.inference_times) >= 10:
            recent_times = [item['time'] for item in list(self.inference_times)[-10:]]
            avg_time = np.mean(recent_times)
            
            if avg_time > 1.0:  # Alert if average inference time > 1 second
                self._create_alert(
                    "slow_inference",
                    f"Slow inference detected: {avg_time:.3f}s average",
                    {"average_time": avg_time}
                )
        
        # Check prediction confidence (for classification models)
        if len(self.predictions) >= 20:
            recent_predictions = list(self.predictions)[-20:]
            
            # Check for low confidence predictions (assuming predictions have confidence scores)
            low_confidence_count = 0
            for pred in recent_predictions:
                if isinstance(pred['value'], dict) and 'confidence' in pred['value']:
                    if pred['value']['confidence'] < 0.7:
                        low_confidence_count += 1
            
            if low_confidence_count > 10:  # More than 50% low confidence
                self._create_alert(
                    "low_confidence",
                    f"High number of low confidence predictions: {low_confidence_count}/20",
                    {"low_confidence_count": low_confidence_count}
                )
    
    def _create_alert(self, alert_type: str, message: str, metadata: Dict[str, Any]):
        """Create a performance alert"""
        alert = {
            'type': alert_type,
            'message': message,
            'metadata': metadata,
            'timestamp': datetime.utcnow().isoformat(),
            'model_name': self.model_name
        }
        
        self.alerts.append(alert)
        print(f"ALERT [{self.model_name}]: {message}")
        
        # Save alerts to disk
        self._save_data()
    
    def detect_data_drift(self, feature_name: str) -> Dict[str, Any]:
        """Detect data drift for a specific feature"""
        if feature_name not in self.feature_distributions:
            return {"drift_detected": False, "reason": "No data for feature"}
        
        values = [item['value'] for item in self.feature_distributions[feature_name]]
        
        if len(values) < 100:  # Need sufficient data
            return {"drift_detected": False, "reason": "Insufficient data"}
        
        # Split into baseline and current windows
        baseline_size = len(values) // 2
        baseline = values[:baseline_size]
        current = values[baseline_size:]
        
        # Calculate distribution statistics
        baseline_mean = np.mean(baseline)
        baseline_std = np.std(baseline)
        current_mean = np.mean(current)
        current_std = np.std(current)
        
        # Detect drift using statistical tests
        mean_shift = abs(current_mean - baseline_mean) / (baseline_std + 1e-8)
        std_shift = abs(current_std - baseline_std) / (baseline_std + 1e-8)
        
        drift_detected = mean_shift > self.drift_threshold or std_shift > self.drift_threshold
        
        result = {
            "drift_detected": drift_detected,
            "feature_name": feature_name,
            "mean_shift": float(mean_shift),
            "std_shift": float(std_shift),
            "baseline_stats": {
                "mean": float(baseline_mean),
                "std": float(baseline_std),
                "count": len(baseline)
            },
            "current_stats": {
                "mean": float(current_mean),
                "std": float(current_std),
                "count": len(current)
            }
        }
        
        if drift_detected:
            self._create_alert(
                "data_drift",
                f"Data drift detected in feature {feature_name}",
                result
            )
        
        return result
    
    def calculate_model_accuracy(self) -> Optional[Dict[str, float]]:
        """Calculate model accuracy from logged predictions and actuals"""
        if len(self.predictions) == 0 or len(self.actuals) == 0:
            return None
        
        # Align predictions and actuals by timestamp
        predictions = list(self.predictions)
        actuals = list(self.actuals)
        
        aligned_pairs = []
        for pred in predictions:
            # Find closest actual within 1 minute
            closest_actual = None
            min_time_diff = timedelta(minutes=1)
            
            for actual in actuals:
                time_diff = abs(pred['timestamp'] - actual['timestamp'])
                if time_diff < min_time_diff:
                    min_time_diff = time_diff
                    closest_actual = actual
            
            if closest_actual:
                aligned_pairs.append((pred['value'], closest_actual['value']))
        
        if len(aligned_pairs) == 0:
            return None
        
        # Calculate metrics based on model type
        predictions_values = [p[0] for p in aligned_pairs]
        actuals_values = [p[1] for p in aligned_pairs]
        
        try:
            # For regression models
            if all(isinstance(p, (int, float)) for p in predictions_values):
                mse = np.mean([(p - a) ** 2 for p, a in aligned_pairs])
                mae = np.mean([abs(p - a) for p, a in aligned_pairs])
                return {
                    "mse": float(mse),
                    "mae": float(mae),
                    "rmse": float(np.sqrt(mse)),
                    "sample_count": len(aligned_pairs)
                }
            
            # For classification models
            else:
                correct = sum(1 for p, a in aligned_pairs if p == a)
                accuracy = correct / len(aligned_pairs)
                return {
                    "accuracy": float(accuracy),
                    "sample_count": len(aligned_pairs),
                    "correct_predictions": correct
                }
        
        except Exception as e:
            print(f"Error calculating accuracy: {e}")
            return None
    
    def get_performance_summary(self) -> Dict[str, Any]:
        """Get comprehensive performance summary"""
        with self.lock:
            summary = {
                "model_name": self.model_name,
                "timestamp": datetime.utcnow().isoformat(),
                "prediction_count": len(self.predictions),
                "recent_alerts": self.alerts[-5:] if self.alerts else [],
                "alert_count": len(self.alerts)
            }
            
            # Inference time statistics
            if self.inference_times:
                times = [item['time'] for item in self.inference_times]
                summary["inference_stats"] = {
                    "avg_time": float(np.mean(times)),
                    "p95_time": float(np.percentile(times, 95)),
                    "p99_time": float(np.percentile(times, 99)),
                    "max_time": float(np.max(times))
                }
            
            # Model accuracy
            accuracy = self.calculate_model_accuracy()
            if accuracy:
                summary["accuracy_stats"] = accuracy
            
            # Feature drift summary
            drift_results = {}
            for feature_name in self.feature_distributions.keys():
                drift_results[feature_name] = self.detect_data_drift(feature_name)
            
            summary["drift_detection"] = drift_results
            
            return summary
    
    def reset_monitoring(self):
        """Reset all monitoring data"""
        with self.lock:
            self.predictions.clear()
            self.actuals.clear()
            self.inference_times.clear()
            self.feature_distributions.clear()
            self.alerts.clear()
            print(f"Reset monitoring data for {self.model_name}")
    
    def export_metrics(self, filepath: str):
        """Export performance metrics to file"""
        summary = self.get_performance_summary()
        
        with open(filepath, 'w') as f:
            json.dump(summary, f, indent=2, default=str)
        
        print(f"Exported performance metrics to {filepath}")

# Global monitoring instances
_monitors = {}

def get_monitor(model_name: str) -> ModelPerformanceMonitor:
    """Get or create a performance monitor for a model"""
    if model_name not in _monitors:
        _monitors[model_name] = ModelPerformanceMonitor(model_name)
    return _monitors[model_name]