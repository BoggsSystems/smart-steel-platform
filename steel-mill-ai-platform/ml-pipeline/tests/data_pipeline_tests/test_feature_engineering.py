"""
Feature Engineering and Preprocessing Tests for Steel Mill AI Platform
Tests feature extraction, scaling, encoding, and data transformation pipelines
"""

import unittest
import numpy as np
import pandas as pd
import sys
import os
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.model_selection import train_test_split
import joblib

# Add the parent directory to the path to import our models
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from models.rebar_quality_prediction import RebarQualityPredictionModel
from models.rebar_defect_detection import RebarDefectDetectionModel
from models.rebar_predictive_maintenance import RebarPredictiveMaintenanceModel
from tests import TEST_CONFIG


class TestFeatureEngineering(unittest.TestCase):
    """Test suite for feature engineering and preprocessing"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.quality_model = RebarQualityPredictionModel()
        cls.defect_model = RebarDefectDetectionModel()
        cls.maintenance_model = RebarPredictiveMaintenanceModel()
        
        # Generate test datasets
        cls.quality_data = cls.quality_model.generate_synthetic_training_data(n_samples=1000)
        cls.defect_data = cls.defect_model.generate_synthetic_training_data(n_samples=1000)
        cls.maintenance_data = cls.maintenance_model.generate_synthetic_training_data(n_samples=1000)
    
    def test_quality_feature_preprocessing(self):
        """Test quality prediction feature preprocessing"""
        model = self.quality_model
        data = self.quality_data
        
        # Prepare categorical encoders
        model.prepare_categorical_encoders(data)
        
        # Test feature preprocessing
        features = model.preprocess_features(data)
        
        # Check feature matrix dimensions
        expected_features = len(model.feature_names)
        self.assertEqual(features.shape[1], expected_features,
                        f"Expected {expected_features} features, got {features.shape[1]}")
        self.assertEqual(features.shape[0], len(data),
                        f"Expected {len(data)} samples, got {features.shape[0]}")
        
        # Check for missing values
        self.assertFalse(np.isnan(features).any(),
                        "Preprocessed features should not contain NaN values")
        
        # Check for infinite values
        self.assertTrue(np.isfinite(features).all(),
                      "Preprocessed features should not contain infinite values")
        
        # Test feature value ranges (should be reasonable after preprocessing)
        for i, feature_name in enumerate(model.feature_names):
            feature_values = features[:, i]
            feature_std = np.std(feature_values)
            
            # Features should have some variation
            self.assertGreater(feature_std, 0,
                             f"Feature {feature_name} has no variation")
            
            # Features should not have extreme outliers (beyond 5 standard deviations)
            feature_mean = np.mean(feature_values)
            extreme_outliers = np.abs(feature_values - feature_mean) > 5 * feature_std
            outlier_rate = np.mean(extreme_outliers)
            
            self.assertLess(outlier_rate, 0.01,
                           f"Feature {feature_name} has {outlier_rate:.1%} extreme outliers")
        
        print(f"✅ Quality Feature Preprocessing:")
        print(f"   Input samples: {len(data)}")
        print(f"   Output features: {features.shape}")
        print(f"   Feature names: {len(model.feature_names)}")
    
    def test_quality_categorical_encoding(self):
        """Test categorical encoding for quality prediction"""
        model = self.quality_model
        data = self.quality_data
        
        # Test grade encoding
        model.prepare_categorical_encoders(data)
        
        # Test that all grades can be encoded
        test_grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
        for grade in test_grades:
            encoded_grade = model.grade_encoder.transform([grade])[0]
            self.assertIsInstance(encoded_grade, (int, np.integer))
            self.assertGreaterEqual(encoded_grade, 0)
            self.assertLess(encoded_grade, len(test_grades))
        
        # Test that encoding is consistent
        grade = 'Grade60'
        encoded1 = model.grade_encoder.transform([grade])[0]
        encoded2 = model.grade_encoder.transform([grade])[0]
        self.assertEqual(encoded1, encoded2)
        
        # Test size encoding
        test_sizes = ['Size3', 'Size5', 'Size8', 'Size10']
        for size in test_sizes:
            encoded_size = model.size_encoder.transform([size])[0]
            self.assertIsInstance(encoded_size, (int, np.integer))
            self.assertGreaterEqual(encoded_size, 0)
        
        print(f"✅ Categorical Encoding:")
        print(f"   Grade classes: {len(model.grade_encoder.classes_)}")
        print(f"   Size classes: {len(model.size_encoder.classes_)}")
    
    def test_quality_feature_scaling(self):
        """Test feature scaling for quality prediction"""
        model = self.quality_model
        data = self.quality_data
        
        # Prepare encoders and preprocess features
        model.prepare_categorical_encoders(data)
        features = model.preprocess_features(data)
        
        # Fit scaler
        scaled_features = model.scaler.fit_transform(features)
        
        # Check scaling properties
        self.assertEqual(scaled_features.shape, features.shape)
        
        # Scaled features should have approximately zero mean and unit variance
        feature_means = np.mean(scaled_features, axis=0)
        feature_stds = np.std(scaled_features, axis=0)
        
        # Test that means are close to zero
        for i, mean in enumerate(feature_means):
            self.assertAlmostEqual(mean, 0, places=1,
                                 msg=f"Feature {i} mean {mean:.3f} not close to 0")
        
        # Test that standard deviations are close to one
        for i, std in enumerate(feature_stds):
            self.assertAlmostEqual(std, 1, places=1,
                                 msg=f"Feature {i} std {std:.3f} not close to 1")
        
        # Test that scaling is invertible
        inverse_features = model.scaler.inverse_transform(scaled_features)
        np.testing.assert_array_almost_equal(features, inverse_features, decimal=5)
        
        print(f"✅ Feature Scaling:")
        print(f"   Mean range: [{feature_means.min():.3f}, {feature_means.max():.3f}]")
        print(f"   Std range: [{feature_stds.min():.3f}, {feature_stds.max():.3f}]")
    
    def test_defect_feature_extraction(self):
        """Test defect detection feature extraction"""
        model = self.defect_model
        data = self.defect_data
        
        # Test surface feature extraction
        surface_features = model.extract_surface_features(data)
        
        expected_surface_features = len(model.surface_feature_names)
        self.assertEqual(surface_features.shape[1], expected_surface_features)
        self.assertEqual(surface_features.shape[0], len(data))
        
        # Check for valid numeric values
        self.assertFalse(np.isnan(surface_features).any())
        self.assertTrue(np.isfinite(surface_features).all())
        
        # Test dimensional feature extraction
        dimensional_features = model.extract_dimensional_features(data)
        
        expected_dimensional_features = len(model.dimensional_feature_names)
        self.assertEqual(dimensional_features.shape[1], expected_dimensional_features)
        self.assertEqual(dimensional_features.shape[0], len(data))
        
        # Check for valid numeric values
        self.assertFalse(np.isnan(dimensional_features).any())
        self.assertTrue(np.isfinite(dimensional_features).all())
        
        print(f"✅ Defect Feature Extraction:")
        print(f"   Surface features: {surface_features.shape}")
        print(f"   Dimensional features: {dimensional_features.shape}")
        print(f"   Surface feature names: {len(model.surface_feature_names)}")
        print(f"   Dimensional feature names: {len(model.dimensional_feature_names)}")
    
    def test_defect_feature_scaling(self):
        """Test defect detection feature scaling"""
        model = self.defect_model
        data = self.defect_data
        
        # Extract and scale surface features
        surface_features = model.extract_surface_features(data)
        scaled_surface = model.surface_scaler.fit_transform(surface_features)
        
        # Check scaling properties for surface features
        surface_means = np.mean(scaled_surface, axis=0)
        surface_stds = np.std(scaled_surface, axis=0)
        
        for i, (mean, std) in enumerate(zip(surface_means, surface_stds)):
            self.assertAlmostEqual(mean, 0, places=1,
                                 msg=f"Surface feature {i} mean {mean:.3f} not close to 0")
            self.assertAlmostEqual(std, 1, places=1,
                                 msg=f"Surface feature {i} std {std:.3f} not close to 1")
        
        # Extract and scale dimensional features
        dimensional_features = model.extract_dimensional_features(data)
        scaled_dimensional = model.dimensional_scaler.fit_transform(dimensional_features)
        
        # Check scaling properties for dimensional features
        dimensional_means = np.mean(scaled_dimensional, axis=0)
        dimensional_stds = np.std(scaled_dimensional, axis=0)
        
        for i, (mean, std) in enumerate(zip(dimensional_means, dimensional_stds)):
            self.assertAlmostEqual(mean, 0, places=1,
                                 msg=f"Dimensional feature {i} mean {mean:.3f} not close to 0")
            self.assertAlmostEqual(std, 1, places=1,
                                 msg=f"Dimensional feature {i} std {std:.3f} not close to 1")
        
        print(f"✅ Defect Feature Scaling:")
        print(f"   Surface features scaled: {scaled_surface.shape}")
        print(f"   Dimensional features scaled: {scaled_dimensional.shape}")
    
    def test_maintenance_feature_extraction(self):
        """Test maintenance prediction feature extraction"""
        model = self.maintenance_model
        data = self.maintenance_data
        
        # Test equipment-specific feature extraction
        equipment_types = data['equipment_type'].unique()
        
        for equipment_type in equipment_types:
            if equipment_type in model.equipment_features:
                equipment_data = data[data['equipment_type'] == equipment_type].head(10)
                
                features = model.extract_equipment_features(equipment_data, equipment_type)
                
                expected_features = len(model.equipment_features[equipment_type])
                self.assertEqual(features.shape[1], expected_features,
                               f"{equipment_type} should have {expected_features} features")
                self.assertEqual(features.shape[0], len(equipment_data))
                
                # Check for valid numeric values
                self.assertFalse(np.isnan(features).any(),
                               f"{equipment_type} features contain NaN values")
                self.assertTrue(np.isfinite(features).all(),
                              f"{equipment_type} features contain infinite values")
                
                print(f"✅ {equipment_type} Features: {features.shape}")
    
    def test_maintenance_equipment_specific_scaling(self):
        """Test equipment-specific feature scaling for maintenance"""
        model = self.maintenance_model
        data = self.maintenance_data
        
        # Test scaling for each equipment type
        for equipment_type in ['rolling_mill', 'heat_treatment', 'cutting_straightening']:
            if equipment_type in model.equipment_models:
                equipment_data = data[data['equipment_type'] == equipment_type].head(100)
                
                if len(equipment_data) > 10:  # Only test if we have enough samples
                    features = model.extract_equipment_features(equipment_data, equipment_type)
                    scaler = model.equipment_models[equipment_type]['scaler']
                    
                    scaled_features = scaler.fit_transform(features)
                    
                    # Check scaling properties
                    feature_means = np.mean(scaled_features, axis=0)
                    feature_stds = np.std(scaled_features, axis=0)
                    
                    # Most features should be approximately normalized
                    mean_close_to_zero = np.abs(feature_means) < 0.2
                    std_close_to_one = np.abs(feature_stds - 1) < 0.2
                    
                    self.assertTrue(np.mean(mean_close_to_zero) > 0.8,
                                   f"{equipment_type} features not properly centered")
                    self.assertTrue(np.mean(std_close_to_one) > 0.8,
                                   f"{equipment_type} features not properly scaled")
                    
                    print(f"✅ {equipment_type} Scaling:")
                    print(f"   Features properly centered: {np.mean(mean_close_to_zero):.1%}")
                    print(f"   Features properly scaled: {np.mean(std_close_to_one):.1%}")
    
    def test_missing_value_handling(self):
        """Test handling of missing values in feature preprocessing"""
        model = self.quality_model
        
        # Create test data with missing values
        test_data = pd.DataFrame({
            'grade': ['Grade60', 'Grade40', 'Grade80'],
            'carbon_content': [0.25, np.nan, 0.30],  # Missing value
            'billet_temperature': [1050, 1000, np.nan],  # Missing value
            'surface_quality_score': [90, 85, 92]
        })
        
        # Prepare encoders
        model.prepare_categorical_encoders(test_data)
        
        # Test preprocessing with missing values
        features = model.preprocess_features(test_data)
        
        # Should not contain NaN values (should be filled with defaults)
        self.assertFalse(np.isnan(features).any(),
                        "Features should not contain NaN after preprocessing")
        
        # Should have correct dimensions
        self.assertEqual(features.shape[0], len(test_data))
        self.assertEqual(features.shape[1], len(model.feature_names))
        
        print(f"✅ Missing Value Handling:")
        print(f"   Input with missing values: {test_data.isnull().sum().sum()}")
        print(f"   Output features shape: {features.shape}")
        print(f"   NaN values in output: {np.isnan(features).sum()}")
    
    def test_feature_importance_tracking(self):
        """Test that feature importance can be tracked"""
        model = self.quality_model
        data = self.quality_data
        
        # Train model to get feature importance
        target_columns = {
            'compliance': 'astm_compliant',
            'yield_strength': 'actual_yield_strength',
            'tensile_strength': 'actual_tensile_strength',
            'elongation': 'actual_elongation'
        }
        
        results = model.train(data, target_columns)
        
        # Check that feature importance is calculated
        self.assertIn('compliance_feature_importance', results)
        
        feature_importance = results['compliance_feature_importance']
        self.assertIsInstance(feature_importance, dict)
        self.assertEqual(len(feature_importance), len(model.feature_names))
        
        # Feature importance should sum to approximately 1.0
        importance_sum = sum(feature_importance.values())
        self.assertAlmostEqual(importance_sum, 1.0, places=2)
        
        # All importance values should be non-negative
        for feature_name, importance in feature_importance.items():
            self.assertGreaterEqual(importance, 0,
                                   f"Feature {feature_name} has negative importance")
        
        # Find top important features
        sorted_features = sorted(feature_importance.items(), key=lambda x: x[1], reverse=True)
        top_features = sorted_features[:5]
        
        print(f"✅ Feature Importance (Top 5):")
        for feature_name, importance in top_features:
            print(f"   {feature_name}: {importance:.3f}")
    
    def test_feature_correlation_analysis(self):
        """Test feature correlation analysis"""
        model = self.quality_model
        data = self.quality_data
        
        # Prepare and preprocess features
        model.prepare_categorical_encoders(data)
        features = model.preprocess_features(data)
        
        # Convert to DataFrame for correlation analysis
        feature_df = pd.DataFrame(features, columns=model.feature_names)
        
        # Calculate correlation matrix
        correlation_matrix = feature_df.corr()
        
        # Check correlation matrix properties
        self.assertEqual(correlation_matrix.shape, (len(model.feature_names), len(model.feature_names)))
        
        # Diagonal should be all ones
        diagonal_values = np.diag(correlation_matrix.values)
        np.testing.assert_array_almost_equal(diagonal_values, 1.0, decimal=10)
        
        # Matrix should be symmetric
        np.testing.assert_array_almost_equal(correlation_matrix.values, 
                                           correlation_matrix.values.T, decimal=10)
        
        # Find highly correlated feature pairs
        high_correlation_threshold = 0.8
        high_correlations = []
        
        for i in range(len(model.feature_names)):
            for j in range(i+1, len(model.feature_names)):
                correlation = correlation_matrix.iloc[i, j]
                if abs(correlation) > high_correlation_threshold:
                    high_correlations.append({
                        'feature1': model.feature_names[i],
                        'feature2': model.feature_names[j],
                        'correlation': correlation
                    })
        
        # Check for multicollinearity issues
        multicollinearity_rate = len(high_correlations) / (len(model.feature_names) * (len(model.feature_names) - 1) / 2)
        
        self.assertLess(multicollinearity_rate, 0.1,
                       f"High correlation rate {multicollinearity_rate:.1%} indicates multicollinearity")
        
        print(f"✅ Feature Correlation Analysis:")
        print(f"   Total feature pairs: {len(model.feature_names) * (len(model.feature_names) - 1) // 2}")
        print(f"   High correlations (>{high_correlation_threshold}): {len(high_correlations)}")
        print(f"   Multicollinearity rate: {multicollinearity_rate:.1%}")
        
        if high_correlations:
            print(f"   Top correlated pairs:")
            for corr_info in sorted(high_correlations, key=lambda x: abs(x['correlation']), reverse=True)[:3]:
                print(f"     {corr_info['feature1']} - {corr_info['feature2']}: {corr_info['correlation']:.3f}")
    
    def test_feature_pipeline_consistency(self):
        """Test that feature processing pipeline is consistent"""
        model = self.quality_model
        data = self.quality_data.head(100)  # Use smaller dataset for speed
        
        # Process features multiple times
        model.prepare_categorical_encoders(data)
        
        features1 = model.preprocess_features(data)
        features2 = model.preprocess_features(data)
        features3 = model.preprocess_features(data)
        
        # Results should be identical
        np.testing.assert_array_equal(features1, features2)
        np.testing.assert_array_equal(features2, features3)
        
        # Test with different data order
        shuffled_data = data.sample(frac=1.0, random_state=42).reset_index(drop=True)
        features_shuffled = model.preprocess_features(shuffled_data)
        
        # Should have same shape
        self.assertEqual(features_shuffled.shape, features1.shape)
        
        print(f"✅ Feature Pipeline Consistency:")
        print(f"   Multiple runs produce identical results")
        print(f"   Works with reordered data")
    
    def test_train_test_split_feature_consistency(self):
        """Test feature consistency across train/test splits"""
        model = self.quality_model
        data = self.quality_data
        
        # Split data
        train_data, test_data = train_test_split(data, test_size=0.2, random_state=42)
        
        # Prepare encoders on training data
        model.prepare_categorical_encoders(train_data)
        
        # Process both splits
        train_features = model.preprocess_features(train_data)
        test_features = model.preprocess_features(test_data)
        
        # Should have same number of features
        self.assertEqual(train_features.shape[1], test_features.shape[1])
        
        # Feature ranges should be similar (within reasonable bounds)
        for i in range(train_features.shape[1]):
            train_feature = train_features[:, i]
            test_feature = test_features[:, i]
            
            train_range = train_feature.max() - train_feature.min()
            test_range = test_feature.max() - test_feature.min()
            
            # Test features should not have extremely different ranges
            if train_range > 0:  # Avoid division by zero
                range_ratio = test_range / train_range
                self.assertTrue(0.1 <= range_ratio <= 10,
                               f"Feature {i} range ratio {range_ratio:.2f} indicates inconsistency")
        
        # Scale features
        scaled_train = model.scaler.fit_transform(train_features)
        scaled_test = model.scaler.transform(test_features)  # Use fitted scaler
        
        # Scaled test features should have reasonable statistics
        test_means = np.mean(scaled_test, axis=0)
        test_stds = np.std(scaled_test, axis=0)
        
        # Test set means should be close to zero (within 2 standard errors)
        mean_threshold = 2.0 / np.sqrt(len(test_data))
        extreme_means = np.abs(test_means) > mean_threshold
        
        self.assertLess(np.mean(extreme_means), 0.1,
                       "Too many test features have extreme means after scaling")
        
        print(f"✅ Train/Test Feature Consistency:")
        print(f"   Train samples: {train_features.shape[0]}")
        print(f"   Test samples: {test_features.shape[0]}")
        print(f"   Features with extreme test means: {np.mean(extreme_means):.1%}")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)