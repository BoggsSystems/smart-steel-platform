#!/usr/bin/env python3
"""
Ensemble Models and A/B Testing Framework for Steel Mill AI Platform
Advanced ML techniques for improved accuracy and reliability
"""
import numpy as np
import pandas as pd
from typing import Dict, List, Any, Optional, Tuple, Union
from dataclasses import dataclass
from datetime import datetime
import json
import random
from abc import ABC, abstractmethod

import joblib
from sklearn.ensemble import VotingClassifier, VotingRegressor
from sklearn.model_selection import cross_val_score
from sklearn.metrics import accuracy_score, mean_squared_error, f1_score
import redis.asyncio as redis

# Import our base models
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from models.predictive_maintenance import PredictiveMaintenanceModel
from models.anomaly_detection import AnomalyDetectionModel
from models.energy_optimization import EnergyOptimizationModel

@dataclass
class ModelVariant:
    name: str
    version: str
    model: Any
    weight: float
    performance_metrics: Dict[str, float]
    deployment_date: datetime
    traffic_percentage: float = 0.0
    is_champion: bool = False

@dataclass
class ABTestResult:
    test_id: str
    champion_variant: str
    challenger_variant: str
    champion_performance: Dict[str, float]
    challenger_performance: Dict[str, float]
    statistical_significance: bool
    p_value: float
    sample_size: int
    test_duration_hours: float
    winner: Optional[str] = None

class EnsembleMethod(ABC):
    """Abstract base class for ensemble methods"""
    
    @abstractmethod
    def predict(self, models: List[ModelVariant], features: np.ndarray) -> Dict[str, Any]:
        pass
    
    @abstractmethod
    def get_feature_importance(self, models: List[ModelVariant]) -> Dict[str, float]:
        pass

