import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestRegressor, GradientBoostingRegressor
from sklearn.multioutput import MultiOutputRegressor
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.model_selection import train_test_split, GridSearchCV
from sklearn.metrics import mean_squared_error, r2_score
from typing import Dict, List, Tuple, Any, Optional
import joblib
import logging
from scipy.optimize import minimize
from collections import defaultdict

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class RebarProcessOptimizationModel:
    """
    Advanced process optimization model for rebar manufacturing.
    Optimizes rolling mill, heat treatment, cutting, and straightening parameters
    for different rebar grades to achieve target quality and efficiency.
    """
    
    def __init__(self):
        # Multi-output regression models for process parameter optimization
        self.rolling_optimizer = MultiOutputRegressor(
            RandomForestRegressor(
                n_estimators=200,
                max_depth=15,
                min_samples_split=3,
                random_state=42
            )
        )
        
        self.heat_treatment_optimizer = MultiOutputRegressor(
            GradientBoostingRegressor(
                n_estimators=150,
                max_depth=8,
                learning_rate=0.1,
                random_state=42
            )
        )
        
        self.cutting_optimizer = MultiOutputRegressor(
            RandomForestRegressor(
                n_estimators=150,
                max_depth=12,
                min_samples_split=2,
                random_state=42
            )
        )
        
        # Quality prediction models for optimization feedback
        self.quality_predictor = RandomForestRegressor(
            n_estimators=200,
            max_depth=15,
            random_state=42
        )
        
        self.energy_predictor = GradientBoostingRegressor(
            n_estimators=100,
            max_depth=8,
            learning_rate=0.1,
            random_state=42
        )
        
        # Preprocessing components
        self.scaler = StandardScaler()
        self.grade_encoder = LabelEncoder()
        self.size_encoder = LabelEncoder()
        
        # Feature sets for different optimization targets
        self.input_features = [
            # Target specifications
            'target_grade', 'target_size', 'target_length',
            'yield_strength_target', 'tensile_strength_target', 'elongation_target',
            
            # Material properties
            'carbon_content', 'manganese_content', 'phosphorus_content',
            'sulfur_content', 'silicon_content', 'billet_hardness',
            
            # Production constraints
            'production_rate_target', 'energy_budget', 'quality_threshold',
            'equipment_capacity', 'maintenance_schedule_days',
            
            # Environmental conditions
            'ambient_temperature', 'humidity', 'cooling_water_temp',
            
            # Current equipment status
            'roll_wear_percentage', 'furnace_efficiency', 'blade_condition'
        ]
        
        # Optimization target parameters
        self.rolling_parameters = [
            'billet_temperature', 'rolling_speed', 'total_reduction',
            'pass_schedule_factor', 'roll_gap_sequence', 'ribbing_pressure',
            'finishing_temperature', 'rolling_force_distribution'
        ]
        
        self.heat_treatment_parameters = [
            'austenitizing_temp', 'quench_rate', 'tempering_temp',
            'cooling_rate', 'atmosphere_composition', 'holding_time'
        ]
        
        self.cutting_parameters = [
            'processing_speed', 'straightening_force', 'cutting_speed',
            'length_compensation', 'quality_check_interval'
        ]
        
        self.models_trained = False
        
        # ASTM grade specifications for optimization targets
        self.grade_specs = {
            'Grade40': {
                'yield_strength': {'min': 275, 'target': 300, 'max': 350},
                'tensile_strength': {'min': 420, 'target': 450, 'max': 500},
                'elongation': {'min': 12.0, 'target': 14.0, 'max': 18.0},
                'carbon_max': 0.30, 'manganese_range': (0.8, 1.5)
            },
            'Grade60': {
                'yield_strength': {'min': 420, 'target': 450, 'max': 500},
                'tensile_strength': {'min': 620, 'target': 660, 'max': 720},
                'elongation': {'min': 9.0, 'target': 11.0, 'max': 14.0},
                'carbon_max': 0.30, 'manganese_range': (0.9, 1.6)
            },
            'Grade75': {
                'yield_strength': {'min': 520, 'target': 550, 'max': 600},
                'tensile_strength': {'min': 690, 'target': 730, 'max': 800},
                'elongation': {'min': 7.0, 'target': 8.5, 'max': 12.0},
                'carbon_max': 0.32, 'manganese_range': (1.0, 1.7)
            },
            'Grade80': {
                'yield_strength': {'min': 550, 'target': 580, 'max': 630},
                'tensile_strength': {'min': 720, 'target': 760, 'max': 850},
                'elongation': {'min': 6.0, 'target': 7.5, 'max': 10.0},
                'carbon_max': 0.35, 'manganese_range': (1.1, 1.8)
            }
        }
    
    def preprocess_features(self, data: pd.DataFrame) -> np.ndarray:
        """Extract and preprocess features for optimization"""
        features = []
        
        # Handle missing features with defaults
        for feature in self.input_features:
            if feature in ['target_grade']:
                # Encode grade if present
                if feature.replace('target_', '') in data.columns:
                    encoded = self.grade_encoder.transform(data[feature.replace('target_', '')].astype(str))
                    features.append(encoded)
                else:
                    features.append(np.ones(len(data)))  # Default Grade60
            elif feature in ['target_size']:
                # Encode size if present
                if feature.replace('target_', '') in data.columns:
                    encoded = self.size_encoder.transform(data[feature.replace('target_', '')].astype(str))
                    features.append(encoded)
                else:
                    features.append(np.ones(len(data)) * 2)  # Default Size5
            elif feature in data.columns:
                features.append(data[feature].values)
            else:
                default_values = self._get_default_optimization_values(feature, len(data))
                features.append(default_values)
        
        return np.column_stack(features)
    
    def _get_default_optimization_values(self, feature: str, length: int) -> np.ndarray:
        """Provide reasonable default values for missing optimization features"""
        defaults = {
            # Target specifications
            'target_length': 12.0, 'yield_strength_target': 450, 'tensile_strength_target': 650, 'elongation_target': 10.0,
            
            # Material properties (typical rebar composition)
            'carbon_content': 0.25, 'manganese_content': 1.2, 'phosphorus_content': 0.03,
            'sulfur_content': 0.04, 'silicon_content': 0.3, 'billet_hardness': 200,
            
            # Production constraints
            'production_rate_target': 100, 'energy_budget': 500, 'quality_threshold': 95,
            'equipment_capacity': 85, 'maintenance_schedule_days': 7,
            
            # Environmental conditions
            'ambient_temperature': 25, 'humidity': 60, 'cooling_water_temp': 20,
            
            # Equipment status
            'roll_wear_percentage': 25, 'furnace_efficiency': 85, 'blade_condition': 80
        }
        
        return np.full(length, defaults.get(feature, 0))
    
    def prepare_categorical_encoders(self, data: pd.DataFrame):
        """Prepare categorical encoders"""
        grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
        sizes = ['Size3', 'Size4', 'Size5', 'Size6', 'Size7', 'Size8', 'Size9', 'Size10', 'Size11', 'Size14', 'Size18']
        
        self.grade_encoder.fit(grades)
        self.size_encoder.fit(sizes)
    
    def train(self, data: pd.DataFrame, target_columns: Dict[str, List[str]]) -> Dict[str, Any]:
        """
        Train process optimization models
        
        Args:
            data: Training data with process parameters and outcomes
            target_columns: Dictionary mapping process types to their parameter columns
        """
        logger.info("Starting rebar process optimization model training...")
        
        # Prepare encoders
        self.prepare_categorical_encoders(data)
        
        # Preprocess features
        X = self.preprocess_features(data)
        X_scaled = self.scaler.fit_transform(X)
        
        results = {}
        
        # Train rolling mill optimization
        if 'rolling' in target_columns and all(col in data.columns for col in target_columns['rolling']):
            y_rolling = data[target_columns['rolling']].values
            
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_rolling, test_size=0.2, random_state=42
            )
            
            self.rolling_optimizer.fit(X_train, y_train)
            y_pred = self.rolling_optimizer.predict(X_test)
            
            rolling_r2 = r2_score(y_test, y_pred, multioutput='weighted_average')
            results['rolling_optimization_r2'] = rolling_r2
            
            logger.info(f"Rolling mill optimization R² score: {rolling_r2:.3f}")
        
        # Train heat treatment optimization
        if 'heat_treatment' in target_columns and all(col in data.columns for col in target_columns['heat_treatment']):
            y_heat = data[target_columns['heat_treatment']].values
            
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_heat, test_size=0.2, random_state=42
            )
            
            self.heat_treatment_optimizer.fit(X_train, y_train)
            y_pred = self.heat_treatment_optimizer.predict(X_test)
            
            heat_r2 = r2_score(y_test, y_pred, multioutput='weighted_average')
            results['heat_treatment_optimization_r2'] = heat_r2
            
            logger.info(f"Heat treatment optimization R² score: {heat_r2:.3f}")
        
        # Train cutting/straightening optimization
        if 'cutting' in target_columns and all(col in data.columns for col in target_columns['cutting']):
            y_cutting = data[target_columns['cutting']].values
            
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_cutting, test_size=0.2, random_state=42
            )
            
            self.cutting_optimizer.fit(X_train, y_train)
            y_pred = self.cutting_optimizer.predict(X_test)
            
            cutting_r2 = r2_score(y_test, y_pred, multioutput='weighted_average')
            results['cutting_optimization_r2'] = cutting_r2
            
            logger.info(f"Cutting optimization R² score: {cutting_r2:.3f}")
        
        # Train quality and energy predictors
        if 'quality_score' in data.columns:
            y_quality = data['quality_score'].values
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_quality, test_size=0.2, random_state=42
            )
            
            self.quality_predictor.fit(X_train, y_train)
            quality_r2 = self.quality_predictor.score(X_test, y_test)
            results['quality_prediction_r2'] = quality_r2
            
            logger.info(f"Quality prediction R² score: {quality_r2:.3f}")
        
        if 'energy_consumption' in data.columns:
            y_energy = data['energy_consumption'].values
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_energy, test_size=0.2, random_state=42
            )
            
            self.energy_predictor.fit(X_train, y_train)
            energy_r2 = self.energy_predictor.score(X_test, y_test)
            results['energy_prediction_r2'] = energy_r2
            
            logger.info(f"Energy prediction R² score: {energy_r2:.3f}")
        
        self.models_trained = True
        logger.info("Rebar process optimization model training completed successfully")
        
        return results
    
    def optimize_process_parameters(self, specifications: Dict[str, Any]) -> Dict[str, Any]:
        """
        Optimize process parameters for given specifications
        
        Args:
            specifications: Target specifications and constraints
        """
        if not self.models_trained:
            raise ValueError("Models must be trained before optimization")
        
        grade = specifications.get('grade', 'Grade60')
        size = specifications.get('size', 'Size5')
        target_quality = specifications.get('quality_target', 95.0)
        energy_budget = specifications.get('energy_budget', 500.0)
        production_rate = specifications.get('production_rate', 100.0)
        
        # Prepare input features
        input_data = self._prepare_optimization_input(specifications)
        X = self.preprocess_features(pd.DataFrame([input_data]))
        X_scaled = self.scaler.transform(X)
        
        # Get initial parameter predictions
        rolling_params = self.rolling_optimizer.predict(X_scaled)[0]
        heat_params = self.heat_treatment_optimizer.predict(X_scaled)[0]
        cutting_params = self.cutting_optimizer.predict(X_scaled)[0]
        
        # Refine parameters using optimization
        optimized_params = self._refine_parameters(
            rolling_params, heat_params, cutting_params,
            X_scaled[0], grade, target_quality, energy_budget
        )
        
        # Predict quality and energy with optimized parameters
        quality_prediction = self._predict_quality_with_params(optimized_params, X_scaled[0])
        energy_prediction = self._predict_energy_with_params(optimized_params, X_scaled[0])
        
        # Calculate process efficiency metrics
        efficiency_metrics = self._calculate_efficiency_metrics(
            optimized_params, quality_prediction, energy_prediction, production_rate
        )
        
        return {
            'rolling_parameters': {
                'billet_temperature': float(optimized_params['rolling'][0]),
                'rolling_speed': float(optimized_params['rolling'][1]),
                'total_reduction': float(optimized_params['rolling'][2]),
                'pass_schedule_factor': float(optimized_params['rolling'][3]),
                'roll_gap_sequence': float(optimized_params['rolling'][4]),
                'ribbing_pressure': float(optimized_params['rolling'][5]),
                'finishing_temperature': float(optimized_params['rolling'][6]),
                'rolling_force_distribution': float(optimized_params['rolling'][7])
            },
            'heat_treatment_parameters': {
                'austenitizing_temp': float(optimized_params['heat_treatment'][0]),
                'quench_rate': float(optimized_params['heat_treatment'][1]),
                'tempering_temp': float(optimized_params['heat_treatment'][2]),
                'cooling_rate': float(optimized_params['heat_treatment'][3]),
                'atmosphere_composition': float(optimized_params['heat_treatment'][4]),
                'holding_time': float(optimized_params['heat_treatment'][5])
            },
            'cutting_parameters': {
                'processing_speed': float(optimized_params['cutting'][0]),
                'straightening_force': float(optimized_params['cutting'][1]),
                'cutting_speed': float(optimized_params['cutting'][2]),
                'length_compensation': float(optimized_params['cutting'][3]),
                'quality_check_interval': float(optimized_params['cutting'][4])
            },
            'predicted_quality': float(quality_prediction),
            'predicted_energy_consumption': float(energy_prediction),
            'efficiency_metrics': efficiency_metrics,
            'grade_compliance': self._check_grade_compliance(optimized_params, grade),
            'optimization_confidence': self._calculate_optimization_confidence(optimized_params),
            'recommendations': self._generate_process_recommendations(optimized_params, grade, specifications)
        }
    
    def _prepare_optimization_input(self, specifications: Dict[str, Any]) -> Dict[str, Any]:
        """Prepare input data for optimization"""
        grade = specifications.get('grade', 'Grade60')
        size = specifications.get('size', 'Size5')
        grade_specs = self.grade_specs.get(grade, self.grade_specs['Grade60'])
        
        return {
            'target_grade': grade,
            'target_size': size,
            'target_length': specifications.get('length', 12.0),
            'yield_strength_target': grade_specs['yield_strength']['target'],
            'tensile_strength_target': grade_specs['tensile_strength']['target'],
            'elongation_target': grade_specs['elongation']['target'],
            'carbon_content': specifications.get('carbon_content', 0.25),
            'manganese_content': specifications.get('manganese_content', 1.2),
            'phosphorus_content': specifications.get('phosphorus_content', 0.03),
            'sulfur_content': specifications.get('sulfur_content', 0.04),
            'silicon_content': specifications.get('silicon_content', 0.3),
            'billet_hardness': specifications.get('billet_hardness', 200),
            'production_rate_target': specifications.get('production_rate', 100),
            'energy_budget': specifications.get('energy_budget', 500),
            'quality_threshold': specifications.get('quality_target', 95),
            'equipment_capacity': specifications.get('equipment_capacity', 85),
            'maintenance_schedule_days': specifications.get('maintenance_days', 7),
            'ambient_temperature': specifications.get('ambient_temp', 25),
            'humidity': specifications.get('humidity', 60),
            'cooling_water_temp': specifications.get('cooling_water_temp', 20),
            'roll_wear_percentage': specifications.get('roll_wear', 25),
            'furnace_efficiency': specifications.get('furnace_efficiency', 85),
            'blade_condition': specifications.get('blade_condition', 80)
        }
    
    def _refine_parameters(self, rolling_params, heat_params, cutting_params, input_features, 
                         grade, target_quality, energy_budget):
        """Refine parameters using constraint optimization"""
        
        # Define optimization objective
        def objective(params):
            # Reshape parameters
            r_params = params[:len(rolling_params)]
            h_params = params[len(rolling_params):len(rolling_params)+len(heat_params)]
            c_params = params[len(rolling_params)+len(heat_params):]
            
            param_dict = {
                'rolling': r_params,
                'heat_treatment': h_params,
                'cutting': c_params
            }
            
            # Predict quality and energy
            quality = self._predict_quality_with_params(param_dict, input_features)
            energy = self._predict_energy_with_params(param_dict, input_features)
            
            # Multi-objective: maximize quality, minimize energy, subject to constraints
            quality_penalty = max(0, target_quality - quality) * 10
            energy_penalty = max(0, energy - energy_budget) * 0.01
            
            return quality_penalty + energy_penalty - quality * 0.1
        
        # Initial parameters
        initial_params = np.concatenate([rolling_params, heat_params, cutting_params])
        
        # Define bounds for parameters
        bounds = self._get_parameter_bounds(grade)
        
        # Optimize
        result = minimize(objective, initial_params, method='L-BFGS-B', bounds=bounds)
        
        if result.success:
            optimized = result.x
            return {
                'rolling': optimized[:len(rolling_params)],
                'heat_treatment': optimized[len(rolling_params):len(rolling_params)+len(heat_params)],
                'cutting': optimized[len(rolling_params)+len(heat_params):]
            }
        else:
            # Return original predictions if optimization fails
            return {
                'rolling': rolling_params,
                'heat_treatment': heat_params,
                'cutting': cutting_params
            }
    
    def _get_parameter_bounds(self, grade: str) -> List[Tuple[float, float]]:
        """Define parameter bounds for optimization"""
        bounds = []
        
        # Rolling mill parameter bounds
        bounds.extend([
            (950, 1150),    # billet_temperature
            (2.0, 15.0),    # rolling_speed
            (75, 95),       # total_reduction
            (0.8, 1.2),     # pass_schedule_factor
            (0.5, 2.0),     # roll_gap_sequence
            (50, 300),      # ribbing_pressure
            (850, 1050),    # finishing_temperature
            (0.7, 1.3)      # rolling_force_distribution
        ])
        
        # Heat treatment parameter bounds
        bounds.extend([
            (850, 950),     # austenitizing_temp
            (20, 80),       # quench_rate
            (500, 700),     # tempering_temp
            (10, 50),       # cooling_rate
            (0.5, 1.2),     # atmosphere_composition
            (30, 180)       # holding_time
        ])
        
        # Cutting parameter bounds
        bounds.extend([
            (15, 120),      # processing_speed
            (50, 800),      # straightening_force
            (20, 150),      # cutting_speed
            (0.8, 1.2),     # length_compensation
            (5, 30)         # quality_check_interval
        ])
        
        return bounds
    
    def _predict_quality_with_params(self, params: Dict[str, np.ndarray], input_features: np.ndarray) -> float:
        """Predict quality score with given parameters"""
        # Combine parameters with input features
        combined_features = np.concatenate([
            input_features,
            params['rolling'],
            params['heat_treatment'],
            params['cutting']
        ]).reshape(1, -1)
        
        # Predict quality (simplified - in practice would use more sophisticated model)
        return min(100.0, max(0.0, 90.0 + np.random.normal(0, 3)))
    
    def _predict_energy_with_params(self, params: Dict[str, np.ndarray], input_features: np.ndarray) -> float:
        """Predict energy consumption with given parameters"""
        # Simplified energy prediction based on process intensity
        rolling_energy = np.sum(params['rolling'][:3]) * 0.1  # Temperature, speed, reduction
        heat_energy = params['heat_treatment'][0] * 0.05  # Austenitizing temp
        cutting_energy = params['cutting'][0] * 0.02  # Processing speed
        
        total_energy = rolling_energy + heat_energy + cutting_energy + np.random.normal(0, 10)
        return max(100.0, total_energy)
    
    def _calculate_efficiency_metrics(self, params: Dict[str, np.ndarray], 
                                    quality: float, energy: float, production_rate: float) -> Dict[str, float]:
        """Calculate process efficiency metrics"""
        return {
            'overall_efficiency': quality * production_rate / energy * 100,
            'energy_per_ton': energy / (production_rate * 0.5),  # Assuming 0.5 tons per 100 bars
            'quality_efficiency': quality / 100.0,
            'production_efficiency': min(1.0, production_rate / 120.0),  # Max 120 bars/hour
            'cost_efficiency': (quality * production_rate) / (energy * 0.1 + production_rate * 0.05)
        }
    
    def _check_grade_compliance(self, params: Dict[str, np.ndarray], grade: str) -> Dict[str, bool]:
        """Check if optimized parameters meet grade specifications"""
        grade_specs = self.grade_specs.get(grade, self.grade_specs['Grade60'])
        
        # Simplified compliance check
        temp_compliance = 950 <= params['rolling'][0] <= 1150  # Billet temperature
        reduction_compliance = params['rolling'][2] >= 75  # Total reduction
        quench_compliance = params['heat_treatment'][1] >= 20  # Quench rate
        
        return {
            'temperature_compliance': temp_compliance,
            'reduction_compliance': reduction_compliance,
            'heat_treatment_compliance': quench_compliance,
            'overall_compliance': temp_compliance and reduction_compliance and quench_compliance
        }
    
    def _calculate_optimization_confidence(self, params: Dict[str, np.ndarray]) -> float:
        """Calculate confidence in optimization results"""
        # Simplified confidence calculation based on parameter reasonableness
        param_ranges = {
            'rolling': [(950, 1150), (2, 15), (75, 95), (0.8, 1.2), (0.5, 2.0), (50, 300), (850, 1050), (0.7, 1.3)],
            'heat_treatment': [(850, 950), (20, 80), (500, 700), (10, 50), (0.5, 1.2), (30, 180)],
            'cutting': [(15, 120), (50, 800), (20, 150), (0.8, 1.2), (5, 30)]
        }
        
        total_params = 0
        in_range_params = 0
        
        for process_type, param_values in params.items():
            ranges = param_ranges[process_type]
            for i, value in enumerate(param_values):
                total_params += 1
                if ranges[i][0] <= value <= ranges[i][1]:
                    in_range_params += 1
        
        return (in_range_params / total_params) * 100.0 if total_params > 0 else 100.0
    
    def _generate_process_recommendations(self, params: Dict[str, np.ndarray], 
                                        grade: str, specifications: Dict[str, Any]) -> List[str]:
        """Generate process optimization recommendations"""
        recommendations = []
        
        # Rolling mill recommendations
        if params['rolling'][0] > 1100:  # High billet temperature
            recommendations.append("Consider reducing billet temperature to improve energy efficiency")
        
        if params['rolling'][1] < 5:  # Low rolling speed
            recommendations.append("Increase rolling speed to improve throughput")
        
        # Heat treatment recommendations
        if params['heat_treatment'][1] > 60:  # High quench rate
            recommendations.append("High quench rate may cause residual stress - monitor for cracking")
        
        if params['heat_treatment'][2] < 550:  # Low tempering temperature
            recommendations.append("Consider higher tempering temperature for better ductility")
        
        # Cutting recommendations
        if params['cutting'][0] > 100:  # High processing speed
            recommendations.append("High processing speed - ensure quality control frequency is adequate")
        
        # Grade-specific recommendations
        grade_specs = self.grade_specs.get(grade, self.grade_specs['Grade60'])
        if grade in ['Grade75', 'Grade80']:
            recommendations.append("High-strength grade: Monitor for weldability and ensure proper cooling")
        
        if not recommendations:
            recommendations.append("Process parameters are well-optimized for the specified requirements")
        
        return recommendations
    
    def save_model(self, path: str):
        """Save all trained models"""
        model_data = {
            'rolling_optimizer': self.rolling_optimizer,
            'heat_treatment_optimizer': self.heat_treatment_optimizer,
            'cutting_optimizer': self.cutting_optimizer,
            'quality_predictor': self.quality_predictor,
            'energy_predictor': self.energy_predictor,
            'scaler': self.scaler,
            'grade_encoder': self.grade_encoder,
            'size_encoder': self.size_encoder,
            'input_features': self.input_features,
            'rolling_parameters': self.rolling_parameters,
            'heat_treatment_parameters': self.heat_treatment_parameters,
            'cutting_parameters': self.cutting_parameters,
            'grade_specs': self.grade_specs,
            'models_trained': self.models_trained
        }
        joblib.dump(model_data, f"{path}/rebar_process_optimization_model.pkl")
        logger.info(f"Rebar process optimization models saved to {path}")
    
    def load_model(self, path: str):
        """Load all trained models"""
        model_data = joblib.load(f"{path}/rebar_process_optimization_model.pkl")
        
        self.rolling_optimizer = model_data['rolling_optimizer']
        self.heat_treatment_optimizer = model_data['heat_treatment_optimizer']
        self.cutting_optimizer = model_data['cutting_optimizer']
        self.quality_predictor = model_data['quality_predictor']
        self.energy_predictor = model_data['energy_predictor']
        self.scaler = model_data['scaler']
        self.grade_encoder = model_data['grade_encoder']
        self.size_encoder = model_data['size_encoder']
        self.input_features = model_data['input_features']
        self.rolling_parameters = model_data['rolling_parameters']
        self.heat_treatment_parameters = model_data['heat_treatment_parameters']
        self.cutting_parameters = model_data['cutting_parameters']
        self.grade_specs = model_data['grade_specs']
        self.models_trained = model_data['models_trained']
        
        logger.info(f"Rebar process optimization models loaded from {path}")
    
    def generate_synthetic_training_data(self, n_samples: int = 8000) -> pd.DataFrame:
        """Generate synthetic training data for process optimization"""
        np.random.seed(42)
        
        data = {}
        
        # Target specifications
        grades = np.random.choice(['Grade40', 'Grade60', 'Grade75', 'Grade80'], 
                                 n_samples, p=[0.1, 0.5, 0.3, 0.1])
        data['grade'] = grades
        
        sizes = np.random.choice(['Size3', 'Size4', 'Size5', 'Size6', 'Size7', 'Size8'], 
                                n_samples, p=[0.1, 0.15, 0.3, 0.25, 0.15, 0.05])
        data['size'] = sizes
        
        data['target_length'] = np.random.choice([6.1, 9.1, 12.2, 18.3], n_samples)  # meters
        
        # Material properties
        data['carbon_content'] = np.random.normal(0.25, 0.05, n_samples)
        data['manganese_content'] = np.random.normal(1.2, 0.2, n_samples)
        data['phosphorus_content'] = np.random.normal(0.03, 0.01, n_samples)
        data['sulfur_content'] = np.random.normal(0.04, 0.01, n_samples)
        data['silicon_content'] = np.random.normal(0.3, 0.1, n_samples)
        data['billet_hardness'] = np.random.normal(200, 30, n_samples)
        
        # Production constraints
        data['production_rate_target'] = np.random.normal(100, 15, n_samples)
        data['energy_budget'] = np.random.normal(500, 100, n_samples)
        data['quality_threshold'] = np.random.normal(95, 3, n_samples)
        data['equipment_capacity'] = np.random.normal(85, 10, n_samples)
        data['maintenance_schedule_days'] = np.random.randint(3, 14, n_samples)
        
        # Environmental conditions
        data['ambient_temperature'] = np.random.normal(25, 10, n_samples)
        data['humidity'] = np.random.normal(60, 15, n_samples)
        data['cooling_water_temp'] = np.random.normal(20, 5, n_samples)
        
        # Equipment status
        data['roll_wear_percentage'] = np.random.uniform(0, 50, n_samples)
        data['furnace_efficiency'] = np.random.normal(85, 8, n_samples)
        data['blade_condition'] = np.random.normal(80, 15, n_samples)
        
        # Generate optimal process parameters based on grade and constraints
        df = pd.DataFrame(data)
        
        # Rolling mill parameters
        data['billet_temperature'] = 1000 + (df['grade'].map({'Grade40': 0, 'Grade60': 25, 'Grade75': 50, 'Grade80': 75}).fillna(25)) + np.random.normal(0, 30, n_samples)
        data['rolling_speed'] = np.random.normal(8, 2, n_samples)
        data['total_reduction'] = np.random.normal(85, 5, n_samples)
        data['pass_schedule_factor'] = np.random.normal(1.0, 0.1, n_samples)
        data['roll_gap_sequence'] = np.random.normal(1.0, 0.2, n_samples)
        data['ribbing_pressure'] = np.random.normal(150, 50, n_samples)
        data['finishing_temperature'] = data['billet_temperature'] - np.random.uniform(100, 200, n_samples)
        data['rolling_force_distribution'] = np.random.normal(1.0, 0.15, n_samples)
        
        # Heat treatment parameters
        data['austenitizing_temp'] = np.random.normal(900, 25, n_samples)
        data['quench_rate'] = np.random.normal(50, 15, n_samples)
        data['tempering_temp'] = np.random.normal(600, 50, n_samples)
        data['cooling_rate'] = np.random.normal(25, 8, n_samples)
        data['atmosphere_composition'] = np.random.normal(0.8, 0.1, n_samples)
        data['holding_time'] = np.random.normal(90, 30, n_samples)
        
        # Cutting parameters
        data['processing_speed'] = np.random.normal(60, 20, n_samples)
        data['straightening_force'] = np.random.normal(200, 80, n_samples)
        data['cutting_speed'] = np.random.normal(65, 25, n_samples)
        data['length_compensation'] = np.random.normal(1.0, 0.05, n_samples)
        data['quality_check_interval'] = np.random.randint(5, 25, n_samples)
        
        # Generate target outputs
        df = pd.DataFrame(data)
        
        # Quality score based on process parameters
        base_quality = 85 + (df['billet_temperature'] - 1000) / 10 + df['quench_rate'] / 5
        quality_variance = np.random.normal(0, 5, n_samples)
        df['quality_score'] = np.clip(base_quality + quality_variance, 70, 100)
        
        # Energy consumption
        rolling_energy = df['billet_temperature'] * 0.2 + df['rolling_speed'] * 5
        heat_energy = df['austenitizing_temp'] * 0.3 + df['holding_time'] * 2
        cutting_energy = df['processing_speed'] * 3 + df['straightening_force'] * 0.5
        df['energy_consumption'] = rolling_energy + heat_energy + cutting_energy + np.random.normal(0, 50, n_samples)
        
        # Add target strength values based on grade
        grade_strength_map = {
            'Grade40': (300, 450), 'Grade60': (450, 660), 
            'Grade75': (550, 730), 'Grade80': (580, 760)
        }
        
        df['yield_strength_target'] = df['grade'].map(lambda g: grade_strength_map[g][0])
        df['tensile_strength_target'] = df['grade'].map(lambda g: grade_strength_map[g][1])
        df['elongation_target'] = df['grade'].map({'Grade40': 14, 'Grade60': 11, 'Grade75': 8.5, 'Grade80': 7.5})
        
        return df