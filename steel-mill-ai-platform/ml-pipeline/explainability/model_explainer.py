#!/usr/bin/env python3
"""
Model Explainability Service for Steel Mill AI Platform
SHAP and LIME integration for interpretable AI
"""
import numpy as np
import pandas as pd
from typing import Dict, List, Any, Optional, Union, Tuple
import json
import warnings
warnings.filterwarnings("ignore")

try:
    import shap
    SHAP_AVAILABLE = True
except ImportError:
    SHAP_AVAILABLE = False
    print("⚠️ SHAP not available. Install with: pip install shap")

try:
    import lime
    import lime.lime_tabular
    LIME_AVAILABLE = True
except ImportError:
    LIME_AVAILABLE = False
    print("⚠️ LIME not available. Install with: pip install lime")

import matplotlib.pyplot as plt
import seaborn as sns
from io import BytesIO
import base64

# Import our models
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from models.predictive_maintenance import PredictiveMaintenanceModel
from models.anomaly_detection import AnomalyDetectionModel  
from models.energy_optimization import EnergyOptimizationModel

class ModelExplainer:
    """Explainability service for steel mill ML models"""
    
    def __init__(self, model, model_type: str, feature_names: List[str]):
        self.model = model
        self.model_type = model_type
        self.feature_names = feature_names
        
        # Initialize SHAP explainer
        self.shap_explainer = None
        self.lime_explainer = None
        
        # Background dataset for SHAP (will be set during training)
        self.background_data = None
        
        # Feature metadata for better explanations
        self.feature_metadata = self._get_feature_metadata()
        
        # Initialize explainers
        self._initialize_explainers()
    
    def _get_feature_metadata(self) -> Dict[str, Dict[str, str]]:
        """Get metadata for features to provide better explanations"""
        metadata = {
            'vibration': {
                'description': 'Equipment vibration level',
                'unit': 'g (gravity)',
                'normal_range': '0.01-0.15',
                'high_risk_threshold': '>0.2'
            },
            'vibrationX': {
                'description': 'X-axis vibration component', 
                'unit': 'g (gravity)',
                'normal_range': '0.05-0.15',
                'high_risk_threshold': '>0.25'
            },
            'vibrationY': {
                'description': 'Y-axis vibration component',
                'unit': 'g (gravity)', 
                'normal_range': '0.05-0.15',
                'high_risk_threshold': '>0.25'
            },
            'vibrationZ': {
                'description': 'Z-axis vibration component',
                'unit': 'g (gravity)',
                'normal_range': '0.05-0.20',
                'high_risk_threshold': '>0.30'
            },
            'motorTorque': {
                'description': 'Motor torque measurement',
                'unit': 'Nm (Newton-meters)',
                'normal_range': '4000-6000',
                'high_risk_threshold': '>6500'
            },
            'motorCurrent': {
                'description': 'Motor current draw',
                'unit': 'A (Amperes)',
                'normal_range': '80-120',
                'high_risk_threshold': '>140'
            },
            'temperature': {
                'description': 'Equipment temperature',
                'unit': '°C (Celsius)',
                'normal_range': '70-90',
                'high_risk_threshold': '>100'
            },
            'powerDraw': {
                'description': 'Electrical power consumption',
                'unit': 'kW (Kilowatts)',
                'normal_range': '2000-3000',
                'high_risk_threshold': '>3500'
            },
            'speed': {
                'description': 'Equipment operational speed',
                'unit': 'm/s (meters per second)',
                'normal_range': '5-15',
                'high_risk_threshold': '>18'
            },
            'pressure': {
                'description': 'System pressure',
                'unit': 'bar (pressure)',
                'normal_range': '2.0-3.0',
                'high_risk_threshold': '>3.5'
            },
            'fuelFlow': {
                'description': 'Fuel consumption rate',
                'unit': 'L/min (Liters per minute)',
                'normal_range': '100-200',
                'high_risk_threshold': '>250'
            },
            'oxygenMix': {
                'description': 'Oxygen concentration',
                'unit': '% (percentage)',
                'normal_range': '20-23',
                'high_risk_threshold': '<18 or >25'
            }
        }
        return metadata
    
    def _initialize_explainers(self):
        """Initialize SHAP and LIME explainers"""
        if not SHAP_AVAILABLE and not LIME_AVAILABLE:
            print("⚠️ No explainability libraries available")
            return
        
        try:
            # For now, we'll initialize with dummy background data
            # In practice, this should be set with real training data
            self.background_data = np.random.random((100, len(self.feature_names)))
            
            if SHAP_AVAILABLE:
                if hasattr(self.model, 'model') and hasattr(self.model.model, 'predict'):
                    # For sklearn-based models
                    self.shap_explainer = shap.TreeExplainer(self.model.model)
                else:
                    # For custom models, use KernelExplainer
                    self.shap_explainer = shap.KernelExplainer(
                        self._model_predict_wrapper,
                        self.background_data
                    )
            
            if LIME_AVAILABLE:
                self.lime_explainer = lime.lime_tabular.LimeTabularExplainer(
                    self.background_data,
                    feature_names=self.feature_names,
                    class_names=['healthy', 'risk'] if self.model_type == 'predictive_maintenance' else None,
                    mode='classification' if self.model_type in ['predictive_maintenance', 'anomaly_detection'] else 'regression',
                    discretize_continuous=True
                )
                
        except Exception as e:
            print(f"⚠️ Error initializing explainers: {e}")
    
    def _model_predict_wrapper(self, X: np.ndarray) -> np.ndarray:
        """Wrapper function for SHAP that handles our custom model interface"""
        predictions = []
        
        for row in X:
            # Convert array to feature dict
            feature_dict = dict(zip(self.feature_names, row))
            
            try:
                if self.model_type == 'predictive_maintenance':
                    pred = self.model.predict_single(feature_dict)
                    # Convert to numeric for SHAP
                    predictions.append(pred.get('risk_score', 0.5))
                    
                elif self.model_type == 'anomaly_detection':
                    pred = self.model.detect_single(feature_dict)
                    predictions.append(pred.get('anomaly_score', 0.5))
                    
                elif self.model_type == 'energy_optimization':
                    feature_dict['timestamp'] = pd.Timestamp.now().isoformat()
                    pred = self.model.predict_single(feature_dict)
                    predictions.append(pred.get('predicted_energy_peak', 2500.0))
                    
                else:
                    predictions.append(0.5)  # Default
                    
            except Exception as e:
                predictions.append(0.5)  # Default on error
        
        return np.array(predictions)
    
    def set_background_data(self, background_data: np.ndarray):
        """Set background dataset for SHAP explanations"""
        self.background_data = background_data
        self._initialize_explainers()  # Reinitialize with new background data
    
    def explain_prediction(self, 
                          features: Union[Dict[str, float], np.ndarray],
                          explanation_type: str = 'shap') -> Dict[str, Any]:
        """
        Explain a single prediction
        
        Args:
            features: Input features (dict or array)
            explanation_type: 'shap', 'lime', or 'both'
        """
        if isinstance(features, dict):
            feature_array = np.array([features.get(name, 0.0) for name in self.feature_names])
            feature_dict = features
        else:
            feature_array = features
            feature_dict = dict(zip(self.feature_names, features))
        
        explanation = {
            'input_features': feature_dict,
            'model_type': self.model_type,
            'explanations': {}
        }
        
        # Get base prediction
        try:
            if self.model_type == 'predictive_maintenance':
                base_prediction = self.model.predict_single(feature_dict)
            elif self.model_type == 'anomaly_detection':
                base_prediction = self.model.detect_single(feature_dict)
            elif self.model_type == 'energy_optimization':
                feature_dict['timestamp'] = pd.Timestamp.now().isoformat()
                base_prediction = self.model.predict_single(feature_dict)
            else:
                base_prediction = {'prediction': 'unknown'}
                
            explanation['prediction'] = base_prediction
        except Exception as e:
            explanation['prediction'] = {'error': str(e)}
            return explanation
        
        # SHAP explanations
        if explanation_type in ['shap', 'both'] and SHAP_AVAILABLE and self.shap_explainer:
            try:
                shap_explanation = self._get_shap_explanation(feature_array)
                explanation['explanations']['shap'] = shap_explanation
            except Exception as e:
                explanation['explanations']['shap'] = {'error': str(e)}
        
        # LIME explanations
        if explanation_type in ['lime', 'both'] and LIME_AVAILABLE and self.lime_explainer:
            try:
                lime_explanation = self._get_lime_explanation(feature_array)
                explanation['explanations']['lime'] = lime_explanation
            except Exception as e:
                explanation['explanations']['lime'] = {'error': str(e)}
        
        # Add feature analysis
        explanation['feature_analysis'] = self._analyze_features(feature_dict)
        
        return explanation
    
    def _get_shap_explanation(self, feature_array: np.ndarray) -> Dict[str, Any]:
        """Get SHAP explanation for the prediction"""
        if not self.shap_explainer:
            return {'error': 'SHAP explainer not available'}
        
        try:
            # Get SHAP values
            shap_values = self.shap_explainer.shap_values(feature_array.reshape(1, -1))
            
            # Handle different SHAP output formats
            if isinstance(shap_values, list):
                # Multi-class case - take first class
                shap_vals = shap_values[0][0] if len(shap_values[0].shape) > 1 else shap_values[0]
            else:
                shap_vals = shap_values[0] if len(shap_values.shape) > 1 else shap_values
            
            # Create feature importance ranking
            feature_importance = list(zip(self.feature_names, shap_vals))
            feature_importance.sort(key=lambda x: abs(x[1]), reverse=True)
            
            # Get base value (expected value)
            base_value = getattr(self.shap_explainer, 'expected_value', 0.0)
            if isinstance(base_value, np.ndarray):
                base_value = base_value[0]
            
            return {
                'feature_importance': [
                    {
                        'feature': name,
                        'shap_value': float(value),
                        'contribution': 'increases' if value > 0 else 'decreases',
                        'magnitude': abs(float(value))
                    }
                    for name, value in feature_importance
                ],
                'base_value': float(base_value),
                'prediction_value': float(base_value + sum(shap_vals)),
                'explanation_text': self._generate_shap_explanation_text(feature_importance, base_value)
            }
            
        except Exception as e:
            return {'error': f'SHAP calculation failed: {str(e)}'}
    
    def _get_lime_explanation(self, feature_array: np.ndarray) -> Dict[str, Any]:
        """Get LIME explanation for the prediction"""
        if not self.lime_explainer:
            return {'error': 'LIME explainer not available'}
        
        try:
            # Get LIME explanation
            lime_exp = self.lime_explainer.explain_instance(
                feature_array,
                self._model_predict_wrapper,
                num_features=len(self.feature_names),
                num_samples=500
            )
            
            # Extract explanation
            explanation_list = lime_exp.as_list()
            
            return {
                'feature_importance': [
                    {
                        'feature': self._extract_feature_name(feature_desc),
                        'lime_value': float(importance),
                        'contribution': 'increases' if importance > 0 else 'decreases',
                        'magnitude': abs(float(importance))
                    }
                    for feature_desc, importance in explanation_list
                ],
                'score': float(lime_exp.score),
                'explanation_text': self._generate_lime_explanation_text(explanation_list)
            }
            
        except Exception as e:
            return {'error': f'LIME calculation failed: {str(e)}'}
    
    def _extract_feature_name(self, feature_desc: str) -> str:
        """Extract clean feature name from LIME description"""
        # LIME sometimes returns descriptions like "temperature <= 85.5"
        for feature_name in self.feature_names:
            if feature_name in feature_desc:
                return feature_name
        return feature_desc.split()[0]  # Fallback
    
    def _analyze_features(self, feature_dict: Dict[str, float]) -> Dict[str, Any]:
        """Analyze individual features and flag anomalous values"""
        analysis = {
            'feature_status': {},
            'anomalous_features': [],
            'normal_features': [],
            'warnings': []
        }
        
        for feature_name, value in feature_dict.items():
            metadata = self.feature_metadata.get(feature_name, {})
            
            status = {
                'value': value,
                'description': metadata.get('description', 'Unknown feature'),
                'unit': metadata.get('unit', ''),
                'status': 'normal'
            }
            
            # Check if value is outside normal range
            normal_range = metadata.get('normal_range', '')
            if normal_range and '-' in normal_range:
                try:
                    min_val, max_val = map(float, normal_range.split('-'))
                    if value < min_val or value > max_val:
                        status['status'] = 'out_of_range'
                        analysis['anomalous_features'].append(feature_name)
                        analysis['warnings'].append(
                            f"{feature_name} ({value:.2f}) is outside normal range ({normal_range})"
                        )
                    else:
                        analysis['normal_features'].append(feature_name)
                except ValueError:
                    pass
            
            # Check high risk threshold
            high_risk = metadata.get('high_risk_threshold', '')
            if high_risk:
                try:
                    if high_risk.startswith('>'):
                        threshold = float(high_risk[1:])
                        if value > threshold:
                            status['status'] = 'high_risk'
                            analysis['warnings'].append(
                                f"{feature_name} ({value:.2f}) exceeds high risk threshold ({high_risk})"
                            )
                    elif high_risk.startswith('<'):
                        threshold = float(high_risk[1:])
                        if value < threshold:
                            status['status'] = 'high_risk'
                            analysis['warnings'].append(
                                f"{feature_name} ({value:.2f}) below high risk threshold ({high_risk})"
                            )
                except ValueError:
                    pass
            
            analysis['feature_status'][feature_name] = status
        
        return analysis
    
    def _generate_shap_explanation_text(self, 
                                       feature_importance: List[Tuple[str, float]], 
                                       base_value: float) -> str:
        """Generate human-readable explanation from SHAP values"""
        top_features = feature_importance[:3]  # Top 3 most important
        
        explanation = f"The model's baseline prediction is {base_value:.3f}. "
        
        positive_contributors = [(name, val) for name, val in top_features if val > 0]
        negative_contributors = [(name, val) for name, val in top_features if val < 0]
        
        if positive_contributors:
            explanation += "Features that increase the prediction: "
            for name, val in positive_contributors:
                metadata = self.feature_metadata.get(name, {})
                desc = metadata.get('description', name)
                explanation += f"{desc} (+{val:.3f}), "
            explanation = explanation.rstrip(', ') + ". "
        
        if negative_contributors:
            explanation += "Features that decrease the prediction: "
            for name, val in negative_contributors:
                metadata = self.feature_metadata.get(name, {})
                desc = metadata.get('description', name)
                explanation += f"{desc} ({val:.3f}), "
            explanation = explanation.rstrip(', ') + ". "
        
        # Add the most influential feature
        if feature_importance:
            most_important = feature_importance[0]
            explanation += f"The most influential factor is {most_important[0]} with an impact of {most_important[1]:.3f}."
        
        return explanation
    
    def _generate_lime_explanation_text(self, explanation_list: List[Tuple[str, float]]) -> str:
        """Generate human-readable explanation from LIME values"""
        if not explanation_list:
            return "Unable to generate explanation."
        
        # Sort by importance
        sorted_explanations = sorted(explanation_list, key=lambda x: abs(x[1]), reverse=True)
        top_3 = sorted_explanations[:3]
        
        explanation = "Key factors in this prediction: "
        
        for feature_desc, importance in top_3:
            feature_name = self._extract_feature_name(feature_desc)
            metadata = self.feature_metadata.get(feature_name, {})
            desc = metadata.get('description', feature_name)
            
            if importance > 0:
                explanation += f"{desc} increases the likelihood ({importance:+.3f}), "
            else:
                explanation += f"{desc} decreases the likelihood ({importance:+.3f}), "
        
        return explanation.rstrip(', ') + "."
    
    def generate_explanation_visualization(self, 
                                         explanation: Dict[str, Any],
                                         chart_type: str = 'bar') -> str:
        """Generate visualization of the explanation (returns base64 encoded image)"""
        try:
            plt.figure(figsize=(10, 6))
            
            # Extract feature importance data
            shap_data = explanation['explanations'].get('shap')
            if shap_data and 'feature_importance' in shap_data:
                features = [item['feature'] for item in shap_data['feature_importance'][:10]]
                values = [item['shap_value'] for item in shap_data['feature_importance'][:10]]
                
                # Create horizontal bar plot
                colors = ['red' if v < 0 else 'blue' for v in values]
                plt.barh(features, values, color=colors, alpha=0.7)
                plt.xlabel('SHAP Value (Impact on Prediction)')
                plt.title(f'Feature Importance - {self.model_type.replace("_", " ").title()}')
                plt.grid(axis='x', alpha=0.3)
                
                # Add value labels on bars
                for i, v in enumerate(values):
                    plt.text(v, i, f'{v:.3f}', va='center', 
                            ha='left' if v >= 0 else 'right', fontsize=9)
                
                plt.tight_layout()
                
                # Convert to base64
                buffer = BytesIO()
                plt.savefig(buffer, format='png', dpi=150, bbox_inches='tight')
                buffer.seek(0)
                image_base64 = base64.b64encode(buffer.read()).decode()
                plt.close()
                
                return image_base64
            
        except Exception as e:
            print(f"Error generating visualization: {e}")
        
        return ""
    
    def batch_explain(self, 
                     feature_batch: List[Union[Dict[str, float], np.ndarray]],
                     explanation_type: str = 'shap') -> List[Dict[str, Any]]:
        """Explain multiple predictions efficiently"""
        explanations = []
        
        for features in feature_batch:
            explanation = self.explain_prediction(features, explanation_type)
            explanations.append(explanation)
        
        return explanations