class WeightedVotingEnsemble(EnsembleMethod):
    """Weighted voting ensemble for multiple models"""
    
    def predict(self, models: List[ModelVariant], features: np.ndarray) -> Dict[str, Any]:
        predictions = []
        weights = []
        model_contributions = {}
        
        for variant in models:
            try:
                # Get prediction from individual model
                if hasattr(variant.model, 'predict_single'):
                    # For dictionary input (our custom models)
                    feature_dict = self._array_to_feature_dict(features, variant.model)
                    pred = variant.model.predict_single(feature_dict)
                elif hasattr(variant.model, 'predict'):
                    # For sklearn-style models
                    pred = variant.model.predict(features.reshape(1, -1))[0]
                else:
                    continue
                
                predictions.append(pred)
                weights.append(variant.weight)
                model_contributions[variant.name] = {
                    'prediction': pred,
                    'weight': variant.weight,
                    'version': variant.version
                }
                
            except Exception as e:
                print(f"Error in model {variant.name}: {e}")
                continue
        
        if not predictions:
            return {"error": "No models available for prediction"}
        
        # Weighted ensemble prediction
        if isinstance(predictions[0], dict):
            # Handle complex predictions (like our ML models)
            ensemble_pred = self._ensemble_dict_predictions(predictions, weights)
        else:
            # Handle simple numeric predictions
            ensemble_pred = np.average(predictions, weights=weights)
        
        return {
            "ensemble_prediction": ensemble_pred,
            "individual_predictions": model_contributions,
            "ensemble_confidence": self._calculate_ensemble_confidence(predictions, weights),
            "num_models": len(predictions)
        }
    
    def _array_to_feature_dict(self, features: np.ndarray, model: Any) -> Dict[str, float]:
        """Convert feature array to dictionary based on model's expected features"""
        feature_dict = {}
        
        # Try to get feature names from model
        if hasattr(model, 'feature_names'):
            feature_names = model.feature_names
        else:
            # Default feature names for steel mill models
            feature_names = [
                'vibration', 'vibrationX', 'vibrationY', 'vibrationZ',
                'motorTorque', 'motorCurrent', 'temperature', 'powerDraw',
                'speed', 'pressure', 'fuelFlow', 'oxygenMix'
            ]
        
        for i, value in enumerate(features):
            if i < len(feature_names):
                feature_dict[feature_names[i]] = float(value)
        
        return feature_dict
    
    def _ensemble_dict_predictions(self, predictions: List[Dict], weights: List[float]) -> Dict[str, Any]:
        """Ensemble complex dictionary predictions"""
        ensemble_result = {}
        
        # Handle different types of predictions
        if 'prediction' in predictions[0]:
            # Predictive maintenance style
            pred_counts = {}
            confidence_sum = 0.0
            
            for pred, weight in zip(predictions, weights):
                pred_value = pred.get('prediction', 'unknown')
                pred_counts[pred_value] = pred_counts.get(pred_value, 0) + weight
                confidence_sum += pred.get('confidence', 0.5) * weight
            
            # Most weighted prediction wins
            best_pred = max(pred_counts, key=pred_counts.get)
            ensemble_result = {
                'prediction': best_pred,
                'confidence': confidence_sum / sum(weights),
                'vote_distribution': pred_counts
            }
            
        elif 'is_anomaly' in predictions[0]:
            # Anomaly detection style
            anomaly_score_sum = 0.0
            anomaly_votes = 0
            
            for pred, weight in zip(predictions, weights):
                anomaly_score_sum += pred.get('anomaly_score', 0.0) * weight
                if pred.get('is_anomaly', False):
                    anomaly_votes += weight
            
            ensemble_result = {
                'is_anomaly': anomaly_votes > (sum(weights) / 2),
                'anomaly_score': anomaly_score_sum / sum(weights),
                'anomaly_vote_weight': anomaly_votes
            }
            
        elif 'predicted_energy_peak' in predictions[0]:
            # Energy optimization style
            energy_sum = 0.0
            savings_sum = 0.0
            all_actions = set()
            
            for pred, weight in zip(predictions, weights):
                energy_sum += pred.get('predicted_energy_peak', 0.0) * weight
                savings_sum += pred.get('potential_savings', 0.0) * weight
                all_actions.update(pred.get('recommended_actions', []))
            
            ensemble_result = {
                'predicted_energy_peak': energy_sum / sum(weights),
                'potential_savings': savings_sum / sum(weights),
                'recommended_actions': list(all_actions)
            }
        
        return ensemble_result
    
    def _calculate_ensemble_confidence(self, predictions: List, weights: List[float]) -> float:
        """Calculate confidence score for ensemble prediction"""
        if isinstance(predictions[0], dict):
            confidences = []
            for pred in predictions:
                if 'confidence' in pred:
                    confidences.append(pred['confidence'])
                elif 'anomaly_score' in pred:
                    confidences.append(1.0 - pred['anomaly_score'])
                else:
                    confidences.append(0.7)  # Default confidence
            
            return np.average(confidences, weights=weights)
        else:
            # For numeric predictions, calculate confidence based on agreement
            pred_std = np.std(predictions)
            pred_mean = np.mean(predictions)
            
            # Higher agreement (lower std relative to mean) = higher confidence
            if pred_mean != 0:
                coefficient_of_variation = pred_std / abs(pred_mean)
                confidence = max(0.1, 1.0 - coefficient_of_variation)
            else:
                confidence = 0.5
            
            return confidence
    
    def get_feature_importance(self, models: List[ModelVariant]) -> Dict[str, float]:
        """Aggregate feature importance from ensemble models"""
        feature_importance = {}
        total_weight = sum(model.weight for model in models)
        
        for model_variant in models:
            model = model_variant.model
            weight = model_variant.weight / total_weight
            
            # Try to get feature importance from model
            model_importance = {}
            if hasattr(model, 'feature_importances_'):
                # Sklearn models
                feature_names = getattr(model, 'feature_names_', 
                                      [f'feature_{i}' for i in range(len(model.feature_importances_))])
                model_importance = dict(zip(feature_names, model.feature_importances_))
            elif hasattr(model, 'get_feature_importance'):
                # Custom method
                model_importance = model.get_feature_importance()
            
            # Aggregate weighted importance
            for feature, importance in model_importance.items():
                feature_importance[feature] = feature_importance.get(feature, 0.0) + importance * weight
        
        return feature_importance

