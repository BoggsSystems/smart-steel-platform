import numpy as np
import pandas as pd
from sklearn.ensemble import IsolationForest, RandomForestClassifier
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, confusion_matrix
from typing import Dict, List, Tuple, Any, Optional
import joblib
import logging
from collections import defaultdict

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class RebarDefectDetectionModel:
    """
    Advanced defect detection model specifically designed for rebar manufacturing.
    Detects surface defects, ribbing issues, dimensional deviations, and quality anomalies.
    """
    
    def __init__(self):
        # Multi-class defect classification model
        self.defect_classifier = RandomForestClassifier(
            n_estimators=300,
            max_depth=20,
            min_samples_split=3,
            min_samples_leaf=1,
            class_weight='balanced',  # Handle imbalanced defect classes
            random_state=42
        )
        
        # Anomaly detection for unusual patterns
        self.anomaly_detector = IsolationForest(
            contamination=0.1,  # Expected 10% anomaly rate
            random_state=42,
            n_estimators=200
        )
        
        # Ribbing pattern defect detector
        self.ribbing_detector = RandomForestClassifier(
            n_estimators=200,
            max_depth=15,
            min_samples_split=2,
            random_state=42
        )
        
        # Dimensional defect detector
        self.dimensional_detector = RandomForestClassifier(
            n_estimators=150,
            max_depth=12,
            random_state=42
        )
        
        # Preprocessing components
        self.scaler = StandardScaler()
        self.grade_encoder = LabelEncoder()
        self.size_encoder = LabelEncoder()
        self.defect_encoder = LabelEncoder()
        
        # Rebar-specific feature names for defect detection
        self.feature_names = [
            # Surface quality features
            'surface_roughness', 'surface_defect_count', 'surface_crack_length',
            'surface_oxidation_level', 'surface_cleanliness_score',
            
            # Ribbing features
            'rib_height', 'rib_height_variance', 'rib_spacing', 'rib_spacing_variance',
            'rib_angle', 'rib_angle_variance', 'rib_consistency', 'relative_rib_area',
            'rib_fill_ratio', 'rib_sharpness',
            
            # Dimensional features
            'diameter_variance', 'ovality', 'straightness_deviation', 'length_deviation',
            'cross_sectional_area_variance', 'weight_per_meter_deviation',
            
            # Process-induced features
            'cooling_rate_variance', 'roll_pressure_variance', 'temperature_gradient',
            'strain_rate', 'deformation_energy',
            
            # Rolling mill features
            'roll_gap_deviation', 'roll_wear_indicator', 'rolling_force_variance',
            'vibration_amplitude', 'motor_current_variance',
            
            # Heat treatment features
            'quench_uniformity', 'tempering_consistency', 'hardness_variance',
            'microstructure_score', 'grain_size_uniformity',
            
            # Encoded categorical features
            'grade_encoded', 'size_encoded'
        ]
        
        # Defect categories
        self.defect_types = [
            'no_defect',           # 0 - No defects detected
            'surface_crack',       # 1 - Surface cracks or fissures
            'surface_fold',        # 2 - Surface folds or laps
            'surface_pit',         # 3 - Surface pits or depressions
            'rib_height_low',      # 4 - Rib height below specification
            'rib_height_high',     # 5 - Rib height above specification
            'rib_spacing_error',   # 6 - Incorrect rib spacing
            'rib_angle_error',     # 7 - Incorrect rib angle
            'rib_fill_poor',       # 8 - Incomplete rib formation
            'dimensional_error',   # 9 - Diameter or length out of tolerance
            'straightness_error',  # 10 - Excessive bow or bend
            'ovality_error',       # 11 - Cross-section not round
            'surface_scale',       # 12 - Excessive surface oxidation
            'inclusion',           # 13 - Non-metallic inclusions
            'mechanical_damage'    # 14 - Physical damage during handling
        ]
        
        self.models_trained = False
        
        # Defect severity mapping
        self.defect_severity = {
            'no_defect': 0,
            'surface_scale': 1,         # Minor
            'rib_fill_poor': 1,         # Minor
            'surface_pit': 2,           # Moderate
            'rib_spacing_error': 2,     # Moderate
            'rib_angle_error': 2,       # Moderate
            'ovality_error': 2,         # Moderate
            'rib_height_low': 3,        # Major
            'rib_height_high': 3,       # Major
            'dimensional_error': 3,     # Major
            'surface_fold': 4,          # Critical
            'surface_crack': 4,         # Critical
            'straightness_error': 4,    # Critical
            'inclusion': 4,             # Critical
            'mechanical_damage': 4      # Critical
        }
    
    def preprocess_features(self, data: pd.DataFrame) -> np.ndarray:
        """Extract and preprocess rebar-specific features for defect detection"""
        features = []
        
        # Handle missing features with reasonable defaults
        for feature in self.feature_names[:-2]:  # Exclude encoded features
            if feature in data.columns:
                features.append(data[feature].values)
            else:
                default_values = self._get_default_defect_feature_values(feature, len(data))
                features.append(default_values)
        
        # Handle categorical encoding
        if 'grade' in data.columns:
            grade_encoded = self.grade_encoder.transform(data['grade'].astype(str))
            features.append(grade_encoded)
        else:
            features.append(np.ones(len(data)))  # Default Grade60
            
        if 'size' in data.columns:
            size_encoded = self.size_encoder.transform(data['size'].astype(str))
            features.append(size_encoded)
        else:
            features.append(np.ones(len(data)) * 2)  # Default Size5
        
        return np.column_stack(features)
    
    def _get_default_defect_feature_values(self, feature: str, length: int) -> np.ndarray:
        """Provide reasonable default values for missing defect detection features"""
        defaults = {
            # Surface quality features (good quality defaults)
            'surface_roughness': 2.5, 'surface_defect_count': 0, 'surface_crack_length': 0,
            'surface_oxidation_level': 1, 'surface_cleanliness_score': 95,
            
            # Ribbing features (standard ASTM values)
            'rib_height': 0.7, 'rib_height_variance': 0.05, 'rib_spacing': 11.2, 'rib_spacing_variance': 0.3,
            'rib_angle': 60, 'rib_angle_variance': 2, 'rib_consistency': 95, 'relative_rib_area': 0.65,
            'rib_fill_ratio': 98, 'rib_sharpness': 90,
            
            # Dimensional features (within tolerance defaults)
            'diameter_variance': 0.1, 'ovality': 0.5, 'straightness_deviation': 2, 'length_deviation': 5,
            'cross_sectional_area_variance': 0.2, 'weight_per_meter_deviation': 1,
            
            # Process features (stable process defaults)
            'cooling_rate_variance': 2, 'roll_pressure_variance': 5, 'temperature_gradient': 3,
            'strain_rate': 1.2, 'deformation_energy': 50,
            
            # Equipment features (good condition defaults)
            'roll_gap_deviation': 0.02, 'roll_wear_indicator': 20, 'rolling_force_variance': 3,
            'vibration_amplitude': 2, 'motor_current_variance': 5,
            
            # Heat treatment features (uniform treatment defaults)
            'quench_uniformity': 95, 'tempering_consistency': 98, 'hardness_variance': 2,
            'microstructure_score': 90, 'grain_size_uniformity': 85
        }
        
        return np.full(length, defaults.get(feature, 0))
    
    def prepare_categorical_encoders(self, data: pd.DataFrame):
        """Prepare categorical encoders"""
        grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
        sizes = ['Size3', 'Size4', 'Size5', 'Size6', 'Size7', 'Size8', 'Size9', 'Size10', 'Size11', 'Size14', 'Size18']
        
        self.grade_encoder.fit(grades)
        self.size_encoder.fit(sizes)
        self.defect_encoder.fit(self.defect_types)
    
    def train(self, data: pd.DataFrame, target_column: str = 'defect_type') -> Dict[str, Any]:
        """
        Train defect detection models
        
        Args:
            data: Training data with process parameters and defect labels
            target_column: Column name containing defect type labels
        """
        logger.info("Starting rebar defect detection model training...")
        
        # Prepare encoders
        self.prepare_categorical_encoders(data)
        
        # Preprocess features
        X = self.preprocess_features(data)
        X_scaled = self.scaler.fit_transform(X)
        
        results = {}
        
        # Train main defect classifier
        if target_column in data.columns:
            y_defects = self.defect_encoder.transform(data[target_column].astype(str))
            
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_defects, test_size=0.2, random_state=42, stratify=y_defects
            )
            
            self.defect_classifier.fit(X_train, y_train)
            defect_score = self.defect_classifier.score(X_test, y_test)
            y_pred = self.defect_classifier.predict(X_test)
            
            results['defect_classification_accuracy'] = defect_score
            results['defect_classification_report'] = classification_report(
                y_test, y_pred, target_names=self.defect_types, output_dict=True
            )
            
            # Feature importance
            feature_importance = dict(zip(self.feature_names, self.defect_classifier.feature_importances_))
            results['defect_feature_importance'] = feature_importance
            
            logger.info(f"Defect classification accuracy: {defect_score:.3f}")
        
        # Train anomaly detector
        self.anomaly_detector.fit(X_scaled)
        anomaly_predictions = self.anomaly_detector.predict(X_scaled)
        anomaly_score = np.mean(anomaly_predictions == 1)  # Proportion of normal samples
        results['anomaly_detection_normal_rate'] = anomaly_score
        
        # Train specialized detectors
        self._train_specialized_detectors(data, X_scaled, results)
        
        self.models_trained = True
        logger.info("Rebar defect detection model training completed successfully")
        
        return results
    
    def _train_specialized_detectors(self, data: pd.DataFrame, X_scaled: np.ndarray, results: Dict[str, Any]):
        """Train specialized detectors for ribbing and dimensional defects"""
        
        # Ribbing defect detector
        ribbing_defects = ['rib_height_low', 'rib_height_high', 'rib_spacing_error', 
                          'rib_angle_error', 'rib_fill_poor']
        
        if 'defect_type' in data.columns:
            y_ribbing = (data['defect_type'].isin(ribbing_defects)).astype(int)
            
            if y_ribbing.sum() > 10:  # Enough positive samples
                X_train, X_test, y_train, y_test = train_test_split(
                    X_scaled, y_ribbing, test_size=0.2, random_state=42
                )
                
                self.ribbing_detector.fit(X_train, y_train)
                ribbing_score = self.ribbing_detector.score(X_test, y_test)
                results['ribbing_defect_accuracy'] = ribbing_score
                
                logger.info(f"Ribbing defect detection accuracy: {ribbing_score:.3f}")
        
        # Dimensional defect detector
        dimensional_defects = ['dimensional_error', 'straightness_error', 'ovality_error']
        
        if 'defect_type' in data.columns:
            y_dimensional = (data['defect_type'].isin(dimensional_defects)).astype(int)
            
            if y_dimensional.sum() > 10:  # Enough positive samples
                X_train, X_test, y_train, y_test = train_test_split(
                    X_scaled, y_dimensional, test_size=0.2, random_state=42
                )
                
                self.dimensional_detector.fit(X_train, y_train)
                dimensional_score = self.dimensional_detector.score(X_test, y_test)
                results['dimensional_defect_accuracy'] = dimensional_score
                
                logger.info(f"Dimensional defect detection accuracy: {dimensional_score:.3f}")
    
    def detect_defects(self, process_data: Dict[str, Any]) -> Dict[str, Any]:
        """Detect defects for a single rebar sample"""
        if not self.models_trained:
            raise ValueError("Models must be trained before prediction")
        
        df = pd.DataFrame([process_data])
        X = self.preprocess_features(df)
        X_scaled = self.scaler.transform(X)
        
        results = {}
        
        # Main defect classification
        defect_probs = self.defect_classifier.predict_proba(X_scaled)[0]
        defect_pred = self.defect_classifier.predict(X_scaled)[0]
        predicted_defect = self.defect_types[defect_pred]
        
        results['primary_defect'] = predicted_defect
        results['defect_confidence'] = float(max(defect_probs))
        results['defect_probabilities'] = dict(zip(self.defect_types, defect_probs.astype(float)))
        
        # Anomaly detection
        anomaly_score = self.anomaly_detector.decision_function(X_scaled)[0]
        is_anomaly = self.anomaly_detector.predict(X_scaled)[0] == -1
        
        results['is_anomaly'] = bool(is_anomaly)
        results['anomaly_score'] = float(anomaly_score)
        
        # Specialized detectors
        ribbing_prob = self.ribbing_detector.predict_proba(X_scaled)[0, 1] if hasattr(self.ribbing_detector, 'predict_proba') else 0.0
        dimensional_prob = self.dimensional_detector.predict_proba(X_scaled)[0, 1] if hasattr(self.dimensional_detector, 'predict_proba') else 0.0
        
        results['ribbing_defect_probability'] = float(ribbing_prob)
        results['dimensional_defect_probability'] = float(dimensional_prob)
        
        # Overall defect severity assessment
        severity = self.defect_severity.get(predicted_defect, 0)
        results['defect_severity'] = severity
        results['severity_level'] = self._get_severity_level(severity)
        
        # Recommendations
        results['recommendations'] = self._get_defect_recommendations(predicted_defect, process_data)
        
        # Root cause analysis
        results['likely_causes'] = self._analyze_root_causes(predicted_defect, process_data)
        
        return results
    
    def _get_severity_level(self, severity: int) -> str:
        """Convert severity number to descriptive level"""
        severity_levels = {
            0: "No Issue",
            1: "Minor",
            2: "Moderate", 
            3: "Major",
            4: "Critical"
        }
        return severity_levels.get(severity, "Unknown")
    
    def _get_defect_recommendations(self, defect_type: str, process_data: Dict[str, Any]) -> List[str]:
        """Get specific recommendations based on detected defect type"""
        recommendations = {
            'surface_crack': [
                "Reduce cooling rate to prevent thermal stress",
                "Check for hydrogen embrittlement in steel",
                "Inspect rolling mill roll condition",
                "Review quenching parameters"
            ],
            'surface_fold': [
                "Check roll gap settings",
                "Verify billet surface condition",
                "Review rolling mill setup",
                "Inspect guide systems"
            ],
            'rib_height_low': [
                "Increase roll pressure in ribbing stand",
                "Check ribbing roll wear",
                "Verify material flow in ribbing zone",
                "Adjust rolling speed"
            ],
            'rib_height_high': [
                "Reduce roll pressure in ribbing stand",
                "Check for material buildup on rolls",
                "Verify ribbing roll calibration",
                "Review material hardness"
            ],
            'rib_spacing_error': [
                "Check ribbing roll alignment",
                "Verify rolling speed consistency",
                "Inspect ribbing pattern setup",
                "Check for roll wear patterns"
            ],
            'dimensional_error': [
                "Calibrate measurement systems",
                "Check roll gap accuracy",
                "Verify temperature control",
                "Review pass schedule design"
            ],
            'straightness_error': [
                "Adjust straightening rolls",
                "Check cooling uniformity",
                "Review material handling",
                "Inspect straightening machine"
            ]
        }
        
        return recommendations.get(defect_type, ["Contact quality control supervisor"])
    
    def _analyze_root_causes(self, defect_type: str, process_data: Dict[str, Any]) -> List[str]:
        """Analyze likely root causes based on process parameters"""
        causes = []
        
        # Temperature-related causes
        billet_temp = process_data.get('surface_roughness', 0)
        if billet_temp < 1000 and defect_type in ['surface_crack', 'surface_fold']:
            causes.append("Low billet temperature may cause surface defects")
        
        # Rolling-related causes
        if defect_type.startswith('rib_') and process_data.get('rolling_force_variance', 0) > 10:
            causes.append("High rolling force variation affects ribbing quality")
        
        # Cooling-related causes
        if defect_type in ['straightness_error', 'dimensional_error'] and process_data.get('cooling_rate_variance', 0) > 5:
            causes.append("Uneven cooling causes dimensional issues")
        
        # Equipment-related causes
        if process_data.get('vibration_amplitude', 0) > 5:
            causes.append("High equipment vibration may cause surface and dimensional defects")
        
        if not causes:
            causes.append("Multiple factors may contribute to this defect type")
        
        return causes
    
    def batch_detect(self, data: pd.DataFrame) -> pd.DataFrame:
        """Detect defects for a batch of samples"""
        if not self.models_trained:
            raise ValueError("Models must be trained before prediction")
        
        X = self.preprocess_features(data)
        X_scaled = self.scaler.transform(X)
        
        # Batch predictions
        defect_preds = self.defect_classifier.predict(X_scaled)
        defect_probs = self.defect_classifier.predict_proba(X_scaled)
        anomaly_preds = self.anomaly_detector.predict(X_scaled)
        
        results = data.copy()
        results['predicted_defect'] = [self.defect_types[pred] for pred in defect_preds]
        results['defect_confidence'] = np.max(defect_probs, axis=1)
        results['is_anomaly'] = anomaly_preds == -1
        results['defect_severity'] = [self.defect_severity.get(self.defect_types[pred], 0) for pred in defect_preds]
        
        return results
    
    def save_model(self, path: str):
        """Save all trained models"""
        model_data = {
            'defect_classifier': self.defect_classifier,
            'anomaly_detector': self.anomaly_detector,
            'ribbing_detector': self.ribbing_detector,
            'dimensional_detector': self.dimensional_detector,
            'scaler': self.scaler,
            'grade_encoder': self.grade_encoder,
            'size_encoder': self.size_encoder,
            'defect_encoder': self.defect_encoder,
            'feature_names': self.feature_names,
            'defect_types': self.defect_types,
            'defect_severity': self.defect_severity,
            'models_trained': self.models_trained
        }
        joblib.dump(model_data, f"{path}/rebar_defect_detection_model.pkl")
        logger.info(f"Rebar defect detection models saved to {path}")
    
    def load_model(self, path: str):
        """Load all trained models"""
        model_data = joblib.load(f"{path}/rebar_defect_detection_model.pkl")
        
        self.defect_classifier = model_data['defect_classifier']
        self.anomaly_detector = model_data['anomaly_detector']
        self.ribbing_detector = model_data['ribbing_detector']
        self.dimensional_detector = model_data['dimensional_detector']
        self.scaler = model_data['scaler']
        self.grade_encoder = model_data['grade_encoder']
        self.size_encoder = model_data['size_encoder']
        self.defect_encoder = model_data['defect_encoder']
        self.feature_names = model_data['feature_names']
        self.defect_types = model_data['defect_types']
        self.defect_severity = model_data['defect_severity']
        self.models_trained = model_data['models_trained']
        
        logger.info(f"Rebar defect detection models loaded from {path}")
    
    def generate_synthetic_defect_data(self, n_samples: int = 15000) -> pd.DataFrame:
        """Generate synthetic training data with various defect types"""
        np.random.seed(42)
        
        data = {}
        
        # Base process parameters (similar to quality model)
        grades = np.random.choice(['Grade40', 'Grade60', 'Grade75', 'Grade80'], 
                                 n_samples, p=[0.1, 0.6, 0.2, 0.1])
        data['grade'] = grades
        
        sizes = np.random.choice(['Size3', 'Size4', 'Size5', 'Size6', 'Size7', 'Size8'], 
                                n_samples, p=[0.1, 0.15, 0.25, 0.25, 0.15, 0.1])
        data['size'] = sizes
        
        # Generate process features with variations that cause defects
        data['surface_roughness'] = np.random.lognormal(1, 0.5, n_samples)
        data['surface_defect_count'] = np.random.poisson(0.5, n_samples)
        data['surface_crack_length'] = np.random.exponential(0.2, n_samples)
        data['surface_oxidation_level'] = np.random.gamma(2, 0.5, n_samples)
        data['surface_cleanliness_score'] = np.random.normal(92, 8, n_samples)
        
        # Ribbing features with defect-inducing variations
        data['rib_height'] = np.random.normal(0.7, 0.15, n_samples)
        data['rib_height_variance'] = np.random.exponential(0.05, n_samples)
        data['rib_spacing'] = np.random.normal(11.2, 1.5, n_samples)
        data['rib_spacing_variance'] = np.random.exponential(0.3, n_samples)
        data['rib_angle'] = np.random.normal(60, 5, n_samples)
        data['rib_angle_variance'] = np.random.exponential(2, n_samples)
        data['rib_consistency'] = np.random.normal(95, 8, n_samples)
        data['relative_rib_area'] = np.random.normal(0.65, 0.1, n_samples)
        data['rib_fill_ratio'] = np.random.normal(98, 5, n_samples)
        data['rib_sharpness'] = np.random.normal(90, 10, n_samples)
        
        # Dimensional features
        data['diameter_variance'] = np.random.exponential(0.2, n_samples)
        data['ovality'] = np.random.exponential(0.3, n_samples)
        data['straightness_deviation'] = np.random.exponential(2, n_samples)
        data['length_deviation'] = np.random.normal(0, 8, n_samples)
        data['cross_sectional_area_variance'] = np.random.exponential(0.3, n_samples)
        data['weight_per_meter_deviation'] = np.random.normal(0, 2, n_samples)
        
        # Process variation features
        data['cooling_rate_variance'] = np.random.exponential(3, n_samples)
        data['roll_pressure_variance'] = np.random.exponential(5, n_samples)
        data['temperature_gradient'] = np.random.exponential(2, n_samples)
        data['strain_rate'] = np.random.lognormal(0.2, 0.3, n_samples)
        data['deformation_energy'] = np.random.normal(50, 15, n_samples)
        
        # Equipment condition features
        data['roll_gap_deviation'] = np.random.exponential(0.02, n_samples)
        data['roll_wear_indicator'] = np.random.gamma(3, 10, n_samples)
        data['rolling_force_variance'] = np.random.exponential(5, n_samples)
        data['vibration_amplitude'] = np.random.exponential(3, n_samples)
        data['motor_current_variance'] = np.random.exponential(3, n_samples)
        
        # Heat treatment uniformity features
        data['quench_uniformity'] = np.random.normal(95, 8, n_samples)
        data['tempering_consistency'] = np.random.normal(98, 5, n_samples)
        data['hardness_variance'] = np.random.exponential(3, n_samples)
        data['microstructure_score'] = np.random.normal(90, 10, n_samples)
        data['grain_size_uniformity'] = np.random.normal(85, 12, n_samples)
        
        df = pd.DataFrame(data)
        
        # Generate defect labels based on process parameters
        defect_probs = self._calculate_defect_probabilities(df)
        defects = []
        
        for i in range(n_samples):
            probs = defect_probs[i]
            defect_idx = np.random.choice(len(self.defect_types), p=probs)
            defects.append(self.defect_types[defect_idx])
        
        df['defect_type'] = defects
        
        return df
    
    def _calculate_defect_probabilities(self, df: pd.DataFrame) -> np.ndarray:
        """Calculate defect probabilities based on process parameters"""
        n_samples = len(df)
        n_defects = len(self.defect_types)
        probs = np.zeros((n_samples, n_defects))
        
        # Base probability for no defect (most common)
        probs[:, 0] = 0.7
        
        # Surface crack probability
        surface_crack_prob = (
            (df['surface_crack_length'] > 0.1) * 0.3 +
            (df['cooling_rate_variance'] > 5) * 0.2 +
            (df['temperature_gradient'] > 4) * 0.2
        ).clip(0, 0.8)
        probs[:, 1] = surface_crack_prob
        
        # Rib height defects
        rib_low_prob = ((df['rib_height'] < 0.5) * 0.4 + (df['roll_pressure_variance'] > 8) * 0.2).clip(0, 0.6)
        rib_high_prob = ((df['rib_height'] > 1.0) * 0.4 + (df['roll_wear_indicator'] > 50) * 0.2).clip(0, 0.6)
        probs[:, 4] = rib_low_prob
        probs[:, 5] = rib_high_prob
        
        # Spacing and angle errors
        spacing_prob = ((df['rib_spacing_variance'] > 0.5) * 0.3 + (df['vibration_amplitude'] > 5) * 0.2).clip(0, 0.5)
        angle_prob = ((df['rib_angle_variance'] > 3) * 0.3 + (df['rolling_force_variance'] > 8) * 0.2).clip(0, 0.5)
        probs[:, 6] = spacing_prob
        probs[:, 7] = angle_prob
        
        # Dimensional errors
        dim_prob = ((df['diameter_variance'] > 0.5) * 0.3 + (df['roll_gap_deviation'] > 0.05) * 0.3).clip(0, 0.6)
        straight_prob = ((df['straightness_deviation'] > 5) * 0.4 + (df['cooling_rate_variance'] > 6) * 0.2).clip(0, 0.6)
        oval_prob = ((df['ovality'] > 1.0) * 0.4 + (df['roll_wear_indicator'] > 40) * 0.2).clip(0, 0.5)
        
        probs[:, 9] = dim_prob
        probs[:, 10] = straight_prob
        probs[:, 11] = oval_prob
        
        # Surface defects
        pit_prob = ((df['surface_defect_count'] > 2) * 0.3 + (df['surface_oxidation_level'] > 2) * 0.2).clip(0, 0.4)
        scale_prob = ((df['surface_oxidation_level'] > 3) * 0.4 + (df['surface_cleanliness_score'] < 85) * 0.2).clip(0, 0.5)
        probs[:, 3] = pit_prob
        probs[:, 12] = scale_prob
        
        # Normalize probabilities
        prob_sums = probs.sum(axis=1, keepdims=True)
        prob_sums[prob_sums == 0] = 1  # Avoid division by zero
        probs = probs / prob_sums
        
        return probs