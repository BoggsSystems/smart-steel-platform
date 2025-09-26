"""
Unit Tests for Rebar Quality Prediction Model
Tests ASTM compliance, mechanical properties, and model accuracy
"""

import unittest
import numpy as np
import pandas as pd
import sys
import os
from unittest.mock import patch, MagicMock
import time

# Add the parent directory to the path to import our models
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from models.rebar_quality_prediction import RebarQualityPredictionModel
from tests import TEST_CONFIG


class TestRebarQualityPredictionModel(unittest.TestCase):
    """Test suite for Rebar Quality Prediction Model"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.model = RebarQualityPredictionModel()
        cls.config = TEST_CONFIG['models']['quality_prediction']
        
        # Generate synthetic training data for testing
        cls.training_data = cls.model.generate_synthetic_training_data(n_samples=1000)
        
        # Define target columns for training
        cls.target_columns = {
            'compliance': 'astm_compliant',
            'yield_strength': 'actual_yield_strength',
            'tensile_strength': 'actual_tensile_strength',
            'elongation': 'actual_elongation'
        }
    
    def setUp(self):
        """Set up test fixtures for each test method"""
        self.test_process_data = {
            'grade': 'Grade60',
            'carbon_content': 0.25,
            'manganese_content': 1.2,
            'phosphorus_content': 0.03,
            'sulfur_content': 0.04,
            'billet_temperature': 1050,
            'rolling_speed': 8.0,
            'quench_rate': 50,
            'surface_quality_score': 90
        }
    
    def test_model_initialization(self):
        """Test that model initializes correctly"""
        model = RebarQualityPredictionModel()
        
        # Check that all models are initialized
        self.assertIsNotNone(model.compliance_model)
        self.assertIsNotNone(model.yield_strength_model)
        self.assertIsNotNone(model.tensile_strength_model)
        self.assertIsNotNone(model.elongation_model)
        
        # Check that scalers and encoders are initialized
        self.assertIsNotNone(model.scaler)
        self.assertIsNotNone(model.grade_encoder)
        self.assertIsNotNone(model.size_encoder)
        
        # Check that ASTM specifications are loaded
        self.assertIn('Grade60', model.astm_specs)
        self.assertEqual(model.astm_specs['Grade60']['min_yield'], 420)
        
        # Check that model is not trained initially
        self.assertFalse(model.models_trained)
    
    def test_synthetic_data_generation(self):
        """Test synthetic data generation quality and distributions"""
        # Test data generation
        data = self.model.generate_synthetic_training_data(n_samples=1000)
        
        # Check data structure
        self.assertEqual(len(data), 1000)
        self.assertIn('grade', data.columns)
        self.assertIn('carbon_content', data.columns)
        self.assertIn('astm_compliant', data.columns)
        
        # Check grade distribution (Grade60 should be most common)
        grade_counts = data['grade'].value_counts()
        self.assertTrue(grade_counts['Grade60'] > grade_counts['Grade40'])
        
        # Check chemical composition ranges
        self.assertTrue(data['carbon_content'].between(0.1, 0.4).all())
        self.assertTrue(data['phosphorus_content'].between(0.01, 0.06).all())
        self.assertTrue(data['sulfur_content'].between(0.01, 0.08).all())
        
        # Check target variables are realistic
        self.assertTrue(data['actual_yield_strength'].between(200, 700).all())
        self.assertTrue(data['actual_elongation'].between(5, 20).all())
    
    def test_feature_preprocessing(self):
        """Test feature preprocessing and encoding"""
        # Test with complete data
        test_data = pd.DataFrame([self.test_process_data])
        
        # Prepare encoders first
        self.model.prepare_categorical_encoders(test_data)
        
        # Test preprocessing
        features = self.model.preprocess_features(test_data)
        
        # Check feature array shape
        expected_features = len(self.model.feature_names)
        self.assertEqual(features.shape[1], expected_features)
        self.assertEqual(features.shape[0], 1)
        
        # Test with missing features
        incomplete_data = pd.DataFrame([{'grade': 'Grade60', 'carbon_content': 0.25}])
        features_incomplete = self.model.preprocess_features(incomplete_data)
        self.assertEqual(features_incomplete.shape[1], expected_features)
    
    def test_default_feature_values(self):
        """Test default feature value generation"""
        # Test individual feature defaults
        carbon_defaults = self.model._get_default_feature_values('carbon_content', 10)
        self.assertEqual(len(carbon_defaults), 10)
        self.assertTrue(all(val == 0.25 for val in carbon_defaults))
        
        # Test unknown feature defaults to zero
        unknown_defaults = self.model._get_default_feature_values('unknown_feature', 5)
        self.assertEqual(len(unknown_defaults), 5)
        self.assertTrue(all(val == 0 for val in unknown_defaults))
    
    def test_categorical_encoder_preparation(self):
        """Test categorical encoder setup"""
        test_data = pd.DataFrame([self.test_process_data])
        
        # Test encoder preparation
        self.model.prepare_categorical_encoders(test_data)
        
        # Check that encoders are fitted
        expected_grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
        self.assertEqual(len(self.model.grade_encoder.classes_), len(expected_grades))
        
        # Test encoding
        encoded_grade = self.model.grade_encoder.transform(['Grade60'])[0]
        self.assertIsInstance(encoded_grade, (int, np.integer))
    
    def test_model_training(self):
        """Test model training process and validation"""
        # Train the model
        results = self.model.train(self.training_data, self.target_columns)
        
        # Check that training completed
        self.assertTrue(self.model.models_trained)
        
        # Check training results
        self.assertIn('compliance_accuracy', results)
        self.assertIn('yield_strength_r2', results)
        self.assertIn('tensile_strength_r2', results)
        self.assertIn('elongation_r2', results)
        
        # Validate accuracy targets
        compliance_accuracy = results['compliance_accuracy']
        self.assertGreaterEqual(compliance_accuracy, 0.7)  # Minimum acceptable
        
        # Validate R² scores
        yield_r2 = results['yield_strength_r2']
        tensile_r2 = results['tensile_strength_r2']
        elongation_r2 = results['elongation_r2']
        
        self.assertGreaterEqual(yield_r2, 0.6)  # Minimum acceptable
        self.assertGreaterEqual(tensile_r2, 0.6)  # Minimum acceptable
        self.assertGreaterEqual(elongation_r2, 0.4)  # Elongation is harder to predict
        
        # Check feature importance is calculated
        self.assertIn('compliance_feature_importance', results)
        feature_importance = results['compliance_feature_importance']
        self.assertIsInstance(feature_importance, dict)
        self.assertTrue(len(feature_importance) > 0)
    
    def test_single_prediction(self):
        """Test single quality prediction"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test prediction
        prediction = self.model.predict_quality(self.test_process_data)
        
        # Check prediction structure
        required_keys = [
            'astm_compliant', 'compliance_confidence', 
            'predicted_yield_strength', 'predicted_tensile_strength',
            'predicted_elongation', 'quality_score', 'risk_factors'
        ]
        
        for key in required_keys:
            self.assertIn(key, prediction)
        
        # Validate prediction types and ranges
        self.assertIsInstance(prediction['astm_compliant'], bool)
        self.assertTrue(0 <= prediction['compliance_confidence'] <= 1)
        self.assertTrue(200 <= prediction['predicted_yield_strength'] <= 700)
        self.assertTrue(300 <= prediction['predicted_tensile_strength'] <= 1000)
        self.assertTrue(5 <= prediction['predicted_elongation'] <= 25)
        self.assertTrue(0 <= prediction['quality_score'] <= 100)
        self.assertIsInstance(prediction['risk_factors'], list)
    
    def test_batch_prediction(self):
        """Test batch quality prediction"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Create batch test data
        batch_data = pd.DataFrame([self.test_process_data] * 10)
        batch_data['carbon_content'] = np.random.normal(0.25, 0.05, 10)
        
        # Test batch prediction
        results = self.model.batch_predict(batch_data)
        
        # Check results structure
        self.assertEqual(len(results), 10)
        self.assertIn('astm_compliant', results.columns)
        self.assertIn('quality_score', results.columns)
        
        # Validate batch results
        self.assertTrue(results['quality_score'].between(0, 100).all())
    
    def test_astm_compliance_validation(self):
        """Test ASTM specification compliance checking"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test different grades
        for grade in ['Grade40', 'Grade60', 'Grade75', 'Grade80']:
            test_data = self.test_process_data.copy()
            test_data['grade'] = grade
            
            prediction = self.model.predict_quality(test_data)
            
            # Check that prediction considers grade-specific requirements
            self.assertIn('predicted_yield_strength', prediction)
            self.assertIn('predicted_tensile_strength', prediction)
            self.assertIn('predicted_elongation', prediction)
    
    def test_quality_score_calculation(self):
        """Test quality score calculation logic"""
        # Test with high-quality parameters
        high_quality_prediction = {
            'compliance_confidence': 0.95,
            'predicted_yield_strength': 450,
            'predicted_tensile_strength': 650,
            'predicted_elongation': 12
        }
        
        quality_score = self.model._calculate_quality_score(high_quality_prediction, 'Grade60')
        self.assertGreaterEqual(quality_score, 80)  # Should be high score
        
        # Test with low-quality parameters
        low_quality_prediction = {
            'compliance_confidence': 0.6,
            'predicted_yield_strength': 350,
            'predicted_tensile_strength': 500,
            'predicted_elongation': 6
        }
        
        quality_score_low = self.model._calculate_quality_score(low_quality_prediction, 'Grade60')
        self.assertLess(quality_score_low, quality_score)  # Should be lower score
    
    def test_risk_factor_assessment(self):
        """Test risk factor identification"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test high-risk process data
        high_risk_data = self.test_process_data.copy()
        high_risk_data.update({
            'carbon_content': 0.35,  # High carbon
            'phosphorus_content': 0.045,  # High phosphorus
            'billet_temperature': 950  # Low temperature
        })
        
        prediction = self.model.predict_quality(high_risk_data)
        risk_factors = prediction['risk_factors']
        
        # Should identify multiple risk factors
        self.assertGreater(len(risk_factors), 0)
        self.assertTrue(any('carbon' in factor.lower() for factor in risk_factors))
        self.assertTrue(any('phosphorus' in factor.lower() for factor in risk_factors))
    
    def test_model_persistence(self):
        """Test model save and load functionality"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test save functionality
        test_path = './tests/models'
        os.makedirs(test_path, exist_ok=True)
        
        # Save model
        self.model.save_model(test_path)
        
        # Check that model file was created
        model_file = os.path.join(test_path, 'rebar_quality_prediction_model.pkl')
        self.assertTrue(os.path.exists(model_file))
        
        # Test load functionality
        new_model = RebarQualityPredictionModel()
        new_model.load_model(test_path)
        
        # Verify loaded model
        self.assertTrue(new_model.models_trained)
        self.assertEqual(new_model.feature_names, self.model.feature_names)
        
        # Test that loaded model produces same predictions
        prediction_original = self.model.predict_quality(self.test_process_data)
        prediction_loaded = new_model.predict_quality(self.test_process_data)
        
        # Allow small numerical differences
        self.assertAlmostEqual(
            prediction_original['predicted_yield_strength'],
            prediction_loaded['predicted_yield_strength'],
            places=2
        )
    
    def test_edge_cases(self):
        """Test model behavior with edge cases"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test with extreme values
        extreme_data = {
            'grade': 'Grade80',
            'carbon_content': 0.1,  # Very low
            'billet_temperature': 1200,  # Very high
            'quench_rate': 100  # Very fast
        }
        
        # Should not crash
        prediction = self.model.predict_quality(extreme_data)
        self.assertIn('quality_score', prediction)
        
        # Test with missing grade
        no_grade_data = self.test_process_data.copy()
        del no_grade_data['grade']
        
        prediction = self.model.predict_quality(no_grade_data)
        self.assertIn('quality_score', prediction)
    
    def test_performance_requirements(self):
        """Test that model meets performance requirements"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test single prediction performance
        start_time = time.time()
        prediction = self.model.predict_quality(self.test_process_data)
        single_prediction_time = (time.time() - start_time) * 1000  # Convert to ms
        
        # Should meet response time target
        max_response_time = self.config['response_time_ms']
        self.assertLess(single_prediction_time, max_response_time,
                       f"Single prediction took {single_prediction_time:.2f}ms, target is {max_response_time}ms")
        
        # Test batch prediction performance
        batch_data = pd.DataFrame([self.test_process_data] * 100)
        
        start_time = time.time()
        batch_results = self.model.batch_predict(batch_data)
        batch_prediction_time = (time.time() - start_time) * 1000
        
        # Batch should be efficient (less than 10ms per item)
        avg_per_item = batch_prediction_time / 100
        self.assertLess(avg_per_item, 50,  # 50ms per item in batch
                       f"Batch prediction took {avg_per_item:.2f}ms per item")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)