class ABTestManager:
    """A/B Testing framework for model comparison"""
    
    def __init__(self, redis_client=None):
        self.redis_client = redis_client
        self.active_tests: Dict[str, Dict] = {}
        self.test_results: List[ABTestResult] = []
    
    async def create_ab_test(self, 
                            test_id: str,
                            champion_model: ModelVariant,
                            challenger_model: ModelVariant,
                            traffic_split: float = 0.1,
                            min_samples: int = 1000,
                            max_duration_hours: int = 168) -> bool:
        """Create a new A/B test"""
        
        test_config = {
            'test_id': test_id,
            'champion': champion_model.name,
            'challenger': challenger_model.name,
            'traffic_split': traffic_split,
            'min_samples': min_samples,
            'max_duration_hours': max_duration_hours,
            'start_time': datetime.now().isoformat(),
            'status': 'active',
            'champion_results': [],
            'challenger_results': [],
            'champion_predictions': [],
            'challenger_predictions': []
        }
        
        self.active_tests[test_id] = test_config
        
        # Store in Redis if available
        if self.redis_client:
            await self.redis_client.setex(
                f"ab_test:{test_id}",
                max_duration_hours * 3600,
                json.dumps(test_config, default=str)
            )
        
        print(f"✅ Created A/B test {test_id}: {champion_model.name} vs {challenger_model.name}")
        return True
    
    def should_use_challenger(self, test_id: str) -> bool:
        """Determine if request should use challenger model"""
        if test_id not in self.active_tests:
            return False
        
        test_config = self.active_tests[test_id]
        return random.random() < test_config['traffic_split']
    
    async def record_prediction(self, 
                               test_id: str, 
                               model_name: str, 
                               prediction: Any, 
                               ground_truth: Any = None,
                               features: np.ndarray = None,
                               response_time_ms: float = None):
        """Record prediction result for A/B test"""
        if test_id not in self.active_tests:
            return
        
        test_config = self.active_tests[test_id]
        timestamp = datetime.now().isoformat()
        
        record = {
            'timestamp': timestamp,
            'prediction': prediction,
            'ground_truth': ground_truth,
            'response_time_ms': response_time_ms,
            'features': features.tolist() if features is not None else None
        }
        
        # Store based on model type
        if model_name == test_config['champion']:
            test_config['champion_results'].append(record)
        elif model_name == test_config['challenger']:
            test_config['challenger_results'].append(record)
        
        # Update Redis
        if self.redis_client:
            await self.redis_client.setex(
                f"ab_test:{test_id}",
                test_config['max_duration_hours'] * 3600,
                json.dumps(test_config, default=str)
            )
        
        # Check if test should be evaluated
        await self._check_test_completion(test_id)
    
    async def _check_test_completion(self, test_id: str):
        """Check if A/B test has enough data for evaluation"""
        test_config = self.active_tests[test_id]
        
        champion_count = len(test_config['champion_results'])
        challenger_count = len(test_config['challenger_results'])
        
        # Check if we have minimum samples
        if champion_count >= test_config['min_samples'] and challenger_count >= test_config['min_samples']:
            await self.evaluate_test(test_id)
        
        # Check if maximum duration exceeded
        start_time = datetime.fromisoformat(test_config['start_time'])
        duration_hours = (datetime.now() - start_time).total_seconds() / 3600
        
        if duration_hours >= test_config['max_duration_hours']:
            await self.evaluate_test(test_id, force_completion=True)
    
    async def evaluate_test(self, test_id: str, force_completion: bool = False) -> ABTestResult:
        """Evaluate A/B test results and determine winner"""
        if test_id not in self.active_tests:
            raise ValueError(f"Test {test_id} not found")
        
        test_config = self.active_tests[test_id]
        champion_results = test_config['champion_results']
        challenger_results = test_config['challenger_results']
        
        if not champion_results or not challenger_results:
            print(f"⚠️ Insufficient data for test {test_id}")
            return None
        
        # Calculate performance metrics
        champion_metrics = self._calculate_performance_metrics(champion_results)
        challenger_metrics = self._calculate_performance_metrics(challenger_results)
        
        # Perform statistical significance test
        significance_result = self._statistical_significance_test(
            champion_results, challenger_results
        )
        
        # Determine winner
        winner = None
        if significance_result['significant']:
            if challenger_metrics['primary_metric'] > champion_metrics['primary_metric']:
                winner = test_config['challenger']
            else:
                winner = test_config['champion']
        
        # Create result object
        start_time = datetime.fromisoformat(test_config['start_time'])
        duration_hours = (datetime.now() - start_time).total_seconds() / 3600
        
        result = ABTestResult(
            test_id=test_id,
            champion_variant=test_config['champion'],
            challenger_variant=test_config['challenger'],
            champion_performance=champion_metrics,
            challenger_performance=challenger_metrics,
            statistical_significance=significance_result['significant'],
            p_value=significance_result['p_value'],
            sample_size=len(champion_results) + len(challenger_results),
            test_duration_hours=duration_hours,
            winner=winner
        )
        
        # Store result and clean up active test
        self.test_results.append(result)
        test_config['status'] = 'completed'
        
        print(f"🏁 A/B test {test_id} completed. Winner: {winner or 'No significant difference'}")
        print(f"   Champion: {champion_metrics['primary_metric']:.3f}")
        print(f"   Challenger: {challenger_metrics['primary_metric']:.3f}")
        print(f"   Statistical significance: {significance_result['significant']} (p={significance_result['p_value']:.4f})")
        
        return result
    
    def _calculate_performance_metrics(self, results: List[Dict]) -> Dict[str, float]:
        """Calculate performance metrics from prediction results"""
        if not results:
            return {'primary_metric': 0.0, 'secondary_metrics': {}}
        
        # Filter results with ground truth
        valid_results = [r for r in results if r.get('ground_truth') is not None]
        
        if not valid_results:
            # Use response time as metric if no ground truth
            response_times = [r.get('response_time_ms', 1000) for r in results if r.get('response_time_ms')]
            return {
                'primary_metric': np.mean(response_times) if response_times else 1000,
                'secondary_metrics': {
                    'avg_response_time_ms': np.mean(response_times) if response_times else 1000,
                    'sample_count': len(results)
                }
            }
        
        # Calculate accuracy/error metrics based on prediction type
        predictions = [r['prediction'] for r in valid_results]
        ground_truths = [r['ground_truth'] for r in valid_results]
        
        metrics = {'secondary_metrics': {'sample_count': len(valid_results)}}
        
        # Handle different prediction types
        if isinstance(predictions[0], dict):
            # Complex predictions - use confidence or score
            if 'confidence' in predictions[0]:
                confidences = [p.get('confidence', 0.5) for p in predictions]
                metrics['primary_metric'] = np.mean(confidences)
            elif 'anomaly_score' in predictions[0]:
                scores = [1.0 - p.get('anomaly_score', 0.5) for p in predictions]
                metrics['primary_metric'] = np.mean(scores)
            else:
                metrics['primary_metric'] = 0.7  # Default
        else:
            # Numeric predictions - calculate MSE
            mse = mean_squared_error(ground_truths, predictions)
            metrics['primary_metric'] = 1.0 / (1.0 + mse)  # Convert to "higher is better"
            metrics['secondary_metrics']['mse'] = mse
        
        # Add response time metrics
        response_times = [r.get('response_time_ms') for r in results if r.get('response_time_ms')]
        if response_times:
            metrics['secondary_metrics'].update({
                'avg_response_time_ms': np.mean(response_times),
                'p95_response_time_ms': np.percentile(response_times, 95)
            })
        
        return metrics
    
    def _statistical_significance_test(self, 
                                     champion_results: List[Dict], 
                                     challenger_results: List[Dict]) -> Dict[str, Any]:
        """Perform statistical significance test between champion and challenger"""
        try:
            from scipy import stats
            
            # Extract primary metrics
            champion_metrics = [self._extract_metric_value(r) for r in champion_results]
            challenger_metrics = [self._extract_metric_value(r) for r in challenger_results]
            
            # Remove None values
            champion_metrics = [m for m in champion_metrics if m is not None]
            challenger_metrics = [m for m in challenger_metrics if m is not None]
            
            if len(champion_metrics) < 30 or len(challenger_metrics) < 30:
                return {'significant': False, 'p_value': 1.0, 'reason': 'Insufficient sample size'}
            
            # Perform two-sample t-test
            statistic, p_value = stats.ttest_ind(challenger_metrics, champion_metrics)
            significant = p_value < 0.05
            
            return {
                'significant': significant,
                'p_value': p_value,
                'statistic': statistic,
                'champion_mean': np.mean(champion_metrics),
                'challenger_mean': np.mean(challenger_metrics)
            }
            
        except ImportError:
            # Fallback without scipy
            champion_vals = [self._extract_metric_value(r) for r in champion_results[-100:]]
            challenger_vals = [self._extract_metric_value(r) for r in challenger_results[-100:]]
            
            champion_vals = [v for v in champion_vals if v is not None]
            challenger_vals = [v for v in challenger_vals if v is not None]
            
            if not champion_vals or not challenger_vals:
                return {'significant': False, 'p_value': 1.0}
            
            # Simple comparison
            champion_mean = np.mean(champion_vals)
            challenger_mean = np.mean(challenger_vals)
            difference_pct = abs(challenger_mean - champion_mean) / champion_mean
            
            return {
                'significant': difference_pct > 0.05,  # 5% difference threshold
                'p_value': 0.04 if difference_pct > 0.05 else 0.1,
                'champion_mean': champion_mean,
                'challenger_mean': challenger_mean
            }
    
    def _extract_metric_value(self, result: Dict) -> Optional[float]:
        """Extract numeric metric value from prediction result"""
        pred = result.get('prediction')
        if pred is None:
            return None
        
        if isinstance(pred, dict):
            if 'confidence' in pred:
                return pred['confidence']
            elif 'anomaly_score' in pred:
                return 1.0 - pred['anomaly_score']
            else:
                return 0.7
        elif isinstance(pred, (int, float)):
            return float(pred)
        else:
            return None