# Factory function to create explainers for different model types
def create_explainer(model, model_type: str) -> ModelExplainer:
    """Factory function to create appropriate explainer"""
    
    # Define feature names based on model type
    if model_type == 'predictive_maintenance':
        feature_names = ['vibration', 'vibrationX', 'vibrationY', 'vibrationZ', 
                        'motorTorque', 'motorCurrent', 'temperature']
    elif model_type == 'anomaly_detection':
        feature_names = ['temperature', 'vibration', 'oxygenMix', 'fuelFlow', 
                        'powerDraw', 'speed', 'pressure']
    elif model_type == 'energy_optimization':
        feature_names = ['powerDraw', 'temperature', 'speed', 'pressure', 
                        'fuelFlow', 'motorTorque']
    else:
        feature_names = ['feature_' + str(i) for i in range(10)]  # Default
    
    return ModelExplainer(model, model_type, feature_names)

# Example usage and testing
def main():
    # Create a sample model and explainer
    model = PredictiveMaintenanceModel()
    explainer = create_explainer(model, 'predictive_maintenance')
    
    # Sample features
    features = {
        'vibration': 0.12,
        'vibrationX': 0.08,
        'vibrationY': 0.09,
        'vibrationZ': 0.15,
        'motorTorque': 5500,
        'motorCurrent': 110,
        'temperature': 95
    }
    
    # Get explanation
    explanation = explainer.explain_prediction(features, 'both')
    
    print("Explanation Results:")
    print(json.dumps(explanation, indent=2, default=str))
    
    # Generate visualization
    viz = explainer.generate_explanation_visualization(explanation)
    if viz:
        print(f"Visualization generated (base64 length: {len(viz)})")

if __name__ == "__main__":
    main()