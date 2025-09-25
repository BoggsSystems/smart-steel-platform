import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier, GradientBoostingRegressor
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, mean_squared_error, r2_score
from typing import Dict, List, Tuple, Any, Optional
import joblib
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class RebarQualityPredictionModel:
    """
    Enhanced quality prediction model specifically designed for rebar manufacturing.
    Predicts ASTM compliance, quality scores, and mechanical properties.
    """
    
    def __init__(self):
        # ASTM Compliance Classification Model
        self.compliance_model = RandomForestClassifier(
            n_estimators=200,
            max_depth=15,
            min_samples_split=5,
            min_samples_leaf=2,
            random_state=42
        )
        
        # Mechanical Property Regression Models
        self.yield_strength_model = GradientBoostingRegressor(
            n_estimators=150,
            max_depth=8,
            learning_rate=0.1,
            random_state=42
        )
        
        self.tensile_strength_model = GradientBoostingRegressor(
            n_estimators=150,
            max_depth=8,
            learning_rate=0.1,
            random_state=42
        )
        
        self.elongation_model = GradientBoostingRegressor(
            n_estimators=100,
            max_depth=6,
            learning_rate=0.1,
            random_state=42
        )
        
        # Scalers and encoders
        self.scaler = StandardScaler()
        self.grade_encoder = LabelEncoder()
        self.size_encoder = LabelEncoder()
        
        # Rebar-specific feature names
        self.feature_names = [
            # Chemical composition
            'carbon_content', 'manganese_content', 'phosphorus_content', 
            'sulfur_content', 'silicon_content', 'nitrogen_content',
            
            # Rolling mill parameters
            'billet_temperature', 'rolling_speed', 'total_reduction',
            'final_diameter', 'rib_height', 'rib_spacing', 'rib_consistency',
            
            # Heat treatment parameters
            'austenitizing_temp', 'quench_rate', 'tempering_temp', 'cooling_rate',
            
            # Process parameters
            'casting_temperature', 'ladle_treatment_time', 'straightness',
            'surface_quality_score', 'dimensional_accuracy',
            
            # Equipment parameters
            'furnace_atmosphere', 'roll_gap_accuracy', 'cutting_precision',
            
            # Encoded categorical features
            'grade_encoded', 'size_encoded'
        ]
        
        self.models_trained = False
        
        # ASTM specification limits for validation
        self.astm_specs = {
            'Grade40': {'min_yield': 275, 'min_tensile': 420, 'min_elongation': 12.0},
            'Grade60': {'min_yield': 420, 'min_tensile': 620, 'min_elongation': 9.0},
            'Grade75': {'min_yield': 520, 'min_tensile': 690, 'min_elongation': 7.0},
            'Grade80': {'min_yield': 550, 'min_tensile': 720, 'min_elongation': 6.0}
        }
    
    def preprocess_features(self, data: pd.DataFrame) -> np.ndarray:
        """Extract and preprocess rebar-specific features from raw process data"""
        features = []
        
        # Handle missing features by filling with defaults or zeros
        for feature in self.feature_names[:-2]:  # Exclude encoded features
            if feature in data.columns:
                features.append(data[feature].values)
            else:
                # Provide reasonable defaults for missing rebar features
                default_values = self._get_default_feature_values(feature, len(data))
                features.append(default_values)
        
        # Handle categorical encoding
        if 'grade' in data.columns:
            grade_encoded = self.grade_encoder.transform(data['grade'].astype(str))
            features.append(grade_encoded)
        else:
            features.append(np.zeros(len(data)))  # Default Grade60 = 1
            
        if 'size' in data.columns:
            size_encoded = self.size_encoder.transform(data['size'].astype(str))
            features.append(size_encoded)
        else:
            features.append(np.zeros(len(data)))  # Default Size5 = 2
        
        return np.column_stack(features)
    
    def _get_default_feature_values(self, feature: str, length: int) -> np.ndarray:
        """Provide reasonable default values for missing rebar process features"""
        defaults = {
            # Chemical composition (typical rebar values)
            'carbon_content': 0.25, 'manganese_content': 1.2, 'phosphorus_content': 0.03,
            'sulfur_content': 0.04, 'silicon_content': 0.3, 'nitrogen_content': 0.012,
            
            # Rolling mill parameters
            'billet_temperature': 1050, 'rolling_speed': 8.0, 'total_reduction': 85,
            'final_diameter': 16, 'rib_height': 0.7, 'rib_spacing': 11.2, 'rib_consistency': 95,
            
            # Heat treatment parameters
            'austenitizing_temp': 900, 'quench_rate': 50, 'tempering_temp': 600, 'cooling_rate': 25,
            
            # Process parameters
            'casting_temperature': 1520, 'ladle_treatment_time': 15, 'straightness': 3,
            'surface_quality_score': 90, 'dimensional_accuracy': 95,
            
            # Equipment parameters
            'furnace_atmosphere': 0.8, 'roll_gap_accuracy': 98, 'cutting_precision': 99
        }
        
        return np.full(length, defaults.get(feature, 0))
    
    def prepare_categorical_encoders(self, data: pd.DataFrame):
        """Prepare categorical encoders for grade and size"""
        if 'grade' in data.columns:
            unique_grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
            self.grade_encoder.fit(unique_grades)
        else:
            self.grade_encoder.fit(['Grade40', 'Grade60', 'Grade75', 'Grade80'])
            
        if 'size' in data.columns:
            unique_sizes = ['Size3', 'Size4', 'Size5', 'Size6', 'Size7', 'Size8', 
                          'Size9', 'Size10', 'Size11', 'Size14', 'Size18']
            self.size_encoder.fit(unique_sizes)
        else:
            self.size_encoder.fit(['Size3', 'Size4', 'Size5', 'Size6', 'Size7', 'Size8', 
                                 'Size9', 'Size10', 'Size11', 'Size14', 'Size18'])
    
    def train(self, data: pd.DataFrame, target_columns: Dict[str, str]) -> Dict[str, Any]:
        """
        Train all rebar quality prediction models
        
        Args:
            data: Training data with process parameters
            target_columns: Dictionary mapping target names to column names
                          {'compliance': 'astm_compliant', 'yield_strength': 'actual_yield_strength', ...}
        """
        logger.info("Starting rebar quality model training...")
        
        # Prepare categorical encoders
        self.prepare_categorical_encoders(data)
        
        # Preprocess features
        X = self.preprocess_features(data)
        X_scaled = self.scaler.fit_transform(X)
        
        results = {}
        
        # Train ASTM compliance classifier
        if target_columns.get('compliance') in data.columns:
            y_compliance = data[target_columns['compliance']].values
            X_train, X_test, y_train, y_test = train_test_split(
                X_scaled, y_compliance, test_size=0.2, random_state=42
            )
            
            self.compliance_model.fit(X_train, y_train)
            compliance_score = self.compliance_model.score(X_test, y_test)
            results['compliance_accuracy'] = compliance_score
            
            # Feature importance for compliance
            feature_importance = dict(zip(self.feature_names, self.compliance_model.feature_importances_))
            results['compliance_feature_importance'] = feature_importance
            
            logger.info(f"ASTM Compliance model accuracy: {compliance_score:.3f}")
        
        # Train mechanical property regression models
        property_models = [
            ('yield_strength', self.yield_strength_model, 'yield_strength'),
            ('tensile_strength', self.tensile_strength_model, 'tensile_strength'),
            ('elongation', self.elongation_model, 'elongation')
        ]
        
        for prop_name, model, target_key in property_models:
            if target_columns.get(target_key) in data.columns:
                y_prop = data[target_columns[target_key]].values
                X_train, X_test, y_train, y_test = train_test_split(
                    X_scaled, y_prop, test_size=0.2, random_state=42
                )
                
                model.fit(X_train, y_train)
                y_pred = model.predict(X_test)
                
                mse = mean_squared_error(y_test, y_pred)
                r2 = r2_score(y_test, y_pred)
                
                results[f'{prop_name}_mse'] = mse
                results[f'{prop_name}_r2'] = r2
                results[f'{prop_name}_feature_importance'] = dict(zip(self.feature_names, model.feature_importances_))
                
                logger.info(f"{prop_name.title()} model R² score: {r2:.3f}")
        
        self.models_trained = True
        logger.info("Rebar quality model training completed successfully")
        
        return results
    
    def predict_quality(self, process_data: Dict[str, Any]) -> Dict[str, Any]:
        """Predict rebar quality metrics for a single production run"""
        if not self.models_trained:
            raise ValueError("Models must be trained before prediction")
        
        df = pd.DataFrame([process_data])
        X = self.preprocess_features(df)
        X_scaled = self.scaler.transform(X)
        
        predictions = {}
        
        # ASTM Compliance prediction
        compliance_prob = self.compliance_model.predict_proba(X_scaled)[0, 1]
        compliance_pred = self.compliance_model.predict(X_scaled)[0]
        
        predictions['astm_compliant'] = bool(compliance_pred)
        predictions['compliance_confidence'] = float(compliance_prob)
        
        # Mechanical property predictions
        predictions['predicted_yield_strength'] = float(self.yield_strength_model.predict(X_scaled)[0])
        predictions['predicted_tensile_strength'] = float(self.tensile_strength_model.predict(X_scaled)[0])
        predictions['predicted_elongation'] = float(self.elongation_model.predict(X_scaled)[0])
        
        # Calculate quality score based on ASTM compliance probability and property predictions
        grade = process_data.get('grade', 'Grade60')
        quality_score = self._calculate_quality_score(predictions, grade)
        predictions['quality_score'] = quality_score
        
        # Risk assessment
        risk_factors = self._assess_risk_factors(process_data, predictions)
        predictions['risk_factors'] = risk_factors
        
        return predictions
    
    def _calculate_quality_score(self, predictions: Dict[str, Any], grade: str) -> float:
        """Calculate overall quality score based on ASTM compliance and properties"""
        base_score = predictions['compliance_confidence'] * 100
        
        # Adjust score based on mechanical property predictions
        if grade in self.astm_specs:
            specs = self.astm_specs[grade]
            
            # Yield strength factor
            yield_factor = min(1.0, predictions['predicted_yield_strength'] / specs['min_yield'])
            
            # Tensile strength factor
            tensile_factor = min(1.0, predictions['predicted_tensile_strength'] / specs['min_tensile'])
            
            # Elongation factor
            elongation_factor = min(1.0, predictions['predicted_elongation'] / specs['min_elongation'])
            
            # Combined property score
            property_score = (yield_factor + tensile_factor + elongation_factor) / 3 * 100
            
            # Weighted combination
            quality_score = 0.6 * base_score + 0.4 * property_score
        else:
            quality_score = base_score
        
        return min(100.0, max(0.0, quality_score))
    
    def _assess_risk_factors(self, process_data: Dict[str, Any], predictions: Dict[str, Any]) -> List[str]:
        """Assess potential risk factors affecting rebar quality"""
        risk_factors = []
        
        # Chemical composition risks
        carbon = process_data.get('carbon_content', 0.25)
        if carbon > 0.30:
            risk_factors.append("High carbon content - may affect weldability")
        if carbon < 0.15:
            risk_factors.append("Low carbon content - may affect strength")
        
        phosphorus = process_data.get('phosphorus_content', 0.03)
        if phosphorus > 0.040:
            risk_factors.append("High phosphorus - may cause brittleness")
        
        sulfur = process_data.get('sulfur_content', 0.04)
        if sulfur > 0.050:
            risk_factors.append("High sulfur - may affect ductility")
        
        # Process parameter risks
        billet_temp = process_data.get('billet_temperature', 1050)
        if billet_temp < 1000 or billet_temp > 1200:
            risk_factors.append("Billet temperature out of optimal range")
        
        quench_rate = process_data.get('quench_rate', 50)
        if quench_rate < 30:
            risk_factors.append("Slow quench rate - may not achieve target hardness")
        
        # Mechanical property risks
        if predictions['predicted_yield_strength'] < 400:  # Minimum for most grades
            risk_factors.append("Low predicted yield strength")
        
        if predictions['predicted_elongation'] < 8:  # Below ASTM minimum
            risk_factors.append("Low predicted elongation - ductility concern")
        
        # Quality score risk
        if predictions['quality_score'] < 85:
            risk_factors.append("Overall quality score below target")
        
        return risk_factors
    
    def batch_predict(self, data: pd.DataFrame) -> pd.DataFrame:
        """Predict quality for a batch of production data"""
        if not self.models_trained:
            raise ValueError("Models must be trained before prediction")
        
        X = self.preprocess_features(data)
        X_scaled = self.scaler.transform(X)
        
        # Batch predictions
        compliance_probs = self.compliance_model.predict_proba(X_scaled)[:, 1]
        compliance_preds = self.compliance_model.predict(X_scaled)
        
        yield_preds = self.yield_strength_model.predict(X_scaled)
        tensile_preds = self.tensile_strength_model.predict(X_scaled)
        elongation_preds = self.elongation_model.predict(X_scaled)
        
        # Create results dataframe
        results = data.copy()
        results['astm_compliant'] = compliance_preds
        results['compliance_confidence'] = compliance_probs
        results['predicted_yield_strength'] = yield_preds
        results['predicted_tensile_strength'] = tensile_preds
        results['predicted_elongation'] = elongation_preds
        
        # Calculate quality scores
        quality_scores = []
        for idx, row in results.iterrows():
            grade = row.get('grade', 'Grade60')
            predictions = {
                'compliance_confidence': row['compliance_confidence'],
                'predicted_yield_strength': row['predicted_yield_strength'],
                'predicted_tensile_strength': row['predicted_tensile_strength'],
                'predicted_elongation': row['predicted_elongation']
            }
            quality_score = self._calculate_quality_score(predictions, grade)
            quality_scores.append(quality_score)
        
        results['quality_score'] = quality_scores
        
        return results
    
    def save_model(self, path: str):
        """Save all trained models to disk"""
        model_data = {
            'compliance_model': self.compliance_model,
            'yield_strength_model': self.yield_strength_model,
            'tensile_strength_model': self.tensile_strength_model,
            'elongation_model': self.elongation_model,
            'scaler': self.scaler,
            'grade_encoder': self.grade_encoder,
            'size_encoder': self.size_encoder,
            'feature_names': self.feature_names,
            'models_trained': self.models_trained,
            'astm_specs': self.astm_specs
        }
        joblib.dump(model_data, f"{path}/rebar_quality_prediction_model.pkl")
        logger.info(f"Rebar quality models saved to {path}")
    
    def load_model(self, path: str):
        """Load all trained models from disk"""
        model_data = joblib.load(f"{path}/rebar_quality_prediction_model.pkl")
        
        self.compliance_model = model_data['compliance_model']
        self.yield_strength_model = model_data['yield_strength_model']
        self.tensile_strength_model = model_data['tensile_strength_model']
        self.elongation_model = model_data['elongation_model']
        self.scaler = model_data['scaler']
        self.grade_encoder = model_data['grade_encoder']
        self.size_encoder = model_data['size_encoder']
        self.feature_names = model_data['feature_names']
        self.models_trained = model_data['models_trained']
        self.astm_specs = model_data['astm_specs']
        
        logger.info(f"Rebar quality models loaded from {path}")
    
    def generate_synthetic_training_data(self, n_samples: int = 10000) -> pd.DataFrame:
        """Generate synthetic training data for rebar quality prediction"""
        np.random.seed(42)
        
        data = {}
        
        # Grade distribution (more Grade60 as it's most common)
        grades = np.random.choice(['Grade40', 'Grade60', 'Grade75', 'Grade80'], 
                                 n_samples, p=[0.1, 0.6, 0.2, 0.1])
        data['grade'] = grades
        
        # Size distribution
        sizes = np.random.choice(['Size3', 'Size4', 'Size5', 'Size6', 'Size7', 'Size8', 'Size9', 'Size10'], 
                                n_samples, p=[0.1, 0.15, 0.2, 0.2, 0.15, 0.1, 0.05, 0.05])
        data['size'] = sizes
        
        # Chemical composition with realistic ranges
        data['carbon_content'] = np.random.normal(0.25, 0.05, n_samples)
        data['manganese_content'] = np.random.normal(1.2, 0.2, n_samples)
        data['phosphorus_content'] = np.random.normal(0.03, 0.01, n_samples)
        data['sulfur_content'] = np.random.normal(0.04, 0.01, n_samples)
        data['silicon_content'] = np.random.normal(0.3, 0.1, n_samples)
        data['nitrogen_content'] = np.random.normal(0.012, 0.003, n_samples)
        
        # Process parameters with realistic ranges
        data['billet_temperature'] = np.random.normal(1050, 50, n_samples)
        data['rolling_speed'] = np.random.normal(8.0, 1.5, n_samples)
        data['total_reduction'] = np.random.normal(85, 5, n_samples)
        data['final_diameter'] = np.random.normal(16, 8, n_samples)
        data['rib_height'] = np.random.normal(0.7, 0.15, n_samples)
        data['rib_spacing'] = np.random.normal(11.2, 2, n_samples)
        data['rib_consistency'] = np.random.normal(95, 3, n_samples)
        
        # Heat treatment parameters
        data['austenitizing_temp'] = np.random.normal(900, 30, n_samples)
        data['quench_rate'] = np.random.normal(50, 10, n_samples)
        data['tempering_temp'] = np.random.normal(600, 50, n_samples)
        data['cooling_rate'] = np.random.normal(25, 5, n_samples)
        
        # Other process parameters
        data['casting_temperature'] = np.random.normal(1520, 30, n_samples)
        data['ladle_treatment_time'] = np.random.normal(15, 3, n_samples)
        data['straightness'] = np.random.normal(3, 1, n_samples)
        data['surface_quality_score'] = np.random.normal(90, 5, n_samples)
        data['dimensional_accuracy'] = np.random.normal(95, 3, n_samples)
        data['furnace_atmosphere'] = np.random.normal(0.8, 0.1, n_samples)
        data['roll_gap_accuracy'] = np.random.normal(98, 1, n_samples)
        data['cutting_precision'] = np.random.normal(99, 1, n_samples)
        
        # Generate target variables based on process parameters
        df = pd.DataFrame(data)
        
        # ASTM compliance based on chemical composition and process parameters
        compliance_scores = (
            (df['carbon_content'] <= 0.30) * 0.2 +
            (df['phosphorus_content'] <= 0.040) * 0.2 +
            (df['sulfur_content'] <= 0.050) * 0.2 +
            (df['billet_temperature'] >= 1000) * 0.1 +
            (df['surface_quality_score'] >= 85) * 0.1 +
            (df['dimensional_accuracy'] >= 90) * 0.2
        )
        
        df['astm_compliant'] = (compliance_scores + np.random.normal(0, 0.1, n_samples) > 0.7).astype(int)
        
        # Mechanical properties based on grade and process parameters
        grade_strength_map = {'Grade40': 275, 'Grade60': 420, 'Grade75': 520, 'Grade80': 550}
        base_yield = df['grade'].map(grade_strength_map)
        
        # Add process-based variations
        process_factor = (
            df['quench_rate'] / 50 * 0.1 +
            df['tempering_temp'] / 600 * (-0.1) +  # Higher tempering reduces strength
            df['carbon_content'] / 0.25 * 0.2
        )
        
        df['actual_yield_strength'] = base_yield * (1 + process_factor) + np.random.normal(0, 20, n_samples)
        df['actual_tensile_strength'] = df['actual_yield_strength'] * 1.4 + np.random.normal(0, 30, n_samples)
        df['actual_elongation'] = 15 - (df['actual_yield_strength'] - 400) / 100 + np.random.normal(0, 1, n_samples)
        
        # Ensure realistic ranges
        df['actual_elongation'] = np.clip(df['actual_elongation'], 5, 20)
        
        return df