class EnsembleModelManager:
    """Manager for ensemble models and A/B testing"""
    
    def __init__(self):
        self.model_variants: Dict[str, List[ModelVariant]] = {
            'predictive_maintenance': [],
            'anomaly_detection': [],
            'energy_optimization': []
        }
        self.ensemble_methods: Dict[str, EnsembleMethod] = {
            'weighted_voting': WeightedVotingEnsemble()
        }
        self.ab_test_manager = ABTestManager()
    
    def register_model_variant(self, 
                              model_type: str, 
                              variant: ModelVariant):
        """Register a new model variant"""
        if model_type not in self.model_variants:
            self.model_variants[model_type] = []
        
        self.model_variants[model_type].append(variant)
        print(f"✅ Registered {variant.name} for {model_type}")
    
    def get_active_models(self, model_type: str) -> List[ModelVariant]:
        """Get active model variants for a given type"""
        return [v for v in self.model_variants.get(model_type, []) 
                if v.traffic_percentage > 0]
    
    async def predict_ensemble(self, 
                              model_type: str, 
                              features: np.ndarray,
                              ensemble_method: str = 'weighted_voting') -> Dict[str, Any]:
        """Make ensemble prediction"""
        active_models = self.get_active_models(model_type)
        
        if not active_models:
            return {"error": f"No active models for {model_type}"}
        
        ensemble = self.ensemble_methods[ensemble_method]
        return ensemble.predict(active_models, features)
    
    async def start_ab_test(self,
                           model_type: str,
                           challenger_variant: ModelVariant,
                           traffic_split: float = 0.1) -> str:
        """Start A/B test with current champion"""
        active_models = self.get_active_models(model_type)
        champion = next((m for m in active_models if m.is_champion), None)
        
        if not champion:
            raise ValueError(f"No champion model found for {model_type}")
        
        test_id = f"{model_type}_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
        
        await self.ab_test_manager.create_ab_test(
            test_id=test_id,
            champion_model=champion,
            challenger_model=challenger_variant,
            traffic_split=traffic_split
        )
        
        return test_id
    
    def promote_challenger(self, test_result: ABTestResult):
        """Promote challenger to champion if it won the A/B test"""
        if test_result.winner == test_result.challenger_variant:
            # Find the model variants
            for model_type, variants in self.model_variants.items():
                for variant in variants:
                    if variant.name == test_result.champion_variant:
                        variant.is_champion = False
                        variant.traffic_percentage = 0.0
                    elif variant.name == test_result.challenger_variant:
                        variant.is_champion = True
                        variant.traffic_percentage = 1.0
            
            print(f"🏆 Promoted {test_result.challenger_variant} to champion")
        else:
            print(f"🛡️ Champion {test_result.champion_variant} retained")

