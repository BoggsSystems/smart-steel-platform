"""
Unit Tests for Rebar Defect Detection Model
Tests 15 types of rebar defects with severity assessment and root cause analysis
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

from models.rebar_defect_detection import RebarDefectDetectionModel
from tests import TEST_CONFIG


class TestRebarDefectDetectionModel(unittest.TestCase):
    """Test suite for Rebar Defect Detection Model"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.model = RebarDefectDetectionModel()
        cls.config = TEST_CONFIG['models']['defect_detection']
        
        # Generate synthetic training data for testing
        cls.training_data = cls.model.generate_synthetic_training_data(n_samples=1000)
        
        # Define target column for training
        cls.target_column = 'primary_defect'
    
    def setUp(self):
        """Set up test fixtures for each test method"""
        self.test_surface_data = {
            'surface_roughness': 2.5,
            'surface_temperature': 45,
            'surface_oxidation': 0.3,
            'surface_decarburization': 0.1,
            'cooling_rate': 25,
            'scale_thickness': 0.05,
            'pickling_quality': 85
        }
        
        self.test_dimensional_data = {
            'rib_height': 0.7,
            'rib_spacing': 11.2,
            'rib_angle': 45,
            'rib_fill': 90,
            'diameter_deviation': 0.1,
            'straightness': 2.5,
            'ovality': 0.8,
            'length_accuracy': 99.5
        }
    
    def test_model_initialization(self):
        """Test that model initializes correctly"""
        model = RebarDefectDetectionModel()
        
        # Check that all models are initialized
        self.assertIsNotNone(model.surface_defect_model)
        self.assertIsNotNone(model.dimensional_defect_model)
        self.assertIsNotNone(model.severity_model)
        
        # Check that scalers are initialized
        self.assertIsNotNone(model.surface_scaler)
        self.assertIsNotNone(model.dimensional_scaler)
        
        # Check defect types are loaded
        expected_defects = [
            'no_defect', 'surface_crack', 'surface_fold', 'surface_pit',
            'rib_height_low', 'rib_height_high', 'rib_spacing_error',
            'rib_angle_error', 'rib_fill_poor', 'dimensional_error',
            'straightness_error', 'ovality_error', 'surface_scale',
            'inclusion', 'mechanical_damage'
        ]
        self.assertEqual(len(model.defect_types), len(expected_defects))
        for defect in expected_defects:
            self.assertIn(defect, model.defect_types)
        
        # Check that model is not trained initially
        self.assertFalse(model.models_trained)
    
    def test_synthetic_data_generation(self):
        """Test synthetic defect data generation"""
        # Test data generation
        data = self.model.generate_synthetic_training_data(n_samples=1000)
        
        # Check data structure
        self.assertEqual(len(data), 1000)
        self.assertIn('primary_defect', data.columns)
        self.assertIn('severity', data.columns)
        self.assertIn('surface_roughness', data.columns)
        self.assertIn('rib_height', data.columns)
        
        # Check defect distribution (no_defect should be most common)
        defect_counts = data['primary_defect'].value_counts()
        self.assertTrue(defect_counts['no_defect'] > defect_counts.get('surface_crack', 0))
        
        # Check severity distribution
        severity_counts = data['severity'].value_counts()
        self.assertIn('low', severity_counts.index)
        self.assertIn('medium', severity_counts.index)
        self.assertIn('high', severity_counts.index)
        
        # Check feature ranges
        self.assertTrue(data['surface_roughness'].between(0.5, 10.0).all())
        self.assertTrue(data['rib_height'].between(0.3, 1.2).all())
        self.assertTrue(data['rib_spacing'].between(8.0, 15.0).all())
    
    def test_feature_extraction(self):
        """Test surface and dimensional feature extraction"""
        # Test surface feature extraction
        test_data = pd.DataFrame([self.test_surface_data])
        surface_features = self.model.extract_surface_features(test_data)
        
        expected_surface_features = len(self.model.surface_feature_names)
        self.assertEqual(surface_features.shape[1], expected_surface_features)
        self.assertEqual(surface_features.shape[0], 1)
        
        # Test dimensional feature extraction
        test_data = pd.DataFrame([self.test_dimensional_data])
        dimensional_features = self.model.extract_dimensional_features(test_data)
        
        expected_dimensional_features = len(self.model.dimensional_feature_names)
        self.assertEqual(dimensional_features.shape[1], expected_dimensional_features)
        self.assertEqual(dimensional_features.shape[0], 1)
        
        # Test with missing features
        incomplete_data = pd.DataFrame([{'surface_roughness': 2.5}])
        surface_features_incomplete = self.model.extract_surface_features(incomplete_data)
        self.assertEqual(surface_features_incomplete.shape[1], expected_surface_features)
    
    def test_defect_classification_rules(self):
        """Test rule-based defect classification logic"""
        # Test surface crack detection
        crack_data = {
            'surface_roughness': 8.0,  # High roughness
            'surface_temperature': 60,  # High temperature
            'cooling_rate': 50,  # Fast cooling
            'scale_thickness': 0.2  # Thick scale
        }
        
        defect_type = self.model._classify_surface_defect(crack_data)
        self.assertIn(defect_type, ['surface_crack', 'surface_scale', 'surface_fold'])
        
        # Test dimensional error detection
        dimensional_error_data = {
            'rib_height': 0.3,  # Too low
            'rib_spacing': 15.5,  # Too high
            'diameter_deviation': 0.5,  # Too high
            'ovality': 2.0  # Too oval
        }
        
        defect_type = self.model._classify_dimensional_defect(dimensional_error_data)
        self.assertIn(defect_type, ['rib_height_low', 'rib_spacing_error', 'dimensional_error', 'ovality_error'])
    
    def test_severity_assessment(self):
        """Test defect severity assessment"""
        # Test high severity conditions
        high_severity_data = {
            'surface_roughness': 9.0,
            'diameter_deviation': 0.8,
            'straightness': 8.0,
            'rib_fill': 60
        }
        
        severity = self.model._assess_severity('surface_crack', high_severity_data)
        self.assertIn(severity, ['medium', 'high'])
        
        # Test low severity conditions
        low_severity_data = {
            'surface_roughness': 1.5,
            'diameter_deviation': 0.05,
            'straightness': 1.0,
            'rib_fill': 95
        }
        
        severity = self.model._assess_severity('surface_scale', low_severity_data)
        self.assertIn(severity, ['low', 'medium'])
    
    def test_model_training(self):
        """Test defect detection model training"""
        # Train the model
        results = self.model.train(self.training_data, self.target_column)
        
        # Check that training completed
        self.assertTrue(self.model.models_trained)
        
        # Check training results
        self.assertIn('surface_defect_accuracy', results)
        self.assertIn('dimensional_defect_accuracy', results)
        self.assertIn('severity_accuracy', results)
        
        # Validate accuracy targets
        surface_accuracy = results['surface_defect_accuracy']
        dimensional_accuracy = results['dimensional_defect_accuracy']
        severity_accuracy = results['severity_accuracy']
        
        # Minimum acceptable accuracies
        self.assertGreaterEqual(surface_accuracy, 0.7)
        self.assertGreaterEqual(dimensional_accuracy, 0.7) 
        self.assertGreaterEqual(severity_accuracy, 0.6)
        
        # Check confusion matrices are calculated
        self.assertIn('surface_confusion_matrix', results)
        self.assertIn('dimensional_confusion_matrix', results)
        
        # Check feature importance
        self.assertIn('surface_feature_importance', results)
        self.assertIn('dimensional_feature_importance', results)
    
    def test_single_defect_detection(self):
        """Test single defect detection prediction"""
        # Train model first
        self.model.train(self.training_data, self.target_column)
        
        # Combine test data
        combined_data = {**self.test_surface_data, **self.test_dimensional_data}
        
        # Test detection
        detection = self.model.detect_defects(combined_data)
        
        # Check detection structure
        required_keys = [
            'primary_defect', 'defect_probability', 'severity',
            'confidence', 'affected_areas', 'root_cause_analysis',
            'corrective_actions', 'quality_impact'
        ]
        
        for key in required_keys:
            self.assertIn(key, detection)
        
        # Validate detection types and ranges
        self.assertIn(detection['primary_defect'], self.model.defect_types)
        self.assertTrue(0 <= detection['defect_probability'] <= 1)
        self.assertIn(detection['severity'], ['low', 'medium', 'high'])
        self.assertTrue(0 <= detection['confidence'] <= 1)
        self.assertIsInstance(detection['affected_areas'], list)
        self.assertIsInstance(detection['root_cause_analysis'], list)
        self.assertIsInstance(detection['corrective_actions'], list)
    
    def test_batch_defect_detection(self):
        """Test batch defect detection"""
        # Train model first
        self.model.train(self.training_data, self.target_column)
        
        # Create batch test data
        combined_data = {**self.test_surface_data, **self.test_dimensional_data}
        batch_data = pd.DataFrame([combined_data] * 10)
        
        # Add some variation
        batch_data['surface_roughness'] = np.random.normal(2.5, 0.5, 10)
        batch_data['rib_height'] = np.random.normal(0.7, 0.1, 10)
        
        # Test batch detection
        results = self.model.batch_detect(batch_data)
        
        # Check results structure
        self.assertEqual(len(results), 10)
        self.assertIn('primary_defect', results.columns)
        self.assertIn('severity', results.columns)
        self.assertIn('defect_probability', results.columns)
        
        # Validate batch results
        self.assertTrue(results['defect_probability'].between(0, 1).all())
        self.assertTrue(results['severity'].isin(['low', 'medium', 'high']).all())
    
    def test_defect_type_specific_detection(self):
        """Test detection of specific defect types"""
        # Train model first
        self.model.train(self.training_data, self.target_column)
        
        # Test surface crack conditions
        crack_conditions = {
            'surface_roughness': 8.0,
            'surface_temperature': 65,
            'cooling_rate': 60,
            'scale_thickness': 0.15,
            **self.test_dimensional_data
        }
        
        detection = self.model.detect_defects(crack_conditions)
        # Should detect some form of surface defect
        self.assertIn(detection['primary_defect'], 
                     ['surface_crack', 'surface_scale', 'surface_fold', 'no_defect'])
        
        # Test rib height issues
        rib_issues = {
            **self.test_surface_data,
            'rib_height': 0.3,  # Too low
            'rib_spacing': 15.5,  # Too wide
            'rib_fill': 70  # Poor fill
        }
        
        detection = self.model.detect_defects(rib_issues)
        # Should detect dimensional defects
        self.assertIn(detection['primary_defect'], 
                     ['rib_height_low', 'rib_spacing_error', 'rib_fill_poor', 'no_defect'])
    
    def test_root_cause_analysis(self):
        """Test root cause analysis generation"""
        # Test with surface defect data
        surface_defect_data = {
            'surface_roughness': 7.0,
            'surface_temperature': 70,
            'cooling_rate': 80,
            'scale_thickness': 0.2,
            **self.test_dimensional_data
        }
        
        root_causes = self.model._generate_root_cause_analysis('surface_crack', surface_defect_data)
        
        # Should identify relevant root causes
        self.assertIsInstance(root_causes, list)
        self.assertGreater(len(root_causes), 0)
        
        # Check for specific root cause patterns
        root_cause_text = ' '.join(root_causes).lower()
        self.assertTrue(any(term in root_cause_text for term in 
                          ['temperature', 'cooling', 'scale', 'surface']))
    
    def test_corrective_actions(self):
        """Test corrective action recommendations"""
        # Test corrective actions for different defect types
        defect_types = ['surface_crack', 'rib_height_low', 'surface_scale', 'dimensional_error']
        
        for defect_type in defect_types:
            actions = self.model._recommend_corrective_actions(defect_type, 'high')
            
            self.assertIsInstance(actions, list)
            self.assertGreater(len(actions), 0)
            
            # Actions should be relevant to defect type
            action_text = ' '.join(actions).lower()
            if 'surface' in defect_type:
                self.assertTrue(any(term in action_text for term in 
                              ['temperature', 'cooling', 'scale', 'surface']))
            elif 'rib' in defect_type:
                self.assertTrue(any(term in action_text for term in 
                              ['roll', 'gap', 'pressure', 'calibration']))
    
    def test_quality_impact_assessment(self):
        """Test quality impact assessment"""
        impact_scores = {}
        
        # Test different defect types and severities
        test_cases = [
            ('no_defect', 'low'),
            ('surface_crack', 'high'),
            ('rib_height_low', 'medium'),
            ('dimensional_error', 'high')
        ]
        
        for defect_type, severity in test_cases:
            impact = self.model._assess_quality_impact(defect_type, severity)
            impact_scores[(defect_type, severity)] = impact
            
            # Impact should be between 0 and 100
            self.assertTrue(0 <= impact <= 100)
        
        # High severity defects should have higher impact
        self.assertGreater(
            impact_scores[('surface_crack', 'high')],
            impact_scores[('no_defect', 'low')]
        )
    
    def test_confidence_calculation(self):
        """Test detection confidence calculation"""
        # Train model first
        self.model.train(self.training_data, self.target_column)
        
        # Test with clear defect indicators
        clear_defect_data = {
            'surface_roughness': 9.0,  # Very rough
            'rib_height': 0.2,  # Very low
            'diameter_deviation': 0.9,  # Very high
            **self.test_surface_data,
            **self.test_dimensional_data
        }
        
        detection = self.model.detect_defects(clear_defect_data)
        high_confidence = detection['confidence']
        
        # Test with ambiguous data
        ambiguous_data = {
            'surface_roughness': 3.0,  # Moderate
            'rib_height': 0.65,  # Slightly low
            'diameter_deviation': 0.15,  # Slightly high
            **self.test_surface_data,
            **self.test_dimensional_data
        }
        
        detection = self.model.detect_defects(ambiguous_data)
        moderate_confidence = detection['confidence']
        
        # Clear cases should have higher confidence
        self.assertGreaterEqual(high_confidence, moderate_confidence)
    
    def test_defect_threshold_tuning(self):
        """Test defect detection threshold adjustment"""
        # Test conservative thresholds (fewer false positives)
        conservative_thresholds = {
            'surface_defect_threshold': 0.8,
            'dimensional_defect_threshold': 0.8,
            'severity_threshold': 0.7
        }
        
        self.model.update_detection_thresholds(conservative_thresholds)
        
        # Check thresholds are updated
        self.assertEqual(self.model.surface_defect_threshold, 0.8)
        self.assertEqual(self.model.dimensional_defect_threshold, 0.8)
        self.assertEqual(self.model.severity_threshold, 0.7)
        
        # Test sensitive thresholds (fewer false negatives)
        sensitive_thresholds = {
            'surface_defect_threshold': 0.3,
            'dimensional_defect_threshold': 0.3,
            'severity_threshold': 0.3
        }
        
        self.model.update_detection_thresholds(sensitive_thresholds)
        self.assertEqual(self.model.surface_defect_threshold, 0.3)
    
    def test_edge_cases(self):
        """Test model behavior with edge cases"""
        # Train model first
        self.model.train(self.training_data, self.target_column)
        
        # Test with extreme values
        extreme_data = {
            'surface_roughness': 15.0,  # Very high
            'rib_height': 0.1,  # Very low
            'diameter_deviation': 2.0,  # Very high
            'straightness': 15.0  # Very poor
        }
        
        # Should not crash
        detection = self.model.detect_defects(extreme_data)
        self.assertIn('primary_defect', detection)
        
        # Test with all zero values
        zero_data = {key: 0.0 for key in self.test_surface_data.keys()}
        zero_data.update({key: 0.0 for key in self.test_dimensional_data.keys()})
        
        detection = self.model.detect_defects(zero_data)
        self.assertIn('primary_defect', detection)
    
    def test_performance_requirements(self):
        """Test that model meets performance requirements"""
        # Train model first
        self.model.train(self.training_data, self.target_column)
        
        # Test single detection performance
        combined_data = {**self.test_surface_data, **self.test_dimensional_data}
        
        start_time = time.time()
        detection = self.model.detect_defects(combined_data)
        single_detection_time = (time.time() - start_time) * 1000  # Convert to ms
        
        # Should meet response time target
        max_response_time = self.config['response_time_ms']
        self.assertLess(single_detection_time, max_response_time,
                       f"Single detection took {single_detection_time:.2f}ms, target is {max_response_time}ms")
        
        # Test batch detection performance
        batch_data = pd.DataFrame([combined_data] * 100)
        
        start_time = time.time()
        batch_results = self.model.batch_detect(batch_data)
        batch_detection_time = (time.time() - start_time) * 1000
        
        # Batch should be efficient
        avg_per_item = batch_detection_time / 100
        self.assertLess(avg_per_item, 100,  # 100ms per item in batch
                       f"Batch detection took {avg_per_item:.2f}ms per item")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)