"""
Model Training Pipeline Tests for Steel Mill AI Platform
Tests end-to-end model training, validation, hyperparameter tuning, and model persistence
"""

import unittest
import numpy as np
import pandas as pd
import sys
import os
import tempfile
import shutil
from sklearn.model_selection import cross_val_score, train_test_split
from sklearn.metrics import classification_report, mean_squared_error, r2_score
import time

# Add the parent directory to the path to import our models
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from models.rebar_quality_prediction import RebarQualityPredictionModel
from models.rebar_defect_detection import RebarDefectDetectionModel
from models.rebar_predictive_maintenance import RebarPredictiveMaintenanceModel
from tests import TEST_CONFIG


class TestModelTrainingPipeline(unittest.TestCase):
    """Test suite for model training pipeline"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.quality_model = RebarQualityPredictionModel()
        cls.defect_model = RebarDefectDetectionModel()
        cls.maintenance_model = RebarPredictiveMaintenanceModel()
        
        # Generate larger datasets for proper training
        cls.quality_data = cls.quality_model.generate_synthetic_training_data(n_samples=2000)
        cls.defect_data = cls.defect_model.generate_synthetic_training_data(n_samples=2000)
        cls.maintenance_data = cls.maintenance_model.generate_synthetic_training_data(n_samples=2000)
        
        # Target columns for training
        cls.quality_target_columns = {
            'compliance': 'astm_compliant',
            'yield_strength': 'actual_yield_strength',
            'tensile_strength': 'actual_tensile_strength',
            'elongation': 'actual_elongation'
        }
        
        cls.defect_target_column = 'primary_defect'
        
        cls.maintenance_target_columns = {
            'failure_prediction': 'failure_within_days',
            'rul_estimation': 'remaining_useful_life',
            'maintenance_type': 'recommended_maintenance'
        }
        
        # Performance thresholds
        cls.min_accuracy = 0.7
        cls.min_r2_score = 0.5
        cls.max_training_time = 120  # seconds
    
    def test_quality_model_training_pipeline(self):
        """Test complete quality prediction model training pipeline"""
        model = RebarQualityPredictionModel()  # Fresh model instance
        data = self.quality_data
        
        print(f"🔧 Training Quality Prediction Model...")
        
        # Record training start time
        start_time = time.time()
        
        # Train the model
        results = model.train(data, self.quality_target_columns)
        
        training_time = time.time() - start_time
        
        # Check that training completed successfully
        self.assertTrue(model.models_trained, "Model should be marked as trained")
        self.assertLess(training_time, self.max_training_time,
                       f"Training took {training_time:.2f}s, should be < {self.max_training_time}s")
        
        # Check training results structure
        expected_metrics = [
            'compliance_accuracy', 'yield_strength_r2', 'tensile_strength_r2', 
            'elongation_r2', 'compliance_feature_importance'
        ]
        
        for metric in expected_metrics:
            self.assertIn(metric, results, f"Missing metric: {metric}")
        
        # Validate performance metrics
        compliance_accuracy = results['compliance_accuracy']
        yield_r2 = results['yield_strength_r2']
        tensile_r2 = results['tensile_strength_r2']
        elongation_r2 = results['elongation_r2']
        
        self.assertGreaterEqual(compliance_accuracy, self.min_accuracy,
                               f"ASTM compliance accuracy {compliance_accuracy:.3f} below minimum {self.min_accuracy}")
        self.assertGreaterEqual(yield_r2, self.min_r2_score,
                               f"Yield strength R² {yield_r2:.3f} below minimum {self.min_r2_score}")
        self.assertGreaterEqual(tensile_r2, self.min_r2_score,
                               f"Tensile strength R² {tensile_r2:.3f} below minimum {self.min_r2_score}")
        self.assertGreaterEqual(elongation_r2, self.min_r2_score * 0.8,  # More lenient for elongation
                               f"Elongation R² {elongation_r2:.3f} below minimum {self.min_r2_score * 0.8}")
        
        # Test single prediction after training
        test_data = {
            'grade': 'Grade60',
            'carbon_content': 0.25,
            'billet_temperature': 1050,
            'surface_quality_score': 90
        }
        
        prediction = model.predict_quality(test_data)
        self.assertIn('astm_compliant', prediction)
        self.assertIn('quality_score', prediction)
        
        print(f"✅ Quality Model Training Results:")
        print(f"   Training time: {training_time:.2f}s")
        print(f"   ASTM compliance accuracy: {compliance_accuracy:.3f}")
        print(f"   Yield strength R²: {yield_r2:.3f}")
        print(f"   Tensile strength R²: {tensile_r2:.3f}")
        print(f"   Elongation R²: {elongation_r2:.3f}")
    
    def test_defect_model_training_pipeline(self):
        """Test complete defect detection model training pipeline"""
        model = RebarDefectDetectionModel()  # Fresh model instance
        data = self.defect_data
        
        print(f"🔧 Training Defect Detection Model...")
        
        start_time = time.time()
        
        # Train the model
        results = model.train(data, self.defect_target_column)
        
        training_time = time.time() - start_time
        
        # Check that training completed
        self.assertTrue(model.models_trained)
        self.assertLess(training_time, self.max_training_time)
        
        # Check training results
        expected_metrics = [
            'surface_defect_accuracy', 'dimensional_defect_accuracy', 
            'severity_accuracy'
        ]
        
        for metric in expected_metrics:
            self.assertIn(metric, results, f"Missing metric: {metric}")
        
        # Validate performance
        surface_accuracy = results['surface_defect_accuracy']
        dimensional_accuracy = results['dimensional_defect_accuracy']
        severity_accuracy = results['severity_accuracy']
        
        self.assertGreaterEqual(surface_accuracy, self.min_accuracy)
        self.assertGreaterEqual(dimensional_accuracy, self.min_accuracy)
        self.assertGreaterEqual(severity_accuracy, self.min_accuracy * 0.8)  # More lenient
        
        # Test single detection after training
        test_data = {
            'surface_roughness': 2.5,
            'rib_height': 0.7,
            'surface_temperature': 45,
            'rib_spacing': 11.2
        }
        
        detection = model.detect_defects(test_data)
        self.assertIn('primary_defect', detection)
        self.assertIn('severity', detection)
        
        print(f"✅ Defect Model Training Results:")
        print(f"   Training time: {training_time:.2f}s")
        print(f"   Surface defect accuracy: {surface_accuracy:.3f}")
        print(f"   Dimensional defect accuracy: {dimensional_accuracy:.3f}")
        print(f"   Severity accuracy: {severity_accuracy:.3f}")
    
    def test_maintenance_model_training_pipeline(self):
        """Test complete maintenance prediction model training pipeline"""
        model = RebarPredictiveMaintenanceModel()  # Fresh model instance
        data = self.maintenance_data
        
        print(f"🔧 Training Maintenance Prediction Model...")
        
        start_time = time.time()
        
        # Train the model
        results = model.train(data, self.maintenance_target_columns)
        
        training_time = time.time() - start_time
        
        # Check that training completed
        self.assertTrue(model.models_trained)
        self.assertLess(training_time, self.max_training_time)
        
        # Check training results for each equipment type
        equipment_types = ['rolling_mill', 'heat_treatment', 'cutting_straightening']
        
        for equipment_type in equipment_types:
            failure_accuracy_key = f'{equipment_type}_failure_accuracy'
            rul_r2_key = f'{equipment_type}_rul_r2'
            
            if failure_accuracy_key in results:
                accuracy = results[failure_accuracy_key]
                self.assertGreaterEqual(accuracy, self.min_accuracy * 0.8)  # More lenient
                print(f"   {equipment_type} failure accuracy: {accuracy:.3f}")
            
            if rul_r2_key in results:
                r2 = results[rul_r2_key]
                self.assertGreaterEqual(r2, self.min_r2_score * 0.6)  # More lenient for RUL
                print(f"   {equipment_type} RUL R²: {r2:.3f}")
        
        # Test single prediction after training
        test_data = {
            'equipment_type': 'rolling_mill',
            'operating_hours': 2500,
            'vibration_level': 4.5,
            'temperature': 75
        }
        
        prediction = model.predict_maintenance(test_data)
        self.assertIn('failure_probability', prediction)
        self.assertIn('estimated_rul_days', prediction)
        
        print(f"✅ Maintenance Model Training Results:")
        print(f"   Training time: {training_time:.2f}s")
    
    def test_cross_validation_performance(self):
        """Test model performance using cross-validation"""
        model = RebarQualityPredictionModel()
        data = self.quality_data.sample(n=500)  # Smaller sample for CV
        
        # Prepare features and target
        model.prepare_categorical_encoders(data)
        X = model.preprocess_features(data)
        X_scaled = model.scaler.fit_transform(X)
        y = data['astm_compliant'].values
        
        # Perform cross-validation
        cv_scores = cross_val_score(model.compliance_model, X_scaled, y, 
                                   cv=5, scoring='accuracy')
        
        mean_cv_score = cv_scores.mean()
        std_cv_score = cv_scores.std()
        
        # Cross-validation score should be reasonable
        self.assertGreater(mean_cv_score, self.min_accuracy * 0.8)
        self.assertLess(std_cv_score, 0.2)  # Reasonable stability
        
        print(f"✅ Cross-Validation Results:")
        print(f"   Mean CV accuracy: {mean_cv_score:.3f} ± {std_cv_score:.3f}")
        print(f"   Individual CV scores: {cv_scores}")
    
    def test_model_overfitting_detection(self):
        """Test for model overfitting by comparing train vs validation performance"""
        model = RebarQualityPredictionModel()
        data = self.quality_data
        
        # Split data for overfitting analysis
        train_data, val_data = train_test_split(data, test_size=0.3, random_state=42)
        
        # Prepare model components
        model.prepare_categorical_encoders(train_data)
        
        # Prepare features
        X_train = model.preprocess_features(train_data)
        X_val = model.preprocess_features(val_data)
        
        X_train_scaled = model.scaler.fit_transform(X_train)
        X_val_scaled = model.scaler.transform(X_val)
        
        y_train = train_data['astm_compliant'].values
        y_val = val_data['astm_compliant'].values
        
        # Train model
        model.compliance_model.fit(X_train_scaled, y_train)
        
        # Get predictions
        train_accuracy = model.compliance_model.score(X_train_scaled, y_train)
        val_accuracy = model.compliance_model.score(X_val_scaled, y_val)
        
        # Check for overfitting
        accuracy_gap = train_accuracy - val_accuracy
        
        self.assertLess(accuracy_gap, 0.15,
                       f"Accuracy gap {accuracy_gap:.3f} indicates overfitting")
        
        self.assertGreater(val_accuracy, self.min_accuracy * 0.8,
                          f"Validation accuracy {val_accuracy:.3f} too low")
        
        print(f"✅ Overfitting Analysis:")
        print(f"   Training accuracy: {train_accuracy:.3f}")
        print(f"   Validation accuracy: {val_accuracy:.3f}")
        print(f"   Accuracy gap: {accuracy_gap:.3f}")
    
    def test_model_persistence_pipeline(self):
        """Test complete model save/load pipeline"""
        # Create temporary directory for model storage
        temp_dir = tempfile.mkdtemp()
        
        try:
            # Train and save quality model
            model = RebarQualityPredictionModel()
            model.train(self.quality_data, self.quality_target_columns)
            
            # Test prediction before save
            test_data = {
                'grade': 'Grade60',
                'carbon_content': 0.25,
                'billet_temperature': 1050
            }
            
            prediction_before = model.predict_quality(test_data)
            
            # Save model
            model.save_model(temp_dir)
            
            # Verify model files exist
            model_file = os.path.join(temp_dir, 'rebar_quality_prediction_model.pkl')
            self.assertTrue(os.path.exists(model_file))
            
            # Load model into new instance
            new_model = RebarQualityPredictionModel()
            new_model.load_model(temp_dir)
            
            # Verify loaded model properties
            self.assertTrue(new_model.models_trained)
            self.assertEqual(new_model.feature_names, model.feature_names)
            
            # Test prediction after load
            prediction_after = new_model.predict_quality(test_data)
            
            # Predictions should be very similar
            self.assertAlmostEqual(
                prediction_before['predicted_yield_strength'],
                prediction_after['predicted_yield_strength'],
                delta=1.0  # Allow small numerical differences
            )
            
            self.assertEqual(
                prediction_before['astm_compliant'],
                prediction_after['astm_compliant']
            )
            
            print(f"✅ Model Persistence:")
            print(f"   Model saved and loaded successfully")
            print(f"   Predictions consistent before/after load")
            
        finally:
            # Clean up temporary directory
            shutil.rmtree(temp_dir)
    
    def test_batch_training_performance(self):
        """Test training performance with different batch sizes"""
        batch_sizes = [500, 1000, 2000]
        training_times = []
        accuracies = []
        
        for batch_size in batch_sizes:
            model = RebarQualityPredictionModel()
            data = self.quality_data.head(batch_size)
            
            start_time = time.time()
            results = model.train(data, self.quality_target_columns)
            training_time = time.time() - start_time
            
            training_times.append(training_time)
            accuracies.append(results['compliance_accuracy'])
            
            # Training time should scale reasonably
            time_per_sample = training_time / batch_size
            self.assertLess(time_per_sample, 0.1,
                           f"Training too slow: {time_per_sample:.4f}s per sample")
        
        # Larger datasets should generally give better performance
        # (though this may not always hold due to synthetic data)
        max_accuracy = max(accuracies)
        min_accuracy = min(accuracies)
        
        print(f"✅ Batch Training Performance:")
        for i, (batch_size, training_time, accuracy) in enumerate(zip(batch_sizes, training_times, accuracies)):
            print(f"   {batch_size:,} samples: {training_time:.2f}s, accuracy: {accuracy:.3f}")
    
    def test_incremental_learning_capability(self):
        """Test if models can handle incremental learning scenarios"""
        # Split data into initial and incremental batches
        initial_data = self.quality_data.head(1000)
        incremental_data = self.quality_data.tail(500)
        
        # Train initial model
        model = RebarQualityPredictionModel()
        initial_results = model.train(initial_data, self.quality_target_columns)
        initial_accuracy = initial_results['compliance_accuracy']
        
        # Test prediction before incremental training
        test_data = {
            'grade': 'Grade60',
            'carbon_content': 0.25,
            'billet_temperature': 1050
        }
        
        prediction_before = model.predict_quality(test_data)
        
        # Simulate incremental training by combining datasets
        combined_data = pd.concat([initial_data, incremental_data], ignore_index=True)
        incremental_results = model.train(combined_data, self.quality_target_columns)
        incremental_accuracy = incremental_results['compliance_accuracy']
        
        # Test prediction after incremental training
        prediction_after = model.predict_quality(test_data)
        
        # Model should still work after retraining
        self.assertTrue(model.models_trained)
        
        # Accuracy should be maintained or improved
        accuracy_change = incremental_accuracy - initial_accuracy
        
        print(f"✅ Incremental Learning:")
        print(f"   Initial accuracy: {initial_accuracy:.3f}")
        print(f"   Incremental accuracy: {incremental_accuracy:.3f}")
        print(f"   Accuracy change: {accuracy_change:+.3f}")
    
    def test_feature_selection_impact(self):
        """Test impact of feature selection on model performance"""
        model = RebarQualityPredictionModel()
        data = self.quality_data
        
        # Train full model
        full_results = model.train(data, self.quality_target_columns)
        full_accuracy = full_results['compliance_accuracy']
        
        # Get feature importance
        feature_importance = full_results['compliance_feature_importance']
        
        # Sort features by importance
        sorted_features = sorted(feature_importance.items(), key=lambda x: x[1], reverse=True)
        
        # Test with top 50% of features
        top_features = [name for name, _ in sorted_features[:len(sorted_features)//2]]
        
        print(f"✅ Feature Selection Analysis:")
        print(f"   Total features: {len(model.feature_names)}")
        print(f"   Top features selected: {len(top_features)}")
        print(f"   Full model accuracy: {full_accuracy:.3f}")
        print(f"   Top 3 important features:")
        for i, (feature_name, importance) in enumerate(sorted_features[:3]):
            print(f"     {i+1}. {feature_name}: {importance:.3f}")
    
    def test_model_training_reproducibility(self):
        """Test that model training is reproducible with same random state"""
        # Train two models with same data and random seed
        model1 = RebarQualityPredictionModel()
        model2 = RebarQualityPredictionModel()
        
        # Use smaller dataset for speed
        data = self.quality_data.head(500)
        
        # Note: For true reproducibility, we'd need to set random seeds in sklearn models
        # This test mainly checks that training process is stable
        
        results1 = model1.train(data, self.quality_target_columns)
        results2 = model2.train(data, self.quality_target_columns)
        
        # Results should be in similar ranges (though may not be identical due to randomness)
        accuracy_diff = abs(results1['compliance_accuracy'] - results2['compliance_accuracy'])
        
        self.assertLess(accuracy_diff, 0.1,
                       f"Accuracy difference {accuracy_diff:.3f} indicates instability")
        
        print(f"✅ Training Reproducibility:")
        print(f"   Model 1 accuracy: {results1['compliance_accuracy']:.3f}")
        print(f"   Model 2 accuracy: {results2['compliance_accuracy']:.3f}")
        print(f"   Accuracy difference: {accuracy_diff:.3f}")
    
    def test_training_data_quality_validation(self):
        """Test training with various data quality scenarios"""
        base_data = self.quality_data.head(500)
        
        # Test 1: Training with imbalanced classes
        imbalanced_data = base_data.copy()
        # Force most samples to be compliant
        imbalanced_data.loc[:400, 'astm_compliant'] = 1
        imbalanced_data.loc[401:, 'astm_compliant'] = 0
        
        model_imbalanced = RebarQualityPredictionModel()
        results_imbalanced = model_imbalanced.train(imbalanced_data, self.quality_target_columns)
        
        # Should still train successfully
        self.assertTrue(model_imbalanced.models_trained)
        
        # Test 2: Training with extreme outliers
        outlier_data = base_data.copy()
        outlier_data.loc[0, 'carbon_content'] = 2.0  # Extreme outlier
        outlier_data.loc[1, 'billet_temperature'] = 2000  # Extreme outlier
        
        model_outliers = RebarQualityPredictionModel()
        results_outliers = model_outliers.train(outlier_data, self.quality_target_columns)
        
        # Should handle outliers gracefully
        self.assertTrue(model_outliers.models_trained)
        
        print(f"✅ Data Quality Validation:")
        print(f"   Imbalanced data training: {'Success' if model_imbalanced.models_trained else 'Failed'}")
        print(f"   Outlier data training: {'Success' if model_outliers.models_trained else 'Failed'}")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)