# Example usage and testing
async def main():
    # Create ensemble manager
    ensemble_manager = EnsembleModelManager()
    
    # Create model variants
    pm_model_v1 = PredictiveMaintenanceModel()
    pm_variant_v1 = ModelVariant(
        name="predictive_maintenance_v1",
        version="1.0",
        model=pm_model_v1,
        weight=0.6,
        performance_metrics={"accuracy": 0.92},
        deployment_date=datetime.now(),
        traffic_percentage=1.0,
        is_champion=True
    )
    
    pm_model_v2 = PredictiveMaintenanceModel()
    pm_variant_v2 = ModelVariant(
        name="predictive_maintenance_v2", 
        version="2.0",
        model=pm_model_v2,
        weight=0.4,
        performance_metrics={"accuracy": 0.94},
        deployment_date=datetime.now(),
        traffic_percentage=0.0
    )
    
    # Register variants
    ensemble_manager.register_model_variant("predictive_maintenance", pm_variant_v1)
    ensemble_manager.register_model_variant("predictive_maintenance", pm_variant_v2)
    
    # Test ensemble prediction
    features = np.array([0.1, 0.05, 0.06, 0.04, 5200, 95, 82])
    result = await ensemble_manager.predict_ensemble("predictive_maintenance", features)
    print("Ensemble prediction:", result)
    
    # Start A/B test
    test_id = await ensemble_manager.start_ab_test("predictive_maintenance", pm_variant_v2)
    print(f"Started A/B test: {test_id}")

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())