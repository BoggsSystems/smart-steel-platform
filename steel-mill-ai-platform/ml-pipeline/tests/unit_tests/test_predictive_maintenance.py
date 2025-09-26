"""
Unit Tests for Rebar Predictive Maintenance Model
Tests equipment failure prediction, RUL estimation, and maintenance scheduling
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

from models.rebar_predictive_maintenance import RebarPredictiveMaintenanceModel
from tests import TEST_CONFIG


class TestRebarPredictiveMaintenanceModel(unittest.TestCase):
    """Test suite for Rebar Predictive Maintenance Model"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.model = RebarPredictiveMaintenanceModel()
        cls.config = TEST_CONFIG['models']['predictive_maintenance']
        
        # Generate synthetic training data for testing
        cls.training_data = cls.model.generate_synthetic_training_data(n_samples=1000)
        
        # Define target columns for training
        cls.target_columns = {
            'failure_prediction': 'failure_within_days',
            'rul_estimation': 'remaining_useful_life',
            'maintenance_type': 'recommended_maintenance'
        }
    
    def setUp(self):
        """Set up test fixtures for each test method"""
        self.test_rolling_mill_data = {
            'equipment_type': 'rolling_mill',
            'operating_hours': 2500,
            'vibration_level': 4.5,
            'temperature': 75,
            'power_consumption': 850,
            'load_factor': 0.85,
            'bearing_temperature': 68,
            'oil_pressure': 45,
            'roll_wear': 0.15,
            'roll_gap_accuracy': 98.5
        }
        
        self.test_heat_treatment_data = {
            'equipment_type': 'heat_treatment',
            'operating_hours': 1800,
            'furnace_temperature': 900,
            'heating_rate': 15,
            'cooling_rate': 25,
            'atmosphere_quality': 92,
            'refractory_condition': 88,
            'burner_efficiency': 94,
            'insulation_integrity': 90
        }
        
        self.test_cutting_data = {
            'equipment_type': 'cutting_straightening',
            'operating_hours': 3200,
            'blade_wear': 0.25,
            'cutting_force': 1200,
            'straightening_force': 800,
            'hydraulic_pressure': 180,
            'motor_vibration': 3.2,
            'alignment_accuracy': 99.1
        }
    
    def test_model_initialization(self):
        """Test that model initializes correctly"""
        model = RebarPredictiveMaintenanceModel()
        
        # Check that all equipment-specific models are initialized
        self.assertIn('rolling_mill', model.equipment_models)
        self.assertIn('heat_treatment', model.equipment_models)
        self.assertIn('cutting_straightening', model.equipment_models)
        
        # Check that each equipment model has required components
        for equipment_type in model.equipment_models:
            equipment_model = model.equipment_models[equipment_type]
            self.assertIn('failure_classifier', equipment_model)
            self.assertIn('rul_regressor', equipment_model)
            self.assertIn('scaler', equipment_model)
        
        # Check equipment-specific feature names
        self.assertIn('rolling_mill', model.equipment_features)
        self.assertIn('heat_treatment', model.equipment_features)
        self.assertIn('cutting_straightening', model.equipment_features)
        
        # Check that model is not trained initially
        self.assertFalse(model.models_trained)
    
    def test_synthetic_data_generation(self):
        """Test synthetic maintenance data generation"""
        # Test data generation
        data = self.model.generate_synthetic_training_data(n_samples=1000)
        
        # Check data structure
        self.assertEqual(len(data), 1000)
        self.assertIn('equipment_type', data.columns)
        self.assertIn('failure_within_days', data.columns)
        self.assertIn('remaining_useful_life', data.columns)
        self.assertIn('recommended_maintenance', data.columns)
        
        # Check equipment type distribution
        equipment_counts = data['equipment_type'].value_counts()
        self.assertIn('rolling_mill', equipment_counts.index)
        self.assertIn('heat_treatment', equipment_counts.index)
        self.assertIn('cutting_straightening', equipment_counts.index)
        
        # Check maintenance type distribution
        maintenance_counts = data['recommended_maintenance'].value_counts()
        self.assertIn('preventive', maintenance_counts.index)
        self.assertIn('predictive', maintenance_counts.index)
        self.assertIn('corrective', maintenance_counts.index)
        
        # Check feature ranges
        self.assertTrue(data['operating_hours'].between(0, 10000).all())
        self.assertTrue(data['remaining_useful_life'].between(0, 365).all())
        self.assertTrue(data['vibration_level'].between(0, 20).all())
    
    def test_equipment_specific_features(self):
        """Test equipment-specific feature extraction"""
        # Test rolling mill features
        rolling_data = pd.DataFrame([self.test_rolling_mill_data])
        rolling_features = self.model.extract_equipment_features(rolling_data, 'rolling_mill')
        
        expected_rolling_features = len(self.model.equipment_features['rolling_mill'])
        self.assertEqual(rolling_features.shape[1], expected_rolling_features)
        self.assertEqual(rolling_features.shape[0], 1)
        
        # Test heat treatment features
        heat_data = pd.DataFrame([self.test_heat_treatment_data])
        heat_features = self.model.extract_equipment_features(heat_data, 'heat_treatment')
        
        expected_heat_features = len(self.model.equipment_features['heat_treatment'])
        self.assertEqual(heat_features.shape[1], expected_heat_features)
        
        # Test cutting/straightening features
        cutting_data = pd.DataFrame([self.test_cutting_data])
        cutting_features = self.model.extract_equipment_features(cutting_data, 'cutting_straightening')
        
        expected_cutting_features = len(self.model.equipment_features['cutting_straightening'])
        self.assertEqual(cutting_features.shape[1], expected_cutting_features)
    
    def test_failure_risk_assessment(self):
        """Test failure risk assessment logic"""
        # Test high-risk conditions for rolling mill
        high_risk_rolling = {
            'operating_hours': 8000,  # High hours
            'vibration_level': 12.0,  # High vibration
            'temperature': 95,  # High temperature
            'bearing_temperature': 85,  # Hot bearings
            'roll_wear': 0.45,  # High wear
            'oil_pressure': 25  # Low pressure
        }
        
        risk_score = self.model._assess_failure_risk('rolling_mill', high_risk_rolling)
        self.assertGreater(risk_score, 0.7)  # Should be high risk
        
        # Test low-risk conditions
        low_risk_rolling = {
            'operating_hours': 500,  # Low hours
            'vibration_level': 2.0,  # Low vibration
            'temperature': 55,  # Normal temperature
            'bearing_temperature': 50,  # Cool bearings
            'roll_wear': 0.05,  # Low wear
            'oil_pressure': 50  # Good pressure
        }
        
        risk_score_low = self.model._assess_failure_risk('rolling_mill', low_risk_rolling)
        self.assertLess(risk_score_low, risk_score)  # Should be lower risk
    
    def test_rul_estimation_logic(self):
        """Test remaining useful life estimation"""
        # Test equipment with high wear
        high_wear_data = {
            'operating_hours': 7000,
            'vibration_level': 8.0,
            'roll_wear': 0.4,
            'bearing_temperature': 80,
            'refractory_condition': 70
        }
        
        rul = self.model._estimate_rul('rolling_mill', high_wear_data)
        self.assertLess(rul, 180)  # Should have short RUL
        
        # Test equipment with low wear
        low_wear_data = {
            'operating_hours': 1000,
            'vibration_level': 2.5,
            'roll_wear': 0.1,
            'bearing_temperature': 55,
            'refractory_condition': 95
        }
        
        rul_high = self.model._estimate_rul('rolling_mill', low_wear_data)
        self.assertGreater(rul_high, rul)  # Should have longer RUL
    
    def test_model_training(self):
        """Test predictive maintenance model training"""
        # Train the model
        results = self.model.train(self.training_data, self.target_columns)
        
        # Check that training completed
        self.assertTrue(self.model.models_trained)
        
        # Check training results for each equipment type
        for equipment_type in ['rolling_mill', 'heat_treatment', 'cutting_straightening']:
            self.assertIn(f'{equipment_type}_failure_accuracy', results)
            self.assertIn(f'{equipment_type}_rul_r2', results)
            
            # Validate accuracy targets
            failure_accuracy = results[f'{equipment_type}_failure_accuracy']
            rul_r2 = results[f'{equipment_type}_rul_r2']
            
            # Minimum acceptable performance
            self.assertGreaterEqual(failure_accuracy, 0.7)
            self.assertGreaterEqual(rul_r2, 0.5)  # RUL is harder to predict
        
        # Check feature importance is calculated
        for equipment_type in ['rolling_mill', 'heat_treatment', 'cutting_straightening']:
            self.assertIn(f'{equipment_type}_feature_importance', results)
    
    def test_single_maintenance_prediction(self):
        """Test single equipment maintenance prediction"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test rolling mill prediction
        prediction = self.model.predict_maintenance(self.test_rolling_mill_data)
        
        # Check prediction structure
        required_keys = [
            'equipment_type', 'failure_probability', 'risk_level',
            'estimated_rul_days', 'recommended_maintenance_type',
            'maintenance_priority', 'failure_indicators',
            'maintenance_actions', 'cost_impact'
        ]
        
        for key in required_keys:
            self.assertIn(key, prediction)
        
        # Validate prediction types and ranges
        self.assertEqual(prediction['equipment_type'], 'rolling_mill')
        self.assertTrue(0 <= prediction['failure_probability'] <= 1)
        self.assertIn(prediction['risk_level'], ['low', 'medium', 'high', 'critical'])
        self.assertGreaterEqual(prediction['estimated_rul_days'], 0)
        self.assertIn(prediction['recommended_maintenance_type'], 
                     ['preventive', 'predictive', 'corrective', 'overhaul'])
        self.assertIsInstance(prediction['failure_indicators'], list)
        self.assertIsInstance(prediction['maintenance_actions'], list)
    
    def test_batch_maintenance_prediction(self):
        """Test batch maintenance prediction"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Create batch test data with mixed equipment types
        batch_data = pd.DataFrame([
            self.test_rolling_mill_data,
            self.test_heat_treatment_data,
            self.test_cutting_data
        ] * 10)  # 30 total records
        
        # Add some variation
        batch_data['operating_hours'] = np.random.normal(3000, 1000, 30).clip(0, 10000)
        batch_data['vibration_level'] = np.random.normal(5.0, 2.0, 30).clip(0, 20)
        
        # Test batch prediction
        results = self.model.batch_predict(batch_data)
        
        # Check results structure
        self.assertEqual(len(results), 30)
        self.assertIn('failure_probability', results.columns)
        self.assertIn('estimated_rul_days', results.columns)
        self.assertIn('risk_level', results.columns)
        
        # Validate batch results
        self.assertTrue(results['failure_probability'].between(0, 1).all())
        self.assertTrue(results['estimated_rul_days'].ge(0).all())
    
    def test_equipment_specific_predictions(self):
        """Test predictions for each equipment type"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test each equipment type
        test_data_sets = [
            (self.test_rolling_mill_data, 'rolling_mill'),
            (self.test_heat_treatment_data, 'heat_treatment'),
            (self.test_cutting_data, 'cutting_straightening')
        ]
        
        for test_data, expected_type in test_data_sets:
            prediction = self.model.predict_maintenance(test_data)
            
            # Should correctly identify equipment type
            self.assertEqual(prediction['equipment_type'], expected_type)
            
            # Should have equipment-specific failure indicators
            indicators = prediction['failure_indicators']
            self.assertGreater(len(indicators), 0)
            
            # Should have appropriate maintenance actions
            actions = prediction['maintenance_actions']
            self.assertGreater(len(actions), 0)
    
    def test_maintenance_priority_calculation(self):
        """Test maintenance priority scoring"""
        # Test critical priority conditions
        critical_data = {
            **self.test_rolling_mill_data,
            'operating_hours': 9000,  # Very high
            'vibration_level': 15.0,  # Very high
            'bearing_temperature': 90,  # Very hot
            'roll_wear': 0.8  # Extreme wear
        }
        
        priority = self.model._calculate_maintenance_priority(
            equipment_type='rolling_mill',
            failure_prob=0.9,
            rul_days=5,
            equipment_data=critical_data
        )
        
        self.assertGreaterEqual(priority, 90)  # Should be very high priority
        
        # Test low priority conditions
        low_priority_data = {
            **self.test_rolling_mill_data,
            'operating_hours': 500,
            'vibration_level': 2.0,
            'bearing_temperature': 50,
            'roll_wear': 0.05
        }
        
        priority_low = self.model._calculate_maintenance_priority(
            equipment_type='rolling_mill',
            failure_prob=0.1,
            rul_days=300,
            equipment_data=low_priority_data
        )
        
        self.assertLess(priority_low, priority)  # Should be much lower priority
    
    def test_failure_indicator_identification(self):
        """Test failure indicator identification"""
        # Test with multiple failure indicators
        failing_equipment = {
            'operating_hours': 8500,  # High
            'vibration_level': 12.0,  # High
            'temperature': 95,  # High
            'bearing_temperature': 88,  # Very high
            'oil_pressure': 20,  # Low
            'roll_wear': 0.6  # High
        }
        
        indicators = self.model._identify_failure_indicators('rolling_mill', failing_equipment)
        
        # Should identify multiple indicators
        self.assertGreater(len(indicators), 2)
        
        # Should identify specific issues
        indicator_text = ' '.join(indicators).lower()
        self.assertTrue(any(term in indicator_text for term in 
                          ['vibration', 'temperature', 'wear', 'pressure']))
    
    def test_maintenance_action_recommendations(self):
        """Test maintenance action recommendations"""
        # Test different maintenance types
        maintenance_types = ['preventive', 'predictive', 'corrective', 'overhaul']
        
        for maintenance_type in maintenance_types:
            actions = self.model._recommend_maintenance_actions(
                equipment_type='rolling_mill',
                maintenance_type=maintenance_type,
                failure_indicators=['High vibration', 'Bearing temperature'],
                priority=85
            )
            
            self.assertIsInstance(actions, list)
            self.assertGreater(len(actions), 0)
            
            # Actions should be relevant to equipment and maintenance type
            action_text = ' '.join(actions).lower()
            if maintenance_type == 'preventive':
                self.assertTrue(any(term in action_text for term in 
                              ['inspect', 'lubricate', 'check', 'replace']))
            elif maintenance_type == 'corrective':
                self.assertTrue(any(term in action_text for term in 
                              ['repair', 'replace', 'adjust', 'rebuild']))
    
    def test_cost_impact_estimation(self):
        """Test maintenance cost impact estimation"""
        # Test different scenarios
        scenarios = [
            ('preventive', 30, 0.2),    # Preventive, 30 days RUL, low failure prob
            ('corrective', 5, 0.9),     # Corrective, 5 days RUL, high failure prob
            ('overhaul', 120, 0.1),     # Overhaul, 120 days RUL, low failure prob
            ('predictive', 60, 0.5)     # Predictive, 60 days RUL, medium failure prob
        ]
        
        cost_impacts = []
        for maintenance_type, rul_days, failure_prob in scenarios:
            cost_impact = self.model._estimate_cost_impact(
                equipment_type='rolling_mill',
                maintenance_type=maintenance_type,
                rul_days=rul_days,
                failure_probability=failure_prob
            )
            
            cost_impacts.append(cost_impact)
            self.assertGreater(cost_impact, 0)  # Should have positive cost
        
        # Emergency corrective maintenance should be most expensive
        corrective_cost = cost_impacts[1]
        preventive_cost = cost_impacts[0]
        self.assertGreater(corrective_cost, preventive_cost)
    
    def test_risk_level_classification(self):
        """Test risk level classification"""
        # Test different risk scenarios
        risk_scenarios = [
            (0.1, 200, 'low'),
            (0.4, 100, 'medium'),
            (0.7, 30, 'high'),
            (0.9, 5, 'critical')
        ]
        
        for failure_prob, rul_days, expected_risk in risk_scenarios:
            risk_level = self.model._classify_risk_level(failure_prob, rul_days)
            self.assertEqual(risk_level, expected_risk)
    
    def test_maintenance_scheduling(self):
        """Test maintenance scheduling recommendations"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test scheduling for different equipment
        equipment_list = [
            self.test_rolling_mill_data,
            self.test_heat_treatment_data,
            self.test_cutting_data
        ]
        
        schedule = self.model.generate_maintenance_schedule(equipment_list)
        
        # Check schedule structure
        self.assertIsInstance(schedule, list)
        self.assertEqual(len(schedule), 3)
        
        # Check schedule sorting (should be by priority)
        priorities = [item['maintenance_priority'] for item in schedule]
        self.assertEqual(priorities, sorted(priorities, reverse=True))
        
        # Check required schedule fields
        for item in schedule:
            required_fields = [
                'equipment_id', 'equipment_type', 'maintenance_priority',
                'recommended_date', 'maintenance_type', 'estimated_duration'
            ]
            for field in required_fields:
                self.assertIn(field, item)
    
    def test_edge_cases(self):
        """Test model behavior with edge cases"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test with extreme operating hours
        extreme_hours_data = {
            **self.test_rolling_mill_data,
            'operating_hours': 15000  # Very high
        }
        
        # Should not crash
        prediction = self.model.predict_maintenance(extreme_hours_data)
        self.assertIn('failure_probability', prediction)
        
        # Test with zero values
        zero_data = {key: 0.0 if isinstance(val, (int, float)) else val 
                    for key, val in self.test_rolling_mill_data.items()}
        
        prediction = self.model.predict_maintenance(zero_data)
        self.assertIn('failure_probability', prediction)
        
        # Test with unknown equipment type
        unknown_equipment = {
            **self.test_rolling_mill_data,
            'equipment_type': 'unknown_equipment'
        }
        
        # Should handle gracefully
        prediction = self.model.predict_maintenance(unknown_equipment)
        self.assertIn('failure_probability', prediction)
    
    def test_performance_requirements(self):
        """Test that model meets performance requirements"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Test single prediction performance
        start_time = time.time()
        prediction = self.model.predict_maintenance(self.test_rolling_mill_data)
        single_prediction_time = (time.time() - start_time) * 1000  # Convert to ms
        
        # Should meet response time target
        max_response_time = self.config['response_time_ms']
        self.assertLess(single_prediction_time, max_response_time,
                       f"Single prediction took {single_prediction_time:.2f}ms, target is {max_response_time}ms")
        
        # Test batch prediction performance
        batch_data = pd.DataFrame([self.test_rolling_mill_data] * 50)
        
        start_time = time.time()
        batch_results = self.model.batch_predict(batch_data)
        batch_prediction_time = (time.time() - start_time) * 1000
        
        # Batch should be efficient
        avg_per_item = batch_prediction_time / 50
        self.assertLess(avg_per_item, 100,  # 100ms per item in batch
                       f"Batch prediction took {avg_per_item:.2f}ms per item")
    
    def test_false_positive_rate(self):
        """Test false positive rate meets requirements"""
        # Train model first
        self.model.train(self.training_data, self.target_columns)
        
        # Create test data with known good equipment
        good_equipment_data = []
        for _ in range(100):
            good_data = {
                **self.test_rolling_mill_data,
                'operating_hours': np.random.uniform(100, 2000),  # Low hours
                'vibration_level': np.random.uniform(1.0, 3.0),   # Low vibration
                'temperature': np.random.uniform(40, 60),          # Normal temp
                'bearing_temperature': np.random.uniform(40, 55),  # Cool bearings
                'roll_wear': np.random.uniform(0.01, 0.1)          # Low wear
            }
            good_equipment_data.append(good_data)
        
        # Predict maintenance for good equipment
        false_positives = 0
        for equipment_data in good_equipment_data:
            prediction = self.model.predict_maintenance(equipment_data)
            
            # Count high-risk predictions as false positives for good equipment
            if prediction['risk_level'] in ['high', 'critical']:
                false_positives += 1
        
        false_positive_rate = false_positives / len(good_equipment_data)
        max_false_positive_rate = self.config['false_positive_rate']
        
        self.assertLessEqual(false_positive_rate, max_false_positive_rate,
                           f"False positive rate {false_positive_rate:.3f} exceeds target {max_false_positive_rate}")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)