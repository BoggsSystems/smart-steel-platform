import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier, GradientBoostingRegressor
from sklearn.preprocessing import StandardScaler
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, mean_squared_error, r2_score
from typing import Dict, List, Tuple, Any, Optional
import joblib
import logging
from datetime import datetime, timedelta

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class RebarPredictiveMaintenanceModel:
    """
    Advanced predictive maintenance model specifically designed for rebar manufacturing equipment.
    Predicts failure risks for rolling mills, heat treatment furnaces, cutting/straightening equipment,
    and quality control systems.
    """
    
    def __init__(self):
        # Equipment-specific failure prediction models
        self.rolling_mill_model = RandomForestClassifier(
            n_estimators=300,
            max_depth=15,
            min_samples_split=3,
            class_weight='balanced',
            random_state=42
        )
        
        self.heat_treatment_model = RandomForestClassifier(
            n_estimators=250,
            max_depth=12,
            min_samples_split=3,
            class_weight='balanced',
            random_state=42
        )
        
        self.cutting_straightening_model = RandomForestClassifier(
            n_estimators=200,
            max_depth=10,
            min_samples_split=3,
            class_weight='balanced',
            random_state=42
        )
        
        self.quality_control_model = RandomForestClassifier(
            n_estimators=150,
            max_depth=8,
            min_samples_split=3,
            class_weight='balanced',
            random_state=42
        )
        
        # Remaining useful life (RUL) regression models
        self.rul_rolling_mill = GradientBoostingRegressor(
            n_estimators=200,
            max_depth=8,
            learning_rate=0.1,
            random_state=42
        )
        
        self.rul_heat_treatment = GradientBoostingRegressor(
            n_estimators=200,
            max_depth=8,
            learning_rate=0.1,
            random_state=42
        )
        
        # Scalers for different equipment types
        self.rolling_scaler = StandardScaler()
        self.heat_treatment_scaler = StandardScaler()
        self.cutting_scaler = StandardScaler()
        self.qc_scaler = StandardScaler()
        
        # Equipment-specific feature sets
        self.rolling_mill_features = [
            # Mechanical features
            'motor_torque', 'motor_current', 'motor_temperature', 'motor_vibration_x',
            'motor_vibration_y', 'motor_vibration_z', 'bearing_temperature',
            
            # Roll-specific features
            'roll_gap', 'roll_pressure', 'roll_force', 'roll_temperature',
            'roll_wear_indicator', 'roll_surface_roughness', 'roll_hardness',
            
            # Process load features
            'processing_speed', 'material_hardness', 'reduction_ratio',
            'bars_processed_today', 'cumulative_tonnage', 'operating_hours',
            
            # Lubrication and cooling
            'lubricant_flow_rate', 'lubricant_temperature', 'cooling_water_flow',
            'cooling_water_temperature', 'hydraulic_pressure',
            
            # Power and efficiency
            'power_consumption', 'energy_per_bar', 'overall_efficiency',
            'power_factor', 'harmonic_distortion'
        ]
        
        self.heat_treatment_features = [
            # Temperature control
            'furnace_temperature', 'target_temperature', 'temperature_deviation',
            'heating_rate', 'cooling_rate', 'temperature_uniformity',
            
            # Atmosphere control
            'oxygen_level', 'carbon_potential', 'nitrogen_flow', 'atmosphere_pressure',
            'combustion_efficiency', 'excess_air_ratio',
            
            # Equipment condition
            'heating_element_resistance', 'heating_element_current', 'fan_speed',
            'fan_vibration', 'fan_current', 'thermocouple_drift',
            
            # Process load
            'furnace_load', 'batch_size', 'cycle_count', 'operating_hours',
            'energy_consumption', 'fuel_consumption',
            
            # Quench system
            'quench_pump_pressure', 'quench_flow_rate', 'quench_temperature',
            'pump_vibration', 'pump_current', 'system_pressure'
        ]
        
        self.cutting_straightening_features = [
            # Straightening system
            'straightening_force', 'roller_pressure', 'roller_positions',
            'roller_wear', 'straightening_motor_current', 'straightening_vibration',
            
            # Cutting system
            'cutting_force', 'blade_position', 'blade_wear', 'cutting_speed',
            'shear_motor_current', 'shear_vibration', 'blade_temperature',
            
            # Measurement systems
            'length_measurement_accuracy', 'diameter_measurement_accuracy',
            'weight_measurement_accuracy', 'measurement_system_drift',
            
            # Hydraulic system
            'hydraulic_pressure', 'hydraulic_flow', 'hydraulic_temperature',
            'pump_efficiency', 'filter_pressure_drop', 'oil_viscosity',
            
            # Conveyor system
            'conveyor_speed', 'conveyor_current', 'belt_tension', 'belt_wear',
            'guide_alignment', 'processing_cycles_today'
        ]
        
        self.quality_control_features = [
            # Testing equipment
            'tensile_machine_force_accuracy', 'tensile_machine_load_cell_drift',
            'bend_test_fixture_wear', 'dimensional_gauge_accuracy',
            'spectrometer_lamp_intensity', 'spectrometer_detector_noise',
            
            # Environmental conditions
            'lab_temperature', 'lab_humidity', 'vibration_isolation_effectiveness',
            'electrical_noise_level', 'air_pressure', 'dust_level',
            
            # Calibration status
            'days_since_calibration', 'calibration_drift', 'reference_standard_age',
            'measurement_repeatability', 'measurement_reproducibility',
            
            # Usage statistics
            'tests_performed_today', 'samples_processed', 'equipment_uptime',
            'operator_changes', 'maintenance_hours'
        ]
        
        self.models_trained = False
        
        # Failure type mappings
        self.failure_types = {
            'rolling_mill': [
                'bearing_failure', 'motor_failure', 'roll_wear_excessive', 
                'hydraulic_failure', 'gear_failure', 'coupling_failure'
            ],
            'heat_treatment': [
                'heating_element_failure', 'thermocouple_failure', 'fan_failure',
                'quench_pump_failure', 'atmosphere_control_failure', 'refractory_damage'
            ],
            'cutting_straightening': [
                'blade_failure', 'hydraulic_failure', 'motor_failure',
                'measurement_system_failure', 'straightening_roll_failure'
            ],
            'quality_control': [
                'load_cell_failure', 'calibration_drift', 'environmental_deviation',
                'equipment_wear', 'software_error'
            ]
        }
    
    def preprocess_equipment_features(self, data: pd.DataFrame, equipment_type: str) -> np.ndarray:
        """Preprocess features for specific equipment type"""
        if equipment_type == 'rolling_mill':
            features = self.rolling_mill_features
            scaler = self.rolling_scaler
        elif equipment_type == 'heat_treatment':
            features = self.heat_treatment_features
            scaler = self.heat_treatment_scaler
        elif equipment_type == 'cutting_straightening':
            features = self.cutting_straightening_features
            scaler = self.cutting_scaler
        elif equipment_type == 'quality_control':
            features = self.quality_control_features
            scaler = self.qc_scaler
        else:
            raise ValueError(f"Unknown equipment type: {equipment_type}")
        
        feature_array = []
        for feature in features:
            if feature in data.columns:
                feature_array.append(data[feature].values)
            else:
                # Provide equipment-specific defaults
                default_values = self._get_default_maintenance_values(feature, equipment_type, len(data))
                feature_array.append(default_values)
        
        return np.column_stack(feature_array)
    
    def _get_default_maintenance_values(self, feature: str, equipment_type: str, length: int) -> np.ndarray:
        """Provide reasonable default values for missing maintenance features"""
        
        # Rolling mill defaults
        rolling_defaults = {
            'motor_torque': 25000, 'motor_current': 150, 'motor_temperature': 65, 
            'motor_vibration_x': 2.5, 'motor_vibration_y': 2.5, 'motor_vibration_z': 2.5,
            'bearing_temperature': 70, 'roll_gap': 16, 'roll_pressure': 200,
            'roll_force': 3000, 'roll_temperature': 45, 'roll_wear_indicator': 25,
            'roll_surface_roughness': 3.2, 'roll_hardness': 65, 'processing_speed': 60,
            'material_hardness': 180, 'reduction_ratio': 15, 'bars_processed_today': 500,
            'cumulative_tonnage': 1000, 'operating_hours': 16, 'lubricant_flow_rate': 10,
            'lubricant_temperature': 40, 'cooling_water_flow': 50, 'cooling_water_temperature': 25,
            'hydraulic_pressure': 200, 'power_consumption': 450, 'energy_per_bar': 0.9,
            'overall_efficiency': 85, 'power_factor': 0.9, 'harmonic_distortion': 3
        }
        
        # Heat treatment defaults
        heat_treatment_defaults = {
            'furnace_temperature': 950, 'target_temperature': 950, 'temperature_deviation': 5,
            'heating_rate': 5, 'cooling_rate': 2, 'temperature_uniformity': 10,
            'oxygen_level': 0.1, 'carbon_potential': 0.8, 'nitrogen_flow': 50,
            'atmosphere_pressure': 1.02, 'combustion_efficiency': 85, 'excess_air_ratio': 1.1,
            'heating_element_resistance': 10, 'heating_element_current': 100, 'fan_speed': 1200,
            'fan_vibration': 3, 'fan_current': 25, 'thermocouple_drift': 2,
            'furnace_load': 70, 'batch_size': 5000, 'cycle_count': 3, 'operating_hours': 20,
            'energy_consumption': 2500, 'fuel_consumption': 150, 'quench_pump_pressure': 5,
            'quench_flow_rate': 100, 'quench_temperature': 25, 'pump_vibration': 2,
            'pump_current': 30, 'system_pressure': 4.5
        }
        
        # Cutting/straightening defaults
        cutting_defaults = {
            'straightening_force': 800, 'roller_pressure': 150, 'roller_positions': 5,
            'roller_wear': 20, 'straightening_motor_current': 80, 'straightening_vibration': 3,
            'cutting_force': 300, 'blade_position': 0, 'blade_wear': 15, 'cutting_speed': 120,
            'shear_motor_current': 50, 'shear_vibration': 2.5, 'blade_temperature': 35,
            'length_measurement_accuracy': 99, 'diameter_measurement_accuracy': 99.5,
            'weight_measurement_accuracy': 99.8, 'measurement_system_drift': 0.1,
            'hydraulic_pressure': 250, 'hydraulic_flow': 80, 'hydraulic_temperature': 45,
            'pump_efficiency': 92, 'filter_pressure_drop': 2, 'oil_viscosity': 46,
            'conveyor_speed': 60, 'conveyor_current': 15, 'belt_tension': 500,
            'belt_wear': 10, 'guide_alignment': 0.5, 'processing_cycles_today': 800
        }
        
        # Quality control defaults
        qc_defaults = {
            'tensile_machine_force_accuracy': 99.8, 'tensile_machine_load_cell_drift': 0.02,
            'bend_test_fixture_wear': 5, 'dimensional_gauge_accuracy': 99.9,
            'spectrometer_lamp_intensity': 95, 'spectrometer_detector_noise': 2,
            'lab_temperature': 23, 'lab_humidity': 45, 'vibration_isolation_effectiveness': 95,
            'electrical_noise_level': 1, 'air_pressure': 101.3, 'dust_level': 0.1,
            'days_since_calibration': 90, 'calibration_drift': 0.1, 'reference_standard_age': 365,
            'measurement_repeatability': 99.5, 'measurement_reproducibility': 99,
            'tests_performed_today': 50, 'samples_processed': 200, 'equipment_uptime': 95,
            'operator_changes': 2, 'maintenance_hours': 1
        }
        
        defaults_map = {
            'rolling_mill': rolling_defaults,
            'heat_treatment': heat_treatment_defaults,
            'cutting_straightening': cutting_defaults,
            'quality_control': qc_defaults
        }
        
        defaults = defaults_map.get(equipment_type, {})
        return np.full(length, defaults.get(feature, 0))
    
    def train(self, equipment_data: Dict[str, pd.DataFrame]) -> Dict[str, Any]:
        """
        Train predictive maintenance models for all equipment types
        
        Args:
            equipment_data: Dictionary with equipment type as key and training data as value
                          Each DataFrame should have features and 'failure_risk' or 'time_to_failure' columns
        """
        logger.info("Starting rebar equipment predictive maintenance model training...")
        
        results = {}
        
        # Train rolling mill model
        if 'rolling_mill' in equipment_data:
            rolling_results = self._train_equipment_model(
                equipment_data['rolling_mill'], 
                'rolling_mill', 
                self.rolling_mill_model,
                self.rul_rolling_mill
            )
            results['rolling_mill'] = rolling_results
        
        # Train heat treatment model
        if 'heat_treatment' in equipment_data:
            heat_results = self._train_equipment_model(
                equipment_data['heat_treatment'],
                'heat_treatment',
                self.heat_treatment_model,
                self.rul_heat_treatment
            )
            results['heat_treatment'] = heat_results
        
        # Train cutting/straightening model
        if 'cutting_straightening' in equipment_data:
            cutting_results = self._train_equipment_model(
                equipment_data['cutting_straightening'],
                'cutting_straightening',
                self.cutting_straightening_model,
                None
            )
            results['cutting_straightening'] = cutting_results
        
        # Train quality control model
        if 'quality_control' in equipment_data:
            qc_results = self._train_equipment_model(
                equipment_data['quality_control'],
                'quality_control',
                self.quality_control_model,
                None
            )
            results['quality_control'] = qc_results
        
        self.models_trained = True
        logger.info("Rebar equipment predictive maintenance training completed")
        
        return results
    
    def _train_equipment_model(self, data: pd.DataFrame, equipment_type: str, 
                             classifier_model, rul_model) -> Dict[str, Any]:
        """Train models for specific equipment type"""
        
        X = self.preprocess_equipment_features(data, equipment_type)
        
        # Get appropriate scaler
        scaler = getattr(self, f"{equipment_type.replace('_', '_')}_scaler" if '_' in equipment_type else f"{equipment_type}_scaler")
        X_scaled = scaler.fit_transform(X)
        
        results = {}
        
        # Train failure risk classifier
        if 'failure_risk' in data.columns:
            y_failure = data['failure_risk'].values
            
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_failure, test_size=0.2, random_state=42
            )
            
            classifier_model.fit(X_train, y_train)
            failure_score = classifier_model.score(X_test, y_test)
            
            results['failure_prediction_accuracy'] = failure_score
            
            # Feature importance
            feature_names = getattr(self, f"{equipment_type}_features")
            feature_importance = dict(zip(feature_names, classifier_model.feature_importances_))
            results['failure_feature_importance'] = feature_importance
            
            logger.info(f"{equipment_type} failure prediction accuracy: {failure_score:.3f}")
        
        # Train RUL regression model (if available)
        if rul_model and 'time_to_failure' in data.columns:
            y_rul = data['time_to_failure'].values
            
            # Remove samples with very high RUL (these are essentially non-failing)
            mask = y_rul <= 365  # Less than 1 year
            if mask.sum() > 100:  # Enough samples
                X_rul = X_scaled[mask]
                y_rul = y_rul[mask]
                
                X_train, X_test, y_train, y_test = train_test_split(
                    X_rul, y_rul, test_size=0.2, random_state=42
                )
                
                rul_model.fit(X_train, y_train)
                y_pred = rul_model.predict(X_test)
                
                rul_mse = mean_squared_error(y_test, y_pred)
                rul_r2 = r2_score(y_test, y_pred)
                
                results['rul_mse'] = rul_mse
                results['rul_r2'] = rul_r2
                
                logger.info(f"{equipment_type} RUL prediction R²: {rul_r2:.3f}")
        
        return results
    
    def predict_maintenance(self, equipment_data: Dict[str, Any], equipment_type: str) -> Dict[str, Any]:
        """Predict maintenance needs for specific equipment"""
        if not self.models_trained:
            raise ValueError("Models must be trained before prediction")
        
        df = pd.DataFrame([equipment_data])
        X = self.preprocess_equipment_features(df, equipment_type)
        
        # Get appropriate scaler and model
        scaler = getattr(self, f"{equipment_type.replace('_', '_')}_scaler" if '_' in equipment_type else f"{equipment_type}_scaler")
        classifier = getattr(self, f"{equipment_type}_model")
        
        X_scaled = scaler.transform(X)
        
        results = {}
        
        # Failure risk prediction
        failure_prob = classifier.predict_proba(X_scaled)[0, 1]  # Probability of failure
        failure_pred = classifier.predict(X_scaled)[0]
        
        results['failure_risk'] = bool(failure_pred)
        results['failure_probability'] = float(failure_prob)
        results['risk_level'] = self._get_risk_level(failure_prob)
        
        # RUL prediction (if available)
        rul_model = getattr(self, f"rul_{equipment_type}", None)
        if rul_model and hasattr(rul_model, 'predict'):
            try:
                rul_days = float(rul_model.predict(X_scaled)[0])
                results['remaining_useful_life_days'] = max(1, rul_days)  # At least 1 day
                results['maintenance_due_date'] = (datetime.now() + timedelta(days=rul_days)).isoformat()
            except Exception as e:
                logger.warning(f"RUL prediction failed for {equipment_type}: {e}")
                results['remaining_useful_life_days'] = None
        
        # Maintenance recommendations
        results['recommendations'] = self._get_maintenance_recommendations(
            equipment_type, failure_prob, equipment_data
        )
        
        # Component-specific analysis
        results['component_risks'] = self._analyze_component_risks(equipment_type, equipment_data)
        
        return results
    
    def _get_risk_level(self, probability: float) -> str:
        """Convert failure probability to risk level"""
        if probability < 0.2:
            return "Low"
        elif probability < 0.5:
            return "Medium"
        elif probability < 0.8:
            return "High"
        else:
            return "Critical"
    
    def _get_maintenance_recommendations(self, equipment_type: str, failure_prob: float, 
                                      equipment_data: Dict[str, Any]) -> List[str]:
        """Get specific maintenance recommendations based on equipment type and condition"""
        recommendations = []
        
        if equipment_type == 'rolling_mill':
            if failure_prob > 0.7:
                recommendations.append("Schedule immediate inspection of bearings and motor")
            if equipment_data.get('roll_wear_indicator', 0) > 50:
                recommendations.append("Plan roll replacement within next maintenance window")
            if equipment_data.get('motor_temperature', 0) > 80:
                recommendations.append("Check motor cooling system and ventilation")
            if equipment_data.get('vibration_level', 0) > 10:
                recommendations.append("Perform vibration analysis and alignment check")
        
        elif equipment_type == 'heat_treatment':
            if failure_prob > 0.7:
                recommendations.append("Schedule furnace inspection and heating element check")
            if equipment_data.get('temperature_deviation', 0) > 15:
                recommendations.append("Calibrate temperature control system")
            if equipment_data.get('heating_element_resistance', 0) < 8:
                recommendations.append("Replace heating elements showing high resistance")
        
        elif equipment_type == 'cutting_straightening':
            if failure_prob > 0.7:
                recommendations.append("Inspect cutting blade and straightening rolls")
            if equipment_data.get('blade_wear', 0) > 70:
                recommendations.append("Replace cutting blade")
            if equipment_data.get('hydraulic_pressure', 0) < 200:
                recommendations.append("Check hydraulic system for leaks")
        
        elif equipment_type == 'quality_control':
            if failure_prob > 0.7:
                recommendations.append("Perform comprehensive calibration check")
            if equipment_data.get('days_since_calibration', 0) > 180:
                recommendations.append("Schedule equipment recalibration")
            if equipment_data.get('measurement_repeatability', 0) < 99:
                recommendations.append("Investigate measurement system stability")
        
        if not recommendations:
            if failure_prob > 0.5:
                recommendations.append(f"Increase monitoring frequency for {equipment_type}")
            else:
                recommendations.append(f"Continue routine maintenance for {equipment_type}")
        
        return recommendations
    
    def _analyze_component_risks(self, equipment_type: str, equipment_data: Dict[str, Any]) -> Dict[str, str]:
        """Analyze risks for individual components"""
        component_risks = {}
        
        if equipment_type == 'rolling_mill':
            # Motor risk
            motor_temp = equipment_data.get('motor_temperature', 65)
            motor_current = equipment_data.get('motor_current', 150)
            if motor_temp > 85 or motor_current > 200:
                component_risks['motor'] = "High"
            elif motor_temp > 75 or motor_current > 180:
                component_risks['motor'] = "Medium"
            else:
                component_risks['motor'] = "Low"
            
            # Bearing risk
            bearing_temp = equipment_data.get('bearing_temperature', 70)
            vibration = equipment_data.get('motor_vibration_x', 2.5)
            if bearing_temp > 90 or vibration > 8:
                component_risks['bearings'] = "High"
            elif bearing_temp > 80 or vibration > 5:
                component_risks['bearings'] = "Medium"
            else:
                component_risks['bearings'] = "Low"
        
        elif equipment_type == 'heat_treatment':
            # Heating elements risk
            resistance = equipment_data.get('heating_element_resistance', 10)
            if resistance < 7 or resistance > 15:
                component_risks['heating_elements'] = "High"
            elif resistance < 8 or resistance > 13:
                component_risks['heating_elements'] = "Medium"
            else:
                component_risks['heating_elements'] = "Low"
            
            # Thermocouple risk
            drift = equipment_data.get('thermocouple_drift', 2)
            if drift > 8:
                component_risks['temperature_sensors'] = "High"
            elif drift > 5:
                component_risks['temperature_sensors'] = "Medium"
            else:
                component_risks['temperature_sensors'] = "Low"
        
        return component_risks
    
    def batch_predict(self, data: pd.DataFrame, equipment_type: str) -> pd.DataFrame:
        """Predict maintenance for multiple equipment instances"""
        if not self.models_trained:
            raise ValueError("Models must be trained before prediction")
        
        X = self.preprocess_equipment_features(data, equipment_type)
        scaler = getattr(self, f"{equipment_type.replace('_', '_')}_scaler" if '_' in equipment_type else f"{equipment_type}_scaler")
        classifier = getattr(self, f"{equipment_type}_model")
        
        X_scaled = scaler.transform(X)
        
        # Batch predictions
        failure_probs = classifier.predict_proba(X_scaled)[:, 1]
        failure_preds = classifier.predict(X_scaled)
        
        results = data.copy()
        results['failure_risk'] = failure_preds
        results['failure_probability'] = failure_probs
        results['risk_level'] = [self._get_risk_level(prob) for prob in failure_probs]
        
        return results
    
    def save_model(self, path: str):
        """Save all trained maintenance models"""
        model_data = {
            'rolling_mill_model': self.rolling_mill_model,
            'heat_treatment_model': self.heat_treatment_model,
            'cutting_straightening_model': self.cutting_straightening_model,
            'quality_control_model': self.quality_control_model,
            'rul_rolling_mill': self.rul_rolling_mill,
            'rul_heat_treatment': self.rul_heat_treatment,
            'rolling_scaler': self.rolling_scaler,
            'heat_treatment_scaler': self.heat_treatment_scaler,
            'cutting_scaler': self.cutting_scaler,
            'qc_scaler': self.qc_scaler,
            'rolling_mill_features': self.rolling_mill_features,
            'heat_treatment_features': self.heat_treatment_features,
            'cutting_straightening_features': self.cutting_straightening_features,
            'quality_control_features': self.quality_control_features,
            'models_trained': self.models_trained,
            'failure_types': self.failure_types
        }
        joblib.dump(model_data, f"{path}/rebar_predictive_maintenance_model.pkl")
        logger.info(f"Rebar predictive maintenance models saved to {path}")
    
    def load_model(self, path: str):
        """Load all trained maintenance models"""
        model_data = joblib.load(f"{path}/rebar_predictive_maintenance_model.pkl")
        
        self.rolling_mill_model = model_data['rolling_mill_model']
        self.heat_treatment_model = model_data['heat_treatment_model']
        self.cutting_straightening_model = model_data['cutting_straightening_model']
        self.quality_control_model = model_data['quality_control_model']
        self.rul_rolling_mill = model_data['rul_rolling_mill']
        self.rul_heat_treatment = model_data['rul_heat_treatment']
        self.rolling_scaler = model_data['rolling_scaler']
        self.heat_treatment_scaler = model_data['heat_treatment_scaler']
        self.cutting_scaler = model_data['cutting_scaler']
        self.qc_scaler = model_data['qc_scaler']
        self.rolling_mill_features = model_data['rolling_mill_features']
        self.heat_treatment_features = model_data['heat_treatment_features']
        self.cutting_straightening_features = model_data['cutting_straightening_features']
        self.quality_control_features = model_data['quality_control_features']
        self.models_trained = model_data['models_trained']
        self.failure_types = model_data['failure_types']
        
        logger.info(f"Rebar predictive maintenance models loaded from {path}")
    
    def generate_synthetic_maintenance_data(self, equipment_type: str, n_samples: int = 5000) -> pd.DataFrame:
        """Generate synthetic maintenance data for training"""
        np.random.seed(42)
        
        feature_names = getattr(self, f"{equipment_type}_features")
        data = {}
        
        # Generate features based on equipment type
        if equipment_type == 'rolling_mill':
            data = self._generate_rolling_mill_data(n_samples)
        elif equipment_type == 'heat_treatment':
            data = self._generate_heat_treatment_data(n_samples)
        elif equipment_type == 'cutting_straightening':
            data = self._generate_cutting_data(n_samples)
        elif equipment_type == 'quality_control':
            data = self._generate_qc_data(n_samples)
        
        df = pd.DataFrame(data)
        
        # Generate failure risk labels based on feature values
        df['failure_risk'] = self._calculate_failure_risk(df, equipment_type)
        df['time_to_failure'] = self._calculate_time_to_failure(df, equipment_type)
        
        return df
    
    def _generate_rolling_mill_data(self, n_samples: int) -> Dict[str, np.ndarray]:
        """Generate synthetic rolling mill data"""
        return {
            'motor_torque': np.random.normal(25000, 5000, n_samples),
            'motor_current': np.random.normal(150, 30, n_samples),
            'motor_temperature': np.random.gamma(3, 20, n_samples) + 40,
            'motor_vibration_x': np.random.exponential(3, n_samples),
            'motor_vibration_y': np.random.exponential(3, n_samples),
            'motor_vibration_z': np.random.exponential(3, n_samples),
            'bearing_temperature': np.random.gamma(3, 20, n_samples) + 50,
            'roll_gap': np.random.normal(16, 2, n_samples),
            'roll_pressure': np.random.normal(200, 40, n_samples),
            'roll_force': np.random.normal(3000, 600, n_samples),
            'roll_temperature': np.random.normal(45, 8, n_samples),
            'roll_wear_indicator': np.random.gamma(2, 15, n_samples),
            'roll_surface_roughness': np.random.lognormal(1, 0.3, n_samples),
            'roll_hardness': np.random.normal(65, 5, n_samples),
            'processing_speed': np.random.normal(60, 15, n_samples),
            'material_hardness': np.random.normal(180, 20, n_samples),
            'reduction_ratio': np.random.normal(15, 3, n_samples),
            'bars_processed_today': np.random.poisson(500, n_samples),
            'cumulative_tonnage': np.random.exponential(1500, n_samples),
            'operating_hours': np.random.poisson(16, n_samples),
            'lubricant_flow_rate': np.random.normal(10, 2, n_samples),
            'lubricant_temperature': np.random.normal(40, 5, n_samples),
            'cooling_water_flow': np.random.normal(50, 8, n_samples),
            'cooling_water_temperature': np.random.normal(25, 3, n_samples),
            'hydraulic_pressure': np.random.normal(200, 20, n_samples),
            'power_consumption': np.random.normal(450, 80, n_samples),
            'energy_per_bar': np.random.normal(0.9, 0.2, n_samples),
            'overall_efficiency': np.random.normal(85, 8, n_samples),
            'power_factor': np.random.normal(0.9, 0.05, n_samples),
            'harmonic_distortion': np.random.exponential(3, n_samples)
        }
    
    def _generate_heat_treatment_data(self, n_samples: int) -> Dict[str, np.ndarray]:
        """Generate synthetic heat treatment data"""
        return {
            'furnace_temperature': np.random.normal(950, 50, n_samples),
            'target_temperature': np.random.normal(950, 30, n_samples),
            'temperature_deviation': np.random.exponential(5, n_samples),
            'heating_rate': np.random.normal(5, 1, n_samples),
            'cooling_rate': np.random.normal(2, 0.5, n_samples),
            'temperature_uniformity': np.random.exponential(8, n_samples),
            'oxygen_level': np.random.exponential(0.15, n_samples),
            'carbon_potential': np.random.normal(0.8, 0.1, n_samples),
            'nitrogen_flow': np.random.normal(50, 10, n_samples),
            'atmosphere_pressure': np.random.normal(1.02, 0.05, n_samples),
            'combustion_efficiency': np.random.normal(85, 5, n_samples),
            'excess_air_ratio': np.random.normal(1.1, 0.1, n_samples),
            'heating_element_resistance': np.random.normal(10, 2, n_samples),
            'heating_element_current': np.random.normal(100, 20, n_samples),
            'fan_speed': np.random.normal(1200, 150, n_samples),
            'fan_vibration': np.random.exponential(3, n_samples),
            'fan_current': np.random.normal(25, 5, n_samples),
            'thermocouple_drift': np.random.exponential(3, n_samples),
            'furnace_load': np.random.normal(70, 15, n_samples),
            'batch_size': np.random.normal(5000, 1000, n_samples),
            'cycle_count': np.random.poisson(3, n_samples),
            'operating_hours': np.random.poisson(20, n_samples),
            'energy_consumption': np.random.normal(2500, 500, n_samples),
            'fuel_consumption': np.random.normal(150, 30, n_samples),
            'quench_pump_pressure': np.random.normal(5, 0.5, n_samples),
            'quench_flow_rate': np.random.normal(100, 15, n_samples),
            'quench_temperature': np.random.normal(25, 3, n_samples),
            'pump_vibration': np.random.exponential(2, n_samples),
            'pump_current': np.random.normal(30, 8, n_samples),
            'system_pressure': np.random.normal(4.5, 0.5, n_samples)
        }
    
    def _generate_cutting_data(self, n_samples: int) -> Dict[str, np.ndarray]:
        """Generate synthetic cutting/straightening data"""
        return {
            'straightening_force': np.random.normal(800, 150, n_samples),
            'roller_pressure': np.random.normal(150, 25, n_samples),
            'roller_positions': np.random.normal(5, 0.5, n_samples),
            'roller_wear': np.random.gamma(2, 10, n_samples),
            'straightening_motor_current': np.random.normal(80, 15, n_samples),
            'straightening_vibration': np.random.exponential(3, n_samples),
            'cutting_force': np.random.normal(300, 60, n_samples),
            'blade_position': np.random.normal(0, 0.1, n_samples),
            'blade_wear': np.random.gamma(2, 10, n_samples),
            'cutting_speed': np.random.normal(120, 20, n_samples),
            'shear_motor_current': np.random.normal(50, 10, n_samples),
            'shear_vibration': np.random.exponential(2.5, n_samples),
            'blade_temperature': np.random.normal(35, 5, n_samples),
            'length_measurement_accuracy': np.random.normal(99, 1, n_samples),
            'diameter_measurement_accuracy': np.random.normal(99.5, 0.5, n_samples),
            'weight_measurement_accuracy': np.random.normal(99.8, 0.3, n_samples),
            'measurement_system_drift': np.random.exponential(0.2, n_samples),
            'hydraulic_pressure': np.random.normal(250, 30, n_samples),
            'hydraulic_flow': np.random.normal(80, 12, n_samples),
            'hydraulic_temperature': np.random.normal(45, 5, n_samples),
            'pump_efficiency': np.random.normal(92, 3, n_samples),
            'filter_pressure_drop': np.random.exponential(2, n_samples),
            'oil_viscosity': np.random.normal(46, 3, n_samples),
            'conveyor_speed': np.random.normal(60, 10, n_samples),
            'conveyor_current': np.random.normal(15, 3, n_samples),
            'belt_tension': np.random.normal(500, 50, n_samples),
            'belt_wear': np.random.gamma(2, 5, n_samples),
            'guide_alignment': np.random.exponential(0.5, n_samples),
            'processing_cycles_today': np.random.poisson(800, n_samples)
        }
    
    def _generate_qc_data(self, n_samples: int) -> Dict[str, np.ndarray]:
        """Generate synthetic quality control data"""
        return {
            'tensile_machine_force_accuracy': np.random.normal(99.8, 0.3, n_samples),
            'tensile_machine_load_cell_drift': np.random.exponential(0.03, n_samples),
            'bend_test_fixture_wear': np.random.gamma(2, 3, n_samples),
            'dimensional_gauge_accuracy': np.random.normal(99.9, 0.2, n_samples),
            'spectrometer_lamp_intensity': np.random.normal(95, 5, n_samples),
            'spectrometer_detector_noise': np.random.exponential(2, n_samples),
            'lab_temperature': np.random.normal(23, 2, n_samples),
            'lab_humidity': np.random.normal(45, 8, n_samples),
            'vibration_isolation_effectiveness': np.random.normal(95, 3, n_samples),
            'electrical_noise_level': np.random.exponential(1, n_samples),
            'air_pressure': np.random.normal(101.3, 1, n_samples),
            'dust_level': np.random.exponential(0.1, n_samples),
            'days_since_calibration': np.random.uniform(0, 365, n_samples),
            'calibration_drift': np.random.exponential(0.2, n_samples),
            'reference_standard_age': np.random.uniform(0, 1095, n_samples),
            'measurement_repeatability': np.random.normal(99.5, 1, n_samples),
            'measurement_reproducibility': np.random.normal(99, 1.5, n_samples),
            'tests_performed_today': np.random.poisson(50, n_samples),
            'samples_processed': np.random.poisson(200, n_samples),
            'equipment_uptime': np.random.normal(95, 5, n_samples),
            'operator_changes': np.random.poisson(2, n_samples),
            'maintenance_hours': np.random.exponential(2, n_samples)
        }
    
    def _calculate_failure_risk(self, df: pd.DataFrame, equipment_type: str) -> np.ndarray:
        """Calculate failure risk based on equipment condition"""
        if equipment_type == 'rolling_mill':
            risk_score = (
                (df['motor_temperature'] > 80) * 0.3 +
                (df['bearing_temperature'] > 85) * 0.3 +
                (df['motor_vibration_x'] > 8) * 0.2 +
                (df['roll_wear_indicator'] > 60) * 0.2
            )
        elif equipment_type == 'heat_treatment':
            risk_score = (
                (df['temperature_deviation'] > 20) * 0.3 +
                (df['heating_element_resistance'] < 7) * 0.3 +
                (df['thermocouple_drift'] > 10) * 0.2 +
                (df['fan_vibration'] > 8) * 0.2
            )
        else:
            # Generic risk calculation
            risk_score = np.random.beta(2, 8, len(df))
        
        # Add some randomness
        risk_score += np.random.normal(0, 0.1, len(df))
        risk_score = np.clip(risk_score, 0, 1)
        
        return (risk_score > 0.6).astype(int)
    
    def _calculate_time_to_failure(self, df: pd.DataFrame, equipment_type: str) -> np.ndarray:
        """Calculate time to failure in days"""
        failure_risk = self._calculate_failure_risk(df, equipment_type)
        
        # Base RUL inversely related to risk factors
        base_rul = np.random.exponential(200, len(df))  # Base around 200 days
        
        # Adjust based on failure risk
        rul = np.where(failure_risk == 1, 
                      np.random.exponential(30, len(df)),    # 30 days for high risk
                      base_rul)                               # Normal RUL for low risk
        
        return np.clip(rul, 1, 1095)  # 1 day to 3 years